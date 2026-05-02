using MixItUp.Base.Model.Kick.Users;
using MixItUp.Base.Model.Kick.Webhooks;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Kick.New;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.User.Platform
{
    [DataContract]
    public class KickUserPlatformV2Model : UserPlatformV2ModelBase
    {
        private const string BadgeBaseURL = "https://files.mixitupapp.com/static/platforms/kick/badges/";
        private const string BroadcasterBadgeURL = BadgeBaseURL + "broadcaster.png";
        private const string ModeratorBadgeURL = BadgeBaseURL + "moderator.png";
        private const string VIPBadgeURL = BadgeBaseURL + "vip.png";
        private const string OGBadgeURL = BadgeBaseURL + "og.png";

        [DataMember]
        public string Color { get; set; }

        [DataMember]
        public string ChannelSlug { get; set; }

        public KickUserPlatformV2Model(UserModel user)
        {
            this.Platform = StreamingPlatformTypeEnum.Kick;
            this.SetUserProperties(user);
        }

        public KickUserPlatformV2Model(WebhookUserReferenceModel user)
        {
            this.Platform = StreamingPlatformTypeEnum.Kick;
            this.SetUserProperties(user);
        }

        public KickUserPlatformV2Model(string id, string username, string displayName)
        {
            this.Platform = StreamingPlatformTypeEnum.Kick;
            this.ID = id;
            this.Username = username;
            this.DisplayName = displayName;
        }

        public override async Task Refresh()
        {
            if (ServiceManager.Get<KickSession>().IsConnected)
            {
                UserModel user = await ServiceManager.Get<KickSession>().StreamerService.GetCurrentUser();
                if (user != null && string.Equals(this.ID, user.UserID.ToString(), StringComparison.OrdinalIgnoreCase))
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

            this.SetCoreProperties(user.UserID.ToString(), user.Username, user.Name, user.ProfilePicture, user.ChannelSlug);
            this.Color = user.Identity?.UsernameColor;
            this.SetIdentityProperties(user.Identity?.Badges);
            this.SetRoleProperties();
        }

        public void SetUserProperties(WebhookUserReferenceModel user)
        {
            if (user == null)
            {
                return;
            }

            this.SetCoreProperties(user.UserID.ToString(), user.Username, null, user.ProfilePicture, user.ChannelSlug);
            this.SetIdentityProperties(user.Identity);
            this.SetRoleProperties();
        }

        public void SetIdentityBadges(IEnumerable<BadgeModel> badges)
        {
            this.SetIdentityProperties(badges);
            this.SetRoleProperties();
        }

        private void SetCoreProperties(string id, string username, string displayName, string avatarLink, string channelSlug = null)
        {
            this.ID = id;

            string resolvedUsername = FirstNonEmpty(username, displayName, channelSlug);
            this.Username = resolvedUsername;
            this.DisplayName = FirstNonEmpty(displayName, resolvedUsername);
            this.AvatarLink = avatarLink;
            this.ChannelSlug = channelSlug;
        }

        private void SetIdentityProperties(IEnumerable<BadgeModel> badges)
        {
            this.Roles.Remove(UserRoleEnum.Moderator);
            this.Roles.Remove(UserRoleEnum.Subscriber);
            this.Roles.Remove(UserRoleEnum.KickVIP);
            this.Roles.Remove(UserRoleEnum.KickOG);
            this.SubscriberBadgeLink = null;
            this.RoleBadgeLink = null;
            this.SpecialtyBadgeLink = null;

            if (badges == null)
            {
                return;
            }

            HashSet<string> badgeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (BadgeModel badge in badges)
            {
                if (badge == null || string.IsNullOrWhiteSpace(badge.Type))
                {
                    continue;
                }

                badgeTypes.Add(badge.Type);
                if (string.Equals(badge.Type, "moderator", StringComparison.OrdinalIgnoreCase))
                {
                    this.Roles.Add(UserRoleEnum.Moderator);
                }
                else if (string.Equals(badge.Type, "subscriber", StringComparison.OrdinalIgnoreCase))
                {
                    this.Roles.Add(UserRoleEnum.Subscriber);
                }
                else if (string.Equals(badge.Type, "vip", StringComparison.OrdinalIgnoreCase))
                {
                    this.Roles.Add(UserRoleEnum.KickVIP);
                }
                else if (string.Equals(badge.Type, "og", StringComparison.OrdinalIgnoreCase))
                {
                    this.Roles.Add(UserRoleEnum.KickOG);
                }
            }

            this.SetBadgeLinksFromTypes(badgeTypes);
        }

        private void SetIdentityProperties(WebhookIdentityModel identity)
        {
            this.Roles.Remove(UserRoleEnum.Moderator);
            this.Roles.Remove(UserRoleEnum.Subscriber);
            this.Roles.Remove(UserRoleEnum.KickVIP);
            this.Roles.Remove(UserRoleEnum.KickOG);
            this.SubscriberBadgeLink = null;
            this.RoleBadgeLink = null;
            this.SpecialtyBadgeLink = null;

            this.Color = identity?.UsernameColor;

            if (identity?.Badges == null)
            {
                return;
            }

            HashSet<string> badgeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (WebhookBadgeModel badge in identity.Badges)
            {
                if (badge == null || string.IsNullOrWhiteSpace(badge.Type))
                {
                    continue;
                }

                badgeTypes.Add(badge.Type);
                if (string.Equals(badge.Type, "moderator", StringComparison.OrdinalIgnoreCase))
                {
                    this.Roles.Add(UserRoleEnum.Moderator);
                }
                else if (string.Equals(badge.Type, "subscriber", StringComparison.OrdinalIgnoreCase))
                {
                    this.Roles.Add(UserRoleEnum.Subscriber);
                }
                else if (string.Equals(badge.Type, "vip", StringComparison.OrdinalIgnoreCase))
                {
                    this.Roles.Add(UserRoleEnum.KickVIP);
                }
                else if (string.Equals(badge.Type, "og", StringComparison.OrdinalIgnoreCase))
                {
                    this.Roles.Add(UserRoleEnum.KickOG);
                }
            }

            this.SetBadgeLinksFromTypes(badgeTypes);
        }

        private void SetRoleProperties()
        {
            bool isStreamer =
                !string.IsNullOrWhiteSpace(this.ID) &&
                !string.IsNullOrWhiteSpace(ServiceManager.Get<KickSession>().StreamerID) &&
                string.Equals(this.ID, ServiceManager.Get<KickSession>().StreamerID, StringComparison.OrdinalIgnoreCase);

            if (isStreamer)
            {
                this.Roles.Add(UserRoleEnum.Streamer);
            }
            else
            {
                this.Roles.Remove(UserRoleEnum.Streamer);
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        }

        private void SetBadgeLinksFromTypes(HashSet<string> badgeTypes)
        {
            if (badgeTypes == null)
            {
                return;
            }

            if (badgeTypes.Contains("broadcaster")) { this.RoleBadgeLink = BroadcasterBadgeURL; }
            else if (badgeTypes.Contains("moderator")) { this.RoleBadgeLink = ModeratorBadgeURL; }
            else if (badgeTypes.Contains("vip")) { this.RoleBadgeLink = VIPBadgeURL; }
            else if (badgeTypes.Contains("og")) { this.RoleBadgeLink = OGBadgeURL; }

            this.SubscriberBadgeLink = null; // TODO: kick has sub badge api??

            // TODO: Keep null for now until Kick specialty badge mapping is documented.
            this.SpecialtyBadgeLink = null;
        }
    }
}
