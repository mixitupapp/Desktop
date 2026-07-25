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
                        options.ServerInstructions = "Tools for inspecting and driving a running Mix It Up instance. Commands are identified by GUID: call list_commands to discover IDs before calling get_command, run_command, or set_command_state.";
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
                    .WithTools<UserTools>();

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
