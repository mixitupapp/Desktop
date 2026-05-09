using MixItUp.Base.Model.Requirements;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Requirements
{
    public class RequirementsSetViewModel : UIViewModelBase
    {
        public RoleRequirementViewModel Role { get; set; } = new RoleRequirementViewModel();

        public CooldownListRequirementViewModel Cooldown { get; set; } = new CooldownListRequirementViewModel();

        public CurrencyListRequirementViewModel Currency { get; set; } = new CurrencyListRequirementViewModel();

        public RankListRequirementViewModel Rank { get; set; } = new RankListRequirementViewModel();

        public InventoryListRequirementViewModel Inventory { get; set; } = new InventoryListRequirementViewModel();

        public ArgumentsRequirementViewModel Arguments { get; set; } = new ArgumentsRequirementViewModel();

        public ThresholdRequirementViewModel Threshold { get; set; } = new ThresholdRequirementViewModel();

        public SettingsRequirementViewModel Settings { get; set; } = new SettingsRequirementViewModel();

        public IEnumerable<RequirementViewModelBase> Requirements
        {
            get
            {
                List<RequirementViewModelBase> requirements = new List<RequirementViewModelBase>();
                requirements.Add(this.Role);
                requirements.AddRange(this.Currency.Items);
                requirements.AddRange(this.Rank.Items);
                requirements.AddRange(this.Inventory.Items);
                requirements.Add(this.Arguments);
                requirements.Add(this.Threshold);
                requirements.Add(this.Settings);
                return requirements;
            }
        }

        public ICommand HelpCommand { get; private set; }

        public RequirementsSetViewModel()
        {
            this.HelpCommand = this.CreateCommand(() =>
            {
                ServiceManager.Get<IProcessService>().LaunchLink("https://wiki.mixitupapp.com/usage-requirements");
            });
        }

        public RequirementsSetViewModel(RequirementsSetModel requirements)
            : this()
        {
            this.Cooldown = new CooldownListRequirementViewModel(includeDefault: false);

            if (requirements?.Requirements != null)
            {
                foreach (RequirementModelBase requirement in requirements.Requirements)
                {
                    if (requirement is RoleRequirementModel)
                    {
                        this.Role = new RoleRequirementViewModel((RoleRequirementModel)requirement);
                    }
                    else if (requirement is CooldownRequirementModel)
                    {
                        this.Cooldown.Add((CooldownRequirementModel)requirement);
                    }
                    else if (requirement is CurrencyRequirementModel)
                    {
                        this.Currency.Add((CurrencyRequirementModel)requirement);
                    }
                    else if (requirement is RankRequirementModel)
                    {
                        this.Rank.Add((RankRequirementModel)requirement);
                    }
                    else if (requirement is InventoryRequirementModel)
                    {
                        this.Inventory.Add((InventoryRequirementModel)requirement);
                    }
                    else if (requirement is ArgumentsRequirementModel)
                    {
                        this.Arguments = new ArgumentsRequirementViewModel((ArgumentsRequirementModel)requirement);
                    }
                    else if (requirement is ThresholdRequirementModel)
                    {
                        this.Threshold = new ThresholdRequirementViewModel((ThresholdRequirementModel)requirement);
                    }
                    else if (requirement is SettingsRequirementModel)
                    {
                        this.Settings = new SettingsRequirementViewModel((SettingsRequirementModel)requirement);
                    }
                }
            }
        }

        public async Task<IEnumerable<Result>> Validate()
        {
            List<Result> results = new List<Result>();
            results.Add(await this.Role.Validate());

            foreach (RequirementViewModelBase cooldown in this.Cooldown.Items)
            {
                results.Add(await cooldown.Validate());
            }

            foreach (RequirementViewModelBase requirement in this.Requirements.Where(r => r != this.Role))
            {
                results.Add(await requirement.Validate());
            }
            return results;
        }

        public RequirementsSetModel GetRequirements()
        {
            List<RequirementModelBase> requirements = new List<RequirementModelBase>();
            RequirementModelBase role = this.Role.GetRequirement();
            if (role != null)
            {
                requirements.Add(role);
            }

            requirements.AddRange(this.Cooldown.GetRequirements());

            foreach (RequirementViewModelBase requirement in this.Requirements.Where(r => r != this.Role))
            {
                RequirementModelBase req = requirement.GetRequirement();
                if (req != null)
                {
                    requirements.Add(req);
                }
            }
            return new RequirementsSetModel(requirements);
        }
    }
}
