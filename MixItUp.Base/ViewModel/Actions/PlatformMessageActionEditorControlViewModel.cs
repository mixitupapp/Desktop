using MixItUp.Base.Model;
using MixItUp.Base.Model.Actions;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.Actions
{
    public class PlatformMessageActionEditorControlViewModel : ActionEditorControlViewModelBase
    {
        public override ActionTypeEnum Type { get { return ActionTypeEnum.PlatformMessage; } }

        public string ChatText
        {
            get { return this.chatText; }
            set
            {
                this.chatText = value;
                this.NotifyPropertyChanged();
            }
        }
        private string chatText;

        public bool SendAsStreamer
        {
            get { return this.sendAsStreamer; }
            set
            {
                this.sendAsStreamer = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool sendAsStreamer = false;

        public ObservableCollection<PlatformOption> PlatformOptions { get; private set; } = new ObservableCollection<PlatformOption>();

        public PlatformOption SelectedPlatform
        {
            get { return this.selectedPlatform; }
            set
            {
                this.selectedPlatform = value;
                this.NotifyPropertyChanged();
            }
        }
        private PlatformOption selectedPlatform;

        public PlatformMessageActionEditorControlViewModel(PlatformMessageActionModel action)
            : base(action)
        {
            this.ChatText = action.ChatText;
            this.SendAsStreamer = action.SendAsStreamer;

            this.BuildPlatformOptions();
            this.SelectedPlatform = this.PlatformOptions.FirstOrDefault(p => p.Platform == action.Platform) ?? this.PlatformOptions.FirstOrDefault();
        }

        public PlatformMessageActionEditorControlViewModel()
            : base()
        {
            this.BuildPlatformOptions();
            this.SelectedPlatform = this.PlatformOptions.FirstOrDefault();
        }

        public override Task<Result> Validate()
        {
            if (string.IsNullOrEmpty(this.ChatText))
            {
                return Task.FromResult(new Result(MixItUp.Base.Resources.ChatActionMissingChatText));
            }

            return Task.FromResult(new Result());
        }

        protected override Task<ActionModelBase> GetActionInternal()
        {
            return Task.FromResult<ActionModelBase>(new PlatformMessageActionModel(this.ChatText, this.SelectedPlatform.Platform, this.SendAsStreamer));
        }

        // Every supported platform is listed rather than only the connected ones, so that a saved action
        // still shows its target after that platform is disconnected.
        private void BuildPlatformOptions()
        {
            foreach (StreamingPlatformTypeEnum platform in StreamingPlatforms.SupportedPlatforms)
            {
                this.PlatformOptions.Add(new PlatformOption
                {
                    Platform = platform,
                    Name = platform.ToString(),
                    LogoPath = StreamingPlatforms.GetPlatformSmallImage(platform)
                });
            }
        }
    }
}
