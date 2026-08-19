using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace MixItUp.Base.Model.VPZone.Streams
{
    /// <summary>
    /// A VPZone channel. GET /channels/{slug} is the read side and PATCH /channels/{slug} the write
    /// side, so this doubles as the session's live view of title, category, tags and viewer count.
    /// </summary>
    public class VPZoneChannelModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>Free text matched case-insensitively against the game catalog on write.</summary>
        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("game_id")]
        public int? GameID { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        [JsonProperty("is_live")]
        public bool IsLive { get; set; }

        [JsonProperty("viewer_count")]
        public int ViewerCount { get; set; }

        [JsonProperty("cover_url")]
        public string CoverUrl { get; set; }

        [JsonProperty("owner")]
        public VPZoneChannelOwnerModel Owner { get; set; }
    }

    public class VPZoneChannelOwnerModel
    {
        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonProperty("is_verified")]
        public bool IsVerified { get; set; }
    }

    /// <summary>A past or running stream session on a channel.</summary>
    public class VPZoneStreamModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("started_at")]
        public string StartedAt { get; set; }

        [JsonProperty("ended_at")]
        public string EndedAt { get; set; }

        [JsonProperty("peak_viewers")]
        public int PeakViewers { get; set; }

        [JsonProperty("thumbnail_url")]
        public string ThumbnailUrl { get; set; }
    }

    /// <summary>
    /// A category from GET /categories, ordered by live stream count descending. VPZone keys a
    /// channel's category on the display name rather than the slug, so Name is what gets written back.
    /// </summary>
    public class VPZoneCategoryModel
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("cover_url")]
        public string CoverUrl { get; set; }

        [JsonProperty("stream_count")]
        public int StreamCount { get; set; }

        [JsonProperty("live_count")]
        public int LiveCount { get; set; }

        [JsonProperty("live_viewers")]
        public int LiveViewers { get; set; }

        public static IEnumerable<VPZoneCategoryModel> ParseList(JToken response)
        {
            List<VPZoneCategoryModel> categories = new List<VPZoneCategoryModel>();

            JArray array = Users.VPZoneResponseEnvelope.Unwrap(response) as JArray;
            if (array != null)
            {
                foreach (JToken item in array)
                {
                    VPZoneCategoryModel category = null;
                    try
                    {
                        category = item?.ToObject<VPZoneCategoryModel>();
                    }
                    catch (JsonException) { }

                    if (!string.IsNullOrWhiteSpace(category?.Name))
                    {
                        categories.Add(category);
                    }
                }
            }

            return categories;
        }
    }

    /// <summary>A clip from GET /channels/{slug}/clips/recent, including automatic hype clips.</summary>
    public class VPZoneClipModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("creator")]
        public VPZoneClipCreatorModel Creator { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        /// <summary>Clip length in seconds.</summary>
        [JsonProperty("duration")]
        public int Duration { get; set; }

        [JsonProperty("thumbnail_url")]
        public string ThumbnailUrl { get; set; }

        [JsonProperty("views")]
        public int Views { get; set; }
    }

    public class VPZoneClipCreatorModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }
    }

    /// <summary>
    /// GET /channels/{slug}/dashboard - the aggregate counters the special identifiers read from.
    /// </summary>
    public class VPZoneChannelDashboardModel
    {
        [JsonProperty("channel")]
        public VPZoneChannelModel Channel { get; set; }

        [JsonProperty("stats")]
        public VPZoneChannelStatsModel Stats { get; set; }

        [JsonProperty("recent_streams")]
        public List<VPZoneStreamModel> RecentStreams { get; set; } = new List<VPZoneStreamModel>();

        [JsonProperty("live_analytics")]
        public List<VPZoneChannelAnalyticsPointModel> LiveAnalytics { get; set; } = new List<VPZoneChannelAnalyticsPointModel>();
    }

    public class VPZoneChannelStatsModel
    {
        [JsonProperty("total_streams")]
        public int TotalStreams { get; set; }

        [JsonProperty("peak_viewers_all_time")]
        public int PeakViewersAllTime { get; set; }

        [JsonProperty("avg_viewers")]
        public int AverageViewers { get; set; }

        [JsonProperty("total_stream_time_seconds")]
        public long TotalStreamTimeSeconds { get; set; }

        [JsonProperty("follower_count")]
        public int FollowerCount { get; set; }

        [JsonProperty("subscriber_count")]
        public int SubscriberCount { get; set; }
    }

    public class VPZoneChannelAnalyticsPointModel
    {
        [JsonProperty("ts")]
        public string Timestamp { get; set; }

        [JsonProperty("viewers")]
        public int Viewers { get; set; }

        [JsonProperty("chat_msgs")]
        public int ChatMessages { get; set; }
    }

    /// <summary>Cursor envelope shared by every keyset-paginated list endpoint.</summary>
    public class VPZonePaginationModel
    {
        [JsonProperty("cursor")]
        public string Cursor { get; set; }
    }
}
