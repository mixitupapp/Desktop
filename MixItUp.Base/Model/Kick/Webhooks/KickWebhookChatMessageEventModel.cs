using Newtonsoft.Json;
using System.Collections.Generic;

namespace MixItUp.Base.Model.Kick.Webhooks
{
    public class KickWebhookChatMessageEventModel
    {
        [JsonProperty("message_id")]
        public string MessageID { get; set; }

        [JsonProperty("replies_to")]
        public KickWebhookRepliesToModel RepliesTo { get; set; }

        [JsonProperty("sender")]
        public KickWebhookSenderModel Sender { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("emotes")]
        public List<KickWebhookEmoteModel> Emotes { get; set; } = new List<KickWebhookEmoteModel>();
    }

    public class KickWebhookRepliesToModel
    {
        [JsonProperty("message_id")]
        public string MessageID { get; set; }
    }

    public class KickWebhookSenderModel
    {
        [JsonProperty("user_id")]
        public long UserID { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("profile_picture")]
        public string ProfilePicture { get; set; }
    }

    public class KickWebhookEmoteModel
    {
        [JsonProperty("emote_id")]
        public string EmoteID { get; set; }

        [JsonProperty("positions")]
        public List<KickWebhookEmotePositionModel> Positions { get; set; } = new List<KickWebhookEmotePositionModel>();
    }

    public class KickWebhookEmotePositionModel
    {
        [JsonProperty("s")]
        public int Start { get; set; }

        [JsonProperty("e")]
        public int End { get; set; }
    }
}
