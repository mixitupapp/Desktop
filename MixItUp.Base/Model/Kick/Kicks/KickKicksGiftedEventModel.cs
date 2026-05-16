using MixItUp.Base.ViewModel.User;

namespace MixItUp.Base.Model.Kick.Kicks
{
    public class KickKicksGiftedEventModel
    {
        public UserV2ViewModel User { get; set; }

        public int Amount { get; set; }

        public string Message { get; set; }

        public KickKicksGiftedEventModel(UserV2ViewModel user, int amount, string message = null)
        {
            User = user;
            Amount = amount;
            Message = message;
        }
    }
}
