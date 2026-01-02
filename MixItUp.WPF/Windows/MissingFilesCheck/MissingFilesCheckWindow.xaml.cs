using MixItUp.Base.ViewModel.MissingFilesCheck;
using System.Windows;

namespace MixItUp.WPF.Windows.MissingFilesCheck
{
    public partial class MissingFilesCheckWindow : Window
    {
        public MissingFilesCheckWindow()
        {
            InitializeComponent();

            this.DataContext = new MissingFilesCheckWindowViewModel();
        }
    }
}