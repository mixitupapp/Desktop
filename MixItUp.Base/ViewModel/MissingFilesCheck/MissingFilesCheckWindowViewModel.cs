using MixItUp.Base.Services;
using MixItUp.Base.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.IO;
using MixItUp.Base.Util;
using System.Threading.Tasks;
using MixItUp.Base.Model.Commands;
using MixItUp.Base;

namespace MixItUp.Base.ViewModel.MissingFilesCheck
{
    public class MissingFilesCheckItemViewModel : UIViewModelBase
    {
        public MissingFilesCheckReference Reference { get; private set; }

        public string CommandName => Reference.CommandName;
        public string ActionType => Reference.ActionTypeName;

        public string FilePath
        {
            get { return Reference.FilePath; }
            set
            {
                Reference.FilePath = value;
                Reference.UpdatePath(value);
                Reference.Validate();
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(Status));
                NotifyPropertyChanged(nameof(IsValid));
                _ = AsyncRunner.RunAsync(() => ChannelSession.Settings.SaveMissingFilesCheckCommand(Reference.Command));
            }
        }

        public MissingFilesCheckStatus Status => Reference.Status;
        public bool IsValid => Status == MissingFilesCheckStatus.Valid;

        public MissingFilesCheckItemViewModel(MissingFilesCheckReference reference)
        {
            Reference = reference;
        }
    }

    public class MissingFilesCheckWindowViewModel : UIViewModelBase
    {
        public ObservableCollection<MissingFilesCheckItemViewModel> AllItems { get; set; } = new ObservableCollection<MissingFilesCheckItemViewModel>();
        public ObservableCollection<MissingFilesCheckItemViewModel> FilteredItems { get; set; } = new ObservableCollection<MissingFilesCheckItemViewModel>();

        public MissingFilesCheckStatus? SelectedFilterStatus
        {
            get { return this.selectedFilterStatus; }
            set
            {
                this.selectedFilterStatus = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(SelectedFilterIndex));
                ApplyFilter();
            }
        }
        private MissingFilesCheckStatus? selectedFilterStatus = null;

        public int SelectedFilterIndex
        {
            get
            {
                if (SelectedFilterStatus == null) return 0;
                if (SelectedFilterStatus == MissingFilesCheckStatus.Valid) return 1;
                if (SelectedFilterStatus == MissingFilesCheckStatus.Warning) return 2;
                if (SelectedFilterStatus == MissingFilesCheckStatus.Invalid) return 3;
                return -1;
            }
            set
            {
                switch (value)
                {
                    case 0: SelectedFilterStatus = null; break;
                    case 1: SelectedFilterStatus = MissingFilesCheckStatus.Valid; break;
                    case 2: SelectedFilterStatus = MissingFilesCheckStatus.Warning; break;
                    case 3: SelectedFilterStatus = MissingFilesCheckStatus.Invalid; break;
                }
                NotifyPropertyChanged();
            }
        }

        public string SearchText
        {
            get { return this.searchText; }
            set
            {
                this.searchText = value;
                NotifyPropertyChanged();
            }
        }
        private string searchText = string.Empty;

        public string ReplaceText
        {
            get { return this.replaceText; }
            set
            {
                this.replaceText = value;
                NotifyPropertyChanged();
            }
        }
        private string replaceText = string.Empty;

        public MissingFilesCheckItemViewModel SelectedItem
        {
            get { return this.selectedItem; }
            set
            {
                this.selectedItem = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(HasSelectedItem));
            }
        }
        private MissingFilesCheckItemViewModel selectedItem;

        public bool HasSelectedItem => SelectedItem != null;

        public int TotalCount => AllItems.Count;
        public int InvalidCount => AllItems.Count(i => i.Status == MissingFilesCheckStatus.Invalid);
        public int WarningCount => AllItems.Count(i => i.Status == MissingFilesCheckStatus.Warning);
        public int ValidCount => AllItems.Count(i => i.Status == MissingFilesCheckStatus.Valid);

        public ICommand ScanCommand { get; set; }
        public ICommand ReplaceAllCommand { get; set; }
        public ICommand BrowseCommand { get; set; }
        public ICommand LocateMissingFilesCommand { get; set; }

        public MissingFilesCheckWindowViewModel()
        {
            ScanCommand = this.CreateCommand(() =>
            {
                LoadFilePaths();
            });

            ReplaceAllCommand = this.CreateCommand(() =>
            {
                if (string.IsNullOrEmpty(SearchText))
                {
                    return;
                }

                foreach (var item in AllItems)
                {
                    if (item.FilePath.Contains(SearchText))
                    {
                        item.FilePath = item.FilePath.Replace(SearchText, ReplaceText);
                    }
                }

                ApplyFilter();
                NotifyPropertyChanged(nameof(InvalidCount));
                NotifyPropertyChanged(nameof(WarningCount));
                NotifyPropertyChanged(nameof(ValidCount));
            });

            BrowseCommand = this.CreateCommand(() =>
            {
                if (SelectedItem == null)
                {
                    return;
                }

                string filePath = ServiceManager.Get<IFileService>().ShowOpenFileDialog();
                if (!string.IsNullOrEmpty(filePath))
                {
                    SelectedItem.FilePath = filePath;
                    ApplyFilter();
                    NotifyPropertyChanged(nameof(InvalidCount));
                    NotifyPropertyChanged(nameof(WarningCount));
                    NotifyPropertyChanged(nameof(ValidCount));
                }
            });

            LocateMissingFilesCommand = this.CreateCommand(async () =>
            {

                string folderPath = ServiceManager.Get<IFileService>().ShowOpenFolderDialog();
                if (string.IsNullOrEmpty(folderPath))
                {
                    return;
                }

                var invalidItems = AllItems.Where(i => !i.IsValid).ToList();
                if (invalidItems.Count == 0)
                {
                    return;
                }

                var fileLookup = new MissingFilesCheckService().GetFileLookup(folderPath);

                int fixedCount = 0;
                foreach (var item in invalidItems)
                {
                    try
                    {
                        string targetFileName = Path.GetFileName(item.FilePath);
                        if (!string.IsNullOrEmpty(targetFileName) && fileLookup.TryGetValue(targetFileName, out string newPath))
                        {
                            item.FilePath = newPath;
                            fixedCount++;
                        }
                    }
                    catch
                    {
                        // Ignore paths that are not valid file paths (e.g. URLs or special identifiers)
                    }
                }

                if (fixedCount > 0)
                {
                    ApplyFilter();
                    NotifyPropertyChanged(nameof(InvalidCount));
                    NotifyPropertyChanged(nameof(WarningCount));
                    NotifyPropertyChanged(nameof(ValidCount));
                }
            });

            LoadFilePaths();
        }

        private void LoadFilePaths()
        {
            AllItems.Clear();
            FilteredItems.Clear();

            MissingFilesCheckService service = new MissingFilesCheckService();
            List<MissingFilesCheckReference> references = service.GetAllFilePaths();

            foreach (var reference in references)
            {
                AllItems.Add(new MissingFilesCheckItemViewModel(reference));
            }

            ApplyFilter();
            NotifyPropertyChanged(nameof(TotalCount));
            NotifyPropertyChanged(nameof(InvalidCount));
            NotifyPropertyChanged(nameof(WarningCount));
            NotifyPropertyChanged(nameof(ValidCount));

            ServiceManager.Get<ITelemetryService>().TrackFeature("MissingFilesCheck", new Dictionary<string, object>
            {
                { "invalid_count", InvalidCount }
            });
        }

        private void ApplyFilter()
        {
            FilteredItems.Clear();

            IEnumerable<MissingFilesCheckItemViewModel> items = AllItems;
            if (SelectedFilterStatus.HasValue)
            {
                items = items.Where(i => i.Status == SelectedFilterStatus.Value);
            }

            foreach (var item in items)
            {
                FilteredItems.Add(item);
            }
        }
    }
}
