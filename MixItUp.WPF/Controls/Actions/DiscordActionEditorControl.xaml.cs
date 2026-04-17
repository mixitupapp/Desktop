using MixItUp.Base.Services;
using MixItUp.Base.ViewModel.Actions;
using System.Collections.Generic;

namespace MixItUp.WPF.Controls.Actions
{
    /// <summary>
    /// Interaction logic for DiscordActionEditorControl.xaml
    /// </summary>
    public partial class DiscordActionEditorControl : ActionEditorControlBase
    {
        public DiscordActionEditorControl()
        {
            InitializeComponent();
        }

        private void FilePathBrowseButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            IEnumerable<string> filePaths = ServiceManager.Get<IFileService>().ShowMultiselectOpenFileDialog("All files (*.*)|*.*");
            if (filePaths != null && this.DataContext is DiscordActionEditorControlViewModel)
            {
                ((DiscordActionEditorControlViewModel)this.DataContext).UploadFilePath = string.Join("|", filePaths);
            }
        }
    }
}
