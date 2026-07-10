using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace MixItUp.Base.Model.Velora.Badges
{
    /// <summary>
    /// A global/platform badge from GET /api/badges/catalog ("all global/platform badges that can be
    /// displayed in chat"). Live shape (confirmed): { badges: [ { id, slug, name, description,
    /// category (subscriber|event|promo|legacy|system), staticAssetUrl, animatedAssetUrl } ] }.
    /// Chat messages reference these badges by slug in their badges[] list.
    /// </summary>
    public class CatalogBadgeModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("staticAssetUrl")]
        public string StaticAssetUrl { get; set; }

        [JsonProperty("animatedAssetUrl")]
        public string AnimatedAssetUrl { get; set; }

        [JsonIgnore]
        public string BestImageUrl { get { return Users.UserModel.FirstNonEmpty(this.StaticAssetUrl, this.AnimatedAssetUrl); } }

        public static List<CatalogBadgeModel> ParseList(JToken response)
        {
            List<CatalogBadgeModel> badges = new List<CatalogBadgeModel>();

            JToken array = response;
            if (response is JObject obj)
            {
                array = obj["badges"] ?? obj["data"];
            }

            if (array is JArray jarray)
            {
                foreach (JToken item in jarray)
                {
                    if (item is JObject itemObj)
                    {
                        CatalogBadgeModel badge = null;
                        try
                        {
                            badge = itemObj.ToObject<CatalogBadgeModel>();
                        }
                        catch (Exception) { }

                        if (badge != null && !string.IsNullOrWhiteSpace(badge.Slug) && !string.IsNullOrWhiteSpace(badge.BestImageUrl))
                        {
                            badges.Add(badge);
                        }
                    }
                }
            }
            return badges;
        }
    }
}
