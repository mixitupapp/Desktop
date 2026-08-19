using MixItUp.Base.Model.VPZone.Emotes;

namespace MixItUp.Base.ViewModel.Chat.VPZone
{
    public class VPZoneChatEmoteViewModel : ChatEmoteViewModelBase
    {
        public override string Provider { get { return "vpzone"; } }

        public VPZoneChatEmoteViewModel(VPZoneEmoteModel emote)
        {
            this.ID = emote.BestCode;
            this.Name = emote.BestCode;
            this.ImageURL = emote.BestImageUrl;
            this.IsAnimated = emote.IsAnimated;
            this.AnimatedImageURL = emote.BestAnimatedUrl;
        }

        public VPZoneChatEmoteViewModel(string code, string imageURL)
        {
            this.ID = code;
            this.Name = code;
            this.ImageURL = imageURL;
        }
    }
}
