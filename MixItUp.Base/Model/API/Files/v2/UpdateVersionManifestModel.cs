using MixItUp.Base.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace MixItUp.Base.Model.API.Files.V2
{
    [DataContract]
    public class UpdateVersionManifestModel
    {
        [DataMember]
        [JsonProperty("schemaVersion")]
        public string schemaVersion { get; set; }

        [DataMember]
        [JsonProperty("version")]
        public string version { get; set; }

        [DataMember]
        [JsonProperty("eulaVersion")]
        public string eulaVersion { get; set; }

        [DataMember]
        [JsonProperty("releasedAt")]
        public DateTimeOffset releasedAt { get; set; }

        [DataMember]
        [JsonProperty("installer")]
        public string installer { get; set; }

        [DataMember]
        [JsonProperty("steps")]
        public List<UpdateStepModel> steps { get; set; } = new List<UpdateStepModel>();

        public Version GetNormalizedVersion() => VersionHelper.NormalizeSemVer(this.version);

        public UpdateStepModel GetDownloadStep() => this.steps?.FirstOrDefault(s => string.Equals(s.action, "download", StringComparison.OrdinalIgnoreCase));
        public UpdateStepModel GetVerifyStep() => this.steps?.FirstOrDefault(s => string.Equals(s.action, "verify", StringComparison.OrdinalIgnoreCase));
        public UpdateStepModel GetExtractStep() => this.steps?.FirstOrDefault(s => string.Equals(s.action, "extract", StringComparison.OrdinalIgnoreCase));

        public string GetEulaUrl(string baseUrl, string channel) => $"{baseUrl}/{channel}/{this.version}/eula";
        public string GetChangelogUrl(string baseUrl, string channel) => $"{baseUrl}/{channel}/{this.version}/changelog";
    }
}
