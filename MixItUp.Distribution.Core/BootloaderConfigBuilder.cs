using System;
using System.Collections.Generic;
using System.Linq;

namespace MixItUp.Distribution.Core
{
    public static class BootloaderConfigBuilder
    {
        public static BootloaderConfigModel BuildOrUpdate(
            BootloaderConfigModel existing,
            string currentVersion,
            IEnumerable<string> availableVersions,
            string installedVersion = null,
            string versionRoot = "app",
            string dataDirName = "data",
            string windowsExecutable = "MixItUp.exe"
        )
        {
            BootloaderConfigModel config = existing ?? new BootloaderConfigModel();

            config.VersionRoot = string.IsNullOrWhiteSpace(versionRoot) ? "app" : versionRoot;
            config.DataDirName = string.IsNullOrWhiteSpace(dataDirName) ? "data" : dataDirName;
            config.CurrentVersion = currentVersion;

            if (config.Executables == null)
            {
                config.Executables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            config.Executables["windows"] = string.IsNullOrWhiteSpace(windowsExecutable)
                ? "MixItUp.exe"
                : windowsExecutable;

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> merged = new List<string>();

            void AddVersion(string version)
            {
                if (string.IsNullOrWhiteSpace(version))
                {
                    return;
                }

                if (seen.Add(version))
                {
                    merged.Add(version);
                }
            }

            if (existing?.Versions != null)
            {
                foreach (string version in existing.Versions)
                {
                    AddVersion(version);
                }
            }

            AddVersion(installedVersion);

            if (availableVersions != null)
            {
                foreach (
                    string version in availableVersions
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                )
                {
                    AddVersion(version);
                }
            }

            AddVersion(currentVersion);

            config.Versions = merged;

            if (config.ExtensionData == null)
            {
                config.ExtensionData = new Dictionary<string, Newtonsoft.Json.Linq.JToken>(
                    StringComparer.OrdinalIgnoreCase
                );
            }

            return config;
        }
    }
}
