using MixItUp.Base.Model;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Model.VPZone.Bots;
using MixItUp.Base.Model.VPZone.ChannelPoints;
using MixItUp.Base.Model.VPZone.Chat;
using MixItUp.Base.Model.VPZone.Streams;
using MixItUp.Base.Model.VPZone.Subscriptions;
using MixItUp.Base.Model.VPZone.Users;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.VPZone.New
{
    public class VPZoneSession : StreamingPlatformSessionBase
    {
        /// <summary>
        /// The scopes the streamer account authorizes. channel_points:grant is deliberately absent:
        /// VPZone accepts only an API key on that endpoint and rejects OAuth tokens outright, so
        /// requesting it would grant nothing.
        /// </summary>
        public static readonly IEnumerable<string> StreamerScopes = new List<string>()
        {
            "profile:read",
            "channel:write",
            "chat:read",
            "chat:write",
            "chat:moderate",
            "chat:announcements",
            "channel_points:read",
            "channel_points:write",
            "dashboard:read",
            "follows:read",
        };

        /// <summary>
        /// The bot account only ever speaks, so it authorizes the minimum needed to identify itself
        /// and send.
        /// </summary>
        public static readonly IEnumerable<string> BotScopes = new List<string>()
        {
            "profile:read",
            "chat:read",
            "chat:write",
        };

        public override int MaxMessageLength { get { return 300; } }
        public override StreamingPlatformTypeEnum Platform { get { return StreamingPlatformTypeEnum.VPZone; } }

        public override OAuthServiceBase StreamerOAuthService { get { return this.StreamerService; } }
        public override OAuthServiceBase BotOAuthService { get { return this.BotService; } }

        public VPZoneService StreamerService { get; private set; } = new VPZoneService(StreamerScopes);
        public VPZoneBotService BotService { get; private set; } = new VPZoneBotService(BotScopes);

        public VPZoneClient Client { get; private set; } = new VPZoneClient();

        /// <summary>The streamer's chat gateway connection, which both receives and sends.</summary>
        public VPZoneChatSocketClient StreamerChatClient { get; private set; }

        /// <summary>
        /// The bot's own chat gateway connection. VPZone has no send-as-bot flag, so the bot is a
        /// second authorized account holding a second socket, and it is send-only to keep every frame
        /// from being processed twice.
        /// </summary>
        public VPZoneChatSocketClient BotChatClient { get; private set; }

        // The webhook relay is left in place but not wired up. Of the five events VPZone will post,
        // four already arrive on the chat gateway (stream.started and stream.ended as system frames,
        // channel.follow as a follow frame, subscription.created as a subscription frame), and the
        // fifth, subscription.cancelled, is not something Mix It Up surfaces. That leaves nothing for
        // the relay to add, so the whole path stays dormant until VPZone's event catalog grows.
        // Bringing it back means uncommenting this property, the two blocks in InitializeStreamer and
        // DisconnectStreamerInternal, and the hub listener in MixItUpService.
        //public VPZoneEventSocketClient EventSocketClient { get; private set; }

        public VPZoneUserModel StreamerModel { get; private set; }
        public VPZoneBotModel BotModel { get; private set; }

        private CancellationTokenSource viewerRefreshCancellationTokenSource;

        public VPZoneChannelModel Channel { get; private set; }

        /// <summary>The channel slug, which is the room name the chat gateway keys on.</summary>
        public string ChannelSlug { get; private set; }

        public int FollowerCount { get; private set; }
        public int SubscriberCount { get; private set; }
        public string StreamDescription { get { return this.Channel?.Description; } }
        public IReadOnlyList<string> StreamTags { get; private set; } = new List<string>();

        /// <summary>
        /// The streamer's own VPZ+ standing, refreshed alongside the rest of the session details so
        /// the special identifiers and role gates stay current.
        /// </summary>
        public VPZPlusMembershipModel VPZPlusMembership { get; private set; }

        public IReadOnlyList<VPZoneChannelPointRewardModel> ChannelPointRewards { get; private set; } = new List<VPZoneChannelPointRewardModel>();

        // VPZone reports a channel's category as free text and returns cover art only from the
        // category list, so the art is looked up separately and cached against the category it was
        // resolved for. A category with genuinely no art then stays resolved rather than being
        // retried on every refresh.
        private string categoryImageName;
        private IReadOnlyList<VPZoneCategoryModel> categories = new List<VPZoneCategoryModel>();

        protected override async Task<Result> InitializeStreamerInternal()
        {
            this.StreamerModel = await this.StreamerService.GetCurrentUser();
            if (this.StreamerModel == null || string.IsNullOrWhiteSpace(this.StreamerModel.UserID))
            {
                return new Result("Failed to get VPZone user data");
            }

            this.StreamerID = this.StreamerModel.UserID;
            this.StreamerUsername = this.StreamerModel.Username;
            this.StreamerAvatarURL = this.StreamerModel.BestAvatarUrl;

            // GET /me carries the profile but not the channel, so the slug comes from the username and
            // is confirmed against the channel lookup below.
            this.ChannelSlug = this.StreamerModel.ChannelSlug;

            this.Channel = await this.StreamerService.GetChannel(this.ChannelSlug);
            if (this.Channel == null)
            {
                return new Result("Failed to get VPZone channel data");
            }

            if (!string.IsNullOrWhiteSpace(this.Channel.Slug))
            {
                this.ChannelSlug = this.Channel.Slug;
            }

            this.ChannelID = this.Channel.ID ?? this.StreamerID;
            this.ChannelLink = string.Format(VPZoneService.ChannelLinkFormat, this.ChannelSlug);

            this.Streamer = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.VPZone, platformID: this.StreamerID);
            if (this.Streamer == null)
            {
                this.Streamer = await ServiceManager.Get<UserService>().CreateUser(new VPZoneUserPlatformV2Model(this.StreamerModel));
            }

            await this.RefreshCategories();
            await this.RefreshChannelPointRewards();

            Result result = await this.Client.Connect();
            if (!result.Success)
            {
                await this.Client.Disconnect();
                return result;
            }

            // The chat gateway is the whole event pipeline, but a miss here is non-fatal: the client
            // keeps rebuilding the connection in the background and replays what it missed on the way
            // back, so it must not fail session initialization.
            this.StreamerChatClient = new VPZoneChatSocketClient(this.Client, () => this.StreamerService.GetOAuthTokenCopy()?.accessToken, () => this.ChannelSlug, processIncomingEvents: true);
            Result chatResult = await this.StreamerChatClient.Connect();
            if (!chatResult.Success)
            {
                Logger.Log(LogLevel.Error, "VPZone chat gateway did not connect during init; retrying in background: " + chatResult.Message);
            }

            // Relay registration, dormant. See the note on EventSocketClient above.
            //this.EventSocketClient = new VPZoneEventSocketClient(this.Client, this.StreamerService);
            //Result eventResult = await this.EventSocketClient.Connect();
            //if (!eventResult.Success)
            //{
            //    Logger.Log(LogLevel.Error, "VPZone lifecycle webhook was not registered during init: " + eventResult.Message);
            //}

            this.viewerRefreshCancellationTokenSource = new CancellationTokenSource();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            AsyncRunner.RunAsyncBackground(this.ViewerUpdateBackground, this.viewerRefreshCancellationTokenSource.Token, 60000);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            return new Result();
        }

        protected override async Task DisconnectStreamerInternal()
        {
            if (this.viewerRefreshCancellationTokenSource != null)
            {
                try
                {
                    this.viewerRefreshCancellationTokenSource.Cancel();
                    this.viewerRefreshCancellationTokenSource.Dispose();
                }
                catch (Exception ex) { Logger.Log(ex); }
                this.viewerRefreshCancellationTokenSource = null;
            }
            if (this.StreamerChatClient != null)
            {
                await this.StreamerChatClient.Disconnect();
                this.StreamerChatClient = null;
            }
            //if (this.EventSocketClient != null)
            //{
            //    await this.EventSocketClient.Disconnect();
            //    this.EventSocketClient = null;
            //}
            await this.Client.Disconnect();
        }

        protected override async Task<Result> InitializeBotInternal()
        {
            VPZoneUserModel botProfile = await this.BotService.GetCurrentUser();
            this.BotModel = VPZoneBotModel.FromProfile(botProfile);
            if (this.BotModel == null)
            {
                return new Result("Failed to get VPZone bot data");
            }

            this.BotID = this.BotModel.BestID;
            this.BotUsername = this.BotModel.BestUsername;
            this.BotAvatarURL = this.BotModel.BestAvatarUrl;

            this.Bot = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.VPZone, platformID: this.BotID);
            if (this.Bot == null)
            {
                this.Bot = await ServiceManager.Get<UserService>().CreateUser(new VPZoneUserPlatformV2Model(this.BotID, this.BotUsername, this.BotModel.BestDisplayName, this.BotAvatarURL));
            }

            // Neither branch above runs the role pass, so without this the bot would show as a plain
            // user until its first chat message.
            VPZoneUserPlatformV2Model botPlatformData = this.Bot?.GetPlatformData<VPZoneUserPlatformV2Model>(StreamingPlatformTypeEnum.VPZone);
            if (botPlatformData != null)
            {
                botPlatformData.RefreshRoleProperties();
                this.Bot.RefreshCachedProperties();
            }

            // The bot needs its own gateway connection because VPZone authenticates a socket by token
            // and has no send-as-someone-else flag. It is send-only, so it never reprocesses frames
            // the streamer's socket already handled.
            this.BotChatClient = new VPZoneChatSocketClient(this.Client, () => this.BotService.GetOAuthTokenCopy()?.accessToken, () => this.ChannelSlug, processIncomingEvents: false);
            Result chatResult = await this.BotChatClient.Connect();
            if (!chatResult.Success)
            {
                Logger.Log(LogLevel.Error, "VPZone bot chat gateway did not connect during init; retrying in background: " + chatResult.Message);
            }

            return new Result();
        }

        protected override async Task DisconnectBotInternal()
        {
            if (this.BotChatClient != null)
            {
                await this.BotChatClient.Disconnect();
                this.BotChatClient = null;
            }
        }

        public override async Task RefreshOAuthTokenIfCloseToExpiring()
        {
            // VPZone authenticates a socket at handshake only, so a refresh that actually rotates the
            // token has to re-handshake the affected connection with the new one.
            string streamerTokenBefore = this.StreamerService.GetOAuthTokenCopy()?.accessToken;
            await this.StreamerService.RefreshOAuthTokenIfCloseToExpiring();
            string streamerTokenAfter = this.StreamerService.GetOAuthTokenCopy()?.accessToken;
            if (!string.IsNullOrEmpty(streamerTokenAfter) && !string.Equals(streamerTokenBefore, streamerTokenAfter, StringComparison.Ordinal))
            {
                if (this.StreamerChatClient != null)
                {
                    await this.StreamerChatClient.ReconnectWithFreshToken();
                }
            }

            if (this.IsBotConnected)
            {
                string botTokenBefore = this.BotService.GetOAuthTokenCopy()?.accessToken;
                await this.BotService.RefreshOAuthTokenIfCloseToExpiring();
                string botTokenAfter = this.BotService.GetOAuthTokenCopy()?.accessToken;
                if (!string.IsNullOrEmpty(botTokenAfter) && !string.Equals(botTokenBefore, botTokenAfter, StringComparison.Ordinal))
                {
                    if (this.BotChatClient != null)
                    {
                        await this.BotChatClient.ReconnectWithFreshToken();
                    }
                }
            }
        }

        public override async Task<Result> RefreshDetails()
        {
            VPZoneChannelModel channel = await this.StreamerService.GetChannel(this.ChannelSlug);
            if (channel == null)
            {
                return new Result("Failed to refresh VPZone channel data");
            }

            this.Channel = channel;
            this.IsLive = channel.IsLive;
            this.StreamTitle = channel.Title;
            this.StreamCategoryID = channel.Category;
            this.StreamCategoryName = channel.Category;
            this.StreamTags = channel.Tags ?? new List<string>();
            await this.RefreshCategoryImage(channel.Category);

            // The presence frame is the live viewer count while the socket is up, so the channel's
            // count is only trusted when it is not.
            if (this.StreamerChatClient == null || !this.StreamerChatClient.IsConnected)
            {
                this.StreamViewerCount = channel.ViewerCount;
            }

            VPZoneChannelDashboardModel dashboard = await this.StreamerService.GetDashboard(this.ChannelSlug);
            if (dashboard?.Stats != null)
            {
                this.FollowerCount = dashboard.Stats.FollowerCount;
                this.SubscriberCount = dashboard.Stats.SubscriberCount;
            }

            if (this.IsLive)
            {
                VPZoneStreamModel currentStream = dashboard?.RecentStreams?.FirstOrDefault(s => string.IsNullOrWhiteSpace(s.EndedAt));
                this.StreamStart = (currentStream != null && !string.IsNullOrWhiteSpace(currentStream.StartedAt))
                    ? DateTimeOffsetExtensions.FromGeneralString(currentStream.StartedAt)
                    : this.StreamStart;
            }
            else
            {
                this.StreamStart = DateTimeOffset.MinValue;
            }

            this.VPZPlusMembership = await this.StreamerService.GetVPZPlusMembership();
            this.ApplyStreamerVPZPlus();

            return new Result();
        }

        /// <summary>Folds the streamer's own VPZ+ standing into their user model.</summary>
        private void ApplyStreamerVPZPlus()
        {
            VPZoneUserPlatformV2Model platformData = this.Streamer?.GetPlatformData<VPZoneUserPlatformV2Model>(StreamingPlatformTypeEnum.VPZone);
            if (platformData != null && this.VPZPlusMembership != null)
            {
                platformData.SetVPZPlusProperties(this.VPZPlusMembership.Active, this.VPZPlusMembership.Since);
                this.Streamer.RefreshCachedProperties();
            }
        }

        /// <summary>
        /// Looks up a member's VPZ+ standing, which is what gates a VPZ+ perk. The lookup carries the
        /// anniversary date, so it also tells you how long they have been a member.
        /// </summary>
        public async Task<VPZoneUserPlatformV2Model> RefreshUserVPZPlus(UserV2ViewModel user)
        {
            VPZoneUserPlatformV2Model platformData = user?.GetPlatformData<VPZoneUserPlatformV2Model>(StreamingPlatformTypeEnum.VPZone);
            if (platformData == null || string.IsNullOrWhiteSpace(platformData.Username))
            {
                return null;
            }

            VPZoneUserModel profile = await this.StreamerService.GetUserByUsername(platformData.Username);
            if (profile != null)
            {
                platformData.SetVPZPlusProperties(profile.VPZPlusActive, profile.VPZPlusSince);
                user.RefreshCachedProperties();
            }
            return platformData;
        }

        public override async Task<Result> SetStreamTitle(string title)
        {
            Result result = await this.StreamerService.UpdateChannel(this.ChannelSlug, title: title);
            if (!result.Success)
            {
                return result;
            }
            return await this.RefreshDetails();
        }

        public override async Task<Result> SetStreamCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return new Result(success: false);
            }

            // VPZone matches the category text against its own game catalog case-insensitively and
            // keeps the raw text when nothing matches, so an unmatched name is still worth sending.
            VPZoneCategoryModel selectedCategory = await this.FindCategory(category);
            Result result = await this.StreamerService.UpdateChannel(this.ChannelSlug, category: selectedCategory?.Name ?? category);
            if (!result.Success)
            {
                return result;
            }
            return await this.RefreshDetails();
        }

        public async Task<Result> SetStreamTags(IEnumerable<string> tags)
        {
            Result result = await this.StreamerService.UpdateChannel(this.ChannelSlug, tags: tags ?? new List<string>());
            if (!result.Success)
            {
                return result;
            }
            return await this.RefreshDetails();
        }

        /// <summary>Resolves a category by name or slug against the cached catalog.</summary>
        public async Task<VPZoneCategoryModel> FindCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return null;
            }

            if (this.categories.Count == 0)
            {
                await this.RefreshCategories();
            }

            return this.categories.FirstOrDefault(c => string.Equals(c.Name, category, StringComparison.OrdinalIgnoreCase))
                ?? this.categories.FirstOrDefault(c => string.Equals(c.Slug, category, StringComparison.OrdinalIgnoreCase))
                ?? this.categories.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Name) && c.Name.StartsWith(category, StringComparison.OrdinalIgnoreCase));
        }

        public async Task RefreshCategories()
        {
            try
            {
                IEnumerable<VPZoneCategoryModel> results = await this.StreamerService.GetCategories();
                if (results != null)
                {
                    this.categories = results.ToList();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public async Task RefreshChannelPointRewards()
        {
            try
            {
                IEnumerable<VPZoneChannelPointRewardModel> rewards = await this.StreamerService.GetChannelPointRewards(this.ChannelSlug);
                if (rewards != null)
                {
                    this.ChannelPointRewards = rewards.ToList();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        /// <summary>Refreshes the category art, skipped when the category has not changed.</summary>
        private async Task RefreshCategoryImage(string categoryName)
        {
            if (string.Equals(this.categoryImageName, categoryName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            this.categoryImageName = categoryName;
            this.StreamCategoryImageURL = (await this.FindCategory(categoryName))?.CoverUrl;
        }

        // ===== Chat =====

        public override async Task SendMessage(string message, bool sendAsStreamer = false)
        {
            foreach (string m in this.SplitLargeMessage(message))
            {
                await this.SendSingleMessage(m, sendAsStreamer, replyToMessageID: null);
            }
        }

        /// <summary>Sends a message as a threaded reply to another message in the channel.</summary>
        public async Task SendReply(string message, string replyToMessageID, bool sendAsStreamer = false)
        {
            bool first = true;
            foreach (string m in this.SplitLargeMessage(message))
            {
                await this.SendSingleMessage(m, sendAsStreamer, first ? replyToMessageID : null);
                first = false;
            }
        }

        /// <summary>
        /// The chat gateway is the send path so the nonce comes back on the broadcast and the message
        /// can be matched to its echo. REST is the fallback for a socket that is down, and its
        /// returned id matches the id on the resulting frame.
        /// </summary>
        private async Task SendSingleMessage(string message, bool sendAsStreamer, string replyToMessageID)
        {
            bool useBot = !sendAsStreamer && this.IsBotConnected;

            VPZoneChatSocketClient socket = useBot ? this.BotChatClient : this.StreamerChatClient;
            if (socket != null && socket.IsConnected)
            {
                try
                {
                    this.Client.TrackSentNonce(await socket.SendMessage(message, replyToMessageID));
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }

            if (useBot)
            {
                // Falling back to the streamer's identity would post under the wrong name, which is
                // not what the streamer configured, so the failure is surfaced instead.
                Logger.Log(LogLevel.Error, "VPZone bot chat gateway is unavailable and the bot has no REST fallback of its own; message not sent.");
                return;
            }

            await this.StreamerService.SendChatMessage(this.ChannelSlug, message, replyToMessageID);
        }

        /// <summary>
        /// Pinning exists on VPZone but only behind the website's own session, so there is nothing an
        /// app holding an OAuth token can call. The message is still posted, because dropping it would
        /// lose the streamer's text outright, and the pin itself is reported as unavailable.
        /// </summary>
        public async Task PinMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            await this.SendMessage(message, sendAsStreamer: true);
            await this.ReportUnsupportedModerationAction(Resources.VPZonePinMessageUnsupported);
        }

        /// <summary>
        /// Reconciles the viewer list against who is actually connected to the channel's chat. The
        /// gateway's presence frame carries a count and nothing else, so without this the list only
        /// ever fills with people who have spoken and nobody is ever removed when they leave.
        /// </summary>
        private async Task ViewerUpdateBackground(CancellationToken cancellationToken)
        {
            IEnumerable<string> viewerNames = await this.StreamerService.GetChatViewers(this.ChannelSlug);
            if (viewerNames == null)
            {
                return;
            }

            HashSet<string> viewers = new HashSet<string>(viewerNames, StringComparer.OrdinalIgnoreCase);

            IEnumerable<UserV2ViewModel> activeUsers = ServiceManager.Get<UserService>().GetActiveUsers(StreamingPlatformTypeEnum.VPZone);
            HashSet<string> activeUsernames = new HashSet<string>(activeUsers.Select(u => u.Username), StringComparer.OrdinalIgnoreCase);

            List<UserV2ViewModel> joins = new List<UserV2ViewModel>();
            foreach (string viewer in viewers)
            {
                if (!activeUsernames.Contains(viewer))
                {
                    UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.VPZone, platformUsername: viewer, performPlatformSearch: true);
                    if (user != null)
                    {
                        joins.Add(user);
                    }
                }
            }

            List<UserV2ViewModel> leaves = activeUsers.Where(u => !viewers.Contains(u.Username)).ToList();

            if (joins.Count > 0)
            {
                await ServiceManager.Get<UserService>().AddOrUpdateActiveUser(joins);
            }
            if (leaves.Count > 0)
            {
                await ServiceManager.Get<UserService>().RemoveActiveUsers(leaves);
            }
        }

        /// <summary>Clearing a pin is behind the same website-only session as setting one.</summary>
        public async Task UnpinMessage()
        {
            await this.ReportUnsupportedModerationAction(Resources.VPZonePinMessageUnsupported);
        }

        /// <summary>
        /// Posts a highlighted announcement banner. This is a first-class endpoint on VPZone rather
        /// than a chat slash command, and it always posts as the channel owner.
        /// </summary>
        public async Task SendAnnouncement(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            Result result = await this.StreamerService.SendAnnouncement(this.ChannelSlug, message);
            if (!result.Success)
            {
                Logger.Log(LogLevel.Error, "VPZone announcement failed: " + result.Message);
                await ServiceManager.Get<ChatService>().AddMessage(new AlertChatMessageViewModel(StreamingPlatformTypeEnum.VPZone,
                    Resources.VPZoneAnnouncementFailed, ChannelSession.Settings.AlertModerationColor));
            }
        }

        // ===== Moderation =====

        public override async Task DeleteMessage(ChatMessageViewModel message)
        {
            VPZoneModerationResult result = await this.StreamerService.DeleteChatMessage(this.ChannelSlug, message?.ID);
            if (result == null || !result.Success)
            {
                await this.ReportModerationFailure("delete", result?.Message);
            }
        }

        public override async Task TimeoutUser(UserV2ViewModel user, int durationInSeconds, string reason = null)
        {
            IReadOnlyList<ChatMessageViewModel> messagesToPurge = this.GetUserMessagesToPurge(user);

            VPZoneModerationResult result = await this.StreamerService.BanUser(this.ChannelSlug, user?.Username, reason, Math.Max(durationInSeconds, 1));
            if (result == null || !result.Success)
            {
                await this.ReportModerationFailure("timeout", result?.Message);
                return;
            }

            await this.PurgeUserMessages(user, messagesToPurge, reason);
        }

        public override async Task BanUser(UserV2ViewModel user, string reason = null)
        {
            IReadOnlyList<ChatMessageViewModel> messagesToPurge = this.GetUserMessagesToPurge(user);

            // Omitting the duration is what makes a ban permanent, which is how VPZone distinguishes
            // it from a timeout on the same endpoint.
            VPZoneModerationResult result = await this.StreamerService.BanUser(this.ChannelSlug, user?.Username, reason, durationSeconds: null);
            if (result == null || !result.Success)
            {
                await this.ReportModerationFailure("ban", result?.Message);
                return;
            }

            await this.PurgeUserMessages(user, messagesToPurge, reason);
        }

        public override async Task UnbanUser(UserV2ViewModel user)
        {
            VPZoneModerationResult result = await this.StreamerService.UnbanUser(this.ChannelSlug, user?.Username);
            if (result == null || !result.Success)
            {
                await this.ReportModerationFailure("unban", result?.Message);
            }
        }

        /// <summary>
        /// VPZone has no moderator management in its API, so promoting and demoting a moderator stays
        /// on the website. Surfacing that is better than failing silently on a call that cannot work.
        /// </summary>
        public override async Task ModUser(UserV2ViewModel user)
        {
            await this.ReportUnsupportedModerationAction(Resources.VPZoneModUserUnsupported);
        }

        public override async Task UnmodUser(UserV2ViewModel user)
        {
            await this.ReportUnsupportedModerationAction(Resources.VPZoneModUserUnsupported);
        }

        /// <summary>
        /// VPZone offers no bulk chat clear to a token-authenticated caller, so clearing deletes the
        /// buffered messages one at a time. Each deletion broadcasts its own frame, so viewers see the
        /// messages disappear the same way they would from a bulk clear, and the local view is dropped
        /// by the caller once this returns.
        /// </summary>
        public override async Task ClearMessages()
        {
            // Snapshotted before the loop starts for the same reason a purge is: VPZone echoes each
            // deletion back over the gateway while the loop is still running, and that echo marks the
            // message deleted locally, so filtering on IsDeleted mid-loop would skip everything after
            // the first one.
            List<ChatMessageViewModel> messagesToDelete = ServiceManager.Get<ChatService>().Messages.ToList().Where(message =>
                message.Platform == StreamingPlatformTypeEnum.VPZone && !message.IsDeleted && !string.IsNullOrEmpty(message.ID)).ToList();

            int failures = 0;
            string lastFailure = null;
            foreach (ChatMessageViewModel message in messagesToDelete)
            {
                VPZoneModerationResult result = await this.StreamerService.DeleteChatMessage(this.ChannelSlug, message.ID);
                if (result == null || !result.Success)
                {
                    failures++;
                    lastFailure = result?.Message;
                }
            }

            Logger.Log(LogLevel.Debug, $"VPZone clear: {messagesToDelete.Count - failures} of {messagesToDelete.Count} message(s) deleted on the platform");

            if (failures > 0)
            {
                await this.ReportModerationFailure("clear", lastFailure);
            }
        }

        /// <summary>
        /// A purge deletes the member's visible messages without timing them out, matching how the
        /// other platforms behave.
        /// </summary>
        public async Task PurgeUser(UserV2ViewModel user)
        {
            await this.PurgeUserMessages(user, this.GetUserMessagesToPurge(user), reason: null);
        }

        /// <summary>
        /// The candidate list is snapshotted before the ban is issued: VPZone echoes the action back
        /// over the gateway while the delete loop is still running, and that echo marks the member's
        /// messages deleted locally, so filtering on IsDeleted mid-loop would skip everything after
        /// the first delete.
        /// </summary>
        private IReadOnlyList<ChatMessageViewModel> GetUserMessagesToPurge(UserV2ViewModel user)
        {
            return ServiceManager.Get<ChatService>().Messages.ToList().Where(message =>
                message.Platform == StreamingPlatformTypeEnum.VPZone && message.User != null && message.User.ID == user.ID &&
                !message.IsDeleted && !string.IsNullOrEmpty(message.ID)).ToList();
        }

        private async Task PurgeUserMessages(UserV2ViewModel user, IReadOnlyList<ChatMessageViewModel> messagesToPurge, string reason)
        {
            foreach (ChatMessageViewModel message in messagesToPurge)
            {
                await this.StreamerService.DeleteChatMessage(this.ChannelSlug, message.ID);
            }

            int purged = await ServiceManager.Get<ChatService>().MarkUserMessagesAsDeleted(user, reason: reason);
            Logger.Log(LogLevel.Debug, $"VPZone purge: {messagesToPurge.Count} platform deletion(s), {purged} marked deleted locally for {user?.Username}");
        }

        // A failed moderation call is always logged; the in-chat alert is throttled because a bulk
        // action would otherwise raise one alert per message.
        private static readonly TimeSpan ModerationAlertInterval = TimeSpan.FromSeconds(30);
        private DateTimeOffset lastModerationAlert = DateTimeOffset.MinValue;

        private async Task ReportModerationFailure(string action, string message)
        {
            Logger.Log(LogLevel.Error, $"VPZone moderation action '{action}' failed: {message ?? "the request could not be sent"}");

            if (DateTimeOffset.Now - this.lastModerationAlert < ModerationAlertInterval)
            {
                return;
            }
            this.lastModerationAlert = DateTimeOffset.Now;

            await ServiceManager.Get<ChatService>().AddMessage(new AlertChatMessageViewModel(StreamingPlatformTypeEnum.VPZone,
                string.Format(Resources.VPZoneModerationActionFailed, action), ChannelSession.Settings.AlertModerationColor));
        }

        private async Task ReportUnsupportedModerationAction(string message)
        {
            Logger.Log(LogLevel.Error, message);
            await ServiceManager.Get<ChatService>().AddMessage(new AlertChatMessageViewModel(StreamingPlatformTypeEnum.VPZone, message, ChannelSession.Settings.AlertModerationColor));
        }

        // ===== Channel points =====

        /// <summary>
        /// Grants channel points to a viewer. VPZone accepts only a channel-bound API key here and
        /// rejects OAuth tokens, so this needs the grant key from the streamer's developer portal and
        /// returns a clear failure when one has not been supplied.
        /// </summary>
        public async Task<Result> GrantChannelPoints(UserV2ViewModel user, int amount, string reason = null)
        {
            VPZoneUserPlatformV2Model platformData = user?.GetPlatformData<VPZoneUserPlatformV2Model>(StreamingPlatformTypeEnum.VPZone);
            if (platformData == null || string.IsNullOrWhiteSpace(platformData.ID))
            {
                return new Result(Resources.VPZoneChannelPointsGrantNoUser);
            }

            VPZoneChannelPointGrantResultModel result = await this.StreamerService.GrantChannelPoints(this.ChannelSlug, platformData.ID, amount, reason);
            if (result == null)
            {
                return new Result(Resources.VPZoneChannelPointsGrantFailed);
            }
            return new Result();
        }

        // ===== Session state applied from the gateway =====

        public void ApplyStreamStatusUpdate(bool isLive, string title = null, string startedAt = null)
        {
            this.IsLive = isLive;
            if (!string.IsNullOrWhiteSpace(title))
            {
                this.StreamTitle = title;
            }

            if (isLive && !string.IsNullOrWhiteSpace(startedAt))
            {
                this.StreamStart = DateTimeOffsetExtensions.FromGeneralString(startedAt);
            }
            else if (!isLive)
            {
                this.StreamStart = DateTimeOffset.MinValue;
                this.StreamViewerCount = 0;
            }
        }

        public async Task ApplyMetadataUpdate(string title, string category)
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                this.StreamTitle = title;
            }
            if (!string.IsNullOrWhiteSpace(category))
            {
                this.StreamCategoryID = category;
                this.StreamCategoryName = category;
                await this.RefreshCategoryImage(category);
            }
        }

        /// <summary>Fed by the gateway's presence frame, which also doubles as its keepalive.</summary>
        public void ApplyViewerCount(int viewerCount)
        {
            this.StreamViewerCount = Math.Max(0, viewerCount);
        }
    }
}
