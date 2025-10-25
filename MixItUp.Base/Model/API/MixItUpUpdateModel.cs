using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;

namespace MixItUp.Base.Model.API
{
    [DataContract]
    public class MixItUpUpdateModel
    {
        private string version;

        [JsonIgnore]
        private Version normalizedVersionCache;

        [DataMember]
        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; set; }

        [DataMember]
        [JsonProperty("product")]
        public string Product { get; set; }

        [DataMember]
        [JsonProperty("version")]
        public string Version
        {
            get { return this.version; }
            set
            {
                this.version = value;
                this.normalizedVersionCache = null;
            }
        }

        [DataMember]
        [JsonProperty("channel")]
        public string Channel { get; set; }

        [DataMember]
        [JsonProperty("os")]
        public string OperatingSystem { get; set; }

        [DataMember]
        [JsonProperty("arch")]
        public string Architecture { get; set; }

        [DataMember]
        [JsonProperty("releasedAt")]
        public DateTimeOffset ReleasedAt { get; set; }

        [DataMember]
        [JsonProperty("active")]
        public bool Active { get; set; }

        [DataMember]
        [JsonProperty("mandatory")]
        public bool Mandatory { get; set; }

        [DataMember]
        [JsonProperty("eula")]
        public string Eula { get; set; }

        [DataMember]
        [JsonProperty("eulaVersion")]
        public string EulaVersion { get; set; }

        [DataMember]
        [JsonProperty("changelog")]
        public string Changelog { get; set; }

        [DataMember]
        [JsonProperty("package")]
        public string Package { get; set; }

        [DataMember]
        [JsonProperty("installer")]
        public string Installer { get; set; }

        [DataMember]
        [JsonProperty("sha256")]
        public string Sha256 { get; set; }

        [JsonIgnore]
        public string ChangelogLink { get { return this.Changelog; } }

        [JsonIgnore]
        public string ZipArchiveLink { get { return this.Package; } }

        [JsonIgnore]
        public string InstallerLink { get { return this.Installer; } }

        [JsonIgnore]
        public Version SystemVersion { get { return this.GetNormalizedVersion(); } }

        [JsonIgnore]
        public bool IsPreview
        {
            get
            {
                return string.Equals(this.Channel, "preview", StringComparison.OrdinalIgnoreCase);
            }
        }

        public Version GetNormalizedVersion()
        {
            if (this.normalizedVersionCache != null)
            {
                return this.normalizedVersionCache;
            }

            string versionValue = this.Version ?? "0.0.0";
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

            this.normalizedVersionCache = new Version(major, minor, patch, 0);
            return this.normalizedVersionCache;
        }
    }
}