using MixItUp.Base.Model.Actions;
using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.Actions
{
    public class RahiTuberActionEditorControlViewModel : ActionEditorControlViewModelBase
    {
        public override ActionTypeEnum Type { get { return ActionTypeEnum.RahiTuber; } }

        public bool RahiTuberConnected { get { return ServiceManager.Get<RahiTuberService>().IsConnected; } }
        public bool RahiTuberNotConnected { get { return !this.RahiTuberConnected; } }

        public IEnumerable<RahiTuberActionTypeEnum> ActionTypes { get { return EnumHelper.GetEnumList<RahiTuberActionTypeEnum>(); } }

        public RahiTuberActionTypeEnum SelectedActionType
        {
            get { return this.selectedActionType; }
            set
            {
                this.selectedActionType = value;
                this.NotifyPropertyChanged();
            }
        }
        private RahiTuberActionTypeEnum selectedActionType;

        /// <summary>
        /// Typed rather than picked from a list. RahiTuber's HTTP integration has no way to ask it
        /// what states exist, so the name or index has to come from the user.
        /// </summary>
        public string State
        {
            get { return this.state; }
            set
            {
                this.state = value;
                this.NotifyPropertyChanged();
            }
        }
        private string state;

        public RahiTuberActionEditorControlViewModel(RahiTuberActionModel action)
            : base(action)
        {
            this.SelectedActionType = action.ActionType;
            this.State = action.State;
        }

        public RahiTuberActionEditorControlViewModel() : base() { }

        public override Task<Result> Validate()
        {
            if (string.IsNullOrWhiteSpace(this.State))
            {
                return Task.FromResult<Result>(new Result(Resources.RahiTuberActionMissingState));
            }
            return Task.FromResult<Result>(new Result());
        }

        protected override Task<ActionModelBase> GetActionInternal()
        {
            return Task.FromResult<ActionModelBase>(RahiTuberActionModel.CreateForState(this.SelectedActionType, this.State));
        }

        protected override async Task OnOpenInternal()
        {
            if (ChannelSession.Settings.RahiTuberEnabled && !this.RahiTuberConnected)
            {
                await ServiceManager.Get<RahiTuberService>().Connect();
            }

            this.NotifyPropertyChanged("RahiTuberConnected");
            this.NotifyPropertyChanged("RahiTuberNotConnected");

            await base.OnOpenInternal();
        }
    }
}
