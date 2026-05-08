using MixItUp.Base.Model.Kick.Channels;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace MixItUp.Base.Model.Kick.Livestreams
{
    public class LivestreamModel
    {
        [JsonProperty("broadcaster_user_id")]
        public long BroadcasterUserID { get; set; }

        [JsonProperty("category")]
        public CategoryModel Category { get; set; }

        [JsonProperty("channel_id")]
        public long ChannelID { get; set; }

        [JsonProperty("custom_tags")]
        public List<string> CustomTags { get; set; }

        [JsonProperty("has_mature_content")]
        public bool HasMatureContent { get; set; }

        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("profile_picture")]
        public string ProfilePicture { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("started_at")]
        public string StartedAt { get; set; }

        [JsonProperty("stream_title")]
        public string StreamTitle { get; set; }

        [JsonProperty("thumbnail")]
        public string Thumbnail { get; set; }

        [JsonProperty("viewer_count")]
        public int ViewerCount { get; set; }
    }

    public class LivestreamStatsModel
    {
        [JsonProperty("total_count")]
        public int TotalCount { get; set; }
    }
}
