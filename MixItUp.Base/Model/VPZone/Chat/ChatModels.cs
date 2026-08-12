using Newtonsoft.Json;
using System.Collections.Generic;

namespace MixItUp.Base.Model.VPZone.Chat
{
    /// <summary>
    /// A stored chat message, returned by GET /channels/{slug}/chat and by POST of a new message. The
    /// id here matches the id on the corresponding realtime frame, so the two can be correlated.
    /// </summary>
    public class VPZoneChatMessageModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        /// <summary>"msg" | "announcement" | "sub" | "raid".</summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("body")]
        public string Body { get; set; }

        [JsonProperty("metadata")]
        public Newtonsoft.Json.Linq.JObject Metadata { get; set; }

        [JsonProperty("is_subscriber")]
        public bool IsSubscriber { get; set; }

        [JsonProperty("sub_months")]
        public int SubscriberMonths { get; set; }

        [JsonProperty("is_owner")]
        public bool IsOwner { get; set; }

        [JsonProperty("tier")]
        public string Tier { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }
    }

    /// <summary>GET /channels/{slug}/chat - recent history for the current stream, or the last hour offline.</summary>
    public class VPZoneChatHistoryModel
    {
        [JsonProperty("messages")]
        public List<VPZoneChatMessageModel> Messages { get; set; } = new List<VPZoneChatMessageModel>();

        [JsonProperty("channel_slug")]
        public string ChannelSlug { get; set; }

        [JsonProperty("is_live")]
        public bool IsLive { get; set; }
    }

    /// <summary>POST /channels/{slug}/chat/announcements - the highlighted announcement banner.</summary>
    public class VPZoneChatAnnouncementModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("body")]
        public string Body { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }
    }

    /// <summary>POST /channels/{slug}/chat/moderation/bans. expires_at is null for a permanent ban.</summary>
    public class VPZoneChatBanResultModel
    {
        [JsonProperty("ok")]
        public bool OK { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("expires_at")]
        public string ExpiresAt { get; set; }
    }

    /// <summary>DELETE /channels/{slug}/chat/moderation - the removed message's id comes back for confirmation.</summary>
    public class VPZoneChatDeleteResultModel
    {
        [JsonProperty("ok")]
        public bool OK { get; set; }

        [JsonProperty("message_id")]
        public string MessageID { get; set; }
    }
}
