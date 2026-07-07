using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace MixItUp.WPF.Branding
{
    /// <summary>
    /// Material Design 3 (Material Symbols Sharp) icon glyphs, matching the icon library used by the
    /// Mix It Up website. Codepoints come from the google/material-design-icons codepoints file and are
    /// rendered with the embedded Assets\Fonts\MaterialSymbolsSharp-Filled.ttf resource font.
    /// </summary>
    public static class MaterialSymbols
    {
        public static readonly FontFamily Font = new FontFamily(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Material Symbols Sharp");

        public const string Api = "\uF1B7";
        public const string Badge = "\uEA67";
        public const string BarChart = "\uE26B";
        public const string BugReport = "\uE868";
        public const string CardGiftcard = "\uE8F6";
        public const string Casino = "\uEB40";
        public const string Category = "\uE72C";
        public const string Chat = "\uE0C9";
        public const string CloudDownload = "\uE2C0";
        public const string Construction = "\uEA3C";
        public const string Dashboard = "\uE871";
        public const string DataObject = "\uEAD3";
        public const string FlashOn = "\uE3E7";
        public const string FormatQuote = "\uE244";
        public const string Forum = "\uE8AF";
        public const string GraphicEq = "\uE1B8";
        public const string History = "\uE8B3";
        public const string Hub = "\uE9F4";
        public const string Info = "\uE88E";
        public const string Inventory = "\uE179";
        public const string Inventory2 = "\uE1A1";
        public const string Layers = "\uE53B";
        public const string Leaderboard = "\uF20C";
        public const string Loyalty = "\uE89A";
        public const string ManageAccounts = "\uF02E";
        public const string MusicNote = "\uE405";
        public const string NewReleases = "\uEF76";
        public const string Paid = "\uF041";
        public const string People = "\uEA21";
        public const string QueuePlayNext = "\uE066";
        public const string RecordVoiceOver = "\uE91F";
        public const string Rule = "\uF1C2";
        public const string Sdk = "\uE720";
        public const string Security = "\uE32A";
        public const string Settings = "\uE8B8";
        public const string ShoppingBasket = "\uE8CB";
        public const string Storefront = "\uEA12";
        public const string Timer = "\uE425";
        public const string Tv = "\uE63B";
        public const string VolunteerActivism = "\uEA70";
        public const string Webhook = "\uEB92";
        public const string WorkspacePremium = "\uE7AF";

        private static readonly Dictionary<string, string> iconNameToGlyph = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "api", Api },
            { "badge", Badge },
            { "bar_chart", BarChart },
            { "bug_report", BugReport },
            { "card_giftcard", CardGiftcard },
            { "casino", Casino },
            { "category", Category },
            { "chat", Chat },
            { "cloud_download", CloudDownload },
            { "construction", Construction },
            { "dashboard", Dashboard },
            { "data_object", DataObject },
            { "flash_on", FlashOn },
            { "format_quote", FormatQuote },
            { "forum", Forum },
            { "graphic_eq", GraphicEq },
            { "history", History },
            { "hub", Hub },
            { "info", Info },
            { "inventory", Inventory },
            { "inventory_2", Inventory2 },
            { "layers", Layers },
            { "leaderboard", Leaderboard },
            { "loyalty", Loyalty },
            { "manage_accounts", ManageAccounts },
            { "music_note", MusicNote },
            { "new_releases", NewReleases },
            { "paid", Paid },
            { "people", People },
            { "queue_play_next", QueuePlayNext },
            { "record_voice_over", RecordVoiceOver },
            { "rule", Rule },
            { "sdk", Sdk },
            { "security", Security },
            { "settings", Settings },
            { "shopping_basket", ShoppingBasket },
            { "storefront", Storefront },
            { "timer", Timer },
            { "tv", Tv },
            { "volunteer_activism", VolunteerActivism },
            { "webhook", Webhook },
            { "workspace_premium", WorkspacePremium },
        };

        /// <summary>
        /// Resolves a Material Symbols icon name (e.g. "flash_on") to its font glyph. Returns null when
        /// the icon name is unknown or empty.
        /// </summary>
        public static string GetGlyph(string iconName)
        {
            if (!string.IsNullOrEmpty(iconName) && iconNameToGlyph.TryGetValue(iconName, out string glyph))
            {
                return glyph;
            }
            return null;
        }
    }
}
