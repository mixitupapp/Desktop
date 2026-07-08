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

            return new Result();
        }

        protected override async Task DisconnectStreamerInternal()
        {
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

            return new Result();
        }

        protected override Task DisconnectBotInternal() { return Task.CompletedTask; }

        public override async Task RefreshOAuthTokenIfCloseToExpiring()
        {
            await this.StreamerService.RefreshOAuthTokenIfCloseToExpiring();
            await this.BotService.RefreshOAuthTokenIfCloseToExpiring();
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

        public override async Task SendMessage(string message, bool sendAsStreamer = false)
        {
            foreach (string m in this.SplitLargeMessage(message))
            {
                if (!sendAsStreamer && this.IsBotConnected && this.BotService.IsConnected)
                {
                    await this.BotService.SendChatMessage(this.ChannelID, m);
                }
                else
                {
                    await this.StreamerService.SendChatMessage(this.ChannelID, m);
                }
            }
        }

        public override async Task DeleteMessage(ChatMessageViewModel message)
        {
            await this.StreamerService.ModerateUser(this.ChannelID, "delete", userID: message.User?.PlatformID, messageID: message.ID);
        }

        public override Task ClearMessages()
        {
            Logger.Log(LogLevel.Debug, "Velora clear chat is not currently supported by the API");
            return Task.CompletedTask;
        }

        public override async Task TimeoutUser(UserV2ViewModel user, int durationInSeconds, string reason = null)
        {
            await this.StreamerService.ModerateUser(this.ChannelID, "timeout", userID: user.PlatformID, username: user.Username, durationSeconds: Math.Max(durationInSeconds, 1), reason: reason);
        }

        public override Task ModUser(UserV2ViewModel user)
        {
            Logger.Log(LogLevel.Debug, "Velora mod user is not currently supported by the API");
            return Task.CompletedTask;
        }

        public override Task UnmodUser(UserV2ViewModel user)
        {
            Logger.Log(LogLevel.Debug, "Velora unmod user is not currently supported by the API");
            return Task.CompletedTask;
        }

        public override async Task BanUser(UserV2ViewModel user, string reason = null)
        {
            await this.StreamerService.ModerateUser(this.ChannelID, "ban", userID: user.PlatformID, username: user.Username, reason: reason);
        }

        public override async Task UnbanUser(UserV2ViewModel user)
        {
            await this.StreamerService.ModerateUser(this.ChannelID, "unban", userID: user.PlatformID, username: user.Username);
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
    }
}
