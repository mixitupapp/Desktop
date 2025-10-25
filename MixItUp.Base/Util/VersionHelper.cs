using System;

namespace MixItUp.Base.Util
{
    public static class VersionHelper
    {
        private static readonly char[] SemVersionMetadataSeparators = new[] { '-', '+' };

        public static string NormalizeSemVerString(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return "0.0.0";
            }

            version = version.Trim();
            int cutIndex = version.IndexOfAny(SemVersionMetadataSeparators);
            if (cutIndex >= 0)
            {
                version = version.Substring(0, cutIndex);
            }

            string[] parts = version.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

            int major = parts.Length > 0 && int.TryParse(parts[0], out int majorValue) ? majorValue : 0;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out int minorValue) ? minorValue : 0;
            int patch = parts.Length > 2 && int.TryParse(parts[2], out int patchValue) ? patchValue : 0;

            return string.Format("{0}.{1}.{2}", major, minor, patch);
        }

        public static string NormalizeSemVerString(Version version)
        {
            if (version == null)
            {
                return "0.0.0";
            }

            int patch = version.Build >= 0 ? version.Build : 0;
            return string.Format("{0}.{1}.{2}", Math.Max(0, version.Major), Math.Max(0, version.Minor), Math.Max(0, patch));
        }

        public static bool SemVerEquals(string left, string right)
        {
            return string.Equals(NormalizeSemVerString(left), NormalizeSemVerString(right), StringComparison.OrdinalIgnoreCase);
        }

        public static bool SemVerEquals(Version version, string semver)
        {
            return string.Equals(NormalizeSemVerString(version), NormalizeSemVerString(semver), StringComparison.OrdinalIgnoreCase);
        }
    }
}
