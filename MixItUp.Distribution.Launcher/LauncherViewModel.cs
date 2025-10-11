using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using MixItUp.Distribution.Core;
using System.Security.Cryptography;

namespace MixItUp.Distribution.Launcher
{
    public sealed class LauncherViewModel : INotifyPropertyChanged
    {
        private const string ProductSlug = "mixitup-desktop";
        private const string Platform = "windows-x64";
        private const string DefaultChannel = "production";
        private const string DefaultBaseUrl = "https://files.mixitupapp.com";

        private readonly string appRoot;
        private readonly string launcherConfigPath;

        private LauncherConfigModel currentConfig;
        private UpdatePackageInfo pendingPackage;
        private string launchExecutablePath;
        private bool updateAvailable;
        private bool isBusy;
        private bool isIndeterminate = true;
        private double progressValue;
        private string installedVersion;
        private string latestVersion;
        private string statusMessage = "Ready.";

        public LauncherViewModel()
        {
            this.appRoot = Path.GetFullPath(AppContext.BaseDirectory);
            this.launcherConfigPath = Path.Combine(this.appRoot, DistributionPaths.LauncherFileName);

            this.CheckForUpdatesCommand = new RelayCommand(
                async _ => await this.CheckForUpdatesAsync(),
                _ => !this.IsBusy
            );

            this.InstallUpdateCommand = new RelayCommand(
                async _ => await this.InstallUpdateAsync(),
                _ => this.CanInstallUpdate
            );

            this.LaunchCommand = new RelayCommand(
                _ => this.LaunchApplication(),
                _ => this.CanLaunch
            );
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public RelayCommand CheckForUpdatesCommand { get; }

        public RelayCommand InstallUpdateCommand { get; }

        public RelayCommand LaunchCommand { get; }

        public string InstalledVersion
        {
            get { return this.installedVersion ?? "Not installed"; }
            private set { this.UpdateInstalledVersion(value); }
        }

        public string LatestVersion
        {
            get { return this.latestVersion ?? string.Empty; }
            private set { this.SetProperty(ref this.latestVersion, value, "LatestVersion"); }
        }

        public string StatusMessage
        {
            get { return this.statusMessage; }
            private set { this.SetProperty(ref this.statusMessage, value, "StatusMessage"); }
        }

        public bool IsBusy
        {
            get { return this.isBusy; }
            private set
            {
                if (this.SetProperty(ref this.isBusy, value, "IsBusy"))
                {
                    this.RaisePropertyChanged("CanInstallUpdate");
                    this.RaisePropertyChanged("CanLaunch");
                    this.CheckForUpdatesCommand.RaiseCanExecuteChanged();
                    this.InstallUpdateCommand.RaiseCanExecuteChanged();
                    this.LaunchCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsIndeterminate
        {
            get { return this.isIndeterminate; }
            private set { this.SetProperty(ref this.isIndeterminate, value, "IsIndeterminate"); }
        }

        public double ProgressValue
        {
            get { return this.progressValue; }
            private set { this.SetProperty(ref this.progressValue, value, "ProgressValue"); }
        }

        public bool CanInstallUpdate
        {
            get { return !this.IsBusy && this.updateAvailable && this.pendingPackage != null; }
        }

        public bool CanLaunch
        {
            get
            {
                return !this.IsBusy
                    && !string.IsNullOrEmpty(this.launchExecutablePath)
                    && File.Exists(this.launchExecutablePath);
            }
        }

        public async Task InitializeAsync()
        {
            await this.LoadInstalledInformationAsync();
            await this.CheckForUpdatesAsync();
        }

        private Task LoadInstalledInformationAsync()
        {
            try
            {
                this.currentConfig = LauncherConfigService.Load(this.launcherConfigPath);
            }
            catch (DistributionException dex)
            {
                this.StatusMessage = "Unable to read Launcher configuration: " + dex.Message;
                this.currentConfig = null;
            }

            string currentVersion = this.currentConfig != null ? this.currentConfig.CurrentVersion : null;
            this.UpdateInstalledVersion(currentVersion);
            this.UpdateLaunchExecutablePath();

            return Task.CompletedTask;
        }

        private async Task CheckForUpdatesAsync()
        {
            if (this.IsBusy)
            {
                return;
            }

            try
            {
                this.BeginOperation("Checking for updates...", true);

                DistributionClient client = this.CreateDistributionClient();
                UpdatePackageInfo package = await client.GetLatestPackageAsync(ProductSlug, Platform, DefaultChannel);

                this.pendingPackage = package;
                this.LatestVersion = package.Version ?? string.Empty;

                string installed = this.installedVersion;
                string latest = package.Version ?? string.Empty;

                bool versionsMatch =
                    !string.IsNullOrEmpty(installed)
                    && !string.IsNullOrEmpty(latest)
                    && string.Equals(installed, latest, StringComparison.OrdinalIgnoreCase);

                this.updateAvailable = !versionsMatch;
                this.RaisePropertyChanged("CanInstallUpdate");
                this.InstallUpdateCommand.RaiseCanExecuteChanged();

                this.StatusMessage = this.updateAvailable
                    ? "Version " + this.LatestVersion + " is available."
                    : "You're on the latest version.";
            }
            catch (DistributionException dex)
            {
                this.StatusMessage = "Failed to reach update server: " + dex.Message;
            }
            catch (Exception ex)
            {
                this.StatusMessage = "Unexpected error while checking for updates: " + ex.Message;
            }
            finally
            {
                this.EndOperation();
            }
        }

        private async Task InstallUpdateAsync()
        {
            if (this.IsBusy || this.pendingPackage == null)
            {
                return;
            }

            UpdatePackageInfo package = this.pendingPackage;
            string targetVersion = !string.IsNullOrEmpty(package.Version) ? package.Version : this.LatestVersion;
            if (string.IsNullOrEmpty(targetVersion))
            {
                this.StatusMessage = "Cannot determine version to install.";
                return;
            }

            try
            {
                bool hasSize = package.File != null && package.File.Size.HasValue;
                this.BeginOperation("Downloading Mix It Up " + targetVersion + "...", !hasSize);

                DistributionClient client = this.CreateDistributionClient();
                Progress<int> downloadProgress = new Progress<int>(percent =>
                {
                    this.IsIndeterminate = false;
                    this.ProgressValue = percent;
                });

                byte[] payload = await client.DownloadPackageAsync(
                    package.DownloadUri,
                    TimeSpan.FromMinutes(10),
                    downloadProgress
                );

                if (payload == null || payload.Length == 0)
                {
                    this.StatusMessage = "Download returned no data.";
                    return;
                }

                string expectedSha = package.File?.Sha256;
                if (!string.IsNullOrWhiteSpace(expectedSha))
                {
                    string actualSha = ComputeSha256Hex(payload);
                    if (!string.Equals(actualSha, expectedSha, StringComparison.OrdinalIgnoreCase))
                    {
                        this.StatusMessage = "Download verification failed. Please try again.";
                        return;
                    }
                }

                string versionRootPath = Path.Combine(this.appRoot, DistributionPaths.VersionDirectoryName);
                string targetDirectory = Path.Combine(versionRootPath, targetVersion);

                if (Directory.Exists(targetDirectory))
                {
                    Directory.Delete(targetDirectory, true);
                }

                Directory.CreateDirectory(versionRootPath);

                Progress<int> extractionProgress = new Progress<int>(percent =>
                {
                    this.IsIndeterminate = false;
                    this.ProgressValue = percent;
                });

                SafeZipExtractor.Extract(
                    payload,
                    targetDirectory,
                    true,
                    extractionProgress,
                    entry =>
                    {
                        string entryPath = entry.FullName ?? string.Empty;
                        if (entryPath.StartsWith("Mix It Up/", StringComparison.OrdinalIgnoreCase))
                        {
                            entryPath = entryPath.Substring("Mix It Up/".Length);
                        }

                        return entryPath;
                    }
                );

                string executablePath = Path.Combine(targetDirectory, DistributionPaths.LauncherExecutableName);
                if (!File.Exists(executablePath))
                {
                    this.StatusMessage = $"Installed payload did not contain {DistributionPaths.LauncherExecutableName}.";
                    return;
                }

                List<string> discoveredVersions = new List<string>();
                try
                {
                    if (Directory.Exists(versionRootPath))
                    {
                        foreach (string path in Directory.GetDirectories(versionRootPath))
                        {
                            string name = Path.GetFileName(path);
                            if (!string.IsNullOrEmpty(name))
                            {
                                discoveredVersions.Add(name);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.StatusMessage = "Installed but failed to enumerate versions: " + ex.Message;
                }

                string installedVersion = this.currentConfig != null ? this.currentConfig.CurrentVersion : null;

                LauncherConfigModel updatedConfig = LauncherConfigBuilder.BuildOrUpdate(
                    this.currentConfig,
                    targetVersion,
                    discoveredVersions,
                    installedVersion,
                    DistributionPaths.VersionDirectoryName,
                    DistributionPaths.DataDirectoryName,
                    DistributionPaths.LauncherExecutableName
                );

                LauncherConfigService.Save(this.launcherConfigPath, updatedConfig);
                this.currentConfig = updatedConfig;
                this.UpdateInstalledVersion(targetVersion);
                this.LatestVersion = targetVersion;
                this.pendingPackage = null;
                this.updateAvailable = false;
                this.StatusMessage = "Mix It Up " + targetVersion + " is ready.";

                this.UpdateLaunchExecutablePath();
            }
            catch (DistributionException dex)
            {
                this.StatusMessage = "Update failed: " + dex.Message;
            }
            catch (Exception ex)
            {
                this.StatusMessage = "Unexpected error during update: " + ex.Message;
            }
            finally
            {
                this.EndOperation();
                this.RaisePropertyChanged("CanInstallUpdate");
                this.InstallUpdateCommand.RaiseCanExecuteChanged();
                this.LaunchCommand.RaiseCanExecuteChanged();
            }
        }

        private void LaunchApplication()
        {
            if (!this.CanLaunch)
            {
                this.StatusMessage = "Unable to locate Mix It Up executable.";
                return;
            }

            string executablePath = this.launchExecutablePath;
            if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
            {
                this.StatusMessage = "Unable to locate Mix It Up executable.";
                return;
            }

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(executablePath);
                startInfo.WorkingDirectory = Path.GetDirectoryName(executablePath) ?? this.appRoot;
                startInfo.UseShellExecute = true;

                Process.Start(startInfo);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                this.StatusMessage = "Failed to launch Mix It Up: " + ex.Message;
            }
        }

        private static string ComputeSha256Hex(byte[] payload)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(payload);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private void BeginOperation(string message, bool indeterminate)
        {
            this.IsBusy = true;
            this.IsIndeterminate = indeterminate;
            this.ProgressValue = 0;
            this.StatusMessage = message;
        }

        private void EndOperation()
        {
            this.IsBusy = false;
            this.IsIndeterminate = true;
            this.ProgressValue = 0;
        }

        private DistributionClient CreateDistributionClient()
        {
            return new DistributionClient(DefaultBaseUrl);
        }

        private void UpdateLaunchExecutablePath()
        {
            string executablePath = string.Empty;
            try
            {
                string versionRoot = DistributionPaths.VersionDirectoryName;
                if (this.currentConfig != null && !string.IsNullOrEmpty(this.currentConfig.VersionRoot))
                {
                    versionRoot = this.currentConfig.VersionRoot;
                }

                string versionRootPath = Path.Combine(this.appRoot, versionRoot);
                string currentVersion = this.currentConfig != null ? this.currentConfig.CurrentVersion : null;

                string targetExecutable = DistributionPaths.LauncherExecutableName;
                if (this.currentConfig != null && this.currentConfig.Executables != null)
                {
                    string configured;
                    if (this.currentConfig.Executables.TryGetValue("windows", out configured) && !string.IsNullOrEmpty(configured))
                    {
                        targetExecutable = configured;
                    }
                }

                if (!string.IsNullOrEmpty(currentVersion))
                {
                    executablePath = Path.Combine(versionRootPath, currentVersion, targetExecutable);
                }
            }
            catch
            {
                executablePath = string.Empty;
            }

            this.launchExecutablePath = executablePath;
            this.RaisePropertyChanged("CanLaunch");
            this.LaunchCommand.RaiseCanExecuteChanged();
        }

        private void UpdateInstalledVersion(string version)
        {
            string normalized = string.IsNullOrEmpty(version) ? null : version;
            if (this.SetProperty(ref this.installedVersion, normalized, "InstalledVersion"))
            {
                this.RaisePropertyChanged("CanInstallUpdate");
                this.InstallUpdateCommand.RaiseCanExecuteChanged();
            }
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            this.RaisePropertyChanged(propertyName);
            return true;
        }

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}




