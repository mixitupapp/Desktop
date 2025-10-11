using System;
using System.IO;

namespace MixItUp.Distribution.Core
{
    public static class DistributionPaths
    {
        public const string ShortcutFileName = "Mix It Up.lnk";
        public const string LauncherExecutableName = "MixItUp.exe";
        public const string UninstallerExecutableName = "MixItUp-Uninstall.exe";
        public const string BootloaderFileName = "bootloader.json";
        public const string VersionDirectoryName = "app";
        public const string DataDirectoryName = "data";

        public static string GetDefaultAppRoot()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MixItUp"
            );
        }

        public static string GetBootloaderPath(string appRoot = null)
        {
            if (string.IsNullOrWhiteSpace(appRoot))
            {
                appRoot = GetDefaultAppRoot();
            }

            return Path.Combine(appRoot, BootloaderFileName);
        }

        public static string GetVersionRoot(string appRoot = null)
        {
            if (string.IsNullOrWhiteSpace(appRoot))
            {
                appRoot = GetDefaultAppRoot();
            }

            return Path.Combine(appRoot, VersionDirectoryName);
        }

        public static string GetStartMenuDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Mix It Up"
            );
        }

        public static string GetStartMenuShortcutPath()
        {
            return Path.Combine(GetStartMenuDirectory(), ShortcutFileName);
        }

        public static string GetDesktopShortcutPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                ShortcutFileName
            );
        }
    }
}
