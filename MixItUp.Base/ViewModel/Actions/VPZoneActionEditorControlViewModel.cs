using MixItUp.Base.Model.Actions;
using MixItUp.Base.Util;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.Actions
{
    public class VPZoneActionEditorControlViewModel : ActionEditorControlViewModelBase
    {
        public override ActionTypeEnum Type { get { return ActionTypeEnum.VPZone; } }

        /// <summary>
        /// Action types offered in the editor. Four are held back rather than removed, so commands that
        /// already reference them keep loading. Each execution path stays complete and starts working
        /// the moment its filter comes off.
        ///
        /// Granting channel points: VPZone accepts only a channel-bound API key on points/grant and
        /// answers an OAuth token with 403 unsupported_auth, and there is nowhere in Mix It Up for a
        /// streamer to supply such a key yet. Drop the filter once a grant key has somewhere to live.
        ///
        /// Pinning and unpinning: the only pin routes VPZone has authenticate off the website's own
        /// session cookie and answer an OAuth token with a flat 401, so neither can succeed from an
        /// app. Drop the filter once pinning reaches the v1 API.
        ///
        /// Announcing: the one announcement route on the platform answers every call with a 500, on a
        /// check constraint its own insert violates. Nothing can post an announcement today, VPZone's
        /// website included. Drop the filter once that endpoint returns a 201.
        /// </summary>
        public IEnumerable<VPZoneActionType> ActionTypes
        {
            get
            {
                return EnumHelper.GetEnumList<VPZoneActionType>().Where(t => t != VPZoneActionType.GrantChannelPoints &&
                    t != VPZoneActionType.PinMessage && t != VPZoneActionType.UnpinMessage &&
                    t != VPZoneActionType.Announce);
            }
        }

        public VPZoneActionType SelectedActionType
        {
            get { return this.selectedActionType; }
            set
            {
                this.selectedActionType = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(this.ShowText));
                this.NotifyPropertyChanged(nameof(this.TextHint));
                this.NotifyPropertyChanged(nameof(this.ShowTargetUser));
                this.NotifyPropertyChanged(nameof(this.ShowAmount));
                this.NotifyPropertyChanged(nameof(this.AmountHint));
                this.NotifyPropertyChanged(nameof(this.ShowReason));
            }
        }
        private VPZoneActionType selectedActionType;

        public string Text
        {
            get { return this.text; }
            set { this.text = value; this.NotifyPropertyChanged(); }
        }
        private string text;

        public string TargetUsername
        {
            get { return this.targetUsername; }
            set { this.targetUsername = value; this.NotifyPropertyChanged(); }
        }
        private string targetUsername;

        public string Amount
        {
            get { return this.amount; }
            set { this.amount = value; this.NotifyPropertyChanged(); }
        }
        private string amount;

        public string Reason
        {
            get { return this.reason; }
            set { this.reason = value; this.NotifyPropertyChanged(); }
        }
        private string reason;

        // Field visibility per action type.
        public bool ShowText
        {
            get
            {
                return this.SelectedActionType == VPZoneActionType.SetTitle
                    || this.SelectedActionType == VPZoneActionType.SetGame
                    || this.SelectedActionType == VPZoneActionType.SetTags
                    || this.SelectedActionType == VPZoneActionType.Announce
                    || this.SelectedActionType == VPZoneActionType.PinMessage;
            }
        }

        public bool ShowTargetUser
        {
            get
            {
                switch (this.SelectedActionType)
                {
                    case VPZoneActionType.BanUser:
                    case VPZoneActionType.UnbanUser:
                    case VPZoneActionType.TimeoutUser:
                    case VPZoneActionType.UntimeoutUser:
                    case VPZoneActionType.GrantChannelPoints:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public bool ShowAmount
        {
            get
            {
                return this.SelectedActionType == VPZoneActionType.TimeoutUser
                    || this.SelectedActionType == VPZoneActionType.GrantChannelPoints;
            }
        }

        public bool ShowReason
        {
            get { return this.SelectedActionType == VPZoneActionType.BanUser || this.SelectedActionType == VPZoneActionType.TimeoutUser; }
        }

        // Context-sensitive hints for the shared text and amount fields.
        public string TextHint
        {
            get
            {
                switch (this.SelectedActionType)
                {
                    case VPZoneActionType.SetTitle: return MixItUp.Base.Resources.Title;
                    case VPZoneActionType.SetGame: return MixItUp.Base.Resources.Game;
                    case VPZoneActionType.SetTags: return MixItUp.Base.Resources.Tags;
                    case VPZoneActionType.Announce: return MixItUp.Base.Resources.Message;
                    case VPZoneActionType.PinMessage: return MixItUp.Base.Resources.Message;
                    default: return MixItUp.Base.Resources.Name;
                }
            }
        }

        public string AmountHint
        {
            get { return this.SelectedActionType == VPZoneActionType.TimeoutUser ? MixItUp.Base.Resources.Duration : MixItUp.Base.Resources.Amount; }
        }

        public VPZoneActionEditorControlViewModel(VPZoneActionModel action)
            : base(action)
        {
            this.SelectedActionType = action.ActionType;
            this.Text = action.Text;
            this.TargetUsername = action.TargetUsername;
            this.Amount = action.Amount;
            this.Reason = action.Reason;
        }

        public VPZoneActionEditorControlViewModel() : base() { }

        public override async Task<Result> Validate()
        {
            if (this.ShowText && string.IsNullOrEmpty(this.Text))
            {
                return new Result(MixItUp.Base.Resources.ValidValueMustBeSpecified);
            }

            if (this.SelectedActionType == VPZoneActionType.GrantChannelPoints && string.IsNullOrEmpty(this.Amount))
            {
                return new Result(MixItUp.Base.Resources.ValidValueMustBeSpecified);
            }

            return await base.Validate();
        }

        protected override Task<ActionModelBase> GetActionInternal()
        {
            VPZoneActionModel action = VPZoneActionModel.CreateAction(this.SelectedActionType);
            action.Text = this.Text;
            action.TargetUsername = this.TargetUsername;
            action.Amount = this.Amount;
            action.Reason = this.Reason;
            return Task.FromResult<ActionModelBase>(action);
        }
    }
}
