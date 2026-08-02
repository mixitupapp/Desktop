using MixItUp.Base.Model;
using MixItUp.Base.ViewModel.Settings.Generic;
using MixItUp.Base.ViewModels;

namespace MixItUp.Base.ViewModel.Settings
{
    public class PlatformsSettingsControlViewModel : UIViewModelBase
    {
        public string YouTubeLogo { get { return StreamingPlatforms.GetPlatformSmallImage(StreamingPlatformTypeEnum.YouTube); } }

        public GenericNumberSettingsOptionControlViewModel YouTubeShortsVideoLengthCap { get; set; }

        public PlatformsSettingsControlViewModel()
        {
            this.YouTubeShortsVideoLengthCap = new GenericNumberSettingsOptionControlViewModel(MixItUp.Base.Resources.ShortsVideoLengthCap,
                ChannelSession.Settings.YouTubeShortsVideoLengthCap,
                (value) => { ChannelSession.Settings.YouTubeShortsVideoLengthCap = value; },
                MixItUp.Base.Resources.ShortsVideoLengthCapTooltip);
            this.YouTubeShortsVideoLengthCap.ValueWidth = 100;
        }
    }
}
