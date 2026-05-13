using Newtonsoft.Json;

namespace MixItUp.Base.Model.Kick.Webhooks
{
    public class WebhookKicksGiftedEventModel
    {
        [JsonProperty("broadcaster")]
        public WebhookUserReferenceModel Broadcaster { get; set; }

        [JsonProperty("sender")]
        public WebhookUserReferenceModel Sender { get; set; }

        [JsonProperty("gift")]
        public WebhookGiftModel Gift { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }
    }

    public class WebhookGiftModel
    {
        [JsonProperty("amount")]
        public int Amount { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("tier")]
        public string Tier { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("pinned_time_seconds")]
        public int PinnedTimeSeconds { get; set; }
    }
}
