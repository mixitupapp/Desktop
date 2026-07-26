using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace MixItUp.Base.Model.Velora.Streams
{
    public class StreamInfoModel
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("categoryName")]
        public string CategoryName { get; set; }

        [JsonProperty("categorySlug")]
        public string CategorySlug { get; set; }

        [JsonProperty("isLive")]
        public bool IsLive { get; set; }

        [JsonProperty("viewerCount")]
        public int ViewerCount { get; set; }

        [JsonProperty("startedAt")]
        public string StartedAt { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; }
    }

    public class CategoryModel
    {
        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("imageUrl")]
        public string ImageUrl { get; set; }

        /// <summary>
        /// Reads a category list out of a response whose shape is not pinned by the docs: a bare
        /// array, or an object nesting the array under categories/results/data.
        /// </summary>
        public static IEnumerable<CategoryModel> ParseList(JToken token)
        {
            List<CategoryModel> categories = new List<CategoryModel>();

            JArray array = token as JArray;
            if (array == null && token is JObject obj)
            {
                array = (obj["categories"] ?? obj["results"] ?? obj["data"]) as JArray;
            }

            if (array != null)
            {
                foreach (JToken item in array)
                {
                    CategoryModel category = item?.ToObject<CategoryModel>();
                    if (!string.IsNullOrWhiteSpace(category?.Slug))
                    {
                        categories.Add(category);
                    }
                }
            }

            return categories;
        }
    }

    public class CategoriesResponseModel
    {
        [JsonProperty("categories")]
        public List<CategoryModel> Categories { get; set; } = new List<CategoryModel>();
    }

    public class UpdateStreamInfoResponseModel
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    /// <summary>
    /// Result of POST streams/:username/clips. The request body is documented
    /// ({ title, durationMs 15000-120000, startOffsetMs?, highlight? }); the response shape is not
    /// pinned, so the clip id / share URL are resolved defensively.
    /// </summary>
    public class VeloraClipModel
    {
        public string ID { get; set; }
        public string Url { get; set; }

        private static readonly string[] IdKeys = { "id", "clipId", "clipID", "slug" };
        private static readonly string[] UrlKeys = { "url", "shareUrl", "clipUrl", "link", "embedUrl" };

        public static VeloraClipModel Parse(JToken token)
        {
            if (token == null || token.Type != JTokenType.Object)
            {
                return null;
            }

            JObject root = (JObject)token;
            JObject clip = (root["clip"] as JObject) ?? (root["data"] as JObject) ?? root;

            string First(JObject obj, string[] keys)
            {
                foreach (string key in keys)
                {
                    string value = obj?.Value<string>(key);
                    if (!string.IsNullOrWhiteSpace(value)) { return value; }
                }
                return null;
            }

            return new VeloraClipModel()
            {
                ID = First(clip, IdKeys) ?? First(root, IdKeys),
                Url = First(clip, UrlKeys) ?? First(root, UrlKeys),
            };
        }
    }

    /// <summary>
    /// Parses GET /api/streams/user/:username (public). The exact JSON shape is not pinned by the
    /// docs, so this resolves title/category/live/thumbnail from either a flat object or one that
    /// nests the stream under "stream" and/or the category under "category".
    /// </summary>
    public class UserStreamModel
    {
        public string Title { get; set; }
        public string CategoryName { get; set; }
        public string CategorySlug { get; set; }
        public string CategoryImageUrl { get; set; }
        public string ThumbnailUrl { get; set; }
        public bool IsLive { get; set; }
        public int ViewerCount { get; set; }

        public static UserStreamModel Parse(JToken token)
        {
            if (token == null || token.Type != JTokenType.Object)
            {
                return null;
            }

            JObject root = (JObject)token;
            JObject stream = (root["stream"] as JObject) ?? root;

            // streams/user returns "category" as a plain name string rather than an object, so the
            // name has to be read off the token itself when it is not nested.
            JToken categoryToken = stream["category"] ?? root["category"];
            JObject category = categoryToken as JObject;
            string categoryText = categoryToken != null && categoryToken.Type == JTokenType.String ? categoryToken.Value<string>() : null;

            string Value(JObject obj, params string[] keys)
            {
                if (obj == null) { return null; }
                foreach (string key in keys)
                {
                    string value = obj.Value<string>(key);
                    if (!string.IsNullOrWhiteSpace(value)) { return value; }
                }
                return null;
            }

            UserStreamModel model = new UserStreamModel()
            {
                Title = Value(stream, "title") ?? Value(root, "title"),
                CategoryName = Value(category, "name") ?? Value(stream, "categoryName") ?? Value(root, "categoryName") ?? categoryText,
                CategorySlug = Value(category, "slug") ?? Value(stream, "categorySlug") ?? Value(root, "categorySlug"),
                CategoryImageUrl = Value(category, "imageUrl", "image") ?? Value(stream, "categoryImageUrl"),
                ThumbnailUrl = Value(stream, "thumbnailUrl", "thumbnail", "previewUrl") ?? Value(root, "thumbnailUrl", "thumbnail"),
            };

            JToken isLive = stream["isLive"] ?? root["isLive"] ?? stream["live"] ?? root["live"];
            model.IsLive = isLive != null && (isLive.Type == JTokenType.Boolean ? isLive.Value<bool>() : string.Equals(isLive.ToString(), "true", System.StringComparison.OrdinalIgnoreCase));

            JToken viewers = stream["viewerCount"] ?? root["viewerCount"] ?? stream["viewers"] ?? root["viewers"];
            if (viewers != null && (viewers.Type == JTokenType.Integer || viewers.Type == JTokenType.Float))
            {
                model.ViewerCount = System.Math.Max(viewers.Value<int>(), 0);
            }

            return model;
        }
    }
}
