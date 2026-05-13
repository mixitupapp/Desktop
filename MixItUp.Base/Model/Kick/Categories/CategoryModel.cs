using Newtonsoft.Json;
using System.Collections.Generic;

namespace MixItUp.Base.Model.Kick.Categories
{
    public class CategoryWithTagsModel
    {
        [JsonProperty("id")]
        public long ID { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; }

        [JsonProperty("thumbnail")]
        public string Thumbnail { get; set; }
    }

    public class CategoryDetailModel : CategoryWithTagsModel
    {
        [JsonProperty("viewer_count")]
        public int ViewerCount { get; set; }
    }
}
