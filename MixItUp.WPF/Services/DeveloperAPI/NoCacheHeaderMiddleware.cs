using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace MixItUp.WPF.Services.DeveloperAPI
{
    public class NoCacheHeaderMiddleware
    {
        private readonly RequestDelegate _next;
        private const string MethodOverrideHeader = "X-HTTP-Method-Override";
        private static readonly string Version = Assembly.GetEntryAssembly().GetName().Version.ToString();

        public NoCacheHeaderMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.Headers.Append("Cache-Control", "no-cache");
            context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Append("Access-Control-Allow-Headers", "Origin, X-Requested-With, Content-Type, Access-Control-Allow-Origin, X-HTTP-Method-Override");
            context.Response.Headers.Append("Access-Control-Allow-Methods", "POST, GET, OPTIONS, PUT, PATCH, DELETE");
            context.Response.Headers.Append("x-mixitup-version", Version);

            if (context.Request.Method.Equals("OPTIONS", StringComparison.InvariantCultureIgnoreCase))
            {
                context.Response.StatusCode = 200;
                return;
            }

            if (context.Request.Headers.ContainsKey(MethodOverrideHeader))
            {
                var methodOverride = context.Request.Headers[MethodOverrideHeader].FirstOrDefault();
                if (!string.IsNullOrEmpty(methodOverride))
                {
                    context.Request.Method = methodOverride;
                }
            }

            await _next(context);
        }
    }
}