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
        private static readonly Regex EmoteRegex = new Regex(@"\[emote:(?<id>\d+):(?<name>[^\]]+)\]", RegexOptions.Compiled);

        public string ReplyThreadID { get; set; }

        public KickChatMessageViewModel(WebhookChatMessageEventModel message, UserV2ViewModel user)
            : base(string.IsNullOrWhiteSpace(message?.MessageID) ? Guid.NewGuid().ToString() : message.MessageID, StreamingPlatformTypeEnum.Kick, user)
        {
            this.ReplyThreadID = message?.RepliesTo?.MessageID;

            if (!string.IsNullOrWhiteSpace(message?.CreatedAt))
            {
                this.Timestamp = DateTimeOffsetExtensions.FromGeneralString(message.CreatedAt);
            }

            this.ProcessMessageContents(message?.Content);
        }

        public KickChatMessageViewModel(UserV2ViewModel user, string message)
            : base(string.Empty, StreamingPlatformTypeEnum.Kick, user)
        {
            this.ProcessMessageContents(message);
        }

        private void ProcessMessageContents(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            MatchCollection matches = EmoteRegex.Matches(message);
            if (matches.Count > 0)
            {
                int currentIndex = 0;
                foreach (Match match in matches)
                {
                    if (match.Index > currentIndex)
                    {
                        string textBefore = message.Substring(currentIndex, match.Index - currentIndex).Trim();
                        foreach (string part in textBefore.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            this.AddStringMessagePart(part);
                        }
                    }

                    string emoteId = match.Groups["id"].Value;
                    string emoteName = match.Groups["name"].Value;
                    this.AddStringMessagePart(emoteName);
                    this.MessageParts[this.MessageParts.Count - 1] = new KickChatEmoteViewModel(emoteId, emoteName);

                    currentIndex = match.Index + match.Length;
                }

                if (currentIndex < message.Length)
                {
                    string remaining = message.Substring(currentIndex).Trim();
                    foreach (string part in remaining.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        this.AddStringMessagePart(part);
                    }
                }
            }
            else
            {
                foreach (string part in message.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    this.AddStringMessagePart(part);
                }
            }
        }
    }
}
