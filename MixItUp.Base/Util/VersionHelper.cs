using System;
using System.Reflection;

namespace MixItUp.Base.Util
{
    public static class VersionHelper
    {
        public static string GetFullVersionString()
        {
            try
            {
                var attr = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyFileVersionAttribute>();
                if (attr != null && !string.IsNullOrWhiteSpace(attr.Version))
                {
                    return NormalizeSemVer(new Version(attr.Version));
                }
            }
            catch { }

            return "0.0.0";
        }

        public static string NormalizeSemVer(Version version)
        {
            if (version == null)
            {
                return "0.0.0";
            }

            int patch = version.Build >= 0 ? version.Build : 0;
            return string.Format("{0}.{1}.{2:D3}", Math.Max(0, version.Major), Math.Max(0, version.Minor), Math.Max(0, patch));
        }

        public static Version NormalizeSemVer(string versionString)
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

        public static bool SemVerEquals(string left, string right)
        {
            return NormalizeSemVer(left) == NormalizeSemVer(right);
        }

        public static Version GetCurrentVersion()
        {
            return NormalizeSemVer(GetFullVersionString());
        }

        public static bool SemVerEquals(Version version, string semver)
        {
            return version == NormalizeSemVer(semver);
        }
    }
}
