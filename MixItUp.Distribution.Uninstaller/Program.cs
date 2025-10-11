using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using MixItUp.Distribution.Core;

namespace MixItUp.Distribution.Uninstaller
{
    internal static class Program
    {
        private static readonly string[] TargetProcessNames = new[]
        {
            "MixItUp",
            "MixItUp.AutoHoster",
        };

        private static readonly string[] AssociationRegistryKeys = new[]
        {
            $@"Software\Classes\mixitup",
            $@"Software\Classes\.miucommand",
            $@"Software\Classes\.mixitupc",
            $@"Software\Classes\MixItUp.MIUCommand.1",
        };

        private const string UninstallRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\MixItUp";

        [Flags]
        private enum MoveFileFlags
        {
            None = 0,
            ReplaceExisting = 1,
            CopyAllowed = 2,
            DelayUntilReboot = 4,
            WriteThrough = 8,
            CreateHardlink = 16,
            FailIfNotTrackable = 32,
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool MoveFileEx(string existingFileName, string newFileName, MoveFileFlags flags);

        public static void Main(string[] args)
        {
            string installRoot = DetermineInstallRoot(args);
            string executablePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

            if (args.Length == 0 && ShouldRelaunchFromTemp(executablePath, installRoot))
            {
                CopySelfToTempAndRun(executablePath, installRoot);
                return;
            }

            WaitForProcessesToClose();

            RemoveShortcuts();
            RemoveRegistryEntries();
            RemoveFileAssociations();
            CleanupInstallation(installRoot);

            QueueSelfForDeletion(executablePath);
        }

        private static string DetermineInstallRoot(string[] args)
        {
            string root = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
                ? args[0]
                : DistributionPaths.GetDefaultAppRoot();

            try
            {
                return Path.GetFullPath(root);
            }
            catch
            {
                return DistributionPaths.GetDefaultAppRoot();
            }
        }

        private static bool ShouldRelaunchFromTemp(string executablePath, string installRoot)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return false;
            }

            try
            {
                string normalizedExecutableDir = Path.GetDirectoryName(Path.GetFullPath(executablePath)) ?? string.Empty;
                string normalizedInstallRoot = Path.GetFullPath(installRoot);
                string tempPath = Path.GetFullPath(Path.GetTempPath());

                if (normalizedExecutableDir.StartsWith(tempPath, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return normalizedExecutableDir.StartsWith(normalizedInstallRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void CopySelfToTempAndRun(string executablePath, string installRoot)
        {
            try
            {
                string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDirectory);

                string destinationExe = Path.Combine(tempDirectory, Path.GetFileName(executablePath));
                File.Copy(executablePath, destinationExe, overwrite: true);

                Process.Start(destinationExe, $"\"{installRoot}\"");
            }
            catch
            {
                // Best effort; swallow to prevent crashing the uninstaller.
            }
        }

        private static void WaitForProcessesToClose()
        {
            try
            {
                const int retries = 10;
                const int delayMs = 500;

                for (int attempt = 0; attempt < retries; attempt++)
                {
                    bool anyRunning = false;

                    foreach (Process process in Process.GetProcesses())
                    {
                        try
                        {
                            if (TargetProcessNames.Any(name => string.Equals(process.ProcessName, name, StringComparison.OrdinalIgnoreCase)))
                            {
                                anyRunning = true;
                                if (attempt >= 4)
                                {
                                    process.CloseMainWindow();
                                }
                            }
                        }
                        finally
                        {
                            process.Dispose();
                        }
                    }

                    if (!anyRunning)
                    {
                        return;
                    }

                    System.Threading.Thread.Sleep(delayMs);
                }
            }
            catch
            {
                // Ignore process enumeration issues.
            }
        }

        private static void RemoveShortcuts()
        {
            try
            {
                string startMenuDirectory = DistributionPaths.GetStartMenuDirectory();
                if (Directory.Exists(startMenuDirectory))
                {
                    Directory.Delete(startMenuDirectory, recursive: true);
                }
            }
            catch
            {
                // Ignore shortcut deletion errors.
            }

            try
            {
                string desktopShortcut = DistributionPaths.GetDesktopShortcutPath();
                if (File.Exists(desktopShortcut))
                {
                    File.Delete(desktopShortcut);
                }
            }
            catch
            {
                // Ignore desktop shortcut deletion errors.
            }
        }

        private static void RemoveRegistryEntries()
        {
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                {
                    try
                    {
                        using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                        {
                            baseKey.DeleteSubKeyTree(UninstallRegistryPath, throwOnMissingSubKey: false);
                        }
                    }
                    catch
                    {
                        // Ignore registry errors.
                    }
                }
            }
        }

        private static void RemoveFileAssociations()
        {
            foreach (string keyPath in AssociationRegistryKeys)
            {
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
                }
                catch
                {
                    // Swallow errors; associations might already be removed.
                }
            }
        }

        private static void CleanupInstallation(string installRoot)
        {
            try
            {
                string launcherConfigPath = DistributionPaths.GetLauncherPath(installRoot);
                if (File.Exists(launcherConfigPath))
                {
                    File.Delete(launcherConfigPath);
                }
            }
            catch { }

            DeleteIfExists(Path.Combine(installRoot, DistributionPaths.LauncherExecutableName));
            DeleteIfExists(Path.Combine(installRoot, DistributionPaths.UninstallerExecutableName));

            DeleteDirectorySafe(Path.Combine(installRoot, DistributionPaths.VersionDirectoryName));

            string dataDirectory = Path.Combine(installRoot, DistributionPaths.DataDirectoryName);
            try
            {
                if (Directory.Exists(dataDirectory))
                {
                    foreach (string directory in Directory.GetDirectories(dataDirectory))
                    {
                        string name = new DirectoryInfo(directory).Name;
                        if (name.StartsWith("Settings", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        DeleteDirectorySafe(directory);
                    }

                    foreach (string file in Directory.GetFiles(dataDirectory))
                    {
                        DeleteIfExists(file);
                    }

                    if (!Directory.EnumerateFileSystemEntries(dataDirectory).Any())
                    {
                        Directory.Delete(dataDirectory, recursive: false);
                    }
                }
            }
            catch
            {
                // Ignore failures when cleaning data directory.
            }

            try
            {
                if (Directory.Exists(installRoot) && !Directory.EnumerateFileSystemEntries(installRoot).Any())
                {
                    Directory.Delete(installRoot, recursive: false);
                }
            }
            catch
            {
                // Ignore root cleanup errors.
            }
        }

        private static void DeleteIfExists(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Ignore file deletion errors.
            }
        }

        private static void DeleteDirectorySafe(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch
            {
                // Ignore directory deletion errors.
            }
        }

        private static void QueueSelfForDeletion(string executablePath)
        {
            try
            {
                MoveFileEx(executablePath, null, MoveFileFlags.DelayUntilReboot);
            }
            catch
            {
                // Fallback best effort: do nothing.
            }
        }
    }
}



