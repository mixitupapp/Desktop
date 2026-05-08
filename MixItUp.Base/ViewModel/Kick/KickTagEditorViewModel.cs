using MixItUp.Base.Services;
using MixItUp.Base.Services.Kick.New;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Kick
{
    public class KickTagViewModel : UIViewModelBase, IEquatable<KickTagViewModel>
    {
        public string Tag
        {
            get { return this.tag; }
            set
            {
                this.tag = value;
                this.NotifyPropertyChanged();
            }
        }
        private string tag;

        public ICommand DeleteTagCommand { get; private set; }

        public event EventHandler TagDeleted = delegate { };

        public KickTagViewModel(string tag)
        {
            this.Tag = tag;

            this.DeleteTagCommand = this.CreateCommand(() =>
            {
                this.TagDeleted.Invoke(this, new EventArgs());
            });
        }

        public bool Equals(KickTagViewModel other) { return this.Tag.Equals(other.Tag, StringComparison.Ordinal); }
    }

    public class KickTagEditorViewModel : UIViewModelBase
    {
        public string SelectedTag
        {
            get { return this.selectedTag; }
            set
            {
                this.selectedTag = value;
                this.NotifyPropertyChanged();
            }
        }
        private string selectedTag;

        public ObservableCollection<KickTagViewModel> CustomTags { get; private set; } = new ObservableCollection<KickTagViewModel>();

        public bool CanAddMoreTags { get { return this.CustomTags.Count < 10; } }

        public ICommand AddTagCommand { get; private set; }

        public Task AddCustomTag(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                KickTagViewModel tagVM = new KickTagViewModel(tag);
                if (!this.CustomTags.Contains(tagVM))
                {
                    this.CustomTags.Add(tagVM);
                    tagVM.TagDeleted += (sender, e) =>
                    {
                        this.RemoveCustomTag((KickTagViewModel)sender);
                    };
                    this.SelectedTag = null;
                }
            }
            this.NotifyPropertyChanged("CanAddMoreTags");
            return Task.CompletedTask;
        }

        public void ClearCustomTags()
        {
            this.CustomTags.Clear();
            this.NotifyPropertyChanged("CanAddMoreTags");
        }

        public async Task LoadCurrentTags()
        {
            if (ServiceManager.Get<KickSession>().IsConnected && ServiceManager.Get<KickSession>().Channel?.Stream?.CustomTags != null)
            {
                this.CustomTags.Clear();
                this.NotifyPropertyChanged("CanAddMoreTags");

                foreach (string tag in ServiceManager.Get<KickSession>().Channel.Stream.CustomTags)
                {
                    await this.AddCustomTag(tag);
                }
            }
        }

        protected override async Task OnOpenInternal()
        {
            this.AddTagCommand = this.CreateCommand(async () =>
            {
                await this.AddCustomTag(this.SelectedTag);
            });
            await base.OnOpenInternal();
        }

        private void RemoveCustomTag(KickTagViewModel tag)
        {
            this.CustomTags.Remove(tag);
            this.NotifyPropertyChanged("CanAddMoreTags");
        }
    }
}
