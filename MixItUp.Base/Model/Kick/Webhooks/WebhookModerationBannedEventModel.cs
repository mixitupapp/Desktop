using Newtonsoft.Json;

namespace MixItUp.Base.Model.Kick.Webhooks
{
    public class WebhookModerationBannedEventModel
    {
        [JsonProperty("broadcaster")]
        public WebhookUserReferenceModel Broadcaster { get; set; }

        [JsonProperty("moderator")]
        public WebhookUserReferenceModel Moderator { get; set; }

        [JsonProperty("banned_user")]
        public WebhookUserReferenceModel BannedUser { get; set; }

        [JsonProperty("metadata")]
        public WebhookModerationBannedMetadataModel Metadata { get; set; }
    }

    public class WebhookModerationBannedMetadataModel
    {
        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("expires_at")]
        public string ExpiresAt { get; set; }
    }
}
