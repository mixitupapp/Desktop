using Newtonsoft.Json;
using System.Collections.Generic;

namespace MixItUp.Base.Model.Kick.Webhooks
{
    public class WebhookChannelSubscriptionRenewalEventModel
    {
        [JsonProperty("broadcaster")]
        public WebhookUserReferenceModel Broadcaster { get; set; }

        [JsonProperty("subscriber")]
        public WebhookUserReferenceModel Subscriber { get; set; }

        [JsonProperty("duration")]
        public int Duration { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("expires_at")]
        public string ExpiresAt { get; set; }
    }

    public class WebhookChannelSubscriptionNewEventModel
    {
        [JsonProperty("broadcaster")]
        public WebhookUserReferenceModel Broadcaster { get; set; }

        [JsonProperty("subscriber")]
        public WebhookUserReferenceModel Subscriber { get; set; }

        [JsonProperty("duration")]
        public int Duration { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("expires_at")]
        public string ExpiresAt { get; set; }
    }

    public class WebhookChannelSubscriptionGiftsEventModel
    {
        [JsonProperty("broadcaster")]
        public WebhookUserReferenceModel Broadcaster { get; set; }

        [JsonProperty("gifter")]
        public WebhookUserReferenceModel Gifter { get; set; }

        [JsonProperty("giftees")]
        public List<WebhookUserReferenceModel> Giftees { get; set; } = new List<WebhookUserReferenceModel>();

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("expires_at")]
        public string ExpiresAt { get; set; }
    }
}
