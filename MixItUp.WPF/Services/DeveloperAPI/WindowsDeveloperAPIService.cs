using MixItUp.Base;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MixItUp.WPF.Services.DeveloperAPI
{
    public class WindowsDeveloperAPIService : IDeveloperAPIService
    {
        private WebApplication app;

        public readonly string[] DeveloperAPIHttpListenerServerAddresses = new string[] { "http://localhost:8911", "http://127.0.0.1:8911" };
        public readonly string[] AdvancedDeveloperAPIHttpListenerServerAddresses = new string[] { "http://*:8911" };

        public string Name { get { return "Developer API"; } }

        public bool IsConnected { get; private set; }

        public async Task<Result> Connect()
        {
            await this.Disconnect();

            var builder = WebApplication.CreateBuilder();

            string[] urls;
            if (ChannelSession.IsElevated && ChannelSession.Settings.EnableDeveloperAPIAdvancedMode)
            {
                urls = AdvancedDeveloperAPIHttpListenerServerAddresses;
            }
            else
            {
                urls = DeveloperAPIHttpListenerServerAddresses;
            }

            builder.WebHost.UseUrls(urls);

            builder.Services.AddControllers()
                .AddApplicationPart(typeof(WindowsDeveloperAPIService).Assembly)
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = null;
                });

            this.app = builder.Build();

            app.UseRouting();
            app.UseMiddleware<NoCacheHeaderMiddleware>();
            app.MapControllers();

            _ = this.app.RunAsync();

            await Task.Delay(100);

            this.IsConnected = true;

            ServiceManager.Get<ITelemetryService>().TrackService("Developer API");

            return new Result();
        }

        public async Task Disconnect()
        {
            if (this.app != null)
            {
                await this.app.StopAsync();
                await this.app.DisposeAsync();
                this.app = null;
            }
            this.IsConnected = false;
        }
    }
}