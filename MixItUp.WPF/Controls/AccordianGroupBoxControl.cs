using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MixItUp.WPF.Controls
{
    /// <summary>
    /// A GroupBox that collapses down to just its header when minimized. Its template lives in
    /// MixItUpTheme.Common.xaml as an implicit style; the control is deliberately code-only so it
    /// carries no XAML name scope, letting consuming XAML put x:Name on header/content children.
    /// </summary>
    public class AccordianGroupBoxControl : GroupBox
    {
        public event RoutedEventHandler Maximized;
        public event RoutedEventHandler Minimized;

        // Using a DependencyProperty as the backing store for IsMinimized.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsMinimizedProperty =
            DependencyProperty.Register("IsMinimized", typeof(bool), typeof(AccordianGroupBoxControl), new PropertyMetadata(false));

        public AccordianGroupBoxControl()
        {
            this.Loaded += AccordianGroupBoxControl_Loaded;
        }

        public bool IsMinimized
        {
            get { return (bool)GetValue(IsMinimizedProperty); }
            set { SetValue(IsMinimizedProperty, value); }
        }

        public bool IsUIMinimized { get { return this.IsMinimized; } }

        public void Minimize()
        {
            this.IsMinimized = true;
            this.Minimized?.Invoke(this, new RoutedEventArgs());
        }

        public void Maximize()
        {
            this.IsMinimized = false;
            this.Maximized?.Invoke(this, new RoutedEventArgs());
        }

        protected override void OnHeaderChanged(object oldHeader, object newHeader)
        {
            base.OnHeaderChanged(oldHeader, newHeader);
            FrameworkElement header = (FrameworkElement)newHeader;
            if (header != null)
            {
                header.MouseLeftButtonUp -= Header_MouseLeftButtonUp;
                header.MouseLeftButtonUp += Header_MouseLeftButtonUp;
            }
        }

        private void AccordianGroupBoxControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.IsMinimized)
            {
                this.Minimize();
            }
        }

        private void Header_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (this.IsMinimized)
            {
                this.Maximize();
            }
            else
            {
                this.Minimize();
            }
        }
    }
}
