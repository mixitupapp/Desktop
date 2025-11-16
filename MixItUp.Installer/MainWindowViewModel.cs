using MixItUp.Base.Model.API;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Text;

namespace MixItUp.Installer
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public const string InstallerLogFileName = "MixItUp-Installer-Log.txt";
        public const string ShortcutFileName = "Mix It Up.lnk";

        public const string OldApplicationSettingsFileName = "ApplicationSettings.xml";
        public const string NewApplicationSettingsFileName = "ApplicationSettings.json";

        public const string MixItUpProcessName = "MixItUp";
        public const string AutoHosterProcessName = "MixItUp.AutoHoster";

        private static readonly Version minimumOSVersion = new Version(10, 0, 0, 0);

        // .NET 10 Runtime constants
        private const int RequiredDotNetMajorVersion = 10;
        private const int RequiredDotNetMinorVersion = 0;
        private const int RequiredDotNetPatchVersion = 0;
        private const string DotNetDesktopRuntimeDownloadUrl = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.0/windowsdesktop-runtime-10.0.0-win-x64.exe";
        private const string DotNetManualInstallUrl = "https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-10.0.0-windows-x64-installer";
        private const string RuntimeInstallerFileName = "windowsdesktop-runtime-10.0.0-win-x64.exe";
        private const string DotNetSharedPath = @"dotnet\shared\Microsoft.WindowsDesktop.App";
        private const string RuntimeTempDirectoryName = "MixItUp-Runtime";

        public static readonly string DefaultInstallDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MixItUp");
        public static readonly string StartMenuDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Mix It Up");

        public static string InstallSettingsDirectory { get { return Path.Combine(MainWindowViewModel.DefaultInstallDirectory, "Settings"); } }

        private const string FileServiceBaseUrl = "https://files.mixitupapp.com/apps/mixitup-desktop/windows-x64";
        private const string TempDirectoryName = ".tmp";
        private const string EulaAcceptedFileName = "eula-accepted";
        private static readonly TimeSpan[] ManifestRetryDelays = new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8) };
        private static readonly TimeSpan[] DownloadRetryDelays = new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8) };
        private static readonly HttpClient HttpClient = new HttpClient();

        private MixItUpUpdateModel latestUpdate;
        private string manifestChannel;
        private string manifestUrl;
        private string packageUrl;
        private string installerUrl;
        private string expectedSha256;
        private string downloadedPackagePath;
        private string downloadedRuntimePath;
        private string tempDirectoryPath;
        private readonly string installDirectoryArgument;
        private string installDirectoryResolutionNote;

        public Func<string, string, Task<bool>> ShowEulaDialogAsync { private get; set; }

        public static string StartMenuShortCutFilePath { get { return Path.Combine(StartMenuDirectory, ShortcutFileName); } }
        public static string DesktopShortCutFilePath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), ShortcutFileName); } }

        public event PropertyChangedEventHandler PropertyChanged;

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

        public bool IsInstall { get { return !this.IsUpdate; } }

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

        public bool ShowHyperlinkAddress { get { return !string.IsNullOrEmpty(this.HyperlinkAddress); } }

        private string installDirectory;

        public MainWindowViewModel()
        {
            this.installDirectory = DefaultInstallDirectory;
            this.installDirectoryArgument = null;
            this.installDirectoryResolutionNote = null;

            string[] args = Environment.GetCommandLineArgs();
            if (args.Length >= 2)
            {
                string rawArgument = args[1];
                string sanitizedArgument = SanitizeInstallDirectoryArgument(rawArgument);
                if (!string.IsNullOrEmpty(sanitizedArgument))
                {
                    this.installDirectoryArgument = sanitizedArgument;

                    if (Directory.Exists(sanitizedArgument))
                    {
                        if (DoesDirectoryContainExistingInstall(sanitizedArgument))
                        {
                            this.installDirectory = sanitizedArgument;
                        }
                        else
                        {
                            this.installDirectory = DefaultInstallDirectory;
                            this.installDirectoryResolutionNote = string.Format("Install directory argument '{0}' does not contain an existing Mix It Up installation; defaulting to '{1}'.", sanitizedArgument, DefaultInstallDirectory);
                        }
                    }
                    else
                    {
                        this.installDirectory = DefaultInstallDirectory;
                        this.installDirectoryResolutionNote = string.Format("Install directory argument '{0}' does not exist; defaulting to '{1}'.", sanitizedArgument, DefaultInstallDirectory);
                    }
                }
                else
                {
                    this.installDirectory = DefaultInstallDirectory;
                    this.installDirectoryResolutionNote = string.Format("Install directory argument '{0}' could not be parsed; defaulting to '{1}'.", rawArgument, DefaultInstallDirectory);
                }
            }

            if (Directory.Exists(this.installDirectory))
            {
                this.IsUpdate = true;
                string applicationSettingsFilePath = Path.Combine(this.installDirectory, NewApplicationSettingsFileName);
                if (!File.Exists(applicationSettingsFilePath))
                {
                    applicationSettingsFilePath = Path.Combine(this.installDirectory, OldApplicationSettingsFileName);
                }

                if (File.Exists(applicationSettingsFilePath))
                {
                    using (StreamReader reader = new StreamReader(File.OpenRead(applicationSettingsFilePath)))
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

            if (this.IsTest)
            {
                this.IsPreview = true;
            }

            this.DisplayText1 = "Preparing installation...";
            this.isOperationBeingPerformed = true;
            this.IsOperationIndeterminate = true;
        }

        public bool CheckCompatability()
        {
            if (Environment.OSVersion.Version < minimumOSVersion)
            {
                this.ShowError("Mix It Up only runs on Windows 10 & higher.", "If incorrect, please contact support@mixitupapp.com");
                return false;
            }
            return true;
        }

        public async Task<bool> Run()
        {
            bool result = false;

            await Task.Run(async () =>
            {
                try
                {
                    File.Delete(InstallerLogFileName);
                    this.WriteToLogFile("Installation started: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    this.WriteToLogFile("OS Version: " + Environment.OSVersion.Version.ToString());

                    if (!string.IsNullOrEmpty(this.installDirectoryArgument))
                    {
                        this.WriteToLogFile("Install directory argument received: " + this.installDirectoryArgument);
                    }
                    if (!string.IsNullOrEmpty(this.installDirectoryResolutionNote))
                    {
                        this.WriteToLogFile(this.installDirectoryResolutionNote);
                    }
                    this.WriteToLogFile("Resolved install directory: " + this.installDirectory);

                    if (!await this.EnsureDotNetRuntimeInstalledAsync())
                    {
                        return;
                    }

                    if (!this.IsUpdate || await this.WaitForMixItUpToClose())
                    {
                        MixItUpUpdateModel update = await this.FetchManifestAsync();

                        if (update == null)
                        {
                            this.ShowNetworkRetryError("We were unable to retrieve update information from the Mix It Up file service. Please check your network connection and try again.");
                            return;
                        }

                        if (!update.Active)
                        {
                            this.WriteToLogFile("Manifest inactive for channel: " + (this.manifestChannel ?? "<unknown>"));
                            this.SpecificErrorMessage = "There are currently no active builds for this update channel. Please try again later or contact support@mixitupapp.com.";
                            this.ShowError("No active updates available.", this.SpecificErrorMessage);
                            return;
                        }

                        this.latestUpdate = update;
                        this.packageUrl = update.Package;
                        this.installerUrl = update.Installer;
                        this.expectedSha256 = update.Sha256;

                        this.WriteToLogFile(string.Format("Manifest summary:{0}- Channel: {1}{0}- Manifest URL: {2}{0}- Package URL: {3}{0}- Installer URL: {4}{0}- Expected SHA-256: {5}",
                            Environment.NewLine,
                            this.manifestChannel ?? "<unknown>",
                            this.manifestUrl ?? "<unknown>",
                            this.packageUrl ?? "<missing>",
                            this.installerUrl ?? "<missing>",
                            string.IsNullOrEmpty(this.expectedSha256) ? "<missing>" : this.expectedSha256));

                        if (string.IsNullOrEmpty(this.packageUrl))
                        {
                            this.SpecificErrorMessage = "The update manifest did not include a package download link. Please try again later.";
                            this.ShowError("Invalid update manifest.", this.SpecificErrorMessage);
                            return;
                        }

                        if (!await this.EnsureEulaAcceptedAsync(update))
                        {
                            return;
                        }

                        if (await this.DownloadPackageAsync(update))
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
                    this.WriteToLogFile(ex.ToString());
                }
            });

            if (!result && !this.ErrorOccurred)
            {
                if (!string.IsNullOrEmpty(this.SpecificErrorMessage))
                {
                    this.HyperlinkAddress = InstallerLogFileName;
                    this.ShowError(string.Format("{0} file created:", InstallerLogFileName), this.SpecificErrorMessage);
                }
                else
                {
                    this.HyperlinkAddress = InstallerLogFileName;
                    this.ShowError(string.Format("{0} file created:", InstallerLogFileName), "An installation error occured. Please visit our support Discord or send an email to support@mixitupapp.com with the contents of this file.");
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
                        UseShellExecute = true
                    };
                    Process.Start(processInfo);
                }
                else if (File.Exists(DesktopShortCutFilePath))
                {
                    ProcessStartInfo processInfo = new ProcessStartInfo(DesktopShortCutFilePath)
                    {
                        UseShellExecute = true
                    };
                    Process.Start(processInfo);
                }
            }
            else
            {
                Process.Start(Path.Combine(this.installDirectory, "MixItUp.exe"));
            }
        }

        protected void NotifyPropertyChanged([CallerMemberName] string name = "")
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private async Task<bool> EnsureDotNetRuntimeInstalledAsync()
        {
            this.DisplayText1 = $"Checking .NET {RequiredDotNetMajorVersion} Desktop Runtime...";
            this.IsOperationIndeterminate = true;
            this.OperationProgress = 0;

            this.WriteToLogFile($"Checking for .NET {RequiredDotNetMajorVersion} Desktop Runtime installation...");

            if (IsDotNetDesktopRuntimeInstalled())
            {
                this.WriteToLogFile($".NET {RequiredDotNetMajorVersion} Desktop Runtime is already installed.");
                this.DisplayText1 = $".NET {RequiredDotNetMajorVersion} Desktop Runtime found.";
                await Task.Delay(500);
                return true;
            }

            this.WriteToLogFile($".NET {RequiredDotNetMajorVersion} Desktop Runtime not found. Downloading installer...");

            this.tempDirectoryPath = Path.Combine(Path.GetTempPath(), RuntimeTempDirectoryName);
            Directory.CreateDirectory(this.tempDirectoryPath);

            this.downloadedRuntimePath = Path.Combine(this.tempDirectoryPath, RuntimeInstallerFileName);
            this.WriteToLogFile("Runtime installer temp path: " + this.downloadedRuntimePath);

            if (!await this.DownloadDotNetRuntimeAsync())
            {
                return false;
            }

            this.DisplayText1 = $"Installing .NET {RequiredDotNetMajorVersion} Desktop Runtime...";
            this.DisplayText2 = "This may take a few minutes.";
            this.IsOperationIndeterminate = true;

            this.WriteToLogFile("Launching .NET runtime installer: " + this.downloadedRuntimePath);

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = this.downloadedRuntimePath,
                    Arguments = "/quiet /norestart",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        await Task.Run(() => process.WaitForExit());

                        this.WriteToLogFile($".NET runtime installer exited with code: {process.ExitCode}");

                        if (process.ExitCode == 0 || process.ExitCode == 3010)
                        {
                            this.WriteToLogFile($".NET {RequiredDotNetMajorVersion} Desktop Runtime installed successfully.");
                            this.CleanupRuntimeDownload();
                            return true;
                        }
                        else if (process.ExitCode == 1638)
                        {
                            this.WriteToLogFile($".NET {RequiredDotNetMajorVersion} Desktop Runtime was already installed (exit code 1638).");
                            this.CleanupRuntimeDownload();
                            return true;
                        }
                        else
                        {
                            this.WriteToLogFile($".NET runtime installer failed with exit code: {process.ExitCode}");
                            this.SpecificErrorMessage = $".NET {RequiredDotNetMajorVersion} Desktop Runtime installation failed (exit code: {process.ExitCode}). Please install it manually from {DotNetManualInstallUrl}";
                            this.ShowError(".NET Runtime installation failed.", this.SpecificErrorMessage);
                            return false;
                        }
                    }
                    else
                    {
                        this.WriteToLogFile("Failed to start .NET runtime installer process.");
                        this.ShowError(".NET Runtime installation failed.", "Could not start the .NET runtime installer.");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                this.WriteToLogFile(".NET runtime installation error: " + ex.ToString());
                this.SpecificErrorMessage = $"An error occurred while installing .NET {RequiredDotNetMajorVersion} Desktop Runtime. Please install it manually from {DotNetManualInstallUrl}";
                this.ShowError(".NET Runtime installation failed.", this.SpecificErrorMessage);
                return false;
            }
        }

        private bool IsDotNetDesktopRuntimeInstalled()
        {
            try
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string dotnetPath = Path.Combine(programFiles, DotNetSharedPath);

                if (Directory.Exists(dotnetPath))
                {
                    string[] versionDirs = Directory.GetDirectories(dotnetPath);

                    foreach (string versionDir in versionDirs)
                    {
                        string versionName = Path.GetFileName(versionDir);
                        string[] versionParts = versionName.Split(new[] { '.', '-' });

                        if (versionParts.Length >= 3 &&
                            int.TryParse(versionParts[0], out int major) &&
                            int.TryParse(versionParts[1], out int minor) &&
                            int.TryParse(versionParts[2], out int patch))
                        {
                            if (major > RequiredDotNetMajorVersion ||
                                (major == RequiredDotNetMajorVersion && minor > RequiredDotNetMinorVersion) ||
                                (major == RequiredDotNetMajorVersion && minor == RequiredDotNetMinorVersion && patch >= RequiredDotNetPatchVersion))
                            {
                                this.WriteToLogFile($".NET {RequiredDotNetMajorVersion}.{RequiredDotNetMinorVersion}.{RequiredDotNetPatchVersion}+ Desktop Runtime detected: " + versionName);
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.WriteToLogFile("Error checking .NET runtime: " + ex.ToString());
            }

            this.WriteToLogFile($".NET {RequiredDotNetMajorVersion}.{RequiredDotNetMinorVersion}.{RequiredDotNetPatchVersion}+ Desktop Runtime not detected.");
            return false;
        }

        private async Task<bool> DownloadDotNetRuntimeAsync()
        {
            int maxAttempts = DownloadRetryDelays.Length + 1;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    this.DisplayText1 = $"Downloading .NET {RequiredDotNetMajorVersion} Desktop Runtime...";
                    this.DisplayText2 = "This is a one-time download";
                    this.IsOperationIndeterminate = true;
                    this.OperationProgress = 0;

                    this.WriteToLogFile($"Downloading .NET runtime (attempt {attempt + 1}): {DotNetDesktopRuntimeDownloadUrl}");

                    if (File.Exists(this.downloadedRuntimePath))
                    {
                        File.Delete(this.downloadedRuntimePath);
                    }

                    using (HttpResponseMessage response = await HttpClient.GetAsync(DotNetDesktopRuntimeDownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            string body = await response.Content.ReadAsStringAsync();
                            this.WriteToLogFile($"Runtime download failed (attempt {attempt + 1}): {(int)response.StatusCode} {response.ReasonPhrase}{Environment.NewLine}{body}");
                        }
                        else
                        {
                            long? contentLength = response.Content.Headers.ContentLength;

                            using (Stream httpStream = await response.Content.ReadAsStreamAsync())
                            using (FileStream fileStream = new FileStream(this.downloadedRuntimePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                            {
                                byte[] buffer = new byte[81920];
                                long totalRead = 0;
                                int bytesRead;
                                while ((bytesRead = await httpStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                {
                                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                                    totalRead += bytesRead;

                                    if (contentLength.HasValue && contentLength.Value > 0)
                                    {
                                        int progress = (int)Math.Min(100, Math.Floor((totalRead / (double)contentLength.Value) * 100));
                                        this.OperationProgress = progress;
                                        this.IsOperationIndeterminate = false;
                                    }
                                }
                            }

                            this.OperationProgress = 100;
                            this.IsOperationIndeterminate = false;
                            this.DisplayText1 = "Download complete.";
                            this.DisplayText2 = string.Empty;
                            this.WriteToLogFile(".NET runtime downloaded successfully: " + this.downloadedRuntimePath);
                            return true;
                        }
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is IOException || ex is TaskCanceledException)
                {
                    this.WriteToLogFile($"Runtime download error (attempt {attempt + 1}): {ex}");
                }

                if (attempt < DownloadRetryDelays.Length)
                {
                    await Task.Delay(DownloadRetryDelays[attempt]);
                }
            }

            this.WriteToLogFile($".NET runtime download failed after {maxAttempts} attempts.");
            this.ShowNetworkRetryError($"We were unable to download the .NET {RequiredDotNetMajorVersion} Desktop Runtime. Please check your connection and try again, or download it manually from {DotNetManualInstallUrl}");
            return false;
        }

        private void CleanupRuntimeDownload()
        {
            if (!string.IsNullOrEmpty(this.downloadedRuntimePath) && File.Exists(this.downloadedRuntimePath))
            {
                try
                {
                    File.Delete(this.downloadedRuntimePath);
                }
                catch (Exception ex)
                {
                    this.WriteToLogFile("Failed to delete runtime installer: " + ex);
                }
                finally
                {
                    this.downloadedRuntimePath = null;
                }
            }

            if (!string.IsNullOrEmpty(this.tempDirectoryPath) && Directory.Exists(this.tempDirectoryPath))
            {
                try
                {
                    if (Directory.GetFileSystemEntries(this.tempDirectoryPath).Length == 0)
                    {
                        Directory.Delete(this.tempDirectoryPath);
                    }
                }
                catch (Exception ex)
                {
                    this.WriteToLogFile("Failed to remove runtime temp directory: " + ex);
                }
            }
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
                    if (clsProcess.ProcessName.Equals(MixItUpProcessName) || clsProcess.ProcessName.Equals(AutoHosterProcessName))
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

        private async Task<MixItUpUpdateModel> FetchManifestAsync()
        {
            string channel = (this.IsPreview || this.IsTest) ? "preview" : "public";
            string url = string.Format("{0}/{1}/latest", FileServiceBaseUrl, channel);

            this.manifestChannel = channel;
            this.manifestUrl = url;

            this.WriteToLogFile("Requesting update manifest: " + url);

            int maxAttempts = ManifestRetryDelays.Length + 1;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        request.Headers.Accept.Clear();
                        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                        using (HttpResponseMessage response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                string json = await response.Content.ReadAsStringAsync();
                                if (string.IsNullOrEmpty(json))
                                {
                                    this.WriteToLogFile("Manifest response empty");
                                }
                                else
                                {
                                    try
                                    {
                                        MixItUpUpdateModel update = JsonConvert.DeserializeObject<MixItUpUpdateModel>(json);
                                        if (update != null)
                                        {
                                            this.latestUpdate = update;
                                            return update;
                                        }

                                        this.WriteToLogFile("Manifest deserialized to null");
                                    }
                                    catch (JsonException jex)
                                    {
                                        this.WriteToLogFile("Manifest parse error: " + jex);
                                        return null;
                                    }
                                }
                            }
                            else
                            {
                                string body = await response.Content.ReadAsStringAsync();
                                this.WriteToLogFile(string.Format("Manifest request failed (attempt {0}): {1} {2}{3}{4}", attempt + 1, (int)response.StatusCode, response.ReasonPhrase, Environment.NewLine, body));
                            }
                        }
                    }
                }
                catch (HttpRequestException hre)
                {
                    this.WriteToLogFile(string.Format("Manifest request error (attempt {0}): {1}", attempt + 1, hre));
                }
                catch (TaskCanceledException tce)
                {
                    this.WriteToLogFile(string.Format("Manifest request timeout (attempt {0}): {1}", attempt + 1, tce));
                }

                if (attempt < ManifestRetryDelays.Length)
                {
                    await Task.Delay(ManifestRetryDelays[attempt]);
                }
            }

            return null;
        }

        private async Task<bool> DownloadPackageAsync(MixItUpUpdateModel update)
        {
            if (string.IsNullOrEmpty(this.packageUrl))
            {
                this.WriteToLogFile("Package URL missing from manifest; cannot download.");
                this.ShowError("Invalid update manifest.", "The update manifest did not include a package download link.");
                return false;
            }

            bool encounteredChecksumMismatch = false;
            string lastExpectedHash = null;
            string lastActualHash = null;

            this.tempDirectoryPath = Path.Combine(this.installDirectory, TempDirectoryName);
            Directory.CreateDirectory(this.tempDirectoryPath);

            string versionSegment = string.IsNullOrEmpty(update?.Version) ? "latest" : update.Version;
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                versionSegment = versionSegment.Replace(invalidChar, '-');
            }

            string filePath = Path.Combine(this.tempDirectoryPath, string.Format("MixItUp-{0}.zip", versionSegment));
            int maxAttempts = DownloadRetryDelays.Length + 1;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    this.DisplayText1 = "Downloading update package...";
                    this.DisplayText2 = string.Empty;
                    this.IsOperationIndeterminate = true;
                    this.OperationProgress = 0;

                    this.WriteToLogFile(string.Format("Downloading package (attempt {0}): {1}", attempt + 1, this.packageUrl));

                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

                    using (HttpResponseMessage response = await HttpClient.GetAsync(this.packageUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            string body = await response.Content.ReadAsStringAsync();
                            this.WriteToLogFile(string.Format("Package download failed (attempt {0}): {1} {2}{3}{4}", attempt + 1, (int)response.StatusCode, response.ReasonPhrase, Environment.NewLine, body));
                        }
                        else
                        {
                            long? contentLength = response.Content.Headers.ContentLength;

                            using (Stream httpStream = await response.Content.ReadAsStreamAsync())
                            using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                            {
                                byte[] buffer = new byte[81920];
                                long totalRead = 0;
                                int bytesRead;
                                while ((bytesRead = await httpStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                {
                                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                                    totalRead += bytesRead;

                                    if (contentLength.HasValue && contentLength.Value > 0)
                                    {
                                        int progress = (int)Math.Min(100, Math.Floor((totalRead / (double)contentLength.Value) * 100));
                                        this.OperationProgress = progress;
                                        this.IsOperationIndeterminate = false;
                                    }
                                }
                            }

                            bool checksumMismatchThisAttempt = false;

                            this.DisplayText1 = "Verifying download package...";

                            string expectedHash = string.IsNullOrWhiteSpace(this.expectedSha256) ? null : this.expectedSha256.Trim();
                            if (!string.IsNullOrEmpty(expectedHash))
                            {
                                string actualHash = ComputeSha256(filePath);

                                if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                                {
                                    encounteredChecksumMismatch = true;
                                    checksumMismatchThisAttempt = true;
                                    lastExpectedHash = expectedHash;
                                    lastActualHash = actualHash;

                                    this.WriteToLogFile(string.Format("Package checksum mismatch (attempt {0}): expected {1}, actual {2}", attempt + 1, expectedHash, actualHash));

                                    if (File.Exists(filePath))
                                    {
                                        File.Delete(filePath);
                                    }

                                    this.downloadedPackagePath = null;
                                    this.OperationProgress = 0;
                                    this.DisplayText1 = "Checksum mismatch detected.";
                                    this.DisplayText2 = "Retrying download...";
                                }
                            }
                            else
                            {
                                this.WriteToLogFile("No expected SHA-256 provided; skipping verification.");
                            }

                            if (!checksumMismatchThisAttempt)
                            {
                                this.downloadedPackagePath = filePath;
                                this.OperationProgress = 100;
                                this.IsOperationIndeterminate = false;
                                this.DisplayText1 = "Download complete.";
                                this.DisplayText2 = string.Empty;
                                this.WriteToLogFile("Package downloaded successfully: " + filePath);
                                return true;
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is IOException || ex is TaskCanceledException)
                {
                    this.WriteToLogFile(string.Format("Package download error (attempt {0}): {1}", attempt + 1, ex));
                }
                catch (Exception ex)
                {
                    this.WriteToLogFile(string.Format("Unexpected package download error (attempt {0}): {1}", attempt + 1, ex));
                    break;
                }

                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (Exception cleanupEx)
                    {
                        this.WriteToLogFile("Failed to delete incomplete download: " + cleanupEx);
                    }
                }

                if (attempt < DownloadRetryDelays.Length)
                {
                    await Task.Delay(DownloadRetryDelays[attempt]);
                }
            }

            this.downloadedPackagePath = null;

            if (encounteredChecksumMismatch)
            {
                this.WriteToLogFile(string.Format("Package download failed after {0} attempts due to checksum mismatches.", maxAttempts));
                this.ShowChecksumMismatchError(lastExpectedHash ?? this.expectedSha256 ?? "<unknown>", lastActualHash ?? "<missing>");
            }
            else
            {
                this.WriteToLogFile(string.Format("Package download failed after {0} attempts due to network errors.", maxAttempts));
                this.ShowNetworkRetryError("We were unable to download the Mix It Up update after multiple attempts. Please check your connection and try again.");
            }

            return false;
        }

        private bool InstallMixItUp()
        {
            this.DisplayText1 = "Installing files...";
            this.IsOperationIndeterminate = false;
            this.OperationProgress = 0;
            this.DisplayText2 = string.Empty;

            try
            {
                if (string.IsNullOrEmpty(this.downloadedPackagePath) || !File.Exists(this.downloadedPackagePath))
                {
                    this.SpecificErrorMessage = "We were unable to locate the downloaded update package. Please try again.";
                    this.WriteToLogFile("Package not found: " + (this.downloadedPackagePath ?? "<null>"));
                    return false;
                }

                Directory.CreateDirectory(this.installDirectory);
                if (!Directory.Exists(this.installDirectory))
                {
                    this.SpecificErrorMessage = "We were unable to prepare the Mix It Up installation directory.";
                    return false;
                }

                using (FileStream packageStream = File.OpenRead(this.downloadedPackagePath))
                using (ZipArchive archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: false))
                {
                    double current = 0;
                    double total = archive.Entries.Count;
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string fullName = entry.FullName;
                        if (entry.FullName.StartsWith("Mix It Up/", StringComparison.Ordinal))
                        {
                            fullName = entry.FullName.Substring("Mix It Up/".Length);
                        }

                        string filePath = Path.Combine(this.installDirectory, fullName);
                        string directoryPath = Path.GetDirectoryName(filePath);
                        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                        {
                            Directory.CreateDirectory(directoryPath);
                        }

                        if (Path.HasExtension(filePath))
                        {
                            entry.ExtractToFile(filePath, overwrite: true);
                        }

                        current++;
                        if (total > 0)
                        {
                            this.OperationProgress = (int)((current / total) * 100);
                        }
                    }
                }

                this.OperationProgress = 100;
                this.CleanupTemporaryDownload();
                return true;
            }
            catch (UnauthorizedAccessException uaex)
            {
                this.SpecificErrorMessage = "We were unable to update due to a file lock issue. Please try rebooting your PC and then running the update. You can also download and re-run our installer to update your installation.";
                this.WriteToLogFile(uaex.ToString());
            }
            catch (IOException ioex)
            {
                this.SpecificErrorMessage = "We were unable to update due to a file lock issue. Please try rebooting your PC and then running the update. You can also download and re-run our installer to update your installation.";
                this.WriteToLogFile(ioex.ToString());
            }
            catch (Exception ex)
            {
                this.WriteToLogFile(ex.ToString());
            }
            return false;
        }

        private async Task<bool> EnsureEulaAcceptedAsync(MixItUpUpdateModel update)
        {
            if (update == null || string.IsNullOrEmpty(update.EulaVersion) || string.IsNullOrEmpty(update.Eula))
            {
                this.WriteToLogFile("EULA not required for this update.");
                return true;
            }

            string eulaAcceptedPath = Path.Combine(this.installDirectory, EulaAcceptedFileName);
            try
            {
                if (File.Exists(eulaAcceptedPath))
                {
                    string[] lines = File.ReadAllLines(eulaAcceptedPath);
                    if (lines.Length > 0)
                    {
                        string recordedVersion = (lines[0] ?? string.Empty).Trim();
                        if (!string.IsNullOrEmpty(recordedVersion) && string.Equals(recordedVersion, update.EulaVersion, StringComparison.OrdinalIgnoreCase))
                        {
                            this.WriteToLogFile("EULA already accepted for version " + recordedVersion);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.WriteToLogFile("Failed to read EULA acceptance file: " + ex);
            }

            if (this.ShowEulaDialogAsync == null)
            {
                this.WriteToLogFile("EULA dialog delegate not provided; cannot display EULA.");
                this.ShowEulaDeclinedError();
                return false;
            }

            string eulaMarkdown;
            try
            {
                this.WriteToLogFile("Downloading EULA markdown: " + update.Eula);
                using (HttpResponseMessage response = await HttpClient.GetAsync(update.Eula, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        string body = await response.Content.ReadAsStringAsync();
                        this.WriteToLogFile(string.Format("Failed to download EULA: {0} {1}{2}{3}", (int)response.StatusCode, response.ReasonPhrase, Environment.NewLine, body));
                        this.ShowNetworkRetryError("We were unable to download the Mix It Up EULA. Please check your connection and try again.");
                        return false;
                    }

                    eulaMarkdown = await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is IOException || ex is TaskCanceledException)
            {
                this.WriteToLogFile("Network error downloading EULA: " + ex);
                this.ShowNetworkRetryError("We were unable to download the Mix It Up EULA. Please check your connection and try again.");
                return false;
            }

            this.DisplayText1 = "Awaiting EULA acceptance...";
            this.DisplayText2 = string.Empty;
            this.IsOperationIndeterminate = true;
            this.OperationProgress = 0;

            this.WriteToLogFile("Displaying EULA version " + update.EulaVersion);

            bool accepted;
            try
            {
                accepted = await this.ShowEulaDialogAsync(eulaMarkdown, update.Eula);
            }
            catch (Exception ex)
            {
                this.WriteToLogFile("EULA dialog threw an exception: " + ex);
                this.ShowEulaDeclinedError();
                return false;
            }

            if (!accepted)
            {
                this.WriteToLogFile("User declined EULA version " + update.EulaVersion);
                this.ShowEulaDeclinedError();
                return false;
            }

            this.WriteToLogFile("User accepted EULA version " + update.EulaVersion);
            this.PersistEulaAcceptance(update.EulaVersion);
            return true;
        }

        private static string ComputeSha256(string filePath)
        {
            using (FileStream stream = File.OpenRead(filePath))
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    builder.AppendFormat("{0:x2}", b);
                }
                return builder.ToString();
            }
        }

        private static string SanitizeInstallDirectoryArgument(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                return null;
            }

            string candidate = argument.Trim().Trim('"');

            if (string.IsNullOrEmpty(candidate) || candidate.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                return null;
            }

            try
            {
                candidate = Path.GetFullPath(candidate);
                return candidate;
            }
            catch
            {
                return null;
            }
        }

        private static bool DoesDirectoryContainExistingInstall(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                if (!Directory.Exists(path))
                {
                    return false;
                }

                if (File.Exists(Path.Combine(path, "MixItUp.exe")))
                {
                    return true;
                }

                if (File.Exists(Path.Combine(path, "MixItUp.Base.dll")))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private void CleanupTemporaryDownload()
        {
            if (!string.IsNullOrEmpty(this.downloadedPackagePath) && File.Exists(this.downloadedPackagePath))
            {
                try
                {
                    File.Delete(this.downloadedPackagePath);
                }
                catch (Exception ex)
                {
                    this.WriteToLogFile("Failed to delete temporary package: " + ex);
                }
                finally
                {
                    this.downloadedPackagePath = null;
                }
            }

            if (!string.IsNullOrEmpty(this.tempDirectoryPath) && Directory.Exists(this.tempDirectoryPath))
            {
                try
                {
                    if (Directory.GetFileSystemEntries(this.tempDirectoryPath).Length == 0)
                    {
                        Directory.Delete(this.tempDirectoryPath);
                        this.tempDirectoryPath = null;
                    }
                }
                catch (Exception ex)
                {
                    this.WriteToLogFile("Failed to remove temporary directory: " + ex);
                }
            }
        }

        private void PersistEulaAcceptance(string eulaVersion)
        {
            string eulaAcceptedPath = Path.Combine(this.installDirectory, EulaAcceptedFileName);
            string tempPath = Path.Combine(this.installDirectory, EulaAcceptedFileName + ".tmp");
            string timestamp = DateTimeOffset.UtcNow.ToString("o");

            try
            {
                Directory.CreateDirectory(this.installDirectory);
                File.WriteAllLines(tempPath, new[] { eulaVersion ?? string.Empty, timestamp });

                if (File.Exists(eulaAcceptedPath))
                {
                    File.Replace(tempPath, eulaAcceptedPath, null);
                }
                else
                {
                    File.Move(tempPath, eulaAcceptedPath);
                }

                this.WriteToLogFile("Recorded EULA acceptance version " + (eulaVersion ?? "<unknown>") + " at " + timestamp);
            }
            catch (Exception ex)
            {
                this.WriteToLogFile("Failed to persist EULA acceptance: " + ex);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch (Exception cleanupEx)
                {
                    this.WriteToLogFile("Failed to remove temporary EULA acceptance file: " + cleanupEx);
                }
            }
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
                    string tempLinkFilePath = Path.Combine(DefaultInstallDirectory, "Mix It Up.link");
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
                                this.ShowError("We were unable to create the Start Menu shortcut.", "You can instead use the Desktop shortcut to launch Mix It Up");
                            }
                            else
                            {
                                this.ShowError("We were unable to create the Start Menu & Desktop shortcuts.", "Email support@mixitupapp.com to help diagnose this issue further.");
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

        private void ShowNetworkRetryError(string detailMessage = null)
        {
            string message = detailMessage ?? "Please check your network connection and try again. Details have been written to MixItUp-Installer-Log.txt.";
            this.SpecificErrorMessage = message;
            this.HyperlinkAddress = InstallerLogFileName;
            this.ShowError("Unable to reach the Mix It Up update service.", message);
        }

        private void ShowChecksumMismatchError(string expectedHash, string actualHash)
        {
            this.SpecificErrorMessage = "The downloaded update file failed verification and was removed. Please try again.";
            this.HyperlinkAddress = InstallerLogFileName;
            this.ShowError("Update verification failed.", this.SpecificErrorMessage);
        }

        private void ShowEulaDeclinedError()
        {
            this.SpecificErrorMessage = "You must accept the Mix It Up End User License Agreement to continue with the installation.";
            this.ShowError("EULA acceptance required.", this.SpecificErrorMessage);
        }

        private void ShowError(string message1, string message2)
        {
            this.IsOperationBeingPerformed = false;
            this.ErrorOccurred = true;
            this.DisplayText1 = message1;
            this.DisplayText2 = message2;
        }

        private void WriteToLogFile(string text)
        {
            File.AppendAllText(InstallerLogFileName, text + Environment.NewLine + Environment.NewLine);
        }
    }
}