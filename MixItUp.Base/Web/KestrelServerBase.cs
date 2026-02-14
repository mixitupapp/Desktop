using MixItUp.Base.Util;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace MixItUp.Base.Web
{
    public abstract class KestrelServerBase
    {
        private WebApplication app;

        public async Task Start(string url)
        {
            if (this.app != null)
            {
                return;
            }

            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions()
            {
                Args = Array.Empty<string>(),
            });
            builder.WebHost.UseKestrel();
            builder.WebHost.UseUrls(url);
            builder.Logging.ClearProviders();
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                });
            });

            this.app = builder.Build();
            this.app.UseCors();
            this.app.UseWebSockets();
            this.app.Run(async context =>
            {
                await this.ProcessConnection(context);
            });
            await this.app.StartAsync();
        }

        public async Task Stop()
        {
            if (this.app != null)
            {
                try
                {
                    await this.StopInternal();
                    await this.app.StopAsync();
                    await this.app.DisposeAsync();
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }
            this.app = null;
        }

        protected virtual Task StopInternal() { return Task.CompletedTask; }

        protected abstract Task ProcessConnection(HttpContext context);
    }
}
