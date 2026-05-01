using Newtonsoft.Json;
using System.Collections.Generic;

namespace MixItUp.Base.Model.Kick.Webhooks
{
    public class WebhookChatMessageEventModel
    {
        [JsonProperty("message_id")]
        public string MessageID { get; set; }

        [JsonProperty("replies_to")]
        public WebhookRepliesToModel RepliesTo { get; set; }

        [JsonProperty("broadcaster")]
        public WebhookUserReferenceModel Broadcaster { get; set; }

        [JsonProperty("sender")]
        public WebhookUserReferenceModel Sender { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("emotes")]
        public List<WebhookEmoteModel> Emotes { get; set; } = new List<WebhookEmoteModel>();
    }

    public class WebhookRepliesToModel
    {
        [JsonProperty("message_id")]
        public string MessageID { get; set; }
        [JsonProperty("sender")]
        public WebhookUserReferenceModel Sender { get; set; }
    }

    public class WebhookEmoteModel
    {
        [JsonProperty("emote_id")]
        public string EmoteID { get; set; }

        [JsonProperty("positions")]
        public List<WebhookEmotePositionModel> Positions { get; set; } = new List<WebhookEmotePositionModel>();
    }

    public class WebhookEmotePositionModel
    {
        [JsonProperty("s")]
        public int Start { get; set; }

        [JsonProperty("e")]
        public int End { get; set; }
    }
}
