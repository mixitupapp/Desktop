using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace MixItUp.Base.Model.Velora.Emotes
{
    public class EmoteModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("imageUrl")]
        public string ImageUrl { get; set; }

        [JsonProperty("animated")]
        public bool? Animated { get; set; }

        [JsonProperty("isAnimated")]
        public bool? IsAnimatedFlag { get; set; }

        [JsonProperty("animatedUrl")]
        public string AnimatedUrl { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        // Live shape (confirmed against GET /api/emotes?channel=... and /api/emotes/resolve): image
        // URLs are nested under assetVariants as static1x/2x/4x + animated1x/2x/4x (all WebP on
        // assets.velora.tv). The flat url/imageUrl/animatedUrl fields are fallbacks for older shapes.
        [JsonProperty("assetVariants")]
        public EmoteAssetVariantsModel AssetVariants { get; set; }

        [JsonIgnore]
        public string BestCode { get { return Users.UserModel.FirstNonEmpty(this.Code, this.Name); } }

        // Largest static variant first (mirrors Twitch's scale-3 preference): WPF downscales via
        // DecodePixelWidth and the overlay sizes via CSS, so a bigger source only renders crisper.
        [JsonIgnore]
        public string BestImageUrl { get { return Users.UserModel.FirstNonEmpty(this.AssetVariants?.Static4x, this.AssetVariants?.Static2x, this.AssetVariants?.Static1x, this.ImageUrl, this.Url); } }

        [JsonIgnore]
        public string BestAnimatedUrl { get { return Users.UserModel.FirstNonEmpty(this.AssetVariants?.Animated4x, this.AssetVariants?.Animated2x, this.AssetVariants?.Animated1x, this.AnimatedUrl); } }

        [JsonIgnore]
        public bool IsAnimated { get { return this.IsAnimatedFlag ?? this.Animated ?? false; } }

        [JsonIgnore]
        public bool IsUsable
        {
            get
            {
                return !string.IsNullOrWhiteSpace(this.BestCode) &&
                    !string.IsNullOrWhiteSpace(this.BestImageUrl) &&
                    this.BestImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrWhiteSpace(this.Status) || string.Equals(this.Status, "active", StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Walks an arbitrary emotes API response and extracts every object that looks like an emote
        /// (has a code/name and an image URL, flat or under assetVariants). The live responses are
        /// { collections: [ { ..., emotes: [...] } ] } for the emote lists and { emotes: [...] } for
        /// /api/emotes/resolve; the walk also covers flat arrays and older grouped shapes.
        /// </summary>
        public static List<EmoteModel> ParseEmotes(JToken response)
        {
            List<EmoteModel> emotes = new List<EmoteModel>();
            if (response != null)
            {
                ParseEmotes(response, emotes, depth: 0);
            }
            return emotes;
        }

        private static void ParseEmotes(JToken token, List<EmoteModel> emotes, int depth)
        {
            if (token == null || depth > 6)
            {
                return;
            }

            if (token.Type == JTokenType.Array)
            {
                foreach (JToken child in (JArray)token)
                {
                    ParseEmotes(child, emotes, depth + 1);
                }
            }
            else if (token.Type == JTokenType.Object)
            {
                JObject jobj = (JObject)token;

                EmoteModel emote = null;
                try
                {
                    emote = jobj.ToObject<EmoteModel>();
                }
                catch (Exception) { }

                if (emote != null && emote.IsUsable)
                {
                    emotes.Add(emote);
                }
                else
                {
                    // Not an emote itself (e.g. the response envelope or a collection) - walk into
                    // any nested objects/arrays to find the emotes they contain.
                    foreach (JProperty property in jobj.Properties())
                    {
                        if (property.Value.Type == JTokenType.Array || property.Value.Type == JTokenType.Object)
                        {
                            ParseEmotes(property.Value, emotes, depth + 1);
                        }
                    }
                }
            }
        }
    }

    public class EmoteAssetVariantsModel
    {
        [JsonProperty("static1x")]
        public string Static1x { get; set; }

        [JsonProperty("static2x")]
        public string Static2x { get; set; }

        [JsonProperty("static4x")]
        public string Static4x { get; set; }

        [JsonProperty("animated1x")]
        public string Animated1x { get; set; }

        [JsonProperty("animated2x")]
        public string Animated2x { get; set; }

        [JsonProperty("animated4x")]
        public string Animated4x { get; set; }
    }
}
