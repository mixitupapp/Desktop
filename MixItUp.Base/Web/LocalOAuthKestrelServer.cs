using MixItUp.Base.Services;
using MixItUp.Base.Util;
using Microsoft.AspNetCore.Http;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Web
{
    public class LocalOAuthKestrelServer : KestrelServerBase
    {
        public const string REDIRECT_URL = "http://localhost:8919/";

        public const string AUTHORIZATION_CODE_URL_PARAMETER = "code";

        public const string LOGIN_REDIRECT_HTML = @"<!DOCTYPE html>
                <html>
                <head>
                <meta charset=""utf-8"">
                <meta name=""viewport"" content=""width=device-width,initial-scale=1"">
                <title>Mix It Up - Logged In</title>
                <link rel=""shortcut icon"" href=""https://files.mixitupapp.com/static/desktop/Mix-It-Up_Logo_Auth-Callback.png"">
                <style>
                *{margin:0;padding:0;box-sizing:border-box}
                body{font-family:system-ui,sans-serif;background:radial-gradient(circle at 125% 125%,#9b305e 0%,#12053a 85%);background-attachment:fixed;height:100vh;display:flex;flex-direction:column;align-items:center;justify-content:center;overflow:hidden;position:relative}
                #particles-js{position:absolute;width:100%;height:100%;z-index:1}
                .content{position:relative;z-index:2;display:flex;flex-direction:column;align-items:center}
                .logo{width:80px;height:80px;margin-bottom:1.5rem;opacity:0;animation:fadeIn .8s .2s forwards}
                .card{background:rgba(255,255,255,.1);backdrop-filter:blur(20px);border:1px solid rgba(255,255,255,.2);border-radius:16px;padding:2.5rem 2rem;text-align:center;box-shadow:0 8px 32px rgba(0,0,0,.3);max-width:400px;width:90%;opacity:0;animation:fadeIn .8s .4s forwards}
                .title{color:#fff;font-size:2rem;font-weight:600;margin-bottom:.5rem}
                .subtitle{color:#e2e8f0;font-size:1.25rem;margin-bottom:1.5rem}
                .message{color:#cbd5e1;font-size:.95rem;line-height:1.5}
                @keyframes fadeIn{to{opacity:1}}
                @media(max-width:640px){.card{padding:2rem 1.5rem}.title{font-size:1.75rem}.logo{width:60px;height:60px}}
                </style>
                </head>
                <body>
                <div id=""particles-js""></div>
                <div class=""content"">
                <div class=""card"">
                <img src=""https://files.mixitupapp.com/static/desktop/Mix-It-Up_Logo_Auth-Callback.png"" alt=""Mix It Up Logo"" class=""logo"">
                <h1 class=""title"">Mix It Up</h1>
                <h2 class=""subtitle"">Logged In Successfully</h2>
                <p class=""message"">You have been logged in successfully. You may now close this webpage.</p>
                </div>
                </div>
                <script src=""https://cdnjs.cloudflare.com/ajax/libs/particles.js/2.0.0/particles.min.js""></script>
                <script>
                particlesJS('particles-js',{particles:{number:{value:40,density:{enable:true,value_area:2000}},color:{value:'#dcb8f5'},shape:{type:'image',stroke:{width:0,color:'#000000'},polygon:{nb_sides:3},image:{src:'https://files.mixitupapp.com/static/desktop/Mix-It-Up_Logo_Auth-Callback.png',width:100,height:100}},opacity:{value:0.5,random:false,anim:{enable:false,speed:1,opacity_min:0.1,sync:false}},size:{value:10,random:true,anim:{enable:true,speed:5,size_min:5,sync:false}},line_linked:{enable:true,distance:155,color:'#b926cd',opacity:0.5,width:3},move:{enable:true,speed:2,direction:'top-left',random:false,straight:false,out_mode:'out',bounce:false,attract:{enable:true,rotateX:600,rotateY:1200}}},interactivity:{detect_on:'canvas',events:{onhover:{enable:true,mode:'repulse'},onclick:{enable:false,mode:'push'},resize:true},modes:{grab:{distance:400,line_linked:{opacity:1}},bubble:{distance:400,size:40,duration:2,opacity:8,speed:3},repulse:{distance:200,duration:0.4},push:{particles_nb:4},remove:{particles_nb:2}}},retina_detect:true});
                </script>
                </body>
                </html>";

        private string authorizationCode;

        public LocalOAuthKestrelServer() { }

        public async Task<string> GetAuthorizationCode(string authorizationURL, int secondsToWait)
        {
            try
            {
                await this.Start(REDIRECT_URL);

                ServiceManager.Get<IProcessService>().LaunchLink(authorizationURL);

                for (int i = 0; i < secondsToWait; i++)
                {
                    if (!string.IsNullOrEmpty(this.authorizationCode))
                    {
                        break;
                    }
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            await this.Stop();

            return this.authorizationCode;
        }

        public async Task<Result<string>> GetAuthorizationCode(string authorizationURL, CancellationToken cancellationToken)
        {
            try
            {
                await this.Start(REDIRECT_URL);

                ServiceManager.Get<IProcessService>().LaunchLink(authorizationURL);

                while (!cancellationToken.IsCancellationRequested && string.IsNullOrWhiteSpace(this.authorizationCode))
                {
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            await this.Stop();

            return new Result<string>(success: !string.IsNullOrWhiteSpace(this.authorizationCode), message: null)
            {
                Value = this.authorizationCode
            };
        }

        protected virtual void ProcessRequestParameters(HttpContext context) { }

        protected override async Task ProcessConnection(HttpContext context)
        {
            int statusCode = StatusCodes.Status500InternalServerError;
            string result = string.Empty;

            this.ProcessRequestParameters(context);

            string token = this.GetRequestParameter(context, AUTHORIZATION_CODE_URL_PARAMETER);
            if (!string.IsNullOrEmpty(token))
            {
                statusCode = StatusCodes.Status200OK;
                result = LOGIN_REDIRECT_HTML;

                this.authorizationCode = token;
            }

            await this.CloseConnection(context, statusCode, result);
        }

        protected string GetRequestParameter(HttpContext context, string parameter)
        {
            if (context.Request.Query.TryGetValue(parameter, out var value))
            {
                return value.ToString();
            }
            return null;
        }

        protected async Task CloseConnection(HttpContext context, int statusCode, string content)
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            context.Response.StatusCode = statusCode;

            byte[] buffer = Encoding.UTF8.GetBytes(content ?? string.Empty);
            context.Response.ContentLength = buffer.Length;
            await context.Response.Body.WriteAsync(buffer, 0, buffer.Length);
            await context.Response.Body.FlushAsync();
            await context.Response.CompleteAsync();
        }
    }
}
