using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Search;
using MixItUp.Base.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;

namespace MixItUp.WPF.Windows.Overlay
{
    public partial class OverlayEditorPopoutWindow : Window
    {
        private readonly XmlFoldingStrategy htmlFoldingStrategy = new XmlFoldingStrategy();
        private readonly DispatcherTimer foldingUpdateTimer = new DispatcherTimer();
        private readonly Dictionary<TextEditor, FoldingManager> foldingManagers = new Dictionary<TextEditor, FoldingManager>();
        private readonly Dictionary<TextEditor, SearchPanel> searchPanels = new Dictionary<TextEditor, SearchPanel>();
        private readonly HashSet<TextEditor> pendingFoldingUpdates = new HashSet<TextEditor>();

        public OverlayEditorPopoutWindow(UIViewModelBase viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;

            this.ConfigureEditor(this.HtmlEditor);
            this.ConfigureEditor(this.CssEditor);
            this.ConfigureEditor(this.JavascriptEditor);

            this.foldingUpdateTimer.Interval = TimeSpan.FromMilliseconds(500);
            this.foldingUpdateTimer.Tick += this.FoldingUpdateTimer_Tick;
            this.foldingUpdateTimer.Start();

            this.Closed += this.OverlayEditorPopoutWindow_Closed;

            this.WordWrapToggleButton.IsChecked = false;
            this.WhitespaceToggleButton.IsChecked = false;
        }

        private void ConfigureEditor(TextEditor editor)
        {
            editor.Options.ConvertTabsToSpaces = true;
            editor.Options.IndentationSize = 2;
            editor.Options.HighlightCurrentLine = true;
            editor.Options.EnableHyperlinks = true;
            editor.Options.AllowScrollBelowDocument = true;
            editor.Options.ShowSpaces = false;
            editor.Options.ShowTabs = false;
            editor.Options.ShowEndOfLine = false;
            editor.WordWrap = false;

            FoldingManager manager = FoldingManager.Install(editor.TextArea);
            this.foldingManagers[editor] = manager;

            SearchPanel searchPanel = SearchPanel.Install(editor);
            this.searchPanels[editor] = searchPanel;
            this.ApplySearchPanelStyles(searchPanel);
            searchPanel.Loaded += this.SearchPanel_Loaded;

            editor.TextChanged += this.Editor_TextChanged;
            this.pendingFoldingUpdates.Add(editor);
        }

        private void OverlayEditorPopoutWindow_Closed(object sender, EventArgs e)
        {
            this.foldingUpdateTimer.Stop();
            this.foldingUpdateTimer.Tick -= this.FoldingUpdateTimer_Tick;

            foreach (TextEditor editor in this.GetEditors())
            {
                editor.TextChanged -= this.Editor_TextChanged;
            }

            foreach (var kvp in this.foldingManagers.ToList())
            {
                FoldingManager.Uninstall(kvp.Value);
            }

            foreach (var kvp in this.searchPanels.ToList())
            {
                kvp.Value.Loaded -= this.SearchPanel_Loaded;
                kvp.Value.Uninstall();
            }

            this.foldingManagers.Clear();
            this.searchPanels.Clear();
            this.pendingFoldingUpdates.Clear();
        }

        private void Editor_TextChanged(object sender, EventArgs e)
        {
            if (sender is TextEditor editor)
            {
                this.pendingFoldingUpdates.Add(editor);
            }
        }

        private void FoldingUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (this.pendingFoldingUpdates.Count == 0)
            {
                return;
            }

            TextEditor[] editorsToUpdate = this.pendingFoldingUpdates.ToArray();
            this.pendingFoldingUpdates.Clear();

            foreach (TextEditor editor in editorsToUpdate)
            {
                this.UpdateFoldings(editor);
            }
        }

        private void UpdateFoldings(TextEditor editor)
        {
            if (editor.Document == null || !this.foldingManagers.TryGetValue(editor, out FoldingManager manager))
            {
                return;
            }

            if (editor == this.HtmlEditor)
            {
                this.UpdateHtmlFoldings(manager, editor.Document);
            }
            else
            {
                manager.UpdateFoldings(CreateBraceFoldings(editor.Document), -1);
            }
        }

