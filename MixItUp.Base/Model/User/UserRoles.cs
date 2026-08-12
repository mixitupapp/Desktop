using MixItUp.Base.Util;
using System;
using System.Collections.Generic;

namespace MixItUp.Base.Model.User
{
    public enum UserRoleEnum
    {
        [Obsolete]
        Banned = 0,

        [GenericUserRole, TwitchUserRole, YouTubeUserRole, KickUserRole, VeloraUserRole, VPZoneUserRole]
        User = 100,

        [TwitchUserRole]
        TwitchAffiliate = 200,

        [TwitchUserRole]
        TwitchPartner = 250,

        [GenericUserRole, TwitchUserRole, KickUserRole, VeloraUserRole, VPZoneUserRole]
        Follower = 300,
        [YouTubeUserRole]
        YouTubeSubscriber = 301,

        [GenericUserRole, TwitchUserRole, YouTubeUserRole, KickUserRole, VeloraUserRole, VPZoneUserRole]
        Regular = 400,

        [TwitchUserRole]
        TwitchVIP = 500,
        [KickUserRole]
        KickVIP = 501,
        [KickUserRole]
        KickOG = 502,
        [VeloraUserRole]
        VeloraVIP = 503,
        [VPZoneUserRole]
        VPZonePlus = 504,
        [VPZoneUserRole]
        VPZoneFounder = 505,
        [VPZoneUserRole]
        VPZoneAmbassador = 506,

        [GenericUserRole, TwitchUserRole, KickUserRole, VeloraUserRole, VPZoneUserRole]
        Subscriber = 600,
        [YouTubeUserRole]
        YouTubeMember = 601,

        [TwitchUserRole]
        TwitchGlobalMod = 701,

        [TwitchUserRole]
        TwitchStaff = 751,

        [GenericUserRole, TwitchUserRole, YouTubeUserRole, KickUserRole, VeloraUserRole, VPZoneUserRole]
        Moderator = 800,

        [TwitchUserRole]
        TwitchChannelEditor = 850,

        [GenericUserRole, TwitchUserRole, YouTubeUserRole, KickUserRole, VeloraUserRole, VPZoneUserRole]
        Streamer = 900,
    }

    public static class UserRoles
    {
        public static IEnumerable<UserRoleEnum> All { get { return all; } }
        private readonly static IEnumerable<UserRoleEnum> all = EnumHelper.GetEnumList<UserRoleEnum>();

        public static IEnumerable<UserRoleEnum> Generic { get { return generic; } }
        private readonly static IEnumerable<UserRoleEnum> generic = GetSelectableRoles<GenericUserRoleAttribute>();

        public static IEnumerable<UserRoleEnum> Twitch { get { return twitch; } }
        private readonly static IEnumerable<UserRoleEnum> twitch = GetSelectableRoles<TwitchUserRoleAttribute>();

        public static IEnumerable<UserRoleEnum> YouTube { get { return youtube; } }
        private readonly static IEnumerable<UserRoleEnum> youtube = GetSelectableRoles<YouTubeUserRoleAttribute>();

        public static IEnumerable<UserRoleEnum> Kick { get { return kick; } }
        private readonly static IEnumerable<UserRoleEnum> kick = GetSelectableRoles<KickUserRoleAttribute>();

        public static IEnumerable<UserRoleEnum> Velora { get { return velora; } }
        private readonly static IEnumerable<UserRoleEnum> velora = GetSelectableRoles<VeloraUserRoleAttribute>();

        public static IEnumerable<UserRoleEnum> VPZone { get { return vpzone; } }
        private readonly static IEnumerable<UserRoleEnum> vpzone = GetSelectableRoles<VPZoneUserRoleAttribute>();

