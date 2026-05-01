using MixItUp.Base.Model;
using MixItUp.Base.Model.Kick.Webhooks;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.User;
using System;
using System.Text.RegularExpressions;

namespace MixItUp.Base.ViewModel.Chat.Kick
{
    public class KickChatMessageViewModel : UserChatMessageViewModel
    {
        private static readonly Regex EmoteTokenRegex = new Regex(@"^\[emote:(?<id>\d+):(?<name>[^\]]+)\]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public string ReplyThreadID { get; set; }

        public KickChatMessageViewModel(KickWebhookChatMessageEventModel message, UserV2ViewModel user)
            : base(string.IsNullOrWhiteSpace(message?.MessageID) ? Guid.NewGuid().ToString() : message.MessageID, StreamingPlatformTypeEnum.Kick, user)
        {
            this.ReplyThreadID = message?.RepliesTo?.MessageID;

            if (!string.IsNullOrWhiteSpace(message?.CreatedAt))
            {
                this.Timestamp = DateTimeOffsetExtensions.FromGeneralString(message.CreatedAt);
            }

            this.ProcessMessageContents(message?.Content);
        }

        private void ProcessMessageContents(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            foreach (string part in message.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                this.AddStringMessagePart(part);

                Match match = EmoteTokenRegex.Match(part);
                if (match.Success)
                {
                    this.MessageParts[this.MessageParts.Count - 1] = new KickChatEmoteViewModel(match.Groups["id"].Value, match.Groups["name"].Value);
                }
            }
        }
    }
}
