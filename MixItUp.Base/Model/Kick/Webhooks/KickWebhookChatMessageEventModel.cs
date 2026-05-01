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

        [JsonProperty("broadcaster")]
        public KickWebhookUserReferenceModel Broadcaster { get; set; }

        [JsonProperty("sender")]
        public KickWebhookUserReferenceModel Sender { get; set; }

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
        [JsonProperty("sender")]
        public KickWebhookUserReferenceModel Sender { get; set; }
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
