using Newtonsoft.Json;

namespace MixItUp.Base.Model.Kick.Webhooks
{
    public class KickWebhookKicksGiftedEventModel
    {
        [JsonProperty("broadcaster")]
        public KickWebhookUserReferenceModel Broadcaster { get; set; }

        [JsonProperty("sender")]
        public KickWebhookUserReferenceModel Sender { get; set; }

        [JsonProperty("gift")]
        public KickWebhookGiftModel Gift { get; set; }
    }

    public class KickWebhookGiftModel
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
    }
}
