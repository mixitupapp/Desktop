using GongSolutions.Wpf.DragDrop;
using MixItUp.WPF.Controls.MainControls;
using System.Collections.ObjectModel;
using System.Windows;

namespace MixItUp.WPF.Util
{
    public class MainMenuDragDropHandler : IDropTarget
    {
        public static MainMenuDragDropHandler Instance { get; } = new MainMenuDragDropHandler();

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is MainMenuItem && dropInfo.TargetCollection != null)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is MainMenuItem sourceItem && dropInfo.TargetCollection != null)
            {
                var items = dropInfo.TargetCollection as ObservableCollection<MainMenuItem>;
                if (items != null)
                {
                    int oldIndex = items.IndexOf(sourceItem);
                    int newIndex = dropInfo.InsertIndex;

                    if (oldIndex != -1)
                    {
                        if (newIndex > oldIndex)
                        {
                            newIndex--;
                        }

                        items.Move(oldIndex, newIndex);
                    }
                }
            }
        }
    }
}