using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace MixItUp.Base.Model.VPZone.Subscriptions
{
    /// <summary>
    /// A member reference on the activity feeds (recent followers, gift senders, redemption users).
    /// VPZone reuses this shape wherever it names a member alongside a timestamp.
    /// </summary>
    public class VPZoneMemberEventModel
    {
        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonProperty("at")]
        public string At { get; set; }

        [JsonIgnore]
        public string BestDisplayName { get { return Users.VPZoneUserModel.FirstNonEmpty(this.DisplayName, this.Username); } }
    }

    /// <summary>
    /// An active subscriber from GET /channels/{slug}/subscriptions/recent, with tier and gift
    /// attribution. gift_sender is populated only when is_gift is true.
    /// </summary>
    public class VPZoneSubscriberModel
    {
        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonProperty("tier")]
        public string Tier { get; set; }

        [JsonProperty("is_gift")]
        public bool IsGift { get; set; }

        [JsonProperty("gift_sender")]
        public VPZoneMemberEventModel GiftSender { get; set; }

        [JsonProperty("at")]
        public string At { get; set; }

        [JsonIgnore]
        public string BestDisplayName { get { return Users.VPZoneUserModel.FirstNonEmpty(this.DisplayName, this.Username); } }

        [JsonIgnore]
        public int TierNumber { get { return Realtime.VPZoneChatEventModel.ParseTierNumber(this.Tier); } }

        [JsonIgnore]
        public string GifterName { get { return this.GiftSender?.BestDisplayName; } }
    }

    /// <summary>An incoming raid from GET /channels/{slug}/raids/recent.</summary>
    public class VPZoneRaidModel
    {
        [JsonProperty("source_channel")]
        public VPZoneRaidSourceModel SourceChannel { get; set; }

        [JsonProperty("viewer_count")]
        public int ViewerCount { get; set; }

        [JsonProperty("at")]
        public string At { get; set; }
    }

    public class VPZoneRaidSourceModel
    {
        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonIgnore]
        public string BestDisplayName { get { return Users.VPZoneUserModel.FirstNonEmpty(this.DisplayName, this.Slug); } }
    }

    /// <summary>A Pixel cheer from GET /channels/{slug}/pixels/recent. One Pixel is one cent CAD of streamer value.</summary>
    public class VPZonePixelCheerModel
    {
        [JsonProperty("from")]
        public VPZoneMemberEventModel From { get; set; }

        [JsonProperty("amount")]
        public int Amount { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("at")]
        public string At { get; set; }
    }

    /// <summary>
    /// GET /channels/{slug}/activity/recent - the single most recent follower, active subscriber and
    /// raid, in one call. Each field is either the event or null.
    /// </summary>
    public class VPZoneRecentActivityModel
    {
        [JsonProperty("last_follower")]
        public VPZoneMemberEventModel LastFollower { get; set; }

        [JsonProperty("last_subscriber")]
        public VPZoneSubscriberModel LastSubscriber { get; set; }

        [JsonProperty("last_raid")]
        public VPZoneRaidModel LastRaid { get; set; }
    }

    /// <summary>
    /// A keyset-paginated list response: { data: [...], pagination: { cursor } }. Pass the cursor back
    /// as ?before= to walk backwards through the history.
    /// </summary>
    public class VPZonePagedListModel<T>
    {
        [JsonProperty("data")]
        public List<T> Data { get; set; } = new List<T>();

        [JsonProperty("pagination")]
        public Streams.VPZonePaginationModel Pagination { get; set; }

        [JsonIgnore]
        public string Cursor { get { return this.Pagination?.Cursor; } }

        public static VPZonePagedListModel<T> Parse(JToken response)
        {
            if (response is JObject obj)
            {
                try
                {
                    return obj.ToObject<VPZonePagedListModel<T>>();
                }
                catch (JsonException) { }
            }
            return new VPZonePagedListModel<T>();
        }
    }
}
