using Microsoft.Win32;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Commands;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MixItUp.WPF.Util
{
    public static class RegistryHelpers
    {
        private static readonly string DefaultInstallDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MixItUp");

        private const string SoftwareClassesRegistryPathPrefx = "SOFTWARE\\Classes\\";
        private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        private static readonly Guid UninstallGuid = new Guid("9BED7BA2-4237-4826-B4C3-F3BB97F01151");

        private const long SHCNE_ASSOCCHANGED = 0x08000000L;
        private const uint SHCNF_IDLIST = 0x0000;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        public static void RegisterFileAssociation()
        {
            try
            {
                if (!RegistryHelpers.KeyExists(SoftwareClassesRegistryPathPrefx + CommandEditorWindowViewModelBase.MixItUpCommandFileExtension))
                {
                    using (var key = Registry.CurrentUser.CreateSubKey(SoftwareClassesRegistryPathPrefx + ActivationProtocolHandler.FileAssociationProgramID))
                    {
                        string applicationLocation = typeof(App).Assembly.Location;

                        key.SetValue("", "Mix It Up");

                        using (var currentVersion = key.CreateSubKey("CurVer"))
                        {
                            currentVersion.SetValue("", ActivationProtocolHandler.FileAssociationProgramID);
                        }

                        using (var defaultIcon = key.CreateSubKey("DefaultIcon"))
                        {
                            defaultIcon.SetValue("", $"{applicationLocation},0");
                        }

                        using (var commandKey = key.CreateSubKey(@"shell\open\command"))
                        {
                            commandKey.SetValue("", "\"" + applicationLocation + "\" \"%1\"");
                        }
                    }

                    using (var key = Registry.CurrentUser.CreateSubKey(SoftwareClassesRegistryPathPrefx + CommandEditorWindowViewModelBase.MixItUpCommandFileExtension))
                    {
                        key.SetValue("", ActivationProtocolHandler.FileAssociationProgramID);
                    }

                    SHChangeNotify((int)SHCNE_ASSOCCHANGED, (int)SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
        }

        public static void RegisterURIActivationProtocol()
        {
            try
            {
                if (!RegistryHelpers.KeyExists(SoftwareClassesRegistryPathPrefx + ActivationProtocolHandler.URIProtocolActivationHeader))
                {
                    using (var key = Registry.CurrentUser.CreateSubKey(SoftwareClassesRegistryPathPrefx + ActivationProtocolHandler.URIProtocolActivationHeader))
                    {
                        string applicationLocation = typeof(App).Assembly.Location;

                        key.SetValue("", "URL:Mix It Up");
                        key.SetValue("URL Protocol", "");

                        using (var defaultIcon = key.CreateSubKey("DefaultIcon"))
                        {
                            defaultIcon.SetValue("", $"{applicationLocation},0");
                        }

                        using (var commandKey = key.CreateSubKey(@"shell\open\command"))
                        {
                            commandKey.SetValue("", "\"" + applicationLocation + "\" \"%1\"");
                        }
                    }
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
        }

        /// <summary>
        /// Registers the app in Add/Remove Programs if running from the default install location.
        /// </summary>
        public static void RegisterUninstaller()
        {
            RegistryKey key = null;
            try
            {
                Assembly asm = Assembly.GetEntryAssembly();
                string exe = asm.Location;
                string installDir = Path.GetDirectoryName(exe);

                if (!string.Equals(DefaultInstallDirectory, installDir, StringComparison.OrdinalIgnoreCase))
                {
                    // Don't register uninstaller if we detect this is NOT running in the user's "local app data" folder
                    return;
                }

                string guidText = UninstallGuid.ToString("B");
                using (RegistryKey parent = Registry.CurrentUser.OpenSubKey(UninstallKey, true))
                {
                    if (parent == null)
                    {
                        Logger.Log($"Unable to find registry key: {UninstallKey}");
                        return;
                    }

                    key = parent.OpenSubKey(guidText, true) ?? parent.CreateSubKey(guidText);

                    if (key == null)
                    {
                        Logger.Log("Unable to create uninstall link.");
                        return;
                    }

                    Version v = asm.GetName().Version;
                    string uninstallerPath = Path.Combine(installDir, "Uninstaller", "MixItUp.Uninstaller.exe");

                    key.SetValue("DisplayName", "Mix It Up");
                    key.SetValue("ApplicationVersion", v.ToString());
                    key.SetValue("Publisher", "Blazing Cacti LLC");
                    key.SetValue("DisplayIcon", exe);
                    key.SetValue("DisplayVersion", v.ToString(4));
                    key.SetValue("URLInfoAbout", "https://mixitupapp.com");
                    key.SetValue("Contact", "support@mixitupapp.com");
                    key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                    key.SetValue("UninstallString", uninstallerPath);
                    key.SetValue("InstallLocation", installDir);
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

                    try
                    {
                        long totalSize = GetDirectorySize(installDir);
                        key.SetValue("EstimatedSize", (int)(totalSize / 1024), RegistryValueKind.DWord);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"An error occurred writing uninstall information to the registry.");
                Logger.Log(ex);
            }
            finally
            {
                if (key != null)
                {
                    key.Close();
                }
            }
        }

        private static long GetDirectorySize(string path)
        {
            long size = 0;
            try
            {
                foreach (string file in Directory.GetFiles(path))
                {
                    size += new FileInfo(file).Length;
                }
                foreach (string dir in Directory.GetDirectories(path))
                {
                    if (!new DirectoryInfo(dir).Name.StartsWith("Settings", StringComparison.OrdinalIgnoreCase))
                    {
                        size += GetDirectorySize(dir);
                    }
                }
            }
            catch { }
            return size;
        }

        public static bool KeyExists(string path)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(path))
            {
                return key != null;
            }
        }
    }
}
