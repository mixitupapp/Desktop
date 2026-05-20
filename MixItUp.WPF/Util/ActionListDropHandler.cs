using GongSolutions.Wpf.DragDrop;
using MixItUp.Base.ViewModel.Actions;
using System.Windows;

using GongDragDrop = GongSolutions.Wpf.DragDrop.DragDrop;

namespace MixItUp.WPF.Util
{
    public class ActionListDropHandler : IDropTarget
    {
        public static ActionListDropHandler Instance { get; } = new ActionListDropHandler();

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is ActionEditorControlViewModelBase actionViewModel && this.CanDrop(dropInfo, actionViewModel))
            {
                GongDragDrop.DefaultDropHandler.DragOver(dropInfo);
                dropInfo.Effects = DragDropEffects.Move;
            }
            else
            {
                dropInfo.Effects = DragDropEffects.None;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is ActionEditorControlViewModelBase actionViewModel)
            {
                ActionEditorListControlViewModel actionEditorListControlViewModel = this.GetActionEditorListControlViewModel(dropInfo);
                actionEditorListControlViewModel?.DropAction(actionViewModel, dropInfo.InsertIndex);
            }
        }

        private bool CanDrop(IDropInfo dropInfo, ActionEditorControlViewModelBase actionViewModel)
        {
            ActionEditorListControlViewModel actionEditorListControlViewModel = this.GetActionEditorListControlViewModel(dropInfo);
            return actionEditorListControlViewModel != null && actionEditorListControlViewModel.CanDropAction(actionViewModel);
        }

        private ActionEditorListControlViewModel GetActionEditorListControlViewModel(IDropInfo dropInfo)
        {
            if (dropInfo.VisualTarget is FrameworkElement visualTarget)
            {
                if (visualTarget.DataContext is ActionEditorListControlViewModel actionEditorListControlViewModel)
                {
                    return actionEditorListControlViewModel;
                }

                if (visualTarget.DataContext is GroupActionEditorControlViewModel groupActionEditorControlViewModel)
                {
                    return groupActionEditorControlViewModel.ActionEditorList;
                }
            }

            if (dropInfo.TargetItem is ActionEditorControlViewModelBase targetActionViewModel)
            {
                return targetActionViewModel.ActionEditorListControlViewModel;
            }

            return null;
        }
    }
}
