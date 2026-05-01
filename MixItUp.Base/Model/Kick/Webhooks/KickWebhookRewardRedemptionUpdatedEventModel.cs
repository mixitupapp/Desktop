using Newtonsoft.Json;

namespace MixItUp.Base.Model.Kick.Webhooks
{
    public class KickWebhookRewardRedemptionUpdatedEventModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("user_input")]
        public string UserInput { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("redeemed_at")]
        public string RedeemedAt { get; set; }

        [JsonProperty("reward")]
        public KickWebhookRewardModel Reward { get; set; }

        [JsonProperty("redeemer")]
        public KickWebhookUserReferenceModel Redeemer { get; set; }

        [JsonProperty("broadcaster")]
        public KickWebhookUserReferenceModel Broadcaster { get; set; }
    }

    public class KickWebhookRewardModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("cost")]
        public int Cost { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }
    }
}
