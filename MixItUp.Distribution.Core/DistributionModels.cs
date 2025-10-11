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

        [JsonProperty("releaseNotes")]
        public string ReleaseNotes { get; set; }

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
            Uri downloadUri,
            string schemaVersion = null,
            string releaseType = null,
            string releaseNotes = null
        )
        {
            Version = version;
            Channel = channel;
            Platform = platform;
            File = file;
            DownloadUri = downloadUri;
            SchemaVersion = schemaVersion;
            ReleaseType = releaseType;
            ReleaseNotes = releaseNotes;
        }

        public string Version { get; }

        public string Channel { get; }

        public string Platform { get; }

        public UpdateFileModel File { get; }

        public Uri DownloadUri { get; }

        public string SchemaVersion { get; }

        public string ReleaseType { get; }

        public string ReleaseNotes { get; }
    }

    public sealed class PolicyManifestModel
    {
        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; set; }

        [JsonProperty("policy")]
        public string Policy { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("publishedAt")]
        public DateTime? PublishedAt { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("content")]
        public PolicyContentModel Content { get; set; }
    }

    public sealed class PolicyContentModel
    {
        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("contentType")]
        public string ContentType { get; set; }

        [JsonProperty("size")]
        public long? Size { get; set; }

        [JsonProperty("sha256")]
        public string Sha256 { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }

    public sealed class PolicyInfo
    {
        public PolicyInfo(
            string schemaVersion,
            string policy,
            string version,
            string title,
            string description,
            DateTime? publishedAt,
            string contentPath,
            string contentType,
            long? contentSize,
            string contentSha256,
            Uri contentUri
        )
        {
            SchemaVersion = schemaVersion;
            Policy = policy;
            Version = version;
            Title = title;
            Description = description;
            PublishedAt = publishedAt;
            ContentPath = contentPath;
            ContentType = contentType;
            ContentSize = contentSize;
            ContentSha256 = contentSha256;
            ContentUri = contentUri ?? throw new ArgumentNullException(nameof(contentUri));
        }

        public string SchemaVersion { get; }

        public string Policy { get; }

        public string Version { get; }

        public string Title { get; }

        public string Description { get; }

        public DateTime? PublishedAt { get; }

        public string ContentPath { get; }

        public string ContentType { get; }

        public long? ContentSize { get; }

        public string ContentSha256 { get; }

        public Uri ContentUri { get; }
    }

    public sealed class LauncherConfigModel
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

        [JsonProperty("acceptedPolicies")]
        public Dictionary<string, PolicyAcceptanceModel> AcceptedPolicies { get; set; } =
            new Dictionary<string, PolicyAcceptanceModel>(StringComparer.OrdinalIgnoreCase);

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; } =
            new Dictionary<string, JToken>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class PolicyAcceptanceModel
    {
        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("acceptedAtUtc")]
        public DateTime? AcceptedAtUtc { get; set; }
    }
}

