using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace MixItUp.Base.Model.Velora.Subscriptions
{
    /// <summary>
    /// Parses the response of GET /api/developer/subscriptions/count. Velora's docs describe this
    /// only as an "authoritative count of active subscribers" without pinning the JSON shape, so we
    /// accept a bare number, a flat object with any of the common count keys, or a nested { data }.
    /// </summary>
    public static class VeloraSubscriberCount
    {
        private static readonly string[] CountKeys = new string[]
        {
            "count", "total", "subscriberCount", "activeSubscribers", "activeSubscriberCount", "active", "subscribers",
        };

        public static int Parse(JToken token)
        {
            return Parse(token, depth: 0);
        }

        private static int Parse(JToken token, int depth)
        {
            if (token == null || depth > 4)
            {
                return 0;
            }

            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                return Math.Max((int)token, 0);
            }

            if (token.Type == JTokenType.Object)
            {
                JObject obj = (JObject)token;
                foreach (string key in CountKeys)
                {
                    JToken value = obj[key];
                    if (value != null && (value.Type == JTokenType.Integer || value.Type == JTokenType.Float))
                    {
                        return Math.Max((int)value, 0);
                    }
                }

                return Parse(obj["data"], depth + 1);
            }

            return 0;
        }
    }

    /// <summary>
    /// A single per-channel subscription milestone badge from GET /api/badges/channel/:username.
    /// Velora renders subscriber badges as predefined per-channel milestones (by months subscribed),
    /// not a global asset. The response shape is not pinned by the docs, so parsing is defensive.
    /// </summary>
    public class ChannelSubscriptionBadgeModel
    {
        public int Months { get; set; }
        public string ImageUrl { get; set; }

        private static readonly string[] ArrayKeys = { "badges", "subscriptionBadges", "subscriberBadges", "tiers", "levels", "milestones", "items", "data" };
        private static readonly string[] MonthKeys = { "months", "minMonths", "monthsRequired", "requiredMonths", "monthThreshold", "threshold", "tier", "level", "min" };
        // Velora returns "staticAssetUrl"/"animatedAssetUrl" (confirmed live); the rest are defensive fallbacks.
        private static readonly string[] ImageKeys = { "staticAssetUrl", "imageUrl", "url", "image", "src", "badgeUrl", "iconUrl", "animatedAssetUrl" };

        public static List<ChannelSubscriptionBadgeModel> ParseList(JToken token)
        {
            List<ChannelSubscriptionBadgeModel> badges = new List<ChannelSubscriptionBadgeModel>();
            JArray array = FindArray(token, 0);
            if (array != null)
            {
                foreach (JToken item in array)
                {
                    if (item is JObject obj)
                    {
                        string image = FirstString(obj, ImageKeys);
                        if (!string.IsNullOrWhiteSpace(image))
                        {
                            badges.Add(new ChannelSubscriptionBadgeModel() { Months = FirstInt(obj, MonthKeys), ImageUrl = image });
                        }
                    }
                }
            }
            badges.Sort((a, b) => a.Months.CompareTo(b.Months));
            return badges;
        }

        /// <summary>
        /// Returns the highest-milestone badge whose month threshold the subscriber has reached, or
        /// null if the channel has no configured badges / the subscriber is below every threshold.
        /// </summary>
        public static string ResolveBadgeUrl(IReadOnlyList<ChannelSubscriptionBadgeModel> badges, int months)
        {
            if (badges == null)
            {
                return null;
            }

            string url = null;
            foreach (ChannelSubscriptionBadgeModel badge in badges)
            {
                // Badges are sorted ascending by Months, so the last qualifying one is the highest tier.
                if (badge.Months <= months)
                {
                    url = badge.ImageUrl;
                }
            }
            return url;
        }

        private static JArray FindArray(JToken token, int depth)
        {
            if (token == null || depth > 4)
            {
                return null;
            }
            if (token.Type == JTokenType.Array)
            {
                return (JArray)token;
            }
            if (token is JObject obj)
            {
                foreach (string key in ArrayKeys)
                {
                    if (obj[key] is JArray arr)
                    {
                        return arr;
                    }
                }
                if (obj["data"] is JObject dataObj)
                {
                    JArray nested = FindArray(dataObj, depth + 1);
                    if (nested != null)
                    {
                        return nested;
                    }
                }
                foreach (JProperty prop in obj.Properties())
                {
                    if (prop.Value.Type == JTokenType.Array)
                    {
                        return (JArray)prop.Value;
                    }
                }
            }
            return null;
        }

        private static string FirstString(JObject obj, string[] keys)
        {
            foreach (string key in keys)
            {
                string value = obj.Value<string>(key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
            return null;
        }

        private static int FirstInt(JObject obj, string[] keys)
        {
            foreach (string key in keys)
            {
                JToken value = obj[key];
                if (value != null)
                {
                    if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
                    {
                        return Math.Max((int)value, 0);
                    }
                    if (value.Type == JTokenType.String && int.TryParse(value.ToString(), out int parsed))
                    {
                        return Math.Max(parsed, 0);
                    }
                }
            }
            return 0;
        }
    }
}
