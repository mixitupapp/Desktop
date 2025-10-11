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
        private static readonly string[] RequiredPolicySlugs = new[] { "eula", "privacy" };
        private const int DefaultRetentionCount = 3;

        private readonly string appRoot;
        private readonly string launcherConfigPath;
        private readonly List<PolicyDocumentState> policyDocuments = new List<PolicyDocumentState>();

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
        private bool policiesAccepted;

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
                    && File.Exists(this.launchExecutablePath)
                    && this.policiesAccepted;
            }
        }

        public IReadOnlyList<PolicyDocumentState> PolicyDocuments
        {
            get { return this.policyDocuments; }
        }

        public bool HasPendingPolicies
        {
            get { return this.policyDocuments.Any(document => !document.IsAccepted); }
        }

        public async Task InitializeAsync()
        {
            await this.LoadInstalledInformationAsync();
            await this.RefreshPolicyStateAsync();
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

                if (!this.policiesAccepted && this.policyDocuments.Count > 0)
                {
                    this.StatusMessage = "Policy updates require review.";
                }
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

        private async Task RefreshPolicyStateAsync(bool showProgress = true)
        {
            bool hadError = false;
            string errorMessage = null;

            if (showProgress)
            {
                this.BeginOperation("Checking policy updates...", true);
            }

            try
            {
                DistributionClient client = this.CreateDistributionClient();
                List<PolicyDocumentState> documents = new List<PolicyDocumentState>();
                bool allAccepted = true;

                Dictionary<string, PolicyAcceptanceModel> acceptedPolicies = this.currentConfig?.AcceptedPolicies;

                foreach (string policySlug in RequiredPolicySlugs)
                {
                    PolicyInfo info = await client.GetLatestPolicyAsync(policySlug).ConfigureAwait(false);
                    PolicyDocumentState document = new PolicyDocumentState(policySlug, info);

                    string key = string.IsNullOrWhiteSpace(document.Policy) ? policySlug : document.Policy;
                    PolicyAcceptanceModel acceptedRecord = null;
                    bool matchesCurrentVersion = false;

                    if (
                        acceptedPolicies != null
                        && acceptedPolicies.TryGetValue(key, out acceptedRecord)
                        && !string.IsNullOrWhiteSpace(acceptedRecord.Version)
                    )
                    {
                        matchesCurrentVersion = string.Equals(
                            acceptedRecord.Version,
                            document.Version,
                            StringComparison.OrdinalIgnoreCase
                        );
                        document.ApplyAcceptanceRecord(acceptedRecord.Version, acceptedRecord.AcceptedAtUtc, matchesCurrentVersion);
                    }
                    else
                    {
                        document.MarkPending();
                    }

                    if (!matchesCurrentVersion)
                    {
                        allAccepted = false;
                    }

                    documents.Add(document);
                }

                this.policyDocuments.Clear();
                this.policyDocuments.AddRange(documents);
                this.policiesAccepted = allAccepted;
            }
            catch (DistributionException dex)
            {
                hadError = true;
                errorMessage = "Failed to retrieve policy updates: " + dex.Message;
                this.policyDocuments.Clear();
                this.policiesAccepted = false;
            }
            catch (Exception ex)
            {
                hadError = true;
                errorMessage = "Unexpected policy check failure: " + ex.Message;
                this.policyDocuments.Clear();
                this.policiesAccepted = false;
            }
            finally
            {
                if (showProgress)
                {
                    this.EndOperation();
                }

                this.RaisePropertyChanged(nameof(this.PolicyDocuments));
                this.RaisePropertyChanged(nameof(this.HasPendingPolicies));
                this.RaisePropertyChanged(nameof(this.CanLaunch));
                this.LaunchCommand.RaiseCanExecuteChanged();

                if (hadError && !string.IsNullOrEmpty(errorMessage))
                {
                    this.StatusMessage = errorMessage;
                }
                else if (!this.policiesAccepted)
                {
                    this.StatusMessage = "Policy updates require review.";
                }
            }
        }

        public void MarkPoliciesAccepted(IEnumerable<PolicyDocumentState> documents)
        {
            if (documents == null)
            {
                return;
            }

            DateTime acceptedAtUtc = DateTime.UtcNow;
            bool anyAccepted = false;

            if (this.currentConfig == null)
            {
                this.currentConfig = new LauncherConfigModel();
            }

            if (this.currentConfig.AcceptedPolicies == null)
            {
                this.currentConfig.AcceptedPolicies = new Dictionary<string, PolicyAcceptanceModel>(
                    StringComparer.OrdinalIgnoreCase
                );
            }

            foreach (PolicyDocumentState document in documents)
            {
                if (document == null)
                {
                    continue;
                }

                string policy = document.Policy;
                string version = document.Version;
                if (string.IsNullOrWhiteSpace(policy) || string.IsNullOrWhiteSpace(version))
                {
                    continue;
                }

                document.ApplyAcceptanceRecord(version, acceptedAtUtc, matchesCurrentVersion: true);
                this.currentConfig.AcceptedPolicies[policy] = new PolicyAcceptanceModel
                {
                    Version = version,
                    AcceptedAtUtc = acceptedAtUtc,
                };
                anyAccepted = true;
            }

            if (anyAccepted)
            {
                this.policiesAccepted = this.policyDocuments.All(doc => doc.IsAccepted);
                this.RaisePropertyChanged(nameof(this.HasPendingPolicies));
                this.RaisePropertyChanged(nameof(this.CanLaunch));
                this.LaunchCommand.RaiseCanExecuteChanged();

                this.StatusMessage = this.policiesAccepted
                    ? "Policies accepted."
                    : "Additional policy reviews required.";
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

            string versionRootPath = Path.Combine(this.appRoot, DistributionPaths.VersionDirectoryName);
            string targetDirectory = Path.Combine(versionRootPath, targetVersion);
            string versionBackupDirectory = null;
            bool versionBackupCreated = false;
            bool installSucceeded = false;
            bool configExistedBefore = File.Exists(this.launcherConfigPath);
            string configBackupPath = null;
            LauncherConfigModel previousConfig = this.currentConfig;
            List<(string Version, string OriginalPath, string BackupPath)> retentionBackups =
                new List<(string, string, string)>();

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

                Directory.CreateDirectory(versionRootPath);

                if (Directory.Exists(targetDirectory))
                {
                    versionBackupDirectory = targetDirectory + ".bak-" + Guid.NewGuid().ToString("N");
                    try
                    {
                        Directory.Move(targetDirectory, versionBackupDirectory);
                        versionBackupCreated = true;
                    }
                    catch (Exception ex)
                    {
                        this.StatusMessage = "Failed to prepare existing version for update: " + ex.Message;
                        return;
                    }
                }

                Directory.CreateDirectory(targetDirectory);

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

                List<(string Version, string Path, DateTime SortKey)> versionDirectories =
                    new List<(string Version, string Path, DateTime SortKey)>();
                try
                {
                    if (Directory.Exists(versionRootPath))
                    {
                        foreach (string path in Directory.GetDirectories(versionRootPath))
                        {
                            string name = Path.GetFileName(path);
                            if (string.IsNullOrEmpty(name))
                            {
                                continue;
                            }

                            DateTime lastWrite;
                            try
                            {
                                lastWrite = Directory.GetLastWriteTimeUtc(path);
                            }
                            catch
                            {
                                lastWrite = DateTime.UtcNow;
                            }

                            versionDirectories.Add((name, path, lastWrite));
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.StatusMessage = "Installed but failed to enumerate versions: " + ex.Message;
                }

                if (
                    !versionDirectories.Any(
                        entry => string.Equals(entry.Version, targetVersion, StringComparison.OrdinalIgnoreCase)
                    )
                )
                {
                    versionDirectories.Add((targetVersion, targetDirectory, DateTime.UtcNow));
                }

                int retentionCount = this.ResolveRetentionCount();
                if (retentionCount < 1)
                {
                    retentionCount = DefaultRetentionCount;
                }

                List<(string Version, string Path, DateTime SortKey)> orderedVersions =
                    versionDirectories
                        .OrderByDescending(entry => entry.SortKey)
                        .ToList();

                HashSet<string> versionsToKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                List<(string Version, string Path, DateTime SortKey)> prioritized = new List<(string, string, DateTime)>();

                (string Version, string Path, DateTime SortKey) targetEntry =
                    orderedVersions.FirstOrDefault(
                        entry => string.Equals(entry.Version, targetVersion, StringComparison.OrdinalIgnoreCase)
                    );
                if (!string.IsNullOrEmpty(targetEntry.Version))
                {
                    prioritized.Add(targetEntry);
                }

                foreach (var entry in orderedVersions)
                {
                    if (
                        !string.IsNullOrEmpty(targetEntry.Version)
                        && string.Equals(entry.Version, targetEntry.Version, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        continue;
                    }

                    prioritized.Add(entry);
                }

                int desiredRetention = Math.Max(retentionCount, 1);
                List<(string Version, string Path, DateTime SortKey)> keptVersions =
                    new List<(string Version, string Path, DateTime SortKey)>();
                foreach (var entry in prioritized)
                {
                    if (versionsToKeep.Count >= desiredRetention)
                    {
                        break;
                    }

                    if (versionsToKeep.Add(entry.Version))
                    {
                        keptVersions.Add(entry);
                    }
                }

                if (keptVersions.Count == 0 && prioritized.Count > 0)
                {
                    var entry = prioritized[0];
                    versionsToKeep.Add(entry.Version);
                    keptVersions.Add(entry);
                }

                List<(string Version, string Path, DateTime SortKey)> pruneTargets =
                    versionDirectories.Where(entry => !versionsToKeep.Contains(entry.Version)).ToList();

                if (pruneTargets.Count > 0)
                {
                    try
                    {
                        foreach (var prune in pruneTargets)
                        {
                            if (!Directory.Exists(prune.Path))
                            {
                                continue;
                            }

                            string backupPath = prune.Path + ".bak-" + Guid.NewGuid().ToString("N");
                            Directory.Move(prune.Path, backupPath);
                            retentionBackups.Add((prune.Version, prune.Path, backupPath));
                        }
                    }
                    catch (Exception ex)
                    {
                        foreach (var backup in retentionBackups)
                        {
                            try
                            {
                                if (Directory.Exists(backup.BackupPath))
                                {
                                    if (Directory.Exists(backup.OriginalPath))
                                    {
                                        Directory.Delete(backup.OriginalPath, true);
                                    }

                                    Directory.Move(backup.BackupPath, backup.OriginalPath);
                                }
                            }
                            catch
                            {
                            }
                        }

                        retentionBackups.Clear();
                        throw new IOException("Failed to prune older versions: " + ex.Message, ex);
                    }
                }

                List<string> keptVersionNames = keptVersions
                    .Select(entry => entry.Version)
                    .Where(version => !string.IsNullOrWhiteSpace(version))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                string installedVersion = this.currentConfig != null ? this.currentConfig.CurrentVersion : null;

                LauncherConfigModel updatedConfig = LauncherConfigBuilder.BuildOrUpdate(
                    this.currentConfig,
                    targetVersion,
                    keptVersionNames,
                    installedVersion,
                    DistributionPaths.VersionDirectoryName,
                    DistributionPaths.DataDirectoryName,
                    DistributionPaths.LauncherExecutableName,
                    retentionCount: retentionCount
                );

                if (configExistedBefore)
                {
                    configBackupPath = this.launcherConfigPath + ".bak-" + Guid.NewGuid().ToString("N");
                    File.Copy(this.launcherConfigPath, configBackupPath, true);
                }

                LauncherConfigService.Save(this.launcherConfigPath, updatedConfig);
                this.currentConfig = updatedConfig;
                this.UpdateInstalledVersion(targetVersion);
                this.LatestVersion = targetVersion;
                this.pendingPackage = null;
                this.updateAvailable = false;

                string completionMessage = retentionBackups.Count > 0
                    ? $"Mix It Up {targetVersion} is ready. Removed {retentionBackups.Count} older version(s)."
                    : "Mix It Up " + targetVersion + " is ready.";

                await this.RefreshPolicyStateAsync(showProgress: false).ConfigureAwait(false);

                if (this.policiesAccepted)
                {
                    this.StatusMessage = completionMessage;
                }

                this.UpdateLaunchExecutablePath();
                installSucceeded = true;
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
                if (!installSucceeded)
                {
                    bool rollbackApplied = false;

                    try
                    {
                        if (Directory.Exists(targetDirectory))
                        {
                            Directory.Delete(targetDirectory, true);
                        }
                    }
                    catch
                    {
                    }

                    if (versionBackupCreated && !string.IsNullOrEmpty(versionBackupDirectory))
                    {
                        try
                        {
                            if (Directory.Exists(versionBackupDirectory))
                            {
                                Directory.Move(versionBackupDirectory, targetDirectory);
                                rollbackApplied = true;
                            }
                        }
                        catch
                        {
                        }
                    }

                    if (!string.IsNullOrEmpty(configBackupPath) && File.Exists(configBackupPath))
                    {
                        bool restoredConfig = false;
                        try
                        {
                            File.Copy(configBackupPath, this.launcherConfigPath, true);
                            restoredConfig = true;
                            rollbackApplied = true;
                        }
                        catch
                        {
                        }

                        if (restoredConfig)
                        {
                            try
                            {
                                File.Delete(configBackupPath);
                            }
                            catch
                            {
                            }
                        }
                    }
                    else if (!configExistedBefore)
                    {
                        try
                        {
                            if (File.Exists(this.launcherConfigPath))
                            {
                                File.Delete(this.launcherConfigPath);
                                rollbackApplied = true;
                            }
                        }
                        catch
                        {
                        }
                    }

                    foreach (var backup in retentionBackups)
                    {
                        try
                        {
                            if (Directory.Exists(backup.BackupPath))
                            {
                                if (Directory.Exists(backup.OriginalPath))
                                {
                                    Directory.Delete(backup.OriginalPath, true);
                                }

                                Directory.Move(backup.BackupPath, backup.OriginalPath);
                                rollbackApplied = true;
                            }
                        }
                        catch
                        {
                        }
                    }

                    retentionBackups.Clear();

                    this.currentConfig = previousConfig;
                    this.UpdateLaunchExecutablePath();
                    this.UpdateInstalledVersion(previousConfig != null ? previousConfig.CurrentVersion : null);

                    if (
                        rollbackApplied
                        && !string.IsNullOrEmpty(this.StatusMessage)
                        && this.StatusMessage.IndexOf("Previous version restored", StringComparison.OrdinalIgnoreCase) < 0
                    )
                    {
                        this.StatusMessage += " Previous version restored.";
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(versionBackupDirectory) && Directory.Exists(versionBackupDirectory))
                    {
                        try
                        {
                            Directory.Delete(versionBackupDirectory, true);
                        }
                        catch
                        {
                        }
                    }

                    if (!string.IsNullOrEmpty(configBackupPath) && File.Exists(configBackupPath))
                    {
                        try
                        {
                            File.Delete(configBackupPath);
                        }
                        catch
                        {
                        }
                    }

                    foreach (var backup in retentionBackups)
                    {
                        try
                        {
                            if (Directory.Exists(backup.BackupPath))
                            {
                                Directory.Delete(backup.BackupPath, true);
                            }
                        }
                        catch
                        {
                        }
                    }

                    retentionBackups.Clear();
                }

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

        private int ResolveRetentionCount()
        {
            int? configured = this.currentConfig != null ? this.currentConfig.RetentionCount : null;
            if (configured.HasValue && configured.Value > 0)
            {
                return configured.Value;
            }

            return DefaultRetentionCount;
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




