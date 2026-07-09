using MixItUp.Base.Model;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Model.Velora.Emotes;
using MixItUp.Base.Model.Velora.Streams;
using MixItUp.Base.Model.Velora.Subscriptions;
using MixItUp.Base.Model.Velora.Users;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.Chat.Velora;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.Velora.New
{
    public class VeloraSession : StreamingPlatformSessionBase
    {
        // Only scopes present in Velora's live scope list (GET /api/developer/oauth/scopes). Reading
        // channel-point rewards and emotes needs no scope (those endpoints are public), and follows
        // arrive via webhooks, so no channel:*/emotes:read/followers:read scopes are requested.
        public static readonly IEnumerable<string> StreamerScopes = new List<string>()
        {
            "user:read",
            "stream:read",
            "stream:write",
            "chat:read",
            "chat:write",
            "chat:moderate",
            "subscriptions:read",
        };

        public static readonly IEnumerable<string> BotScopes = new List<string>()
        {
            "user:read",
            "chat:read",
            "chat:write",
        };

        public override int MaxMessageLength { get { return 500; } }
        public override StreamingPlatformTypeEnum Platform { get { return StreamingPlatformTypeEnum.Velora; } }

        public override OAuthServiceBase StreamerOAuthService { get { return this.StreamerService; } }
        public override OAuthServiceBase BotOAuthService { get { return this.BotService; } }

        public VeloraService StreamerService { get; private set; } = new VeloraService(StreamerScopes);
        public VeloraService BotService { get; private set; } = new VeloraService(BotScopes, isBotService: true);
        public VeloraClient Client { get; private set; } = new VeloraClient();

        // Client-direct Chat WebSocket connections (namespace /chat). The streamer socket receives + sends;
        // the bot socket is send-only. Created + owned here across the session lifecycle.
        public VeloraChatSocketClient StreamerChatClient { get; private set; }
        public VeloraChatSocketClient BotChatClient { get; private set; }

        // Client-direct Events WebSocket (wss://api.velora.tv/ws/events) for the streamer's own channel.
        // Auto-subscribed on connect; replaces the webhook relay path for non-chat real-time events.
        public VeloraEventSocketClient EventSocketClient { get; private set; }

        public UserModel StreamerModel { get; private set; }
        public UserModel BotModel { get; private set; }

        // Velora has no single "channel" object (like Kick), so subscriber count / description / tags
        // are gathered from separate endpoints and cached here for the special identifier builders.
        public int SubscriberCount { get; private set; }
        public string StreamDescription { get { return this.StreamerModel?.Bio; } }
        public IReadOnlyList<string> StreamTags { get; private set; } = new List<string>();

        // Velora subscriber badges are per-channel milestone badges (by months subscribed); fetched
        // once and cached so each chat message can resolve the right badge without an API call.
        public IReadOnlyList<ChannelSubscriptionBadgeModel> SubscriptionBadges { get; private set; } = new List<ChannelSubscriptionBadgeModel>();

        public Dictionary<string, VeloraChatEmoteViewModel> Emotes { get; private set; } = new Dictionary<string, VeloraChatEmoteViewModel>(StringComparer.Ordinal);

        protected override async Task<Result> InitializeStreamerInternal()
        {
            this.StreamerModel = await this.StreamerService.GetCurrentUser();
            if (this.StreamerModel == null || string.IsNullOrWhiteSpace(this.StreamerModel.UserID))
            {
                return new Result("Failed to get Velora user data");
            }

            this.StreamerID = this.StreamerModel.UserID;
            this.StreamerUsername = this.StreamerModel.Username;
            this.StreamerAvatarURL = this.StreamerModel.BestAvatarUrl;

            // On Velora, a channel is identified by the broadcaster's user ID.
            this.ChannelID = this.StreamerModel.UserID;
            this.ChannelLink = $"https://velora.tv/{this.StreamerModel.Username}";

            this.Streamer = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Velora, platformID: this.StreamerID);
            if (this.Streamer == null)
            {
                this.Streamer = await ServiceManager.Get<UserService>().CreateUser(new VeloraUserPlatformV2Model(this.StreamerModel));
            }

            await this.RefreshEmotes();
            await this.RefreshSubscriptionBadges();

            Result result = await this.Client.Connect();
            if (!result.Success)
            {
                await this.Client.Disconnect();
                return result;
            }

            // Connect the client-direct Events WS for the streamer's own channel. Non-fatal: SocketIOClient
            // keeps retrying in the background, so a miss here must not fail session initialization.
            this.EventSocketClient = new VeloraEventSocketClient(this.Client, () => this.StreamerService.GetOAuthTokenCopy()?.accessToken);
            Result eventResult = await this.EventSocketClient.Connect();
            if (!eventResult.Success)
            {
                Logger.Log(LogLevel.Error, "Velora events socket did not connect during init; retrying in background: " + eventResult.Message);
            }

            // Connect the client-direct Chat WS for the streamer. A socket miss here is non-fatal: it keeps
            // retrying in the background, so it must not fail session initialization.
            this.StreamerChatClient = new VeloraChatSocketClient(this.Client, () => this.StreamerService.GetOAuthTokenCopy()?.accessToken, () => this.ChannelID, processIncomingEvents: true);
            Result chatResult = await this.StreamerChatClient.Connect();
            if (!chatResult.Success)
            {
                Logger.Log(LogLevel.Error, "Velora streamer chat socket did not connect during init; retrying in background: " + chatResult.Message);
            }

            return new Result();
        }

        protected override async Task DisconnectStreamerInternal()
        {
            if (this.StreamerChatClient != null)
            {
                await this.StreamerChatClient.Disconnect();
                this.StreamerChatClient = null;
            }
            if (this.EventSocketClient != null)
            {
                await this.EventSocketClient.Disconnect();
                this.EventSocketClient = null;
            }
            await this.Client.Disconnect();
        }

        protected override async Task<Result> InitializeBotInternal()
        {
            this.BotModel = await this.BotService.GetCurrentUser();
            if (this.BotModel == null || string.IsNullOrWhiteSpace(this.BotModel.UserID))
            {
                return new Result("Failed to get Velora bot data");
            }

            this.BotID = this.BotModel.UserID;
            this.BotUsername = this.BotModel.Username;
            this.BotAvatarURL = this.BotModel.BestAvatarUrl;

            this.Bot = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Velora, platformID: this.BotID);
            if (this.Bot == null)
            {
                this.Bot = await ServiceManager.Get<UserService>().CreateUser(new VeloraUserPlatformV2Model(this.BotModel));
            }

            // Connect the bot's own send-only Chat WS (the bot sends on its own OAuth token). Non-fatal:
            // if it can't connect, SendMessage falls back to REST for the bot account.
            this.BotChatClient = new VeloraChatSocketClient(this.Client, () => this.BotService.GetOAuthTokenCopy()?.accessToken, () => this.ChannelID, processIncomingEvents: false);
            Result botChatResult = await this.BotChatClient.Connect();
            if (!botChatResult.Success)
            {
                Logger.Log(LogLevel.Error, "Velora bot chat socket did not connect during init; retrying in background: " + botChatResult.Message);
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
            // Velora authenticates the sockets at handshake only, so when a refresh actually rotates the
            // access token, re-handshake the affected socket(s) with the new token.
            string streamerTokenBefore = this.StreamerService.GetOAuthTokenCopy()?.accessToken;
            await this.StreamerService.RefreshOAuthTokenIfCloseToExpiring();
            string streamerTokenAfter = this.StreamerService.GetOAuthTokenCopy()?.accessToken;
            if (!string.IsNullOrEmpty(streamerTokenAfter) && !string.Equals(streamerTokenBefore, streamerTokenAfter, StringComparison.Ordinal))
            {
                // Streamer token rotated: re-handshake both of the streamer's sockets with the new token.
                if (this.StreamerChatClient != null)
                {
                    await this.StreamerChatClient.ReconnectWithFreshToken();
                }
                if (this.EventSocketClient != null)
                {
                    await this.EventSocketClient.ReconnectWithFreshToken();
                }
            }

            string botTokenBefore = this.BotService.GetOAuthTokenCopy()?.accessToken;
            await this.BotService.RefreshOAuthTokenIfCloseToExpiring();
            string botTokenAfter = this.BotService.GetOAuthTokenCopy()?.accessToken;
            if (this.BotChatClient != null && !string.IsNullOrEmpty(botTokenAfter) && !string.Equals(botTokenBefore, botTokenAfter, StringComparison.Ordinal))
            {
                await this.BotChatClient.ReconnectWithFreshToken();
            }
        }

        public override async Task<Result> RefreshDetails()
        {
            StreamInfoModel streamInfo = await this.StreamerService.GetStreamInfo();
            if (streamInfo == null)
            {
                return new Result("Failed to refresh Velora stream data");
            }

            this.IsLive = streamInfo.IsLive;
            this.StreamTitle = streamInfo.Title;
            this.StreamCategoryID = streamInfo.CategorySlug;
            this.StreamCategoryName = streamInfo.CategoryName;
            this.StreamViewerCount = streamInfo.ViewerCount;
            this.StreamTags = streamInfo.Tags ?? new List<string>();

            int? subscriberCount = await this.StreamerService.GetSubscriberCount();
            if (subscriberCount.HasValue)
            {
                this.SubscriberCount = subscriberCount.Value;
            }

            if (this.IsLive && !string.IsNullOrWhiteSpace(streamInfo.StartedAt))
            {
                this.StreamStart = DateTimeOffsetExtensions.FromGeneralString(streamInfo.StartedAt);
            }
            else
            {
                this.StreamStart = DateTimeOffset.MinValue;
            }

            return new Result();
        }

        public override async Task<Result> SetStreamTitle(string title)
        {
            Result result = await this.StreamerService.UpdateStreamInfo(title: title);
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

            CategoryModel selectedCategory = null;
            IEnumerable<CategoryModel> categories = await this.StreamerService.GetStreamCategories();
            if (categories != null && categories.Count() > 0)
            {
                selectedCategory = categories.FirstOrDefault(c => string.Equals(c.Name, category, StringComparison.OrdinalIgnoreCase));
                if (selectedCategory == null)
                {
                    selectedCategory = categories.FirstOrDefault(c => string.Equals(c.Slug, category, StringComparison.OrdinalIgnoreCase));
                }
                if (selectedCategory == null)
                {
                    selectedCategory = categories.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Name) && c.Name.StartsWith(category, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (selectedCategory == null)
            {
                return new Result(success: false);
            }

            Result result = await this.StreamerService.UpdateStreamInfo(categorySlug: selectedCategory.Slug);
            if (!result.Success)
            {
                return result;
            }
            return await this.RefreshDetails();
        }

        // Set-tags action: PUT stream/info with a tags[] array (stream:write).
        public async Task<Result> SetStreamTags(IEnumerable<string> tags)
        {
            Result result = await this.StreamerService.UpdateStreamInfo(tags: tags ?? new List<string>());
            if (!result.Success)
            {
                return result;
            }
            return await this.RefreshDetails();
        }

        public override async Task SendMessage(string message, bool sendAsStreamer = false)
        {
            await this.SendMessage(message, effect: null, effectColor: null, sendAsStreamer: sendAsStreamer);
        }

        // Chat WS send with optional Velora message effects (glow/galaxy/rainbow/gigantify) - a new
        // capability the migration unlocks. Falls back to the REST send when the socket is down (the
        // REST path cannot carry effects).
        public async Task SendMessage(string message, string effect, string effectColor, bool sendAsStreamer = false)
        {
            foreach (string m in this.SplitLargeMessage(message))
            {
                bool useBot = !sendAsStreamer && this.IsBotConnected;

                VeloraChatSocketClient chatClient = useBot ? this.BotChatClient : this.StreamerChatClient;
                if (chatClient != null && chatClient.IsConnected)
                {
                    await chatClient.SendMessage(m, effect: effect, effectColor: effectColor);
                }
                else
                {
                    VeloraService service = (useBot && this.BotService.IsConnected) ? this.BotService : this.StreamerService;
                    await service.SendChatMessage(this.ChannelID, m);
                }
            }
        }

        // Chat announcement (newly unlocked via the Chat WS slash command). /announce and its colored
        // variants have documented grammar; sent from the channel-owner (streamer) account by default.
        public async Task SendAnnouncement(string message, string color = null, bool sendAsStreamer = true)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            string prefix = string.IsNullOrWhiteSpace(color) ? "/announce" : "/announce" + color.Trim().ToLowerInvariant();
            string command = prefix + " " + message;

            VeloraChatSocketClient chatClient = (!sendAsStreamer && this.IsBotConnected) ? this.BotChatClient : this.StreamerChatClient;
            if (chatClient != null && chatClient.IsConnected)
            {
                await chatClient.SendSlashCommand(command);
            }
            else
            {
                Logger.Log(LogLevel.Error, "Cannot send Velora announcement: the chat socket is not connected (announce is a Chat WS slash command with no REST equivalent).");
            }
        }

        // Delete / timeout / ban stay on the REST moderate endpoint: it takes an explicit durationSeconds
        // and a reason (the slash /timeout is only "<username> [duration]" - no reason, ambiguous unit),
        // and there is no /delete slash command at all. The slash grammar is otherwise confirmed live
        // (GET /api/chat/slash-commands) and drives the mod/unmod/clear/unban paths below.
        public override async Task DeleteMessage(ChatMessageViewModel message)
        {
            await this.StreamerService.ModerateUser(this.ChannelID, "delete", userID: message.User?.PlatformID, messageID: message.ID);
        }

        public override async Task TimeoutUser(UserV2ViewModel user, int durationInSeconds, string reason = null)
        {
            await this.StreamerService.ModerateUser(this.ChannelID, "timeout", userID: user.PlatformID, username: user.Username, durationSeconds: Math.Max(durationInSeconds, 1), reason: reason);
        }

        public override async Task BanUser(UserV2ViewModel user, string reason = null)
        {
            await this.StreamerService.ModerateUser(this.ChannelID, "ban", userID: user.PlatformID, username: user.Username, reason: reason);
        }

        // mod / unmod / clear / unban have no REST equivalent, so they run as Chat WS slash commands
        // (grammar confirmed via GET /api/chat/slash-commands), executed with channel-owner permissions.
        public override async Task ModUser(UserV2ViewModel user)
        {
            await this.SendModerationSlashCommand($"/mod {user?.Username}", user);
        }

        public override async Task UnmodUser(UserV2ViewModel user)
        {
            await this.SendModerationSlashCommand($"/unmod {user?.Username}", user);
        }

        public override async Task ClearMessages()
        {
            await this.SendModerationSlashCommand("/clear", null);
        }

        public override async Task UnbanUser(UserV2ViewModel user)
        {
            await this.SendModerationSlashCommand($"/unban {user?.Username}", user);
        }

        // Additional slash-command capabilities the migration unlocks (no base-session hook yet; available
        // for Velora-specific actions/commands to call). Grammar confirmed via GET /api/chat/slash-commands.
        public async Task VIPUser(UserV2ViewModel user)
        {
            await this.SendModerationSlashCommand($"/vip {user?.Username}", user);
        }

        public async Task UnVIPUser(UserV2ViewModel user)
        {
            await this.SendModerationSlashCommand($"/unvip {user?.Username}", user);
        }

        public async Task Raid(string targetChannel)
        {
            if (!string.IsNullOrWhiteSpace(targetChannel))
            {
                await this.SendModerationSlashCommand($"/raid {targetChannel}", null);
            }
        }

        public async Task Shoutout(UserV2ViewModel user)
        {
            await this.SendModerationSlashCommand($"/shoutout {user?.Username}", user);
        }

        public async Task UntimeoutUser(UserV2ViewModel user)
        {
            await this.SendModerationSlashCommand($"/untimeout {user?.Username}", user);
        }

        // /cp add|remove <amount> <@user | @all | @active | @followers | @subscribers | ...>. The target is
        // a username or one of Velora's group keywords; a leading "@" is added if the caller omits it.
        public async Task AdjustChannelPoints(string direction, string amount, string target)
        {
            if (string.IsNullOrWhiteSpace(amount) || string.IsNullOrWhiteSpace(target))
            {
                return;
            }

            string dir = (string.Equals(direction, "remove", StringComparison.OrdinalIgnoreCase) || string.Equals(direction, "deduct", StringComparison.OrdinalIgnoreCase)) ? "remove" : "add";
            string resolvedTarget = target.Trim();
            if (!resolvedTarget.StartsWith("@")) { resolvedTarget = "@" + resolvedTarget; }

            await this.SendModerationSlashCommand($"/cp {dir} {amount} {resolvedTarget}", null);
        }

        // Velora slash commands execute with the channel owner's permissions, so they are sent from the
        // streamer's chat socket. These have no REST fallback: if the socket is down, they cannot run.
        private async Task SendModerationSlashCommand(string command, UserV2ViewModel user)
        {
            if (user != null && string.IsNullOrWhiteSpace(user.Username))
            {
                Logger.Log(LogLevel.Error, "Cannot send Velora slash command: the target username is empty.");
                return;
            }

            if (this.StreamerChatClient != null && this.StreamerChatClient.IsConnected)
            {
                await this.StreamerChatClient.SendSlashCommand(command);
            }
            else
            {
                string name = command.Split(' ')[0];
                Logger.Log(LogLevel.Error, $"Cannot run Velora '{name}': the streamer chat socket is not connected (this command has no REST equivalent).");
            }
        }

        public async Task RefreshEmotes()
        {
            try
            {
                IEnumerable<EmoteModel> emotes = await this.StreamerService.GetEmotes(this.StreamerUsername);
                if (emotes != null)
                {
                    Dictionary<string, VeloraChatEmoteViewModel> newEmotes = new Dictionary<string, VeloraChatEmoteViewModel>(StringComparer.Ordinal);
                    foreach (EmoteModel emote in emotes)
                    {
                        if (!string.IsNullOrWhiteSpace(emote.BestCode))
                        {
                            newEmotes[emote.BestCode] = new VeloraChatEmoteViewModel(emote);
                        }
                    }
                    this.Emotes = newEmotes;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public async Task RefreshSubscriptionBadges()
        {
            try
            {
                IEnumerable<ChannelSubscriptionBadgeModel> badges = await this.StreamerService.GetChannelSubscriptionBadges(this.StreamerUsername);
                if (badges != null)
                {
                    this.SubscriptionBadges = badges.ToList();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public string GetSubscriberBadgeUrl(int months)
        {
            return ChannelSubscriptionBadgeModel.ResolveBadgeUrl(this.SubscriptionBadges, months);
        }

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

        public void ApplyMetadataUpdate(string title, string categorySlug, string categoryName)
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                this.StreamTitle = title;
            }
            if (!string.IsNullOrWhiteSpace(categorySlug))
            {
                this.StreamCategoryID = categorySlug;
            }
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                this.StreamCategoryName = categoryName;
            }
        }

        // Fed by the Chat WS viewer_count_update event.
        public void ApplyViewerCount(int viewerCount)
        {
            this.StreamViewerCount = Math.Max(0, viewerCount);
        }
    }
}
