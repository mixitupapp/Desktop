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
            // Kestrel's own diagnostics are the only place a bind failure explains itself, so forward the
            // serious ones into the app log rather than dropping them along with the console providers.
            builder.Logging.AddProvider(new AppLoggerProvider());
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
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
                finally
                {
                    await this.app.DisposeAsync();
                    this.app = null;
                }
            }
        }

        protected virtual Task StopInternal() { return Task.CompletedTask; }

        protected abstract Task ProcessConnection(HttpContext context);

        private class AppLoggerProvider : ILoggerProvider
        {
            public ILogger CreateLogger(string categoryName) { return new AppLogger(categoryName); }

            public void Dispose() { }

            private class AppLogger : ILogger
            {
                private readonly string categoryName;

                public AppLogger(string categoryName) { this.categoryName = categoryName; }

                public IDisposable BeginScope<TState>(TState state) { return NullScope.Instance; }

                public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
                {
                    return logLevel >= Microsoft.Extensions.Logging.LogLevel.Warning;
                }

                public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                {
                    if (!this.IsEnabled(logLevel))
                    {
                        return;
                    }

                    try
                    {
                        string message = formatter != null ? formatter(state, exception) : state?.ToString();
                        if (exception != null)
                        {
                            message += Environment.NewLine + exception.ToString();
                        }

                        MixItUp.Base.Util.LogLevel level = logLevel >= Microsoft.Extensions.Logging.LogLevel.Error
                            ? MixItUp.Base.Util.LogLevel.Error
                            : MixItUp.Base.Util.LogLevel.Warning;
                        Logger.Log(level, $"{this.categoryName} - {message}");
                    }
                    catch (Exception) { }
                }
            }

            private class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new NullScope();

                public void Dispose() { }
            }
        }
    }
}
