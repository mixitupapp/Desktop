using Newtonsoft.Json;

namespace MixItUp.Base.Model.Kick.Channels
{
    public class KickCategoryModel
    {
        [JsonProperty("id")]
        public long ID { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("thumbnail")]
        public string Thumbnail { get; set; }
    }

    public class KickStreamModel
    {
        [JsonProperty("is_live")]
        public bool IsLive { get; set; }

        [JsonProperty("start_time")]
        public string StartTime { get; set; }

        [JsonProperty("viewer_count")]
        public int ViewerCount { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("thumbnail")]
        public string Thumbnail { get; set; }
    }

    public class KickChannelModel
    {
        [JsonProperty("broadcaster_user_id")]
        public long BroadcasterUserID { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("stream_title")]
        public string StreamTitle { get; set; }

        [JsonProperty("category")]
        public KickCategoryModel Category { get; set; }

        [JsonProperty("stream")]
        public KickStreamModel Stream { get; set; }
    }
}

