using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace MixItUp.Base.Model.API.Files.V2
{
    [DataContract]
    public class UpdateStepModel
    {
        [DataMember]
        [JsonProperty("action")]
        public string action { get; set; }

        [DataMember]
        [JsonProperty("target")]
        public string target { get; set; }

        [DataMember]
        [JsonProperty("value")]
        public string value { get; set; }
    }
}
