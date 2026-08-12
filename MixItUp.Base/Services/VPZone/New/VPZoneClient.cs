using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.User;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Model.VPZone;
using MixItUp.Base.Model.VPZone.Realtime;
using MixItUp.Base.Model.VPZone.Webhooks;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.Chat.VPZone;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.VPZone.New
{
    /// <summary>
    /// Routes everything VPZone pushes at the app. The chat gateway is the primary source and carries
    /// almost the whole catalog, so nearly all of this handles socket frames; the webhook relay covers
    /// only what the socket deliberately omits.
    /// </summary>
    public class VPZoneClient : ServiceClientBase
    {
        private const int MaxProcessedEventsCacheSize = 3000;

        public override bool IsConnected { get { return this.isConnected; } }
        private bool isConnected;

        /// <summary>
        /// Whether the streamer's chat gateway connection is up. The webhook relay carries the stream
        /// lifecycle as a backstop, so this gate keeps it from double-firing while the socket is live.
        /// </summary>
        public bool ChatSocketActive { get; set; }

        private readonly object processedEventIDsLock = new object();
        private readonly Queue<string> processedEventIDsQueue = new Queue<string>();
        private readonly HashSet<string> processedEventIDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Nonces the app sent on its own messages. VPZone echoes the nonce on the broadcast frame, so
        /// this identifies the app's own output without comparing message text.
        /// </summary>
        private readonly HashSet<string> sentMessageNonces = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Callers waiting to learn the id VPZone assigned to a message this client sent, keyed by the
        /// nonce they sent it under. Pinning needs this: the pin endpoint takes an existing message id,
        /// and the echoed nonce on the broadcast frame is the only thing tying a send to its id.
        /// </summary>
        private readonly Dictionary<string, TaskCompletionSource<string>> awaitedSentMessageIDs = new Dictionary<string, TaskCompletionSource<string>>(StringComparer.Ordinal);

        /// <summary>
        /// The message currently pinned in the channel, tracked off pin_update frames so an unpin does
        /// not have to ask VPZone what is pinned first. Null when nothing is pinned.
        /// </summary>
        public string PinnedMessageID { get; private set; }

        public override Task<Result> Connect()
        {
            this.isConnected = true;
            return Task.FromResult(new Result());
        }

        public override Task Disconnect()
        {
            this.isConnected = false;
            lock (this.processedEventIDsLock)
            {
                this.sentMessageNonces.Clear();
            }
            return Task.CompletedTask;
        }

        /// <summary>Records a nonce the app just sent so the resulting broadcast can be matched to it.</summary>
        public void TrackSentNonce(string nonce)
        {
            if (!string.IsNullOrWhiteSpace(nonce))
            {
                lock (this.processedEventIDsLock)
                {
                    this.sentMessageNonces.Add(nonce);

                    // The set only ever holds messages still in flight, so a hard cap keeps a long
                    // session from growing it without bound.
                    if (this.sentMessageNonces.Count > MaxProcessedEventsCacheSize)
                    {
                        this.sentMessageNonces.Clear();
                    }
                }
            }
        }

        public bool WasSentByThisClient(string nonce)
        {
            if (string.IsNullOrWhiteSpace(nonce))
            {
                return false;
            }

            lock (this.processedEventIDsLock)
            {
                return this.sentMessageNonces.Remove(nonce);
            }
        }

        /// <summary>
        /// Waits for the broadcast frame carrying this nonce and hands back the id VPZone gave the
        /// message. Returns null if the echo does not arrive in time, which is the same outcome as the
        /// send having failed outright as far as the caller is concerned.
        /// </summary>
        public async Task<string> AwaitSentMessageID(string nonce, TimeSpan timeout)
        {
            if (string.IsNullOrWhiteSpace(nonce))
            {
                return null;
            }

            TaskCompletionSource<string> completion = new TaskCompletionSource<string>();
            lock (this.processedEventIDsLock)
            {
                this.awaitedSentMessageIDs[nonce] = completion;
            }

            try
            {
                Task finished = await Task.WhenAny(completion.Task, Task.Delay(timeout));
                return finished == completion.Task ? completion.Task.Result : null;
            }
            finally
            {
                lock (this.processedEventIDsLock)
                {
                    this.awaitedSentMessageIDs.Remove(nonce);
                }
            }
        }

        private void ResolveSentMessageID(string nonce, string messageID)
        {
            if (string.IsNullOrWhiteSpace(nonce) || string.IsNullOrWhiteSpace(messageID))
            {
                return;
            }

            TaskCompletionSource<string> completion;
            lock (this.processedEventIDsLock)
            {
                if (!this.awaitedSentMessageIDs.TryGetValue(nonce, out completion))
                {
                    return;
                }
            }
            completion.TrySetResult(messageID);
        }

        // ===== Chat gateway frames =====

        /// <summary>
        /// Routes one gateway frame. Unrecognized frame types and metadata kinds are ignored on
        /// purpose: VPZone adds them without a version bump, and body always carries a readable
        /// fallback for anything a future release introduces.
        /// </summary>
        public async Task HandleFrame(VPZoneChatEventModel frame)
        {
            try
            {
                if (!this.IsConnected || frame == null || string.IsNullOrWhiteSpace(frame.Type))
                {
                    return;
                }

                // The gateway replays history on connect and on a since-cursor reconnect, so a frame
                // that was already handled has to be dropped rather than replayed into chat.
                if (!string.IsNullOrWhiteSpace(frame.ID) && !this.ShouldProcessEvent($"vpzone.frame:{frame.Type}:{frame.ID}"))
                {
                    return;
                }

                switch (frame.Type.ToLowerInvariant())
                {
                    case VPZoneFrameTypes.Message:
                        await this.HandleChatMessage(frame);
                        break;
                    case VPZoneFrameTypes.System:
                        await this.HandleSystemFrame(frame);
                        break;
                    case VPZoneFrameTypes.Presence:
                        this.HandlePresence(frame);
                        break;
                    case VPZoneFrameTypes.Follow:
                        await this.HandleFollow(frame);
                        break;
                    case VPZoneFrameTypes.Subscription:
                        await this.HandleSubscription(frame);
                        break;
                    case VPZoneFrameTypes.Gift:
                        await this.HandleSubscriptionGift(frame);
                        break;
                    case VPZoneFrameTypes.Raid:
                        await this.HandleRaid(frame);
                        break;
                    case VPZoneFrameTypes.Clip:
                        await this.HandleClip(frame);
                        break;
                    case VPZoneFrameTypes.Shoutout:
                        await this.HandleShoutout(frame);
                        break;
                    case VPZoneFrameTypes.DeleteMessage:
                        await this.HandleDeleteMessage(frame);
                        break;
                    case VPZoneFrameTypes.ClearChat:
                        await this.HandleClearChat(frame);
                        break;
                    case VPZoneFrameTypes.PinUpdate:
                        this.HandlePinUpdate(frame);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        /// <summary>
        /// An error frame reports why this connection's own send was rejected. Branching on code
        /// rather than on the text is what VPZone's contract asks for, and slow_mode carries the wait
        /// in retry_after_ms.
        /// </summary>
        public async Task HandleErrorFrame(VPZoneChatEventModel frame)
        {
            if (frame == null)
            {
                return;
            }

            string reason;
            switch ((frame.Code ?? string.Empty).ToLowerInvariant())
            {
                case VPZoneErrorCodes.AuthRequired:
                    reason = Resources.VPZoneSendErrorAuthRequired;
                    break;
                case VPZoneErrorCodes.Banned:
                    reason = Resources.VPZoneSendErrorBanned;
                    break;
                case VPZoneErrorCodes.RateLimited:
                    reason = Resources.VPZoneSendErrorRateLimited;
                    break;
                case VPZoneErrorCodes.SubsOnly:
                    reason = Resources.VPZoneSendErrorSubsOnly;
                    break;
                case VPZoneErrorCodes.FollowersOnly:
                    reason = Resources.VPZoneSendErrorFollowersOnly;
                    break;
                case VPZoneErrorCodes.SlowMode:
                    reason = string.Format(Resources.VPZoneSendErrorSlowMode, Math.Max((frame.RetryAfterMilliseconds ?? 0) / 1000, 1));
                    break;
                case VPZoneErrorCodes.EmoteOnly:
                    reason = Resources.VPZoneSendErrorEmoteOnly;
                    break;
                case VPZoneErrorCodes.BannedWord:
                    reason = Resources.VPZoneSendErrorBannedWord;
                    break;
                case VPZoneErrorCodes.CozyFiltered:
                    reason = Resources.VPZoneSendErrorCozyFiltered;
                    break;
                default:
                    // An unknown code still has a readable body, which is what it is there for.
                    reason = MixItUp.Base.Model.VPZone.Users.VPZoneUserModel.FirstNonEmpty(frame.Body, frame.Message, frame.Code);
                    break;
            }

            Logger.Log(LogLevel.Error, $"VPZone rejected a chat message ({frame.Code}): {reason}");
            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(StreamingPlatformTypeEnum.VPZone,
                string.Format(Resources.VPZoneMessageRejected, reason), ChannelSession.Settings.AlertModerationColor));
        }

        private async Task<UserV2ViewModel> GetOrCreateUser(string username, string displayName = null, string userID = null)
        {
            if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(userID))
            {
                return null;
            }

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.VPZone, platformID: userID, platformUsername: username);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new VPZoneUserPlatformV2Model(userID, username, displayName ?? username));
            }
            return user;
        }

        private async Task<UserV2ViewModel> GetOrCreateUser(WebhookUserModel userReference)
        {
            if (userReference == null || !userReference.IsValid)
            {
                return null;
            }

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.VPZone, platformID: userReference.UserID, platformUsername: userReference.Username);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new VPZoneUserPlatformV2Model(userReference));
            }
            else
            {
                user.GetPlatformData<VPZoneUserPlatformV2Model>(StreamingPlatformTypeEnum.VPZone)?.SetUserProperties(userReference);
            }
            return user;
        }

        private async Task HandleChatMessage(VPZoneChatEventModel frame)
        {
            if (string.IsNullOrWhiteSpace(frame.Body) || frame.IsSystemAuthored)
            {
                return;
            }

            UserV2ViewModel user = await this.GetOrCreateUser(frame.Username);
            if (user == null)
            {
                return;
            }

            VPZoneUserPlatformV2Model platformData = user.GetPlatformData<VPZoneUserPlatformV2Model>(StreamingPlatformTypeEnum.VPZone);
            platformData?.SetChatMessageProperties(frame);

            // A msg frame is the only place a VPZone member's chat color appears, since the REST
            // profile does not carry one, so a user first seen through a follow, a raid or an earlier
            // session has none until they speak. Roll it onto the view model before the message is
            // built, otherwise their first message renders uncolored. The guard keeps this to one
            // refresh per user rather than one per message.
            if (platformData != null && string.IsNullOrEmpty(user.Color) && !string.IsNullOrEmpty(platformData.Color))
            {
                user.RefreshCachedProperties();
            }

            if (frame.SubscriberMonths > 0)
            {
                user.TotalMonthsSubbed = Math.Max(user.TotalMonthsSubbed, (uint)frame.SubscriberMonths);
            }

            // Clear the nonce for the app's own message so the tracking set does not accumulate; the
            // message still renders, exactly as it does on the other platforms.
            this.WasSentByThisClient(frame.Nonce);
            this.ResolveSentMessageID(frame.Nonce, frame.ID);

            await ServiceManager.Get<ChatService>().AddMessage(new VPZoneChatMessageViewModel(frame, user));
        }

        private async Task HandleSystemFrame(VPZoneChatEventModel frame)
        {
            switch ((frame.Kind ?? string.Empty).ToLowerInvariant())
            {
                case VPZoneSystemKinds.StreamStarted:
                    await this.HandleStreamStarted(frame);
                    break;
                case VPZoneSystemKinds.StreamEnded:
                    await this.HandleStreamEnded(frame);
                    break;
                case VPZoneSystemKinds.PixelsCheer:
                    await this.HandlePixelsCheer(frame);
                    break;
                case VPZoneSystemKinds.ChannelPointsRedeem:
                case VPZoneSystemKinds.ChannelPointsAnnounce:
                case VPZoneSystemKinds.ChannelPointsHighlight:
                    await this.HandleChannelPointsRedemption(frame);
                    break;
                case VPZoneSystemKinds.ModerationBan:
                case VPZoneSystemKinds.ModerationCut:
                    await this.HandleModerationNotice(frame);
                    break;
                case VPZoneSystemKinds.LevelUp:
                case VPZoneSystemKinds.ModMessage:
                case VPZoneSystemKinds.CaseOpened:
                case VPZoneSystemKinds.CaseClaimed:
                case VPZoneSystemKinds.CaseGift:
                case VPZoneSystemKinds.CaseDrop:
                case VPZoneSystemKinds.CaseDropClaim:
                case VPZoneSystemKinds.CostreamInvite:
                case VPZoneSystemKinds.CostreamJoin:
                    // These have no Mix It Up counterpart to fire, so they surface in chat using the
                    // readable body VPZone ships with every frame.
                    await this.AddSystemAlert(frame);
                    break;
            }
        }

        private async Task AddSystemAlert(VPZoneChatEventModel frame)
        {
            if (!string.IsNullOrWhiteSpace(frame.Body))
            {
                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(StreamingPlatformTypeEnum.VPZone, frame.Body, ChannelSession.Settings.AlertUserJoinLeaveColor));
            }
        }

        private void HandlePresence(VPZoneChatEventModel frame)
        {
            if (frame.Count.HasValue && frame.Count.Value >= 0)
            {
                ServiceManager.Get<VPZoneSession>().ApplyViewerCount(frame.Count.Value);
            }
        }

        private async Task HandleFollow(VPZoneChatEventModel frame)
        {
            UserV2ViewModel user = await this.GetOrCreateUser(frame.Username);
            if (user == null)
            {
                return;
            }

            user.Roles.Add(UserRoleEnum.Follower);
            user.FollowDate = DateTimeOffset.Now;

            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.VPZone);
            if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VPZoneChannelFollowed, parameters))
            {
                EventService.FollowOccurred(user);
                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(Resources.AlertFollow, user.FullDisplayName), ChannelSession.Settings.AlertFollowColor));
            }
        }

        // VPZone subscriptions are numbered tiers with no streamer-set plan names, so $usersubplan and
        // $usersubplanname both carry the tier label the Twitch and YouTube commands already expect.
        private static string GetSubPlanName(int tier)
        {
            return $"{Resources.Tier} {tier}";
        }

        private async Task HandleSubscription(VPZoneChatEventModel frame)
        {
            UserV2ViewModel user = await this.GetOrCreateUser(frame.Username);
            if (user == null)
            {
                return;
            }

            int months = Math.Max(frame.SubscriberMonths, 1);
            int tier = frame.TierNumber;

            user.Roles.Add(UserRoleEnum.Subscriber);
            user.SubscribeDate = DateTimeOffset.Now;
            user.TotalMonthsSubbed = Math.Max(user.TotalMonthsSubbed, (uint)months);

            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.VPZone);
            parameters.SpecialIdentifiers["usersubmonths"] = months.ToString();
            parameters.SpecialIdentifiers["usersubtier"] = tier.ToString();
            parameters.SpecialIdentifiers["usersubplan"] = GetSubPlanName(tier);
            parameters.SpecialIdentifiers["usersubplanname"] = GetSubPlanName(tier);

            string message = frame.GetMetadataString("message");
            if (!string.IsNullOrWhiteSpace(message))
            {
                parameters.SpecialIdentifiers["message"] = message;
            }

            if (months > 1)
            {
                parameters.SpecialIdentifiers["usersubstreak"] = months.ToString();
                if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VPZoneChannelResubscribed, parameters))
                {
                    EventService.ResubscribeOccurred(new SubscriptionDetailsModel(StreamingPlatformTypeEnum.VPZone, user, months: months, tier: tier));
                    await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(Resources.AlertResubscribedTier, user.FullDisplayName, months, $"VPZone Tier {tier}"), ChannelSession.Settings.AlertSubColor));
                }
            }
            else
            {
                if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VPZoneChannelSubscribed, parameters))
                {
                    EventService.SubscribeOccurred(new SubscriptionDetailsModel(StreamingPlatformTypeEnum.VPZone, user, months: months, tier: tier));
                    await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(Resources.AlertSubscribedTier, user.FullDisplayName, $"VPZone Tier {tier}"), ChannelSession.Settings.AlertSubColor));
                }
            }
        }

        private async Task HandleSubscriptionGift(VPZoneChatEventModel frame)
        {
            // On a gift frame the frame's own username is the gifter, and the recipients ride in
            // metadata. A gift with no named gifter is an anonymous one.
            string gifterUsername = MixItUp.Base.Model.VPZone.Users.VPZoneUserModel.FirstNonEmpty(frame.GetMetadataString("gift_sender"), frame.GetMetadataString("gifter"), frame.Username);

            UserV2ViewModel gifter = null;
            if (!string.IsNullOrWhiteSpace(gifterUsername) && !frame.IsSystemAuthored)
            {
                gifter = await this.GetOrCreateUser(gifterUsername);
            }
            bool isAnonymous = gifter == null;
            if (gifter == null)
            {
                gifter = UserV2ViewModel.CreateUnassociated("Anonymous");
            }

            int tier = frame.TierNumber;
            List<string> recipientNames = ReadRecipients(frame);
            int quantity = Math.Max(frame.GetMetadataInt("count") ?? frame.GetMetadataInt("quantity") ?? recipientNames.Count, Math.Max(recipientNames.Count, 1));

            int filterAmount = ChannelSession.Settings.MassGiftedSubsFilterAmount;
            bool fireMassEvent = filterAmount == 0 || quantity > filterAmount;
            bool fireIndividualEvents = filterAmount == 0 || quantity <= filterAmount;

            List<SubscriptionDetailsModel> subscriptions = new List<SubscriptionDetailsModel>();
            foreach (string recipientName in recipientNames)
            {
                UserV2ViewModel recipient = await this.GetOrCreateUser(recipientName);
                if (recipient == null)
                {
                    continue;
                }

                recipient.Roles.Add(UserRoleEnum.Subscriber);
                recipient.SubscribeDate = DateTimeOffset.Now;
                recipient.TotalSubsReceived++;

                if (fireIndividualEvents)
                {
                    CommandParametersModel giftParameters = BuildGiftParameters(gifter, tier, isAnonymous);
                    giftParameters.TargetUser = recipient;
                    giftParameters.Arguments.Add(recipient.Username);
                    await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VPZoneChannelSubscriptionGifted, giftParameters);
                }

                subscriptions.Add(new SubscriptionDetailsModel(StreamingPlatformTypeEnum.VPZone, recipient, gifter, tier: tier));
            }

            if (recipientNames.Count == 0 && fireIndividualEvents)
            {
                // The frame did not name the recipients, so fire the individual gifted event once per
                // gifted subscription without a target user.
                for (int i = 0; i < quantity; i++)
                {
                    await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VPZoneChannelSubscriptionGifted, BuildGiftParameters(gifter, tier, isAnonymous));
                }
            }

            if (!isAnonymous)
            {
                gifter.TotalSubsGifted += (uint)quantity;
            }

            if (fireMassEvent)
            {
                CommandParametersModel parameters = BuildGiftParameters(gifter, tier, isAnonymous);
                parameters.SpecialIdentifiers["subsgiftedamount"] = quantity.ToString();
                parameters.SpecialIdentifiers["subsgiftedlifetimeamount"] = gifter.TotalSubsGifted.ToString();
                foreach (SubscriptionDetailsModel sub in subscriptions)
                {
                    parameters.Arguments.Add(sub.User.Username);
                }

                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VPZoneChannelMassSubscriptionsGifted, parameters);
                if (subscriptions.Count > 0)
                {
                    EventService.MassSubscriptionsGiftedOccurred(subscriptions);
                }
                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(gifter, string.Format(Resources.AlertMassSubscriptionsGiftedTier, gifter.FullDisplayName, quantity, $"VPZone Tier {tier}"), ChannelSession.Settings.AlertMassGiftedSubColor));
            }
        }

        private static CommandParametersModel BuildGiftParameters(UserV2ViewModel gifter, int tier, bool isAnonymous)
        {
            CommandParametersModel parameters = new CommandParametersModel(gifter, StreamingPlatformTypeEnum.VPZone);
            parameters.SpecialIdentifiers["isanonymous"] = isAnonymous.ToString();
            parameters.SpecialIdentifiers["usersubtier"] = tier.ToString();
            parameters.SpecialIdentifiers["usersubplan"] = GetSubPlanName(tier);
            parameters.SpecialIdentifiers["usersubplanname"] = GetSubPlanName(tier);
            return parameters;
        }

        /// <summary>Reads the gift recipients out of the frame's metadata, whether one or many.</summary>
        private static List<string> ReadRecipients(VPZoneChatEventModel frame)
        {
            List<string> recipients = new List<string>();

            if (frame.Metadata?["recipients"] is JArray array)
            {
                foreach (JToken item in array)
                {
                    string name = (item.Type == JTokenType.String) ? item.ToString() : (item as JObject)?.Value<string>("username");
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        recipients.Add(name);
                    }
                }
            }

            string single = frame.GetMetadataString("recipient") ?? frame.GetMetadataString("target_user");
            if (!string.IsNullOrWhiteSpace(single) && !recipients.Contains(single, StringComparer.OrdinalIgnoreCase))
            {
                recipients.Add(single);
            }

            return recipients;
        }

        private async Task HandleRaid(VPZoneChatEventModel frame)
        {
            // Raids fire on both ends of the hop. Only an incoming raid brings viewers into this
            // channel, so an outgoing one is left alone.
            if (frame.MetadataKindIs(VPZoneSystemKinds.RaidOutgoing))
            {
                return;
            }

            string raiderName = MixItUp.Base.Model.VPZone.Users.VPZoneUserModel.FirstNonEmpty(frame.GetMetadataString("source_channel"), frame.GetMetadataString("from"), frame.Username);
            UserV2ViewModel user = await this.GetOrCreateUser(raiderName);
            if (user == null)
            {
                return;
            }

            int viewers = Math.Max(frame.GetMetadataInt("viewer_count") ?? frame.GetMetadataInt("viewers") ?? frame.Count ?? 0, 0);

            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.VPZone);
            parameters.SpecialIdentifiers["hostviewercount"] = viewers.ToString();
            parameters.SpecialIdentifiers["raidviewercount"] = viewers.ToString();

            if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VPZoneChannelRaided, parameters))
            {
                ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestRaidUserData] = user.ID;
                ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestRaidViewerCountData] = viewers;

                foreach (CurrencyModel currency in ChannelSession.Settings.Currency.Values.ToList())
                {
                    currency.AddAmount(user, currency.OnHostBonus);
                }

                foreach (StreamPassModel streamPass in ChannelSession.Settings.StreamPass.Values)
                {
                    if (user.MeetsRole(streamPass.UserPermission))
                    {
                        streamPass.AddAmount(user, streamPass.HostBonus);
                    }
                }

                EventService.RaidOccurred(user, viewers);

                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(Resources.AlertRaid, user.FullDisplayName, viewers), ChannelSession.Settings.AlertRaidColor));
            }
        }

        private async Task HandlePixelsCheer(VPZoneChatEventModel frame)
        {
            UserV2ViewModel user = await this.GetOrCreateUser(frame.GetMetadataString("username") ?? frame.Username);
            int amount = Math.Max(frame.GetMetadataInt("amount") ?? 0, 0);
            if (user == null || amount <= 0)
            {
                return;
            }

            string cheerMessage = frame.GetMetadataString("message") ?? string.Empty;
            VPZoneChatMessageViewModel message = new VPZoneChatMessageViewModel(user, cheerMessage);

            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.VPZone);
            parameters.SpecialIdentifiers["cheeramount"] = amount.ToString();
            parameters.SpecialIdentifiers["pixelsamount"] = amount.ToString();
            parameters.SpecialIdentifiers["message"] = cheerMessage;
            parameters.SpecialIdentifiers["messagenoemotes"] = message.TextOnlyMessageContents ?? string.Empty;

            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VPZoneChannelCheered, parameters);
            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(Resources.AlertVPZoneCheered, user.FullDisplayName, amount), ChannelSession.Settings.AlertVPZoneCheeredColor));

            EventService.VPZoneChannelCheeredOccurred(new VPZoneCheeredEventModel(user, amount, cheerMessage));
        }

        private async Task HandleChannelPointsRedemption(VPZoneChatEventModel frame)
        {
            string rewardTitle = MixItUp.Base.Model.VPZone.Users.VPZoneUserModel.FirstNonEmpty(frame.GetMetadataString("reward_name"), frame.GetMetadataString("reward"), frame.GetMetadataString("name"));
            if (string.IsNullOrWhiteSpace(rewardTitle))
            {
                return;
            }

            UserV2ViewModel user = await this.GetOrCreateUser(frame.GetMetadataString("username") ?? frame.Username);
            if (user == null)
            {
                return;
            }

            string rewardID = MixItUp.Base.Model.VPZone.Users.VPZoneUserModel.FirstNonEmpty(frame.GetMetadataString("reward_id"), frame.GetMetadataString("rewardId"));
            int cost = Math.Max(frame.GetMetadataInt("points_spent") ?? frame.GetMetadataInt("cost") ?? 0, 0);
            string userInput = MixItUp.Base.Model.VPZone.Users.VPZoneUserModel.FirstNonEmpty(frame.GetMetadataString("prompt"), frame.GetMetadataString("user_message"));

            List<string> arguments = null;
            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.VPZone);
            parameters.SpecialIdentifiers["rewardname"] = rewardTitle;
            parameters.SpecialIdentifiers["rewardcost"] = cost.ToString();
            if (!string.IsNullOrWhiteSpace(userInput))
            {
                VPZoneChatMessageViewModel message = new VPZoneChatMessageViewModel(user, userInput);
                parameters.SpecialIdentifiers["message"] = userInput;
                parameters.SpecialIdentifiers["messagenoemotes"] = message.TextOnlyMessageContents;
                parameters.SpecialIdentifiers["messageemotecount"] = message.EmotesOnlyContents.Count().ToString();
                arguments = new List<string>(userInput.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                parameters.Arguments.AddRange(arguments);
            }

            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VPZoneChannelPointsRedeemed, parameters);
            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(Resources.AlertVPZoneChannelPointRedeemed, user.FullDisplayName, rewardTitle), ChannelSession.Settings.AlertVPZoneChannelPointsColor));

            EventService.ChannelPointsRedeemedOccurred(user, cost);

            VPZoneChannelPointsCommandModel command = ServiceManager.Get<CommandService>().VPZoneChannelPointsCommands.FirstOrDefault(c => string.Equals(c.ChannelPointRewardID, rewardID, StringComparison.OrdinalIgnoreCase));
            if (command == null)
            {
                command = ServiceManager.Get<CommandService>().VPZoneChannelPointsCommands.FirstOrDefault(c => string.Equals(c.Name, rewardTitle, StringComparison.CurrentCultureIgnoreCase));
            }

            if (command != null)
            {
                Dictionary<string, string> channelPointSpecialIdentifiers = new Dictionary<string, string>(parameters.SpecialIdentifiers);
                await ServiceManager.Get<CommandService>().Queue(command, new CommandParametersModel(user, platform: StreamingPlatformTypeEnum.VPZone, arguments: arguments, specialIdentifiers: channelPointSpecialIdentifiers));
            }
        }

        private async Task HandleClip(VPZoneChatEventModel frame)
        {
            UserV2ViewModel user = await this.GetOrCreateUser(frame.GetMetadataString("username") ?? frame.Username);

            CommandParametersModel parameters = (user != null)
                ? new CommandParametersModel(user, StreamingPlatformTypeEnum.VPZone)
                : new CommandParametersModel(StreamingPlatformTypeEnum.VPZone);
            parameters.SpecialIdentifiers["cliptitle"] = frame.GetMetadataString("title") ?? string.Empty;
            parameters.SpecialIdentifiers["clipurl"] = frame.GetMetadataString("url") ?? string.Empty;
            parameters.SpecialIdentifiers["clipid"] = frame.GetMetadataString("clip_id") ?? frame.GetMetadataString("id") ?? string.Empty;

            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VPZoneChannelClipCreated, parameters);
        }

        private async Task HandleShoutout(VPZoneChatEventModel frame)
        {
            string target = frame.GetMetadataString("target_user");
            if (string.IsNullOrWhiteSpace(target))
            {
                return;
            }

            UserV2ViewModel user = await this.GetOrCreateUser(frame.Username);
            CommandParametersModel parameters = (user != null)
                ? new CommandParametersModel(user, StreamingPlatformTypeEnum.VPZone)
                : new CommandParametersModel(StreamingPlatformTypeEnum.VPZone);
            parameters.SpecialIdentifiers["shoutouttarget"] = target;
            parameters.Arguments.Add(target);

            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VPZoneChannelShoutout, parameters);
        }

        private async Task HandleModerationNotice(VPZoneChatEventModel frame)
        {
            string targetUsername = MixItUp.Base.Model.VPZone.Users.VPZoneUserModel.FirstNonEmpty(frame.GetMetadataString("target_user"), frame.GetMetadataString("username"));
            if (string.IsNullOrWhiteSpace(targetUsername))
            {
                return;
            }

            UserV2ViewModel bannedUser = await this.GetOrCreateUser(targetUsername);
            if (bannedUser == null)
            {
                return;
            }

            string reason = frame.GetMetadataString("reason");
            int timeoutLength = Math.Max(frame.GetMetadataInt("duration_seconds") ?? frame.GetMetadataInt("duration") ?? 0, 0);

            if (timeoutLength > 0)
            {
                CommandParametersModel parameters = new CommandParametersModel(StreamingPlatformTypeEnum.VPZone);
                parameters.Arguments.Add("@" + bannedUser.Username);
                parameters.TargetUser = bannedUser;
                parameters.SpecialIdentifiers["timeoutlength"] = timeoutLength.ToString();
                parameters.SpecialIdentifiers["timeoutreason"] = reason ?? string.Empty;
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.ChatUserTimeout, parameters);

                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(bannedUser, string.Format(Resources.AlertTimedOut, bannedUser.FullDisplayName, timeoutLength), ChannelSession.Settings.AlertModerationColor));
                ChatService.ChatUserTimedOut(bannedUser);
            }
            else
            {
                CommandParametersModel parameters = new CommandParametersModel(StreamingPlatformTypeEnum.VPZone);
                parameters.Arguments.Add("@" + bannedUser.Username);
                parameters.TargetUser = bannedUser;
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.ChatUserBan, parameters);

                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(bannedUser, string.Format(Resources.AlertBanned, bannedUser.FullDisplayName), ChannelSession.Settings.AlertModerationColor));
                ChatService.ChatUserBanned(bannedUser);
            }

            // A ban or timeout hides the member's visible messages, which VPZone leaves to the client.
            await ServiceManager.Get<ChatService>().MarkUserMessagesAsDeleted(bannedUser, reason: reason);
        }

        private async Task HandleDeleteMessage(VPZoneChatEventModel frame)
        {
            string messageID = frame.GetMetadataString("messageId") ?? frame.GetMetadataString("message_id");
            if (!string.IsNullOrWhiteSpace(messageID))
            {
                await ServiceManager.Get<ChatService>().DeleteMessage(messageID);
            }
        }

        private async Task HandleClearChat(VPZoneChatEventModel frame)
        {
            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(StreamingPlatformTypeEnum.VPZone, Resources.ChatCleared, ChannelSession.Settings.AlertModerationColor));
            ChatService.ChatCleared();
        }

        /// <summary>
        /// metadata.pinned is null when the pin is cleared and otherwise carries the pinned message.
        /// Tracking the id here means an unpin does not need a round trip to find out what is pinned,
        /// and it stays correct when a moderator pins something from the website.
        /// </summary>
        private void HandlePinUpdate(VPZoneChatEventModel frame)
        {
            JToken pinned = frame.Metadata?["pinned"];
            if (pinned == null || pinned.Type == JTokenType.Null)
            {
                this.PinnedMessageID = null;
                Logger.Log(LogLevel.Debug, "VPZone pinned message cleared");
                return;
            }

            this.PinnedMessageID = pinned["id"]?.ToString() ?? pinned["messageId"]?.ToString() ?? pinned["message_id"]?.ToString();
            Logger.Log(LogLevel.Debug, "VPZone pinned message updated");
        }

        private async Task HandleStreamStarted(VPZoneChatEventModel frame)
        {
            ServiceManager.Get<VPZoneSession>().ApplyStreamStatusUpdate(true, startedAt: DateTimeOffset.Now.ToString("o"));

            // The frame carries the channel and stream ids but no title or category, so the session
            // refreshes them before the event fires and stream-start commands read stale values.
            await ServiceManager.Get<VPZoneSession>().RefreshDetails();

            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VPZoneChannelStreamStart, new CommandParametersModel(StreamingPlatformTypeEnum.VPZone));
        }

        private async Task HandleStreamEnded(VPZoneChatEventModel frame)
        {
            ServiceManager.Get<VPZoneSession>().ApplyStreamStatusUpdate(false);

            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VPZoneChannelStreamStop, new CommandParametersModel(StreamingPlatformTypeEnum.VPZone));
        }

        // ===== Webhook relay =====

        /// <summary>
        /// Handles a delivery relayed from the Desktop API. Only the events the chat gateway does not
        /// carry are acted on here; the stream lifecycle is a backstop and is skipped while the socket
        /// is up, so it never fires twice.
        /// </summary>
        public async Task HandleWebhookEvent(string eventType, JObject payload)
        {
            try
            {
                if (!this.IsConnected || string.IsNullOrWhiteSpace(eventType) || payload == null)
                {
                    return;
                }

                switch (eventType.ToLowerInvariant())
                {
                    case VPZoneWebhookEventTypes.SubscriptionCancelled:
                        await this.HandleSubscriptionCancelled(payload);
                        break;
                    case VPZoneWebhookEventTypes.StreamStarted:
                        if (!this.ChatSocketActive)
                        {
                            await this.HandleWebhookStreamStarted(payload);
                        }
                        break;
                    case VPZoneWebhookEventTypes.StreamEnded:
                        if (!this.ChatSocketActive)
                        {
                            await this.HandleWebhookStreamEnded(payload);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        /// <summary>
        /// A cancellation never appears in chat, by design, so the webhook is the only way to learn
        /// about it. Only the local role is reconciled: there is nothing to announce.
        /// </summary>
        private async Task HandleSubscriptionCancelled(JObject payload)
        {
            WebhookSubscriptionEventModel subEvent = payload.ToObject<WebhookSubscriptionEventModel>();
            WebhookUserModel userReference = subEvent?.ResolvedUser;
            if (userReference == null)
            {
                return;
            }

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.VPZone, platformID: userReference.UserID, platformUsername: userReference.Username);
            user?.Roles.Remove(UserRoleEnum.Subscriber);
        }

        private async Task HandleWebhookStreamStarted(JObject payload)
        {
            WebhookStreamEventModel streamEvent = payload.ToObject<WebhookStreamEventModel>();
            if (!this.ShouldProcessEvent($"vpzone.webhook.stream.started:{streamEvent?.StreamID}"))
            {
                return;
            }

            ServiceManager.Get<VPZoneSession>().ApplyStreamStatusUpdate(true, streamEvent?.Title, streamEvent?.StartedAt);
            await ServiceManager.Get<VPZoneSession>().ApplyMetadataUpdate(streamEvent?.Title, streamEvent?.Category);

            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VPZoneChannelStreamStart, new CommandParametersModel(StreamingPlatformTypeEnum.VPZone));
        }

        private async Task HandleWebhookStreamEnded(JObject payload)
        {
            WebhookStreamEventModel streamEvent = payload.ToObject<WebhookStreamEventModel>();
            if (!this.ShouldProcessEvent($"vpzone.webhook.stream.ended:{streamEvent?.StreamID}"))
            {
                return;
            }

            ServiceManager.Get<VPZoneSession>().ApplyStreamStatusUpdate(false, streamEvent?.Title);

            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VPZoneChannelStreamStop, new CommandParametersModel(StreamingPlatformTypeEnum.VPZone));
        }

        /// <summary>
        /// Collapses redelivery. The gateway replays history on connect and again on a since-cursor
        /// reconnect, and the webhook relay retries on its own schedule, so both paths key on an id
        /// that survives the retry.
        /// </summary>
        private bool ShouldProcessEvent(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return true;
            }

            lock (this.processedEventIDsLock)
            {
                if (!this.processedEventIDs.Add(key))
                {
                    return false;
                }

                this.processedEventIDsQueue.Enqueue(key);
                while (this.processedEventIDsQueue.Count > MaxProcessedEventsCacheSize)
                {
                    this.processedEventIDs.Remove(this.processedEventIDsQueue.Dequeue());
                }
            }
            return true;
        }
    }
}
