using MixItUp.Base.Model;
using MixItUp.Base.Model.VPZone.Webhooks;
using MixItUp.Base.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.VPZone.New
{
    /// <summary>
    /// The lifecycle event path for VPZone, which is the one thing the chat gateway does not cover.
    /// Almost everything that happens in a channel arrives on the socket, so this exists for the
    /// exceptions: subscription.cancelled is deliberately silent in chat and never appears there, and
    /// the stream and follow webhooks act as a backstop while the socket is down.
    ///
    /// VPZone posts these deliveries to the Mix It Up Desktop API, which verifies the signature and
    /// relays them down the webhook hub to this client, so nothing is exposed on the streamer's
    /// machine and the relay is the only inbound path.
    /// </summary>
    public class VPZoneEventSocketClient
    {
        private readonly VPZoneClient dispatch;
        private readonly VPZoneService streamerService;

        /// <summary>
        /// The events routed through the relay. The rest of VPZone's catalog is dropped here on
        /// purpose: the chat gateway already delivers it live, and consuming both would double up.
        /// </summary>
        private static readonly IReadOnlyList<string> RelayedEvents = new List<string>()
        {
            VPZoneWebhookEventTypes.SubscriptionCancelled,
            VPZoneWebhookEventTypes.StreamStarted,
            VPZoneWebhookEventTypes.StreamEnded,
        };

        /// <summary>The webhook registered for this session, kept so it can be removed on disconnect.</summary>
        public VPZoneWebhookModel RegisteredWebhook { get; private set; }

        public bool IsConnected { get; private set; }

        public VPZoneEventSocketClient(VPZoneClient dispatch, VPZoneService streamerService)
        {
            this.dispatch = dispatch;
            this.streamerService = streamerService;
        }

        /// <summary>
        /// Registers the relay callback with VPZone, reusing an existing registration when one already
        /// points at the same URL so a reconnect does not pile up duplicates.
        /// </summary>
        public async Task<Result> Connect()
        {
            try
            {
                string channelSlug = ServiceManager.Get<VPZoneSession>()?.ChannelSlug;
                string callbackURL = ServiceManager.Get<MixItUpService>()?.GetVPZoneWebhookCallbackURL(channelSlug);
                if (string.IsNullOrWhiteSpace(callbackURL))
                {
                    return new Result(Resources.VPZoneWebhookCallbackUnavailable);
                }

                IEnumerable<VPZoneWebhookModel> existing = await this.streamerService.GetWebhooks();
                VPZoneWebhookModel match = existing?.FirstOrDefault(w => string.Equals(w.Url, callbackURL, StringComparison.OrdinalIgnoreCase));
                if (match != null && match.Active && RelayedEvents.All(e => match.Events.Contains(e, StringComparer.OrdinalIgnoreCase)))
                {
                    this.RegisteredWebhook = match;
                    this.IsConnected = true;
                    return new Result();
                }

                // A stale registration (inactive, or missing an event added since it was made) is
                // replaced rather than edited, since VPZone has no webhook update endpoint.
                if (match != null)
                {
                    await this.streamerService.DeleteWebhook(match.ID);
                }

                VPZoneWebhookModel created = await this.streamerService.CreateWebhook(callbackURL, RelayedEvents);
                if (created == null || string.IsNullOrWhiteSpace(created.ID))
                {
                    return new Result(Resources.VPZoneWebhookRegistrationFailed);
                }

                // VPZone returns the signing secret only on creation, so it has to reach the relay
                // now or the relay can never verify a delivery.
                if (!string.IsNullOrWhiteSpace(created.Secret))
                {
                    if (!await ServiceManager.Get<MixItUpService>().RegisterVPZoneWebhookSecret(channelSlug, created.Secret))
                    {
                        Logger.Log(LogLevel.Error, "Failed to hand the VPZone webhook signing secret to the relay; lifecycle events will not be delivered.");
                    }
                }

                this.RegisteredWebhook = created;
                this.IsConnected = true;
                return new Result();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return new Result(ex);
            }
        }

        public async Task Disconnect()
        {
            this.IsConnected = false;

            try
            {
                if (this.RegisteredWebhook != null && !string.IsNullOrWhiteSpace(this.RegisteredWebhook.ID))
                {
                    await this.streamerService.DeleteWebhook(this.RegisteredWebhook.ID);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            this.RegisteredWebhook = null;
        }

        /// <summary>The registration is bound to the callback URL rather than to a token, so a token
        /// refresh needs nothing more than confirming the registration is still there.</summary>
        public async Task<Result> ReconnectWithFreshToken()
        {
            return await this.Connect();
        }

        /// <summary>
        /// Entry point for a delivery relayed down the webhook hub. The relay has already verified
        /// VPZone's signature, so this only has to route the payload.
        /// </summary>
        public async Task HandleRelayedEvent(string eventType, Newtonsoft.Json.Linq.JObject payload)
        {
            if (string.IsNullOrWhiteSpace(eventType) || payload == null)
            {
                return;
            }

            await this.dispatch.HandleWebhookEvent(eventType, payload);
        }
    }
}
