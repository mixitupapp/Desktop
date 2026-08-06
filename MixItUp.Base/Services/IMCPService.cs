using MixItUp.Base.Services.External;

namespace MixItUp.Base.Services
{
    public interface IMCPService : IExternalService
    {
        /// <summary>
        /// The address an MCP client should be pointed at. Surfaced so the Services page can show
        /// it without the view models needing to know the platform-specific hosting details.
        /// </summary>
        string ServerAddress { get; }
    }
}
