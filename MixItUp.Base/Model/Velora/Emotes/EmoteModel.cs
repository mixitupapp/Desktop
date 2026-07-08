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

        [JsonProperty("animatedUrl")]
        public string AnimatedUrl { get; set; }

        [JsonIgnore]
        public string BestCode { get { return Users.UserModel.FirstNonEmpty(this.Code, this.Name); } }

        [JsonIgnore]
        public string BestImageUrl { get { return Users.UserModel.FirstNonEmpty(this.ImageUrl, this.Url); } }

        /// <summary>
        /// Walks an arbitrary emotes API response (object or array, flat or grouped into collections)
        /// and extracts every object that looks like an emote (has a code/name and an image URL).
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
                string code = jobj.Value<string>("code") ?? jobj.Value<string>("name");
                string url = jobj.Value<string>("imageUrl") ?? jobj.Value<string>("url") ?? jobj.Value<string>("src");
                if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(url) &&
                    url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    emotes.Add(new EmoteModel()
                    {
                        ID = jobj.Value<string>("id"),
                        Code = code,
                        Name = jobj.Value<string>("name"),
                        Url = url,
                        ImageUrl = jobj.Value<string>("imageUrl"),
                        Animated = jobj.Value<bool?>("animated"),
                        AnimatedUrl = jobj.Value<string>("animatedUrl"),
                    });
                }
                else
                {
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
}
