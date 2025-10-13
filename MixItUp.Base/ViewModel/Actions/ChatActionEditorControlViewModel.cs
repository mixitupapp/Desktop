using MixItUp.Base.Model;
using MixItUp.Base.Model.Actions;
using MixItUp.Base.Util;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.Actions
{
    public class PlatformOption
    {
        public StreamingPlatformTypeEnum Platform { get; set; }
        public string Name { get; set; }
        public string LogoPath { get; set; }
    }

    public class ChatActionEditorControlViewModel : ActionEditorControlViewModelBase
    {
        public override ActionTypeEnum Type { get { return ActionTypeEnum.Chat; } }

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

        public bool IsWhisper
        {
            get { return this.isWhisper; }
            set
            {
                this.isWhisper = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool isWhisper = false;

        public string WhisperUserName
        {
            get { return this.whisperUserName; }
            set
            {
                this.whisperUserName = value;
                this.NotifyPropertyChanged();
            }
        }
        private string whisperUserName;

        public ObservableCollection<PlatformOption> PlatformOptions { get; set; } = new ObservableCollection<PlatformOption>();
        private PlatformOption selectedPlatform;
        public PlatformOption SelectedPlatform
        {
            get => selectedPlatform;
            set
            {
                selectedPlatform = value;
                this.NotifyPropertyChanged();
            }
        }

        public ChatActionEditorControlViewModel(ChatActionModel action)
            : base(action)
        {
            this.ChatText = action.ChatText;
            this.SendAsStreamer = action.SendAsStreamer;
            this.IsWhisper = action.IsWhisper;
            this.WhisperUserName = action.WhisperUserName;
            this.UpdatePlatformOptions();
            this.SelectedPlatform = this.PlatformOptions.FirstOrDefault(p => p.Platform == action.Platform) ?? this.PlatformOptions.FirstOrDefault();
        }

        public ChatActionEditorControlViewModel() : base()
        {
            this.UpdatePlatformOptions();
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
            return Task.FromResult<ActionModelBase>(new ChatActionModel(this.ChatText, this.SendAsStreamer, this.IsWhisper, this.WhisperUserName, this.SelectedPlatform?.Platform ?? StreamingPlatformTypeEnum.All));
        }

        private void UpdatePlatformOptions()
        {
            PlatformOptions.Clear();

            PlatformOptions.Add(new PlatformOption
            {
                Platform = StreamingPlatformTypeEnum.All,
                Name = "All",
                LogoPath = null
            });

            foreach (StreamingPlatformTypeEnum platform in StreamingPlatforms.SupportedPlatforms)
            {
                if (StreamingPlatforms.IsPlatformConnected(platform))
                {
                    PlatformOptions.Add(new PlatformOption
                    {
                        Platform = platform,
                        Name = platform.ToString(),
                        LogoPath = StreamingPlatforms.GetPlatformSmallImage(platform)
                    });
                }
            }

            if (SelectedPlatform == null || !PlatformOptions.Contains(SelectedPlatform))
                SelectedPlatform = PlatformOptions.FirstOrDefault();
        }
    }
}