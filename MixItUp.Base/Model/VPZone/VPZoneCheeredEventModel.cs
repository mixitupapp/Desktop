using MixItUp.Base.ViewModel.User;

namespace MixItUp.Base.Model.VPZone
{
    /// <summary>
    /// A Pixel cheer. One Pixel is one cent CAD of value to the streamer, and the amount is the raw
    /// Pixel count as VPZone reports it.
    /// </summary>
    public class VPZoneCheeredEventModel
    {
        public UserV2ViewModel User { get; set; }

        public int Amount { get; set; }

        public string Message { get; set; }

        public VPZoneCheeredEventModel(UserV2ViewModel user, int amount, string message = null)
        {
            this.User = user;
            this.Amount = amount;
            this.Message = message;
        }
    }
}
