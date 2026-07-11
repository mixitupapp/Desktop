using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;

namespace MixItUp.WPF.Controls.Dialogs
{
    /// <summary>
    /// A Browse-button image picker dialog with client-side extension + size validation; Save stays
    /// disabled until a valid file is selected. The cancel button can be relabeled (e.g. "Skip") so the
    /// same dialog serves both optional setup steps and edit flows.
    /// </summary>
    public partial class ImageFileBrowserDialogControl : UserControl
    {
        private readonly HashSet<string> validExtensions;
        private readonly long maxFileSizeBytes;

        public ImageFileBrowserDialogControl(string description, IEnumerable<string> validExtensions, long maxFileSizeBytes, string cancelText = null)
        {
            this.validExtensions = new HashSet<string>(validExtensions ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            this.maxFileSizeBytes = maxFileSizeBytes;

            InitializeComponent();

            this.DescriptionTextBlock.Text = description;
            if (!string.IsNullOrEmpty(cancelText))
            {
                this.CancelButton.Content = cancelText;
            }
        }

        public string SelectedFilePath { get; private set; }

        private void BrowseButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            string extensionList = string.Join(";", this.validExtensions.OrderBy(ext => ext).Select(ext => "*" + ext));
            string filePath = ServiceManager.Get<IFileService>().ShowOpenFileDialog($"Image Files ({extensionList})|{extensionList}|All files (*.*)|*.*");
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            this.SelectedFilePath = null;
            this.SaveButton.IsEnabled = false;
            this.FilePathTextBox.Text = filePath;

            if (!this.validExtensions.Contains(Path.GetExtension(filePath)))
            {
                this.ShowError(string.Format(MixItUp.Base.Resources.ImageFileBrowserUnsupportedType, string.Join(", ", this.validExtensions.OrderBy(ext => ext))));
                return;
            }

            try
            {
                if (new FileInfo(filePath).Length > this.maxFileSizeBytes)
                {
                    this.ShowError(string.Format(MixItUp.Base.Resources.ImageFileBrowserFileTooLarge, this.maxFileSizeBytes / (1024 * 1024)));
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                this.ShowError(string.Format(MixItUp.Base.Resources.ImageFileBrowserUnsupportedType, string.Join(", ", this.validExtensions.OrderBy(ext => ext))));
                return;
            }

            this.ErrorTextBlock.Visibility = System.Windows.Visibility.Collapsed;
            this.SelectedFilePath = filePath;
            this.SaveButton.IsEnabled = true;
        }

        private void ShowError(string message)
        {
            this.ErrorTextBlock.Text = message;
            this.ErrorTextBlock.Visibility = System.Windows.Visibility.Visible;
        }
    }
}
