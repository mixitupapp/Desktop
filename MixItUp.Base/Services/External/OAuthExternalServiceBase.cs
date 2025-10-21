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
            LocalOAuthHttpListenerServer oauthServer = new LocalOAuthHttpListenerServer();
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
