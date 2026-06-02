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

        public Version GetNormalizedVersion() => NormalizeSemVer(this.version);

        public UpdateStepModel GetDownloadStep() => this.steps?.FirstOrDefault(s => string.Equals(s.action, "download", StringComparison.OrdinalIgnoreCase));
        public UpdateStepModel GetVerifyStep() => this.steps?.FirstOrDefault(s => string.Equals(s.action, "verify", StringComparison.OrdinalIgnoreCase));
        public UpdateStepModel GetExtractStep() => this.steps?.FirstOrDefault(s => string.Equals(s.action, "extract", StringComparison.OrdinalIgnoreCase));

        public string GetEulaUrl(string baseUrl, string channel) => $"{baseUrl}/{channel}/{this.version}/eula";
        public string GetChangelogUrl(string baseUrl, string channel) => $"{baseUrl}/{channel}/{this.version}/changelog";

        private static Version NormalizeSemVer(string versionString)
        {
            string versionValue = versionString ?? "0.0.0";
            int prereleaseSeparator = versionValue.IndexOf('-');
            int buildSeparator = versionValue.IndexOf('+');
            int cutIndex = -1;

            if (prereleaseSeparator >= 0)
            {
                cutIndex = prereleaseSeparator;
            }

            if (buildSeparator >= 0 && (cutIndex < 0 || buildSeparator < cutIndex))
            {
                cutIndex = buildSeparator;
            }

            if (cutIndex >= 0)
            {
                versionValue = versionValue.Substring(0, cutIndex);
            }

            string[] parts = versionValue.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            int major = parts.Length > 0 && int.TryParse(parts[0], out int majorValue) ? majorValue : 0;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out int minorValue) ? minorValue : 0;
            int patch = parts.Length > 2 && int.TryParse(parts[2], out int patchValue) ? patchValue : 0;

            return new Version(major, minor, patch, 0);
        }
    }
}
