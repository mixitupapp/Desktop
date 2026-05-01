using Newtonsoft.Json;

namespace MixItUp.Base.Model.Kick.Common
{
    public class ResponseModel<T>
    {
        [JsonProperty("data")]
        public T Data { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class PaginatedResponseModel<T>
    {
        [JsonProperty("data")]
        public System.Collections.Generic.List<T> Data { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("pagination")]
        public PaginationModel Pagination { get; set; }
    }

    public class PaginationModel
    {
        [JsonProperty("next_cursor")]
        public string NextCursor { get; set; }
    }
}

