using System;

namespace MixItUp.Base.ViewModel.Settings.Generic
{
    public class GenericNumberSettingsOptionControlViewModel : GenericSettingsOptionControlViewModelBase
    {
        public int Value
        {
            get { return this.value; }
            set
            {
                if (value >= 0)
                {
                    this.value = value;
                    this.valueSetter(value);
                }
                this.NotifyPropertyChanged();
            }
        }
        private int value;
        private Action<int> valueSetter;

        public bool Enabled
        {
            get { return this.enabled; }
            set
            {
                this.enabled = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool enabled = true;

        // Wide enough for the two or three digits most of these take. Options expecting larger
        // numbers can widen their own field without disturbing every other one.
        public int ValueWidth { get; set; } = 50;

        public GenericNumberSettingsOptionControlViewModel(string name, int initialValue, Action<int> valueSetter, string tooltip = null)
            : base(name, tooltip)
        {
            this.value = initialValue;
            this.valueSetter = valueSetter;
        }
    }
}
