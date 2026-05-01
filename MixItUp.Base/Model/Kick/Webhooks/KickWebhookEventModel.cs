using Newtonsoft.Json;

namespace MixItUp.Base.Model.Kick.Webhooks
{
    public class KickWebhookEventModel
    {
        [JsonProperty("EventVersion")]
        public string EventVersion { get; set; }

        [JsonProperty("MessageId")]
        public string MessageID { get; set; }

        [JsonProperty("SubscriptionId")]
        public string SubscriptionID { get; set; }
    }
}
