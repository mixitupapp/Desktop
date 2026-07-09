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

    // Carries chat.message (Events WS / legacy webhook) AND the Chat WS "newMessage" shape. The three
    // delivery channels name fields differently, so every accessor unions the known variants (see the
    // migration spec 3.6): id/messageId, message/content, isMod/isModerator, isVip/channelRole,
    // color/accentColor, nested sender{} vs flat sender fields.
    public class WebhookChatMessageEventModel : WebhookEventPayloadModelBase
    {
        // The chat.message webhook identifies the message with "id"; older docs/samples used "messageId".
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("messageId")]
        public string MessageIdLegacy { get; set; }

        [JsonIgnore]
        public string MessageID { get { return Users.UserModel.FirstNonEmpty(this.Id, this.MessageIdLegacy); } }

        // Webhook / Events WS chat.message uses "message"; the Chat WS newMessage uses "content".
        [JsonProperty("message")]
        public string MessageText { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonIgnore]
        public string Message { get { return Users.UserModel.FirstNonEmpty(this.MessageText, this.Content); } }

        [JsonProperty("badges")]
        public List<string> Badges { get; set; } = new List<string>();

        // The Chat WS newMessage carries the sender's role in this channel as "channelRole"
        // (broadcaster/moderator/vip/subscriber/viewer/...); chat.message uses isMod/isVip/isSubscriber.
        [JsonProperty("channelRole")]
        public string ChannelRole { get; set; }

        // The chat.message webhook uses "isModerator"; older samples used "isMod".
        [JsonProperty("isMod")]
        public bool IsModLegacy { get; set; }

        [JsonProperty("isModerator")]
        public bool IsModerator { get; set; }

        [JsonIgnore]
        public bool IsMod { get { return this.IsModLegacy || this.IsModerator || this.ChannelRoleIs("moderator") || this.ChannelRoleIs("mod"); } }

        [JsonProperty("isVip")]
        public bool IsVipFlag { get; set; }

        [JsonIgnore]
        public bool IsVip { get { return this.IsVipFlag || this.ChannelRoleIs("vip"); } }

        [JsonProperty("isSubscriber")]
        public bool IsSubscriberFlag { get; set; }

        [JsonIgnore]
        public bool IsSubscriber { get { return this.IsSubscriberFlag || this.ChannelRoleIs("subscriber") || this.ChannelRoleIs("sub"); } }

        [JsonIgnore]
        public bool IsBroadcasterRole { get { return this.ChannelRoleIs("broadcaster") || this.ChannelRoleIs("streamer") || this.ChannelRoleIs("owner"); } }

        private bool ChannelRoleIs(string role)
        {
            return !string.IsNullOrWhiteSpace(this.ChannelRole) && string.Equals(this.ChannelRole, role, System.StringComparison.OrdinalIgnoreCase);
        }

        [JsonProperty("subscriberMonths")]
        public int? SubscriberMonths { get; set; }

        // The chat.message webhook uses "accentColor"; older samples used "color".
        [JsonProperty("color")]
        public string ColorLegacy { get; set; }

        [JsonProperty("accentColor")]
        public string AccentColor { get; set; }

        [JsonIgnore]
        public string Color { get { return Users.UserModel.FirstNonEmpty(this.ColorLegacy, this.AccentColor); } }

        [JsonProperty("avatarUrl")]
        public string AvatarUrl { get; set; }

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
                WebhookUserModel user = WebhookUserModel.FirstValid(this.Sender, this.User);
                if (user == null)
                {
                    // chat.message delivers the sender as flat fields (id/username/avatarUrl/accentColor),
                    // not a nested object, so build the reference from those.
                    user = WebhookUserModel.FromFlatFields(this.UserId, this.Username, this.DisplayName, this.AvatarUrl, this.Color);
                }
                if (user != null)
                {
                    if (string.IsNullOrWhiteSpace(user.Color)) { user.Color = this.Color; }
                    if (string.IsNullOrWhiteSpace(user.AvatarUrl)) { user.AvatarUrl = this.AvatarUrl; }
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
        public int? StreakLegacy { get; set; }

        [JsonProperty("streakMonths")]
        public int? StreakMonths { get; set; }

        [JsonIgnore]
        public int? Streak { get { return this.StreakMonths ?? this.StreakLegacy; } }

        [JsonProperty("tier")]
        public string Tier { get; set; }

        [JsonProperty("isRenewal")]
        public bool? IsRenewal { get; set; }

        // Gifted subs also arrive as channel.subscribe with isGift=true (alongside
        // channel.subscription.gift); HandleSubscribe skips those to avoid double-processing.
        [JsonProperty("isGift")]
        public bool? IsGift { get; set; }

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

        [JsonProperty("giftCount")]
        public int? GiftCount { get; set; }

        // Legacy/fallback count field; superseded by "giftCount".
        [JsonProperty("quantity")]
        public int? Quantity { get; set; }

        // Deserialized for completeness only. For gift events "amount" is NOT the sub count (see
        // ResolvedQuantity) — the count comes from "giftCount"/"quantity" or the recipient list.
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
                // "giftCount" is the authoritative number of gifted subs; the recipient list can be
                // partial. Do NOT fall back to "amount" — for gift events that is a separate value whose
                // unit is undefined (it carries a monetary value elsewhere) and would over-count.
                return System.Math.Max(this.GiftCount ?? this.Quantity ?? recipients, System.Math.Max(recipients, 1));
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

        // channel.ban carries "duration" (seconds; null for a permanent ban) and "isPermanent".
        [JsonProperty("duration")]
        public int? Duration { get; set; }

        [JsonProperty("isPermanent")]
        public bool? IsPermanent { get; set; }

        [JsonIgnore]
        public int? ResolvedDurationSeconds { get { return this.Duration ?? this.DurationSeconds; } }

        [JsonIgnore]
        public WebhookUserModel ResolvedUser { get { return WebhookUserModel.FirstValid(this.Target, this.User, this.FlatUser); } }

        // moderator.add/remove carry the affected user under "moderator" only.
        [JsonIgnore]
        public WebhookUserModel ResolvedModerator { get { return WebhookUserModel.FirstValid(this.Moderator, this.Target, this.User, this.FlatUser); } }
    }

    public class WebhookChannelPointsRedemptionEventModel : WebhookEventPayloadModelBase
    {
        [JsonProperty("redemptionId")]
        public string RedemptionID { get; set; }

        // The redemption webhook nests reward details under "reward" and puts the viewer's text in
        // "userMessage"; older samples used flat rewardId/rewardTitle/rewardCost/userInput.
        [JsonProperty("reward")]
        public WebhookChannelPointRewardModel Reward { get; set; }

        [JsonProperty("rewardId")]
        public string RewardIdLegacy { get; set; }

        [JsonProperty("rewardTitle")]
        public string RewardTitleLegacy { get; set; }

        [JsonProperty("rewardCost")]
        public int? RewardCostLegacy { get; set; }

        [JsonProperty("userInput")]
        public string UserInputLegacy { get; set; }

        [JsonProperty("userMessage")]
        public string UserMessage { get; set; }

        [JsonIgnore]
        public string RewardID { get { return Users.UserModel.FirstNonEmpty(this.Reward?.ID, this.RewardIdLegacy); } }

        [JsonIgnore]
        public string RewardTitle { get { return Users.UserModel.FirstNonEmpty(this.Reward?.Name, this.RewardTitleLegacy); } }

        [JsonIgnore]
        public int RewardCost { get { return this.Reward?.Cost ?? this.RewardCostLegacy ?? 0; } }

        [JsonIgnore]
        public string UserInput { get { return Users.UserModel.FirstNonEmpty(this.UserMessage, this.UserInputLegacy); } }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("redeemedAt")]
        public string RedeemedAt { get; set; }

        [JsonProperty("redeemer")]
        public WebhookUserModel Redeemer { get; set; }

        [JsonIgnore]
        public WebhookUserModel ResolvedUser { get { return WebhookUserModel.FirstValid(this.Redeemer, this.User, this.FlatUser); } }
    }

    public class WebhookChannelPointRewardModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("cost")]
        public int? Cost { get; set; }
    }

    public class WebhookStreamEventModel : WebhookEventPayloadModelBase
    {
        // stream.* events nest the stream details under "stream" (category under "stream.category");
        // the flat fields below are retained as fallbacks for older payloads / test events.
        [JsonProperty("stream")]
        public WebhookStreamDetailsModel Stream { get; set; }

        [JsonProperty("streamId")]
        public string StreamIdLegacy { get; set; }

        [JsonProperty("title")]
        public string TitleLegacy { get; set; }

        [JsonProperty("categoryName")]
        public string CategoryNameLegacy { get; set; }

        [JsonProperty("categorySlug")]
        public string CategorySlugLegacy { get; set; }

        [JsonProperty("category")]
        public WebhookStreamCategoryModel CategoryLegacy { get; set; }

        [JsonProperty("startedAt")]
        public string StartedAtLegacy { get; set; }

        [JsonProperty("endedAt")]
        public string EndedAtLegacy { get; set; }

        [JsonProperty("viewerCount")]
        public int? ViewerCount { get; set; }

        [JsonIgnore]
        public string StreamID { get { return Users.UserModel.FirstNonEmpty(this.Stream?.ID, this.StreamIdLegacy); } }

        [JsonIgnore]
        public string Title { get { return Users.UserModel.FirstNonEmpty(this.Stream?.Title, this.TitleLegacy); } }

        [JsonIgnore]
        public string StartedAt { get { return Users.UserModel.FirstNonEmpty(this.Stream?.StartedAt, this.StartedAtLegacy); } }

        [JsonIgnore]
        public string ResolvedCategoryName { get { return Users.UserModel.FirstNonEmpty(this.Stream?.Category?.Name, this.CategoryLegacy?.Name, this.CategoryNameLegacy); } }

        [JsonIgnore]
        public string ResolvedCategorySlug { get { return Users.UserModel.FirstNonEmpty(this.Stream?.Category?.Slug, this.CategoryLegacy?.Slug, this.CategorySlugLegacy); } }
    }

    public class WebhookStreamDetailsModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("category")]
        public WebhookStreamCategoryModel Category { get; set; }

        [JsonProperty("startedAt")]
        public string StartedAt { get; set; }

        [JsonProperty("endedAt")]
        public string EndedAt { get; set; }

        [JsonProperty("updatedAt")]
        public string UpdatedAt { get; set; }
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
