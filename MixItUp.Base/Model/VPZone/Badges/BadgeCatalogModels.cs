using System;
using System.Collections.Generic;

namespace MixItUp.Base.Model.VPZone.Badges
{
    /// <summary>
    /// VPZone carries chat badges as boolean flags on each msg frame (is_owner, is_mod, is_founder,
    /// is_ambassador, vpz_plus, guest_pass, has_discord, is_subscriber) rather than as a catalog of
    /// slugs, so there is no badge catalog endpoint to fetch. This maps each flag to the artwork
    /// Mix It Up hosts for it, in the order they take precedence in the chat list.
    /// </summary>
    public static class VPZoneBadges
    {
        /// <summary>
        /// Whether the artwork below has been published to the file service. The source lives in
        /// Media/Platforms/VPZone/Badges and is uploaded alongside a release, the same way Kick's and
        /// Velora's badge sets are. While it is false every accessor hands back null, which collapses
        /// the badge slots in chat and skips the request entirely.
        ///
        /// This matters because a missing badge fails quietly but not cheaply: ImageHelper never
        /// caches a failed download, so a link pointing at art that is not there would be re-requested
        /// on every single chat message for the life of the session.
        /// </summary>
        private const bool ArtworkPublished = false;

        private const string BadgeBaseURL = "https://files.mixitup.bot/static/platforms/vpzone/badges/";

        private const string BroadcasterBadge = BadgeBaseURL + "broadcaster.png";
        private const string ModeratorBadge = BadgeBaseURL + "moderator.png";
        private const string FounderBadge = BadgeBaseURL + "founder.png";
        private const string AmbassadorBadge = BadgeBaseURL + "ambassador.png";
        private const string VPZPlusBadge = BadgeBaseURL + "vpzplus.png";
        private const string GuestPassBadge = BadgeBaseURL + "guestpass.png";
        private const string DiscordBadge = BadgeBaseURL + "discord.png";
        private const string SubscriberBadge = BadgeBaseURL + "subscriber.png";

        public static string BroadcasterBadgeURL { get { return Resolve(BroadcasterBadge); } }
        public static string ModeratorBadgeURL { get { return Resolve(ModeratorBadge); } }
        public static string FounderBadgeURL { get { return Resolve(FounderBadge); } }
        public static string AmbassadorBadgeURL { get { return Resolve(AmbassadorBadge); } }
        public static string VPZPlusBadgeURL { get { return Resolve(VPZPlusBadge); } }
        public static string GuestPassBadgeURL { get { return Resolve(GuestPassBadge); } }
        public static string DiscordBadgeURL { get { return Resolve(DiscordBadge); } }
        public static string SubscriberBadgeURL { get { return Resolve(SubscriberBadge); } }

        /// <summary>
        /// Subscriber badge art by tier. VPZone reports the tier as "tier1"/"tier2"/"tier3" on the
        /// frame, and a subscriber whose tier the frame omits falls back to the tier 1 badge.
        /// </summary>
        private static readonly Dictionary<int, string> SubscriberTierBadges = new Dictionary<int, string>()
        {
            { 1, BadgeBaseURL + "subscriber-tier1.png" },
            { 2, BadgeBaseURL + "subscriber-tier2.png" },
            { 3, BadgeBaseURL + "subscriber-tier3.png" },
        };

        public static string GetSubscriberBadgeUrl(int tier)
        {
            return SubscriberTierBadges.TryGetValue(Math.Max(tier, 1), out string url) ? Resolve(url) : SubscriberBadgeURL;
        }

        /// <summary>
        /// The single specialty badge shown alongside the role and subscriber badges. Founder outranks
        /// ambassador, which outranks the paid VPZ+ membership, which outranks the trial guest pass.
        /// </summary>
        public static string GetSpecialtyBadgeUrl(bool isFounder, bool isAmbassador, bool isVPZPlus, bool hasGuestPass, bool hasDiscord)
        {
            if (isFounder) { return FounderBadgeURL; }
            if (isAmbassador) { return AmbassadorBadgeURL; }
            if (isVPZPlus) { return VPZPlusBadgeURL; }
            if (hasGuestPass) { return GuestPassBadgeURL; }
            if (hasDiscord) { return DiscordBadgeURL; }
            return null;
        }

        private static string Resolve(string url)
        {
            return ArtworkPublished ? url : null;
        }
    }
}
