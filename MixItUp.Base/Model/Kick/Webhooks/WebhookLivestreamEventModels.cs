using Newtonsoft.Json;

namespace MixItUp.Base.Model.Kick.Webhooks
{
    public class WebhookLivestreamStatusUpdatedEventModel
    {
        [JsonProperty("broadcaster")]
        public WebhookUserReferenceModel Broadcaster { get; set; }

        [JsonProperty("is_live")]
        public bool IsLive { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("started_at")]
        public string StartedAt { get; set; }

        [JsonProperty("ended_at")]
        public string EndedAt { get; set; }
    }

    public class WebhookLivestreamMetadataUpdatedEventModel
    {
        [JsonProperty("broadcaster")]
        public WebhookUserReferenceModel Broadcaster { get; set; }

        [JsonProperty("metadata")]
        public WebhookLivestreamMetadataModel Metadata { get; set; }
    }

    public class WebhookLivestreamMetadataModel
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("has_mature_content")]
        public bool HasMatureContent { get; set; }

        [JsonProperty("category")]
        public WebhookCategoryModel Category { get; set; }
    }

    public class WebhookCategoryModel
    {
        [JsonProperty("id")]
        public long ID { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("thumbnail")]
        public string Thumbnail { get; set; }
    }
}
