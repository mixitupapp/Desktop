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
        private const string BroadcasterBadgeURL = BadgeBaseURL + "broadcaster.png";
        private const string ModeratorBadgeURL = BadgeBaseURL + "moderator.png";
        private const string VIPBadgeURL = BadgeBaseURL + "vip.png";
        private const string BotBadgeURL = BadgeBaseURL + "bot.png";

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

        public VeloraUserPlatformV2Model(string id, string username, string displayName)
        {
            this.Platform = StreamingPlatformTypeEnum.Velora;
            this.ID = id;
            this.Username = username;
            this.DisplayName = displayName;
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
            if (message.Badges != null)
            {
                foreach (string badge in message.Badges)
                {
                    if (!string.IsNullOrWhiteSpace(badge))
                    {
                        badgeTypes.Add(badge);
                    }
                }
            }

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
            // Velora's chat payload carries no broadcaster boolean/slug, but Velora does have a
            // broadcaster badge, so treat the channel owner (message author == streamer) as the
            // broadcaster.
            bool isStreamer =
                !string.IsNullOrWhiteSpace(this.ID) &&
                string.Equals(this.ID, ServiceManager.Get<VeloraSession>().StreamerID, StringComparison.OrdinalIgnoreCase);

            if (isStreamer || badgeTypes.Contains("broadcaster") || badgeTypes.Contains("streamer")) { this.RoleBadgeLink = BroadcasterBadgeURL; }
            else if (message.IsMod || badgeTypes.Contains("moderator")) { this.RoleBadgeLink = ModeratorBadgeURL; }
            else if (message.IsVip || badgeTypes.Contains("vip")) { this.RoleBadgeLink = VIPBadgeURL; }
            else if (badgeTypes.Contains("bot") || (message.IsBot ?? false)) { this.RoleBadgeLink = BotBadgeURL; }
            else { this.RoleBadgeLink = null; }

            // Velora subscriber badges are per-channel milestone badges keyed by months subscribed
            // (fetched from /api/badges/channel/:username and cached on the session).
            bool isSubscriber = message.IsSubscriber || badgeTypes.Contains("subscriber");
            this.SubscriberBadgeLink = isSubscriber
                ? ServiceManager.Get<VeloraSession>().GetSubscriberBadgeUrl(message.SubscriberMonths ?? 0)
                : null;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        }
    }
}
