using MixItUp.Distribution.Core;
using Xunit;

namespace MixItUp.Distribution.Installer.Tests
{
    public sealed class DistributionClientTests
    {
        private const string BaseUrl = "https://files.mixitupapp.com";

        [Fact]
        public void BuildManifestUrl_UsesAppsRoute()
        {
            DistributionClient client = new DistributionClient(BaseUrl);

            string manifestUrl = client.BuildManifestUrl("mixitup-desktop", "windows-x64", "production");

            Assert.Equal(
                "https://files.mixitupapp.com/apps/mixitup-desktop/windows-x64/production/latest",
                manifestUrl
            );
        }

        [Theory]
        [InlineData("/download/mixitup-desktop/windows-x64/production/9.9.9.zip")]
        [InlineData("download/mixitup-desktop/windows-x64/production/9.9.9.zip")]
        public void BuildDownloadUri_ComposesDownloadRoute(string relativePath)
        {
            DistributionClient client = new DistributionClient(BaseUrl);

            System.Uri downloadUri = client.BuildDownloadUri(relativePath);

            Assert.NotNull(downloadUri);
            Assert.Equal("https", downloadUri.Scheme);
            Assert.Equal("files.mixitupapp.com", downloadUri.Host);
            Assert.Equal("/download/mixitup-desktop/windows-x64/production/9.9.9.zip", downloadUri.AbsolutePath);
        }

        [Fact]
        public void BuildDownloadUri_AllowsAbsoluteUrlPassthrough()
        {
            DistributionClient client = new DistributionClient(BaseUrl);

            System.Uri downloadUri = client.BuildDownloadUri("https://cdn.mixitupapp.com/custom.zip");

            Assert.NotNull(downloadUri);
            Assert.Equal("https://cdn.mixitupapp.com/custom.zip", downloadUri.AbsoluteUri);
        }
    }
}
