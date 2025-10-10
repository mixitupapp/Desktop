using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MixItUp.Distribution.Installer;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MixItUp.Distribution.Installer.Tests
{
    public sealed class MainWindowViewModelTests : IDisposable
    {
        private readonly string tempRoot;

        public MainWindowViewModelTests()
        {
            this.tempRoot = Path.Combine(
                Path.GetTempPath(),
                "MixItUpInstallerTests_" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(this.tempRoot);
        }

        [Fact]
        public async Task DiscoverInstallContextAsync_NormalizesPathsAndDetectsPortableAsync()
        {
            string installRoot = Path.Combine(this.tempRoot, "InstallRoot");
            Directory.CreateDirectory(installRoot);

            string messyAppRoot = Path.Combine(installRoot, ".", "..", "InstallRoot")
                + Path.DirectorySeparatorChar;

            MainWindowViewModel viewModel = new MainWindowViewModel();
            viewModel.AppRoot = messyAppRoot;

            string portableExecutablePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "MixItUp.exe"
            );

            bool createdStubExe = false;
            if (!File.Exists(portableExecutablePath))
            {
                File.WriteAllText(portableExecutablePath, string.Empty);
                createdStubExe = true;
            }

            try
            {
                bool result = await viewModel.DiscoverInstallContextAsync();

                Assert.True(result);

                string expectedAppRoot = Path.GetFullPath(installRoot)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                Assert.Equal(expectedAppRoot, viewModel.AppRoot);
                Assert.Equal(
                    Path.Combine(expectedAppRoot, ".tmp"),
                    viewModel.DownloadTempPath
                );
                Assert.Equal(
                    Path.Combine(expectedAppRoot, "app"),
                    viewModel.VersionedAppDirRoot
                );
                Assert.True(viewModel.TargetDirExists);
                Assert.True(viewModel.PortableCandidateFound);
                Assert.False(viewModel.IsRunningFromAppRoot);
            }
            finally
            {
                if (createdStubExe)
                {
                    File.Delete(portableExecutablePath);
                }
            }
        }

        [Fact]
        public async Task DiscoverInstallContextAsync_RunningFromAppRootDisablesPortableFlag()
        {
            string runningDirectory = AppDomain.CurrentDomain.BaseDirectory;

            MainWindowViewModel viewModel = new MainWindowViewModel();
            viewModel.AppRoot = runningDirectory + Path.DirectorySeparatorChar;

            bool result = await viewModel.DiscoverInstallContextAsync();

            Assert.True(result);

            string expectedAppRoot = Path.GetFullPath(runningDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Assert.Equal(expectedAppRoot, viewModel.AppRoot);
            Assert.True(viewModel.IsRunningFromAppRoot);
            Assert.False(viewModel.PortableCandidateFound);
        }

        [Fact]
        public async Task CopyUserDataAsync_LegacyCopiesAllFilesAndSkipsExisting()
        {
            string versionRoot = Path.Combine(this.tempRoot, "app");
            string latestVersion = "9.9.9";
            string versionDirectory = Path.Combine(versionRoot, latestVersion);
            string legacyDataPath = Path.Combine(this.tempRoot, "legacy-data");

            Directory.CreateDirectory(Path.Combine(versionDirectory, "data"));
            Directory.CreateDirectory(legacyDataPath);
            Directory.CreateDirectory(Path.Combine(legacyDataPath, "subdir"));

            File.WriteAllText(Path.Combine(legacyDataPath, "ApplicationSettings.json"), "legacy-json");
            File.WriteAllText(Path.Combine(legacyDataPath, "subdir", "note.txt"), "legacy-note");

            string targetDataPath = Path.Combine(versionDirectory, "data");
            Directory.CreateDirectory(targetDataPath);
            string preExistingFile = Path.Combine(targetDataPath, "ApplicationSettings.json");
            File.WriteAllText(preExistingFile, "existing-json");

            MainWindowViewModel viewModel = new MainWindowViewModel();
            viewModel.AppRoot = this.tempRoot;
            viewModel.LatestVersion = latestVersion;
            viewModel.LegacyDetected = true;
            viewModel.LegacyDataPath = legacyDataPath;
            bool result = await viewModel.CopyUserDataAsync();

            Assert.True(result);

            string copiedFile = Path.Combine(targetDataPath, "subdir", "note.txt");
            Assert.True(File.Exists(copiedFile));
            Assert.Equal("legacy-note", File.ReadAllText(copiedFile));

            Assert.Equal("existing-json", File.ReadAllText(preExistingFile));
        }

        [Fact]
        public async Task CopyUserDataAsync_AllowListCopiesExpectedContent()
        {
            MainWindowViewModel viewModel = new MainWindowViewModel();
            viewModel.AppRoot = this.tempRoot;

            string versionRoot = Path.Combine(this.tempRoot, "app");
            Directory.CreateDirectory(versionRoot);

            string latestVersion = "2.0.0";
            string previousVersion = "1.0.0";

            string latestVersionDirectory = Path.Combine(versionRoot, latestVersion);
            string targetDataDirectory = Path.Combine(latestVersionDirectory, "data");
            Directory.CreateDirectory(targetDataDirectory);

            string previousVersionDirectory = Path.Combine(versionRoot, previousVersion);
            string previousDataDirectory = Path.Combine(previousVersionDirectory, "data");
            Directory.CreateDirectory(previousDataDirectory);

            Directory.CreateDirectory(Path.Combine(previousDataDirectory, "Settings"));
            File.WriteAllText(
                Path.Combine(previousDataDirectory, "Settings", "config.json"),
                "settings"
            );

            Directory.CreateDirectory(Path.Combine(previousDataDirectory, "Cache"));
            File.WriteAllText(
                Path.Combine(previousDataDirectory, "Cache", "cache.dat"),
                "cache"
            );

            File.WriteAllText(
                Path.Combine(previousDataDirectory, "ApplicationSettings.json"),
                "appsettings"
            );
            File.WriteAllText(
                Path.Combine(previousDataDirectory, "notes.txt"),
                "note"
            );

            Directory.SetLastWriteTimeUtc(previousVersionDirectory, DateTime.UtcNow.AddMinutes(-5));
            Directory.SetLastWriteTimeUtc(latestVersionDirectory, DateTime.UtcNow);

            viewModel.LegacyDetected = false;
            viewModel.PortableCandidateFound = false;
            viewModel.LatestVersion = latestVersion;
            bool result = await viewModel.CopyUserDataAsync();

            Assert.True(result);

            Assert.True(
                File.Exists(Path.Combine(targetDataDirectory, "Settings", "config.json"))
            );
            Assert.False(Directory.Exists(Path.Combine(targetDataDirectory, "Cache")));
            Assert.True(File.Exists(Path.Combine(targetDataDirectory, "ApplicationSettings.json")));
            Assert.False(File.Exists(Path.Combine(targetDataDirectory, "notes.txt")));
        }

        [Fact]
        public async Task WriteOrUpdateBootloaderConfigAsync_MergesExistingVersionsAndAppendsLatest()
        {
            MainWindowViewModel viewModel = new MainWindowViewModel();
            viewModel.AppRoot = this.tempRoot;

            string versionRoot = Path.Combine(this.tempRoot, "app");
            Directory.CreateDirectory(versionRoot);

            string previousVersion = "1.2.3";
            string latestVersion = "4.5.6";

            Directory.CreateDirectory(Path.Combine(versionRoot, previousVersion));
            Directory.CreateDirectory(Path.Combine(versionRoot, latestVersion));

            string bootloaderPath = viewModel.BootloaderConfigPath;
            File.WriteAllText(
                bootloaderPath,
                "{\n" +
                "  \"currentVersion\": \"1.2.3\",\n" +
                "  \"versionRoot\": \"app\",\n" +
                "  \"versions\": [\"1.0.0\", \"1.2.3\"],\n" +
                "  \"executables\": { \"windows\": \"MixItUp.exe\" },\n" +
                "  \"dataDirName\": \"data\"\n" +
                "}\n"
            );

            viewModel.InstalledVersion = previousVersion;
            viewModel.LatestVersion = latestVersion;

            bool result = await viewModel.WriteOrUpdateBootloaderConfigAsync();

            Assert.True(result);
            Assert.True(File.Exists(bootloaderPath));

            JObject config = JObject.Parse(File.ReadAllText(bootloaderPath));
            Assert.Equal(latestVersion, (string)config["currentVersion"]);
            Assert.Equal("app", (string)config["versionRoot"]);
            Assert.Equal("data", (string)config["dataDirName"]);
            Assert.Equal("MixItUp.exe", (string)config["executables"]["windows"]);

            var versions = config["versions"].Select(token => token.ToString()).ToList();
            Assert.Contains("1.0.0", versions);
            Assert.Contains(previousVersion, versions);
            Assert.Contains(latestVersion, versions);
            Assert.Equal(latestVersion, versions[versions.Count - 1]);
            Assert.Equal(
                versions.Count,
                versions.Distinct(StringComparer.OrdinalIgnoreCase).Count()
            );
        }

#pragma warning disable IL2026
#pragma warning disable IL2070
#pragma warning disable IL2072
#pragma warning disable IL2075

#pragma warning restore IL2075
#pragma warning restore IL2072
#pragma warning restore IL2070
#pragma warning restore IL2026

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(this.tempRoot))
                {
                    Directory.Delete(this.tempRoot, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup failures in tests.
            }
        }
    }
}
