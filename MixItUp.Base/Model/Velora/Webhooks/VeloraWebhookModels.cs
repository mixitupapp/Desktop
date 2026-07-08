using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace MixItUp.Base.Model.Velora.Webhooks
{
    /// <summary>
    /// Relay metadata forwarded by the Mix It Up Desktop API alongside each Velora webhook event.
    /// </summary>
    public class WebhookEventModel
    {
        [JsonProperty("MessageId")]
        public string MessageID { get; set; }

        [JsonProperty("EventType")]
        public string EventType { get; set; }

        [JsonProperty("Timestamp")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// A user reference inside a Velora webhook payload. Payload shapes vary between flat fields
    /// (userId/username/displayName) and nested objects (follower/subscriber/gifter/etc.), so every
    /// event model resolves to this common form.
    /// </summary>
    public class WebhookUserModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("avatarUrl")]
        public string AvatarUrl { get; set; }

        [JsonProperty("avatar")]
        public string Avatar { get; set; }

        [JsonProperty("profilePicture")]
        public string ProfilePicture { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("isAnonymous")]
        public bool IsAnonymous { get; set; }

        [JsonIgnore]
        public string UserID { get { return Users.UserModel.FirstNonEmpty(this.ID, this.UserId); } }

        [JsonIgnore]
        public string BestAvatarUrl { get { return Users.UserModel.FirstNonEmpty(this.AvatarUrl, this.Avatar, this.ProfilePicture); } }

        [JsonIgnore]
        public bool IsValid { get { return !string.IsNullOrWhiteSpace(this.UserID) || !string.IsNullOrWhiteSpace(this.Username); } }

        public static WebhookUserModel FromFlatFields(string userID, string username, string displayName, string avatarUrl = null, string color = null)
        {
            if (string.IsNullOrWhiteSpace(userID) && string.IsNullOrWhiteSpace(username))
            {
                return null;
            }
            return new WebhookUserModel() { ID = userID, Username = username, DisplayName = displayName, AvatarUrl = avatarUrl, Color = color };
        }

        public static WebhookUserModel FirstValid(params WebhookUserModel[] users)
        {
            return users?.FirstOrDefault(u => u != null && u.IsValid);
        }
    }

    public abstract class WebhookEventPayloadModelBase
    {
        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("channel")]
        public WebhookUserModel Channel { get; set; }

        [JsonProperty("user")]
        public WebhookUserModel User { get; set; }

        [JsonProperty("isTest")]
        public bool IsTest { get; set; }

        [JsonIgnore]
        public WebhookUserModel FlatUser { get { return WebhookUserModel.FromFlatFields(this.UserId, this.Username, this.DisplayName); } }
    }

    public class WebhookChatMessageEventModel : WebhookEventPayloadModelBase
    {
        [JsonProperty("messageId")]
        public string MessageID { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("badges")]
        public List<string> Badges { get; set; } = new List<string>();

        [JsonProperty("isMod")]
        public bool IsMod { get; set; }

        [JsonProperty("isVip")]
        public bool IsVip { get; set; }

        [JsonProperty("isSubscriber")]
        public bool IsSubscriber { get; set; }

        [JsonProperty("subscriberMonths")]
        public int? SubscriberMonths { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("isSystem")]
        public bool? IsSystem { get; set; }

        [JsonProperty("isBot")]
        public bool? IsBot { get; set; }

        [JsonProperty("sender")]
        public WebhookUserModel Sender { get; set; }

        [JsonProperty("replyTo")]
        public WebhookChatMessageReplyModel ReplyTo { get; set; }

        [JsonProperty("card")]
        public WebhookChatMessageCardModel Card { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }

        [JsonIgnore]
        public WebhookUserModel ResolvedSender
        {
            get
            {
                WebhookUserModel user = WebhookUserModel.FirstValid(this.Sender, this.User, this.FlatUser);
                if (user != null && string.IsNullOrWhiteSpace(user.Color))
                {
                    user.Color = this.Color;
                }
                return user;
            }
        }
    }

    public class WebhookChatMessageReplyModel
    {
        [JsonProperty("messageId")]
        public string MessageID { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("snippet")]
        public string Snippet { get; set; }
    }

    public class WebhookChatMessageCardModel
    {
        [JsonProperty("type")]
        public string Type { get; set; }
    }

    public class WebhookFollowEventModel : WebhookEventPayloadModelBase
    {
        [JsonProperty("follower")]
        public WebhookUserModel Follower { get; set; }

        [JsonProperty("followedAt")]
        public string FollowedAt { get; set; }

        [JsonIgnore]
        public WebhookUserModel ResolvedUser { get { return WebhookUserModel.FirstValid(this.Follower, this.User, this.FlatUser); } }
    }

    public class WebhookSubscribeEventModel : WebhookEventPayloadModelBase
    {
        [JsonProperty("subscriber")]
        public WebhookUserModel Subscriber { get; set; }

        [JsonProperty("months")]
        public int? Months { get; set; }

        [JsonProperty("streak")]
        public int? Streak { get; set; }

        [JsonProperty("tier")]
        public string Tier { get; set; }

        [JsonProperty("isRenewal")]
        public bool? IsRenewal { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonIgnore]
        public WebhookUserModel ResolvedUser { get { return WebhookUserModel.FirstValid(this.Subscriber, this.User, this.FlatUser); } }

        [JsonIgnore]
        public int ResolvedMonths { get { return System.Math.Max(this.Months ?? 1, 1); } }

        [JsonIgnore]
        public bool IsResubscribe { get { return this.IsRenewal ?? (this.ResolvedMonths > 1); } }

        [JsonIgnore]
        public int TierNumber { get { return ParseTierNumber(this.Tier); } }

        public static int ParseTierNumber(string tier)
        {
            if (!string.IsNullOrWhiteSpace(tier))
            {
                string digits = new string(tier.Where(char.IsDigit).ToArray());
                if (int.TryParse(digits, out int result) && result > 0)
                {
                    return result;
                }
            }
            return 1;
        }
    }

    public class WebhookSubscriptionGiftEventModel : WebhookEventPayloadModelBase
    {
        [JsonProperty("gifter")]
        public WebhookUserModel Gifter { get; set; }

        [JsonProperty("quantity")]
        public int? Quantity { get; set; }

        [JsonProperty("amount")]
        public int? Amount { get; set; }

        [JsonProperty("tier")]
        public string Tier { get; set; }

        [JsonProperty("recipients")]
        public List<WebhookUserModel> Recipients { get; set; } = new List<WebhookUserModel>();

        [JsonProperty("recipient")]
        public WebhookUserModel Recipient { get; set; }

        [JsonIgnore]
        public WebhookUserModel ResolvedGifter { get { return WebhookUserModel.FirstValid(this.Gifter, this.User, this.FlatUser); } }

        [JsonIgnore]
        public List<WebhookUserModel> ResolvedRecipients
        {
            get
            {
                List<WebhookUserModel> recipients = new List<WebhookUserModel>();
                if (this.Recipients != null)
                {
                    recipients.AddRange(this.Recipients.Where(r => r != null && r.IsValid));
                }
                if (this.Recipient != null && this.Recipient.IsValid)
                {
                    recipients.Add(this.Recipient);
                }
                return recipients;
            }
        }

        [JsonIgnore]
        public int ResolvedQuantity
        {
            get
            {
                int recipients = this.ResolvedRecipients.Count;
                return System.Math.Max(this.Quantity ?? this.Amount ?? recipients, System.Math.Max(recipients, 1));
            }
        }

        [JsonIgnore]
        public int TierNumber { get { return WebhookSubscribeEventModel.ParseTierNumber(this.Tier); } }
    }

    public class WebhookSubscriptionEndEventModel : WebhookEventPayloadModelBase
    {
        [JsonProperty("subscriber")]
        public WebhookUserModel Subscriber { get; set; }

        [JsonIgnore]
        public WebhookUserModel ResolvedUser { get { return WebhookUserModel.FirstValid(this.Subscriber, this.User, this.FlatUser); } }
    }

    public class WebhookCheerEventModel : WebhookEventPayloadModelBase
    {
        [JsonProperty("sender")]
        public WebhookUserModel Sender { get; set; }

        [JsonProperty("amount")]
        public int? Amount { get; set; }

        [JsonProperty("volts")]
        public int? Volts { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonIgnore]
        public WebhookUserModel ResolvedUser { get { return WebhookUserModel.FirstValid(this.Sender, this.User, this.FlatUser); } }

        [JsonIgnore]
        public int ResolvedAmount { get { return System.Math.Max(this.Amount ?? this.Volts ?? 0, 0); } }
    }

    public class WebhookRaidEventModel : WebhookEventPayloadModelBase
    {
        [JsonProperty("raider")]
        public WebhookUserModel Raider { get; set; }

        [JsonProperty("from")]
        public WebhookUserModel From { get; set; }

        [JsonProperty("viewers")]
        public int? Viewers { get; set; }

        [JsonProperty("viewerCount")]
        public int? ViewerCount { get; set; }

        [JsonProperty("amount")]
        public int? Amount { get; set; }

        [JsonIgnore]
        public WebhookUserModel ResolvedUser { get { return WebhookUserModel.FirstValid(this.Raider, this.From, this.User, this.FlatUser); } }

        [JsonIgnore]
        public int ResolvedViewers { get { return System.Math.Max(this.Viewers ?? this.ViewerCount ?? this.Amount ?? 0, 0); } }
    }

    public class WebhookModerationEventModel : WebhookEventPayloadModelBase
    {
        [JsonProperty("target")]
        public WebhookUserModel Target { get; set; }

        [JsonProperty("moderator")]
        public WebhookUserModel Moderator { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("expiresAt")]
        public string ExpiresAt { get; set; }

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }

        [JsonProperty("durationSeconds")]
        public int? DurationSeconds { get; set; }

        [JsonIgnore]
        public WebhookUserModel ResolvedUser { get { return WebhookUserModel.FirstValid(this.Target, this.User, this.FlatUser); } }
    }

    public class WebhookChannelPointsRedemptionEventModel : WebhookEventPayloadModelBase
    {
        [JsonProperty("redemptionId")]
        public string RedemptionID { get; set; }

        [JsonProperty("rewardId")]
        public string RewardID { get; set; }

        [JsonProperty("rewardTitle")]
        public string RewardTitle { get; set; }

        [JsonProperty("rewardCost")]
        public int RewardCost { get; set; }

        [JsonProperty("userInput")]
        public string UserInput { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("redeemedAt")]
        public string RedeemedAt { get; set; }

        [JsonProperty("redeemer")]
        public WebhookUserModel Redeemer { get; set; }

        [JsonIgnore]
        public WebhookUserModel ResolvedUser { get { return WebhookUserModel.FirstValid(this.Redeemer, this.User, this.FlatUser); } }
    }

    public class WebhookStreamEventModel : WebhookEventPayloadModelBase
    {
        [JsonProperty("streamId")]
        public string StreamID { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("categoryName")]
        public string CategoryName { get; set; }

        [JsonProperty("categorySlug")]
        public string CategorySlug { get; set; }

        [JsonProperty("category")]
        public WebhookStreamCategoryModel Category { get; set; }

        [JsonProperty("startedAt")]
        public string StartedAt { get; set; }

        [JsonProperty("endedAt")]
        public string EndedAt { get; set; }

        [JsonProperty("viewerCount")]
        public int? ViewerCount { get; set; }

        [JsonIgnore]
        public string ResolvedCategoryName { get { return Users.UserModel.FirstNonEmpty(this.CategoryName, this.Category?.Name); } }

        [JsonIgnore]
        public string ResolvedCategorySlug { get { return Users.UserModel.FirstNonEmpty(this.CategorySlug, this.Category?.Slug); } }
    }

    public class WebhookStreamCategoryModel
    {
        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("imageUrl")]
        public string ImageUrl { get; set; }
    }
}
