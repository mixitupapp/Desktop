using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace MixItUp.WPF.Util
{
    public static class AvalonEditBehaviour
    {
        private static readonly HashSet<TextEditor> updatingEditors = new HashSet<TextEditor>();

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.RegisterAttached("Text", typeof(string), typeof(AvalonEditBehaviour), new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, PropertyChangedCallback));

        private static readonly DependencyProperty ThemeIntegrationInstalledProperty =
            DependencyProperty.RegisterAttached("ThemeIntegrationInstalled", typeof(bool), typeof(AvalonEditBehaviour), new PropertyMetadata(false));

        private static readonly DependencyProperty ThemeHandlerProperty =
            DependencyProperty.RegisterAttached("ThemeHandler", typeof(EventHandler), typeof(AvalonEditBehaviour), new PropertyMetadata(null));

        private static readonly DependencyProperty ColorizerProperty =
            DependencyProperty.RegisterAttached("Colorizer", typeof(ThemeAwareContrastColorizer), typeof(AvalonEditBehaviour), new PropertyMetadata(null));

        public static string GetText(DependencyObject dp)
        {
            return (string)dp.GetValue(TextProperty);
        }

        public static void SetText(DependencyObject dp, string value)
        {
            dp.SetValue(TextProperty, value);
        }

        private static void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextEditor editor)
            {
                EnsureThemeIntegration(editor);

                if (editor.Document != null && !updatingEditors.Contains(editor))
                {
                    string newText = e.NewValue as string;
                    if (editor.Document.Text != newText)
                    {
                        int caretOffset = editor.CaretOffset;
                        editor.Document.Text = newText ?? string.Empty;
                        try
                        {
                            editor.CaretOffset = Math.Min(caretOffset, editor.Document.TextLength);
                        }
                        catch (InvalidOperationException ex)
                        {
                            Logger.Log(ex);
                        }
                    }
                }
            }
        }

        private static void Editor_TextChanged(object sender, EventArgs e)
        {
            if (sender is TextEditor editor && editor.Document != null)
            {
                updatingEditors.Add(editor);
                try
                {
                    SetText(editor, editor.Document.Text);
                }
                finally
                {
                    updatingEditors.Remove(editor);
                }
            }
        }

        private static void EnsureThemeIntegration(TextEditor editor)
        {
            if ((bool)editor.GetValue(ThemeIntegrationInstalledProperty))
            {
                return;
            }

            editor.SetValue(ThemeIntegrationInstalledProperty, true);

            var colorizer = new ThemeAwareContrastColorizer(editor);
            IThemeService themeService = ServiceManager.Has<IThemeService>() ? ServiceManager.Get<IThemeService>() : null;
            colorizer.SetIsDarkTheme(themeService?.IsDarkTheme ?? false);
            editor.TextArea.TextView.LineTransformers.Add(colorizer);
            editor.SetValue(ColorizerProperty, colorizer);

            editor.TextChanged += Editor_TextChanged;

            EventHandler themeChangedHandler = (s, args) =>
            {
                var c = (ThemeAwareContrastColorizer)editor.GetValue(ColorizerProperty);
                IThemeService ts = ServiceManager.Has<IThemeService>() ? ServiceManager.Get<IThemeService>() : null;

                if (c != null)
                {
                    c.SetIsDarkTheme(ts?.IsDarkTheme ?? false);
                    c.ClearCache();
                }

                editor.TextArea?.TextView?.Redraw();
            };

            editor.SetValue(ThemeHandlerProperty, themeChangedHandler);

            editor.Unloaded += Editor_Unloaded;
            editor.Loaded += Editor_Loaded;

            SubscribeToThemeChanges(editor);
        }

        private static void Editor_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextEditor editor)
            {
                SubscribeToThemeChanges(editor);

                var colorizer = (ThemeAwareContrastColorizer)editor.GetValue(ColorizerProperty);
                IThemeService themeService = ServiceManager.Has<IThemeService>() ? ServiceManager.Get<IThemeService>() : null;
                if (colorizer != null)
                {
                    colorizer.SetIsDarkTheme(themeService?.IsDarkTheme ?? false);
                    colorizer.ClearCache();
                }

                editor.TextArea?.TextView?.Redraw();
            }
        }

        private static void Editor_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextEditor editor)
            {
                UnsubscribeFromThemeChanges(editor);
            }
        }

        private static void SubscribeToThemeChanges(TextEditor editor)
        {
            EventHandler handler = (EventHandler)editor.GetValue(ThemeHandlerProperty);
            if (handler == null)
            {
                return;
            }

            if (ServiceManager.Has<IThemeService>())
            {
                IThemeService themeService = ServiceManager.Get<IThemeService>();
                if (themeService != null)
                {
                    themeService.ThemeChanged -= handler;
                    themeService.ThemeChanged += handler;
                }
            }
        }

        private static void UnsubscribeFromThemeChanges(TextEditor editor)
        {
            EventHandler handler = (EventHandler)editor.GetValue(ThemeHandlerProperty);
            if (handler == null)
            {
                return;
            }

            if (ServiceManager.Has<IThemeService>())
            {
                IThemeService themeService = ServiceManager.Get<IThemeService>();
                if (themeService != null)
                {
                    themeService.ThemeChanged -= handler;
                }
            }
        }
    }

    public class ThemeAwareContrastColorizer : DocumentColorizingTransformer
    {
        private const double MinimumContrastRatio = 4.5;

        private readonly TextEditor editor;
        private readonly Dictionary<(Color Foreground, Color Background), Brush> adjustedBrushCache = new Dictionary<(Color Foreground, Color Background), Brush>();
        private bool isDarkTheme = false;

        public ThemeAwareContrastColorizer(TextEditor editor)
        {
            this.editor = editor;
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (!this.isDarkTheme)
            {
                return;
            }

            Color background = this.GetEditorBackgroundColor();

            this.ChangeLinePart(line.Offset, line.EndOffset, (VisualLineElement element) =>
            {
                if (element.TextRunProperties.ForegroundBrush is SolidColorBrush foregroundBrush)
                {
                    Color foreground = foregroundBrush.Color;
                    if (GetContrastRatio(foreground, background) < MinimumContrastRatio)
                    {
                        element.TextRunProperties.SetForegroundBrush(this.GetAdjustedForegroundBrush(foreground, background));
                    }
                }
            });
        }

        public void SetIsDarkTheme(bool isDark)
        {
            this.isDarkTheme = isDark;
        }

        public void ClearCache()
        {
            this.adjustedBrushCache.Clear();
            this.cachedBackgroundColor = null;
        }

        private Color? cachedBackgroundColor;

        private Color GetEditorBackgroundColor()
        {
            if (this.cachedBackgroundColor.HasValue)
            {
                return this.cachedBackgroundColor.Value;
            }

            Color color = Colors.Black;
            if (this.editor.Background is SolidColorBrush editorBackgroundBrush)
            {
                color = editorBackgroundBrush.Color;
            }
            else if (this.editor.TryFindResource("MaterialDesignPaper") is SolidColorBrush paperBrush)
            {
                color = paperBrush.Color;
            }

            this.cachedBackgroundColor = color;
            return color;
        }

        private Brush GetAdjustedForegroundBrush(Color foreground, Color background)
        {
            var key = (foreground, background);
            if (this.adjustedBrushCache.TryGetValue(key, out Brush cachedBrush))
            {
                return cachedBrush;
            }

            Color adjustedColor = EnsureMinimumContrast(foreground, background, MinimumContrastRatio);
            SolidColorBrush adjustedBrush = new SolidColorBrush(adjustedColor);
            adjustedBrush.Freeze();

            this.adjustedBrushCache[key] = adjustedBrush;
            return adjustedBrush;
        }

        private static Color EnsureMinimumContrast(Color foreground, Color background, double minContrastRatio)
        {
            if (GetContrastRatio(foreground, background) >= minContrastRatio)
            {
                return foreground;
            }

            double low = 0.0;
            double high = 1.0;
            Color best = foreground;

            for (int i = 0; i < 12; i++)
            {
                double t = (low + high) / 2.0;
                Color candidate = Blend(foreground, Colors.White, t);
                if (GetContrastRatio(candidate, background) >= minContrastRatio)
                {
                    best = candidate;
                    high = t;
                }
                else
                {
                    low = t;
                }
            }

            return best;
        }

        private static Color Blend(Color source, Color target, double amount)
        {
            byte r = (byte)Math.Round(source.R + ((target.R - source.R) * amount));
            byte g = (byte)Math.Round(source.G + ((target.G - source.G) * amount));
            byte b = (byte)Math.Round(source.B + ((target.B - source.B) * amount));
            return Color.FromRgb(r, g, b);
        }

        private static double GetContrastRatio(Color a, Color b)
        {
            double luminanceA = GetRelativeLuminance(a);
            double luminanceB = GetRelativeLuminance(b);

            double lighter = Math.Max(luminanceA, luminanceB);
            double darker = Math.Min(luminanceA, luminanceB);
            return (lighter + 0.05) / (darker + 0.05);
        }

        private static double GetRelativeLuminance(Color c)
        {
            return (0.2126 * ToLinear(c.R / 255.0)) + (0.7152 * ToLinear(c.G / 255.0)) + (0.0722 * ToLinear(c.B / 255.0));
        }

        private static double ToLinear(double channel)
        {
            if (channel <= 0.03928)
            {
                return channel / 12.92;
            }

            return Math.Pow((channel + 0.055) / 1.055, 2.4);
        }
    }
}
