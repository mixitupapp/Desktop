using MixItUp.Base.Model.VPZone.Badges;
using MixItUp.Base.Model.VPZone.Realtime;
using MixItUp.Base.Model.VPZone.Users;
using MixItUp.Base.Model.VPZone.Webhooks;
using MixItUp.Base.Services;
using MixItUp.Base.Services.VPZone.New;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.User.Platform
{
    [DataContract]
    public class VPZoneUserPlatformV2Model : UserPlatformV2ModelBase
    {
        [DataMember]
        public string Color { get; set; }

        /// <summary>
        /// Whether the member holds an active VPZ+ membership, as of the last frame or lookup that
        /// reported it. Backs the VPZ+ role that commands and overlays gate on.
        /// </summary>
        [DataMember]
        public bool VPZPlusActive { get; set; }

        /// <summary>
        /// The member's first-ever VPZ+ activation, which is the membership anniversary. VPZone never
        /// resets it on renewal, so it survives a lapse and is only cleared when it goes inactive.
        /// </summary>
        [DataMember]
        public DateTimeOffset? VPZPlusSince { get; set; }

        public VPZoneUserPlatformV2Model(VPZoneUserModel user)
        {
            this.Platform = StreamingPlatformTypeEnum.VPZone;
            this.SetUserProperties(user);
        }

        public VPZoneUserPlatformV2Model(WebhookUserModel user)
        {
            this.Platform = StreamingPlatformTypeEnum.VPZone;
            this.SetUserProperties(user);
        }

        public VPZoneUserPlatformV2Model(string id, string username, string displayName, string avatarLink = null)
        {
            this.Platform = StreamingPlatformTypeEnum.VPZone;
            this.ID = id;
            this.Username = username;
            this.DisplayName = displayName;
            if (!string.IsNullOrWhiteSpace(avatarLink))
            {
                this.AvatarLink = avatarLink;
            }
        }

        [JsonConstructor]
        public VPZoneUserPlatformV2Model() : base() { }

        public override async Task Refresh()
        {
            if (ServiceManager.Get<VPZoneSession>().IsConnected && !string.IsNullOrWhiteSpace(this.Username))
            {
                VPZoneUserModel user = await ServiceManager.Get<VPZoneSession>().StreamerService.GetUserByUsername(this.Username);
                if (user != null)
                {
                    this.SetUserProperties(user);
                }
            }
        }

        public void SetUserProperties(VPZoneUserModel user)
        {
            if (user == null)
            {
                return;
            }

            this.SetCoreProperties(user.UserID, user.Username, user.BestDisplayName, user.BestAvatarUrl);
            this.SetVPZPlusProperties(user.VPZPlusActive, user.VPZPlusSince);
            this.SetRoleProperties();
        }

        public void SetUserProperties(WebhookUserModel user)
        {
            if (user == null)
            {
                return;
            }

            this.SetCoreProperties(user.UserID, user.Username, user.DisplayName, user.AvatarUrl);
            this.SetRoleProperties();
        }

        /// <summary>
        /// Applies everything a msg frame reports about its sender. The badge flags are authoritative
        /// per message, so each role is reconciled rather than only added: a viewer who loses their
        /// moderator status stops being flagged as one on their next message.
        /// </summary>
        public void SetChatMessageProperties(VPZoneChatEventModel frame)
        {
            if (frame == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(frame.Color))
            {
                this.Color = frame.Color;
            }

            if (frame.IsOwner) { this.Roles.Add(UserRoleEnum.Streamer); }
            if (frame.IsModerator) { this.Roles.Add(UserRoleEnum.Moderator); } else { this.Roles.Remove(UserRoleEnum.Moderator); }
            if (frame.IsSubscriber) { this.Roles.Add(UserRoleEnum.Subscriber); } else { this.Roles.Remove(UserRoleEnum.Subscriber); }
            if (frame.IsFounder) { this.Roles.Add(UserRoleEnum.VPZoneFounder); } else { this.Roles.Remove(UserRoleEnum.VPZoneFounder); }
            if (frame.IsAmbassador) { this.Roles.Add(UserRoleEnum.VPZoneAmbassador); } else { this.Roles.Remove(UserRoleEnum.VPZoneAmbassador); }
            if (frame.VPZPlus) { this.Roles.Add(UserRoleEnum.VPZonePlus); } else { this.Roles.Remove(UserRoleEnum.VPZonePlus); }

            // A frame reports the current state but never the anniversary, so an existing since date is
            // preserved across messages and only the active flag tracks the frame.
            this.VPZPlusActive = frame.VPZPlus;
            if (!frame.VPZPlus)
            {
                this.VPZPlusSince = null;
            }

            this.SetBadgeLinksFromFrame(frame);
            this.SetRoleProperties();
        }

        /// <summary>
        /// Records the VPZ+ standing from a profile lookup, which is the only source that carries the
        /// anniversary date.
        /// </summary>
        public void SetVPZPlusProperties(bool active, string since)
        {
            this.VPZPlusActive = active;
            if (active)
            {
                this.Roles.Add(UserRoleEnum.VPZonePlus);
                if (!string.IsNullOrWhiteSpace(since) && DateTimeOffset.TryParse(since, out DateTimeOffset sinceDate))
                {
                    this.VPZPlusSince = sinceDate;
                }
            }
            else
            {
                this.Roles.Remove(UserRoleEnum.VPZonePlus);
                this.VPZPlusSince = null;
            }
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

        /// <summary>
        /// Re-applies the identity-derived roles (streamer and connected bot). The session calls this
        /// once the bot identity is known, because the bot's user is resolved before the role pass can
        /// see it.
        /// </summary>
        public void RefreshRoleProperties() { this.SetRoleProperties(); }

        private void SetRoleProperties()
        {
            if (string.IsNullOrWhiteSpace(this.ID))
            {
                return;
            }

            VPZoneSession session = ServiceManager.Get<VPZoneSession>();

            // Reconciled rather than add-only, so the role does not outlive the account it was granted
            // for: Roles is persisted, and a streamer who switches VPZone accounts would otherwise
            // leave the old one flagged as Streamer forever. Only reconciled once the streamer is
            // actually known, so a not-yet-connected session never strips anything.
            if (!string.IsNullOrWhiteSpace(session.StreamerID))
            {
                if (string.Equals(this.ID, session.StreamerID, StringComparison.OrdinalIgnoreCase))
                {
                    this.Roles.Add(UserRoleEnum.Streamer);
                }
                else
                {
                    this.Roles.Remove(UserRoleEnum.Streamer);
                }
            }
        }

        /// <summary>
        /// VPZone carries badges as boolean flags on the frame rather than as a catalog of slugs, so
        /// each badge link is resolved straight from those flags.
        /// </summary>
        private void SetBadgeLinksFromFrame(VPZoneChatEventModel frame)
        {
            VPZoneSession session = ServiceManager.Get<VPZoneSession>();

            bool isStreamer = frame.IsOwner ||
                (!string.IsNullOrWhiteSpace(this.ID) && string.Equals(this.ID, session.StreamerID, StringComparison.OrdinalIgnoreCase));

            if (isStreamer) { this.RoleBadgeLink = VPZoneBadges.BroadcasterBadgeURL; }
            else if (frame.IsModerator) { this.RoleBadgeLink = VPZoneBadges.ModeratorBadgeURL; }
            else { this.RoleBadgeLink = null; }

            this.SubscriberBadgeLink = frame.IsSubscriber ? VPZoneBadges.GetSubscriberBadgeUrl(frame.TierNumber) : null;

            this.SpecialtyBadgeLink = VPZoneBadges.GetSpecialtyBadgeUrl(frame.IsFounder, frame.IsAmbassador, frame.VPZPlus, frame.GuestPass, frame.HasDiscord);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        }
    }
}
