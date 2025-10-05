using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.WPF.Services.DeveloperAPI
{
    public class NoCacheHeaderMiddleware
    {
        private const string MethodOverrideHeader = "X-HTTP-Method-Override";
        private readonly RequestDelegate _next;

        public NoCacheHeaderMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Headers.ContainsKey(MethodOverrideHeader))
            {
                var methodOverride = context.Request.Headers[MethodOverrideHeader].FirstOrDefault();
                if (!string.IsNullOrEmpty(methodOverride))
                {
                    context.Request.Method = methodOverride;
                }
            }

            if (context.Request.Method.Equals("OPTIONS", StringComparison.InvariantCultureIgnoreCase))
            {
                context.Response.StatusCode = 200;

                context.Response.Headers["Cache-Control"] = "no-cache";
                context.Response.Headers["Access-Control-Allow-Origin"] = "*";
                context.Response.Headers["Access-Control-Allow-Headers"] = "Origin, X-Requested-With, Content-Type, Access-Control-Allow-Origin";
                context.Response.Headers["Access-Control-Allow-Methods"] = "POST, GET, OPTIONS, PUT, PATCH, DELETE";

                return;
            }

            await _next(context);

            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            context.Response.Headers["Access-Control-Allow-Headers"] = "Origin, X-Requested-With, Content-Type, Access-Control-Allow-Origin";
            context.Response.Headers["Access-Control-Allow-Methods"] = "POST, GET, OPTIONS, PUT, PATCH, DELETE";
        }
    }
}