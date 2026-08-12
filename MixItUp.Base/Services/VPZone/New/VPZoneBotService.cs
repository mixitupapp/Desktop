using System.Collections.Generic;

namespace MixItUp.Base.Services.VPZone.New
{
    /// <summary>
    /// The session's bot-side OAuth service for VPZone. VPZone has no bot entity: the chat gateway
    /// authenticates a connection by the token in its query string and has no send-as-someone-else
    /// flag, so a bot here is a second VPZone account that authorizes the app in its own right and
    /// holds its own socket. That makes this an ordinary streaming-platform service with the bot's
    /// narrower scope set, and the whole connect and disconnect flow comes from the base class.
    /// </summary>
    public class VPZoneBotService : VPZoneService
    {
        public override string Name { get { return "VPZone Bot"; } }

        public VPZoneBotService(IEnumerable<string> scopes)
            : base(scopes, isBotService: true) { }
    }
}
