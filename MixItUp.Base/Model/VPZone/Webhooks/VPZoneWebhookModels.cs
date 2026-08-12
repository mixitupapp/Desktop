using Newtonsoft.Json;
using System.Collections.Generic;

namespace MixItUp.Base.Model.VPZone.Webhooks
{
    /// <summary>
    /// The five webhook events VPZone delivers. Four of them also ride the chat gateway, so the socket
    /// is the primary path and these are the reconciliation path. subscription.cancelled is the only
    /// one with no socket equivalent: cancellations are deliberately silent in chat.
    /// </summary>
    public static class VPZoneWebhookEventTypes
    {
        public const string StreamStarted = "stream.started";
        public const string StreamEnded = "stream.ended";
        public const string ChannelFollow = "channel.follow";
        public const string SubscriptionCreated = "subscription.created";
        public const string SubscriptionCancelled = "subscription.cancelled";

        public static readonly IReadOnlyList<string> All = new List<string>()
        {
            StreamStarted,
            StreamEnded,
            ChannelFollow,
            SubscriptionCreated,
            SubscriptionCancelled,
        };
    }

    /// <summary>
    /// A registered webhook subscription. The secret is returned only on creation, so it is captured
    /// once and stored through the settings rather than re-read from the platform.
    /// </summary>
    public class VPZoneWebhookModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("events")]
        public List<string> Events { get; set; } = new List<string>();

        [JsonProperty("active")]
        public bool Active { get; set; }

        [JsonProperty("secret")]
        public string Secret { get; set; }
    }

    /// <summary>
    /// A webhook delivery relayed to the desktop app by the Mix It Up Desktop API. VPZone signs the
    /// raw body with HMAC-SHA256 and sends it in X-Streamity-Signature, which the relay verifies before
    /// forwarding, so the desktop side only ever sees deliveries that already passed that check.
    /// </summary>
    public class WebhookEventModel
    {
        [JsonProperty("event")]
        public string Event { get; set; }

        [JsonProperty("data")]
        public Newtonsoft.Json.Linq.JObject Data { get; set; }

        [JsonProperty("delivered_at")]
        public string DeliveredAt { get; set; }

        [JsonProperty("webhook_id")]
        public string WebhookID { get; set; }
    }

    /// <summary>
    /// A member reference inside a webhook payload. The payloads name the member with snake_case
    /// fields, and a follow names the follower while a subscription names the subscriber, so every
    /// event model resolves down to this common form.
    /// </summary>
    public class WebhookUserModel
    {
        [JsonProperty("user_id")]
        public string UserID { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonIgnore]
        public string BestDisplayName { get { return Users.VPZoneUserModel.FirstNonEmpty(this.DisplayName, this.Username); } }

        [JsonIgnore]
        public bool IsValid { get { return !string.IsNullOrWhiteSpace(this.UserID) || !string.IsNullOrWhiteSpace(this.Username); } }

        public static WebhookUserModel FromFlatFields(string userID, string username, string displayName, string avatarUrl = null)
        {
            if (string.IsNullOrWhiteSpace(userID) && string.IsNullOrWhiteSpace(username))
            {
                return null;
            }
            return new WebhookUserModel() { UserID = userID, Username = username, DisplayName = displayName, AvatarUrl = avatarUrl };
        }

        public static WebhookUserModel FirstValid(params WebhookUserModel[] users)
        {
            return System.Linq.Enumerable.FirstOrDefault(users ?? new WebhookUserModel[0], u => u != null && u.IsValid);
        }
    }

    public abstract class WebhookEventPayloadModelBase
    {
        [JsonProperty("channel_slug")]
        public string ChannelSlug { get; set; }

        [JsonProperty("user_id")]
        public string UserID { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonIgnore]
        public WebhookUserModel FlatUser { get { return WebhookUserModel.FromFlatFields(this.UserID, this.Username, this.DisplayName, this.AvatarUrl); } }
    }

    public class WebhookFollowEventModel : WebhookEventPayloadModelBase
    {
        [JsonProperty("follower")]
        public WebhookUserModel Follower { get; set; }

        [JsonProperty("followed_at")]
        public string FollowedAt { get; set; }

        [JsonIgnore]
        public WebhookUserModel ResolvedUser { get { return WebhookUserModel.FirstValid(this.Follower, this.FlatUser); } }
    }

    /// <summary>
    /// subscription.created and subscription.cancelled. Both now deliver to the channel owner's
    /// webhooks with subscriber_id in the payload, rather than to the subscriber's own webhooks.
    /// </summary>
    public class WebhookSubscriptionEventModel : WebhookEventPayloadModelBase
    {
        [JsonProperty("subscriber")]
        public WebhookUserModel Subscriber { get; set; }

        [JsonProperty("subscriber_id")]
        public string SubscriberID { get; set; }

        [JsonProperty("tier")]
        public string Tier { get; set; }

        [JsonProperty("is_gift")]
        public bool IsGift { get; set; }

        [JsonProperty("gift_sender")]
        public WebhookUserModel GiftSender { get; set; }

        [JsonProperty("months")]
        public int? Months { get; set; }

        [JsonProperty("at")]
        public string At { get; set; }

        [JsonIgnore]
        public WebhookUserModel ResolvedUser
        {
            get
            {
                WebhookUserModel user = WebhookUserModel.FirstValid(this.Subscriber, this.FlatUser);
                if (user == null && !string.IsNullOrWhiteSpace(this.SubscriberID))
                {
                    user = new WebhookUserModel() { UserID = this.SubscriberID };
                }
                return user;
            }
        }

        [JsonIgnore]
        public int ResolvedMonths { get { return System.Math.Max(this.Months ?? 1, 1); } }

        [JsonIgnore]
        public bool IsResubscribe { get { return this.ResolvedMonths > 1; } }

        [JsonIgnore]
        public int TierNumber { get { return Realtime.VPZoneChatEventModel.ParseTierNumber(this.Tier); } }
    }

    /// <summary>stream.started and stream.ended, which carry the stream session alongside the channel.</summary>
    public class WebhookStreamEventModel : WebhookEventPayloadModelBase
    {
        [JsonProperty("stream_id")]
        public string StreamID { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("started_at")]
        public string StartedAt { get; set; }

        [JsonProperty("ended_at")]
        public string EndedAt { get; set; }

        [JsonProperty("viewer_count")]
        public int? ViewerCount { get; set; }
    }
}
