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

namespace MixItUp.Distribution.Launcher
{
    public sealed class LauncherViewModel : INotifyPropertyChanged
    {
        private const string ProductSlug = "mixitup-desktop";
        private const string Platform = "windows-x64";
        private const string DefaultChannel = "production";
        private const string DefaultBaseUrl = "https://files.mixitupapp.com";
        private const string VersionRootName = "app";
        private const string DataDirectoryName = "data";
        private const string WindowsExecutableName = "MixItUp.exe";

        private readonly string appRoot;
        private readonly string bootloaderPath;

        private BootloaderConfigModel? currentConfig;
        private UpdatePackageInfo? pendingPackage;
        private string? launchExecutablePath;
        private bool updateAvailable;
        private bool isBusy;
        private bool isIndeterminate = true;
        private double progressValue;
        private string? installedVersion;
        private string? latestVersion;
        private string statusMessage = "Ready.";

        public LauncherViewModel()
        {
            this.appRoot = Path.GetFullPath(AppContext.BaseDirectory);
            this.bootloaderPath = Path.Combine(this.appRoot, "bootloader.json");

            this.CheckForUpdatesCommand = new RelayCommand(
                _ => _ = this.CheckForUpdatesAsync(),
                _ => !this.IsBusy
            );

            this.InstallUpdateCommand = new RelayCommand(
                _ => _ = this.InstallUpdateAsync(),
                _ => this.CanInstallUpdate
            );

            this.LaunchCommand = new RelayCommand(
                _ => this.LaunchApplication(),
                _ => this.CanLaunch
            );
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public RelayCommand CheckForUpdatesCommand { get; }

        public RelayCommand InstallUpdateCommand { get; }

        public RelayCommand LaunchCommand { get; }

        public string InstalledVersion => this.installedVersion ?? "Not installed";

        public string LatestVersion
        {
            get => this.latestVersion ?? string.Empty;
            private set => this.SetProperty(ref this.latestVersion, value);
        }

        public string StatusMessage
        {
            get => this.statusMessage;
            private set => this.SetProperty(ref this.statusMessage, value);
        }

        public bool IsBusy
        {
            get => this.isBusy;
            private set
            {
                if (this.SetProperty(ref this.isBusy, value))
                {
                    this.RaisePropertyChanged(nameof(this.CanInstallUpdate));
                    this.RaisePropertyChanged(nameof(this.CanLaunch));
                    this.CheckForUpdatesCommand.RaiseCanExecuteChanged();
                    this.InstallUpdateCommand.RaiseCanExecuteChanged();
                    this.LaunchCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsIndeterminate
        {
            get => this.isIndeterminate;
            private set => this.SetProperty(ref this.isIndeterminate, value);
        }

        public double ProgressValue
        {
            get => this.progressValue;
            private set => this.SetProperty(ref this.progressValue, value);
        }

        public bool CanInstallUpdate => !this.IsBusy && this.updateAvailable && this.pendingPackage != null;

        public bool CanLaunch =>
            !this.IsBusy
            && !string.IsNullOrWhiteSpace(this.launchExecutablePath)
            && File.Exists(this.launchExecutablePath);

        public async Task InitializeAsync()
        {
            await this.LoadInstalledInformationAsync().ConfigureAwait(true);
            await this.CheckForUpdatesAsync().ConfigureAwait(true);
        }

        private Task LoadInstalledInformationAsync()
        {
            try
            {
                this.currentConfig = BootloaderConfigService.Load(this.bootloaderPath);
            }
            catch (DistributionException dex)
            {
                this.StatusMessage = $"Unable to read bootloader configuration: {dex.Message}";
                this.currentConfig = null;
            }

            string? currentVersion = this.currentConfig?.CurrentVersion;
            this.UpdateInstalledVersion(currentVersion);
            this.UpdateLaunchExecutablePath();

            return Task.CompletedTask;
        }

        private void UpdateInstalledVersion(string? version)
        {
            string? normalized = string.IsNullOrWhiteSpace(version) ? null : version;
            if (this.SetProperty(ref this.installedVersion, normalized, nameof(this.InstalledVersion)))
            {
                this.RaisePropertyChanged(nameof(this.CanInstallUpdate));
                this.InstallUpdateCommand.RaiseCanExecuteChanged();
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            if (this.IsBusy)
            {
                return;
            }

            try
            {
                this.BeginOperation("Checking for updates...", indeterminate: true);

                DistributionClient client = this.CreateDistributionClient();
                UpdatePackageInfo package = await client
                    .GetLatestPackageAsync(ProductSlug, Platform, DefaultChannel)
                    .ConfigureAwait(true);

                this.pendingPackage = package;
                this.LatestVersion = package.Version ?? string.Empty;

                string? installedVersion = this.installedVersion;
                string latest = package.Version ?? string.Empty;

                bool versionsMatch =
                    !string.IsNullOrWhiteSpace(installedVersion)
                    && !string.IsNullOrWhiteSpace(latest)
                    && string.Equals(installedVersion, latest, StringComparison.OrdinalIgnoreCase);

                this.updateAvailable = !versionsMatch;
                this.RaisePropertyChanged(nameof(this.CanInstallUpdate));
                this.InstallUpdateCommand.RaiseCanExecuteChanged();

                this.StatusMessage = this.updateAvailable
                    ? $"Version {package.Version} is available."
                    : "You're on the latest version.";
            }
            catch (DistributionException dex)
            {
                this.StatusMessage = $"Failed to reach update server: {dex.Message}";
            }
            catch (Exception ex)
            {
                this.StatusMessage = $"Unexpected error while checking for updates: {ex.Message}";
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
            string targetVersion = package.Version ?? this.LatestVersion;
            if (string.IsNullOrWhiteSpace(targetVersion))
            {
                this.StatusMessage = "Cannot determine version to install.";
                return;
            }

            try
            {
                bool hasSize = package.File?.Size.HasValue ?? false;
                this.BeginOperation($"Downloading Mix It Up {targetVersion}...", indeterminate: !hasSize);

                DistributionClient client = this.CreateDistributionClient();
                Progress<int> downloadProgress = new Progress<int>(percent =>
                {
                    this.IsIndeterminate = false;
                    this.ProgressValue = percent;
                });

                byte[] payload = await client
                    .DownloadPackageAsync(
                        package.DownloadUri,
                        TimeSpan.FromMinutes(10),
                        downloadProgress
                    )
                    .ConfigureAwait(true);

                if (payload == null || payload.Length == 0)
                {
                    this.StatusMessage = "Download returned no data.";
                    return;
                }

                string versionRootPath = Path.Combine(this.appRoot, VersionRootName);
                string targetDirectory = Path.Combine(versionRootPath, targetVersion);

                if (Directory.Exists(targetDirectory))
                {
                    Directory.Delete(targetDirectory, recursive: true);
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
                    overwriteExisting: true,
                    progress: extractionProgress,
                    entryPathSelector: entry =>
                    {
                        string entryPath = entry.FullName ?? string.Empty;
                        if (entryPath.StartsWith("Mix It Up/", StringComparison.OrdinalIgnoreCase))
                        {
                            entryPath = entryPath.Substring("Mix It Up/".Length);
                        }

                        return entryPath;
                    }
                );

                string executablePath = Path.Combine(targetDirectory, WindowsExecutableName);
                if (!File.Exists(executablePath))
                {
                    this.StatusMessage = "Installed payload did not contain MixItUp.exe.";
                    return;
                }

                IEnumerable<string> discoveredVersions = Enumerable.Empty<string>();
                try
                {
                discoveredVersions = Directory.Exists(versionRootPath)
                    ? Directory
                        .GetDirectories(versionRootPath)
                        .Select(path => Path.GetFileName(path) ?? string.Empty)
                    : Enumerable.Empty<string>();
                }
                catch (Exception ex)
                {
                    this.StatusMessage = $"Installed but failed to enumerate versions: {ex.Message}";
                }

                BootloaderConfigModel updatedConfig = BootloaderConfigBuilder.BuildOrUpdate(
                    this.currentConfig,
                    targetVersion,
                    discoveredVersions,
                    this.currentConfig?.CurrentVersion,
                    versionRoot: VersionRootName,
                    dataDirName: DataDirectoryName,
                    windowsExecutable: WindowsExecutableName
                );

                BootloaderConfigService.Save(this.bootloaderPath, updatedConfig);
                this.currentConfig = updatedConfig;
                this.UpdateInstalledVersion(targetVersion);
                this.LatestVersion = targetVersion;
                this.pendingPackage = null;
                this.updateAvailable = false;
                this.StatusMessage = $"Mix It Up {targetVersion} is ready.";

                this.UpdateLaunchExecutablePath();
            }
            catch (DistributionException dex)
            {
                this.StatusMessage = $"Update failed: {dex.Message}";
            }
            catch (Exception ex)
            {
                this.StatusMessage = $"Unexpected error during update: {ex.Message}";
            }
            finally
            {
                this.EndOperation();
                this.RaisePropertyChanged(nameof(this.CanInstallUpdate));
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

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(this.launchExecutablePath!)
                {
                    WorkingDirectory = Path.GetDirectoryName(this.launchExecutablePath!) ?? this.appRoot,
                    UseShellExecute = true,
                };

                Process.Start(startInfo);
                Application.Current?.Shutdown();
            }
            catch (Exception ex)
            {
                this.StatusMessage = $"Failed to launch Mix It Up: {ex.Message}";
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
                string versionRoot = Path.Combine(this.appRoot, this.currentConfig?.VersionRoot ?? VersionRootName);
                string currentVersion = this.currentConfig?.CurrentVersion ?? string.Empty;
                string targetExecutable = this.currentConfig?.Executables != null
                    && this.currentConfig.Executables.TryGetValue("windows", out string? configuredExecutable)
                    && !string.IsNullOrWhiteSpace(configuredExecutable)
                        ? configuredExecutable
                        : WindowsExecutableName;

                if (!string.IsNullOrWhiteSpace(currentVersion))
                {
                    executablePath = Path.Combine(versionRoot, currentVersion, targetExecutable);
                }
            }
            catch
            {
                executablePath = string.Empty;
            }

            this.launchExecutablePath = executablePath;
            this.RaisePropertyChanged(nameof(this.CanLaunch));
            this.LaunchCommand.RaiseCanExecuteChanged();
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            this.RaisePropertyChanged(propertyName);
            return true;
        }

        private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
