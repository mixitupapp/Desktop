using MixItUp.Base.Model.Commands;
using MixItUp.Base.ViewModel;
using MixItUp.Base.ViewModel.MainControls;
using MixItUp.WPF.Util;
using MixItUp.WPF.Windows.Commands;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MixItUp.WPF.Controls.MainControls
{
    /// <summary>
    /// Interaction logic for MusicPlayerControl.xaml
    /// </summary>
    public partial class MusicPlayerControl : MainControlBase
    {
        private MusicPlayerMainControlViewModel viewModel;

        private bool isScrubbing = false;

        public MusicPlayerControl()
        {
            InitializeComponent();
        }

        protected override async Task InitializeInternal()
        {
            this.DataContext = this.viewModel = new MusicPlayerMainControlViewModel((MainWindowViewModel)this.Window.ViewModel);

            GongSolutions.Wpf.DragDrop.DragDrop.SetIsDragSource(this.QueueListBox, true);
            GongSolutions.Wpf.DragDrop.DragDrop.SetIsDropTarget(this.QueueListBox, true);
            GongSolutions.Wpf.DragDrop.DragDrop.SetDropHandler(this.QueueListBox, MusicPlayerQueueDragDropHandler.Instance);

            this.TrackScrubber.PreviewMouseDown += TrackScrubber_PreviewMouseDown;
            this.TrackScrubber.PreviewMouseUp += TrackScrubber_PreviewMouseUp;
            this.viewModel.PropertyChanged += ViewModel_PropertyChanged;

            await this.viewModel.OnOpen();
            await base.InitializeInternal();
        }

        protected override async Task OnVisibilityChanged()
        {
            await this.viewModel.OnVisible();
        }

        private void OnSongChangedCommand_EditClicked(object sender, RoutedEventArgs e)
        {
            CommandEditorWindow window = CommandEditorWindow.GetCommandEditorWindow(FrameworkElementHelpers.GetDataContext<CustomCommandModel>(sender));
            window.CommandSaved += (object s, CommandModelBase command) => { this.viewModel.OnSongChangedCommand = command; };
            window.ForceShow();
        }

        private void TrackScrubber_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            this.isScrubbing = true;
        }

        private async void TrackScrubber_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (this.isScrubbing && this.viewModel != null)
            {
                await this.viewModel.SeekTo((int)this.TrackScrubber.Value);
            }
            this.isScrubbing = false;
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(MusicPlayerMainControlViewModel.TrackPosition)))
            {
                this.Dispatcher.Invoke(() =>
                {
                    if (!this.isScrubbing)
                    {
                        this.TrackScrubber.Value = this.viewModel.TrackPosition;
                    }
                });
            }
        }
    }
}
