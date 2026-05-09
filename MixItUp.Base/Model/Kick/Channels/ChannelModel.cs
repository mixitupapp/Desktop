using Newtonsoft.Json;
using System.Collections.Generic;

namespace MixItUp.Base.Model.Kick.Channels
{
    public class CategoryModel
    {
        [JsonProperty("id")]
        public long ID { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("thumbnail")]
        public string Thumbnail { get; set; }
    }

    public class StreamModel
    {
        [JsonProperty("custom_tags")]
        public List<string> CustomTags { get; set; }

        [JsonProperty("is_live")]
        public bool IsLive { get; set; }

        [JsonProperty("is_mature")]
        public bool IsMature { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("start_time")]
        public string StartTime { get; set; }

        [JsonProperty("viewer_count")]
        public int ViewerCount { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("thumbnail")]
        public string Thumbnail { get; set; }
    }

    public class ChannelModel
    {
        [JsonProperty("active_subscribers_count")]
        public int ActiveSubscribersCount { get; set; }

        [JsonProperty("banner_picture")]
        public string BannerPicture { get; set; }

        [JsonProperty("broadcaster_user_id")]
        public long BroadcasterUserID { get; set; }

        [JsonProperty("canceled_subscribers_count")]
        public int CanceledSubscribersCount { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("channel_description")]
        public string ChannelDescription { get; set; }

        [JsonProperty("stream_title")]
        public string StreamTitle { get; set; }

        [JsonProperty("category")]
        public CategoryModel Category { get; set; }

        [JsonProperty("stream")]
        public StreamModel Stream { get; set; }
    }
}