        private void UpdateHtmlFoldings(FoldingManager manager, TextDocument document)
        {
            try
            {
                this.htmlFoldingStrategy.UpdateFoldings(manager, document);
            }
            catch (System.Xml.XmlException)
            {
                manager.UpdateFoldings(Array.Empty<NewFolding>(), -1);
            }
            catch (InvalidOperationException)
            {
                manager.UpdateFoldings(Array.Empty<NewFolding>(), -1);
            }
        }

        private static IEnumerable<NewFolding> CreateBraceFoldings(TextDocument document)
        {
            var startOffsets = new Stack<int>();
            var foldings = new List<NewFolding>();

            for (int i = 0; i < document.TextLength; i++)
            {
                char c = document.GetCharAt(i);
                if (c == '{')
                {
                    startOffsets.Push(i);
                }
                else if (c == '}' && startOffsets.Count > 0)
                {
                    int startOffset = startOffsets.Pop();
                    if (startOffset >= i)
                    {
                        continue;
                    }

                    if (document.GetLineByOffset(startOffset).LineNumber < document.GetLineByOffset(i).LineNumber)
                    {
                        foldings.Add(new NewFolding(startOffset, i + 1));
                    }
                }
            }

            return foldings.OrderBy(f => f.StartOffset);
        }

        private void ApplySearchPanelStyles(SearchPanel searchPanel)
        {
            if (this.TryFindResource("PopoutSearchPanelButtonStyle") is Style buttonStyle)
            {
                searchPanel.Resources[typeof(Button)] = buttonStyle;
            }

            if (this.TryFindResource("PopoutSearchPanelToggleButtonStyle") is Style toggleButtonStyle)
            {
                searchPanel.Resources[typeof(ToggleButton)] = toggleButtonStyle;
            }

            if (this.TryFindResource("PopoutSearchPanelTextBoxStyle") is Style textBoxStyle)
            {
                searchPanel.Resources[typeof(TextBox)] = textBoxStyle;
            }

            if (this.TryFindResource("PopoutSearchPanelCheckBoxStyle") is Style checkBoxStyle)
            {
                searchPanel.Resources[typeof(CheckBox)] = checkBoxStyle;
            }
        }

        private void SearchPanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is SearchPanel searchPanel)
            {
                this.ApplySearchPanelStyles(searchPanel);
                this.ApplySearchPanelMaterialIcons(searchPanel);
            }
        }

        private void ApplySearchPanelMaterialIcons(SearchPanel searchPanel)
        {
            foreach (ButtonBase button in FindVisualChildren<ButtonBase>(searchPanel))
            {
                if (button.Command == SearchCommands.FindNext)
                {
                    this.SetSearchButtonIcon(button, "chevron_right");
                }
                else if (button.Command == SearchCommands.FindPrevious)
                {
                    this.SetSearchButtonIcon(button, "chevron_left");
                }
                else if (button.Command == SearchCommands.CloseSearchPanel)
                {
                    this.SetSearchButtonIcon(button, "close");
                }
            }
        }

        private void SetSearchButtonIcon(ButtonBase button, string iconName)
        {
            Brush foregroundBrush = button.Foreground;
            button.Content = new Controls.MaterialSymbolIcon() { IconName = iconName, FontSize = 16, Foreground = foregroundBrush };
            button.Padding = new Thickness(2);
            button.MinWidth = 30;
            button.MinHeight = 26;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.VerticalContentAlignment = VerticalAlignment.Center;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
            {
                yield break;
            }

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (T descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        private void WordWrapToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            this.SetWordWrap(true);
        }

        private void WordWrapToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            this.SetWordWrap(false);
        }

        private void SetWordWrap(bool enabled)
        {
            foreach (TextEditor editor in this.GetEditors())
            {
                editor.WordWrap = enabled;
            }
        }

        private void WhitespaceToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            this.SetWhitespaceVisibility(true);
        }

        private void WhitespaceToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            this.SetWhitespaceVisibility(false);
        }

        private void SetWhitespaceVisibility(bool visible)
        {
            foreach (TextEditor editor in this.GetEditors())
            {
                editor.Options.ShowSpaces = visible;
                editor.Options.ShowTabs = visible;
                editor.Options.ShowEndOfLine = visible;
            }
        }

        private IEnumerable<TextEditor> GetEditors()
        {
            yield return this.HtmlEditor;
            yield return this.CssEditor;
            yield return this.JavascriptEditor;
        }

    }
}
