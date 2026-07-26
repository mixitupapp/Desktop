using System;
using System.Collections.Generic;

namespace MixItUp.WPF.Branding
{
    /// <summary>
    /// A Mix It Up feature area with its Material Design 3 icon, mirroring the feature catalog in
    /// Website/src/data/features.json so both apps present features with the same iconography.
    /// </summary>
    public sealed class Feature
    {
        public string Id { get; private set; }

        public string IconName { get; private set; }

        public string Glyph { get; private set; }

        public Feature(string id, string iconName)
        {
            this.Id = id;
            this.IconName = iconName;
            this.Glyph = MaterialSymbols.GetGlyph(iconName);
        }
    }

    /// <summary>
    /// Static registry of Mix It Up features and their Material Design 3 icons. Icon names match
    /// Website/src/data/features.json where a feature exists there; Desktop-only areas use icons
    /// from the same Material Symbols library.
    /// </summary>
    public static class Features
    {
        public static readonly Feature Channel = new Feature("channel", "tv");
        public static readonly Feature Chat = new Feature("chat", "forum");
        public static readonly Feature ChatCommands = new Feature("chat-commands", "chat");
        public static readonly Feature EventCommands = new Feature("event-commands", "flash_on");
        public static readonly Feature TimerCommands = new Feature("timer-commands", "timer");
        public static readonly Feature ActionGroups = new Feature("action-groups", "category");
        public static readonly Feature CommunityCommands = new Feature("community-commands", "cloud_download");
        public static readonly Feature WebhookCommands = new Feature("webhook-commands", "webhook");
        public static readonly Feature CommandRequirements = new Feature("requirements", "rule");
        public static readonly Feature CommandHistory = new Feature("command-history", "history");
        public static readonly Feature Variables = new Feature("variables", "data_object");
        public static readonly Feature Users = new Feature("users", "people");
        public static readonly Feature MusicPlayer = new Feature("music-player", "music_note");
        public static readonly Feature Consumables = new Feature("consumables", "shopping_basket");
        public static readonly Feature Currency = new Feature("currency", "paid");
        public static readonly Feature Rank = new Feature("rank", "leaderboard");
        public static readonly Feature Inventory = new Feature("inventory", "inventory_2");
        public static readonly Feature StreamPass = new Feature("stream-pass", "workspace_premium");
        public static readonly Feature RedemptionStore = new Feature("redemption-store", "storefront");
        public static readonly Feature OverlayWidgets = new Feature("overlay-widgets", "layers");
        public static readonly Feature Games = new Feature("games", "casino");
        public static readonly Feature Giveaways = new Feature("giveaways", "card_giftcard");
        public static readonly Feature GameQueue = new Feature("game-queue", "queue_play_next");
        public static readonly Feature Quotes = new Feature("quotes", "format_quote");
        public static readonly Feature Moderation = new Feature("moderation", "security");
        public static readonly Feature Donations = new Feature("donations", "paid");
        public static readonly Feature TextToSpeech = new Feature("text-to-speech", "record_voice_over");
        public static readonly Feature StreamTools = new Feature("stream-tools", "construction");
        public static readonly Feature Statistics = new Feature("statistics", "bar_chart");
        public static readonly Feature Dashboard = new Feature("dashboard", "dashboard");
        public static readonly Feature Services = new Feature("services", "hub");
        public static readonly Feature Accounts = new Feature("accounts", "manage_accounts");
        public static readonly Feature Settings = new Feature("settings", "settings");
        public static readonly Feature DeveloperAPI = new Feature("developer-api", "api");
        public static readonly Feature MCPServer = new Feature("mcp-server", "sdk");
        public static readonly Feature Changelog = new Feature("changelog", "new_releases");
        public static readonly Feature About = new Feature("about", "info");
        public static readonly Feature Debug = new Feature("debug", "bug_report");
        public static readonly Feature IconMap = new Feature("icon-map", "swap_horiz");

        private static readonly Dictionary<string, Feature> lookup = new Dictionary<string, Feature>(StringComparer.OrdinalIgnoreCase);

        static Features()
        {
            foreach (var field in typeof(Features).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (field.GetValue(null) is Feature feature)
                {
                    lookup[feature.Id] = feature;
                }
            }
        }

        public static Feature Get(string id)
        {
            if (!string.IsNullOrEmpty(id) && lookup.TryGetValue(id, out Feature feature))
            {
                return feature;
            }
            return null;
        }
    }
}
