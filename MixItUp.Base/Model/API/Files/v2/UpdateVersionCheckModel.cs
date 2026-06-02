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

        public Version GetNormalizedMinimumVersion() => NormalizeSemVer(this.minimumVersion);
        public Version GetNormalizedLatestVersion() => NormalizeSemVer(this.latestVersion);

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