        private static IEnumerable<UserRoleEnum> GetSelectableRoles<T>() where T : UserRoleAttributeBase
        {
            List<UserRoleEnum> roles = new List<UserRoleEnum>();
            foreach (UserRoleEnum role in EnumHelper.GetEnumList<UserRoleEnum>())
            {
                var attributes = (T[])role.GetType().GetField(role.ToString()).GetCustomAttributes(typeof(T), false);
                if (attributes != null && attributes.Length > 0)
                {
                    roles.Add(role);
                }
            }
            return roles;
        }
    }

    public abstract class UserRoleAttributeBase : Attribute { }

    [AttributeUsage(AttributeTargets.All)]
    public class GenericUserRoleAttribute : UserRoleAttributeBase
    {
        public static readonly GenericUserRoleAttribute Default;

        public GenericUserRoleAttribute() { }

        public override bool Equals(object obj) { return (obj is GenericUserRoleAttribute); }

        public override int GetHashCode()
        {
            int hashCode = -86145682;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<object>.Default.GetHashCode(TypeId);
            return hashCode;
        }

        public override bool IsDefaultAttribute() { return this.Equals(GenericUserRoleAttribute.Default); }
    }

    [AttributeUsage(AttributeTargets.All)]
    public class TwitchUserRoleAttribute : UserRoleAttributeBase
    {
        public static readonly TwitchUserRoleAttribute Default;

        public TwitchUserRoleAttribute() { }

        public override bool Equals(object obj) { return (obj is TwitchUserRoleAttribute); }

        public override int GetHashCode()
        {
            int hashCode = -86145682;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<object>.Default.GetHashCode(TypeId);
            return hashCode;
        }

        public override bool IsDefaultAttribute() { return this.Equals(TwitchUserRoleAttribute.Default); }
    }

    [AttributeUsage(AttributeTargets.All)]
    public class YouTubeUserRoleAttribute : UserRoleAttributeBase
    {
        public static readonly YouTubeUserRoleAttribute Default;

        public YouTubeUserRoleAttribute() { }

        public override bool Equals(object obj) { return (obj is YouTubeUserRoleAttribute); }

        public override int GetHashCode()
        {
            int hashCode = -86145682;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<object>.Default.GetHashCode(TypeId);
            return hashCode;
        }

        public override bool IsDefaultAttribute() { return this.Equals(YouTubeUserRoleAttribute.Default); }
    }

    [AttributeUsage(AttributeTargets.All)]
    public class KickUserRoleAttribute : UserRoleAttributeBase
    {
        public static readonly KickUserRoleAttribute Default;

        public KickUserRoleAttribute() { }

        public override bool Equals(object obj) { return (obj is KickUserRoleAttribute); }

        public override int GetHashCode()
        {
            int hashCode = -86145682;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<object>.Default.GetHashCode(TypeId);
            return hashCode;
        }

        public override bool IsDefaultAttribute() { return this.Equals(KickUserRoleAttribute.Default); }
    }

    [AttributeUsage(AttributeTargets.All)]
    public class VeloraUserRoleAttribute : UserRoleAttributeBase
    {
        public static readonly VeloraUserRoleAttribute Default;

        public VeloraUserRoleAttribute() { }

        public override bool Equals(object obj) { return (obj is VeloraUserRoleAttribute); }

        public override int GetHashCode()
        {
            int hashCode = -86145682;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<object>.Default.GetHashCode(TypeId);
            return hashCode;
        }

        public override bool IsDefaultAttribute() { return this.Equals(VeloraUserRoleAttribute.Default); }
    }

    [AttributeUsage(AttributeTargets.All)]
    public class VPZoneUserRoleAttribute : UserRoleAttributeBase
    {
        public static readonly VPZoneUserRoleAttribute Default;

        public VPZoneUserRoleAttribute() { }

        public override bool Equals(object obj) { return (obj is VPZoneUserRoleAttribute); }

        public override int GetHashCode()
        {
            int hashCode = -86145682;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<object>.Default.GetHashCode(TypeId);
            return hashCode;
        }

        public override bool IsDefaultAttribute() { return this.Equals(VPZoneUserRoleAttribute.Default); }
    }
}
