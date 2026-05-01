using Newtonsoft.Json;

namespace MixItUp.Base.Model.Kick.Common
{
    public class KickResponseModel<T>
    {
        [JsonProperty("data")]
        public T Data { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}

