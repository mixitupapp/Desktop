using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using MixItUp.Base.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace MixItUp.WPF.Util
{
    public static class AvalonEditBehaviour
    {
        private static readonly ConditionalWeakTable<TextEditor, ThemeSubscriptionState> themeSubscriptions = new ConditionalWeakTable<TextEditor, ThemeSubscriptionState>();

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.RegisterAttached("Text", typeof(string), typeof(AvalonEditBehaviour), new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, PropertyChangedCallback));

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

                if (editor.Document != null)
                {
                    string newText = e.NewValue as string;
                    if (editor.Document.Text != newText)
                    {
                        var caretOffset = editor.CaretOffset;
                        editor.Document.Text = newText ?? string.Empty;
                        try
                        {
                            editor.CaretOffset = Math.Min(caretOffset, editor.Document.TextLength);
                        }
                        catch { }
                    }
                }

                editor.TextChanged -= Editor_TextChanged;
                editor.TextChanged += Editor_TextChanged;
            }
        }

        private static void Editor_TextChanged(object sender, EventArgs e)
        {
            if (sender is TextEditor editor)
            {
                SetText(editor, editor.Document.Text);
            }
        }

        private static void EnsureThemeIntegration(TextEditor editor)
        {
            if (!editor.TextArea.TextView.LineTransformers.OfType<ThemeAwareContrastColorizer>().Any())
            {
                var colorizer = new ThemeAwareContrastColorizer(editor);
                if (ServiceManager.Has<IThemeService>())
                {
                    IThemeService themeService = ServiceManager.Get<IThemeService>();
                    colorizer.SetIsDarkTheme(themeService?.IsDarkTheme ?? false);
                }
                editor.TextArea.TextView.LineTransformers.Add(colorizer);
            }

            if (!themeSubscriptions.TryGetValue(editor, out _))
            {
                EventHandler themeChangedHandler = (s, e) =>
                {
                    var colorizer = editor.TextArea.TextView.LineTransformers.OfType<ThemeAwareContrastColorizer>().FirstOrDefault();
                    IThemeService themeService = ServiceManager.Has<IThemeService>() ? ServiceManager.Get<IThemeService>() : null;
                    if (colorizer != null && themeService != null)
                    {
                        colorizer.SetIsDarkTheme(themeService.IsDarkTheme);
                        colorizer.ClearCache();
                    }
                    editor.TextArea?.TextView?.Redraw();
                };

                var state = new ThemeSubscriptionState(themeChangedHandler);
                themeSubscriptions.Add(editor, state);

                editor.Unloaded += Editor_Unloaded;
                editor.Loaded += Editor_Loaded;

                SubscribeToThemeChanges(state);
            }
        }

        private static void Editor_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextEditor editor && themeSubscriptions.TryGetValue(editor, out ThemeSubscriptionState state))
            {
                SubscribeToThemeChanges(state);
                var colorizer = editor.TextArea.TextView.LineTransformers.OfType<ThemeAwareContrastColorizer>().FirstOrDefault();
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
            if (sender is TextEditor editor && themeSubscriptions.TryGetValue(editor, out ThemeSubscriptionState state))
            {
                UnsubscribeFromThemeChanges(state);
            }
        }

        private static void SubscribeToThemeChanges(ThemeSubscriptionState state)
        {
            if (state.IsSubscribed)
            {
                return;
            }

            if (ServiceManager.Has<IThemeService>())
            {
                IThemeService themeService = ServiceManager.Get<IThemeService>();
                if (themeService != null)
                {
                    themeService.ThemeChanged += state.ThemeChangedHandler;
                    state.IsSubscribed = true;
                }
            }
        }

        private static void UnsubscribeFromThemeChanges(ThemeSubscriptionState state)
        {
            if (!state.IsSubscribed)
            {
                return;
            }

            if (ServiceManager.Has<IThemeService>())
            {
                IThemeService themeService = ServiceManager.Get<IThemeService>();
                if (themeService != null)
                {
                    themeService.ThemeChanged -= state.ThemeChangedHandler;
                }
            }

            state.IsSubscribed = false;
        }

        private sealed class ThemeSubscriptionState
        {
            public EventHandler ThemeChangedHandler { get; }

            public bool IsSubscribed { get; set; }

            public ThemeSubscriptionState(EventHandler themeChangedHandler)
            {
                this.ThemeChangedHandler = themeChangedHandler;
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
