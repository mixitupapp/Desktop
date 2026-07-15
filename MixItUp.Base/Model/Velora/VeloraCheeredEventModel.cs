using MixItUp.Base.ViewModel.User;

namespace MixItUp.Base.Model.Velora
{
    public class VeloraCheeredEventModel
    {
        public UserV2ViewModel User { get; set; }

        public int Amount { get; set; }

        public string Message { get; set; }

        public VeloraCheeredEventModel(UserV2ViewModel user, int amount, string message = null)
        {
            this.User = user;
            this.Amount = amount;
            this.Message = message;
        }
    }
}
