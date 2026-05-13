using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Kick.Webhooks;
using MixItUp.Base.Model.User;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.Chat.Kick;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.Kick.New
{
    public class KickClient : ServiceClientBase
    {
        private const int MaxProcessedEventsCacheSize = 3000;

        private readonly IReadOnlyDictionary<string, string> KickEventVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "chat.message.sent", "1" },
            { "channel.followed", "1" },
            { "channel.subscription.new", "1" },
            { "channel.subscription.renewal", "1" },
            { "channel.subscription.gifts", "1" },
            { "channel.reward.redemption.updated", "1" },
            { "livestream.status.updated", "1" },
            { "livestream.metadata.updated", "1" },
            { "moderation.banned", "1" },
            { "kicks.gifted", "1" },
        };

        public override bool IsConnected { get { return this.isConnected; } }
        private bool isConnected;

        private readonly object processedEventIDsLock = new object();
        private readonly Queue<string> processedEventIDsQueue = new Queue<string>();
        private readonly HashSet<string> processedEventIDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public override Task<Result> Connect()
        {
            this.isConnected = true;
            return Task.FromResult(new Result());
        }

        public override Task Disconnect()
        {
            this.isConnected = false;
            return Task.CompletedTask;
        }

        public async Task HandleWebhookEvent(string eventType, JObject payload, WebhookEventModel metadata = null)
        {
            try
            {
                if (!this.IsConnected || string.IsNullOrWhiteSpace(eventType) || payload == null)
                {
                    return;
                }

                if (!this.ShouldProcessEvent(eventType, metadata))
                {
                    return;
                }

                switch (eventType.ToLowerInvariant())
                {
                    case "chat.message.sent":
                        await this.HandleChatMessage(payload);
                        break;
                    case "channel.followed":
                        await this.HandleFollow(payload);
                        break;
                    case "channel.subscription.new":
                        await this.HandleSubscriptionNew(payload);
                        break;
                    case "channel.subscription.renewal":
                        await this.HandleSubscriptionRenewal(payload);
                        break;
                    case "channel.subscription.gifts":
                        await this.HandleSubscriptionGifts(payload);
                        break;
                    case "channel.reward.redemption.updated":
                        await this.HandleRewardRedemptionUpdated(payload);
                        break;
                    case "livestream.status.updated":
                        await this.HandleLivestreamStatusUpdated(payload);
                        break;
                    case "livestream.metadata.updated":
                        await this.HandleLivestreamMetadataUpdated(payload);
                        break;
                    case "moderation.banned":
                        await this.HandleModerationBanned(payload);
                        break;
                    case "kicks.gifted":
                        await this.HandleKicksGifted(payload);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private async Task HandleChatMessage(JObject payload)
        {
            WebhookChatMessageEventModel messageEvent = payload.ToObject<WebhookChatMessageEventModel>();
            if (messageEvent?.Sender == null || messageEvent.Sender.UserID <= 0 || string.IsNullOrWhiteSpace(messageEvent.Content))
            {
                return;
            }

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Kick, platformID: messageEvent.Sender.UserID.ToString(), platformUsername: messageEvent.Sender.Username);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new KickUserPlatformV2Model(messageEvent.Sender));
            }
            else
            {
                KickUserPlatformV2Model platformData = user.GetPlatformData<KickUserPlatformV2Model>(StreamingPlatformTypeEnum.Kick);
                platformData?.SetUserProperties(messageEvent.Sender);
            }

            if (user == null)
            {
                return;
            }

            await ServiceManager.Get<ChatService>().AddMessage(new KickChatMessageViewModel(messageEvent, user));
        }

        private async Task HandleFollow(JObject payload)
        {
            WebhookChannelFollowedEventModel followEvent = payload.ToObject<WebhookChannelFollowedEventModel>();
            if (followEvent?.Follower == null)
            {
                return;
            }

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Kick, platformID: followEvent.Follower.UserID.ToString(), platformUsername: followEvent.Follower.Username);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new KickUserPlatformV2Model(followEvent.Follower));
            }
            else
            {
                KickUserPlatformV2Model platformData = user.GetPlatformData<KickUserPlatformV2Model>(StreamingPlatformTypeEnum.Kick);
                platformData?.SetUserProperties(followEvent.Follower);
            }

            if (user == null)
            {
                return;
            }

            user.Roles.Add(UserRoleEnum.Follower);
            user.FollowDate = DateTimeOffset.Now;

            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Kick);
            if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.KickChannelFollowed, parameters))
            {
                EventService.FollowOccurred(user);
                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertFollow, user.FullDisplayName), ChannelSession.Settings.AlertFollowColor));
            }
        }

        private async Task HandleSubscriptionNew(JObject payload)
        {
            WebhookChannelSubscriptionNewEventModel subEvent = payload.ToObject<WebhookChannelSubscriptionNewEventModel>();
            if (subEvent?.Subscriber == null)
            {
                return;
            }

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Kick, platformID: subEvent.Subscriber.UserID.ToString(), platformUsername: subEvent.Subscriber.Username);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new KickUserPlatformV2Model(subEvent.Subscriber));
            }
            else
            {
                KickUserPlatformV2Model platformData = user.GetPlatformData<KickUserPlatformV2Model>(StreamingPlatformTypeEnum.Kick);
                platformData?.SetUserProperties(subEvent.Subscriber);
            }

            if (user == null)
            {
                return;
            }

            user.Roles.Add(UserRoleEnum.Subscriber);
            user.SubscribeDate = DateTimeOffset.Now;
            user.TotalMonthsSubbed = Math.Max(user.TotalMonthsSubbed, (uint)Math.Max(subEvent.Duration, 1));

            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Kick);
            parameters.SpecialIdentifiers["usersubmonths"] = Math.Max(subEvent.Duration, 1).ToString();
            if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.KickChannelSubscribed, parameters))
            {
                EventService.SubscribeOccurred(new SubscriptionDetailsModel(StreamingPlatformTypeEnum.Kick, user, months: Math.Max(subEvent.Duration, 1), tier: 1));
                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertSubscribedTier, user.FullDisplayName, "Kick Subscription"), ChannelSession.Settings.AlertSubColor));
            }
        }

        private async Task HandleSubscriptionRenewal(JObject payload)
        {
            WebhookChannelSubscriptionRenewalEventModel subEvent = payload.ToObject<WebhookChannelSubscriptionRenewalEventModel>();
            if (subEvent?.Subscriber == null)
            {
                return;
            }

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Kick, platformID: subEvent.Subscriber.UserID.ToString(), platformUsername: subEvent.Subscriber.Username);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new KickUserPlatformV2Model(subEvent.Subscriber));
            }
            else
            {
                KickUserPlatformV2Model platformData = user.GetPlatformData<KickUserPlatformV2Model>(StreamingPlatformTypeEnum.Kick);
                platformData?.SetUserProperties(subEvent.Subscriber);
            }

            if (user == null)
            {
                return;
            }

            user.Roles.Add(UserRoleEnum.Subscriber);
            user.SubscribeDate = DateTimeOffset.Now;
            user.TotalMonthsSubbed = Math.Max(user.TotalMonthsSubbed, (uint)Math.Max(subEvent.Duration, 1));

            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Kick);
            parameters.SpecialIdentifiers["usersubmonths"] = Math.Max(subEvent.Duration, 1).ToString();
            parameters.SpecialIdentifiers["usersubstreak"] = Math.Max(subEvent.Duration, 1).ToString();
            if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.KickChannelResubscribed, parameters))
            {
                EventService.ResubscribeOccurred(new SubscriptionDetailsModel(StreamingPlatformTypeEnum.Kick, user, months: Math.Max(subEvent.Duration, 1), tier: 1));
                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertResubscribedTier, user.FullDisplayName, Math.Max(subEvent.Duration, 1), "Kick Subscription"), ChannelSession.Settings.AlertSubColor));
            }
        }

        private async Task HandleSubscriptionGifts(JObject payload)
        {
            WebhookChannelSubscriptionGiftsEventModel giftsEvent = payload.ToObject<WebhookChannelSubscriptionGiftsEventModel>();
            if (giftsEvent == null || giftsEvent.Giftees == null || giftsEvent.Giftees.Count == 0)
            {
                return;
            }

            UserV2ViewModel gifter = null;
            if (giftsEvent.Gifter != null && giftsEvent.Gifter.UserID > 0)
            {
                gifter = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Kick, platformID: giftsEvent.Gifter.UserID.ToString(), platformUsername: giftsEvent.Gifter.Username);
                if (gifter == null)
                {
                    gifter = await ServiceManager.Get<UserService>().CreateUser(new KickUserPlatformV2Model(giftsEvent.Gifter));
                }
                else
                {
                    KickUserPlatformV2Model platformData = gifter.GetPlatformData<KickUserPlatformV2Model>(StreamingPlatformTypeEnum.Kick);
                    platformData?.SetUserProperties(giftsEvent.Gifter);
                }
            }
            if (gifter == null)
            {
                gifter = UserV2ViewModel.CreateUnassociated("Anonymous");
            }

            int filterAmount = ChannelSession.Settings.MassGiftedSubsFilterAmount;
            bool fireMassEvent = filterAmount == 0 || giftsEvent.Giftees.Count > filterAmount;
            bool fireIndividualEvents = filterAmount == 0 || giftsEvent.Giftees.Count <= filterAmount;

            List<SubscriptionDetailsModel> subscriptions = new List<SubscriptionDetailsModel>();
            foreach (WebhookUserReferenceModel gifteeRef in giftsEvent.Giftees)
            {
                UserV2ViewModel giftee = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Kick, platformID: gifteeRef.UserID.ToString(), platformUsername: gifteeRef.Username);
                if (giftee == null)
                {
                    giftee = await ServiceManager.Get<UserService>().CreateUser(new KickUserPlatformV2Model(gifteeRef));
                }
                else
                {
                    KickUserPlatformV2Model platformData = giftee.GetPlatformData<KickUserPlatformV2Model>(StreamingPlatformTypeEnum.Kick);
                    platformData?.SetUserProperties(gifteeRef);
                }

                if (giftee == null)
                {
                    continue;
                }

                giftee.Roles.Add(UserRoleEnum.Subscriber);
                giftee.SubscribeDate = DateTimeOffset.Now;
                giftee.TotalSubsReceived++;

                if (fireIndividualEvents)
                {
                    CommandParametersModel giftParameters = new CommandParametersModel(gifter, StreamingPlatformTypeEnum.Kick);
                    giftParameters.SpecialIdentifiers["isanonymous"] = (giftsEvent.Gifter?.IsAnonymous ?? false).ToString();
                    giftParameters.TargetUser = giftee;
                    giftParameters.Arguments.Add(giftee.Username);
                    await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.KickChannelSubscriptionGifted, giftParameters);
                }

                subscriptions.Add(new SubscriptionDetailsModel(StreamingPlatformTypeEnum.Kick, giftee, gifter, tier: 1));
            }

            if (subscriptions.Count > 0)
            {
                if (giftsEvent.Gifter != null && !giftsEvent.Gifter.IsAnonymous)
                {
                    gifter.TotalSubsGifted += (uint)subscriptions.Count;
                }

                if (fireMassEvent)
                {
                    CommandParametersModel parameters = new CommandParametersModel(gifter, StreamingPlatformTypeEnum.Kick);
                    parameters.SpecialIdentifiers["subsgiftedamount"] = subscriptions.Count.ToString();
                    parameters.SpecialIdentifiers["subsgiftedlifetimeamount"] = gifter.TotalSubsGifted.ToString();
                    parameters.SpecialIdentifiers["isanonymous"] = (giftsEvent.Gifter?.IsAnonymous ?? false).ToString();
                    foreach (SubscriptionDetailsModel sub in subscriptions)
                    {
                        parameters.Arguments.Add(sub.User.Username);
                    }

                    await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.KickChannelMassSubscriptionsGifted, parameters);
                    EventService.MassSubscriptionsGiftedOccurred(subscriptions);
                    await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(gifter, string.Format(MixItUp.Base.Resources.AlertMassSubscriptionsGiftedTier, gifter.FullDisplayName, subscriptions.Count, "Kick Subscription"), ChannelSession.Settings.AlertMassGiftedSubColor));
                }
            }
        }

        private async Task HandleRewardRedemptionUpdated(JObject payload)
        {
            WebhookRewardRedemptionUpdatedEventModel redemptionEvent = payload.ToObject<WebhookRewardRedemptionUpdatedEventModel>();
            if (redemptionEvent?.Redeemer == null || redemptionEvent.Reward == null || !string.Equals(redemptionEvent.Status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Kick, platformID: redemptionEvent.Redeemer.UserID.ToString(), platformUsername: redemptionEvent.Redeemer.Username);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new KickUserPlatformV2Model(redemptionEvent.Redeemer));
            }
            else
            {
                KickUserPlatformV2Model platformData = user.GetPlatformData<KickUserPlatformV2Model>(StreamingPlatformTypeEnum.Kick);
                platformData?.SetUserProperties(redemptionEvent.Redeemer);
            }

            if (user == null)
            {
                return;
            }

            List<string> arguments = null;
            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Kick);
            parameters.SpecialIdentifiers["rewardname"] = redemptionEvent.Reward.Title;
            parameters.SpecialIdentifiers["rewardcost"] = redemptionEvent.Reward.Cost.ToString();
            if (!string.IsNullOrWhiteSpace(redemptionEvent.UserInput))
            {
                parameters.SpecialIdentifiers["message"] = redemptionEvent.UserInput;
                arguments = new List<string>(redemptionEvent.UserInput.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                parameters.Arguments.AddRange(arguments);
            }

            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.KickChannelPointsRedeemed, parameters);

            KickChannelPointsCommandModel command = ServiceManager.Get<CommandService>().KickChannelPointsCommands.FirstOrDefault(c => string.Equals(c.ChannelPointRewardID, redemptionEvent.Reward.ID, StringComparison.OrdinalIgnoreCase));
            if (command == null)
            {
                command = ServiceManager.Get<CommandService>().KickChannelPointsCommands.FirstOrDefault(c => string.Equals(c.Name, redemptionEvent.Reward.Title, StringComparison.CurrentCultureIgnoreCase));
            }

            if (command != null)
            {
                Dictionary<string, string> channelPointSpecialIdentifiers = new Dictionary<string, string>(parameters.SpecialIdentifiers);
                await ServiceManager.Get<CommandService>().Queue(command, new CommandParametersModel(user, platform: StreamingPlatformTypeEnum.Kick, arguments: arguments, specialIdentifiers: channelPointSpecialIdentifiers));
            }
        }

        private async Task HandleLivestreamStatusUpdated(JObject payload)
        {
            WebhookLivestreamStatusUpdatedEventModel streamEvent = payload.ToObject<WebhookLivestreamStatusUpdatedEventModel>();
            if (streamEvent == null)
            {
                return;
            }

            if (streamEvent.IsLive)
            {
                ServiceManager.Get<KickSession>().ApplyStreamStatusUpdate(true, streamEvent.Title, streamEvent.StartedAt);

                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.KickChannelStreamStart, new CommandParametersModel(StreamingPlatformTypeEnum.Kick));
            }
            else
            {
                ServiceManager.Get<KickSession>().ApplyStreamStatusUpdate(false, streamEvent.Title, null);

                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.KickChannelStreamStop, new CommandParametersModel(StreamingPlatformTypeEnum.Kick));
            }
        }

        private async Task HandleLivestreamMetadataUpdated(JObject payload)
        {
            WebhookLivestreamMetadataUpdatedEventModel metadataEvent = payload.ToObject<WebhookLivestreamMetadataUpdatedEventModel>();
            if (metadataEvent?.Metadata == null)
            {
                return;
            }

            ServiceManager.Get<KickSession>().ApplyMetadataUpdate(metadataEvent.Metadata.Title, metadataEvent.Metadata.Category?.ID, metadataEvent.Metadata.Category?.Name, metadataEvent.Metadata.Category?.Thumbnail);

            CommandParametersModel parameters = new CommandParametersModel(ChannelSession.User, StreamingPlatformTypeEnum.Kick);
            parameters.SpecialIdentifiers["streamtitle"] = metadataEvent.Metadata.Title;
            parameters.SpecialIdentifiers["streamgameid"] = metadataEvent.Metadata.Category?.ID.ToString();
            parameters.SpecialIdentifiers["streamgameimage"] = metadataEvent.Metadata.Category?.Thumbnail;
            parameters.SpecialIdentifiers["streamgame"] = metadataEvent.Metadata.Category?.Name;
            parameters.SpecialIdentifiers["streamgamename"] = metadataEvent.Metadata.Category?.Name;
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.KickChannelUpdated, parameters);
        }

        private async Task HandleModerationBanned(JObject payload)
        {
            WebhookModerationBannedEventModel moderationEvent = payload.ToObject<WebhookModerationBannedEventModel>();
            if (moderationEvent?.BannedUser == null)
            {
                return;
            }

            UserV2ViewModel bannedUser = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Kick, platformID: moderationEvent.BannedUser.UserID.ToString(), platformUsername: moderationEvent.BannedUser.Username);
            if (bannedUser == null)
            {
                bannedUser = await ServiceManager.Get<UserService>().CreateUser(new KickUserPlatformV2Model(moderationEvent.BannedUser));
            }
            else
            {
                KickUserPlatformV2Model platformData = bannedUser.GetPlatformData<KickUserPlatformV2Model>(StreamingPlatformTypeEnum.Kick);
                platformData?.SetUserProperties(moderationEvent.BannedUser);
            }

            if (bannedUser == null)
            {
                return;
            }

            int timeoutLength = 0;
            if (moderationEvent.Metadata != null && !string.IsNullOrWhiteSpace(moderationEvent.Metadata.ExpiresAt))
            {
                DateTimeOffset createdAt = DateTimeOffset.Now;
                if (!string.IsNullOrWhiteSpace(moderationEvent.Metadata.CreatedAt) && DateTimeOffset.TryParse(moderationEvent.Metadata.CreatedAt, out DateTimeOffset createdAtResult))
                {
                    createdAt = createdAtResult;
                }

                if (DateTimeOffset.TryParse(moderationEvent.Metadata.ExpiresAt, out DateTimeOffset expiresAt))
                {
                    timeoutLength = Math.Max(0, (int)Math.Round((expiresAt - createdAt).TotalSeconds));
                }
            }
            if (timeoutLength > 0)
            {
                CommandParametersModel parameters = new CommandParametersModel(StreamingPlatformTypeEnum.Kick);
                parameters.Arguments.Add("@" + bannedUser.Username);
                parameters.TargetUser = bannedUser;
                parameters.SpecialIdentifiers["timeoutlength"] = timeoutLength.ToString();
                parameters.SpecialIdentifiers["timeoutreason"] = moderationEvent.Metadata?.Reason;
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.ChatUserTimeout, parameters);

                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(bannedUser, string.Format(MixItUp.Base.Resources.AlertTimedOut, bannedUser.FullDisplayName, timeoutLength), ChannelSession.Settings.AlertModerationColor));
                ChatService.ChatUserTimedOut(bannedUser);
            }
            else
            {
                CommandParametersModel parameters = new CommandParametersModel(StreamingPlatformTypeEnum.Kick);
                parameters.Arguments.Add("@" + bannedUser.Username);
                parameters.TargetUser = bannedUser;
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.ChatUserBan, parameters);

                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(bannedUser, string.Format(MixItUp.Base.Resources.AlertBanned, bannedUser.FullDisplayName), ChannelSession.Settings.AlertModerationColor));
                ChatService.ChatUserBanned(bannedUser);
            }
        }

        private async Task HandleKicksGifted(JObject payload)
        {
            WebhookKicksGiftedEventModel kicksEvent = payload.ToObject<WebhookKicksGiftedEventModel>();
            if (kicksEvent?.Sender == null || kicksEvent.Gift == null)
            {
                return;
            }

            UserV2ViewModel sender = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Kick, platformID: kicksEvent.Sender.UserID.ToString(), platformUsername: kicksEvent.Sender.Username);
            if (sender == null)
            {
                sender = await ServiceManager.Get<UserService>().CreateUser(new KickUserPlatformV2Model(kicksEvent.Sender));
            }
            else
            {
                KickUserPlatformV2Model platformData = sender.GetPlatformData<KickUserPlatformV2Model>(StreamingPlatformTypeEnum.Kick);
                platformData?.SetUserProperties(kicksEvent.Sender);
            }

            if (sender == null)
            {
                return;
            }

            CommandParametersModel parameters = new CommandParametersModel(sender, StreamingPlatformTypeEnum.Kick);
            parameters.SpecialIdentifiers["kicksamount"] = kicksEvent.Gift.Amount.ToString();
            parameters.SpecialIdentifiers["giftname"] = kicksEvent.Gift.Name;
            parameters.SpecialIdentifiers["gifttype"] = kicksEvent.Gift.Type;
            parameters.SpecialIdentifiers["gifttier"] = kicksEvent.Gift.Tier;
            parameters.SpecialIdentifiers["message"] = kicksEvent.Gift.Message;
            parameters.SpecialIdentifiers["giftpinnedseconds"] = kicksEvent.Gift.PinnedTimeSeconds.ToString();
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.KickChannelKicksGifted, parameters);

            int kicksAmount = kicksEvent.Gift.Amount;
            KickKicksCommandModel command = ServiceManager.Get<CommandService>().KickKicksCommands.FirstOrDefault(c => c.IsEnabled && c.IsSingle && c.StartingAmount == kicksAmount);
            if (command == null)
            {
                command = ServiceManager.Get<CommandService>().KickKicksCommands.Where(c => c.IsEnabled && c.IsRange).OrderBy(c => c.Range).FirstOrDefault(c => c.IsInRange(kicksAmount));
            }

            if (command != null)
            {
                await ServiceManager.Get<CommandService>().Queue(command, parameters);
            }
        }

        private bool ShouldProcessEvent(string eventType, WebhookEventModel metadata)
        {
            if (!string.IsNullOrWhiteSpace(metadata?.EventVersion) &&
                this.KickEventVersions.TryGetValue(eventType, out string desiredVersion) &&
                !string.Equals(metadata.EventVersion, desiredVersion, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Log(LogLevel.Warning, $"Ignoring unsupported Kick webhook event version: {metadata.EventVersion} for event type {eventType}");
                return false;
            }

            string eventID = metadata?.MessageID;
            if (string.IsNullOrWhiteSpace(eventID))
            {
                return true;
            }

            lock (this.processedEventIDsLock)
            {
                if (!this.processedEventIDs.Add(eventID))
                {
                    Logger.Log(LogLevel.Debug, $"Skipping duplicate Kick webhook event: {eventType} ({eventID})");
                    return false;
                }

                this.processedEventIDsQueue.Enqueue(eventID);
                while (this.processedEventIDsQueue.Count > MaxProcessedEventsCacheSize)
                {
                    this.processedEventIDs.Remove(this.processedEventIDsQueue.Dequeue());
                }
            }
            return true;
        }
    }
}
