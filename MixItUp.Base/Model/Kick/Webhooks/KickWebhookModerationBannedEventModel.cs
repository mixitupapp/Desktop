using Newtonsoft.Json;

namespace MixItUp.Base.Model.Kick.Webhooks
{
    public class KickWebhookModerationBannedEventModel
    {
        [JsonProperty("broadcaster")]
        public KickWebhookUserReferenceModel Broadcaster { get; set; }

        [JsonProperty("moderator")]
        public KickWebhookUserReferenceModel Moderator { get; set; }

        [JsonProperty("banned_user")]
        public KickWebhookUserReferenceModel BannedUser { get; set; }

        [JsonProperty("metadata")]
        public KickWebhookModerationBannedMetadataModel Metadata { get; set; }
    }

    public class KickWebhookModerationBannedMetadataModel
    {
        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("expires_at")]
        public string ExpiresAt { get; set; }
    }
}
