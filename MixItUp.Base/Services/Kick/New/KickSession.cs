using MixItUp.Base.Model;
using MixItUp.Base.Model.Kick.Channels;
using MixItUp.Base.Model.Kick.Users;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.Kick.New
{
    public class KickSession : StreamingPlatformSessionBase
    {
        public static readonly IEnumerable<string> StreamerScopes = new List<string>()
        {
            "user:read",
            "channel:read",
            "channel:write",
            "channel:rewards:read",
            "channel:rewards:write",
            "chat:write",
            "kicks:read",
            "moderation:ban",
            "moderation:chat_message:manage",
            "events:subscribe",
        };

        public static readonly IEnumerable<string> BotScopes = new List<string>()
        {
            "user:read",
            "chat:write",
            "moderation:chat_message:manage",
        };

        public override int MaxMessageLength { get { return 500; } }
        public override StreamingPlatformTypeEnum Platform { get { return StreamingPlatformTypeEnum.Kick; } }

        public override OAuthServiceBase StreamerOAuthService { get { return this.StreamerService; } }
        public override OAuthServiceBase BotOAuthService { get { return this.BotService; } }

        public KickService StreamerService { get; private set; } = new KickService(StreamerScopes);
        public KickService BotService { get; private set; } = new KickService(BotScopes, isBotService: true);
        public KickClient Client { get; private set; } = new KickClient();

        public UserModel StreamerModel { get; private set; }
        public UserModel BotModel { get; private set; }

        public ChannelModel Channel { get; private set; }

        protected override async Task<Result> InitializeStreamerInternal()
        {
            this.StreamerModel = await this.StreamerService.GetCurrentUser();
            if (this.StreamerModel == null)
            {
                return new Result("Failed to get Kick user data");
            }

            this.Channel = await this.StreamerService.GetCurrentChannel();
            if (this.Channel == null)
            {
                return new Result("Failed to get Kick channel data");
            }

            this.StreamerID = this.StreamerModel.UserID.ToString();
            this.StreamerUsername = this.StreamerModel.Name;
            this.StreamerAvatarURL = this.StreamerModel.ProfilePicture;

            this.ChannelID = this.Channel.BroadcasterUserID.ToString();
            this.ChannelLink = $"https://kick.com/{this.Channel.Slug}";

            this.Streamer = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Kick, platformID: this.StreamerID);
            if (this.Streamer == null)
            {
                this.Streamer = await ServiceManager.Get<UserService>().CreateUser(new KickUserPlatformV2Model(this.StreamerModel));
            }

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
            if (this.BotModel == null)
            {
                return new Result("Failed to get Kick bot data");
            }

            this.BotID = this.BotModel.UserID.ToString();
            this.BotUsername = this.BotModel.Name;
            this.BotAvatarURL = this.BotModel.ProfilePicture;

            this.Bot = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Kick, platformID: this.BotID);
            if (this.Bot == null)
            {
                this.Bot = await ServiceManager.Get<UserService>().CreateUser(new KickUserPlatformV2Model(this.BotModel));
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
            this.Channel = await this.StreamerService.GetCurrentChannel();
            if (this.Channel == null)
            {
                return new Result("Failed to refresh Kick channel data");
            }

            this.IsLive = this.Channel.Stream?.IsLive ?? false;
            this.StreamTitle = this.Channel.StreamTitle;
            this.StreamCategoryID = this.Channel.Category?.ID.ToString();
            this.StreamCategoryName = this.Channel.Category?.Name;
            this.StreamCategoryImageURL = this.Channel.Category?.Thumbnail;
            this.StreamViewerCount = this.Channel.Stream?.ViewerCount ?? 0;

            if (this.IsLive &&
                !string.IsNullOrWhiteSpace(this.Channel.Stream?.StartTime) &&
                !string.Equals(this.Channel.Stream.StartTime, "0001-01-01T00:00:00Z", StringComparison.OrdinalIgnoreCase))
            {
                this.StreamStart = DateTimeOffsetExtensions.FromGeneralString(this.Channel.Stream.StartTime);
            }
            else
            {
                this.StreamStart = DateTimeOffset.MinValue;
            }

            return new Result();
        }

        public override async Task<Result> SetStreamTitle(string title)
        {
            await this.StreamerService.UpdateChannel(title: title);
            return await this.RefreshDetails();
        }

        public override Task<Result> SetStreamCategory(string category)
        {
            return Task.FromResult(new Result());
        }

        public override async Task SendMessage(string message, bool sendAsStreamer = false)
        {
            foreach (string m in this.SplitLargeMessage(message))
            {
                if (!sendAsStreamer && this.IsBotConnected && this.BotService.IsConnected)
                {
                    await this.BotService.SendChatMessage(m, isBot: true, broadcasterUserID: this.Channel.BroadcasterUserID);
                }
                else
                {
                    await this.StreamerService.SendChatMessage(m, isBot: false, broadcasterUserID: this.Channel.BroadcasterUserID);
                }
            }
        }

        public override Task DeleteMessage(ChatMessageViewModel message)
        {
            return Task.CompletedTask;
        }

        public override Task ClearMessages()
        {
            return Task.CompletedTask;
        }

        public override async Task TimeoutUser(UserV2ViewModel user, int durationInSeconds, string reason = null)
        {
            if (long.TryParse(user.PlatformID, out long userID) && long.TryParse(this.ChannelID, out long channelID))
            {
                int durationInMinutes = Math.Max(1, (int)Math.Ceiling(durationInSeconds / 60.0));
                await this.StreamerService.TimeoutUser(channelID, userID, durationInMinutes, reason);
            }
        }

        public override Task ModUser(UserV2ViewModel user)
        {
            return Task.CompletedTask;
        }

        public override Task UnmodUser(UserV2ViewModel user)
        {
            return Task.CompletedTask;
        }

        public override async Task BanUser(UserV2ViewModel user, string reason = null)
        {
            if (long.TryParse(user.PlatformID, out long userID) && long.TryParse(this.ChannelID, out long channelID))
            {
                await this.StreamerService.BanUser(channelID, userID, reason);
            }
        }

        public override async Task UnbanUser(UserV2ViewModel user)
        {
            if (long.TryParse(user.PlatformID, out long userID) && long.TryParse(this.ChannelID, out long channelID))
            {
                await this.StreamerService.UnbanUser(channelID, userID);
            }
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

        public void ApplyMetadataUpdate(string title, long? categoryID, string categoryName, string categoryThumbnail)
        {
            this.StreamTitle = title;
            this.StreamCategoryID = categoryID?.ToString();
            this.StreamCategoryName = categoryName;
            this.StreamCategoryImageURL = categoryThumbnail;
        }

    }
}


