using MixItUp.Base.ViewModels;
using System.Windows;

namespace MixItUp.WPF.Windows.Overlay
{
    public partial class OverlayEditorPopoutWindow : Window
    {
        public OverlayEditorPopoutWindow(UIViewModelBase viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }
    }
}
