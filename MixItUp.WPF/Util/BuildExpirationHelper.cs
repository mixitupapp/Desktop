using MixItUp.Base.Util;
using System;
using System.Threading.Tasks;

namespace MixItUp.WPF.Util
{
    public static class BuildExpirationHelper
    {
        private static readonly bool ENABLED = false;  // Set to true to enable build expiration checking

        private static readonly DateTime EXPIRATION_DATE = new DateTime(2026, 1, 1); // Set expiration date here

        public static async Task<bool> CheckBuildExpiration()
        {
            if (!ENABLED)
            {
                return false;
            }

            if (DateTime.Now > EXPIRATION_DATE)
            {
                Logger.ForceLog(LogLevel.Warning, $"Build expired on {EXPIRATION_DATE.ToShortDateString()}");

                await DialogHelper.ShowMessage(
                    $"This build expired on {EXPIRATION_DATE.ToShortDateString()}.\n\n" +
                    "Please download the latest version to continue using Mix It Up.");

                return true;
            }

            return false;
        }
    }
}
