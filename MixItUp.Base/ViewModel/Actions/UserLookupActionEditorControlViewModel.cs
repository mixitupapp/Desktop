using MixItUp.Base.Model;
using MixItUp.Base.Model.Actions;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.Actions
{
    public class UserLookupActionEditorControlViewModel : ActionEditorControlViewModelBase
    {
        public override ActionTypeEnum Type { get { return ActionTypeEnum.UserLookup; } }

        public string UsernameOrID
        {
            get { return this.usernameOrID; }
            set
            {
                this.usernameOrID = value;
                this.NotifyPropertyChanged();
            }
        }
        private string usernameOrID;

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

        public UserLookupActionEditorControlViewModel(UserLookupActionModel action)
            : base(action)
        {
            this.UsernameOrID = action.UsernameOrID;

            this.BuildPlatformOptions();
            this.SelectedPlatform = this.PlatformOptions.FirstOrDefault(p => p.Platform == action.Platform) ?? this.PlatformOptions.FirstOrDefault();
        }

        public UserLookupActionEditorControlViewModel()
            : base()
        {
            this.BuildPlatformOptions();
            this.SelectedPlatform = this.PlatformOptions.FirstOrDefault();
        }

        public override Task<Result> Validate()
        {
            if (string.IsNullOrEmpty(this.UsernameOrID))
            {
                return Task.FromResult(new Result(MixItUp.Base.Resources.UserLookupActionMissingUsernameOrID));
            }

            return Task.FromResult(new Result());
        }

        protected override Task<ActionModelBase> GetActionInternal()
        {
            return Task.FromResult<ActionModelBase>(new UserLookupActionModel(this.UsernameOrID, this.SelectedPlatform.Platform));
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
