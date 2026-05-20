using MixItUp.Base.Model.Actions;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Actions
{
    public class GroupActionEditorControlViewModel : ActionEditorControlViewModelBase
    {
        public ICommand ImportActionsCommand { get; private set; }
        public ICommand ExportActionsCommand { get; private set; }

        public ActionEditorListControlViewModel ActionEditorList
        {
            get { return this.actionEditorList; }
            set
            {
                this.actionEditorList = value;
                if (this.actionEditorList != null)
                {
                    this.actionEditorList.ParentAction = this;
                }
            }
        }
        private ActionEditorListControlViewModel actionEditorList = new ActionEditorListControlViewModel();

        public override ActionTypeEnum Type { get { return ActionTypeEnum.Group; } }

        private List<ActionModelBase> subActions = new List<ActionModelBase>();

        public GroupActionEditorControlViewModel(GroupActionModel action)
            : base(action)
        {
            this.InitializeActionEditorList();

            foreach (ActionModelBase subAction in action.Actions)
            {
                this.subActions.Add(subAction);
            }
        }

        public GroupActionEditorControlViewModel()
            : base()
        {
            this.InitializeActionEditorList();
        }

        protected override async Task OnOpenInternal()
        {
            this.ImportActionsCommand = this.CreateCommand(async () =>
            {
                try
                {
                    await this.ImportActionsFromCommand(await CommandEditorWindowViewModelBase.ImportCommandFromFile(CommandEditorWindowViewModelBase.OpenCommandFileBrowser()));
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                    await DialogHelper.ShowMessage(MixItUp.Base.Resources.FailedToImportCommand + ": " + ex.ToString());
                }
            });

            this.ExportActionsCommand = this.CreateCommand(async () =>
            {
                try
                {
                    IEnumerable<Result> results = await this.ActionEditorList.ValidateActions();
                    if (results.Any(r => !r.Success))
                    {
                        await DialogHelper.ShowFailedResults(results.Where(r => !r.Success));
                        return;
                    }

                    CustomCommandModel command = new CustomCommandModel(this.Name);
                    command.Actions.AddRange(await this.ActionEditorList.GetActions());

                    string fileName = ServiceManager.Get<IFileService>().ShowSaveFileDialog(this.Name + CommandEditorWindowViewModelBase.MixItUpCommandFileExtension, MixItUp.Base.Resources.MixItUpCommandFileFormatFilter);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        await FileSerializerHelper.SerializeToFile(fileName, command);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                    await DialogHelper.ShowMessage(MixItUp.Base.Resources.FailedToExportCommand + ": " + ex.ToString());
                }
            });

            await this.ActionEditorList.AddActions(subActions);
            subActions.Clear();

            await base.OnOpenInternal();
        }

        public override async Task<Result> Validate()
        {
            IEnumerable<Result> results = await this.ActionEditorList.ValidateActions();
            if (results.Any(r => !r.Success))
            {
                return new Result(results.Where(r => !r.Success));
            }
            return new Result();
        }

        public async Task ImportActionsFromCommand(CommandModelBase command)
        {
            if (command != null)
            {
                await this.ActionEditorList.AddActions(command.Actions);
            }
        }

        protected override async Task<ActionModelBase> GetActionInternal()
        {
            return new GroupActionModel(await this.ActionEditorList.GetActions());
        }

        private void InitializeActionEditorList()
        {
            if (this.ActionEditorList != null)
            {
                this.ActionEditorList.ParentAction = this;
            }
        }
    }
}
