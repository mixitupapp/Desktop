using MixItUp.Base.Model.Actions;
using MixItUp.Base.Util;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.Actions
{
    public class VeloraActionEditorControlViewModel : ActionEditorControlViewModelBase
    {
        public override ActionTypeEnum Type { get { return ActionTypeEnum.Velora; } }

        public IEnumerable<VeloraActionType> ActionTypes { get { return EnumHelper.GetEnumList<VeloraActionType>(); } }

        public VeloraActionType SelectedActionType
        {
            get { return this.selectedActionType; }
            set
            {
                this.selectedActionType = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(this.ShowTextGrid));
            }
        }
        private VeloraActionType selectedActionType;

        public bool ShowTextGrid { get { return this.SelectedActionType == VeloraActionType.SetTitle || this.SelectedActionType == VeloraActionType.SetGame; } }

        public string Text
        {
            get { return this.text; }
            set
            {
                this.text = value;
                this.NotifyPropertyChanged();
            }
        }
        private string text;

        public VeloraActionEditorControlViewModel(VeloraActionModel action)
            : base(action)
        {
            this.SelectedActionType = action.ActionType;

            if (this.ShowTextGrid)
            {
                this.Text = action.Text;
            }
        }

        public VeloraActionEditorControlViewModel() : base() { }

        public override async Task<Result> Validate()
        {
            if (this.ShowTextGrid)
            {
                if (string.IsNullOrEmpty(this.Text))
                {
                    return new Result(MixItUp.Base.Resources.ValidValueMustBeSpecified);
                }
            }
            return await base.Validate();
        }

        protected override Task<ActionModelBase> GetActionInternal()
        {
            if (this.ShowTextGrid)
            {
                return Task.FromResult<ActionModelBase>(VeloraActionModel.CreateTextAction(this.SelectedActionType, this.Text));
            }
            else
            {
                return Task.FromResult<ActionModelBase>(VeloraActionModel.CreateAction(this.SelectedActionType));
            }
        }
    }
}
