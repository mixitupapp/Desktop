using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.User;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Model.Velora;
using MixItUp.Base.Model.Velora.Webhooks;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.Chat.Velora;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.Velora.New
{
    public class VeloraClient : ServiceClientBase
    {
        private const int MaxProcessedEventsCacheSize = 3000;

        public override bool IsConnected { get { return this.isConnected; } }
        private bool isConnected;

        private readonly object processedEventIDsLock = new object();
        private readonly Queue<string> processedEventIDsQueue = new Queue<string>();
        private readonly HashSet<string> processedEventIDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> channelPointRedemptionCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

                // Velora's docs and dashboard use a couple of naming variants for some events, so
                // several cases carry an alias to stay compatible with whichever form is delivered.
                switch (eventType.ToLowerInvariant())
                {
                    case "chat.message":
                        await this.HandleChatMessage(payload);
                        break;
                    case "user.follow":
                    case "channel.follow":
                        await this.HandleFollow(payload);
                        break;
                    case "user.unfollow":
                        break;
                    case "channel.subscribe":
                        await this.HandleSubscribe(payload);
                        break;
                    case "channel.subscription.gift":
                    case "channel.gift":
                        await this.HandleSubscriptionGift(payload);
                        break;
                    case "channel.subscription.end":
                        await this.HandleSubscriptionEnd(payload);
                        break;
                    case "channel.cheer":
                    case "channel.volts":
                        await this.HandleCheer(payload);
                        break;
                    case "channel.raid":
                        await this.HandleRaid(payload);
                        break;
                    case "channel.ban":
                        await this.HandleBan(payload);
                        break;
                    case "channel.unban":
                        await this.HandleUnban(payload);
                        break;
                    case "channel.moderator.add":
                        await this.HandleModerator(payload, added: true);
                        break;
                    case "channel.moderator.remove":
                        await this.HandleModerator(payload, added: false);
                        break;
                    case "channel.channel_points_redemption":
                    case "channel.points.redeem":
                        await this.HandleChannelPointsRedemption(payload);
                        break;
                    case "stream.online":
                        await this.HandleStreamOnline(payload);
                        break;
                    case "stream.offline":
                        await this.HandleStreamOffline(payload);
                        break;
                    case "stream.update":
                    case "channel.update":
                        await this.HandleStreamUpdate(payload);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private async Task<UserV2ViewModel> GetOrCreateUser(WebhookUserModel userReference)
        {
            if (userReference == null || !userReference.IsValid)
            {
                return null;
            }

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Velora, platformID: userReference.UserID, platformUsername: userReference.Username);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new VeloraUserPlatformV2Model(userReference));
            }
            else
            {
                VeloraUserPlatformV2Model platformData = user.GetPlatformData<VeloraUserPlatformV2Model>(StreamingPlatformTypeEnum.Velora);
                platformData?.SetUserProperties(userReference);
            }
            return user;
        }

        private async Task HandleChatMessage(JObject payload)
        {
            WebhookChatMessageEventModel messageEvent = payload.ToObject<WebhookChatMessageEventModel>();
            if (messageEvent == null || string.IsNullOrWhiteSpace(messageEvent.Message))
            {
                return;
            }

            UserV2ViewModel user = await this.GetOrCreateUser(messageEvent.ResolvedSender);
            if (user == null)
            {
                return;
            }

            VeloraUserPlatformV2Model platformData = user.GetPlatformData<VeloraUserPlatformV2Model>(StreamingPlatformTypeEnum.Velora);
            platformData?.SetChatMessageProperties(messageEvent);

            if (messageEvent.SubscriberMonths.HasValue)
            {
                user.TotalMonthsSubbed = Math.Max(user.TotalMonthsSubbed, (uint)Math.Max(messageEvent.SubscriberMonths.Value, 0));
            }

            await ServiceManager.Get<ChatService>().AddMessage(new VeloraChatMessageViewModel(messageEvent, user));
        }

        private async Task HandleFollow(JObject payload)
        {
            WebhookFollowEventModel followEvent = payload.ToObject<WebhookFollowEventModel>();
            UserV2ViewModel user = await this.GetOrCreateUser(followEvent?.ResolvedUser);
            if (user == null)
            {
                return;
            }

            user.Roles.Add(UserRoleEnum.Follower);
            user.FollowDate = DateTimeOffset.Now;

            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Velora);
            if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelFollowed, parameters))
            {
                EventService.FollowOccurred(user);
                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertFollow, user.FullDisplayName), ChannelSession.Settings.AlertFollowColor));
            }
        }

        private async Task HandleSubscribe(JObject payload)
        {
            WebhookSubscribeEventModel subEvent = payload.ToObject<WebhookSubscribeEventModel>();
            UserV2ViewModel user = await this.GetOrCreateUser(subEvent?.ResolvedUser);
            if (user == null)
            {
                return;
            }

            int months = subEvent.ResolvedMonths;
            int tier = subEvent.TierNumber;

            user.Roles.Add(UserRoleEnum.Subscriber);
            user.SubscribeDate = DateTimeOffset.Now;
            user.TotalMonthsSubbed = Math.Max(user.TotalMonthsSubbed, (uint)months);

            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Velora);
            parameters.SpecialIdentifiers["usersubmonths"] = months.ToString();
            parameters.SpecialIdentifiers["usersubtier"] = tier.ToString();
            if (!string.IsNullOrWhiteSpace(subEvent.Message))
            {
                parameters.SpecialIdentifiers["message"] = subEvent.Message;
            }

            if (subEvent.IsResubscribe)
            {
                parameters.SpecialIdentifiers["usersubstreak"] = Math.Max(subEvent.Streak ?? months, 1).ToString();
                if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelResubscribed, parameters))
                {
                    EventService.ResubscribeOccurred(new SubscriptionDetailsModel(StreamingPlatformTypeEnum.Velora, user, months: months, tier: tier));
                    await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertResubscribedTier, user.FullDisplayName, months, $"Velora Tier {tier}"), ChannelSession.Settings.AlertSubColor));
                }
            }
            else
            {
                if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelSubscribed, parameters))
                {
                    EventService.SubscribeOccurred(new SubscriptionDetailsModel(StreamingPlatformTypeEnum.Velora, user, months: months, tier: tier));
                    await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertSubscribedTier, user.FullDisplayName, $"Velora Tier {tier}"), ChannelSession.Settings.AlertSubColor));
                }
            }
        }

        private async Task HandleSubscriptionGift(JObject payload)
        {
            WebhookSubscriptionGiftEventModel giftEvent = payload.ToObject<WebhookSubscriptionGiftEventModel>();
            if (giftEvent == null)
            {
                return;
            }

            WebhookUserModel gifterReference = giftEvent.ResolvedGifter;
            UserV2ViewModel gifter = null;
            if (gifterReference != null && !gifterReference.IsAnonymous)
            {
                gifter = await this.GetOrCreateUser(gifterReference);
            }
            if (gifter == null)
            {
                gifter = UserV2ViewModel.CreateUnassociated("Anonymous");
            }

            int tier = giftEvent.TierNumber;
            int quantity = giftEvent.ResolvedQuantity;
            List<WebhookUserModel> recipients = giftEvent.ResolvedRecipients;

            int filterAmount = ChannelSession.Settings.MassGiftedSubsFilterAmount;
            bool fireMassEvent = filterAmount == 0 || quantity > filterAmount;
            bool fireIndividualEvents = filterAmount == 0 || quantity <= filterAmount;

            bool isAnonymous = gifterReference?.IsAnonymous ?? (gifter.IsUnassociated);

            List<SubscriptionDetailsModel> subscriptions = new List<SubscriptionDetailsModel>();
            foreach (WebhookUserModel recipientReference in recipients)
            {
                UserV2ViewModel recipient = await this.GetOrCreateUser(recipientReference);
                if (recipient == null)
                {
                    continue;
                }

                recipient.Roles.Add(UserRoleEnum.Subscriber);
                recipient.SubscribeDate = DateTimeOffset.Now;
                recipient.TotalSubsReceived++;

                if (fireIndividualEvents)
                {
                    CommandParametersModel giftParameters = new CommandParametersModel(gifter, StreamingPlatformTypeEnum.Velora);
                    giftParameters.SpecialIdentifiers["isanonymous"] = isAnonymous.ToString();
                    giftParameters.SpecialIdentifiers["usersubtier"] = tier.ToString();
                    giftParameters.TargetUser = recipient;
                    giftParameters.Arguments.Add(recipient.Username);
                    await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelSubscriptionGifted, giftParameters);
                }

                subscriptions.Add(new SubscriptionDetailsModel(StreamingPlatformTypeEnum.Velora, recipient, gifter, tier: tier));
            }

            if (recipients.Count == 0 && fireIndividualEvents)
            {
                // The payload did not identify the recipients, so fire the individual gifted event
                // once per gifted subscription without a target user.
                for (int i = 0; i < quantity; i++)
                {
                    CommandParametersModel giftParameters = new CommandParametersModel(gifter, StreamingPlatformTypeEnum.Velora);
                    giftParameters.SpecialIdentifiers["isanonymous"] = isAnonymous.ToString();
                    giftParameters.SpecialIdentifiers["usersubtier"] = tier.ToString();
                    await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelSubscriptionGifted, giftParameters);
                }
            }

            if (!isAnonymous)
            {
                gifter.TotalSubsGifted += (uint)quantity;
            }

            if (fireMassEvent)
            {
                CommandParametersModel parameters = new CommandParametersModel(gifter, StreamingPlatformTypeEnum.Velora);
                parameters.SpecialIdentifiers["subsgiftedamount"] = quantity.ToString();
                parameters.SpecialIdentifiers["subsgiftedlifetimeamount"] = gifter.TotalSubsGifted.ToString();
                parameters.SpecialIdentifiers["isanonymous"] = isAnonymous.ToString();
                parameters.SpecialIdentifiers["usersubtier"] = tier.ToString();
                foreach (SubscriptionDetailsModel sub in subscriptions)
                {
                    parameters.Arguments.Add(sub.User.Username);
                }

                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelMassSubscriptionsGifted, parameters);
                if (subscriptions.Count > 0)
                {
                    EventService.MassSubscriptionsGiftedOccurred(subscriptions);
                }
                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(gifter, string.Format(MixItUp.Base.Resources.AlertMassSubscriptionsGiftedTier, gifter.FullDisplayName, quantity, $"Velora Tier {tier}"), ChannelSession.Settings.AlertMassGiftedSubColor));
            }
        }

        private async Task HandleSubscriptionEnd(JObject payload)
        {
            WebhookSubscriptionEndEventModel subEndEvent = payload.ToObject<WebhookSubscriptionEndEventModel>();
            if (subEndEvent?.ResolvedUser == null)
            {
                return;
            }

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Velora, platformID: subEndEvent.ResolvedUser.UserID, platformUsername: subEndEvent.ResolvedUser.Username);
            if (user != null)
            {
                user.Roles.Remove(UserRoleEnum.Subscriber);
            }
        }

        private async Task HandleCheer(JObject payload)
        {
            WebhookCheerEventModel cheerEvent = payload.ToObject<WebhookCheerEventModel>();
            UserV2ViewModel user = await this.GetOrCreateUser(cheerEvent?.ResolvedUser);
            if (user == null || cheerEvent.ResolvedAmount <= 0)
            {
                return;
            }

            VeloraChatMessageViewModel message = new VeloraChatMessageViewModel(user, cheerEvent.Message);

            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Velora);
            parameters.SpecialIdentifiers["cheeramount"] = cheerEvent.ResolvedAmount.ToString();
            parameters.SpecialIdentifiers["voltsamount"] = cheerEvent.ResolvedAmount.ToString();
            parameters.SpecialIdentifiers["message"] = cheerEvent.Message ?? string.Empty;
            parameters.SpecialIdentifiers["messagenoemotes"] = message.TextOnlyMessageContents ?? string.Empty;
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelCheered, parameters);
            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertVeloraCheered, user.FullDisplayName, cheerEvent.ResolvedAmount), ChannelSession.Settings.AlertVeloraCheeredColor));

            EventService.VeloraChannelCheeredOccurred(new VeloraCheeredEventModel(user, cheerEvent.ResolvedAmount, cheerEvent.Message));
        }

        private async Task HandleRaid(JObject payload)
        {
            WebhookRaidEventModel raidEvent = payload.ToObject<WebhookRaidEventModel>();
            UserV2ViewModel user = await this.GetOrCreateUser(raidEvent?.ResolvedUser);
            if (user == null)
            {
                return;
            }

            int viewers = raidEvent.ResolvedViewers;

            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Velora);
            parameters.SpecialIdentifiers["hostviewercount"] = viewers.ToString();
            parameters.SpecialIdentifiers["raidviewercount"] = viewers.ToString();

            if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelRaided, parameters))
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

                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertRaid, user.FullDisplayName, viewers), ChannelSession.Settings.AlertRaidColor));
            }
        }

        private async Task HandleBan(JObject payload)
        {
            WebhookModerationEventModel moderationEvent = payload.ToObject<WebhookModerationEventModel>();
            UserV2ViewModel bannedUser = await this.GetOrCreateUser(moderationEvent?.ResolvedUser);
            if (bannedUser == null)
            {
                return;
            }

            int timeoutLength = 0;
            if (moderationEvent.DurationSeconds.HasValue)
            {
                timeoutLength = Math.Max(0, moderationEvent.DurationSeconds.Value);
            }
            else if (!string.IsNullOrWhiteSpace(moderationEvent.ExpiresAt))
            {
                DateTimeOffset createdAt = DateTimeOffset.Now;
                if (!string.IsNullOrWhiteSpace(moderationEvent.CreatedAt) && DateTimeOffset.TryParse(moderationEvent.CreatedAt, out DateTimeOffset createdAtResult))
                {
                    createdAt = createdAtResult;
                }

                if (DateTimeOffset.TryParse(moderationEvent.ExpiresAt, out DateTimeOffset expiresAt))
                {
                    timeoutLength = Math.Max(0, (int)Math.Round((expiresAt - createdAt).TotalSeconds));
                }
            }

            if (timeoutLength > 0)
            {
                CommandParametersModel parameters = new CommandParametersModel(StreamingPlatformTypeEnum.Velora);
                parameters.Arguments.Add("@" + bannedUser.Username);
                parameters.TargetUser = bannedUser;
                parameters.SpecialIdentifiers["timeoutlength"] = timeoutLength.ToString();
                parameters.SpecialIdentifiers["timeoutreason"] = moderationEvent.Reason ?? string.Empty;
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.ChatUserTimeout, parameters);

                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(bannedUser, string.Format(MixItUp.Base.Resources.AlertTimedOut, bannedUser.FullDisplayName, timeoutLength), ChannelSession.Settings.AlertModerationColor));
                ChatService.ChatUserTimedOut(bannedUser);
            }
            else
            {
                CommandParametersModel parameters = new CommandParametersModel(StreamingPlatformTypeEnum.Velora);
                parameters.Arguments.Add("@" + bannedUser.Username);
                parameters.TargetUser = bannedUser;
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.ChatUserBan, parameters);

                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(bannedUser, string.Format(MixItUp.Base.Resources.AlertBanned, bannedUser.FullDisplayName), ChannelSession.Settings.AlertModerationColor));
                ChatService.ChatUserBanned(bannedUser);
            }
        }

        private async Task HandleUnban(JObject payload)
        {
            WebhookModerationEventModel moderationEvent = payload.ToObject<WebhookModerationEventModel>();
            if (moderationEvent?.ResolvedUser != null)
            {
                Logger.Log(LogLevel.Debug, $"Velora user unbanned: {moderationEvent.ResolvedUser.Username}");
            }
            await Task.CompletedTask;
        }

        private async Task HandleModerator(JObject payload, bool added)
        {
            WebhookModerationEventModel moderationEvent = payload.ToObject<WebhookModerationEventModel>();
            UserV2ViewModel user = await this.GetOrCreateUser(moderationEvent?.ResolvedUser);
            if (user == null)
            {
                return;
            }

            if (added)
            {
                user.Roles.Add(UserRoleEnum.Moderator);
            }
            else
            {
                user.Roles.Remove(UserRoleEnum.Moderator);
            }
        }

        private async Task HandleChannelPointsRedemption(JObject payload)
        {
            WebhookChannelPointsRedemptionEventModel redemptionEvent = payload.ToObject<WebhookChannelPointsRedemptionEventModel>();
            if (redemptionEvent == null || string.IsNullOrWhiteSpace(redemptionEvent.RewardTitle))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(redemptionEvent.RedemptionID) && this.channelPointRedemptionCache.Contains(redemptionEvent.RedemptionID))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(redemptionEvent.Status) &&
                (string.Equals(redemptionEvent.Status, "canceled", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(redemptionEvent.Status, "cancelled", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(redemptionEvent.Status, "rejected", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(redemptionEvent.RedemptionID))
            {
                this.channelPointRedemptionCache.Add(redemptionEvent.RedemptionID);
            }

            UserV2ViewModel user = await this.GetOrCreateUser(redemptionEvent.ResolvedUser);
            if (user == null)
            {
                return;
            }

            List<string> arguments = null;
            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Velora);
            parameters.SpecialIdentifiers["rewardname"] = redemptionEvent.RewardTitle;
            parameters.SpecialIdentifiers["rewardcost"] = redemptionEvent.RewardCost.ToString();
            if (!string.IsNullOrWhiteSpace(redemptionEvent.UserInput))
            {
                VeloraChatMessageViewModel message = new VeloraChatMessageViewModel(user, redemptionEvent.UserInput);
                parameters.SpecialIdentifiers["message"] = redemptionEvent.UserInput;
                parameters.SpecialIdentifiers["messagenoemotes"] = message.TextOnlyMessageContents;
                parameters.SpecialIdentifiers["messageemotecount"] = message.EmotesOnlyContents.Count().ToString();
                arguments = new List<string>(redemptionEvent.UserInput.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                parameters.Arguments.AddRange(arguments);
            }

            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelPointsRedeemed, parameters);
            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertVeloraChannelPointRedeemed, user.FullDisplayName, redemptionEvent.RewardTitle), ChannelSession.Settings.AlertVeloraChannelPointsColor));

            VeloraChannelPointsCommandModel command = ServiceManager.Get<CommandService>().VeloraChannelPointsCommands.FirstOrDefault(c => string.Equals(c.ChannelPointRewardID, redemptionEvent.RewardID, StringComparison.OrdinalIgnoreCase));
            if (command == null)
            {
                command = ServiceManager.Get<CommandService>().VeloraChannelPointsCommands.FirstOrDefault(c => string.Equals(c.Name, redemptionEvent.RewardTitle, StringComparison.CurrentCultureIgnoreCase));
            }

            if (command != null)
            {
                Dictionary<string, string> channelPointSpecialIdentifiers = new Dictionary<string, string>(parameters.SpecialIdentifiers);
                await ServiceManager.Get<CommandService>().Queue(command, new CommandParametersModel(user, platform: StreamingPlatformTypeEnum.Velora, arguments: arguments, specialIdentifiers: channelPointSpecialIdentifiers));
            }
        }

        private async Task HandleStreamOnline(JObject payload)
        {
            WebhookStreamEventModel streamEvent = payload.ToObject<WebhookStreamEventModel>();

            ServiceManager.Get<VeloraSession>().ApplyStreamStatusUpdate(true, streamEvent?.Title, streamEvent?.StartedAt);

            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelStreamStart, new CommandParametersModel(StreamingPlatformTypeEnum.Velora));
        }

        private async Task HandleStreamOffline(JObject payload)
        {
            WebhookStreamEventModel streamEvent = payload.ToObject<WebhookStreamEventModel>();

            ServiceManager.Get<VeloraSession>().ApplyStreamStatusUpdate(false, streamEvent?.Title, null);

            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelStreamStop, new CommandParametersModel(StreamingPlatformTypeEnum.Velora));
        }

        private async Task HandleStreamUpdate(JObject payload)
        {
            WebhookStreamEventModel streamEvent = payload.ToObject<WebhookStreamEventModel>();
            if (streamEvent == null)
            {
                return;
            }

            ServiceManager.Get<VeloraSession>().ApplyMetadataUpdate(streamEvent.Title, streamEvent.ResolvedCategorySlug, streamEvent.ResolvedCategoryName);

            CommandParametersModel parameters = new CommandParametersModel(ChannelSession.User, StreamingPlatformTypeEnum.Velora);
            parameters.SpecialIdentifiers["streamtitle"] = streamEvent.Title ?? string.Empty;
            parameters.SpecialIdentifiers["streamgameid"] = streamEvent.ResolvedCategorySlug ?? string.Empty;
            parameters.SpecialIdentifiers["streamgame"] = streamEvent.ResolvedCategoryName ?? string.Empty;
            parameters.SpecialIdentifiers["streamgamename"] = streamEvent.ResolvedCategoryName ?? string.Empty;
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelUpdated, parameters);
        }

        private bool ShouldProcessEvent(string eventType, WebhookEventModel metadata)
        {
            string eventID = metadata?.MessageID;
            if (string.IsNullOrWhiteSpace(eventID))
            {
                return true;
            }

            lock (this.processedEventIDsLock)
            {
                if (!this.processedEventIDs.Add(eventID))
                {
                    Logger.Log(LogLevel.Debug, $"Skipping duplicate Velora webhook event: {eventType} ({eventID})");
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
