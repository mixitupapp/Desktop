using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MixItUp.Distribution.Core
{
    public sealed class UpdateManifestModel
    {
        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; set; }

        [JsonProperty("product")]
        public string Product { get; set; }

        [JsonProperty("channel")]
        public string Channel { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("releasedAt")]
        public DateTime? ReleasedAt { get; set; }

        [JsonProperty("releaseType")]
        public string ReleaseType { get; set; }

        [JsonProperty("platforms")]
        public List<UpdatePlatformModel> Platforms { get; set; }
    }

    public sealed class UpdatePlatformModel
    {
        [JsonProperty("platform")]
        public string Platform { get; set; }

        [JsonProperty("files")]
        public List<UpdateFileModel> Files { get; set; }
    }

    public sealed class UpdateFileModel
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("size")]
        public long? Size { get; set; }

        [JsonProperty("sha256")]
        public string Sha256 { get; set; }

        [JsonProperty("contentType")]
        public string ContentType { get; set; }

        [JsonProperty("arch")]
        public string Architecture { get; set; }
    }

    public sealed class UpdatePackageInfo
    {
        public UpdatePackageInfo(
            string version,
            string channel,
            string platform,
            UpdateFileModel file,
            Uri downloadUri
        )
        {
            Version = version;
            Channel = channel;
            Platform = platform;
            File = file;
            DownloadUri = downloadUri;
        }

        public string Version { get; }

        public string Channel { get; }

        public string Platform { get; }

        public UpdateFileModel File { get; }

        public Uri DownloadUri { get; }
    }

    public sealed class BootloaderConfigModel
    {
        [JsonProperty("currentVersion")]
        public string CurrentVersion { get; set; }

        [JsonProperty("versionRoot")]
        public string VersionRoot { get; set; }

        [JsonProperty("versions")]
        public List<string> Versions { get; set; } = new List<string>();

        [JsonProperty("executables")]
        public Dictionary<string, string> Executables { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        [JsonProperty("dataDirName")]
        public string DataDirName { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; } =
            new Dictionary<string, JToken>(StringComparer.OrdinalIgnoreCase);
    }
}
