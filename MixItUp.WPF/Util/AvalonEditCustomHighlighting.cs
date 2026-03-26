using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Xml;

namespace MixItUp.WPF.Util
{
    public static class AvalonEditCustomHighlighting
    {
        private const string HTML = "HTML";
        private const string CSS = "CSS";
        private const string JAVASCRIPT = "JavaScript";

        private const string HtmlDarkDefinition = "MixItUpHTML.Dark";
        private const string HtmlLightDefinition = "MixItUpHTML.Light";
        private const string CssDarkDefinition = "MixItUpCSS.Dark";
        private const string CssLightDefinition = "MixItUpCSS.Light";
        private const string JavaScriptDarkDefinition = "MixItUpJavaScript.Dark";
        private const string JavaScriptLightDefinition = "MixItUpJavaScript.Light";

        private static bool initialized = false;
        private static readonly HashSet<TextEditor> trackedEditors = new HashSet<TextEditor>();
        private static EventHandler themeChangedHandler;

        public static readonly DependencyProperty LanguageProperty =
            DependencyProperty.RegisterAttached("Language", typeof(string), typeof(AvalonEditCustomHighlighting), new PropertyMetadata(null, LanguageChangedCallback));

        public static string GetLanguage(DependencyObject dp)
        {
            return (string)dp.GetValue(LanguageProperty);
        }

        public static void SetLanguage(DependencyObject dp, string value)
        {
            dp.SetValue(LanguageProperty, value);
        }

        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            RegisterHighlighting(HtmlDarkDefinition, "/Assets/Syntax/MixItUpHTML.xshd");
            RegisterHighlighting(HtmlLightDefinition, "/Assets/Syntax/MixItUpHTML.Light.xshd");

            RegisterHighlighting(CssDarkDefinition, "/Assets/Syntax/MixItUpCSS.xshd");
            RegisterHighlighting(CssLightDefinition, "/Assets/Syntax/MixItUpCSS.Light.xshd");

            RegisterHighlighting(JavaScriptDarkDefinition, "/Assets/Syntax/MixItUpJavaScript.xshd");
            RegisterHighlighting(JavaScriptLightDefinition, "/Assets/Syntax/MixItUpJavaScript.Light.xshd");

            SubscribeToThemeChanges();
        }

        private static void RegisterHighlighting(string name, string packPath)
        {
            try
            {
                if (HighlightingManager.Instance.GetDefinition(name) != null)
                {
                    return;
                }

                Stream stream = TryOpenResourceStream(packPath);
                if (stream == null)
                {
                    Logger.Log(LogLevel.Warning, $"Unable to load AvalonEdit highlighting resource: {packPath}");
                    return;
                }

                using (stream)
                using (XmlReader reader = XmlReader.Create(stream))
                {
                    var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    HighlightingManager.Instance.RegisterHighlighting(name, Array.Empty<string>(), definition);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private static void LanguageChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextEditor editor)
            {
                return;
            }

            if (!initialized)
            {
                Initialize();
            }

            string language = e.NewValue as string;
            if (string.IsNullOrEmpty(language))
            {
                trackedEditors.Remove(editor);
                editor.Loaded -= Editor_Loaded;
                editor.Unloaded -= Editor_Unloaded;
                return;
            }

            trackedEditors.Add(editor);
            editor.Loaded -= Editor_Loaded;
            editor.Loaded += Editor_Loaded;
            editor.Unloaded -= Editor_Unloaded;
            editor.Unloaded += Editor_Unloaded;

            ApplySyntaxHighlighting(editor);
        }

        private static void Editor_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextEditor editor)
            {
                trackedEditors.Add(editor);
                ApplySyntaxHighlighting(editor);
            }
        }

        private static void Editor_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextEditor editor)
            {
                trackedEditors.Remove(editor);
            }
        }

        private static void SubscribeToThemeChanges()
        {
            if (!ServiceManager.Has<IThemeService>())
            {
                return;
            }

            IThemeService themeService = ServiceManager.Get<IThemeService>();
            if (themeService == null)
            {
                return;
            }

            if (themeChangedHandler == null)
            {
                themeChangedHandler = (s, e) => ApplySyntaxHighlightingToTrackedEditors();
            }

            themeService.ThemeChanged -= themeChangedHandler;
            themeService.ThemeChanged += themeChangedHandler;
        }

        private static void ApplySyntaxHighlightingToTrackedEditors()
        {
            foreach (TextEditor editor in new List<TextEditor>(trackedEditors))
            {
                ApplySyntaxHighlighting(editor);
            }
        }

        private static void ApplySyntaxHighlighting(TextEditor editor)
        {
            string language = GetLanguage(editor);
            if (string.IsNullOrEmpty(language))
            {
                return;
            }

            bool isDarkTheme = ServiceManager.Has<IThemeService>() && (ServiceManager.Get<IThemeService>()?.IsDarkTheme ?? false);
            string definitionName = GetDefinitionName(language, isDarkTheme);
            if (string.IsNullOrEmpty(definitionName))
            {
                return;
            }

            var definition = HighlightingManager.Instance.GetDefinition(definitionName);
            if (definition != null)
            {
                editor.SyntaxHighlighting = definition;
            }
        }

        private static string GetDefinitionName(string language, bool isDarkTheme)
        {
            if (string.Equals(language, HTML, StringComparison.OrdinalIgnoreCase))
            {
                return isDarkTheme ? HtmlDarkDefinition : HtmlLightDefinition;
            }
            if (string.Equals(language, CSS, StringComparison.OrdinalIgnoreCase))
            {
                return isDarkTheme ? CssDarkDefinition : CssLightDefinition;
            }
            if (string.Equals(language, JAVASCRIPT, StringComparison.OrdinalIgnoreCase))
            {
                return isDarkTheme ? JavaScriptDarkDefinition : JavaScriptLightDefinition;
            }

            return null;
        }

        private static Stream TryOpenResourceStream(string packPath)
        {
            Uri[] urisToTry = new[]
            {
                new Uri(packPath, UriKind.Relative),
                new Uri($"pack://application:,,,{packPath}", UriKind.Absolute),
                new Uri($"pack://application:,,,/{Assembly.GetExecutingAssembly().GetName().Name};component{packPath}", UriKind.Absolute),
            };

            foreach (Uri uri in urisToTry)
            {
                try
                {
                    var info = Application.GetResourceStream(uri);
                    if (info?.Stream != null)
                    {
                        return info.Stream;
                    }
                }
                catch
                { }
            }

            return null;
        }
    }
}
