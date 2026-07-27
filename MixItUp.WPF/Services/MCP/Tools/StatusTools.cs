using MixItUp.Base;
using MixItUp.Base.Model;
using MixItUp.Base.Util;
using ModelContextProtocol.Server;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace MixItUp.WPF.Services.MCP.Tools
{
    [McpServerToolType]
    public class StatusTools
    {
        [McpServerTool(Name = "get_status", ReadOnly = true, UseStructuredContent = true)]
        [Description("Get the running state of the Mix It Up instance: version, release channel, whether a settings profile is loaded, and which streaming platforms are currently connected. Call this first to confirm the app is ready before using other tools.")]
        public static Task<StatusResult> GetStatus()
        {
            return ToolHelpers.RunWithTimeout("get_status", () =>
            {
                StatusResult result = new StatusResult()
                {
                    Version = Assembly.GetEntryAssembly().GetName().Version.ToString(),
                    ReleaseChannel = BuildChannelHelper.GetReleaseChannel(),
                    IsSettingsLoaded = ChannelSession.Settings != null,
                    SettingsName = ChannelSession.Settings?.Name,
                };

                if (result.IsSettingsLoaded)
                {
                    result.ConnectedPlatforms = StreamingPlatforms.GetConnectedPlatforms().Select(p => p.ToString()).ToList();
                }

                return Task.FromResult(result);
            });
        }

        public class StatusResult
        {
            [Description("The application version, for example 1.8.10.0.")]
            public string Version { get; set; }

            [Description("The build channel the app was compiled under: debug, test, preview, or public.")]
            public string ReleaseChannel { get; set; }

            [Description("Whether a settings profile has finished loading. Other tools will fail until this is true.")]
            public bool IsSettingsLoaded { get; set; }

            [Description("The name of the loaded settings profile, or null if none is loaded.")]
            public string SettingsName { get; set; }

            [Description("Streaming platforms currently connected, for example Twitch or YouTube.")]
            public List<string> ConnectedPlatforms { get; set; } = new List<string>();
        }
    }
}
