using MixItUp.Base.Model.Velora.Users;
using MixItUp.Base.Model.Velora.Webhooks;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Velora.New;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.User.Platform
{
    [DataContract]
    public class VeloraUserPlatformV2Model : UserPlatformV2ModelBase
    {
        private const string BadgeBaseURL = "https://files.mixitup.bot/static/platforms/velora/badges/";
        // The uploaded asset (and its source in Media/Platforms/Velora/Badges) is named broadcast.png,
        // unlike Kick's broadcaster.png.
        private const string BroadcasterBadgeURL = BadgeBaseURL + "broadcast.png";
        private const string ModeratorBadgeURL = BadgeBaseURL + "moderator.png";
        private const string VIPBadgeURL = BadgeBaseURL + "vip.png";
        private const string BotBadgeURL = BadgeBaseURL + "bot.png";

        // Slugs in a message's badges[] that describe roles rather than global catalog badges
        // ("creator" is Velora's platform-wide has-a-channel status, not a per-channel role).
        private static readonly HashSet<string> RoleBadgeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "broadcaster", "streamer", "owner", "creator", "moderator", "mod", "vip", "subscriber", "sub", "bot", "system", "verified",
        };

        [DataMember]
        public string Color { get; set; }

        public VeloraUserPlatformV2Model(UserModel user)
        {
            this.Platform = StreamingPlatformTypeEnum.Velora;
            this.SetUserProperties(user);
        }

        public VeloraUserPlatformV2Model(WebhookUserModel user)
        {
            this.Platform = StreamingPlatformTypeEnum.Velora;
            this.SetUserProperties(user);
        }

        public VeloraUserPlatformV2Model(string id, string username, string displayName, string avatarLink = null)
        {
            this.Platform = StreamingPlatformTypeEnum.Velora;
            this.ID = id;
            this.Username = username;
            this.DisplayName = displayName;
            if (!string.IsNullOrWhiteSpace(avatarLink))
            {
                this.AvatarLink = avatarLink;
            }
        }

        [JsonConstructor]
        public VeloraUserPlatformV2Model() : base() { }

        public override async Task Refresh()
        {
            if (ServiceManager.Get<VeloraSession>().IsConnected && !string.IsNullOrWhiteSpace(this.Username))
            {
                UserModel user = await ServiceManager.Get<VeloraSession>().StreamerService.GetUserByUsername(this.Username);
                if (user != null)
                {
                    this.SetUserProperties(user);
                }
            }
        }

        public void SetUserProperties(UserModel user)
        {
            if (user == null)
            {
                return;
            }

            this.SetCoreProperties(user.UserID, user.Username, user.BestDisplayName, user.BestAvatarUrl);
            if (!string.IsNullOrWhiteSpace(user.Color))
            {
                this.Color = user.Color;
            }
            this.SetRoleProperties();
        }

        public void SetUserProperties(WebhookUserModel user)
        {
            if (user == null)
            {
                return;
            }

            this.SetCoreProperties(user.UserID, user.Username, user.DisplayName, user.BestAvatarUrl);
            if (!string.IsNullOrWhiteSpace(user.Color))
            {
                this.Color = user.Color;
            }
            this.SetRoleProperties();
        }

        public void SetChatMessageProperties(WebhookChatMessageEventModel message)
        {
            if (message == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(message.Color))
            {
                this.Color = message.Color;
            }

            HashSet<string> badgeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddBadgeTypes(badgeTypes, message.Badges);
            AddBadgeTypes(badgeTypes, message.Sender?.Badges);
            if (message.BadgeDetails != null)
            {
                foreach (WebhookBadgeDetailModel detail in message.BadgeDetails)
                {
                    if (!string.IsNullOrWhiteSpace(detail?.BestSlug))
                    {
                        badgeTypes.Add(detail.BestSlug);
                    }
                }
            }

            // Fold the CHANNEL-scoped role (top-level or sender-nested) in as a slug so the
            // broadcaster/moderator/vip/subscriber logic below sees it. The platform-wide role and
            // userRoles fields ("creator" = owns a channel, observed on non-broadcasters) reach the
            // logic only through the message's IsMod/IsVip/IsSubscriber getters, never as slugs that
            // could satisfy the broadcaster check.
            AddBadgeTypes(badgeTypes, new List<string>() { message.ChannelRole, message.Sender?.ChannelRole });

            if (message.IsMod || badgeTypes.Contains("moderator")) { this.Roles.Add(UserRoleEnum.Moderator); } else { this.Roles.Remove(UserRoleEnum.Moderator); }
            if (message.IsVip || badgeTypes.Contains("vip")) { this.Roles.Add(UserRoleEnum.VeloraVIP); } else { this.Roles.Remove(UserRoleEnum.VeloraVIP); }
            if (message.IsSubscriber || badgeTypes.Contains("subscriber")) { this.Roles.Add(UserRoleEnum.Subscriber); } else { this.Roles.Remove(UserRoleEnum.Subscriber); }
            if (badgeTypes.Contains("broadcaster") || badgeTypes.Contains("streamer")) { this.Roles.Add(UserRoleEnum.Streamer); }

            this.SetBadgeLinksFromTypes(badgeTypes, message);
            this.SetRoleProperties();
        }

        private void SetCoreProperties(string id, string username, string displayName, string avatarLink)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                this.ID = id;
            }

            string resolvedUsername = FirstNonEmpty(username, displayName, this.Username);
            this.Username = resolvedUsername;
            this.DisplayName = FirstNonEmpty(displayName, resolvedUsername);
            if (!string.IsNullOrWhiteSpace(avatarLink))
            {
                this.AvatarLink = avatarLink;
            }
        }

        private void SetRoleProperties()
        {
            bool isStreamer =
                !string.IsNullOrWhiteSpace(this.ID) &&
                !string.IsNullOrWhiteSpace(ServiceManager.Get<VeloraSession>().StreamerID) &&
                string.Equals(this.ID, ServiceManager.Get<VeloraSession>().StreamerID, StringComparison.OrdinalIgnoreCase);

            if (isStreamer)
            {
                this.Roles.Add(UserRoleEnum.Streamer);
            }
        }

        private void SetBadgeLinksFromTypes(HashSet<string> badgeTypes, WebhookChatMessageEventModel message)
        {
            VeloraSession session = ServiceManager.Get<VeloraSession>();

            // Velora's chat payload carries no broadcaster boolean/slug, but Velora does have a
            // broadcaster badge, so treat the channel owner (message author == streamer) as the
            // broadcaster.
            bool isStreamer =
                !string.IsNullOrWhiteSpace(this.ID) &&
                string.Equals(this.ID, session.StreamerID, StringComparison.OrdinalIgnoreCase);

            // The catalog carries no system/role badges today (its "system" category is empty), so
            // role badges fall back to Mix It Up's own hosted icons; the catalog is checked first so
            // Velora-published role badges win if they ever appear.
            if (isStreamer || badgeTypes.Contains("broadcaster") || badgeTypes.Contains("streamer")) { this.RoleBadgeLink = session.GetCatalogBadgeUrl("broadcaster") ?? BroadcasterBadgeURL; }
            else if (message.IsMod || badgeTypes.Contains("moderator")) { this.RoleBadgeLink = session.GetCatalogBadgeUrl("moderator") ?? ModeratorBadgeURL; }
            else if (message.IsVip || badgeTypes.Contains("vip")) { this.RoleBadgeLink = session.GetCatalogBadgeUrl("vip") ?? VIPBadgeURL; }
            else if (badgeTypes.Contains("bot") || (message.IsBot ?? false)) { this.RoleBadgeLink = session.GetCatalogBadgeUrl("bot") ?? BotBadgeURL; }
            else { this.RoleBadgeLink = null; }

            // Velora subscriber badges are per-channel milestone badges keyed by months subscribed
            // (fetched from /api/badges/channel/:username and cached on the session). The Chat WS
            // newMessage carries no subscriberMonths, so a subscriber of unknown tenure counts as
            // 1 month and still receives the channel's first milestone badge.
            bool isSubscriber = message.IsSubscriber || badgeTypes.Contains("subscriber");
            this.SubscriberBadgeLink = isSubscriber
                ? session.GetSubscriberBadgeUrl(Math.Max(message.BestSubscriberMonths ?? 1, 1))
                : null;

            // Global catalog badges (event/promo, e.g. "christmas-2025") arrive as slugs in badges[];
            // the first one that resolves against the catalog shows as the specialty badge.
            this.SpecialtyBadgeLink = null;
            foreach (string badgeType in badgeTypes)
            {
                if (!RoleBadgeTypes.Contains(badgeType))
                {
                    string catalogBadgeUrl = session.GetCatalogBadgeUrl(badgeType);
                    if (!string.IsNullOrEmpty(catalogBadgeUrl))
                    {
                        this.SpecialtyBadgeLink = catalogBadgeUrl;
                        break;
                    }
                }
            }

            // A structured badgeDetails entry may carry its own image URL for a badge that is
            // missing from the global catalog.
            if (string.IsNullOrEmpty(this.SpecialtyBadgeLink) && message.BadgeDetails != null)
            {
                foreach (WebhookBadgeDetailModel detail in message.BadgeDetails)
                {
                    if (detail != null && !string.IsNullOrEmpty(detail.BestImageUrl) && !RoleBadgeTypes.Contains(detail.BestSlug ?? string.Empty))
                    {
                        this.SpecialtyBadgeLink = detail.BestImageUrl;
                        break;
                    }
                }
            }
        }

        private static void AddBadgeTypes(HashSet<string> badgeTypes, IEnumerable<string> values)
        {
            if (values != null)
            {
                foreach (string value in values)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        badgeTypes.Add(value);
                    }
                }
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        }
    }
}
