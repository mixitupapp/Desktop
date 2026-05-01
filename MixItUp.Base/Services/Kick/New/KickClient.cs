using MixItUp.Base.Model;
using MixItUp.Base.Model.Kick.Users;
using MixItUp.Base.Model.Kick.Webhooks;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat.Kick;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.Kick.New
{
    public class KickClient : ServiceClientBase
    {
        public override bool IsConnected { get { return this.isConnected; } }
        private bool isConnected;

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

        public async Task HandleWebhookEvent(string eventType, JObject payload, KickWebhookEventModel metadata = null)
        {
            try
            {
                if (!this.IsConnected || string.IsNullOrWhiteSpace(eventType) || payload == null)
                {
                    return;
                }

                if (!string.Equals(eventType, "chat.message.sent", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                KickWebhookChatMessageEventModel messageEvent = payload.ToObject<KickWebhookChatMessageEventModel>();
                if (messageEvent?.Sender == null || messageEvent.Sender.UserID <= 0 || string.IsNullOrWhiteSpace(messageEvent.Content))
                {
                    return;
                }

                KickUserModel kickUser = new KickUserModel()
                {
                    UserID = messageEvent.Sender.UserID,
                    Name = messageEvent.Sender.Username,
                    ProfilePicture = messageEvent.Sender.ProfilePicture,
                };

                UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Kick, platformID: kickUser.UserID.ToString(), platformUsername: kickUser.Name);
                if (user == null)
                {
                    user = await ServiceManager.Get<UserService>().CreateUser(new KickUserPlatformV2Model(kickUser));
                }
                else
                {
                    KickUserPlatformV2Model platformData = user.GetPlatformData<KickUserPlatformV2Model>(StreamingPlatformTypeEnum.Kick);
                    platformData?.SetUserProperties(kickUser);
                }

                if (user == null)
                {
                    return;
                }

                await ServiceManager.Get<ChatService>().AddMessage(new KickChatMessageViewModel(messageEvent, user));
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }
    }
}
