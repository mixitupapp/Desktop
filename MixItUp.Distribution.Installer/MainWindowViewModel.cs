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
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using MixItUp.Base.Model.API;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Principal;

namespace MixItUp.Distribution.Installer
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public const string InstallerLogFileName = "installer.log";
        public const string ShortcutFileName = "Mix It Up.lnk";

        public const string OldApplicationSettingsFileName = "ApplicationSettings.xml";
        public const string NewApplicationSettingsFileName = "ApplicationSettings.json";

        public const string MixItUpProcessName = "MixItUp";
        public const string AutoHosterProcessName = "MixItUp.AutoHoster";

        private static readonly IReadOnlyList<string> TargetProcessNames = new[]
        {
            MixItUpProcessName,
            AutoHosterProcessName,
        };

        private const string LauncherProductSlug = "mixitup-desktop";
        private const string LauncherPlatform = "windows-x64";
        private const string AppProductSlug = "mixitup-desktop";
        private const string AppPlatform = "windows-x64";

        private static readonly IReadOnlyList<string> AllowListedDataDirectories = new[]
        {
        "Settings",
        "Logs",
        "ChatEventLogs",
        "Counters",
    };

        private static readonly IReadOnlyList<string> AllowListedDataFiles = new[]
        {
        NewApplicationSettingsFileName,
        OldApplicationSettingsFileName,
    };

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

        private sealed class BootloaderConfigModel
        {
            [JsonProperty("currentVersion")]
            public string CurrentVersion { get; set; }

            [JsonProperty("versionRoot")]
            public string VersionRoot { get; set; }

            [JsonProperty("versions")]
            public List<string> Versions { get; set; } = new List<string>();

            [JsonProperty("executables")]
            public Dictionary<string, string> Executables { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            [JsonProperty("dataDirName")]
            public string DataDirName { get; set; }

            [JsonExtensionData]
            public IDictionary<string, JToken> ExtensionData { get; set; }
                = new Dictionary<string, JToken>(StringComparer.OrdinalIgnoreCase);
        }

        private enum ShortcutCreationResult
        {
            Success,
            AlreadyExists,
            AccessDenied,
            Failed,
        }

        private sealed class RelayCommand : ICommand
        {
            private readonly Action<object> execute;
            private readonly Func<object, bool> canExecute;

            public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
            {
                this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
                this.canExecute = canExecute;
            }

            public event EventHandler CanExecuteChanged;

            public bool CanExecute(object parameter)
            {
                return this.canExecute == null || this.canExecute(parameter);
            }

            public void Execute(object parameter)
            {
                this.execute(parameter);
            }

            public void RaiseCanExecuteChanged()
            {
                this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private enum InstallerStep
        {
            Preflight,
            Discover,
            CloseProcesses,
            Migrate,
            LauncherFetch,
            LauncherInstall,
            AppFetch,
            AppExtract,
            DataCopy,
            ConfigWrite,
            Register,
            Shortcuts,
            Complete,
        }

        private enum StepStatus
        {
            Pending,
            InProgress,
            Completed,
            Failed,
        }

        private static readonly IReadOnlyDictionary<InstallerStep, string> StepDisplayNames =
            new Dictionary<InstallerStep, string>
            {
                { InstallerStep.Preflight, "Preflight" },
                { InstallerStep.Discover, "Discover" },
                { InstallerStep.CloseProcesses, "CloseProcesses" },
                { InstallerStep.Migrate, "Migrate" },
                { InstallerStep.LauncherFetch, "LauncherFetch" },
                { InstallerStep.LauncherInstall, "LauncherInstall" },
                { InstallerStep.AppFetch, "AppFetch" },
                { InstallerStep.AppExtract, "AppExtract" },
                { InstallerStep.DataCopy, "DataCopy" },
                { InstallerStep.ConfigWrite, "ConfigWrite" },
                { InstallerStep.Register, "Register" },
                { InstallerStep.Shortcuts, "Shortcuts" },
                { InstallerStep.Complete, "Complete" },
            };

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

        private string InstallerLogFilePath
        {
            get { return this.GetInstallerLogFilePath(); }
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

        private RelayCommand launchCommand;
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

            this.launchCommand = new RelayCommand(
                _ => this.ExecuteLaunch(),
                _ => this.StepCompleteDone && !this.HasError
            );
            this.LaunchCommand = this.launchCommand;
        }

        private void SetStepState(InstallerStep step, StepStatus status, bool logTransition = true)
        {
            bool pending = status == StepStatus.Pending;
            bool inProgress = status == StepStatus.InProgress;
            bool done = status == StepStatus.Completed;

            switch (step)
            {
                case InstallerStep.Preflight:
                    this.StepPreflightPending = pending;
                    this.StepPreflightInProgress = inProgress;
                    this.StepPreflightDone = done;
                    break;
                case InstallerStep.Discover:
                    this.StepDiscoverPending = pending;
                    this.StepDiscoverInProgress = inProgress;
                    this.StepDiscoverDone = done;
                    break;
                case InstallerStep.CloseProcesses:
                    this.StepCloseProcessesPending = pending;
                    this.StepCloseProcessesInProgress = inProgress;
                    this.StepCloseProcessesDone = done;
                    break;
                case InstallerStep.Migrate:
                    this.StepMigratePending = pending;
                    this.StepMigrateInProgress = inProgress;
                    this.StepMigrateDone = done;
                    break;
                case InstallerStep.LauncherFetch:
                    this.StepLauncherFetchPending = pending;
                    this.StepLauncherFetchInProgress = inProgress;
                    this.StepLauncherFetchDone = done;
                    break;
                case InstallerStep.LauncherInstall:
                    this.StepLauncherInstallPending = pending;
                    this.StepLauncherInstallInProgress = inProgress;
                    this.StepLauncherInstallDone = done;
                    break;
                case InstallerStep.AppFetch:
                    this.StepAppFetchPending = pending;
                    this.StepAppFetchInProgress = inProgress;
                    this.StepAppFetchDone = done;
                    break;
                case InstallerStep.AppExtract:
                    this.StepAppExtractPending = pending;
                    this.StepAppExtractInProgress = inProgress;
                    this.StepAppExtractDone = done;
                    break;
                case InstallerStep.DataCopy:
                    this.StepDataCopyPending = pending;
                    this.StepDataCopyInProgress = inProgress;
                    this.StepDataCopyDone = done;
                    break;
                case InstallerStep.ConfigWrite:
                    this.StepConfigWritePending = pending;
                    this.StepConfigWriteInProgress = inProgress;
                    this.StepConfigWriteDone = done;
                    break;
                case InstallerStep.Register:
                    this.StepRegisterPending = pending;
                    this.StepRegisterInProgress = inProgress;
                    this.StepRegisterDone = done;
                    break;
                case InstallerStep.Shortcuts:
                    this.StepShortcutsPending = pending;
                    this.StepShortcutsInProgress = inProgress;
                    this.StepShortcutsDone = done;
                    break;
                case InstallerStep.Complete:
                    this.StepCompletePending = pending;
                    this.StepCompleteInProgress = inProgress;
                    this.StepCompleteDone = done;
                    break;
            }

            if (!logTransition)
            {
                return;
            }

            if (status == StepStatus.Pending)
            {
                return;
            }

            this.LogStepTransition(step, status);
        }

        private void LogStepTransition(InstallerStep step, StepStatus status)
        {
            string stepName = StepDisplayNames.TryGetValue(step, out string value)
                ? value
                : step.ToString();

            string statusText;
            switch (status)
            {
                case StepStatus.InProgress:
                    statusText = "started";
                    break;
                case StepStatus.Completed:
                    statusText = "completed";
                    break;
                case StepStatus.Failed:
                    statusText = "failed";
                    break;
                default:
                    statusText = "updated";
                    break;
            }

            string message = string.Format("Step '{0}' {1}.", stepName, statusText);
            string level = status == StepStatus.Failed ? "ERROR" : "INFO";
            this.LogActivity(message, level);
        }

        private void ResetStepStates()
        {
            foreach (InstallerStep step in Enum.GetValues(typeof(InstallerStep)))
            {
                this.SetStepState(step, StepStatus.Pending, logTransition: false);
            }
        }

        private void UpdateEnvironmentState()
        {
            OperatingSystem operatingSystem = Environment.OSVersion;
            this.OSVersionDisplay = operatingSystem.VersionString;
            this.IsSupportedOS = EnvironmentRequirements.IsWindows10Or11(operatingSystem);
            this.Is64BitOS = EnvironmentRequirements.Is64BitOS(
                Environment.Is64BitOperatingSystem,
                Environment.Is64BitProcess
            );
        }

        private bool Preflight()
        {
            this.DisplayText1 = "Validating system requirements...";
            this.DisplayText2 = string.Empty;
            this.IsOperationIndeterminate = true;
            this.IsOperationBeingPerformed = true;
            this.LogActivity("Starting preflight checks...");

            this.SetStepState(InstallerStep.Preflight, StepStatus.InProgress);

            OperatingSystem operatingSystem = Environment.OSVersion;
            this.OSVersionDisplay = operatingSystem.VersionString;
            this.LogActivity($"Detected operating system: {this.OSVersionDisplay}");

            this.IsSupportedOS = EnvironmentRequirements.IsWindows10Or11(operatingSystem);
            if (!this.IsSupportedOS)
            {
                this.LogActivity("Unsupported Windows version detected.");
                this.SetStepState(InstallerStep.Preflight, StepStatus.Failed);
                this.HasError = true;
                this.ShowError(
                    "Unsupported Windows Version",
                    "MixItUp requires Windows 10 or 11 (64-bit)."
                );
                return false;
            }
            this.LogActivity("Windows version is supported.");

            this.Is64BitOS = EnvironmentRequirements.Is64BitOS(
                Environment.Is64BitOperatingSystem,
                Environment.Is64BitProcess
            );
            if (!this.Is64BitOS)
            {
                this.LogActivity("Unsupported architecture detected (not 64-bit).");
                this.SetStepState(InstallerStep.Preflight, StepStatus.Failed);
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
                this.SetStepState(InstallerStep.Preflight, StepStatus.Failed);
                this.HasError = true;
                this.ShowError(
                    "Write Permission Denied",
                    "Installer needs write access to %LOCALAPPDATA%/MixItUp, the Start Menu, or Desktop. Run with sufficient permissions."
                );
                return false;
            }

            this.LogActivity("Write permissions validated for required locations.");

            this.SetStepState(InstallerStep.Preflight, StepStatus.Completed);
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

        internal Task<bool> DiscoverInstallContextAsync()
        {
            return Task.FromResult(this.DiscoverInstallContext());
        }

        private bool DiscoverInstallContext()
        {
            this.DisplayText1 = "Discovering install context...";
            this.DisplayText2 = string.Empty;
            this.IsOperationIndeterminate = true;
            this.IsOperationBeingPerformed = true;

            this.SetStepState(InstallerStep.Discover, StepStatus.InProgress);

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
                this.SetStepState(InstallerStep.Discover, StepStatus.Failed);

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

            this.SetStepState(InstallerStep.Discover, StepStatus.Completed);
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

            this.SetStepState(InstallerStep.Migrate, StepStatus.InProgress);

            if (this.MigrationAlreadyDone)
            {
                this.LogActivity("Migration step skipped: already completed previously.");
                this.PendingVersionDirectoryPath = string.Empty;
                this.SetStepState(InstallerStep.Migrate, StepStatus.Completed);
                return true;
            }

            if (!this.LegacyDetected && !this.PortableCandidateFound)
            {
                this.LogActivity("Migration step skipped: no legacy or portable install detected.");
                this.PendingVersionDirectoryPath = string.Empty;
                this.SetStepState(InstallerStep.Migrate, StepStatus.Completed);
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
                this.SetStepState(InstallerStep.Migrate, StepStatus.Failed);
                this.ShowError(
                    "Migration Failed",
                    "Unable to prepare version directory. Check permissions and retry."
                );
                this.SetHyperlinkToLogFile();
                return false;
            }

            bool isLegacySource = this.LegacyDetected;
            string sourceDirectory = isLegacySource ? this.AppRoot : this.RunningDirectory;
            if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
            {
                this.LogActivity("Migration aborted: source directory not found.");
                this.SetStepState(InstallerStep.Migrate, StepStatus.Failed);
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
                this.SetStepState(InstallerStep.Migrate, StepStatus.Failed);
                this.ShowError(
                    "Migration Failed",
                    "Unable to create migration workspace under AppRoot."
                );
                this.SetHyperlinkToLogFile();
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
                this.SetStepState(InstallerStep.Migrate, StepStatus.Failed);
                this.ShowError(
                    "Migration Failed",
                    "Unable to migrate existing files. Check permissions and retry."
                );
                this.SetHyperlinkToLogFile();
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

            this.SetStepState(InstallerStep.Migrate, StepStatus.Completed);
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
            return EnvironmentRequirements.IsWindows10Or11(Environment.OSVersion);
        }

        private async Task<bool> WaitForProcessesToExitAsync()
        {
            this.DisplayText1 = "Closing Mix It Up and companion apps...";
            this.DisplayText2 = string.Empty;
            this.IsOperationIndeterminate = true;
            this.IsOperationBeingPerformed = true;

            this.SetStepState(InstallerStep.CloseProcesses, StepStatus.InProgress);

            this.LogActivity("Inspecting running Mix It Up processes...");

            List<Process> initialProcesses = this.GetTargetProcesses();
            if (initialProcesses.Count == 0)
            {
                this.LogActivity("No running Mix It Up processes detected.");
                this.DisplayText1 = "No Mix It Up processes detected.";
                this.SetStepState(InstallerStep.CloseProcesses, StepStatus.Completed);
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
                    this.SetStepState(InstallerStep.CloseProcesses, StepStatus.Completed);
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

                this.HasError = true;
                this.ShowError(
                    "Close MixItUp",
                    "Please close MixItUp or AutoHoster before continuing."
                );
                this.SetStepState(InstallerStep.CloseProcesses, StepStatus.Failed);
                return false;
            }

            this.SetStepState(InstallerStep.CloseProcesses, StepStatus.Completed);
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

        private static string GetRelativeDisplayPath(string rootPath, string itemPath)
        {
            string normalizedRoot = NormalizePath(rootPath);
            string normalizedItem = NormalizePath(itemPath);

            if (
                string.IsNullOrEmpty(normalizedRoot)
                || string.IsNullOrEmpty(normalizedItem)
                || normalizedItem.Length <= normalizedRoot.Length
            )
            {
                return Path.GetFileName(normalizedItem);
            }

            string comparisonSeed = normalizedRoot.EndsWith(
                Path.DirectorySeparatorChar.ToString(),
                StringComparison.Ordinal
            )
                ? normalizedRoot
                : normalizedRoot + Path.DirectorySeparatorChar;

            if (
                normalizedItem.StartsWith(
                    comparisonSeed,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return normalizedItem.Substring(comparisonSeed.Length);
            }

            return Path.GetFileName(normalizedItem);
        }

        private static bool PathsEqual(string left, string right)
        {
            string normalizedLeft = NormalizePath(left);
            string normalizedRight = NormalizePath(right);

            if (string.IsNullOrEmpty(normalizedLeft) || string.IsNullOrEmpty(normalizedRight))
            {
                return false;
            }

            return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsProcessElevated()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                if (identity == null)
                {
                    return false;
                }

                using (identity)
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        internal Task<bool> CopyUserDataAsync()
        {
            return Task.FromResult(this.CopyUserData());
        }

        private bool CopyUserData()
        {
            this.DisplayText1 = "Copying user data...";
            this.DisplayText2 = string.Empty;
            this.IsOperationIndeterminate = true;
            this.IsOperationBeingPerformed = true;

            this.SetStepState(InstallerStep.DataCopy, StepStatus.InProgress);

            string versionDirectory = this.PendingVersionDirectoryPath;
            if (string.IsNullOrWhiteSpace(versionDirectory) || !Directory.Exists(versionDirectory))
            {
                if (!string.IsNullOrWhiteSpace(this.VersionedAppDirRoot) && !string.IsNullOrWhiteSpace(this.LatestVersion))
                {
                    string candidate = Path.Combine(this.VersionedAppDirRoot, this.LatestVersion);
                    if (Directory.Exists(candidate))
                    {
                        versionDirectory = candidate;
                        this.PendingVersionDirectoryPath = candidate;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(versionDirectory) || !Directory.Exists(versionDirectory))
            {
                this.LogActivity("Unable to locate extracted application directory for data copy.");
                this.SetStepState(InstallerStep.DataCopy, StepStatus.Failed);
                this.ShowError(
                    "Data Copy Failed",
                    "We couldn't locate the application data directory. Please check installation paths."
                );
                return false;
            }

            string targetDataDirectory = Path.Combine(versionDirectory, "data");
            try
            {
                Directory.CreateDirectory(targetDataDirectory);
            }
            catch (Exception ex)
            {
                this.LogActivity(
                    $"Failed to ensure data directory exists: {ex.GetType().Name} - {ex.Message}"
                );
                this.WriteToLogFile(ex.ToString());
                this.SetStepState(InstallerStep.DataCopy, StepStatus.Failed);
                this.ShowError(
                    "Data Copy Failed",
                    "Unable to prepare application data directory. Check permissions and try again."
                );
                return false;
            }

            List<string> skippedItems = new List<string>();
            int totalFilesCopied = 0;

            try
            {
                bool handledLegacy =
                    !string.IsNullOrWhiteSpace(this.LegacyDataPath)
                    && Directory.Exists(this.LegacyDataPath)
                    && (this.LegacyDetected || this.PortableCandidateFound);

                if (handledLegacy)
                {
                    string normalizedSource = NormalizePath(this.LegacyDataPath);
                    string normalizedDestination = NormalizePath(targetDataDirectory);

                    this.LogActivity(
                        $"Copying legacy data from '{normalizedSource}' to '{normalizedDestination}'."
                    );

                    totalFilesCopied += this.CopyDataDirectoryWithoutOverwrite(
                        this.LegacyDataPath,
                        targetDataDirectory,
                        skippedItems,
                        targetDataDirectory
                    );
                }
                else
                {
                    BootloaderConfigModel existingConfig = this.LoadBootloaderConfig();
                    string previousVersionDirectory = this.ResolvePreviousVersionDirectory(
                        existingConfig,
                        versionDirectory
                    );

                    if (!string.IsNullOrEmpty(previousVersionDirectory))
                    {
                        string previousDataDirectory = Path.Combine(previousVersionDirectory, "data");
                        if (Directory.Exists(previousDataDirectory))
                        {
                            string normalizedSource = NormalizePath(previousDataDirectory);
                            string normalizedDestination = NormalizePath(targetDataDirectory);
                            this.LogActivity(
                                $"Copying allow-listed data from '{normalizedSource}' to '{normalizedDestination}'."
                            );

                            foreach (string directoryName in AllowListedDataDirectories)
                            {
                                string sourceSubDir = Path.Combine(previousDataDirectory, directoryName);
                                if (!Directory.Exists(sourceSubDir))
                                {
                                    continue;
                                }

                                totalFilesCopied += this.CopyDataDirectoryWithoutOverwrite(
                                    sourceSubDir,
                                    Path.Combine(targetDataDirectory, directoryName),
                                    skippedItems,
                                    targetDataDirectory
                                );
                            }

                            foreach (string fileName in AllowListedDataFiles)
                            {
                                string sourceFilePath = Path.Combine(previousDataDirectory, fileName);
                                if (!File.Exists(sourceFilePath))
                                {
                                    continue;
                                }

                                string destinationFilePath = Path.Combine(
                                    targetDataDirectory,
                                    fileName
                                );

                                if (File.Exists(destinationFilePath))
                                {
                                    string skippedLabel = GetRelativeDisplayPath(
                                        targetDataDirectory,
                                        destinationFilePath
                                    );
                                    skippedItems.Add(skippedLabel);
                                    this.LogActivity(
                                        $"Skipped copy for '{NormalizePath(sourceFilePath)}' because destination exists."
                                    );
                                    continue;
                                }

                                string parentDirectory = Path.GetDirectoryName(destinationFilePath);
                                if (!string.IsNullOrEmpty(parentDirectory))
                                {
                                    Directory.CreateDirectory(parentDirectory);
                                }

                                File.Copy(sourceFilePath, destinationFilePath, overwrite: false);
                                totalFilesCopied++;
                                this.LogActivity(
                                    $"Copied '{NormalizePath(sourceFilePath)}' to '{NormalizePath(destinationFilePath)}'."
                                );
                            }
                        }
                        else
                        {
                            this.LogActivity(
                                $"No data directory found in previous version path '{NormalizePath(previousVersionDirectory)}'."
                            );
                        }
                    }
                    else
                    {
                        this.LogActivity(
                            "No previous version directory found for upgrade data copy."
                        );
                    }
                }

                this.SetStepState(InstallerStep.DataCopy, StepStatus.Completed);
                this.DisplayText1 = "User data copied.";
                this.DisplayText2 = string.Empty;

                if (totalFilesCopied > 0)
                {
                    this.LogActivity(
                        $"Copied {totalFilesCopied} data file(s) into '{NormalizePath(targetDataDirectory)}'."
                    );
                }
                else
                {
                    this.LogActivity("No user data changes were needed.");
                }

                if (skippedItems.Count > 0)
                {
                    string preview = string.Join(", ", skippedItems.Take(5));
                    if (skippedItems.Count > 5)
                    {
                        preview += ", ...";
                    }

                    this.LogActivity(
                        $"Skipped {skippedItems.Count} item(s) that already existed: {preview}."
                    );
                }

                return true;
            }
            catch (Exception ex)
            {
                this.LogActivity(
                    $"Data copy failed: {ex.GetType().Name} - {ex.Message}"
                );
                this.WriteToLogFile(ex.ToString());
                this.SetStepState(InstallerStep.DataCopy, StepStatus.Failed);
                this.ShowError(
                    "Data Copy Failed",
                    "Unable to copy user data into the new version directory."
                );
                return false;
            }
        }

        private int CopyDataDirectoryWithoutOverwrite(
            string sourceDir,
            string destinationDir,
            List<string> skippedItems,
            string destinationRoot
        )
        {
            if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
            {
                return 0;
            }

            Directory.CreateDirectory(destinationDir);

            int filesCopied = 0;

            foreach (string filePath in Directory.GetFiles(sourceDir))
            {
                string destinationFilePath = Path.Combine(
                    destinationDir,
                    Path.GetFileName(filePath)
                );

                if (File.Exists(destinationFilePath))
                {
                    string skippedLabel = GetRelativeDisplayPath(destinationRoot, destinationFilePath);
                    if (!string.IsNullOrEmpty(skippedLabel))
                    {
                        skippedItems.Add(skippedLabel);
                    }

                    this.LogActivity(
                        $"Skipped copy for '{NormalizePath(filePath)}' because destination exists."
                    );
                    continue;
                }

                string parentDirectory = Path.GetDirectoryName(destinationFilePath);
                if (!string.IsNullOrEmpty(parentDirectory))
                {
                    Directory.CreateDirectory(parentDirectory);
                }

                File.Copy(filePath, destinationFilePath, overwrite: false);
                filesCopied++;
                this.LogActivity(
                    $"Copied '{NormalizePath(filePath)}' to '{NormalizePath(destinationFilePath)}'."
                );
            }

            foreach (string dirPath in Directory.GetDirectories(sourceDir))
            {
                string destinationSubDirectory = Path.Combine(
                    destinationDir,
                    Path.GetFileName(dirPath)
                );

                filesCopied += this.CopyDataDirectoryWithoutOverwrite(
                    dirPath,
                    destinationSubDirectory,
                    skippedItems,
                    destinationRoot
                );
            }

            return filesCopied;
        }

        private BootloaderConfigModel LoadBootloaderConfig()
        {
            string bootloaderPath = this.BootloaderConfigPath;
            if (string.IsNullOrWhiteSpace(bootloaderPath) || !File.Exists(bootloaderPath))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(bootloaderPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<BootloaderConfigModel>(json);
            }
            catch (Exception ex)
            {
                this.LogActivity(
                    $"Failed to read existing bootloader configuration: {ex.GetType().Name} - {ex.Message}"
                );
                this.WriteToLogFile(ex.ToString());
                return null;
            }
        }

        private string ResolvePreviousVersionDirectory(
            BootloaderConfigModel existingConfig,
            string latestVersionDirectory
        )
        {
            string versionRoot = this.VersionedAppDirRoot;
            if (string.IsNullOrWhiteSpace(versionRoot) || !Directory.Exists(versionRoot))
            {
                return null;
            }

            string normalizedLatest = NormalizePath(latestVersionDirectory);

            Func<string, string> resolveCandidate = versionName =>
            {
                if (string.IsNullOrWhiteSpace(versionName))
                {
                    return null;
                }

                string candidatePath = Path.Combine(versionRoot, versionName);
                return Directory.Exists(candidatePath) ? NormalizePath(candidatePath) : null;
            };

            if (existingConfig != null)
            {
                string candidate = resolveCandidate(existingConfig.CurrentVersion);
                if (!string.IsNullOrEmpty(candidate) && !PathsEqual(candidate, normalizedLatest))
                {
                    return candidate;
                }

                if (existingConfig.Versions != null)
                {
                    foreach (string versionName in existingConfig.Versions)
                    {
                        if (string.IsNullOrWhiteSpace(versionName))
                        {
                            continue;
                        }

                        if (
                            !string.IsNullOrEmpty(this.LatestVersion)
                            && string.Equals(
                                versionName,
                                this.LatestVersion,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        {
                            continue;
                        }

                        candidate = resolveCandidate(versionName);
                        if (!string.IsNullOrEmpty(candidate) && !PathsEqual(candidate, normalizedLatest))
                        {
                            return candidate;
                        }
                    }
                }
            }

            try
            {
                DirectoryInfo rootInfo = new DirectoryInfo(versionRoot);
                if (!rootInfo.Exists)
                {
                    return null;
                }

                DirectoryInfo[] directories = rootInfo.GetDirectories();
                Array.Sort(
                    directories,
                    (left, right) => DateTime.Compare(right.LastWriteTimeUtc, left.LastWriteTimeUtc)
                );

                foreach (DirectoryInfo directory in directories)
                {
                    string candidate = NormalizePath(directory.FullName);
                    if (PathsEqual(candidate, normalizedLatest))
                    {
                        continue;
                    }

                    return candidate;
                }
            }
            catch (Exception ex)
            {
                this.WriteToLogFile(
                    $"Failed to enumerate version directories: {ex.GetType().Name} - {ex.Message}"
                );
            }

            return null;
        }

        internal Task<bool> WriteOrUpdateBootloaderConfigAsync()
        {
            return Task.FromResult(this.WriteOrUpdateBootloaderConfig());
        }

        private bool WriteOrUpdateBootloaderConfig()
        {
            this.DisplayText1 = "Writing bootloader configuration...";
            this.DisplayText2 = string.Empty;
            this.IsOperationIndeterminate = true;
            this.IsOperationBeingPerformed = true;

            this.SetStepState(InstallerStep.ConfigWrite, StepStatus.InProgress);

            string bootloaderPath = this.BootloaderConfigPath;
            if (string.IsNullOrWhiteSpace(bootloaderPath))
            {
                this.LogActivity("Bootloader path not defined; cannot write configuration.");
                this.SetStepState(InstallerStep.ConfigWrite, StepStatus.Failed);
                this.ShowError(
                    "Configuration Failed",
                    "Installer could not determine the bootloader file path."
                );
                return false;
            }

            string latestVersion = this.LatestVersion;
            if (string.IsNullOrWhiteSpace(latestVersion) && !string.IsNullOrWhiteSpace(this.PendingVersionDirectoryPath))
            {
                string candidate = Path.GetFileName(NormalizePath(this.PendingVersionDirectoryPath));
                if (!string.IsNullOrEmpty(candidate))
                {
                    latestVersion = candidate;
                    this.LatestVersion = candidate;
                }
            }

            if (string.IsNullOrWhiteSpace(latestVersion))
            {
                this.LogActivity("Latest version identifier not available; cannot update bootloader configuration.");
                this.SetStepState(InstallerStep.ConfigWrite, StepStatus.Failed);
                this.ShowError(
                    "Configuration Failed",
                    "Installer could not determine the version being installed."
                );
                return false;
            }

            string bootloaderDirectory = Path.GetDirectoryName(bootloaderPath);
            try
            {
                if (!string.IsNullOrEmpty(bootloaderDirectory))
                {
                    Directory.CreateDirectory(bootloaderDirectory);
                }
            }
            catch (Exception ex)
            {
                this.LogActivity(
                    $"Failed to prepare bootloader directory: {ex.GetType().Name} - {ex.Message}"
                );
                this.WriteToLogFile(ex.ToString());
                this.SetStepState(InstallerStep.ConfigWrite, StepStatus.Failed);
                this.ShowError(
                    "Configuration Failed",
                    "Installer was unable to prepare the bootloader directory."
                );
                return false;
            }

            try
            {
                bool existingFile = File.Exists(bootloaderPath);
                BootloaderConfigModel config = this.LoadBootloaderConfig() ?? new BootloaderConfigModel();

                if (config.Executables == null)
                {
                    config.Executables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                if (config.Versions == null)
                {
                    config.Versions = new List<string>();
                }

                if (config.ExtensionData == null)
                {
                    config.ExtensionData = new Dictionary<string, JToken>(StringComparer.OrdinalIgnoreCase);
                }

                config.CurrentVersion = latestVersion;
                config.VersionRoot = "app";
                config.DataDirName = "data";
                config.Executables["windows"] = "MixItUp.exe";

                HashSet<string> seenVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                List<string> mergedVersions = new List<string>();

                Action<string> addVersion = versionName =>
                {
                    if (string.IsNullOrWhiteSpace(versionName))
                    {
                        return;
                    }

                    if (seenVersions.Add(versionName))
                    {
                        mergedVersions.Add(versionName);
                    }
                };

                foreach (string versionName in config.Versions)
                {
                    addVersion(versionName);
                }

                if (!string.IsNullOrWhiteSpace(this.InstalledVersion))
                {
                    addVersion(this.InstalledVersion);
                }

                string versionRoot = this.VersionedAppDirRoot;
                if (!string.IsNullOrWhiteSpace(versionRoot) && Directory.Exists(versionRoot))
                {
                    DirectoryInfo[] directories = new DirectoryInfo(versionRoot).GetDirectories();
                    Array.Sort(
                        directories,
                        (left, right) => string.Compare(
                            left.Name,
                            right.Name,
                            StringComparison.OrdinalIgnoreCase
                        )
                    );

                    foreach (DirectoryInfo directory in directories)
                    {
                        addVersion(directory.Name);
                    }
                }

                addVersion(latestVersion);

                config.Versions = mergedVersions;

                string serialized = JsonConvert.SerializeObject(
                    config,
                    Formatting.Indented,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                    }
                );

                File.WriteAllText(bootloaderPath, serialized);

                this.SetStepState(InstallerStep.ConfigWrite, StepStatus.Completed);
                this.DisplayText1 = "Bootloader updated.";
                this.DisplayText2 = string.Empty;

                string normalizedBootloaderPath = NormalizePath(bootloaderPath);
                string versionList = string.Join(", ", config.Versions);
                this.LogActivity(
                    existingFile
                        ? $"bootloader.json updated at '{normalizedBootloaderPath}'. Versions: {versionList}."
                        : $"bootloader.json created at '{normalizedBootloaderPath}'. Versions: {versionList}."
                );
                this.LogActivity($"Current version set to {latestVersion}.");

                this.InstalledVersion = latestVersion;

                return true;
            }
            catch (Exception ex)
            {
                this.LogActivity(
                    $"Failed to write bootloader configuration: {ex.GetType().Name} - {ex.Message}"
                );
                this.WriteToLogFile(ex.ToString());
                this.SetStepState(InstallerStep.ConfigWrite, StepStatus.Failed);
                this.ShowError(
                    "Configuration Failed",
                    "Installer was unable to update bootloader.json."
                );
                return false;
            }
        }

        private bool RegisterUninstallEntry()
        {
            this.DisplayText1 = "Registering Mix It Up...";
            this.DisplayText2 = string.Empty;
            this.IsOperationIndeterminate = true;
            this.IsOperationBeingPerformed = true;

            this.SetStepState(InstallerStep.Register, StepStatus.InProgress);

            string appRoot = this.AppRoot;
            if (string.IsNullOrWhiteSpace(appRoot))
            {
                appRoot = DefaultInstallDirectory;
            }

            string normalizedAppRoot = NormalizePath(appRoot);
            string launcherPath = Path.Combine(appRoot, "MixItUp.exe");
            string normalizedLauncherPath = NormalizePath(launcherPath);
            bool launcherExists = File.Exists(launcherPath);

            string uninstallerPath = Path.Combine(appRoot, "MixItUp.Uninstaller.exe");
            string normalizedUninstallerPath = NormalizePath(uninstallerPath);
            bool uninstallerExists = File.Exists(uninstallerPath);

            string uninstallCommand = uninstallerExists
                ? $"\"{normalizedUninstallerPath}\""
                : $"\"{normalizedLauncherPath}\" --uninstall";

            string displayVersion = this.LatestVersion;
            if (string.IsNullOrWhiteSpace(displayVersion))
            {
                string pendingPath = this.PendingVersionDirectoryPath;
                if (!string.IsNullOrWhiteSpace(pendingPath))
                {
                    displayVersion = Path.GetFileName(NormalizePath(pendingPath));
                }
            }

            if (string.IsNullOrWhiteSpace(displayVersion))
            {
                displayVersion = "0.0.0";
            }

            bool isElevated = IsProcessElevated();
            RegistryView view = Environment.Is64BitOperatingSystem
                ? RegistryView.Registry64
                : RegistryView.Registry32;

            string uninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\MixItUp";

            RegistryHive? hiveUsed = null;
            Exception lastError = null;

            RegistryHive[] candidateHives = isElevated
                ? new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser }
                : new[] { RegistryHive.CurrentUser };

            foreach (RegistryHive hive in candidateHives)
            {
                try
                {
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                    using (RegistryKey uninstallKey = baseKey.CreateSubKey(uninstallKeyPath, writable: true))
                    {
                        uninstallKey.SetValue("DisplayName", "Mix It Up", RegistryValueKind.String);
                        uninstallKey.SetValue("DisplayVersion", displayVersion, RegistryValueKind.String);
                        uninstallKey.SetValue("InstallLocation", normalizedAppRoot, RegistryValueKind.String);
                        uninstallKey.SetValue("Publisher", "Mix It Up", RegistryValueKind.String);
                        uninstallKey.SetValue("UninstallString", uninstallCommand, RegistryValueKind.String);

                        string iconPath = launcherExists
                            ? normalizedLauncherPath
                            : (uninstallerExists ? normalizedUninstallerPath : normalizedLauncherPath);
                        uninstallKey.SetValue("DisplayIcon", iconPath, RegistryValueKind.String);

                        uninstallKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        uninstallKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);

                        if (uninstallerExists)
                        {
                            uninstallKey.SetValue(
                                "QuietUninstallString",
                                $"\"{normalizedUninstallerPath}\"",
                                RegistryValueKind.String
                            );
                        }

                        hiveUsed = hive;
                        break;
                    }
                }
                catch (UnauthorizedAccessException uaex)
                {
                    lastError = uaex;
                    this.WriteToLogFile(
                        $"Unauthorized to write uninstall key under {hive}: {uaex.GetType().Name} - {uaex.Message}"
                    );
                    continue;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    this.WriteToLogFile(
                        $"Failed to write uninstall key under {hive}: {ex.GetType().Name} - {ex.Message}"
                    );
                }
            }

            if (!hiveUsed.HasValue)
            {
                this.SetStepState(InstallerStep.Register, StepStatus.Failed);

                this.LogActivity("Failed to register uninstall information with Windows.");
                if (lastError != null)
                {
                    this.WriteToLogFile(lastError.ToString());
                }

                this.ShowError(
                    "Registration Failed",
                    "Couldn't register uninstall entry. You can still use the app; try reinstalling to fix."
                );
                this.SetHyperlinkToLogFile();
                return false;
            }

            string hiveDisplay = hiveUsed == RegistryHive.LocalMachine ? "HKLM" : "HKCU";
            this.SetStepState(InstallerStep.Register, StepStatus.Completed);
            this.DisplayText1 = "Windows uninstall entry registered.";
            this.DisplayText2 = string.Empty;

            if (uninstallerExists)
            {
                this.LogActivity(
                    $"Registered uninstall entry under {hiveDisplay} pointing to '{normalizedUninstallerPath}'."
                );
            }
            else
            {
                this.LogActivity(
                    $"Registered uninstall entry under {hiveDisplay} using launcher '--uninstall' handler."
                );
            }

            return true;
        }

        private bool CreateShortcuts()
        {
            this.DisplayText1 = "Creating Start Menu shortcut...";
            this.DisplayText2 = string.Empty;
            this.IsOperationIndeterminate = true;
            this.IsOperationBeingPerformed = true;

            this.SetStepState(InstallerStep.Shortcuts, StepStatus.InProgress);

            string appRoot = this.AppRoot;
            if (string.IsNullOrWhiteSpace(appRoot))
            {
                appRoot = DefaultInstallDirectory;
            }

            string normalizedAppRoot = NormalizePath(appRoot);
            string launcherPath = Path.Combine(appRoot, "MixItUp.exe");
            string normalizedLauncherPath = NormalizePath(launcherPath);

            ShortcutCreationResult startMenuResult = this.TryCreateShortcutAtLocation(
                StartMenuShortCutFilePath,
                normalizedLauncherPath,
                normalizedAppRoot
            );

            bool startMenuSucceeded = startMenuResult == ShortcutCreationResult.Success
                || startMenuResult == ShortcutCreationResult.AlreadyExists;

            ShortcutCreationResult desktopResult = ShortcutCreationResult.Failed;
            bool desktopAttempted = false;

            if (!startMenuSucceeded)
            {
                this.DisplayText1 = "Creating Desktop shortcut...";
                desktopAttempted = true;
                desktopResult = this.TryCreateShortcutAtLocation(
                    DesktopShortCutFilePath,
                    normalizedLauncherPath,
                    normalizedAppRoot
                );
            }

            bool desktopSucceeded = desktopResult == ShortcutCreationResult.Success
                || desktopResult == ShortcutCreationResult.AlreadyExists;

            bool shortcutAvailable = startMenuSucceeded || desktopSucceeded;

            if (!shortcutAvailable)
            {
                this.SetStepState(InstallerStep.Shortcuts, StepStatus.Failed);

                this.LogActivity("Unable to create Start Menu or Desktop shortcuts.");

                this.ShowError(
                    "Shortcut Locations Locked",
                    "Unable to write to Start Menu or Desktop. Create shortcuts manually."
                );
                this.SetHyperlinkToLogFile();
                return false;
            }

            this.SetStepState(InstallerStep.Shortcuts, StepStatus.Completed);
            this.DisplayText1 = "Shortcuts created.";
            this.DisplayText2 = string.Empty;

            if (startMenuSucceeded)
            {
                this.LogActivity(
                    $"Start Menu shortcut available at '{NormalizePath(StartMenuShortCutFilePath)}'."
                );
            }

            if (desktopAttempted)
            {
                if (desktopSucceeded)
                {
                    this.LogActivity(
                        $"Desktop shortcut available at '{NormalizePath(DesktopShortCutFilePath)}'."
                    );
                }
                else if (desktopResult == ShortcutCreationResult.AccessDenied)
                {
                    this.LogActivity("Desktop shortcut creation skipped due to access restrictions.");
                }
            }

            return true;
        }

        private ShortcutCreationResult TryCreateShortcutAtLocation(
            string shortcutPath,
            string launcherPath,
            string workingDirectory
        )
        {
            string directory = Path.GetDirectoryName(shortcutPath);
            if (!string.IsNullOrEmpty(directory))
            {
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (UnauthorizedAccessException uaex)
                {
                    this.LogActivity(
                        $"Access denied when creating shortcut directory '{NormalizePath(directory)}'."
                    );
                    this.WriteToLogFile(
                        $"Unauthorized to create shortcut directory '{directory}': {uaex}"
                    );
                    return ShortcutCreationResult.AccessDenied;
                }
                catch (Exception ex)
                {
                    this.LogActivity(
                        $"Failed to prepare shortcut directory '{NormalizePath(directory)}'."
                    );
                    this.WriteToLogFile(
                        $"Failed to ensure shortcut directory '{directory}': {ex}"
                    );
                    return ShortcutCreationResult.Failed;
                }
            }

            if (File.Exists(shortcutPath))
            {
                this.LogActivity(
                    $"Shortcut already exists at '{NormalizePath(shortcutPath)}'; skipping creation."
                );
                return ShortcutCreationResult.AlreadyExists;
            }

            if (this.TryCopyTemplateShortcut(shortcutPath, launcherPath, workingDirectory))
            {
                return ShortcutCreationResult.Success;
            }

            if (this.TryCreateShortcutWithCom(shortcutPath, launcherPath, workingDirectory))
            {
                return ShortcutCreationResult.Success;
            }

            this.WriteToLogFile(
                $"Failed to create shortcut at '{NormalizePath(shortcutPath)}' using template or COM."
            );
            return ShortcutCreationResult.Failed;
        }

        private bool TryCopyTemplateShortcut(
            string shortcutPath,
            string launcherPath,
            string workingDirectory
        )
        {
            string runningDirectory = this.RunningDirectory;
            if (string.IsNullOrWhiteSpace(runningDirectory))
            {
                return false;
            }

            string templatePath = Path.Combine(runningDirectory, "Mix It Up.link");
            if (!File.Exists(templatePath))
            {
                return false;
            }

            try
            {
                File.Copy(templatePath, shortcutPath, overwrite: false);

                if (!this.TryConfigureShortcut(shortcutPath, launcherPath, workingDirectory, launcherPath))
                {
                    this.TryDeleteFile(shortcutPath);
                    return false;
                }

                this.LogActivity(
                    $"Shortcut created from template at '{NormalizePath(shortcutPath)}'."
                );
                return true;
            }
            catch (IOException ioex)
            {
                this.WriteToLogFile(ioex.ToString());
            }
            catch (UnauthorizedAccessException uaex)
            {
                this.WriteToLogFile(uaex.ToString());
            }
            catch (Exception ex)
            {
                this.WriteToLogFile(ex.ToString());
            }

            return false;
        }

        private bool TryCreateShortcutWithCom(
            string shortcutPath,
            string launcherPath,
            string workingDirectory
        )
        {
            if (!this.TryConfigureShortcut(shortcutPath, launcherPath, workingDirectory, launcherPath))
            {
                return false;
            }

            this.LogActivity(
                $"Shortcut created via COM automation at '{NormalizePath(shortcutPath)}'."
            );
            return true;
        }

        private bool TryConfigureShortcut(
            string shortcutPath,
            string launcherPath,
            string workingDirectory,
            string iconPath
        )
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                this.LogActivity("WScript.Shell COM automation is unavailable on this system.");
                this.WriteToLogFile("WScript.Shell COM automation is unavailable on this system.");
                return false;
            }

            object shellObject = null;
            object shortcutObject = null;

            try
            {
                shellObject = Activator.CreateInstance(shellType);
                dynamic shell = shellObject;
                shortcutObject = shell.CreateShortcut(shortcutPath);
                dynamic shortcut = shortcutObject;

                shortcut.TargetPath = launcherPath;
                shortcut.WorkingDirectory = workingDirectory;
                shortcut.Arguments = string.Empty;
                shortcut.IconLocation = iconPath;
                shortcut.Description = "Launch Mix It Up";
                shortcut.Save();

                return true;
            }
            catch (Exception ex)
            {
                this.WriteToLogFile(
                    $"Failed to configure shortcut '{NormalizePath(shortcutPath)}': {ex}"
                );
                return false;
            }
            finally
            {
                if (shortcutObject != null)
                {
                    try
                    {
                        Marshal.FinalReleaseComObject(shortcutObject);
                    }
                    catch
                    {
                    }
                }

                if (shellObject != null)
                {
                    try
                    {
                        Marshal.FinalReleaseComObject(shellObject);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void MarkInstallationComplete()
        {
            this.SetStepState(InstallerStep.Complete, StepStatus.InProgress);

            this.DisplayText1 = "Installation complete.";
            this.DisplayText2 = "Mix It Up is ready.";

            this.IsOperationIndeterminate = false;
            this.IsOperationBeingPerformed = false;
            this.OperationProgress = 100;
            this.DownloadPercent = 0;

            this.ErrorMessage = string.Empty;
            this.SpecificErrorMessage = string.Empty;
            this.HyperlinkAddress = string.Empty;

            this.SetStepState(InstallerStep.Complete, StepStatus.Completed);

            this.launchCommand?.RaiseCanExecuteChanged();

            this.LogActivity($"Installer completed successfully at {DateTime.UtcNow:u}.");
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

            bool downloadWorkspacePrepared = false;
            InstallerStep? activeStep = null;

            try
            {
                activeStep = InstallerStep.Preflight;
                if (!this.Preflight())
                {
                    activeStep = null;
                    return false;
                }

                activeStep = InstallerStep.Discover;
                if (!await this.DiscoverInstallContextAsync())
                {
                    activeStep = null;
                    return false;
                }

                this.PrepareDownloadWorkspace();
                downloadWorkspacePrepared = true;

                activeStep = InstallerStep.CloseProcesses;
                if (!await this.WaitForProcessesToExitAsync())
                {
                    activeStep = null;
                    return false;
                }

                activeStep = InstallerStep.Migrate;
                if (!await this.MigrateIfNeededAsync())
                {
                    activeStep = null;
                    return false;
                }

                activeStep = InstallerStep.LauncherFetch;
                this.SetStepState(InstallerStep.LauncherFetch, StepStatus.InProgress);

                UpdatePackageInfo launcherPackage = await this.ResolveLauncherPackageAsync();
                if (launcherPackage == null)
                {
                    this.SetStepState(InstallerStep.LauncherFetch, StepStatus.Failed);
                    activeStep = null;
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
                    this.SetStepState(InstallerStep.LauncherFetch, StepStatus.Failed);
                    activeStep = null;
                    return false;
                }

                this.SetStepState(InstallerStep.LauncherFetch, StepStatus.Completed);

                activeStep = InstallerStep.LauncherInstall;
                this.SetStepState(InstallerStep.LauncherInstall, StepStatus.InProgress);

                long launcherSizeHint = launcherPackage?.File?.Size ?? (launcherArchive?.LongLength ?? 0L);
                if (
                    !this.EnsureDiskSpace(
                        this.AppRoot,
                        launcherSizeHint,
                        InstallerStep.LauncherInstall,
                        "Launcher installation"
                    )
                )
                {
                    activeStep = null;
                    return false;
                }

                bool launcherInstalled = this.InstallLauncherArchive(
                    launcherArchive,
                    launcherPackage
                );

                if (launcherArchive != null)
                {
                    Array.Clear(launcherArchive, 0, launcherArchive.Length);
                }
                launcherArchive = null;

                if (!launcherInstalled)
                {
                    this.SetStepState(InstallerStep.LauncherInstall, StepStatus.Failed);
                    activeStep = null;
                    return false;
                }

                this.SetStepState(InstallerStep.LauncherInstall, StepStatus.Completed);

                this.OperationProgress = 0;
                this.DownloadPercent = 0;
                this.IsOperationIndeterminate = true;

                activeStep = InstallerStep.AppFetch;
                this.SetStepState(InstallerStep.AppFetch, StepStatus.InProgress);

                UpdatePackageInfo appPackage = await this.ResolveAppPackageAsync();
                if (appPackage == null)
                {
                    this.SetStepState(InstallerStep.AppFetch, StepStatus.Failed);
                    activeStep = null;
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
                    this.SetStepState(InstallerStep.AppFetch, StepStatus.Failed);
                    activeStep = null;
                    return false;
                }

                this.SetStepState(InstallerStep.AppFetch, StepStatus.Completed);

                activeStep = InstallerStep.AppExtract;
                this.SetStepState(InstallerStep.AppExtract, StepStatus.InProgress);

                long appSizeHint = appPackage?.File?.Size ?? (appArchive?.LongLength ?? 0L);
                if (
                    !this.EnsureDiskSpace(
                        this.VersionedAppDirRoot,
                        appSizeHint,
                        InstallerStep.AppExtract,
                        "Application extraction"
                    )
                )
                {
                    activeStep = null;
                    return false;
                }

                bool appInstalled = this.InstallAppArchive(appArchive, appPackage);

                if (appArchive != null)
                {
                    Array.Clear(appArchive, 0, appArchive.Length);
                }
                appArchive = null;

                if (!appInstalled)
                {
                    this.SetStepState(InstallerStep.AppExtract, StepStatus.Failed);
                    activeStep = null;
                    return false;
                }

                this.SetStepState(InstallerStep.AppExtract, StepStatus.Completed);

                this.OperationProgress = 0;
                this.DownloadPercent = 0;
                this.IsOperationIndeterminate = true;

                activeStep = InstallerStep.DataCopy;
                if (!await this.CopyUserDataAsync())
                {
                    activeStep = null;
                    return false;
                }
                activeStep = null;

                activeStep = InstallerStep.ConfigWrite;
                if (!await this.WriteOrUpdateBootloaderConfigAsync())
                {
                    activeStep = null;
                    return false;
                }
                activeStep = null;

                activeStep = InstallerStep.Register;
                if (!this.RegisterUninstallEntry())
                {
                    activeStep = null;
                    return false;
                }
                activeStep = null;

                activeStep = InstallerStep.Shortcuts;
                if (!this.CreateShortcuts())
                {
                    activeStep = null;
                    return false;
                }

                activeStep = InstallerStep.Complete;
                this.MarkInstallationComplete();
                activeStep = null;
                return true;
            }
            catch (Exception ex)
            {
                string stepContext = string.Empty;
                if (activeStep.HasValue)
                {
                    stepContext = StepDisplayNames.TryGetValue(activeStep.Value, out string displayName)
                        ? displayName
                        : activeStep.Value.ToString();
                }

                string errorContext = string.IsNullOrEmpty(stepContext)
                    ? "installer execution"
                    : string.Format("step '{0}'", stepContext);

                this.WriteToLogFile($"Unexpected error during {errorContext}: {ex}", "ERROR");
                this.LogActivity($"Unexpected error during {errorContext}.", "ERROR");

                if (activeStep.HasValue)
                {
                    this.SetStepState(activeStep.Value, StepStatus.Failed);
                }

                this.ShowError(
                    "Unexpected Installer Error",
                    "See the installer log for details, correct the issue, and try again."
                );
                this.SetHyperlinkToLogFile();
                return false;
            }
            finally
            {
                if (downloadWorkspacePrepared)
                {
                    this.CleanupDownloadWorkspace();
                }
            }
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
                if (!File.Exists(this.InstallerLogFilePath))
                {
                    this.WriteToLogFile("Installer exited without additional log details.");
                }

                Uri logUri = this.GetInstallerLogFileUri();

                if (!string.IsNullOrEmpty(this.SpecificErrorMessage))
                {
                    this.HyperlinkAddress = logUri?.AbsoluteUri ?? string.Empty;
                    this.ShowError(
                        string.Format("{0} file created:", InstallerLogFileName),
                        this.SpecificErrorMessage
                    );
                }
                else
                {
                    this.HyperlinkAddress = logUri?.AbsoluteUri ?? string.Empty;
                    this.ShowError(
                        string.Format("{0} file created:", InstallerLogFileName),
                        "Please visit our support Discord or send an email to support@mixitupapp.com with the contents of this file."
                    );
                }
            }
            return result;
        }

        private void ExecuteLaunch()
        {
            this.LogActivity("Launch button clicked; attempting to start Mix It Up.");
            this.Launch();
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
            if (!string.IsNullOrWhiteSpace(combinedMessage))
            {
                this.LogActivity(combinedMessage, "ERROR");
            }
            else
            {
                this.LogActivity("An error occurred with no additional message.", "ERROR");
            }
            this.launchCommand?.RaiseCanExecuteChanged();
        }

        private void LogActivity(string message, string level = "INFO")
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            string trimmedMessage = message.Trim();
            string timestampedMessage = string.Format(
                "[{0:HH:mm:ss}] {1}",
                DateTime.UtcNow,
                level.Equals("INFO", StringComparison.OrdinalIgnoreCase)
                    ? trimmedMessage
                    : string.Format("{0}: {1}", level.ToUpperInvariant(), trimmedMessage)
            );

            this.WriteToLogFile(trimmedMessage, level);

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
            string logPath = this.InstallerLogFilePath;

            try
            {
                string directory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(logPath, string.Empty);
            }
            catch
            {
                // If we cannot truncate the previous log, we'll append to it instead.
            }
        }

        private void WriteToLogFile(string text, string level = "INFO")
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string logPath = this.InstallerLogFilePath;

            try
            {
                string directory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string entry = string.Format(
                    "[{0:u}] [{1}] {2}{3}",
                    DateTime.UtcNow,
                    level,
                    text.Trim(),
                    Environment.NewLine
                );

                File.AppendAllText(logPath, entry);
            }
            catch
            {
                // Swallow logging errors to avoid masking the original issue.
            }
        }

        private string GetInstallerLogFilePath()
        {
            string root = this.AppRoot;
            if (string.IsNullOrWhiteSpace(root))
            {
                root = DefaultInstallDirectory;
            }

            string normalizedRoot = NormalizePath(root);
            if (string.IsNullOrWhiteSpace(normalizedRoot))
            {
                normalizedRoot = root;
            }

            return Path.Combine(normalizedRoot, InstallerLogFileName);
        }

        private Uri GetInstallerLogFileUri()
        {
            try
            {
                return new Uri(this.InstallerLogFilePath);
            }
            catch
            {
                return null;
            }
        }

        private void SetHyperlinkToLogFile()
        {
            Uri logUri = this.GetInstallerLogFileUri();
            this.HyperlinkAddress = logUri?.AbsoluteUri ?? string.Empty;
        }

        private void PrepareDownloadWorkspace()
        {
            string tempPath = this.DownloadTempPath;
            if (string.IsNullOrWhiteSpace(tempPath))
            {
                return;
            }

            try
            {
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, recursive: true);
                }

                Directory.CreateDirectory(tempPath);
                this.LogActivity($"Prepared temporary workspace at '{NormalizePath(tempPath)}'.");
            }
            catch (Exception ex)
            {
                this.WriteToLogFile($"Failed to prepare temporary workspace '{tempPath}': {ex}");
                this.LogActivity(
                    $"Failed to prepare temporary workspace at '{NormalizePath(tempPath)}'.",
                    "ERROR"
                );
            }
        }

        private void CleanupDownloadWorkspace()
        {
            string tempPath = this.DownloadTempPath;
            if (string.IsNullOrWhiteSpace(tempPath))
            {
                return;
            }

            try
            {
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, recursive: true);
                    this.LogActivity($"Cleaned temporary workspace at '{NormalizePath(tempPath)}'.");
                }
            }
            catch (Exception ex)
            {
                this.WriteToLogFile($"Failed to clean temporary workspace '{tempPath}': {ex}");
                this.LogActivity(
                    $"Failed to clean temporary workspace at '{NormalizePath(tempPath)}'.",
                    "WARN"
                );
            }
        }

        private bool EnsureDiskSpace(string targetPath, long? sizeHintBytes, InstallerStep step, string componentName)
        {
            try
            {
                string resolvedPath = string.IsNullOrWhiteSpace(targetPath)
                    ? DefaultInstallDirectory
                    : targetPath;

                string normalized = NormalizePath(resolvedPath);
                string root = Path.GetPathRoot(normalized);
                if (string.IsNullOrEmpty(root))
                {
                    return true;
                }

                DriveInfo driveInfo = new DriveInfo(root);
                long available = driveInfo.AvailableFreeSpace;

                long required = sizeHintBytes.GetValueOrDefault();
                if (required <= 0)
                {
                    required = 200L * 1024 * 1024; // default to 200 MB
                }

                required = (long)Math.Min(long.MaxValue, Math.Max(required, 50L * 1024 * 1024));
                required = (long)Math.Ceiling(required * 1.3); // 30% buffer

                if (available < required)
                {
                    this.LogActivity(
                        $"{componentName} requires approximately {FormatBytes(required)} of free space, but only {FormatBytes(available)} is available on {driveInfo.Name}.",
                        "ERROR"
                    );
                    this.SetStepState(step, StepStatus.Failed);
                    this.ShowError(
                        "Not Enough Disk Space",
                        $"Free at least {FormatBytes(required)} on {driveInfo.Name} and try again."
                    );
                    this.SetHyperlinkToLogFile();
                    return false;
                }
            }
            catch (Exception ex)
            {
                this.WriteToLogFile($"Disk space validation failed for '{targetPath}': {ex}");
            }

            return true;
        }

        private static string FormatBytes(long bytes)
        {
            const long Kilo = 1024;
            const long Mega = Kilo * 1024;
            const long Giga = Mega * 1024;
            const long Tera = Giga * 1024;

            double value = bytes;
            string unit = "B";

            if (bytes >= Tera)
            {
                value = bytes / (double)Tera;
                unit = "TB";
            }
            else if (bytes >= Giga)
            {
                value = bytes / (double)Giga;
                unit = "GB";
            }
            else if (bytes >= Mega)
            {
                value = bytes / (double)Mega;
                unit = "MB";
            }
            else if (bytes >= Kilo)
            {
                value = bytes / (double)Kilo;
                unit = "KB";
            }

            return string.Format("{0:0.##} {1}", value, unit);
        }
    }
}
