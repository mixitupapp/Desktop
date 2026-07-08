using MixItUp.Base.Model.Velora.Emotes;

namespace MixItUp.Base.ViewModel.Chat.Velora
{
    public class VeloraChatEmoteViewModel : ChatEmoteViewModelBase
    {
        public override string Provider { get { return "velora"; } }

        public VeloraChatEmoteViewModel(EmoteModel emote)
        {
            this.ID = emote.ID;
            this.Name = emote.BestCode;
            this.ImageURL = emote.BestImageUrl;
            this.IsAnimated = emote.Animated ?? false;
            this.AnimatedImageURL = emote.AnimatedUrl;
        }
    }
}
