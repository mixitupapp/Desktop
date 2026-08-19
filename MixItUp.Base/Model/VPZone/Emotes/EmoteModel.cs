using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace MixItUp.Base.Model.VPZone.Emotes
{
    /// <summary>
    /// One emote resolved from a message's emoteMap. VPZone publishes no global emote catalog: each
    /// msg frame carries its own token to image-URL map, already filtered to what the sender was
    /// entitled to use, so emotes are only ever resolved per message rather than from a shared
    /// dictionary. This model is the per-message entry the chat view model renders from.
    /// </summary>
    public class VPZoneEmoteModel
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonIgnore]
        public string BestCode { get { return this.Code ?? string.Empty; } }

        [JsonIgnore]
        public string BestImageUrl { get { return this.Url ?? string.Empty; } }

        // VPZone serves animated emotes from the same URL as static ones, with no separate variant or
        // animated flag, so the renderer treats every emote as a single image.
        [JsonIgnore]
        public bool IsAnimated { get { return false; } }

        [JsonIgnore]
        public string BestAnimatedUrl { get { return null; } }

        [JsonIgnore]
        public bool IsUsable
        {
            get
            {
                return !string.IsNullOrWhiteSpace(this.BestCode) &&
                    !string.IsNullOrWhiteSpace(this.BestImageUrl) &&
                    this.BestImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>Turns a frame's emoteMap into the emote list for that one message.</summary>
        public static List<VPZoneEmoteModel> ParseEmoteMap(IDictionary<string, string> emoteMap)
        {
            List<VPZoneEmoteModel> emotes = new List<VPZoneEmoteModel>();
            if (emoteMap != null)
            {
                foreach (KeyValuePair<string, string> entry in emoteMap)
                {
                    VPZoneEmoteModel emote = new VPZoneEmoteModel() { Code = entry.Key, Url = entry.Value };
                    if (emote.IsUsable)
                    {
                        emotes.Add(emote);
                    }
                }
            }
            return emotes;
        }
    }
}
