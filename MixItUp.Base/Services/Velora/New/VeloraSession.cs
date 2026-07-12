using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Requirements;
using MixItUp.Base.Model.User;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Model.Velora.Badges;
using MixItUp.Base.Model.Velora.Bots;
using MixItUp.Base.Model.Velora.Chat;
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
using System.Threading;
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
            // The bot is a Velora-side entity managed and spoken-as entirely through the streamer's
            // token (see VeloraBotService), so the bot scopes ride the streamer authorization.
            // NOTE: the app's per-app grant table (developer dashboard / the OAuth consent page) is the
            // authority on scope names - the generic GET /api/developer/oauth/scopes catalog lists a
            // stale "bot:read" that the consent page rejects. The granted set is bot:connect (connect
            // to + speak as a bot on your channel), bot:write (send chat as the connected bot), and
            // bot:commands (publish the command list to the bot profile page; enforced live with a 403,
            // unlike the other bot routes).
            "bot:connect",
            "bot:write",
            "bot:commands",
        };

        public override int MaxMessageLength { get { return 500; } }
        public override StreamingPlatformTypeEnum Platform { get { return StreamingPlatformTypeEnum.Velora; } }

        public override OAuthServiceBase StreamerOAuthService { get { return this.StreamerService; } }
        public override OAuthServiceBase BotOAuthService { get { return this.BotService; } }

        public VeloraService StreamerService { get; private set; } = new VeloraService(StreamerScopes);

        // Velora bots have no OAuth login of their own: the bot service manages the app's server-side
        // bot connection through the streamer's service and never authenticates anything itself.
        public VeloraBotService BotService { get; private set; }

        public VeloraClient Client { get; private set; } = new VeloraClient();

        // Client-direct Chat WebSocket connection (namespace /chat) for the streamer, which receives +
        // sends. The bot has no socket - bot messages go over REST with sendAsBot on the streamer's token.
        public VeloraChatSocketClient StreamerChatClient { get; private set; }

        public VeloraSession()
        {
            this.BotService = new VeloraBotService(this.StreamerService);
        }

        // Client-direct Events WebSocket (wss://api.velora.tv/ws/events) for the streamer's own channel.
        // Auto-subscribed on connect; replaces the webhook relay path for non-chat real-time events.
        public VeloraEventSocketClient EventSocketClient { get; private set; }

        public UserModel StreamerModel { get; private set; }
        public VeloraBotModel BotModel { get; private set; }

        // Velora has no single "channel" object (like Kick), so subscriber count / description / tags
        // are gathered from separate endpoints and cached here for the special identifier builders.
        public int SubscriberCount { get; private set; }
        public string StreamDescription { get { return this.StreamerModel?.Bio; } }
        public IReadOnlyList<string> StreamTags { get; private set; } = new List<string>();

        // Velora subscriber badges are per-channel milestone badges (by months subscribed); fetched
        // once and cached so each chat message can resolve the right badge without an API call.
        public IReadOnlyList<ChannelSubscriptionBadgeModel> SubscriptionBadges { get; private set; } = new List<ChannelSubscriptionBadgeModel>();

        public Dictionary<string, VeloraChatEmoteViewModel> Emotes { get; private set; } = new Dictionary<string, VeloraChatEmoteViewModel>(StringComparer.Ordinal);

        // Global/platform badge catalog (event/promo/etc. badges), keyed by the slug that chat
        // messages carry in their badges[] list; fetched once at session init.
        private Dictionary<string, CatalogBadgeModel> badgeCatalog = new Dictionary<string, CatalogBadgeModel>(StringComparer.OrdinalIgnoreCase);

        protected override async Task<Result> InitializeStreamerInternal()
        {
            // Re-probe the REST moderation endpoint on every (re)connect: a fresh token may carry chat:moderate.
            this.restUserModerationUnavailable = false;

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
            await this.RefreshBadgeCatalog();

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
            // The bot service adopted Velora's stored connection during connect; it is the bot identity.
            this.BotModel = this.BotService.ConnectedBot;
            if (this.BotModel == null || string.IsNullOrWhiteSpace(this.BotModel.BestID))
            {
                return new Result("Failed to get Velora bot data");
            }

            this.BotID = this.BotModel.BestID;
            this.BotUsername = this.BotModel.BestUsername;
            this.BotAvatarURL = this.BotModel.BestAvatarUrl;

            this.Bot = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Velora, platformID: this.BotID);
            if (this.Bot == null)
            {
                this.Bot = await ServiceManager.Get<UserService>().CreateUser(new VeloraUserPlatformV2Model(this.BotID, this.BotUsername, this.BotModel.BestDisplayName, this.BotAvatarURL));
            }

            // Publish the command list to the bot's Velora profile page right away; RefreshDetails keeps
            // it current from here. Non-fatal: a sync problem must not fail the bot connection.
            await this.SyncBotCommandsIfChanged();

            // No bot chat socket: a Velora bot has no token to authenticate one. Bot messages are sent
            // over REST with sendAsBot on the streamer's token (see SendMessage).
            return new Result();
        }

        protected override Task DisconnectBotInternal()
        {
            // A future reconnect must re-publish the command list (Velora clears synced commands on
            // disconnect), so forget what was last synced.
            this.lastSyncedBotCommandsFingerprint = null;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Edit Bot on the Accounts page: change the connected bot's profile image or rename it. Both are
        /// in-place edits of the same bot entity (its ID and app connection survive; renames are limited
        /// by Velora to one per 90 days). Dialog dismissals return quiet successes with nothing to show.
        /// </summary>
        public async Task<Result> EditBot()
        {
            if (!this.IsBotConnected || this.BotService.ConnectedBot == null)
            {
                return new Result(Resources.VeloraBotNotConnected);
            }

            string changeAvatarOption = Resources.VeloraBotEditChangeAvatar;
            string renameOption = Resources.VeloraBotEditRenameBot;
            string choice = await DialogHelper.ShowDropDown(new List<string>() { changeAvatarOption, renameOption }, Resources.VeloraBotEditPrompt);
            if (string.IsNullOrEmpty(choice))
            {
                return new Result();
            }

            Result result;
            if (string.Equals(choice, changeAvatarOption, StringComparison.Ordinal))
            {
                result = await this.BotService.PromptForAvatar(offerSkip: false, CancellationToken.None);
            }
            else
            {
                result = await this.BotService.PromptRenameBot(CancellationToken.None);
            }

            this.ApplyBotIdentity();
            return result;
        }

        // Re-adopts the bot service's (re-fetched) view of the connected bot into the session identity
        // fields after an edit, so the Accounts page and special identifiers reflect the change.
        private void ApplyBotIdentity()
        {
            VeloraBotModel bot = this.BotService.ConnectedBot;
            if (bot != null && !string.IsNullOrWhiteSpace(bot.BestID))
            {
                this.BotModel = bot;
                this.BotID = bot.BestID;
                this.BotUsername = bot.BestUsername;
                this.BotAvatarURL = bot.BestAvatarUrl;
            }
        }

        // ===== Bot command sync (POST /integrations/oauth/bot/commands/sync) =====
        //
        // Publishes the same list the "!commands" premade prints - enabled, non-wildcard chat-accessible
        // commands - to the bot's Velora profile page / the channel's available-commands list
        // (GET /api/chat/bot-commands). Command edits have no single mutation hook in MIU, so the list is
        // fingerprinted and re-synced from the session's periodic RefreshDetails whenever it changes;
        // each sync is a full replacement on Velora's side and the rate limit is 10 req/min.

        private string lastSyncedBotCommandsFingerprint;

        public async Task SyncBotCommandsIfChanged()
        {
            try
            {
                // bot:commands is enforced live (403), so a pre-upgrade token skips quietly until re-auth.
                if (!this.IsBotConnected || !this.StreamerService.HasScope("bot:commands"))
                {
                    return;
                }

                List<VeloraBotSyncCommandModel> commands = BuildBotCommandSyncList();
                string fingerprint = string.Join("\n", commands.Select(c => c.Trigger + "|" + c.Description + "|" + string.Join(",", c.Aliases ?? new List<string>())));
                if (string.Equals(fingerprint, this.lastSyncedBotCommandsFingerprint, StringComparison.Ordinal))
                {
                    return;
                }

                Result result = await this.StreamerService.SyncBotCommands(this.BotID, commands);
                if (result.Success)
                {
                    this.lastSyncedBotCommandsFingerprint = fingerprint;
                    Logger.Log(LogLevel.Debug, $"Synced {commands.Count} command(s) to the Velora bot profile");
                }
                else
                {
                    // Left un-fingerprinted so the next RefreshDetails retries; the body names any DTO
                    // property Velora rejected, which is the diagnostic for shape drift.
                    Logger.Log(LogLevel.Error, "Velora bot command sync failed: " + result.Message);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        /// <summary>The sync counterpart of the "!commands" premade: enabled, non-wildcard chat-accessible
        /// commands (premade + chat + game). The first full trigger is the command, the remaining
        /// triggers ride along as aliases, and the command's name becomes its description. Velora's list
        /// is channel-wide with no per-role visibility (every synced command shows as minimumRole
        /// "everyone"), so only commands everyone can actually run are published - role-gated commands
        /// (mod/VIP/sub/etc.) are left off entirely.</summary>
        private static List<VeloraBotSyncCommandModel> BuildBotCommandSyncList()
        {
            List<VeloraBotSyncCommandModel> commands = new List<VeloraBotSyncCommandModel>();
            foreach (CommandModelBase command in ServiceManager.Get<CommandService>().AllEnabledChatAccessibleCommands)
            {
                if (command is ChatCommandModel chatCommand && !chatCommand.Wildcards && IsRunnableByEveryone(chatCommand))
                {
                    List<string> triggers = chatCommand.GetFullTriggers()
                        .Where(t => !string.IsNullOrWhiteSpace(t) && t.Length <= VeloraBotSyncCommandModel.MaxTriggerLength)
                        .ToList();
                    if (triggers.Count > 0)
                    {
                        string description = chatCommand.Name ?? string.Empty;
                        commands.Add(new VeloraBotSyncCommandModel()
                        {
                            Trigger = triggers[0],
                            Description = description.Length > 200 ? description.Substring(0, 200) : description,
                            Aliases = (triggers.Count > 1) ? triggers.Skip(1).ToList() : null,
                        });
                    }
                }
            }
            return commands.OrderBy(c => c.Trigger, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Whether a command's role requirement is the open "everyone" baseline. The advanced
        /// role-list mode counts only when it includes the base User role (every viewer carries it), and
        /// Patreon-benefit / YouTube-membership gates always exclude the command.</summary>
        private static bool IsRunnableByEveryone(ChatCommandModel command)
        {
            RoleRequirementModel role = command.Requirements?.Role;
            if (role == null)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(role.PatreonBenefitID) || !string.IsNullOrEmpty(role.YouTubeMembershipLevelID))
            {
                return false;
            }

            if (role.UserRoleList != null && role.UserRoleList.Count > 0)
            {
                return role.UserRoleList.Contains(UserRoleEnum.User);
            }

            return role.UserRole == UserRoleEnum.User;
        }

        public override async Task RefreshOAuthTokenIfCloseToExpiring()
        {
            // Velora authenticates the sockets at handshake only, so when a refresh actually rotates the
            // access token, re-handshake the affected socket(s) with the new token. The bot holds no
            // OAuth token of its own (its "credential" is Velora's server-side app connection), so only
            // the streamer's token ever refreshes.
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

            // RefreshDetails runs on the session background cadence, which doubles as the change-detection
            // pass for the bot-profile command list (see SyncBotCommandsIfChanged).
            await this.SyncBotCommandsIfChanged();

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

        // Send with optional Velora message effects (glow/galaxy/rainbow/gigantify). Bot messages go
        // over REST with sendAsBot on the streamer's token - a Velora bot has no token of its own, and
        // the Chat WS has no documented as-the-bot send. Streamer messages prefer the Chat WS and fall
        // back to REST; both REST paths carry effects (documented on the messages endpoint).
        public async Task SendMessage(string message, string effect, string effectColor, bool sendAsStreamer = false)
        {
            foreach (string m in this.SplitLargeMessage(message))
            {
                bool useBot = !sendAsStreamer && this.IsBotConnected;
                if (useBot)
                {
                    SendChatMessageResponseModel response = await this.StreamerService.SendChatMessage(this.ChannelID, m, sendAsBot: true, effect: effect, effectColor: effectColor);
                    if (response == null)
                    {
                        // Surface the failure rather than silently re-sending as the streamer: the user
                        // configured these messages to come from the bot identity.
                        Logger.Log(LogLevel.Error, "Velora sendAsBot chat message failed; see prior log entries for the request error.");
                    }
                }
                else if (this.StreamerChatClient != null && this.StreamerChatClient.IsConnected)
                {
                    await this.StreamerChatClient.SendMessage(m, effect: effect, effectColor: effectColor);
                }
                else
                {
                    await this.StreamerService.SendChatMessage(this.ChannelID, m, effect: effect, effectColor: effectColor);
                }
            }
        }

        // Chat announcement. /announce and its colored variants have documented grammar; slash commands
        // execute with the channel owner's permissions. Two paths, both confirmed live: as the bot, REST
        // with sendAsBot parses the slash command; as the streamer, only the chat socket works - REST
        // without sendAsBot posts the literal "/announce ..." text as a plain message.
        public async Task SendAnnouncement(string message, string color = null, bool sendAsStreamer = false)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            string prefix = string.IsNullOrWhiteSpace(color) ? "/announce" : "/announce" + color.Trim().ToLowerInvariant();
            string command = prefix + " " + message;

            if (!sendAsStreamer && this.IsBotConnected)
            {
                SendChatMessageResponseModel response = await this.StreamerService.SendChatMessage(this.ChannelID, command, sendAsBot: true);
                if (response == null)
                {
                    // Surface the failure rather than silently re-sending as the streamer: the user
                    // configured these announcements to come from the bot identity.
                    Logger.Log(LogLevel.Error, "Velora sendAsBot announcement failed; see prior log entries for the request error.");
                }
            }
            else if (this.StreamerChatClient != null && this.StreamerChatClient.IsConnected)
            {
                await this.StreamerChatClient.SendSlashCommand(command);
            }
            else
            {
                Logger.Log(LogLevel.Error, "Cannot send Velora announcement as the streamer: the chat socket is not connected (REST only parses slash commands when sending as the bot).");
            }
        }

        // The OAuth moderate endpoint requires a target user for every action, deletion included ("targetUserId
        // or targetUsername is required"), even though the website's session-authenticated route accepts just
        // messageId + action. There is no /delete slash command, so this has no fallback.
        public override async Task DeleteMessage(ChatMessageViewModel message)
        {
            VeloraModerationResult result = await this.StreamerService.ModerateUser(this.ChannelID, "delete",
                targetUserID: message.User?.PlatformID, targetUsername: message.User?.Username, messageID: message.ID);
            if (result == null || !result.Success)
            {
                await this.ReportModerationFailure("delete", result?.Message);
            }
        }

        // Ban / timeout try REST first because it is the only path that carries an explicit durationSeconds and
        // a reason, then fall back to the Chat WS slash command, which is confirmed working. Velora's own web
        // client never bans or times out over REST - it uses the socket - so the REST field names for those two
        // actions are unverified. After the first client-side rejection the REST attempt is skipped for the rest
        // of the session rather than failing on every action. The slash fallback carries no reason.
        private bool restUserModerationUnavailable;

        public override async Task TimeoutUser(UserV2ViewModel user, int durationInSeconds, string reason = null)
        {
            IReadOnlyList<ChatMessageViewModel> messagesToPurge = this.GetUserMessagesToPurge(user);

            int duration = Math.Max(durationInSeconds, 1);
            if (!this.restUserModerationUnavailable)
            {
                VeloraModerationResult result = await this.StreamerService.ModerateUser(this.ChannelID, "timeout", targetUserID: user.PlatformID, targetUsername: user.Username, durationSeconds: duration, reason: reason);
                if (result != null && result.Success)
                {
                    await this.PurgeUserMessages(user, messagesToPurge, reason);
                    return;
                }
                this.RecordRestModerationFailure("timeout", result);
            }

            // The slash grammar is "/timeout <username> [duration]". Velora reports timeouts back over the chat
            // socket as durationSeconds, so the duration is passed through as seconds.
            Logger.Log(LogLevel.Debug, $"Velora '/timeout {user?.Username} {duration}' (seconds){ReasonDropped(reason)}");
            if (!await this.SendModerationSlashCommand($"/timeout {user?.Username} {duration}", user))
            {
                await this.ReportModerationFailure("timeout", SocketUnavailableMessage);
            }
            else
            {
                await this.PurgeUserMessages(user, messagesToPurge, reason);
            }
        }

        public override async Task BanUser(UserV2ViewModel user, string reason = null)
        {
            IReadOnlyList<ChatMessageViewModel> messagesToPurge = this.GetUserMessagesToPurge(user);

            if (!this.restUserModerationUnavailable)
            {
                VeloraModerationResult result = await this.StreamerService.ModerateUser(this.ChannelID, "ban", targetUserID: user.PlatformID, targetUsername: user.Username, reason: reason);
                if (result != null && result.Success)
                {
                    await this.PurgeUserMessages(user, messagesToPurge, reason);
                    return;
                }
                this.RecordRestModerationFailure("ban", result);
            }

            Logger.Log(LogLevel.Debug, $"Velora '/ban {user?.Username}'{ReasonDropped(reason)}");
            if (!await this.SendModerationSlashCommand($"/ban {user?.Username}", user))
            {
                await this.ReportModerationFailure("ban", SocketUnavailableMessage);
            }
            else
            {
                await this.PurgeUserMessages(user, messagesToPurge, reason);
            }
        }

        // Velora does not remove a banned or timed-out user's messages platform-side (confirmed live: a REST
        // moderate ban/timeout succeeds and the website keeps showing the messages - userBanned only tells
        // chat clients to purge their own views), and its API has no bulk purge (the OAuth moderate endpoint
        // is exactly timeout/ban/delete-one-message). A Twitch-style purge therefore deletes each of the
        // user's visible messages individually, then marks the local copies deleted.
        //
        // The candidate list MUST be snapshotted before the timeout/ban is issued: Velora echoes the action
        // back over the chat socket (userTimedOut/userBanned) while the delete loop is still running, and
        // that echo marks all of the user's messages deleted locally - filtering on IsDeleted mid-loop then
        // skips everything after the first delete.
        private IReadOnlyList<ChatMessageViewModel> GetUserMessagesToPurge(UserV2ViewModel user)
        {
            return ServiceManager.Get<ChatService>().Messages.ToList().Where(message =>
                message.Platform == StreamingPlatformTypeEnum.Velora && message.User != null && message.User.ID == user.ID &&
                !message.IsDeleted && !string.IsNullOrEmpty(message.ID)).ToList();
        }

        private async Task PurgeUserMessages(UserV2ViewModel user, IReadOnlyList<ChatMessageViewModel> messagesToPurge, string reason)
        {
            foreach (ChatMessageViewModel message in messagesToPurge)
            {
                await this.DeleteMessage(message);
            }

            int purged = await ServiceManager.Get<ChatService>().MarkUserMessagesAsDeleted(user, reason: reason);
            Logger.Log(LogLevel.Debug, $"Velora purge: {messagesToPurge.Count} platform deletion(s), {purged} marked deleted locally for {user?.Username}");
        }

        // A purge on Velora is not a timeout (ChatService.PurgeUser routes here instead): a timeout does not
        // touch existing messages and announces itself in the channel's chat, so purging is purely deleting
        // the user's visible messages.
        public async Task PurgeUser(UserV2ViewModel user)
        {
            await this.PurgeUserMessages(user, this.GetUserMessagesToPurge(user), reason: null);
        }

        // Only a 4xx means Velora rejected the request shape; a transient failure must not permanently downgrade
        // this session to the slash command, which cannot carry a ban/timeout reason.
        private void RecordRestModerationFailure(string action, VeloraModerationResult result)
        {
            if (result != null && result.IsRequestRejected)
            {
                this.restUserModerationUnavailable = true;
                Logger.Log(LogLevel.Error, $"Velora rejected the REST '{action}' request; using the slash command for the rest of this session: {result.Message}");
            }
            else
            {
                Logger.Log(LogLevel.Error, $"Velora REST '{action}' failed, falling back to the slash command: {result?.Message ?? "the request could not be sent"}");
            }
        }

        /// <summary>The slash-command fallbacks take no reason argument, so note when one is being discarded.</summary>
        private static string ReasonDropped(string reason)
        {
            return string.IsNullOrWhiteSpace(reason) ? string.Empty : $" (reason '{reason}' dropped: the slash command takes no reason)";
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
        // streamer's chat socket. Returns false when the command could not be emitted, which lets the callers
        // that have a REST equivalent decide what to do about it.
        private async Task<bool> SendModerationSlashCommand(string command, UserV2ViewModel user)
        {
            if (user != null && string.IsNullOrWhiteSpace(user.Username))
            {
                Logger.Log(LogLevel.Error, "Cannot send Velora slash command: the target username is empty.");
                return false;
            }

            if (this.StreamerChatClient != null && this.StreamerChatClient.IsConnected)
            {
                await this.StreamerChatClient.SendSlashCommand(command);
                return true;
            }

            string name = command.Split(' ')[0];
            Logger.Log(LogLevel.Error, $"Cannot run Velora '{name}': the streamer chat socket is not connected.");
            return false;
        }

        // A failed moderation call used to be discarded silently, which is why a dead REST endpoint looked
        // like a Mix It Up bug. Every failure is logged; the in-chat alert is throttled because "Disable Chat"
        // deletes every incoming message and would otherwise raise one alert per message.
        private const string SocketUnavailableMessage = "the streamer chat socket is not connected";

        private static readonly TimeSpan ModerationAlertInterval = TimeSpan.FromSeconds(30);
        private DateTimeOffset lastModerationAlert = DateTimeOffset.MinValue;

        private async Task ReportModerationFailure(string action, string message)
        {
            Logger.Log(LogLevel.Error, $"Velora moderation action '{action}' failed: {message ?? "the request could not be sent"}");

            if (DateTimeOffset.Now - this.lastModerationAlert < ModerationAlertInterval)
            {
                return;
            }
            this.lastModerationAlert = DateTimeOffset.Now;

            await ServiceManager.Get<ChatService>().AddMessage(new AlertChatMessageViewModel(StreamingPlatformTypeEnum.Velora,
                string.Format(MixItUp.Base.Resources.VeloraModerationActionFailed, action), ChannelSession.Settings.AlertModerationColor));
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

        public async Task RefreshBadgeCatalog()
        {
            try
            {
                IEnumerable<CatalogBadgeModel> badges = await this.StreamerService.GetBadgeCatalog();
                if (badges != null)
                {
                    Dictionary<string, CatalogBadgeModel> newCatalog = new Dictionary<string, CatalogBadgeModel>(StringComparer.OrdinalIgnoreCase);
                    foreach (CatalogBadgeModel badge in badges)
                    {
                        if (!string.IsNullOrWhiteSpace(badge.Slug) && !string.IsNullOrWhiteSpace(badge.BestImageUrl))
                        {
                            newCatalog[badge.Slug] = badge;
                        }
                    }
                    this.badgeCatalog = newCatalog;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        /// <summary>Resolves a chat badge slug (e.g. "christmas-2025") against the global badge catalog.</summary>
        public string GetCatalogBadgeUrl(string badgeSlug)
        {
            if (!string.IsNullOrWhiteSpace(badgeSlug) && this.badgeCatalog.TryGetValue(badgeSlug, out CatalogBadgeModel badge))
            {
                return badge.BestImageUrl;
            }
            return null;
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
