using System;
using MixItUp.Distribution.Installer;
using Xunit;

namespace MixItUp.Distribution.Installer.Tests
{
    public class EnvironmentRequirementsTests
    {
        [Theory]
        [InlineData(10, 0)]
        [InlineData(10, 22000)]
        [InlineData(11, 0)]
        [InlineData(12, 0)]
        public void IsWindows10Or11_Win32WithModernVersion_ReturnsTrue(int major, int build)
        {
            OperatingSystem os = new OperatingSystem(PlatformID.Win32NT, new Version(major, build));

            bool result = EnvironmentRequirements.IsWindows10Or11(os);

            Assert.True(result);
        }

        [Theory]
        [InlineData(6, 1)]
        [InlineData(6, 3)]
        [InlineData(5, 0)]
        public void IsWindows10Or11_LegacyWindowsVersion_ReturnsFalse(int major, int build)
        {
            OperatingSystem os = new OperatingSystem(PlatformID.Win32NT, new Version(major, build));

            bool result = EnvironmentRequirements.IsWindows10Or11(os);

            Assert.False(result);
        }

        [Fact]
        public void IsWindows10Or11_NonWindowsPlatform_ReturnsFalse()
        {
            OperatingSystem os = new OperatingSystem(PlatformID.Unix, new Version(10, 0));

            bool result = EnvironmentRequirements.IsWindows10Or11(os);

            Assert.False(result);
        }

        [Fact]
        public void IsWindows10Or11_NullOperatingSystem_ReturnsFalse()
        {
            bool result = EnvironmentRequirements.IsWindows10Or11(null);

            Assert.False(result);
        }

        [Theory]
        [InlineData(true, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, true, true)]
        [InlineData(false, false, false)]
        public void Is64BitOS_Combinations_ReturnExpected(
            bool is64BitOperatingSystem,
            bool is64BitProcess,
            bool expected
        )
        {
            bool result = EnvironmentRequirements.Is64BitOS(
                is64BitOperatingSystem,
                is64BitProcess
            );

            Assert.Equal(expected, result);
        }
    }
}
