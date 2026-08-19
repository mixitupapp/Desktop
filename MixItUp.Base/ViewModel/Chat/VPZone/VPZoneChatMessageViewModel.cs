using MixItUp.Base.Model;
using MixItUp.Base.Model.VPZone.Realtime;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Generic;

namespace MixItUp.Base.ViewModel.Chat.VPZone
{
    public class VPZoneChatMessageViewModel : UserChatMessageViewModel
    {
        public string ReplyThreadID { get; set; }

        /// <summary>
        /// The nonce sent alongside this message, echoed back on the broadcast frame. Matching on it
        /// identifies the app's own messages without comparing their text.
        /// </summary>
        public string Nonce { get; set; }

        public VPZoneChatMessageViewModel(VPZoneChatEventModel frame, UserV2ViewModel user)
            : base(string.IsNullOrWhiteSpace(frame?.ID) ? Guid.NewGuid().ToString() : frame.ID, StreamingPlatformTypeEnum.VPZone, user)
        {
            this.ReplyThreadID = frame?.ReplyTo?.MessageID;
            this.Nonce = frame?.Nonce;

            if (frame != null && frame.Timestamp > 0)
            {
                this.Timestamp = frame.TimestampOffset;
            }

            this.ProcessMessageContents(frame?.Body, frame?.EmoteMap);
        }

        public VPZoneChatMessageViewModel(UserV2ViewModel user, string message)
            : base(string.Empty, StreamingPlatformTypeEnum.VPZone, user)
        {
            this.ProcessMessageContents(message, emoteMap: null);
        }

        /// <summary>
        /// VPZone resolves emotes per message: each msg frame carries its own emoteMap of token to
        /// image URL, already filtered to what the sender was entitled to use. There is no global emote
        /// dictionary to consult, so a message without a map renders as plain text.
        /// </summary>
        private void ProcessMessageContents(string message, IDictionary<string, string> emoteMap)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            foreach (string part in message.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                this.AddStringMessagePart(part);
                if (emoteMap != null && emoteMap.TryGetValue(part, out string imageURL) && !string.IsNullOrWhiteSpace(imageURL))
                {
                    this.MessageParts[this.MessageParts.Count - 1] = new VPZoneChatEmoteViewModel(part, imageURL);
                }
            }
        }
    }
}
