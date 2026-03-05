using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Web
{
    /// <summary>
    /// Handles web socket communication for client connections.
    /// </summary>
    public abstract class ClientWebSocketBase : WebSocketBase
    {
        /// <summary>
        /// The web socket connection.
        /// </summary>
        protected new ClientWebSocket webSocket;

        /// <summary>
        /// Connects the web socket to the server.
        /// </summary>
        /// <param name="endpoint">The endpoint to connect to</param>
        /// <returns>Whether the connection was successful</returns>
        public virtual async Task<bool> Connect(string endpoint)
        {
            try
            {
                this.webSocket = this.CreateWebSocket();
                this.SetWebSocket(this.webSocket);

                await this.webSocket.ConnectAsync(new Uri(endpoint), CancellationToken.None);

                await Task.Delay(1000);

                this.Receive().Wait(1);

                return IsOpen();
            }
            catch (Exception ex)
            {
                await this.Disconnect();
                if (ex is WebSocketException websocketEx)
                {
                    if (websocketEx.InnerException is WebException webException &&
                        webException.Response is HttpWebResponse httpWebResponse)
                    {
                        using (StreamReader reader = new StreamReader(httpWebResponse.GetResponseStream()))
                        {
                            string responseString = reader.ReadToEnd();
                            throw new WebSocketException(string.Format("{0} - {1} - {2}", httpWebResponse.StatusCode, httpWebResponse.StatusDescription, responseString), ex);
                        }
                    }

                    if (websocketEx.InnerException is HttpRequestException httpRequestEx &&
                        httpRequestEx.StatusCode != null)
                    {
                        throw new WebSocketException(string.Format("{0} - {1}", (int)httpRequestEx.StatusCode.Value, httpRequestEx.Message), ex);
                    }
                }
                throw;
            }
        }

        /// <summary>
        /// Sends the packet of data.
        /// </summary>
        /// <param name="buffer">The buffer to send</param>
        /// <returns>An awaitable task</returns>
        protected override async Task SendInternal(byte[] buffer)
        {
            await this.webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        /// <summary>
        /// Creates the web socket for connecting with.
        /// </summary>
        /// <returns>The web socket to use</returns>
        protected virtual ClientWebSocket CreateWebSocket() { return new ClientWebSocket(); }
    }
}