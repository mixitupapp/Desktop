using System;
using System.Collections.Generic;
using System.Linq;

namespace MixItUp.Distribution.Core
{
    public static class LauncherConfigBuilder
    {
        public static LauncherConfigModel BuildOrUpdate(
            LauncherConfigModel existing,
            string currentVersion,
            IEnumerable<string> availableVersions,
            string installedVersion = null,
            string versionRoot = "app",
            string dataDirName = "data",
            string windowsExecutable = "MixItUp.exe",
            IReadOnlyDictionary<string, PolicyAcceptanceModel> acceptedPolicies = null
        )
        {
            LauncherConfigModel config = existing ?? new LauncherConfigModel();

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

            Dictionary<string, PolicyAcceptanceModel> acceptedPolicyStore =
                new Dictionary<string, PolicyAcceptanceModel>(StringComparer.OrdinalIgnoreCase);
            if (existing?.AcceptedPolicies != null)
            {
                foreach (KeyValuePair<string, PolicyAcceptanceModel> entry in existing.AcceptedPolicies)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null)
                    {
                        continue;
                    }

                    acceptedPolicyStore[entry.Key] = new PolicyAcceptanceModel
                    {
                        Version = entry.Value.Version,
                        AcceptedAtUtc = entry.Value.AcceptedAtUtc,
                    };
                }
            }

            if (acceptedPolicies != null)
            {
                foreach (KeyValuePair<string, PolicyAcceptanceModel> entry in acceptedPolicies)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null)
                    {
                        continue;
                    }

                    acceptedPolicyStore[entry.Key] = new PolicyAcceptanceModel
                    {
                        Version = entry.Value.Version,
                        AcceptedAtUtc = entry.Value.AcceptedAtUtc,
                    };
                }
            }

            config.AcceptedPolicies = acceptedPolicyStore;

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


