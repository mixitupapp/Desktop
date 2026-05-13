using Newtonsoft.Json;

namespace MixItUp.Base.Model.Kick.Users
{
    public class TokenIntrospectionModel
    {
        [JsonProperty("active")]
        public bool Active { get; set; }

        [JsonProperty("client_id")]
        public string ClientID { get; set; }

        [JsonProperty("exp")]
        public long Expiration { get; set; }

        [JsonProperty("scope")]
        public string Scope { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; }
    }
}
