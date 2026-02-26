using ICSharpCode.AvalonEdit.Highlighting;
using MixItUp.Base.Model.Actions;
using MixItUp.Base.ViewModel.Actions;
using System.Windows;
using System.Windows.Controls;

namespace MixItUp.WPF.Controls.Actions
{
    /// <summary>
    /// Interaction logic for ScriptActionEditorControl.xaml
    /// </summary>
    public partial class ScriptActionEditorControl : ActionEditorControlBase
    {
        public ScriptActionEditorControl()
        {
            InitializeComponent();
            this.ScriptEditor.Options.HighlightCurrentLine = true;
            this.Loaded += this.ScriptActionEditorControl_Loaded;
        }

        private void ScriptActionEditorControl_Loaded(object sender, RoutedEventArgs e)
        {
            this.UpdateSyntaxHighlighting();
        }

        private void ActionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.UpdateSyntaxHighlighting();
        }

        private void UpdateSyntaxHighlighting()
        {
            ScriptActionType? actionType = null;
            if (this.DataContext is ScriptActionEditorControlViewModel viewModel)
            {
                actionType = viewModel.SelectedActionType;
            }
            else if (this.ActionTypeComboBox.SelectedItem is ScriptActionType selectedActionType)
            {
                actionType = selectedActionType;
            }

            if (!actionType.HasValue)
            {
                return;
            }

            string extension = ".cs";
            switch (actionType.Value)
            {
                case ScriptActionType.Python:
                    extension = ".py";
                    break;
                case ScriptActionType.Javascript:
                    extension = ".js";
                    break;
            }

            this.ScriptEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(extension);
        }
    }
}
