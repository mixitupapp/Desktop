using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Twitch.Bits;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Twitch.New;
using MixItUp.Base.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.Commands
{
    public class TwitchCustomPowerUpCommandEditorWindowViewModel : CommandEditorWindowViewModelBase
    {
        public ObservableCollection<CustomPowerUpModel> CustomPowerUps { get; set; } = new ObservableCollection<CustomPowerUpModel>();

        public CustomPowerUpModel CustomPowerUp
        {
            get { return this.customPowerUp; }
            set
            {
                this.customPowerUp = value;
                this.NotifyPropertyChanged();

                this.Name = (this.customPowerUp != null) ? this.customPowerUp.title : string.Empty;
            }
        }
        private CustomPowerUpModel customPowerUp;

        private string existingCustomPowerUpID;

        public TwitchCustomPowerUpCommandEditorWindowViewModel(TwitchCustomPowerUpCommandModel existingCommand)
            : base(existingCommand)
        {
            this.existingCustomPowerUpID = existingCommand.CustomPowerUpID;
        }

        public TwitchCustomPowerUpCommandEditorWindowViewModel() : base(CommandTypeEnum.TwitchCustomPowerUp) { }

        public override Task<Result> Validate()
        {
            if (this.CustomPowerUp == null)
            {
                return Task.FromResult(new Result(MixItUp.Base.Resources.CustomPowerUpMissing));
            }

            return Task.FromResult(new Result());
        }

        public override Dictionary<string, string> GetTestSpecialIdentifiers() { return TwitchCustomPowerUpCommandModel.GetCustomPowerUpTestSpecialIdentifiers(); }

        public override Task<CommandModelBase> CreateNewCommand() { return Task.FromResult<CommandModelBase>(new TwitchCustomPowerUpCommandModel(this.CustomPowerUp.title, this.CustomPowerUp.id)); }

        public override async Task UpdateExistingCommand(CommandModelBase command)
        {
            await base.UpdateExistingCommand(command);
            ((TwitchCustomPowerUpCommandModel)command).CustomPowerUpID = this.CustomPowerUp.id;
        }

        public override Task SaveCommandToSettings(CommandModelBase command)
        {
            ServiceManager.Get<CommandService>().TwitchCustomPowerUpCommands.Remove((TwitchCustomPowerUpCommandModel)this.existingCommand);
            ServiceManager.Get<CommandService>().TwitchCustomPowerUpCommands.Add((TwitchCustomPowerUpCommandModel)command);
            return Task.CompletedTask;
        }

        protected override async Task OnOpenInternal()
        {
            if (ServiceManager.Get<TwitchSession>().IsConnected)
            {
                IEnumerable<CustomPowerUpModel> powerUps = await ServiceManager.Get<TwitchSession>().StreamerService.GetCustomPowerUps(ServiceManager.Get<TwitchSession>().StreamerModel);
                if (powerUps != null && powerUps.Count() > 0)
                {
                    foreach (CustomPowerUpModel powerUp in powerUps.OrderBy(p => p.title))
                    {
                        this.CustomPowerUps.Add(powerUp);
                    }

                    if (!string.IsNullOrEmpty(this.existingCustomPowerUpID))
                    {
                        this.CustomPowerUp = this.CustomPowerUps.FirstOrDefault(p => string.Equals(p.id, this.existingCustomPowerUpID, StringComparison.OrdinalIgnoreCase));
                    }
                    else if (!string.IsNullOrEmpty(this.Name))
                    {
                        this.CustomPowerUp = this.CustomPowerUps.FirstOrDefault(p => p.title.Equals(this.Name));
                    }
                }
            }
            await base.OnOpenInternal();
        }
    }
}
