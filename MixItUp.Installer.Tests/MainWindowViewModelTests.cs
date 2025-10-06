using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MixItUp.Installer;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MixItUp.Installer.Tests
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
            SetNonPublicProperty(viewModel, "PendingVersionDirectoryPath", versionDirectory);

            bool result = await InvokePrivateBoolTask(viewModel, "CopyUserDataAsync");

            Assert.True(result);

            string copiedFile = Path.Combine(targetDataPath, "subdir", "note.txt");
            Assert.True(File.Exists(copiedFile));
            Assert.Equal("legacy-note", File.ReadAllText(copiedFile));

            Assert.Equal("existing-json", File.ReadAllText(preExistingFile));
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
            SetNonPublicProperty(
                viewModel,
                "PendingVersionDirectoryPath",
                Path.Combine(versionRoot, latestVersion)
            );
            viewModel.LatestVersion = latestVersion;

            bool result = await InvokePrivateBoolTask(viewModel, "WriteOrUpdateBootloaderConfigAsync");

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

        private static async Task<bool> InvokePrivateBoolTask(
            MainWindowViewModel target,
            string methodName
        )
        {
            Type targetType = typeof(MainWindowViewModel);
            MethodInfo method = typeof(MainWindowViewModel).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            );
            if (method == null)
            {
                throw new InvalidOperationException($"Method '{methodName}' was not found.");
            }

            if (method.Invoke(target, null) is Task<bool> task)
            {
                return await task.ConfigureAwait(false);
            }

            throw new InvalidOperationException(
                $"Method '{methodName}' did not return a Task<bool>."
            );
        }

        private static void SetNonPublicProperty(
            MainWindowViewModel target,
            string propertyName,
            object value
        )
        {
            Type targetType = typeof(MainWindowViewModel);
            PropertyInfo property = typeof(MainWindowViewModel).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            );

            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Property '{propertyName}' was not found on type '{target.GetType().FullName}'."
                );
            }

            MethodInfo setter = property.GetSetMethod(nonPublic: true);
            if (setter == null)
            {
                throw new InvalidOperationException(
                    $"Property '{propertyName}' does not have a setter."
                );
            }

            setter.Invoke(target, new[] { value });
        }

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
