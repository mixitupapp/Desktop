using MixItUp.Base.Util;
using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;

namespace MixItUp.Base.Model.API.Files.V2
{
    [DataContract]
    public class UpdateVersionCheckModel
    {
        [DataMember]
        [JsonProperty("schemaVersion")]
        public string schemaVersion { get; set; }

        [DataMember]
        [JsonProperty("app")]
        public string app { get; set; }

        [DataMember]
        [JsonProperty("channel")]
        public string channel { get; set; }

        [DataMember]
        [JsonProperty("os")]
        public string os { get; set; }

        [DataMember]
        [JsonProperty("arch")]
        public string arch { get; set; }

        [DataMember]
        [JsonProperty("minimumVersion")]
        public string minimumVersion { get; set; }

        [DataMember]
        [JsonProperty("latestVersion")]
        public string latestVersion { get; set; }

        [DataMember]
        [JsonProperty("rolloutPercentage")]
        public int rolloutPercentage { get; set; }

        [DataMember]
        [JsonProperty("updatePaused")]
        public bool updatePaused { get; set; }

        public Version GetNormalizedMinimumVersion() => VersionHelper.NormalizeSemVer(this.minimumVersion);
        public Version GetNormalizedLatestVersion() => VersionHelper.NormalizeSemVer(this.latestVersion);
    }
}
