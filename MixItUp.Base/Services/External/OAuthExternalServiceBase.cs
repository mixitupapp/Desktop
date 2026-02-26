using MixItUp.Base.Model.Web;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.External
{
    public interface IOAuthExternalService : IExternalService
    {
        Task<Result> Connect(OAuthTokenModel token);

        OAuthTokenModel GetOAuthTokenCopy();
    }

    public abstract class OAuthExternalServiceBase : OAuthRestServiceBase, IOAuthExternalService, IDisposable
    {
        public const string DEFAULT_OAUTH_LOCALHOST_URL = "http://localhost:8919/";
        public const string HTTPS_OAUTH_REDIRECT_URL = "https://mixitupapp.com/oauthredirect/";

        public const string DEFAULT_AUTHORIZATION_CODE_URL_PARAMETER = "code";

        public const string LoginRedirectPageHTML = @"<!DOCTYPE html>
                <html>
                <head>
                <meta charset=""utf-8"">
                <meta name=""viewport"" content=""width=device-width,initial-scale=1"">
                <title>Mix It Up - Logged In</title>
                <link rel=""shortcut icon"" href=""https://files.mixitupapp.com/static/branding/mixitup.ico"">
                <style>
                *{margin:0;padding:0;box-sizing:border-box}
                body{font-family:system-ui,-apple-system,sans-serif;background:#12053a;height:100vh;display:flex;flex-direction:column;align-items:center;justify-content:flex-end;overflow:hidden;position:relative}
                .bg{position:absolute;inset:0;background-size:cover;background-position:center;background-repeat:no-repeat;opacity:0;transition:opacity .6s ease}
                .bg.loaded{opacity:1}
                .loader{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;z-index:5;transition:opacity .4s ease}
                .loader.hidden{opacity:0;pointer-events:none}
                .spinner{width:36px;height:36px;border:3px solid rgba(255,255,255,.15);border-top-color:rgba(255,255,255,.7);border-radius:50%;animation:spin .8s linear infinite}
                @keyframes spin{to{transform:rotate(360deg)}}
                .content{position:relative;z-index:2;display:flex;flex-direction:column;align-items:center;margin-bottom:3rem;opacity:0;transform:translateY(12px);transition:opacity .6s ease .15s,transform .6s ease .15s}
                .content.visible{opacity:1;transform:translateY(0)}
                .card{background:rgba(0,0,0,.45);backdrop-filter:blur(24px);-webkit-backdrop-filter:blur(24px);border:1px solid rgba(255,255,255,.12);border-radius:14px;padding:1.75rem 2.25rem;text-align:center;box-shadow:0 8px 32px rgba(0,0,0,.4);max-width:420px;width:90%}
                .check{width:44px;height:44px;margin:0 auto 1rem;border-radius:50%;background:rgba(74,222,128,.15);display:flex;align-items:center;justify-content:center}
                .check svg{width:24px;height:24px}
                .subtitle{color:#fff;font-size:1.35rem;font-weight:600;margin-bottom:.4rem}
                .message{color:rgba(255,255,255,.7);font-size:.9rem;line-height:1.5}
                @media(max-width:640px){.card{padding:1.5rem 1.25rem}.subtitle{font-size:1.15rem}.content{margin-bottom:2rem}}
                </style>
                </head>
                <body>
                <div class=""bg"" id=""bg""></div>
                <div class=""loader"" id=""loader""><div class=""spinner""></div></div>
                <div class=""content"" id=""content"">
                <div class=""card"">
                <div class=""check""><svg viewBox=""0 0 24 24"" fill=""none"" stroke=""#4ade80"" stroke-width=""2.5"" stroke-linecap=""round"" stroke-linejoin=""round""><polyline points=""20 6 9 17 4 12""/></svg></div>
                <h2 class=""subtitle"">Logged In Successfully</h2>
                <p class=""message"">You may now close this page and return to Mix It Up.</p>
                </div>
                </div>
                <script>
                (function(){var done=false;function show(){if(done)return;done=true;document.getElementById('loader').classList.add('hidden');document.getElementById('content').classList.add('visible')}var img=new Image();img.onload=function(){document.getElementById('bg').style.backgroundImage='url('+img.src+')';document.getElementById('bg').classList.add('loaded');show()};img.onerror=function(){show()};setTimeout(show,5000);img.src='https://files.mixitupapp.com/static/branding/mixitup_wallpaper-color_1080.png'})();
                </script>
                </body>
                </html>";

        protected OAuthTokenModel token;

        protected string baseAddress;

        protected OAuthExternalServiceBase(string baseAddress) { this.baseAddress = baseAddress; }

        public abstract string Name { get; }

        public virtual bool IsConnected { get { return this.token != null; } }

        public abstract Task<Result> Connect();

        public virtual async Task<Result> Connect(OAuthTokenModel token)
        {
            try
            {
                this.token = token;
                await this.RefreshOAuthToken();

                Result result = await this.InitializeInternal();
                if (!result.Success)
                {
                    this.token = null;
                }
                return result;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return new Result(ex);
            }
        }

        public abstract Task Disconnect();

        public virtual OAuthTokenModel GetOAuthTokenCopy()
        {
            if (this.token != null)
            {
                return new OAuthTokenModel()
                {
                    clientID = this.token.clientID,
                    refreshToken = this.token.refreshToken,
                    accessToken = this.token.accessToken,
                    expiresIn = this.token.expiresIn
                };
            }
            return null;
        }

        protected abstract Task<Result> InitializeInternal();

        protected async Task<string> ConnectViaOAuthRedirect(string oauthPageURL, int secondsToWait = 30) { return await this.ConnectViaOAuthRedirect(oauthPageURL, OAuthExternalServiceBase.DEFAULT_OAUTH_LOCALHOST_URL, secondsToWait); }

        protected virtual async Task<string> ConnectViaOAuthRedirect(string oauthPageURL, string listeningAddress, int secondsToWait = 45)
        {
            LocalOAuthKestrelServer oauthServer = new LocalOAuthKestrelServer();
            return await oauthServer.GetAuthorizationCode(oauthPageURL, secondsToWait);
        }

        protected override string GetBaseAddress() { return this.baseAddress; }

        protected override async Task<OAuthTokenModel> GetOAuthToken(bool autoRefreshToken = true)
        {
            if (autoRefreshToken && this.token != null && this.token.ExpirationDateTime < DateTimeOffset.Now)
            {
                await this.RefreshOAuthToken();
            }
            return this.token;
        }

        protected async Task<OAuthTokenModel> GetWWWFormUrlEncodedOAuthToken(string endpoint, List<KeyValuePair<string, string>> bodyContent)
        {
            return await this.GetWWWFormUrlEncodedOAuthToken(endpoint, null, null, bodyContent);
        }

        protected async Task<OAuthTokenModel> GetWWWFormUrlEncodedOAuthToken(string endpoint, string clientID, string clientSecret, List<KeyValuePair<string, string>> bodyContent)
        {
            try
            {
                using (AdvancedHttpClient client = new AdvancedHttpClient())
                {
                    if (!string.IsNullOrEmpty(clientID) && !string.IsNullOrEmpty(clientSecret))
                    {
                        client.SetEncodedBasicAuthorization(clientID, clientSecret);
                    }

                    using (var content = new FormUrlEncodedContent(bodyContent))
                    {
                        content.Headers.Clear();
                        content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

                        HttpResponseMessage response = await client.PostAsync(endpoint, content);
                        return await response.ProcessResponse<OAuthTokenModel>();
                    }
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
            return null;
        }

        protected abstract Task RefreshOAuthToken();

        protected void TrackServiceTelemetry(string name) { ServiceManager.Get<ITelemetryService>().TrackService(name); }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void DisposeInternal() { }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects).
                    this.DisposeInternal();
                }

                // Free unmanaged resources (unmanaged objects) and override a finalizer below.
                // Set large fields to null.

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        #endregion
    }
}
