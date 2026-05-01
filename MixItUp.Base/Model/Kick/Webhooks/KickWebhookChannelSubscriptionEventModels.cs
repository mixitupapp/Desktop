using Newtonsoft.Json;
using System.Collections.Generic;

namespace MixItUp.Base.Model.Kick.Webhooks
{
    public class KickWebhookChannelSubscriptionRenewalEventModel
    {
        [JsonProperty("broadcaster")]
        public KickWebhookUserReferenceModel Broadcaster { get; set; }

        [JsonProperty("subscriber")]
        public KickWebhookUserReferenceModel Subscriber { get; set; }

        [JsonProperty("duration")]
        public int Duration { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("expires_at")]
        public string ExpiresAt { get; set; }
    }

    public class KickWebhookChannelSubscriptionNewEventModel
    {
        [JsonProperty("broadcaster")]
        public KickWebhookUserReferenceModel Broadcaster { get; set; }

        [JsonProperty("subscriber")]
        public KickWebhookUserReferenceModel Subscriber { get; set; }

        [JsonProperty("duration")]
        public int Duration { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("expires_at")]
        public string ExpiresAt { get; set; }
    }

    public class KickWebhookChannelSubscriptionGiftsEventModel
    {
        [JsonProperty("broadcaster")]
        public KickWebhookUserReferenceModel Broadcaster { get; set; }

        [JsonProperty("gifter")]
        public KickWebhookUserReferenceModel Gifter { get; set; }

        [JsonProperty("giftees")]
        public List<KickWebhookUserReferenceModel> Giftees { get; set; } = new List<KickWebhookUserReferenceModel>();

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("expires_at")]
        public string ExpiresAt { get; set; }
    }
}
