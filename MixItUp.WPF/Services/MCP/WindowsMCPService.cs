using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.WPF.Services.MCP.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace MixItUp.WPF.Services.MCP
{
    /// <summary>
    /// Hosts the local MCP server that exposes Mix It Up to local AI agents. Kept deliberately
    /// separate from the Developer API: this is its own host, its own port, and its own tool
    /// surface, so neither feature constrains the other.
    /// </summary>
    public class WindowsMCPService : IMCPService
    {
        /// <summary>
        /// The Developer API owns 8911, so the MCP server sits alongside it on 8912. Bound to
        /// loopback only, with no advanced all-interfaces mode.
        /// </summary>
        public const int MCPServerPort = 8912;

        public const string MCPServerEndpoint = "/mcp";

        private WebApplication app;

        public string Name { get { return "MCP Server"; } }

        public string ServerAddress { get { return $"http://localhost:{MCPServerPort}{MCPServerEndpoint}"; } }

        public bool IsConnected { get; private set; }

        public async Task<Result> Connect()
        {
            await this.Disconnect();

            try
            {
                WebApplicationBuilder builder = WebApplication.CreateBuilder(Array.Empty<string>());

                builder.WebHost.UseUrls($"http://localhost:{MCPServerPort}");

                // Kestrel's own providers write to a console this app does not have, so route its
                // diagnostics into the app log the same way the other Kestrel hosts here do.
                builder.Logging.ClearProviders();

                builder.Services
                    .AddMcpServer(options =>
                    {
                        options.ServerInfo = new ModelContextProtocol.Protocol.Implementation()
                        {
                            Name = "mixitup",
                            Title = "Mix It Up",
                            Version = Assembly.GetEntryAssembly().GetName().Version.ToString(),
                        };
                        options.ServerInstructions = "Tools for inspecting and driving a running Mix It Up instance. Commands are identified by GUID: call list_commands to discover IDs before calling get_command, run_command, or set_command_state."
#if DEV_BRIDGE
                            + " This is a Dev build, so the ui_* dev bridge tools are also present."
                            + " Start with ui_screenshot: looking at the screen is usually cheaper and more direct than enumerating it, and it is enough to decide what to press."
                            + " To drive the app, read state with ui_get, then write it with ui_set or run a command with ui_invoke. Navigation here is a list selection, so setting a selector's SelectedIndex is how you change pages."
                            + " Fall back to the tree tools for what a picture cannot answer: ui_dump_tree for structure, ui_find to locate an element, ui_get_text to read what a window says. Prefer a targeted read over a dump, and address elements by #x:Name or type name, which needs no discovery pass at all."
                            + " Handles are per-process and do not survive an app restart. Branch on the 'status' field rather than the message text."
#endif
                            ;
                    })
                    // Set explicitly rather than taking the SDK default, which flipped to stateless
                    // in the 2026-07-28 protocol revision (SEP-2567). Stateless exists so a server can
                    // be load balanced without session affinity, which is worth nothing to a single
                    // local process, and it costs every server-to-client capability: the /sse endpoint
                    // is disabled and elicitation, sampling, and roots all become unavailable.
                    .WithHttpTransport(options => options.Stateless = false)
                    .WithTools<StatusTools>()
                    .WithTools<CommandTools>()
                    .WithTools<ChatTools>()
                    .WithTools<UserTools>()
#if DEV_BRIDGE
                    // The dev bridge grants arbitrary inspection of the running UI, so it is gated at
                    // compile time rather than behind a setting: in any other configuration these types
                    // do not exist in the assembly at all. Directory.Build.props defines DEV_BRIDGE only
                    // for the Dev configuration and fails the build if it is defined anywhere else.
                    .WithTools<DevBridge.UITools>()
                    .WithTools<DevBridge.ScreenshotTools>()
                    .WithTools<DevBridge.PropertyTools>()
                    .WithTools<DevBridge.InvokeTools>()
#endif
                    ;

                this.app = builder.Build();

                this.app.MapMcp(MCPServerEndpoint);

                await this.app.StartAsync();

                this.IsConnected = true;

                // ForceLog: the default log level is Warning, which would silently drop this.
                Logger.ForceLog(MixItUp.Base.Util.LogLevel.Information, $"MCP server started at {this.ServerAddress}");

                return new Result();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                await this.Disconnect();
                return new Result(ex.Message);
            }
        }

        public async Task Disconnect()
        {
            if (this.app != null)
            {
                try
                {
                    await this.app.StopAsync();
                    await this.app.DisposeAsync();
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
                this.app = null;
            }
            this.IsConnected = false;
        }
    }
}
