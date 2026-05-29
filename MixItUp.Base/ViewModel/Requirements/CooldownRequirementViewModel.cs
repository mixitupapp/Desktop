using MixItUp.Base.Model.Requirements;
using MixItUp.Base.Util;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Requirements
{
    public class CooldownListRequirementViewModel : ListRequirementViewModelBase
    {
        public ObservableCollection<CooldownRequirementViewModel> Items { get; set; } = new ObservableCollection<CooldownRequirementViewModel>();

        public ICommand AddItemCommand { get; private set; }

        public CooldownListRequirementViewModel(bool includeDefault = false)
        {
            this.AddItemCommand = this.CreateCommand(() =>
            {
                this.Items.Add(new CooldownRequirementViewModel(this));
                this.NotifyDeleteStatesChanged();
            });

            if (includeDefault)
            {
                this.Items.Add(new CooldownRequirementViewModel(this));
            }
        }

        public void Add(CooldownRequirementModel requirement)
        {
            this.Items.Add(new CooldownRequirementViewModel(this, requirement));
            this.NotifyDeleteStatesChanged();
        }

        public void Delete(CooldownRequirementViewModel requirement)
        {
            this.Items.Remove(requirement);
            this.NotifyDeleteStatesChanged();
        }

        public IEnumerable<RequirementModelBase> GetRequirements()
        {
            List<RequirementModelBase> requirements = new List<RequirementModelBase>();
            foreach (CooldownRequirementViewModel item in this.Items)
            {
                requirements.Add(item.GetRequirement());
            }
            return requirements;
        }

        private void NotifyDeleteStatesChanged()
        {
            foreach (CooldownRequirementViewModel item in this.Items)
            {
                item.NotifyDeleteStateChanged();
            }
        }
    }

    public class CooldownRequirementViewModel : RequirementViewModelBase
    {
        public IEnumerable<CooldownTypeEnum> Types { get { return EnumHelper.GetEnumList<CooldownTypeEnum>(); } }

        public CooldownTypeEnum SelectedType
        {
            get { return this.selectedType; }
            set
            {
                this.selectedType = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged("IsGroupSelected");

                this.SelectedGroupName = null;
            }
        }
        private CooldownTypeEnum selectedType = CooldownTypeEnum.Standard;

        public bool IsGroupSelected { get { return this.SelectedType == CooldownTypeEnum.Group || this.SelectedType == CooldownTypeEnum.PerPersonGroup; } }

        public IEnumerable<string> GroupNames { get { return ChannelSession.Settings.CooldownGroupAmounts.Keys.ToList(); } }

        public string SelectedGroupName
        {
            get { return this.selectedGroupName; }
            set
            {
                this.selectedGroupName = value;
                this.NotifyPropertyChanged();

                if (!string.IsNullOrEmpty(this.SelectedGroupName) && ChannelSession.Settings.CooldownGroupAmounts.ContainsKey(this.SelectedGroupName))
                {
                    this.Amount = ChannelSession.Settings.CooldownGroupAmounts[this.SelectedGroupName];
                }
            }
        }
        private string selectedGroupName;

        public int Amount
        {
            get { return this.amount; }
            set
            {
                this.amount = value;
                this.NotifyPropertyChanged();
            }
        }
        private int amount;

        public bool CanDelete { get { return this.viewModel != null; } }

        public ICommand DeleteCommand { get; private set; }

        private CooldownListRequirementViewModel viewModel;

        public CooldownRequirementViewModel() : this((CooldownListRequirementViewModel)null) { }

        public CooldownRequirementViewModel(CooldownListRequirementViewModel viewModel)
        {
            this.viewModel = viewModel;

            this.DeleteCommand = this.CreateCommand(() =>
            {
                this.viewModel?.Delete(this);
            });
        }

        public CooldownRequirementViewModel(CooldownRequirementModel requirement) : this(null, requirement) { }

        public CooldownRequirementViewModel(CooldownListRequirementViewModel viewModel, CooldownRequirementModel requirement)
            : this(viewModel)
        {
            this.SelectedType = requirement.Type;
            if (requirement.IsGroup)
            {
                this.SelectedGroupName = requirement.GroupName;
            }
            else
            {
                this.Amount = requirement.Amount;
            }
        }

        public override Task<Result> Validate()
        {
            if (this.Amount < 0)
            {
                return Task.FromResult(new Result(MixItUp.Base.Resources.ValidCooldownAmountMustBeSpecified));
            }

            if ((this.SelectedType == CooldownTypeEnum.Group || this.SelectedType == CooldownTypeEnum.PerPersonGroup) && string.IsNullOrEmpty(this.SelectedGroupName))
            {
                return Task.FromResult(new Result(MixItUp.Base.Resources.ValidCooldownGroupMustBeSpecified));
            }

            return Task.FromResult(new Result());
        }

        public override RequirementModelBase GetRequirement()
        {
            if (this.SelectedType == CooldownTypeEnum.Group || this.SelectedType == CooldownTypeEnum.PerPersonGroup)
            {
                ChannelSession.Settings.CooldownGroupAmounts[this.SelectedGroupName] = this.Amount;
            }
            return new CooldownRequirementModel(this.SelectedType, this.Amount, this.SelectedGroupName);
        }

        public void NotifyDeleteStateChanged() { this.NotifyPropertyChanged("CanDelete"); }
    }
}
