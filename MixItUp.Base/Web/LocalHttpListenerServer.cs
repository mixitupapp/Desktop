using MixItUp.Base.Util;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace MixItUp.Base.Web
{
    /// <summary>
    /// An Http listening service for intercepting requests &amp; processing them.
    /// </summary>
    public abstract class LocalHttpListenerServer
    {
        private HttpListener httpListener;

        /// <summary>
        /// Creates a new instance of the LocalHttpListenerServer class with the specified address.
        /// </summary>
        public LocalHttpListenerServer() { }

        /// <summary>
        /// Starts listening for requests.
        /// </summary>
        /// <param name="address">The address to start from</param>
        /// <returns>Whether the listener started successfully</returns>
        public bool Start(string address)
        {
            try
            {
                this.httpListener = new HttpListener();
                this.httpListener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
                this.httpListener.Prefixes.Add(address);

                this.httpListener.Start();

                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        while (this.httpListener != null && this.httpListener.IsListening)
                        {
                            try
                            {
                                HttpListenerContext context = this.httpListener.GetContext();
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await this.ProcessConnection(context);
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Log(ex);
                                    }
                                    finally
                                    {
                                        try
                                        {
                                            context.Response.OutputStream.Close();
                                            context.Response.Close();
                                        }
                                        catch { }
                                    }
                                });
                            }
                            catch (HttpListenerException) { }
                            catch (Exception ex) { Logger.Log(ex); }
                        }
                    }
                    catch (Exception ex) { Logger.Log(ex); }

                    this.Stop();
                }, TaskCreationOptions.LongRunning);

                return true;
            }
            catch (Exception ex) { Logger.Log(ex); }

            return false;
        }

        /// <summary>
        /// Stops listening for requests.
        /// </summary>
        public void Stop()
        {
            try
            {
                if (this.httpListener != null)
                {
                    this.httpListener.Stop();
                }
            }
            catch (HttpListenerException) { }
            catch (Exception ex) { Logger.Log(ex); }
            this.httpListener = null;
        }

        /// <summary>
        /// Processes an http request.
        /// </summary>
        /// <param name="listenerContext">The context of the request</param>
        /// <returns>An awaitable task to process the request</returns>
        protected abstract Task ProcessConnection(HttpListenerContext listenerContext);

        /// <summary>
        /// Gets a parameter value of the request.
        /// </summary>
        /// <param name="listenerContext">The request context</param>
        /// <param name="parameter">The name of the parameter</param>
        /// <returns>The parameter value of the request</returns>
        protected string GetRequestParameter(HttpListenerContext listenerContext, string parameter)
        {
            var queryString = HttpUtility.ParseQueryString(listenerContext.Request.Url.Query);
            string value = queryString[parameter];

            if (value != null)
                return value;

            string fragment = listenerContext.Request.Url.Fragment;
            if (!string.IsNullOrEmpty(fragment) && fragment.StartsWith("#"))
            {
                var fragmentParams = HttpUtility.ParseQueryString(fragment.Substring(1));
                return fragmentParams[parameter];
            }

            return null;
        }

        /// <summary>
        /// Gets the content of the request. 
        /// </summary>
        /// <param name="listenerContext">The request context</param>
        /// <returns>The content of the request</returns>
        protected async Task<string> GetRequestContent(HttpListenerContext listenerContext)
        {
            string data = await new StreamReader(listenerContext.Request.InputStream, listenerContext.Request.ContentEncoding).ReadToEndAsync();
            return HttpUtility.UrlDecode(data);
        }

        /// <summary>
        /// Closes the connection of the request.
        /// </summary>
        /// <param name="listenerContext">The request context</param>
        /// <param name="statusCode">The status code to send</param>
        /// <param name="content">The text content to include</param>
        /// <returns></returns>
        protected async Task CloseConnection(HttpListenerContext listenerContext, HttpStatusCode statusCode, string content)
        {
            listenerContext.Response.Headers["Access-Control-Allow-Origin"] = "*";
            listenerContext.Response.StatusCode = (int)statusCode;
            listenerContext.Response.StatusDescription = statusCode.ToString();

            byte[] buffer = Encoding.UTF8.GetBytes(content);
            listenerContext.Response.ContentLength64 = buffer.Length;
            await listenerContext.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            await listenerContext.Response.OutputStream.FlushAsync();
        }
    }
}
