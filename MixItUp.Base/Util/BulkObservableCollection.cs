using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MixItUp.Base.Util
{
    public class BulkObservableCollection<T> : ObservableCollection<T>
    {
        private bool suppressNotification = false;

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!this.suppressNotification)
            {
                base.OnCollectionChanged(e);
            }
        }

        public void AddRange(IEnumerable<T> items)
        {
            if (items == null)
            {
                return;
            }

            this.suppressNotification = true;
            int count = 0;
            try
            {
                foreach (T item in items)
                {
                    Items.Add(item);
                    count++;
                }
            }
            finally
            {
                this.suppressNotification = false;
            }

            if (count > 0)
            {
                this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                this.OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
                this.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            }
        }
    }
}
