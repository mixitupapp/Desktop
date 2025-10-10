using System;

namespace MixItUp.Distribution.Installer
{
    internal static class EnvironmentRequirements
    {
        public static bool IsWindows10Or11(OperatingSystem operatingSystem)
        {
            if (operatingSystem == null)
            {
                return false;
            }

            if (operatingSystem.Platform != PlatformID.Win32NT)
            {
                return false;
            }

            Version version = operatingSystem.Version;
            if (version == null)
            {
                return false;
            }

            if (version.Major > 10)
            {
                return true;
            }

            if (version.Major == 10)
            {
                return true;
            }

            return false;
        }

        public static bool Is64BitOS(bool is64BitOperatingSystem, bool is64BitProcess)
        {
            return is64BitOperatingSystem || is64BitProcess;
        }
    }
}
