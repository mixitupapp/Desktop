using MixItUp.Base.Model;
using MixItUp.Base.Model.Velora.Webhooks;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Velora.New;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.User;
using System;

namespace MixItUp.Base.ViewModel.Chat.Velora
{
    public class VeloraChatMessageViewModel : UserChatMessageViewModel
    {
        public string ReplyThreadID { get; set; }

        public VeloraChatMessageViewModel(WebhookChatMessageEventModel message, UserV2ViewModel user)
            : base(string.IsNullOrWhiteSpace(message?.MessageID) ? Guid.NewGuid().ToString() : message.MessageID, StreamingPlatformTypeEnum.Velora, user)
        {
            this.ReplyThreadID = message?.ReplyTo?.MessageID;

            if (!string.IsNullOrWhiteSpace(message?.Timestamp))
            {
                this.Timestamp = DateTimeOffsetExtensions.FromGeneralString(message.Timestamp);
            }

            this.ProcessMessageContents(message?.Message);
        }

        public VeloraChatMessageViewModel(UserV2ViewModel user, string message)
            : base(string.Empty, StreamingPlatformTypeEnum.Velora, user)
        {
            this.ProcessMessageContents(message);
        }

        private void ProcessMessageContents(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            // Velora emotes are sent as plain-text codes within the message and are resolved
            // against the global + channel emote list loaded when the session connects.
            var emotes = ServiceManager.Get<VeloraSession>()?.Emotes;
            foreach (string part in message.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                this.AddStringMessagePart(part);
                if (emotes != null && emotes.TryGetValue(part, out VeloraChatEmoteViewModel emote))
                {
                    this.MessageParts[this.MessageParts.Count - 1] = emote;
                }
            }
        }
    }
}
