using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using MixItUp.Base.Model.API;
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
        public const string AutoHosterProcessName = "MixItUp.AutoHoster";

        private static readonly Version minimumOSVersion = new Version(6, 2, 0, 0);

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
            this.IsSupportedOS = Environment.OSVersion.Version >= minimumOSVersion;
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

        public bool CheckCompatability()
        {
            this.UpdateEnvironmentState();

            if (!this.IsSupportedOS)
            {
                this.ShowError(
                    "Mix It Up only runs on Windows 8 & higher.",
                    "If incorrect, please contact support@mixitupapp.com"
                );
                return false;
            }
            return true;
        }

        public async Task<bool> Run()
        {
            bool result = false;
            string failureMessage = null;

            await Task.Run(async () =>
            {
                try
                {
                    this.ResetLogFile();

                    bool canProceed = !this.IsUpdate || await this.WaitForMixItUpToClose();
                    if (canProceed)
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
                    else
                    {
                        failureMessage =
                            "Please close Mix It Up (and MixItUp.AutoHoster) before running the installer.";
                        this.WriteToLogFile(
                            "Installation aborted because Mix It Up or MixItUp.AutoHoster was still running."
                        );
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

        private async Task<bool> WaitForMixItUpToClose()
        {
            this.DisplayText1 = "Waiting for Mix It Up to close...";
            this.IsOperationIndeterminate = true;
            this.OperationProgress = 0;

            for (int i = 0; i < 10; i++)
            {
                bool isRunning = false;
                foreach (Process clsProcess in Process.GetProcesses())
                {
                    if (
                        clsProcess.ProcessName.Equals(MixItUpProcessName)
                        || clsProcess.ProcessName.Equals(AutoHosterProcessName)
                    )
                    {
                        isRunning = true;
                        if (i == 5)
                        {
                            clsProcess.CloseMainWindow();
                        }
                    }
                }

                if (!isRunning)
                {
                    return true;
                }
                await Task.Delay(1000);
            }
            return false;
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
