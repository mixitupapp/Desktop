using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;

namespace MixItUp.Installer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainWindowViewModel viewModel;

        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = this.viewModel = new MainWindowViewModel();
            this.viewModel.ShowEulaDialogAsync = this.ShowEulaDialogAsync;

            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.viewModel.CheckCompatability())
            {
                if (await this.viewModel.Run())
                {
                    this.viewModel.Launch();
                    this.Close();
                }
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            string path = (e.Uri.IsAbsoluteUri) ? e.Uri.AbsoluteUri : e.Uri.OriginalString;
            ProcessStartInfo processInfo = new ProcessStartInfo(path)
            {
                UseShellExecute = true
            };
            Process.Start(processInfo);
            e.Handled = true;
        }

        private Task<bool> ShowEulaDialogAsync(string markdownContent, string eulaUrl)
        {
            TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool>();

            this.Dispatcher.Invoke(() =>
            {
                try
                {
                    EulaDialog dialog = new EulaDialog(markdownContent, eulaUrl)
                    {
                        Owner = this
                    };

                    bool? result = dialog.ShowDialog();
                    completionSource.TrySetResult(result.GetValueOrDefault(false));
                }
                catch (Exception ex)
                {
                    completionSource.TrySetException(ex);
                }
            });

            return completionSource.Task;
        }
    }
}
