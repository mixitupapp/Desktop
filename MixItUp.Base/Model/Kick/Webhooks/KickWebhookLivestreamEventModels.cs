using Newtonsoft.Json;

namespace MixItUp.Base.Model.Kick.Webhooks
{
    public class KickWebhookLivestreamStatusUpdatedEventModel
    {
        [JsonProperty("broadcaster")]
        public KickWebhookUserReferenceModel Broadcaster { get; set; }

        [JsonProperty("is_live")]
        public bool IsLive { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("started_at")]
        public string StartedAt { get; set; }
    }

    public class KickWebhookLivestreamMetadataUpdatedEventModel
    {
        [JsonProperty("broadcaster")]
        public KickWebhookUserReferenceModel Broadcaster { get; set; }

        [JsonProperty("metadata")]
        public KickWebhookLivestreamMetadataModel Metadata { get; set; }
    }

    public class KickWebhookLivestreamMetadataModel
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("category")]
        public KickWebhookCategoryModel Category { get; set; }
    }

    public class KickWebhookCategoryModel
    {
        [JsonProperty("id")]
        public long ID { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("thumbnail")]
        public string Thumbnail { get; set; }
    }
}
