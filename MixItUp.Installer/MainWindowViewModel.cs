using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MixItUp.Base.Model.API;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MixItUp.Installer
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public const string InstallerLogFileName = "MixItUp-Installer-Log.txt";
        public static readonly string InstallerLogFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory ?? Environment.CurrentDirectory,
            InstallerLogFileName
        );
        public const string ShortcutFileName = "Mix It Up.lnk";

        public const string OldApplicationSettingsFileName = "ApplicationSettings.xml";
        public const string NewApplicationSettingsFileName = "ApplicationSettings.json";

        public const string MixItUpProcessName = "MixItUp";
        public const string MixItUpStreamBotProcessName = "MixItUp.StreamBot";
        public const string AutoHosterProcessName = "MixItUp.AutoHoster";

        private static readonly IReadOnlyList<string> TargetProcessNames = new[]
        {
            MixItUpProcessName,
            MixItUpStreamBotProcessName,
            AutoHosterProcessName,
        };

    private const string LauncherProductSlug = "mixitup-desktop";
    private const string LauncherPlatform = "windows-x64";
    private const string AppProductSlug = "mixitup-desktop";
    private const string AppPlatform = "windows-x64";

        private sealed class UpdateManifestModel
        {
            [JsonProperty("schemaVersion")]
            public string SchemaVersion { get; set; }

            [JsonProperty("product")]
            public string Product { get; set; }

            [JsonProperty("channel")]
            public string Channel { get; set; }

            [JsonProperty("version")]
            public string Version { get; set; }

            [JsonProperty("releasedAt")]
            public DateTime? ReleasedAt { get; set; }

            [JsonProperty("releaseType")]
            public string ReleaseType { get; set; }

            [JsonProperty("platforms")]
            public List<UpdatePlatformModel> Platforms { get; set; }
        }

        private sealed class UpdatePlatformModel
        {
            [JsonProperty("platform")]
            public string Platform { get; set; }

            [JsonProperty("files")]
            public List<UpdateFileModel> Files { get; set; }
        }

        private sealed class UpdateFileModel
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("url")]
            public string Url { get; set; }

            [JsonProperty("size")]
            public long? Size { get; set; }

            [JsonProperty("sha256")]
            public string Sha256 { get; set; }

            [JsonProperty("contentType")]
            public string ContentType { get; set; }

            [JsonProperty("arch")]
            public string Architecture { get; set; }
        }

        private sealed class UpdatePackageInfo
        {
            public UpdatePackageInfo(
                string version,
                string channel,
                string platform,
                UpdateFileModel file,
                Uri downloadUri
            )
            {
                this.Version = version;
                this.Channel = channel;
                this.Platform = platform;
                this.File = file;
                this.DownloadUri = downloadUri;
            }

            public string Version { get; }

            public string Channel { get; }

            public string Platform { get; }

            public UpdateFileModel File { get; }

            public Uri DownloadUri { get; }
        }

        public static readonly string DefaultInstallDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MixItUp"
        );
        public static readonly string StartMenuDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Mix It Up"
        );

        public static string InstallSettingsDirectory
        {
            get { return Path.Combine(MainWindowViewModel.InstallSettingsDirectory, "Settings"); }
        }

        public static byte[] ZipArchiveData { get; set; }

        public static string StartMenuShortCutFilePath
        {
            get { return Path.Combine(StartMenuDirectory, ShortcutFileName); }
        }
        public static string DesktopShortCutFilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    ShortcutFileName
                );
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private bool isSupportedOS;
        public bool IsSupportedOS
        {
            get { return this.isSupportedOS; }
            set { this.SetProperty(ref this.isSupportedOS, value); }
        }

        private bool is64BitOS;
        public bool Is64BitOS
        {
            get { return this.is64BitOS; }
            set { this.SetProperty(ref this.is64BitOS, value); }
        }

        private string osVersionDisplay;
        public string OSVersionDisplay
        {
            get { return this.osVersionDisplay; }
            set { this.SetProperty(ref this.osVersionDisplay, value); }
        }

        private string appRoot;
        public string AppRoot
        {
            get { return this.appRoot; }
            set
            {
                if (this.SetProperty(ref this.appRoot, value))
                {
                    this.RefreshPathState();
                }
            }
        }

        private string runningDirectory;
        public string RunningDirectory
        {
            get { return this.runningDirectory; }
            set
            {
                if (this.SetProperty(ref this.runningDirectory, value))
                {
                    this.RefreshPathState();
                }
            }
        }

        private bool isRunningFromAppRoot;
        public bool IsRunningFromAppRoot
        {
            get { return this.isRunningFromAppRoot; }
            private set { this.SetProperty(ref this.isRunningFromAppRoot, value); }
        }

        private string bootloaderConfigPath;
        public string BootloaderConfigPath
        {
            get { return this.bootloaderConfigPath; }
            private set { this.SetProperty(ref this.bootloaderConfigPath, value); }
        }

        private string versionedAppDirRoot;
        public string VersionedAppDirRoot
        {
            get { return this.versionedAppDirRoot; }
            private set { this.SetProperty(ref this.versionedAppDirRoot, value); }
        }

        private string downloadTempPath;
        public string DownloadTempPath
        {
            get { return this.downloadTempPath; }
            private set { this.SetProperty(ref this.downloadTempPath, value); }
        }

        private string pendingVersionDirectoryPath;
        public string PendingVersionDirectoryPath
        {
            get { return this.pendingVersionDirectoryPath; }
            private set { this.SetProperty(ref this.pendingVersionDirectoryPath, value); }
        }

        private bool targetDirExists;
        public bool TargetDirExists
        {
            get { return this.targetDirExists; }
            private set { this.SetProperty(ref this.targetDirExists, value); }
        }

        private bool targetExeExists;
        public bool TargetExeExists
        {
            get { return this.targetExeExists; }
            private set { this.SetProperty(ref this.targetExeExists, value); }
        }

        private bool portableCandidateFound;
        public bool PortableCandidateFound
        {
            get { return this.portableCandidateFound; }
            set { this.SetProperty(ref this.portableCandidateFound, value); }
        }

        private bool legacyDetected;
        public bool LegacyDetected
        {
            get { return this.legacyDetected; }
            set { this.SetProperty(ref this.legacyDetected, value); }
        }

        private bool migrationAlreadyDone;
        public bool MigrationAlreadyDone
        {
            get { return this.migrationAlreadyDone; }
            set { this.SetProperty(ref this.migrationAlreadyDone, value); }
        }

        private string updateServerBaseUrl;
        public string UpdateServerBaseUrl
        {
            get { return this.updateServerBaseUrl; }
            set { this.SetProperty(ref this.updateServerBaseUrl, value); }
        }

        private string latestVersion;
        public string LatestVersion
        {
            get { return this.latestVersion; }
            set { this.SetProperty(ref this.latestVersion, value); }
        }

        private string installedVersion;
        public string InstalledVersion
        {
            get { return this.installedVersion; }
            set { this.SetProperty(ref this.installedVersion, value); }
        }

        private ObservableCollection<string> activityLog;
        public ObservableCollection<string> ActivityLog
        {
            get { return this.activityLog; }
            private set { this.SetProperty(ref this.activityLog, value); }
        }

        private int downloadPercent;
        public int DownloadPercent
        {
            get { return this.downloadPercent; }
            set { this.SetProperty(ref this.downloadPercent, value); }
        }

        private string errorMessage;
        public string ErrorMessage
        {
            get { return this.errorMessage; }
            set
            {
                if (this.SetProperty(ref this.errorMessage, value))
                {
                    this.HasError = !string.IsNullOrEmpty(value);
                }
            }
        }

        private bool hasError;
        public bool HasError
        {
            get { return this.hasError; }
            private set { this.SetProperty(ref this.hasError, value); }
        }

        private string legacyDataPath;
        public string LegacyDataPath
        {
            get { return this.legacyDataPath; }
            set { this.SetProperty(ref this.legacyDataPath, value); }
        }

        private ObservableCollection<string> stepExecutionOrder;
        public ObservableCollection<string> StepExecutionOrder
        {
            get { return this.stepExecutionOrder; }
            private set { this.SetProperty(ref this.stepExecutionOrder, value); }
        }

        private bool stepPreflightPending;
        public bool StepPreflightPending
        {
            get { return this.stepPreflightPending; }
            set { this.SetProperty(ref this.stepPreflightPending, value); }
        }

        private bool stepPreflightInProgress;
        public bool StepPreflightInProgress
        {
            get { return this.stepPreflightInProgress; }
            set { this.SetProperty(ref this.stepPreflightInProgress, value); }
        }

        private bool stepPreflightDone;
        public bool StepPreflightDone
        {
            get { return this.stepPreflightDone; }
            set { this.SetProperty(ref this.stepPreflightDone, value); }
        }

        private bool stepDiscoverPending;
        public bool StepDiscoverPending
        {
            get { return this.stepDiscoverPending; }
            set { this.SetProperty(ref this.stepDiscoverPending, value); }
        }

        private bool stepDiscoverInProgress;
        public bool StepDiscoverInProgress
        {
            get { return this.stepDiscoverInProgress; }
            set { this.SetProperty(ref this.stepDiscoverInProgress, value); }
        }

        private bool stepDiscoverDone;
        public bool StepDiscoverDone
        {
            get { return this.stepDiscoverDone; }
            set { this.SetProperty(ref this.stepDiscoverDone, value); }
        }

        private bool stepCloseProcessesPending;
        public bool StepCloseProcessesPending
        {
            get { return this.stepCloseProcessesPending; }
            set { this.SetProperty(ref this.stepCloseProcessesPending, value); }
        }

        private bool stepCloseProcessesInProgress;
        public bool StepCloseProcessesInProgress
        {
            get { return this.stepCloseProcessesInProgress; }
            set { this.SetProperty(ref this.stepCloseProcessesInProgress, value); }
        }

        private bool stepCloseProcessesDone;
        public bool StepCloseProcessesDone
        {
            get { return this.stepCloseProcessesDone; }
            set { this.SetProperty(ref this.stepCloseProcessesDone, value); }
        }

        private bool stepMigratePending;
        public bool StepMigratePending
        {
            get { return this.stepMigratePending; }
            set { this.SetProperty(ref this.stepMigratePending, value); }
        }

        private bool stepMigrateInProgress;
        public bool StepMigrateInProgress
        {
            get { return this.stepMigrateInProgress; }
            set { this.SetProperty(ref this.stepMigrateInProgress, value); }
        }

        private bool stepMigrateDone;
        public bool StepMigrateDone
        {
            get { return this.stepMigrateDone; }
            set { this.SetProperty(ref this.stepMigrateDone, value); }
        }

        private bool stepLauncherFetchPending;
        public bool StepLauncherFetchPending
        {
            get { return this.stepLauncherFetchPending; }
            set { this.SetProperty(ref this.stepLauncherFetchPending, value); }
        }

        private bool stepLauncherFetchInProgress;
        public bool StepLauncherFetchInProgress
        {
            get { return this.stepLauncherFetchInProgress; }
            set { this.SetProperty(ref this.stepLauncherFetchInProgress, value); }
        }

        private bool stepLauncherFetchDone;
        public bool StepLauncherFetchDone
        {
            get { return this.stepLauncherFetchDone; }
            set { this.SetProperty(ref this.stepLauncherFetchDone, value); }
        }

        private bool stepLauncherInstallPending;
        public bool StepLauncherInstallPending
        {
            get { return this.stepLauncherInstallPending; }
            set { this.SetProperty(ref this.stepLauncherInstallPending, value); }
        }

        private bool stepLauncherInstallInProgress;
        public bool StepLauncherInstallInProgress
        {
            get { return this.stepLauncherInstallInProgress; }
            set { this.SetProperty(ref this.stepLauncherInstallInProgress, value); }
        }

        private bool stepLauncherInstallDone;
        public bool StepLauncherInstallDone
        {
            get { return this.stepLauncherInstallDone; }
            set { this.SetProperty(ref this.stepLauncherInstallDone, value); }
        }

        private bool stepAppFetchPending;
        public bool StepAppFetchPending
        {
            get { return this.stepAppFetchPending; }
            set { this.SetProperty(ref this.stepAppFetchPending, value); }
        }

        private bool stepAppFetchInProgress;
        public bool StepAppFetchInProgress
        {
            get { return this.stepAppFetchInProgress; }
            set { this.SetProperty(ref this.stepAppFetchInProgress, value); }
        }

        private bool stepAppFetchDone;
        public bool StepAppFetchDone
        {
            get { return this.stepAppFetchDone; }
            set { this.SetProperty(ref this.stepAppFetchDone, value); }
        }

        private bool stepAppExtractPending;
        public bool StepAppExtractPending
        {
            get { return this.stepAppExtractPending; }
            set { this.SetProperty(ref this.stepAppExtractPending, value); }
        }

        private bool stepAppExtractInProgress;
        public bool StepAppExtractInProgress
        {
            get { return this.stepAppExtractInProgress; }
            set { this.SetProperty(ref this.stepAppExtractInProgress, value); }
        }

        private bool stepAppExtractDone;
        public bool StepAppExtractDone
        {
            get { return this.stepAppExtractDone; }
            set { this.SetProperty(ref this.stepAppExtractDone, value); }
        }

        private bool stepDataCopyPending;
        public bool StepDataCopyPending
        {
            get { return this.stepDataCopyPending; }
            set { this.SetProperty(ref this.stepDataCopyPending, value); }
        }

        private bool stepDataCopyInProgress;
        public bool StepDataCopyInProgress
        {
            get { return this.stepDataCopyInProgress; }
            set { this.SetProperty(ref this.stepDataCopyInProgress, value); }
        }

        private bool stepDataCopyDone;
        public bool StepDataCopyDone
        {
            get { return this.stepDataCopyDone; }
            set { this.SetProperty(ref this.stepDataCopyDone, value); }
        }

        private bool stepConfigWritePending;
        public bool StepConfigWritePending
        {
            get { return this.stepConfigWritePending; }
            set { this.SetProperty(ref this.stepConfigWritePending, value); }
        }

        private bool stepConfigWriteInProgress;
        public bool StepConfigWriteInProgress
        {
            get { return this.stepConfigWriteInProgress; }
            set { this.SetProperty(ref this.stepConfigWriteInProgress, value); }
        }

        private bool stepConfigWriteDone;
        public bool StepConfigWriteDone
        {
            get { return this.stepConfigWriteDone; }
            set { this.SetProperty(ref this.stepConfigWriteDone, value); }
        }

        private bool stepRegisterPending;
        public bool StepRegisterPending
        {
            get { return this.stepRegisterPending; }
            set { this.SetProperty(ref this.stepRegisterPending, value); }
        }

        private bool stepRegisterInProgress;
        public bool StepRegisterInProgress
        {
            get { return this.stepRegisterInProgress; }
            set { this.SetProperty(ref this.stepRegisterInProgress, value); }
        }

        private bool stepRegisterDone;
        public bool StepRegisterDone
        {
            get { return this.stepRegisterDone; }
            set { this.SetProperty(ref this.stepRegisterDone, value); }
        }

        private bool stepShortcutsPending;
        public bool StepShortcutsPending
        {
            get { return this.stepShortcutsPending; }
            set { this.SetProperty(ref this.stepShortcutsPending, value); }
        }

        private bool stepShortcutsInProgress;
        public bool StepShortcutsInProgress
        {
            get { return this.stepShortcutsInProgress; }
            set { this.SetProperty(ref this.stepShortcutsInProgress, value); }
        }

        private bool stepShortcutsDone;
        public bool StepShortcutsDone
        {
            get { return this.stepShortcutsDone; }
            set { this.SetProperty(ref this.stepShortcutsDone, value); }
        }

        private bool stepCompletePending;
        public bool StepCompletePending
        {
            get { return this.stepCompletePending; }
            set { this.SetProperty(ref this.stepCompletePending, value); }
        }

        private bool stepCompleteInProgress;
        public bool StepCompleteInProgress
        {
            get { return this.stepCompleteInProgress; }
            set { this.SetProperty(ref this.stepCompleteInProgress, value); }
        }

        private bool stepCompleteDone;
        public bool StepCompleteDone
        {
            get { return this.stepCompleteDone; }
            set { this.SetProperty(ref this.stepCompleteDone, value); }
        }

        public ICommand CancelCommand { get; private set; }

        public ICommand OpenLogCommand { get; private set; }

        public ICommand LaunchCommand { get; private set; }

        public bool IsUpdate
        {
            get { return this.isUpdate; }
            private set
            {
                this.isUpdate = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged("IsInstall");
            }
        }
        private bool isUpdate;

        public bool IsInstall
        {
            get { return !this.IsUpdate; }
        }

        public bool IsPreview
        {
            get { return this.isPreview; }
            private set
            {
                this.isPreview = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool isPreview;

        public bool IsTest
        {
            get { return this.isTest; }
            private set
            {
                this.isTest = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool isTest;

        public bool IsOperationBeingPerformed
        {
            get { return this.isOperationBeingPerformed; }
            private set
            {
                this.isOperationBeingPerformed = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool isOperationBeingPerformed;

        public bool IsOperationIndeterminate
        {
            get { return this.isOperationIndeterminate; }
            private set
            {
                this.isOperationIndeterminate = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool isOperationIndeterminate;

        public int OperationProgress
        {
            get { return this.operationProgress; }
            private set
            {
                this.operationProgress = value;
                this.NotifyPropertyChanged();
            }
        }
        private int operationProgress;

        public string DisplayText1
        {
            get { return this.displayText1; }
            private set
            {
                this.displayText1 = value;
                this.NotifyPropertyChanged();
            }
        }
        private string displayText1;

        public string DisplayText2
        {
            get { return this.displayText2; }
            private set
            {
                this.displayText2 = value;
                this.NotifyPropertyChanged();
            }
        }
        private string displayText2;

        public bool ErrorOccurred
        {
            get { return this.errorOccurred; }
            private set
            {
                this.errorOccurred = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool errorOccurred;

        public string SpecificErrorMessage
        {
            get { return this.specificErrorMessage; }
            private set
            {
                this.specificErrorMessage = value;
                this.NotifyPropertyChanged();
            }
        }
        private string specificErrorMessage;

        public string HyperlinkAddress
        {
            get { return this.hyperlinkAddress; }
            private set
            {
                this.hyperlinkAddress = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged("ShowHyperlinkAddress");
            }
        }
        private string hyperlinkAddress;

        public bool ShowHyperlinkAddress
        {
            get { return !string.IsNullOrEmpty(this.HyperlinkAddress); }
        }

        private string installDirectory;

        public MainWindowViewModel()
        {
            this.ActivityLog = new ObservableCollection<string>();
            this.StepExecutionOrder = new ObservableCollection<string>(
                new[]
                {
                    "Preflight",
                    "Discover",
                    "CloseProcesses",
                    "Migrate",
                    "LauncherFetch",
                    "LauncherInstall",
                    "AppFetch",
                    "AppExtract",
                    "DataCopy",
                    "ConfigWrite",
                    "Register",
                    "Shortcuts",
                    "Complete",
                }
            );
            this.ResetStepStates();

            this.UpdateServerBaseUrl = "https://files.mixitupapp.com";
            this.LegacyDataPath = string.Empty;
            this.PendingVersionDirectoryPath = string.Empty;
            this.LatestVersion = string.Empty;
            this.InstalledVersion = string.Empty;
            this.DownloadPercent = 0;
            this.ErrorMessage = string.Empty;

            this.RunningDirectory = Path.GetFullPath(
                AppDomain.CurrentDomain.BaseDirectory ?? Environment.CurrentDirectory
            );

            this.installDirectory = DefaultInstallDirectory;
            this.AppRoot = this.installDirectory;

            string[] args = Environment.GetCommandLineArgs();
            if (args.Length == 2 && !string.IsNullOrWhiteSpace(args[1]))
            {
                this.installDirectory = args[1];
                this.AppRoot = this.installDirectory;
            }

            this.UpdateEnvironmentState();

            if (Directory.Exists(this.installDirectory))
            {
                this.IsUpdate = true;
                string applicationSettingsFilePath = Path.Combine(
                    this.installDirectory,
                    NewApplicationSettingsFileName
                );
                if (!File.Exists(applicationSettingsFilePath))
                {
                    applicationSettingsFilePath = Path.Combine(
                        this.installDirectory,
                        OldApplicationSettingsFileName
                    );
                }

                if (File.Exists(applicationSettingsFilePath))
                {
                    using (
                        StreamReader reader = new StreamReader(
                            File.OpenRead(applicationSettingsFilePath)
                        )
                    )
                    {
                        JObject jobj = JObject.Parse(reader.ReadToEnd());
                        if (jobj != null)
                        {
                            if (jobj.ContainsKey("PreviewProgram"))
                            {
                                this.IsPreview = jobj["PreviewProgram"].ToObject<bool>();
                            }

                            if (jobj.ContainsKey("TestBuild"))
                            {
                                this.IsTest = jobj["TestBuild"].ToObject<bool>();
                            }
                        }
                    }
                }
            }

            this.DisplayText1 = "Preparing installation...";
            this.isOperationBeingPerformed = true;
            this.IsOperationIndeterminate = true;
        }

        private void ResetStepStates()
        {
            this.StepPreflightPending = true;
            this.StepPreflightInProgress = false;
            this.StepPreflightDone = false;

            this.StepDiscoverPending = true;
            this.StepDiscoverInProgress = false;
            this.StepDiscoverDone = false;

            this.StepCloseProcessesPending = true;
            this.StepCloseProcessesInProgress = false;
            this.StepCloseProcessesDone = false;

            this.StepMigratePending = true;
            this.StepMigrateInProgress = false;
            this.StepMigrateDone = false;

            this.StepLauncherFetchPending = true;
            this.StepLauncherFetchInProgress = false;
            this.StepLauncherFetchDone = false;

            this.StepLauncherInstallPending = true;
            this.StepLauncherInstallInProgress = false;
            this.StepLauncherInstallDone = false;

            this.StepAppFetchPending = true;
            this.StepAppFetchInProgress = false;
            this.StepAppFetchDone = false;

            this.StepAppExtractPending = true;
            this.StepAppExtractInProgress = false;
            this.StepAppExtractDone = false;

            this.StepDataCopyPending = true;
            this.StepDataCopyInProgress = false;
            this.StepDataCopyDone = false;

            this.StepConfigWritePending = true;
            this.StepConfigWriteInProgress = false;
            this.StepConfigWriteDone = false;

            this.StepRegisterPending = true;
            this.StepRegisterInProgress = false;
            this.StepRegisterDone = false;

            this.StepShortcutsPending = true;
            this.StepShortcutsInProgress = false;
            this.StepShortcutsDone = false;

            this.StepCompletePending = true;
            this.StepCompleteInProgress = false;
            this.StepCompleteDone = false;
        }

        private void UpdateEnvironmentState()
        {
            this.Is64BitOS = Environment.Is64BitOperatingSystem;
            this.OSVersionDisplay = Environment.OSVersion.VersionString;
            this.IsSupportedOS = this.IsSupportedWindowsVersion();
        }

        private bool Preflight()
        {
            this.DisplayText1 = "Validating system requirements...";
            this.DisplayText2 = string.Empty;
            this.IsOperationIndeterminate = true;
            this.IsOperationBeingPerformed = true;
            this.LogActivity("Starting preflight checks...");

            this.StepPreflightPending = false;
            this.StepPreflightInProgress = true;
            this.StepPreflightDone = false;

            this.OSVersionDisplay = Environment.OSVersion.VersionString;
            this.LogActivity($"Detected operating system: {this.OSVersionDisplay}");

            this.IsSupportedOS = this.IsSupportedWindowsVersion();
            if (!this.IsSupportedOS)
            {
                this.LogActivity("Unsupported Windows version detected.");
                this.StepPreflightInProgress = false;
                this.HasError = true;
                this.ShowError(
                    "Unsupported Windows Version",
                    "MixItUp requires Windows 10 or 11 (64-bit)."
                );
                return false;
            }
            this.LogActivity("Windows version is supported.");

            this.Is64BitOS = Environment.Is64BitOperatingSystem;
            if (!this.Is64BitOS)
            {
                this.LogActivity("Unsupported architecture detected (not 64-bit).");
                this.StepPreflightInProgress = false;
                this.HasError = true;
                this.ShowError(
                    "Unsupported Architecture",
                    "MixItUp requires Windows 10/11 (64-bit)."
                );
                return false;
            }
            this.LogActivity("64-bit operating system confirmed.");

            if (!this.ValidateWritePermissions())
            {
                this.LogActivity("Write permission check failed.");
                this.StepPreflightInProgress = false;
                this.HasError = true;
                this.ShowError(
                    "Write Permission Denied",
                    "Installer needs write access to %LOCALAPPDATA%/MixItUp, the Start Menu, or Desktop. Run with sufficient permissions."
                );
                return false;
            }

            this.LogActivity("Write permissions validated for required locations.");

            this.StepPreflightInProgress = false;
            this.StepPreflightDone = true;
            this.DisplayText1 = "System requirements validated.";
            this.LogActivity("Preflight checks completed successfully.");
            return true;
        }

        private bool ValidateWritePermissions()
        {
            bool success = true;

            success &= this.TryValidateWriteAccess(this.AppRoot, "AppRoot");
            success &= this.TryValidateWriteAccess(StartMenuDirectory, "Start Menu");

            string desktopPath = Environment.GetFolderPath(
                Environment.SpecialFolder.DesktopDirectory
            );
            success &= this.TryValidateWriteAccess(desktopPath, "Desktop");

            return success;
        }

        private Task<bool> DiscoverInstallContextAsync()
        {
            return Task.FromResult(this.DiscoverInstallContext());
        }

        private bool DiscoverInstallContext()
        {
            this.DisplayText1 = "Discovering install context...";
            this.DisplayText2 = string.Empty;
            this.IsOperationIndeterminate = true;
            this.IsOperationBeingPerformed = true;

            this.StepDiscoverPending = false;
            this.StepDiscoverInProgress = true;
            this.StepDiscoverDone = false;

            this.LogActivity("Starting install context discovery...");

            string resolvedAppRoot = this.AppRoot;
            if (string.IsNullOrWhiteSpace(resolvedAppRoot))
            {
                resolvedAppRoot = DefaultInstallDirectory;
                this.LogActivity(
                    $"AppRoot not provided; defaulting to {NormalizePath(resolvedAppRoot)}."
                );
                this.AppRoot = resolvedAppRoot;
            }

            string normalizedAppRoot = NormalizePath(resolvedAppRoot);
            if (!string.Equals(resolvedAppRoot, normalizedAppRoot, StringComparison.Ordinal))
            {
                this.AppRoot = normalizedAppRoot;
                resolvedAppRoot = normalizedAppRoot;
            }

            string resolvedRunningDirectory = Path.GetFullPath(
                AppDomain.CurrentDomain.BaseDirectory ?? Environment.CurrentDirectory
            );
            this.RunningDirectory = resolvedRunningDirectory;

            string normalizedRunningDirectory = NormalizePath(resolvedRunningDirectory);

            this.LogActivity($"AppRoot resolved to: {normalizedAppRoot}");
            this.LogActivity($"Installer running from: {normalizedRunningDirectory}");

            try
            {
                if (!Directory.Exists(resolvedAppRoot))
                {
                    this.LogActivity("AppRoot directory not found. Creating...");
                }

                Directory.CreateDirectory(resolvedAppRoot);
                this.LogActivity("AppRoot directory is available.");
            }
            catch (Exception ex)
            {
                this.StepDiscoverInProgress = false;
                this.StepDiscoverDone = false;

                this.LogActivity(
                    $"Failed to ensure AppRoot directory exists: {ex.GetType().Name} - {ex.Message}"
                );
                this.WriteToLogFile(ex.ToString());

                this.ShowError(
                    "Write Permission Denied",
                    $"Installer needs write access to {normalizedAppRoot}. Run with sufficient permissions."
                );

                return false;
            }

            bool targetDirExists = Directory.Exists(resolvedAppRoot);
            bool targetExeExists = File.Exists(Path.Combine(resolvedAppRoot, "MixItUp.exe"));
            bool versionDirExists = Directory.Exists(Path.Combine(resolvedAppRoot, "app"));
            string bootloaderPath = Path.Combine(resolvedAppRoot, "bootloader.json");
            bool bootloaderExists = File.Exists(bootloaderPath);
            bool migrationAlreadyDone = versionDirExists || bootloaderExists;

            bool isRunningFromAppRoot =
                !string.IsNullOrEmpty(normalizedAppRoot)
                && string.Equals(
                    normalizedAppRoot,
                    normalizedRunningDirectory,
                    StringComparison.OrdinalIgnoreCase
                );

            bool portableCandidateFound =
                !isRunningFromAppRoot
                && File.Exists(Path.Combine(resolvedRunningDirectory, "MixItUp.exe"));

            bool legacyDetected = targetExeExists && !migrationAlreadyDone;
            bool isUpdate = targetExeExists || bootloaderExists;

            this.TargetDirExists = targetDirExists;
            this.TargetExeExists = targetExeExists;
            this.MigrationAlreadyDone = migrationAlreadyDone;
            this.LegacyDetected = legacyDetected;
            this.PortableCandidateFound = portableCandidateFound;
            this.IsRunningFromAppRoot = isRunningFromAppRoot;
            this.IsUpdate = isUpdate;

            this.BootloaderConfigPath = bootloaderPath;
            this.VersionedAppDirRoot = Path.Combine(resolvedAppRoot, "app");
            this.DownloadTempPath = Path.Combine(resolvedAppRoot, ".tmp");

            if (isRunningFromAppRoot)
            {
                this.LogActivity("Installer is running from the target AppRoot directory.");
            }
            else
            {
                this.LogActivity("Installer is running from a separate directory.");
            }

            if (portableCandidateFound)
            {
                this.LogActivity("Portable install identified in the running directory.");
            }

            if (legacyDetected)
            {
                this.LogActivity("Legacy layout detected at the AppRoot.");
            }

            if (migrationAlreadyDone)
            {
                this.LogActivity("Migration markers detected (app directory or bootloader).");
            }

            if (isUpdate)
            {
                this.LogActivity("Existing installation detected; update path selected.");
            }
            else
            {
                this.LogActivity("No existing installation detected; fresh install path selected.");
            }

            this.StepDiscoverInProgress = false;
            this.StepDiscoverDone = true;
            this.DisplayText1 = "Install context discovered.";
            this.DisplayText2 = string.Empty;

            return true;
        }

        private Task<bool> MigrateIfNeededAsync()
        {
            return Task.FromResult(this.MigrateIfNeeded());
        }

        private bool MigrateIfNeeded()
        {
            this.DisplayText1 = "Preparing existing files...";
            this.DisplayText2 = string.Empty;
            this.IsOperationIndeterminate = true;
            this.IsOperationBeingPerformed = true;

            this.StepMigratePending = false;
            this.StepMigrateInProgress = true;
            this.StepMigrateDone = false;

            if (this.MigrationAlreadyDone)
            {
                this.LogActivity("Migration step skipped: already completed previously.");
                this.PendingVersionDirectoryPath = string.Empty;
                this.StepMigrateInProgress = false;
                this.StepMigrateDone = true;
                return true;
            }

            if (!this.LegacyDetected && !this.PortableCandidateFound)
            {
                this.LogActivity("Migration step skipped: no legacy or portable install detected.");
                this.PendingVersionDirectoryPath = string.Empty;
                this.StepMigrateInProgress = false;
                this.StepMigrateDone = true;
                return true;
            }

            string versionRoot = this.VersionedAppDirRoot;
            if (string.IsNullOrWhiteSpace(versionRoot))
            {
                versionRoot = Path.Combine(this.AppRoot ?? DefaultInstallDirectory, "app");
                this.VersionedAppDirRoot = versionRoot;
            }

            try
            {
                Directory.CreateDirectory(versionRoot);
            }
            catch (Exception ex)
            {
                this.LogActivity(
                    $"Failed to ensure version root exists: {ex.GetType().Name} - {ex.Message}"
                );
                this.WriteToLogFile(ex.ToString());
                this.StepMigrateInProgress = false;
                this.StepMigrateDone = false;
                this.ShowError(
                    "Migration Failed",
                    "Unable to prepare version directory. Check permissions and retry."
                );
                this.HyperlinkAddress = new Uri(InstallerLogFilePath).AbsoluteUri;
                return false;
            }

            bool isLegacySource = this.LegacyDetected;
            string sourceDirectory = isLegacySource ? this.AppRoot : this.RunningDirectory;
            if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
            {
                this.LogActivity("Migration aborted: source directory not found.");
                this.StepMigrateInProgress = false;
                this.StepMigrateDone = false;
                this.ShowError("Migration Failed", "Unable to locate existing installation files.");
                return false;
            }

            string migrationFolderName = "legacy-temp";
            string migrationFolderPath = Path.Combine(versionRoot, migrationFolderName);
            int folderSuffix = 1;
            while (Directory.Exists(migrationFolderPath))
            {
                migrationFolderName = $"legacy-temp-{folderSuffix}";
                migrationFolderPath = Path.Combine(versionRoot, migrationFolderName);
                folderSuffix++;
            }

            try
            {
                Directory.CreateDirectory(migrationFolderPath);
            }
            catch (Exception ex)
            {
                this.LogActivity(
                    $"Failed to create migration directory: {ex.GetType().Name} - {ex.Message}"
                );
                this.WriteToLogFile(ex.ToString());
                this.StepMigrateInProgress = false;
                this.StepMigrateDone = false;
                this.ShowError(
                    "Migration Failed",
                    "Unable to create migration workspace under AppRoot."
                );
                this.HyperlinkAddress = new Uri(InstallerLogFilePath).AbsoluteUri;
                return false;
            }

            this.PendingVersionDirectoryPath = migrationFolderPath;

            string normalizedSourceDirectory = NormalizePath(sourceDirectory);
            string sourceKind = isLegacySource ? "legacy layout" : "portable layout";
            this.LogActivity($"Migrating {sourceKind} from '{normalizedSourceDirectory}'.");
            string normalizedInstallerPath = string.Empty;
            try
            {
                Process currentProcess = Process.GetCurrentProcess();
                string modulePath = currentProcess?.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(modulePath))
                {
                    normalizedInstallerPath = NormalizePath(modulePath);
                }
            }
            catch (Win32Exception)
            {
                // Access to process modules can fail under certain environments; ignore gracefully.
            }
            catch (InvalidOperationException)
            {
                // Process module information might be unavailable; ignore gracefully.
            }

            int filesMigrated = 0;
            int directoriesMigrated = 0;
            List<string> skippedItems = new List<string>();

            try
            {
                foreach (string entry in Directory.EnumerateFileSystemEntries(sourceDirectory))
                {
                    string entryName = Path.GetFileName(entry);
                    if (string.IsNullOrEmpty(entryName))
                    {
                        continue;
                    }

                    string normalizedEntry = NormalizePath(entry);

                    if (
                        string.Equals(
                            normalizedEntry,
                            NormalizePath(migrationFolderPath),
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        continue;
                    }

                    if (
                        !string.IsNullOrEmpty(this.VersionedAppDirRoot)
                        && string.Equals(
                            normalizedEntry,
                            NormalizePath(this.VersionedAppDirRoot),
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        continue;
                    }

                    if (
                        !string.IsNullOrEmpty(this.DownloadTempPath)
                        && string.Equals(
                            normalizedEntry,
                            NormalizePath(this.DownloadTempPath),
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        continue;
                    }

                    string destinationPath = Path.Combine(migrationFolderPath, entryName);

                    if (Directory.Exists(entry))
                    {
                        this.CopyDirectoryRecursive(
                            entry,
                            destinationPath,
                            normalizedInstallerPath,
                            ref filesMigrated,
                            ref directoriesMigrated,
                            skippedItems
                        );

                        if (isLegacySource)
                        {
                            this.TryDeleteDirectory(entry);
                        }
                    }
                    else if (File.Exists(entry))
                    {
                        if (
                            !string.IsNullOrEmpty(normalizedInstallerPath)
                            && string.Equals(
                                normalizedEntry,
                                normalizedInstallerPath,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        {
                            skippedItems.Add(entryName);
                            continue;
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                        File.Copy(entry, destinationPath, overwrite: true);
                        filesMigrated++;

                        if (isLegacySource)
                        {
                            this.TryDeleteFile(entry);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.LogActivity($"Migration failed: {ex.GetType().Name} - {ex.Message}");
                this.WriteToLogFile(ex.ToString());
                this.StepMigrateInProgress = false;
                this.StepMigrateDone = false;
                this.ShowError(
                    "Migration Failed",
                    "Unable to migrate existing files. Check permissions and retry."
                );
                this.HyperlinkAddress = new Uri(InstallerLogFilePath).AbsoluteUri;
                return false;
            }

            string migratedDataPath = Path.Combine(migrationFolderPath, "data");
            if (Directory.Exists(migratedDataPath))
            {
                this.LegacyDataPath = migratedDataPath;
            }
            else
            {
                string originalDataPath = Path.Combine(sourceDirectory, "data");
                this.LegacyDataPath = Directory.Exists(originalDataPath)
                    ? originalDataPath
                    : string.Empty;
            }

            string contextDescription = isLegacySource ? "legacy" : "portable";
            this.LogActivity(
                $"Migration prepared from {contextDescription} source '{normalizedSourceDirectory}'."
            );
            this.LogActivity(
                $"Migrated {directoriesMigrated} directories and {filesMigrated} files into '{NormalizePath(migrationFolderPath)}'."
            );

            if (skippedItems.Count > 0)
            {
                string skippedPreview = string.Join(", ", skippedItems.Take(5));
                if (skippedItems.Count > 5)
                {
                    skippedPreview += ", ...";
                }
                this.LogActivity(
                    $"Skipped {skippedItems.Count} item(s) during migration: {skippedPreview}."
                );
            }

            this.StepMigrateInProgress = false;
            this.StepMigrateDone = true;
            this.DisplayText1 = "Existing files prepared for update.";

            return true;
        }

        private bool TryValidateWriteAccess(string path, string displayName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                this.LogActivity($"{displayName} path is not configured.");
                return false;
            }

            string normalizedPath = NormalizePath(path);
            this.LogActivity($"Validating write access to {displayName} ({normalizedPath})...");

            string tempFilePath = null;

            try
            {
                Directory.CreateDirectory(normalizedPath);

                tempFilePath = Path.Combine(
                    normalizedPath,
                    $"mixitup_installer_perm_{Guid.NewGuid():N}.tmp"
                );

                using (FileStream stream = File.Create(tempFilePath))
                {
                    stream.WriteByte(0);
                }

                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // Swallow cleanup errors; we'll retry below if needed.
                }

                this.LogActivity($"Write access confirmed for {displayName}.");
                return true;
            }
            catch (Exception ex)
            {
                this.LogActivity($"Write access check failed for {displayName}: {ex.Message}");
                this.WriteToLogFile(ex.ToString());
                if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch
                    {
                        // Suppress cleanup errors
                    }
                }
                return false;
            }
        }

        private void CopyDirectoryRecursive(
            string sourceDir,
            string destinationDir,
            string installerPath,
            ref int filesCopied,
            ref int directoriesCopied,
            List<string> skippedItems
        )
        {
            Directory.CreateDirectory(destinationDir);
            directoriesCopied++;

            foreach (string filePath in Directory.GetFiles(sourceDir))
            {
                string normalizedFilePath = NormalizePath(filePath);
                if (
                    !string.IsNullOrEmpty(installerPath)
                    && string.Equals(
                        normalizedFilePath,
                        installerPath,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    if (skippedItems != null)
                    {
                        skippedItems.Add(Path.GetFileName(filePath));
                    }
                    continue;
                }

                string destinationFilePath = Path.Combine(
                    destinationDir,
                    Path.GetFileName(filePath)
                );
                File.Copy(filePath, destinationFilePath, overwrite: true);
                filesCopied++;
            }

            foreach (string dirPath in Directory.GetDirectories(sourceDir))
            {
                string destinationSubDir = Path.Combine(destinationDir, Path.GetFileName(dirPath));
                this.CopyDirectoryRecursive(
                    dirPath,
                    destinationSubDir,
                    installerPath,
                    ref filesCopied,
                    ref directoriesCopied,
                    skippedItems
                );
            }
        }

        private void TryDeleteFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                this.WriteToLogFile($"Failed to delete file '{filePath}': {ex}");
            }
        }

        private void TryDeleteDirectory(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return;
            }

            try
            {
                if (Directory.Exists(directoryPath))
                {
                    Directory.Delete(directoryPath, recursive: true);
                }
            }
            catch (Exception ex)
            {
                this.WriteToLogFile($"Failed to delete directory '{directoryPath}': {ex}");
            }
        }

        private bool IsSupportedWindowsVersion()
        {
            OperatingSystem os = Environment.OSVersion;
            if (os.Platform != PlatformID.Win32NT)
            {
                return false;
            }

            if (os.Version.Major >= 10)
            {
                return true;
            }

            return false;
        }

        private async Task<bool> WaitForProcessesToExitAsync()
        {
            this.DisplayText1 = "Closing Mix It Up and companion apps...";
            this.DisplayText2 = string.Empty;
            this.IsOperationIndeterminate = true;
            this.IsOperationBeingPerformed = true;

            this.StepCloseProcessesPending = false;
            this.StepCloseProcessesInProgress = true;
            this.StepCloseProcessesDone = false;

            this.LogActivity("Inspecting running Mix It Up processes...");

            List<Process> initialProcesses = this.GetTargetProcesses();
            if (initialProcesses.Count == 0)
            {
                this.LogActivity("No running Mix It Up processes detected.");
                this.DisplayText1 = "No Mix It Up processes detected.";
                this.StepCloseProcessesInProgress = false;
                this.StepCloseProcessesDone = true;
                return true;
            }

            string initialNames = string.Join(
                ", ",
                initialProcesses
                    .Select(p => p.ProcessName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
            );
            this.LogActivity($"Detected running processes: {initialNames}.");

            foreach (Process process in initialProcesses)
            {
                process.Dispose();
            }

            const int timeoutSeconds = 10;

            for (int elapsedSeconds = 0; elapsedSeconds < timeoutSeconds; elapsedSeconds++)
            {
                List<Process> processes = this.GetTargetProcesses();
                if (processes.Count == 0)
                {
                    this.StepCloseProcessesInProgress = false;
                    this.StepCloseProcessesDone = true;
                    this.LogActivity("All Mix It Up processes have exited.");
                    this.DisplayText1 = "Mix It Up processes closed.";
                    return true;
                }

                int secondsRemaining = timeoutSeconds - elapsedSeconds;
                string processNames = string.Join(
                    ", ",
                    processes.Select(p => p.ProcessName).Distinct(StringComparer.OrdinalIgnoreCase)
                );
                this.LogActivity(
                    $"Waiting for {processNames} to close... {secondsRemaining}s remaining."
                );

                if (secondsRemaining == 5)
                {
                    foreach (Process process in processes)
                    {
                        try
                        {
                            if (!process.HasExited)
                            {
                                this.LogActivity($"Requesting {process.ProcessName} to close.");
                                process.CloseMainWindow();
                            }
                        }
                        catch (Exception ex)
                        {
                            this.WriteToLogFile(
                                $"Failed to signal {process.ProcessName} to close: {ex}"
                            );
                        }
                    }
                }

                foreach (Process process in processes)
                {
                    process.Dispose();
                }

                await Task.Delay(1000);
            }

            List<Process> remainingProcesses = this.GetTargetProcesses();
            if (remainingProcesses.Count > 0)
            {
                string processNames = string.Join(
                    ", ",
                    remainingProcesses
                        .Select(p => p.ProcessName)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                );
                this.LogActivity($"Processes still running after timeout: {processNames}.");

                foreach (Process process in remainingProcesses)
                {
                    process.Dispose();
                }

                this.StepCloseProcessesInProgress = false;
                this.HasError = true;
                this.ShowError(
                    "Close MixItUp",
                    "Please close MixItUp or AutoHoster before continuing."
                );
                return false;
            }

            this.StepCloseProcessesInProgress = false;
            this.StepCloseProcessesDone = true;
            this.LogActivity("All Mix It Up processes have exited.");
            this.DisplayText1 = "Mix It Up processes closed.";
            return true;
        }

        private List<Process> GetTargetProcesses()
        {
            List<Process> processes = new List<Process>();
            foreach (string processName in TargetProcessNames)
            {
                try
                {
                    processes.AddRange(Process.GetProcessesByName(processName));
                }
                catch (Exception ex)
                {
                    this.WriteToLogFile(
                        $"Unable to enumerate process '{processName}': {ex.Message}"
                    );
                }
            }
            return processes;
        }

        private void RefreshPathState()
        {
            string normalizedAppRoot = NormalizePath(this.appRoot);
            string normalizedRunningDir = NormalizePath(this.runningDirectory);

            if (!string.IsNullOrEmpty(this.appRoot))
            {
                this.BootloaderConfigPath = Path.Combine(this.appRoot, "bootloader.json");
                this.VersionedAppDirRoot = Path.Combine(this.appRoot, "app");
                this.DownloadTempPath = Path.Combine(this.appRoot, ".tmp");
            }
            else
            {
                this.BootloaderConfigPath = null;
                this.VersionedAppDirRoot = null;
                this.DownloadTempPath = null;
            }

            this.TargetDirExists =
                !string.IsNullOrEmpty(this.appRoot) && Directory.Exists(this.appRoot);

            string potentialExePath = null;
            if (!string.IsNullOrEmpty(this.appRoot))
            {
                potentialExePath = Path.Combine(this.appRoot, "MixItUp.exe");
                if (
                    !File.Exists(potentialExePath)
                    && !string.IsNullOrEmpty(this.versionedAppDirRoot)
                )
                {
                    potentialExePath = Path.Combine(this.versionedAppDirRoot, "MixItUp.exe");
                }
            }

            this.TargetExeExists =
                !string.IsNullOrEmpty(potentialExePath) && File.Exists(potentialExePath);

            this.IsRunningFromAppRoot =
                !string.IsNullOrEmpty(normalizedAppRoot)
                && string.Equals(
                    normalizedAppRoot,
                    normalizedRunningDir,
                    StringComparison.OrdinalIgnoreCase
                );
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                path = Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }

            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public async Task<bool> RunAsync()
        {
            this.ResetLogFile();
            this.ResetStepStates();
            this.ActivityLog.Clear();

            this.ErrorMessage = string.Empty;
            this.SpecificErrorMessage = string.Empty;
            this.DisplayText2 = string.Empty;
            this.ErrorOccurred = false;
            this.HasError = false;
            this.HyperlinkAddress = string.Empty;

            this.IsOperationBeingPerformed = true;
            this.IsOperationIndeterminate = true;
            this.OperationProgress = 0;
            this.DownloadPercent = 0;
            this.LogActivity("Mix It Up installer initialized.");

            if (!this.Preflight())
            {
                return false;
            }

            if (!await this.DiscoverInstallContextAsync())
            {
                return false;
            }

            if (!await this.WaitForProcessesToExitAsync())
            {
                return false;
            }

            if (!await this.MigrateIfNeededAsync())
            {
                return false;
            }

            this.StepLauncherFetchPending = false;
            this.StepLauncherFetchInProgress = true;

            UpdatePackageInfo launcherPackage = await this.ResolveLauncherPackageAsync();
            if (launcherPackage == null)
            {
                this.StepLauncherFetchInProgress = false;
                return false;
            }

            IProgress<int> launcherProgress = new Progress<int>(percent =>
            {
                this.OperationProgress = percent;
                this.DownloadPercent = percent;
            });

            byte[] launcherArchive = await this.DownloadLauncherArchiveAsync(
                launcherPackage,
                launcherProgress
            );

            if (launcherArchive == null || launcherArchive.Length == 0)
            {
                this.StepLauncherFetchInProgress = false;
                return false;
            }

            this.StepLauncherFetchInProgress = false;
            this.StepLauncherFetchDone = true;

            this.StepLauncherInstallPending = false;
            this.StepLauncherInstallInProgress = true;

            bool launcherInstalled = this.InstallLauncherArchive(launcherArchive, launcherPackage);

            if (launcherArchive != null)
            {
                Array.Clear(launcherArchive, 0, launcherArchive.Length);
            }
            launcherArchive = null;

            if (!launcherInstalled)
            {
                this.StepLauncherInstallInProgress = false;
                return false;
            }

            this.StepLauncherInstallInProgress = false;
            this.StepLauncherInstallDone = true;

            this.OperationProgress = 0;
            this.DownloadPercent = 0;
            this.IsOperationIndeterminate = true;

            this.StepAppFetchPending = false;
            this.StepAppFetchInProgress = true;

            UpdatePackageInfo appPackage = await this.ResolveAppPackageAsync();
            if (appPackage == null)
            {
                this.StepAppFetchInProgress = false;
                return false;
            }

            IProgress<int> appProgress = new Progress<int>(percent =>
            {
                this.OperationProgress = percent;
                this.DownloadPercent = percent;
            });

            byte[] appArchive = await this.DownloadAppArchiveAsync(appPackage, appProgress);

            if (appArchive == null || appArchive.Length == 0)
            {
                this.StepAppFetchInProgress = false;
                return false;
            }

            this.StepAppFetchInProgress = false;
            this.StepAppFetchDone = true;

            this.StepAppExtractPending = false;
            this.StepAppExtractInProgress = true;

            bool appInstalled = this.InstallAppArchive(appArchive, appPackage);

            if (appArchive != null)
            {
                Array.Clear(appArchive, 0, appArchive.Length);
            }
            appArchive = null;

            if (!appInstalled)
            {
                this.StepAppExtractInProgress = false;
                return false;
            }

            this.StepAppExtractInProgress = false;
            this.StepAppExtractDone = true;

            this.OperationProgress = 0;
            this.DownloadPercent = 0;
            this.IsOperationIndeterminate = true;

            return await this.RunLegacyPipelineAsync();
        }

        private string ResolveUpdateChannel()
        {
            if (this.IsTest)
            {
                return "test";
            }

            if (this.IsPreview)
            {
                return "preview";
            }

            return "production";
        }

        private string BuildManifestUrl(string product, string platform, string channel)
        {
            string trimmedBase = (this.UpdateServerBaseUrl ?? string.Empty).TrimEnd('/');
            return string.Format(
                "{0}/updates/{1}/{2}/{3}/latest",
                trimmedBase,
                product,
                platform,
                channel
            );
        }

        private Uri BuildDownloadUri(string fileUrl)
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                return null;
            }

            if (Uri.TryCreate(fileUrl, UriKind.Absolute, out Uri absoluteUri))
            {
                return absoluteUri;
            }

            string trimmedBase = (this.UpdateServerBaseUrl ?? string.Empty).TrimEnd('/');
            if (!Uri.TryCreate(trimmedBase, UriKind.Absolute, out Uri baseUri))
            {
                return null;
            }

            string relativePath = fileUrl.StartsWith("/") ? fileUrl : "/" + fileUrl;
            if (Uri.TryCreate(baseUri, relativePath, out Uri combinedUri))
            {
                return combinedUri;
            }

            return null;
        }

    private async Task<UpdatePackageInfo> ResolveLauncherPackageAsync()
        {
            string channel = this.ResolveUpdateChannel();
            string manifestUrl = this.BuildManifestUrl(LauncherProductSlug, LauncherPlatform, channel);

            this.DisplayText1 = "Checking for launcher updates...";
            this.DisplayText2 = string.Empty;
            this.IsOperationIndeterminate = true;
            this.OperationProgress = 0;
            this.DownloadPercent = 0;

            this.LogActivity($"Requesting launcher manifest from {manifestUrl}");

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(15);

                    HttpResponseMessage response = await client.GetAsync(manifestUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorBody = await response.Content.ReadAsStringAsync();
                        this.WriteToLogFile($"{manifestUrl} - {response.StatusCode} - {errorBody}");
                        this.ShowError(
                            "Download Failed",
                            "Couldn't reach the update server. Check connection and try again."
                        );
                        return null;
                    }

                    string responseBody = await response.Content.ReadAsStringAsync();
                    UpdateManifestModel manifest =
                        JsonConvert.DeserializeObject<UpdateManifestModel>(responseBody);

                    if (manifest == null)
                    {
                        this.WriteToLogFile("Launcher manifest response was empty or invalid.");
                        this.ShowError(
                            "Download Failed",
                            "Couldn't reach the update server. Check connection and try again."
                        );
                        return null;
                    }

                    UpdatePlatformModel platform = manifest.Platforms?.FirstOrDefault(p =>
                        string.Equals(
                            p.Platform,
                            LauncherPlatform,
                            StringComparison.OrdinalIgnoreCase
                        )
                    );

                    if (platform == null)
                    {
                        this.WriteToLogFile(
                            $"Launcher manifest missing expected platform '{LauncherPlatform}'."
                        );
                        this.ShowError(
                            "Download Failed",
                            "Couldn't reach the update server. Check connection and try again."
                        );
                        return null;
                    }

                    UpdateFileModel file =
                        platform.Files?.FirstOrDefault(f =>
                            string.Equals(
                                f.ContentType,
                                "application/zip",
                                StringComparison.OrdinalIgnoreCase
                            )
                        ) ?? platform.Files?.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f.Url));

                    if (file == null || string.IsNullOrWhiteSpace(file.Url))
                    {
                        this.WriteToLogFile(
                            "Launcher manifest did not include a valid download file."
                        );
                        this.ShowError(
                            "Download Failed",
                            "Couldn't reach the update server. Check connection and try again."
                        );
                        return null;
                    }

                    Uri downloadUri = this.BuildDownloadUri(file.Url);
                    if (downloadUri == null)
                    {
                        this.WriteToLogFile(
                            $"Unable to construct launcher download URL from '{file.Url}'."
                        );
                        this.ShowError(
                            "Download Failed",
                            "Couldn't reach the update server. Check connection and try again."
                        );
                        return null;
                    }

                    string sanitizedUrl = downloadUri.GetLeftPart(UriPartial.Path);
                    this.LogActivity(
                        $"Launcher manifest resolved version {manifest.Version} ({channel})."
                    );
                    this.LogActivity($"Launcher download endpoint: {sanitizedUrl}");

                    this.LatestVersion = manifest.Version ?? string.Empty;
                    this.DisplayText2 = string.IsNullOrEmpty(manifest.Version)
                        ? string.Empty
                        : $"Version {manifest.Version}";

                    return new UpdatePackageInfo(
                        manifest.Version ?? string.Empty,
                        manifest.Channel ?? channel,
                        platform.Platform,
                        file,
                        downloadUri
                    );
                }
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    || ex is TaskCanceledException
                    || ex is JsonException
                )
            {
                this.WriteToLogFile(ex.ToString());
                this.ShowError(
                    "Download Failed",
                    "Couldn't reach the update server. Check connection and try again."
                );
            }

            return null;
        }

        private async Task<UpdatePackageInfo> ResolveAppPackageAsync()
        {
            string channel = this.ResolveUpdateChannel();
            string manifestUrl = this.BuildManifestUrl(AppProductSlug, AppPlatform, channel);

            this.DisplayText1 = "Checking for application updates...";
            this.DisplayText2 = string.Empty;
            this.IsOperationIndeterminate = true;
            this.OperationProgress = 0;
            this.DownloadPercent = 0;

            this.LogActivity($"Requesting application manifest from {manifestUrl}");

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(15);

                    HttpResponseMessage response = await client.GetAsync(manifestUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorBody = await response.Content.ReadAsStringAsync();
                        this.WriteToLogFile($"{manifestUrl} - {response.StatusCode} - {errorBody}");
                        this.ShowError(
                            "Download Failed",
                            "Couldn't reach the update server. Check connection and try again."
                        );
                        return null;
                    }

                    string responseBody = await response.Content.ReadAsStringAsync();
                    UpdateManifestModel manifest =
                        JsonConvert.DeserializeObject<UpdateManifestModel>(responseBody);

                    if (manifest == null)
                    {
                        this.WriteToLogFile("Application manifest response was empty or invalid.");
                        this.ShowError(
                            "Download Failed",
                            "Couldn't reach the update server. Check connection and try again."
                        );
                        return null;
                    }

                    UpdatePlatformModel platform = manifest.Platforms?.FirstOrDefault(p =>
                        string.Equals(
                            p.Platform,
                            AppPlatform,
                            StringComparison.OrdinalIgnoreCase
                        )
                    );

                    if (platform == null)
                    {
                        this.WriteToLogFile(
                            $"Application manifest missing expected platform '{AppPlatform}'."
                        );
                        this.ShowError(
                            "Download Failed",
                            "Couldn't reach the update server. Check connection and try again."
                        );
                        return null;
                    }

                    UpdateFileModel file =
                        platform.Files?.FirstOrDefault(f =>
                            string.Equals(
                                f.ContentType,
                                "application/zip",
                                StringComparison.OrdinalIgnoreCase
                            )
                        ) ?? platform.Files?.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f.Url));

                    if (file == null || string.IsNullOrWhiteSpace(file.Url))
                    {
                        this.WriteToLogFile(
                            "Application manifest did not include a valid download file."
                        );
                        this.ShowError(
                            "Download Failed",
                            "Couldn't reach the update server. Check connection and try again."
                        );
                        return null;
                    }

                    Uri downloadUri = this.BuildDownloadUri(file.Url);
                    if (downloadUri == null)
                    {
                        this.WriteToLogFile(
                            $"Unable to construct application download URL from '{file.Url}'."
                        );
                        this.ShowError(
                            "Download Failed",
                            "Couldn't reach the update server. Check connection and try again."
                        );
                        return null;
                    }

                    string sanitizedUrl = downloadUri.GetLeftPart(UriPartial.Path);
                    this.LogActivity(
                        $"Application manifest resolved version {manifest.Version} ({channel})."
                    );
                    this.LogActivity($"Application download endpoint: {sanitizedUrl}");

                    this.LatestVersion = manifest.Version ?? string.Empty;
                    this.DisplayText2 = string.IsNullOrEmpty(manifest.Version)
                        ? string.Empty
                        : $"Version {manifest.Version}";

                    return new UpdatePackageInfo(
                        manifest.Version ?? string.Empty,
                        manifest.Channel ?? channel,
                        platform.Platform,
                        file,
                        downloadUri
                    );
                }
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    || ex is TaskCanceledException
                    || ex is JsonException
                )
            {
                this.WriteToLogFile(ex.ToString());
                this.ShowError(
                    "Download Failed",
                    "Couldn't reach the update server. Check connection and try again."
                );
            }

            return null;
        }

        private async Task<byte[]> DownloadLauncherArchiveAsync(
            UpdatePackageInfo package,
            IProgress<int> progress
        )
        {
            if (package == null)
            {
                return null;
            }

            this.DisplayText1 = "Downloading launcher...";
            this.DisplayText2 = string.IsNullOrEmpty(package.Version)
                ? string.Empty
                : $"Version {package.Version}";
            this.IsOperationIndeterminate = false;
            this.OperationProgress = 0;
            this.DownloadPercent = 0;

            string sanitizedUrl = package.DownloadUri.GetLeftPart(UriPartial.Path);
            this.LogActivity($"Starting launcher download from {sanitizedUrl}");

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(5);

                    using (
                        HttpResponseMessage response = await client.GetAsync(
                            package.DownloadUri,
                            HttpCompletionOption.ResponseHeadersRead
                        )
                    )
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            string errorBody = await response.Content.ReadAsStringAsync();
                            this.WriteToLogFile(
                                $"{package.DownloadUri} - {response.StatusCode} - {errorBody}"
                            );
                            this.ShowError(
                                "Download Failed",
                                "Couldn't reach the update server. Check connection and try again."
                            );
                            return null;
                        }

                        long? contentLength = response.Content.Headers.ContentLength;
                        this.IsOperationIndeterminate = !contentLength.HasValue;
                        progress?.Report(0);

                        using (Stream contentStream = await response.Content.ReadAsStreamAsync())
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            byte[] buffer = new byte[81920];
                            long totalRead = 0;
                            int read;
                            while (
                                (read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0
                            )
                            {
                                memoryStream.Write(buffer, 0, read);
                                totalRead += read;

                                if (contentLength.HasValue && contentLength.Value > 0)
                                {
                                    int percent = (int)
                                        Math.Min(100, (totalRead * 100) / contentLength.Value);
                                    progress?.Report(percent);
                                }
                            }

                            progress?.Report(100);

                            double sizeInMb = memoryStream.Length / 1024d / 1024d;
                            this.LogActivity(
                                $"Launcher download complete ({sizeInMb:F2} MB received)."
                            );

                            return memoryStream.ToArray();
                        }
                    }
                }
            }
            catch (Exception ex)
                when (ex is HttpRequestException || ex is TaskCanceledException || ex is IOException
                )
            {
                this.WriteToLogFile(ex.ToString());
                this.ShowError(
                    "Download Failed",
                    "Couldn't reach the update server. Check connection and try again."
                );
            }

            return null;
        }

        private async Task<byte[]> DownloadAppArchiveAsync(
            UpdatePackageInfo package,
            IProgress<int> progress
        )
        {
            if (package == null)
            {
                return null;
            }

            this.DisplayText1 = "Downloading application payload...";
            this.DisplayText2 = string.IsNullOrEmpty(package.Version)
                ? string.Empty
                : $"Version {package.Version}";
            this.IsOperationIndeterminate = false;
            this.OperationProgress = 0;
            this.DownloadPercent = 0;

            string sanitizedUrl = package.DownloadUri.GetLeftPart(UriPartial.Path);
            this.LogActivity($"Starting application download from {sanitizedUrl}");

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(10);

                    using (
                        HttpResponseMessage response = await client.GetAsync(
                            package.DownloadUri,
                            HttpCompletionOption.ResponseHeadersRead
                        )
                    )
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            string errorBody = await response.Content.ReadAsStringAsync();
                            this.WriteToLogFile(
                                $"{package.DownloadUri} - {response.StatusCode} - {errorBody}"
                            );
                            this.ShowError(
                                "Download Failed",
                                "Couldn't reach the update server. Check connection and try again."
                            );
                            return null;
                        }

                        long? contentLength = response.Content.Headers.ContentLength;
                        this.IsOperationIndeterminate = !contentLength.HasValue;
                        progress?.Report(0);

                        using (Stream contentStream = await response.Content.ReadAsStreamAsync())
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            byte[] buffer = new byte[81920];
                            long totalRead = 0;
                            int read;
                            while (
                                (read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0
                            )
                            {
                                memoryStream.Write(buffer, 0, read);
                                totalRead += read;

                                if (contentLength.HasValue && contentLength.Value > 0)
                                {
                                    int percent = (int)
                                        Math.Min(100, (totalRead * 100) / contentLength.Value);
                                    progress?.Report(percent);
                                }
                            }

                            progress?.Report(100);

                            double sizeInMb = memoryStream.Length / 1024d / 1024d;
                            this.LogActivity(
                                $"Application download complete ({sizeInMb:F2} MB received)."
                            );

                            return memoryStream.ToArray();
                        }
                    }
                }
            }
            catch (Exception ex)
                when (ex is HttpRequestException || ex is TaskCanceledException || ex is IOException)
            {
                this.WriteToLogFile(ex.ToString());
                this.ShowError(
                    "Download Failed",
                    "Couldn't reach the update server. Check connection and try again."
                );
            }

            return null;
        }

    private bool InstallLauncherArchive(byte[] archiveBytes, UpdatePackageInfo package)
        {
            this.DisplayText1 = "Installing launcher...";
            this.DisplayText2 = string.IsNullOrEmpty(package?.Version)
                ? string.Empty
                : $"Version {package.Version}";
            this.IsOperationIndeterminate = false;
            this.OperationProgress = 0;

            if (archiveBytes == null || archiveBytes.Length == 0)
            {
                this.WriteToLogFile("Launcher archive data was empty.");
                this.ShowError(
                    "Package Corrupt",
                    "The downloaded launcher package is invalid. Please try again."
                );
                return false;
            }

            try
            {
                Directory.CreateDirectory(this.AppRoot);
                string normalizedAppRoot = Path.GetFullPath(this.AppRoot);

                using (MemoryStream zipStream = new MemoryStream(archiveBytes))
                using (ZipArchive archive = new ZipArchive(zipStream))
                {
                    if (archive.Entries.Count == 0)
                    {
                        this.WriteToLogFile("Launcher archive contained no entries.");
                        this.ShowError(
                            "Package Corrupt",
                            "The downloaded launcher package is invalid. Please try again."
                        );
                        return false;
                    }

                    double processed = 0;
                    double total = archive.Entries.Count;

                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string entryPath = entry.FullName.Replace('/', Path.DirectorySeparatorChar);

                        if (string.IsNullOrWhiteSpace(entryPath))
                        {
                            processed++;
                            continue;
                        }

                        string destinationPath = Path.GetFullPath(
                            Path.Combine(normalizedAppRoot, entryPath)
                        );

                        if (
                            !destinationPath.StartsWith(
                                normalizedAppRoot,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        {
                            this.WriteToLogFile(
                                $"Skipping extraction of '{entry.FullName}' due to path traversal."
                            );
                            processed++;
                            continue;
                        }

                        if (
                            destinationPath.EndsWith(
                                Path.DirectorySeparatorChar.ToString(),
                                StringComparison.Ordinal
                            )
                        )
                        {
                            Directory.CreateDirectory(destinationPath);
                        }
                        else
                        {
                            string parentDirectory = Path.GetDirectoryName(destinationPath);
                            if (!string.IsNullOrEmpty(parentDirectory))
                            {
                                Directory.CreateDirectory(parentDirectory);
                            }

                            entry.ExtractToFile(destinationPath, overwrite: true);
                        }

                        processed++;
                        this.OperationProgress =
                            total > 0
                                ? (int)Math.Min(100, Math.Round((processed / total) * 100))
                                : 100;
                    }
                }

                string launcherPath = Path.Combine(this.AppRoot, "MixItUp.exe");
                if (!File.Exists(launcherPath))
                {
                    this.WriteToLogFile(
                        "Launcher extraction completed but MixItUp.exe was not found."
                    );
                    this.ShowError(
                        "Package Corrupt",
                        "The downloaded launcher package is invalid. Please try again."
                    );
                    return false;
                }

                this.LogActivity("Launcher files extracted successfully.");
                return true;
            }
            catch (InvalidDataException idex)
            {
                this.WriteToLogFile(idex.ToString());
                this.ShowError(
                    "Package Corrupt",
                    "The downloaded launcher package is invalid. Please try again."
                );
            }
            catch (UnauthorizedAccessException uaex)
            {
                this.WriteToLogFile(uaex.ToString());
                this.ShowError(
                    "Package Corrupt",
                    "We couldn't write files to the installation directory. Check permissions and try again."
                );
            }
            catch (IOException ioex)
            {
                this.WriteToLogFile(ioex.ToString());
                this.ShowError(
                    "Package Corrupt",
                    "We couldn't write files to the installation directory. Check permissions and try again."
                );
            }

            return false;
        }

        private bool InstallAppArchive(byte[] archiveBytes, UpdatePackageInfo package)
        {
            this.DisplayText1 = "Extracting application payload...";
            this.DisplayText2 = string.IsNullOrEmpty(package?.Version)
                ? string.Empty
                : $"Version {package.Version}";
            this.IsOperationIndeterminate = false;
            this.OperationProgress = 0;

            if (archiveBytes == null || archiveBytes.Length == 0)
            {
                this.WriteToLogFile("Application archive data was empty.");
                this.ShowError(
                    "Package Corrupt",
                    "The downloaded application payload is invalid. Please try again."
                );
                return false;
            }

            string version = package?.Version;
            if (string.IsNullOrWhiteSpace(version))
            {
                version = this.LatestVersion;
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                this.WriteToLogFile("Application manifest did not include a version identifier.");
                this.ShowError(
                    "Package Corrupt",
                    "The downloaded application payload is invalid. Please try again."
                );
                return false;
            }

            string versionRoot = this.VersionedAppDirRoot;
            if (string.IsNullOrWhiteSpace(versionRoot))
            {
                versionRoot = Path.Combine(this.AppRoot ?? DefaultInstallDirectory, "app");
                this.VersionedAppDirRoot = versionRoot;
            }

            try
            {
                Directory.CreateDirectory(versionRoot);
            }
            catch (Exception ex)
            {
                this.WriteToLogFile(ex.ToString());
                this.ShowError(
                    "Package Corrupt",
                    "We couldn't prepare the application directory. Check permissions and try again."
                );
                return false;
            }

            string versionDirectory = Path.Combine(versionRoot, version);
            string normalizedVersionDirectory;
            try
            {
                Directory.CreateDirectory(versionDirectory);
                normalizedVersionDirectory = Path.GetFullPath(versionDirectory);
            }
            catch (Exception ex)
            {
                this.WriteToLogFile(ex.ToString());
                this.ShowError(
                    "Package Corrupt",
                    "We couldn't prepare the versioned application directory. Check permissions and try again."
                );
                return false;
            }

            try
            {
                using (MemoryStream zipStream = new MemoryStream(archiveBytes))
                using (ZipArchive archive = new ZipArchive(zipStream))
                {
                    if (archive.Entries.Count == 0)
                    {
                        this.WriteToLogFile("Application archive contained no entries.");
                        this.ShowError(
                            "Package Corrupt",
                            "The downloaded application payload is invalid. Please try again."
                        );
                        return false;
                    }

                    double processed = 0;
                    double total = archive.Entries.Count;

                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string entryPath = entry.FullName ?? string.Empty;
                        if (entryPath.StartsWith("Mix It Up/", StringComparison.OrdinalIgnoreCase))
                        {
                            entryPath = entryPath.Substring("Mix It Up/".Length);
                        }

                        entryPath = entryPath.Replace('/', Path.DirectorySeparatorChar);

                        if (string.IsNullOrWhiteSpace(entryPath))
                        {
                            processed++;
                            continue;
                        }

                        string destinationPath = Path.GetFullPath(
                            Path.Combine(normalizedVersionDirectory, entryPath)
                        );

                        if (
                            !destinationPath.StartsWith(
                                normalizedVersionDirectory,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        {
                            this.WriteToLogFile(
                                $"Skipping extraction of '{entry.FullName}' due to path traversal."
                            );
                            processed++;
                            continue;
                        }

                        bool isDirectoryEntry = entry.FullName.EndsWith("/", StringComparison.Ordinal);
                        if (
                            isDirectoryEntry
                            || destinationPath.EndsWith(
                                Path.DirectorySeparatorChar.ToString(),
                                StringComparison.Ordinal
                            )
                        )
                        {
                            Directory.CreateDirectory(destinationPath);
                        }
                        else
                        {
                            string parentDirectory = Path.GetDirectoryName(destinationPath);
                            if (!string.IsNullOrEmpty(parentDirectory))
                            {
                                Directory.CreateDirectory(parentDirectory);
                            }

                            entry.ExtractToFile(destinationPath, overwrite: true);
                        }

                        processed++;
                        this.OperationProgress = total > 0
                            ? (int)Math.Min(100, Math.Round((processed / total) * 100))
                            : 100;
                    }
                }

                bool hasExecutable = false;
                try
                {
                    hasExecutable = Directory
                        .EnumerateFiles(versionDirectory, "*.exe", SearchOption.AllDirectories)
                        .Any();
                }
                catch (Exception ex)
                {
                    this.WriteToLogFile(
                        $"Failed to inspect application payload contents: {ex.GetType().Name} - {ex.Message}"
                    );
                }

                if (!hasExecutable)
                {
                    this.WriteToLogFile(
                        "Application extraction completed but no executable files were found."
                    );
                    this.ShowError(
                        "Package Corrupt",
                        "The downloaded application payload is invalid. Please try again."
                    );
                    return false;
                }

                if (!this.ReconcileMigrationDirectory(versionDirectory))
                {
                    return false;
                }

                this.PendingVersionDirectoryPath = versionDirectory;
                this.LogActivity(
                    $"Application payload extracted to '{NormalizePath(versionDirectory)}'."
                );
                return true;
            }
            catch (InvalidDataException idex)
            {
                this.WriteToLogFile(idex.ToString());
                this.ShowError(
                    "Package Corrupt",
                    "The downloaded application payload is invalid. Please try again."
                );
            }
            catch (UnauthorizedAccessException uaex)
            {
                this.WriteToLogFile(uaex.ToString());
                this.ShowError(
                    "Package Corrupt",
                    "We couldn't write files to the installation directory. Check permissions and try again."
                );
            }
            catch (IOException ioex)
            {
                this.WriteToLogFile(ioex.ToString());
                this.ShowError(
                    "Package Corrupt",
                    "We couldn't write files to the installation directory. Check permissions and try again."
                );
            }

            return false;
        }

        private bool ReconcileMigrationDirectory(string versionDirectory)
        {
            string pendingPath = this.PendingVersionDirectoryPath;
            if (string.IsNullOrWhiteSpace(pendingPath))
            {
                return true;
            }

            if (!Directory.Exists(pendingPath))
            {
                this.LogActivity("Migration directory no longer exists; skipping merge.");
                this.PendingVersionDirectoryPath = versionDirectory;
                return true;
            }

            string normalizedPending = NormalizePath(pendingPath);
            string normalizedTarget = NormalizePath(versionDirectory);

            if (
                string.Equals(normalizedPending, normalizedTarget, StringComparison.OrdinalIgnoreCase)
            )
            {
                this.LogActivity("Migration directory already aligned with target version folder.");
                return true;
            }

            this.LogActivity(
                $"Merging migrated content from '{normalizedPending}' into '{normalizedTarget}'."
            );

            int filesMerged = 0;
            int directoriesMerged = 0;

            try
            {
                Directory.CreateDirectory(versionDirectory);

                foreach (string entry in Directory.EnumerateFileSystemEntries(pendingPath))
                {
                    string name = Path.GetFileName(entry);
                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    string destinationPath = Path.Combine(versionDirectory, name);

                    if (Directory.Exists(entry))
                    {
                        this.CopyDirectoryRecursive(
                            entry,
                            destinationPath,
                            installerPath: string.Empty,
                            ref filesMerged,
                            ref directoriesMerged,
                            skippedItems: null
                        );
                    }
                    else if (File.Exists(entry))
                    {
                        string destinationParent = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(destinationParent))
                        {
                            Directory.CreateDirectory(destinationParent);
                        }

                        File.Copy(entry, destinationPath, overwrite: true);
                        filesMerged++;
                    }
                }

                this.TryDeleteDirectory(pendingPath);

                if (!string.IsNullOrEmpty(this.LegacyDataPath))
                {
                    string normalizedLegacy = NormalizePath(this.LegacyDataPath);
                    if (
                        !string.IsNullOrEmpty(normalizedLegacy)
                        && normalizedLegacy.StartsWith(
                            normalizedPending,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        string remainder = normalizedLegacy
                            .Substring(normalizedPending.Length)
                            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        this.LegacyDataPath = string.IsNullOrEmpty(remainder)
                            ? versionDirectory
                            : Path.Combine(versionDirectory, remainder);
                    }
                }

                this.PendingVersionDirectoryPath = versionDirectory;

                this.LogActivity(
                    $"Merged {directoriesMerged} directories and {filesMerged} files from migration staging."
                );
                return true;
            }
            catch (Exception ex)
            {
                this.WriteToLogFile(ex.ToString());
                this.ShowError(
                    "Migration Failed",
                    "Unable to merge migrated files into the new version. Check permissions and retry."
                );
                return false;
            }
        }

        private async Task<bool> RunLegacyPipelineAsync()
        {
            bool result = false;
            string failureMessage = null;

            await Task.Run(async () =>
            {
                try
                {
                    MixItUpUpdateModel update = await this.GetUpdateData();

                    if (this.IsPreview)
                    {
                        MixItUpUpdateModel preview = await this.GetUpdateData(preview: true);
                        if (preview != null && preview.SystemVersion > update.SystemVersion)
                        {
                            update = preview;
                        }
                    }

                    if (this.IsTest)
                    {
                        MixItUpUpdateModel test = await this.GetUpdateData(test: true);
                        if (test != null && test.SystemVersion > update.SystemVersion)
                        {
                            update = test;
                        }
                    }

                    if (update != null)
                    {
                        if (await this.DownloadZipArchive(update))
                        {
                            if (this.InstallMixItUp() && this.CreateMixItUpShortcut())
                            {
                                result = true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (failureMessage == null)
                    {
                        failureMessage =
                            "An unexpected error occurred while running the installer.";
                    }
                    this.WriteToLogFile(ex.ToString());
                }
            });

            if (
                !string.IsNullOrEmpty(failureMessage)
                && string.IsNullOrEmpty(this.SpecificErrorMessage)
            )
            {
                this.SpecificErrorMessage = failureMessage;
            }

            if (!result && !this.ErrorOccurred)
            {
                if (!File.Exists(InstallerLogFilePath))
                {
                    this.WriteToLogFile("Installer exited without additional log details.");
                }

                Uri logUri = new Uri(InstallerLogFilePath);

                if (!string.IsNullOrEmpty(this.SpecificErrorMessage))
                {
                    this.HyperlinkAddress = logUri.AbsoluteUri;
                    this.ShowError(
                        string.Format("{0} file created:", InstallerLogFileName),
                        this.SpecificErrorMessage
                    );
                }
                else
                {
                    this.HyperlinkAddress = logUri.AbsoluteUri;
                    this.ShowError(
                        string.Format("{0} file created:", InstallerLogFileName),
                        "Please visit our support Discord or send an email to support@mixitupapp.com with the contents of this file."
                    );
                }
            }
            return result;
        }

        public void Launch()
        {
            if (Path.Equals(this.installDirectory, DefaultInstallDirectory))
            {
                if (File.Exists(StartMenuShortCutFilePath))
                {
                    ProcessStartInfo processInfo = new ProcessStartInfo(StartMenuShortCutFilePath)
                    {
                        UseShellExecute = true,
                    };
                    Process.Start(processInfo);
                }
                else if (File.Exists(DesktopShortCutFilePath))
                {
                    ProcessStartInfo processInfo = new ProcessStartInfo(DesktopShortCutFilePath)
                    {
                        UseShellExecute = true,
                    };
                    Process.Start(processInfo);
                }
            }
            else
            {
                Process.Start(Path.Combine(this.installDirectory, "MixItUp.exe"));
            }
        }

        protected bool SetProperty<T>(
            ref T storage,
            T value,
            [CallerMemberName] string propertyName = null
        )
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }

            storage = value;
            this.NotifyPropertyChanged(propertyName);
            return true;
        }

        protected void NotifyPropertyChanged([CallerMemberName] string name = "")
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private async Task<MixItUpUpdateModel> GetUpdateData(
            bool preview = false,
            bool test = false
        )
        {
            this.DisplayText1 = "Finding latest version...";
            this.IsOperationIndeterminate = true;
            this.OperationProgress = 0;

            MixItUpUpdateModel update = await this.GetUpdateDataV2(preview, test);
            if (update != null)
            {
                return update;
            }

            string url = "https://api.mixitupapp.com/api/updates";
            if (preview)
            {
                url = "https://api.mixitupapp.com/api/updates/preview";
            }
            else if (test)
            {
                url = "https://api.mixitupapp.com/api/updates/test";
            }

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = new TimeSpan(0, 0, 5);

                    HttpResponseMessage response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        string responseString = await response.Content.ReadAsStringAsync();
                        JObject jobj = JObject.Parse(responseString);
                        return jobj.ToObject<MixItUpUpdateModel>();
                    }
                    else
                    {
                        this.WriteToLogFile(
                            $"{url} - {response.StatusCode} - {await response.Content.ReadAsStringAsync()}"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                this.WriteToLogFile(ex.ToString());
            }

            return null;
        }

        private async Task<MixItUpUpdateModel> GetUpdateDataV2(
            bool preview = false,
            bool test = false
        )
        {
            this.DisplayText1 = "Finding latest version...";
            this.IsOperationIndeterminate = true;
            this.OperationProgress = 0;

            string type = "public";
            if (preview)
            {
                type = "preview";
            }
            else if (test)
            {
                type = "test";
            }

            string url =
                $"https://raw.githubusercontent.com/mixitupapp/mixitupdesktop-data/main/Updates/{type}.json";

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        client.Timeout = new TimeSpan(0, 0, 5 * (i + 1));

                        HttpResponseMessage response = await client.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            string responseString = await response.Content.ReadAsStringAsync();
                            JObject jobj = JObject.Parse(responseString);
                            MixItUpUpdateV2Model update = jobj.ToObject<MixItUpUpdateV2Model>();
                            if (update != null)
                            {
                                return new MixItUpUpdateModel(update);
                            }
                        }
                        else
                        {
                            this.WriteToLogFile(
                                $"{url} - {response.StatusCode} - {await response.Content.ReadAsStringAsync()}"
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.WriteToLogFile(ex.ToString());
                }
            }

            return null;
        }

        private async Task<bool> DownloadZipArchive(MixItUpUpdateModel update)
        {
            this.DisplayText1 = "Downloading files...";
            this.IsOperationIndeterminate = false;
            this.OperationProgress = 0;
            this.DownloadPercent = 0;

            bool downloadComplete = false;

            WebClient client = new WebClient();
            client.DownloadProgressChanged += (s, e) =>
            {
                this.OperationProgress = e.ProgressPercentage;
                this.DownloadPercent = e.ProgressPercentage;
            };

            client.DownloadDataCompleted += (s, e) =>
            {
                downloadComplete = true;
                if (e.Error == null && !e.Cancelled)
                {
                    ZipArchiveData = e.Result;
                    this.OperationProgress = 100;
                    this.DownloadPercent = 100;
                }
                else if (e.Error != null)
                {
                    this.WriteToLogFile(e.Error.ToString());
                    this.DownloadPercent = 0;
                }
            };

            client.DownloadDataAsync(new Uri(update.ZipArchiveLink));

            while (!downloadComplete)
            {
                await Task.Delay(1000);
            }

            client.Dispose();

            return (ZipArchiveData != null && ZipArchiveData.Length > 0);
        }

        private bool InstallMixItUp()
        {
            this.DisplayText1 = "Installing files...";
            this.IsOperationIndeterminate = false;
            this.OperationProgress = 0;

            try
            {
                if (ZipArchiveData != null && ZipArchiveData.Length > 0)
                {
                    Directory.CreateDirectory(this.installDirectory);
                    if (Directory.Exists(this.installDirectory))
                    {
                        using (MemoryStream zipStream = new MemoryStream(ZipArchiveData))
                        {
                            ZipArchive archive = new ZipArchive(zipStream);
                            double current = 0;
                            double total = archive.Entries.Count;
                            foreach (ZipArchiveEntry entry in archive.Entries)
                            {
                                var fullName = entry.FullName;
                                if (entry.FullName.StartsWith("Mix It Up/"))
                                {
                                    fullName = entry.FullName.Substring("Mix It Up/".Length);
                                }

                                string filePath = Path.Combine(this.installDirectory, fullName);
                                string directoryPath = Path.GetDirectoryName(filePath);
                                if (!Directory.Exists(directoryPath))
                                {
                                    Directory.CreateDirectory(directoryPath);
                                }

                                if (Path.HasExtension(filePath))
                                {
                                    entry.ExtractToFile(filePath, overwrite: true);
                                }

                                current++;
                                this.OperationProgress = (int)((current / total) * 100);
                            }
                            return true;
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException uaex)
            {
                this.SpecificErrorMessage =
                    "We were unable to update due to a file lock issue. Please try rebooting your PC and then running the update. You can also download and re-run our installer to update your installation.";
                this.WriteToLogFile(uaex.ToString());
            }
            catch (IOException ioex)
            {
                this.SpecificErrorMessage =
                    "We were unable to update due to a file lock issue. Please try rebooting your PC and then running the update. You can also download and re-run our installer to update your installation.";
                this.WriteToLogFile(ioex.ToString());
            }
            catch (WebException wex)
            {
                this.SpecificErrorMessage =
                    "We were unable to update due to a network issue, please try again later. If this issue persists, please try restarting your PC and/or router or flush the DNS cache on your computer.";
                this.WriteToLogFile(wex.ToString());
            }
            catch (Exception ex)
            {
                this.WriteToLogFile(ex.ToString());
            }
            return false;
        }

        private bool CreateMixItUpShortcut()
        {
            try
            {
                this.DisplayText1 = "Creating Start Menu & Desktop shortcuts...";
                this.IsOperationIndeterminate = true;
                this.OperationProgress = 0;

                if (!Directory.Exists(StartMenuDirectory))
                {
                    Directory.CreateDirectory(StartMenuDirectory);
                }

                if (Directory.Exists(StartMenuDirectory))
                {
                    string tempLinkFilePath = Path.Combine(
                        DefaultInstallDirectory,
                        "Mix It Up.link"
                    );
                    if (File.Exists(tempLinkFilePath))
                    {
                        File.Copy(tempLinkFilePath, StartMenuShortCutFilePath, overwrite: true);
                        if (File.Exists(StartMenuShortCutFilePath))
                        {
                            return true;
                        }
                        else
                        {
                            File.Copy(tempLinkFilePath, DesktopShortCutFilePath, overwrite: true);
                            if (File.Exists(DesktopShortCutFilePath))
                            {
                                this.ShowError(
                                    "We were unable to create the Start Menu shortcut.",
                                    "You can instead use the Desktop shortcut to launch Mix It Up"
                                );
                            }
                            else
                            {
                                this.ShowError(
                                    "We were unable to create the Start Menu & Desktop shortcuts.",
                                    "Email support@mixitupapp.com to help diagnose this issue further."
                                );
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.WriteToLogFile(ex.ToString());
            }
            return false;
        }

        private void ShowError(string message1, string message2)
        {
            this.IsOperationBeingPerformed = false;
            this.ErrorOccurred = true;
            this.DisplayText1 = message1;
            this.DisplayText2 = message2;

            string combinedMessage = message1 ?? string.Empty;
            if (!string.IsNullOrEmpty(message2))
            {
                combinedMessage = string.IsNullOrEmpty(combinedMessage)
                    ? message2
                    : string.Format("{0} {1}", combinedMessage, message2);
            }

            this.ErrorMessage = combinedMessage;
            this.HasError = true;
        }

        private void LogActivity(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            string timestampedMessage = string.Format(
                "[{0:HH:mm:ss}] {1}",
                DateTime.Now,
                message.Trim()
            );

            this.WriteToLogFile(message.Trim());

            try
            {
                if (Application.Current == null || Application.Current.Dispatcher == null)
                {
                    this.ActivityLog.Add(timestampedMessage);
                }
                else if (Application.Current.Dispatcher.CheckAccess())
                {
                    this.ActivityLog.Add(timestampedMessage);
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        this.ActivityLog.Add(timestampedMessage)
                    );
                }
            }
            catch
            {
                // As a fallback, swallow logging issues to keep installer moving.
            }
        }

        private void ResetLogFile()
        {
            try
            {
                if (File.Exists(InstallerLogFilePath))
                {
                    File.Delete(InstallerLogFilePath);
                }
            }
            catch
            {
                // If we cannot delete the previous log, we'll append to it instead.
            }
        }

        private void WriteToLogFile(string text)
        {
            try
            {
                File.AppendAllText(
                    InstallerLogFilePath,
                    string.Format("[{0:u}] {1}{2}{2}", DateTime.UtcNow, text, Environment.NewLine)
                );
            }
            catch
            {
                // Swallow logging errors to avoid masking the original issue.
            }
        }
    }
}
