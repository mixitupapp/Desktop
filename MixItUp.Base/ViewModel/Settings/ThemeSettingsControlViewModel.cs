using MixItUp.Base.Model.Settings;
using MixItUp.Base.ViewModel.Settings.Generic;
using MixItUp.Base.ViewModels;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MixItUp.Base.ViewModel.Settings
{
    public class ThemeViewModel : UIViewModelBase
    {
        public string Key { get; set; }
        public string Name { get; set; }

        public ThemeViewModel(string key, string name)
        {
            this.Key = key;
            this.Name = name;
        }

        public override string ToString() { return this.Name; }
    }

    public class ThemeSettingsControlViewModel : UIViewModelBase
    {
        public List<string> AvailableBackgroundColors { get; set; } = new List<string>() { "Light", "Dark" };
        public List<string> AvailableForegroundColors { get; set; } = new List<string>() { "Default", "White", "Black" };

        public Dictionary<string, string> FullThemes { get; set; } = new Dictionary<string, string>()
        {
            { string.Empty, MixItUp.Base.Resources.None },
            { "1YearAnniversary", "1 Year Anniversary" },
            { "Mixer", "Mixer" },
            { "Twitch", "Twitch" },
            { "Atl3msPlexify", "Atl3m's Plexify" },
            { "AwkwardTysonAmericana", "AwkwardTyson - Americana" },
            { "AzhtralsCosmicFire", "Azhtral's Cosmic Fire" },
            { "BlueLeprechaunTV", "BlueLeprechaunTV" },
            { "DrewsTheme", "Drew's Theme" },
            { "DustysPurplePotion", "Dusty's Purple Potion" },
            { "Elmza", "Elmza" },
            { "InsertCoinTheater", "Insert Coin Theater" },
            { "KaciesGalaxy", "Kacie's Galaxy" },
            { "KarebearXp", "KarebearXp" },
            { "NibblesCarrotPatch", "Nibbles' Carrot Patch" },
            { "StarkContrast", "Stark Contrast" },
            { "TacosAfterDark", "Tacos After Dark" },
            { "TeamBoom", "Team Boom" },
            { "WildWestDan", "WildWestDan's Carnival Theme" }
        };

        public GenericColorComboBoxSettingsOptionControlViewModel ColorScheme { get; set; }
        public GenericComboBoxSettingsOptionControlViewModel<string> BackgroundColor { get; set; }
        public GenericComboBoxSettingsOptionControlViewModel<string> ForegroundColor { get; set; }
        public GenericComboBoxSettingsOptionControlViewModel<ThemeViewModel> FullTheme { get; set; }
        public GenericComboBoxSettingsOptionControlViewModel<string> UIFont { get; set; }
        public GenericSliderSettingsOptionControlViewModel UIScale { get; set; }
        public GenericSliderSettingsOptionControlViewModel ChatWindowFontSize { get; set; }
        public GenericToggleSettingsOptionControlViewModel SupportMode { get; set; }
        public GenericButtonSettingsOptionControlViewModel ResetToDefaults { get; set; }

        /// <summary>
        /// False while support mode is active, so the rest of the page cannot be edited into a state
        /// that the temporary defaults are silently overriding.
        /// </summary>
        public bool AreThemeControlsEnabled
        {
            get { return this.areThemeControlsEnabled; }
            set
            {
                this.areThemeControlsEnabled = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool areThemeControlsEnabled = true;

        // Deliberately not persisted: support mode is a troubleshooting aid, and a crash or restart
        // should never be able to strand someone in it.
        private bool isSupportModeActive = false;

        private bool isColorSchemeEnabled = true;
        public bool IsColorSchemeEnabled
        {
            get { return this.isColorSchemeEnabled; }
            set
            {
                this.isColorSchemeEnabled = value;
                this.NotifyPropertyChanged();
            }
        }

        private bool isBackgroundColorEnabled = true;
        public bool IsBackgroundColorEnabled
        {
            get { return this.isBackgroundColorEnabled; }
            set
            {
                this.isBackgroundColorEnabled = value;
                this.NotifyPropertyChanged();
            }
        }

        private bool isForegroundColorEnabled = true;
        public bool IsForegroundColorEnabled
        {
            get { return this.isForegroundColorEnabled; }
            set
            {
                this.isForegroundColorEnabled = value;
                this.NotifyPropertyChanged();
            }
        }

        public ThemeSettingsControlViewModel()
        {
            this.ColorScheme = new GenericColorComboBoxSettingsOptionControlViewModel(
                MixItUp.Base.Resources.ColorScheme,
                ChannelSession.AppSettings.ColorScheme,
                (value) =>
                {
                    if (value != null && !string.Equals(ChannelSession.AppSettings.ColorScheme, value))
                    {
                        ChannelSession.AppSettings.ColorScheme = value;
                        ApplyCurrentTheme();
                    }
                });
            this.ColorScheme.RemoveNonThemes();

            this.BackgroundColor = new GenericComboBoxSettingsOptionControlViewModel<string>(
                MixItUp.Base.Resources.BackgroundColor,
                AvailableBackgroundColors,
                ChannelSession.AppSettings.BackgroundColor,
                (value) =>
                {
                    if (!string.Equals(ChannelSession.AppSettings.BackgroundColor, value))
                    {
                        ChannelSession.AppSettings.BackgroundColor = value;
                        ApplyCurrentTheme();
                    }
                });

            this.ForegroundColor = new GenericComboBoxSettingsOptionControlViewModel<string>(
                MixItUp.Base.Resources.ForegroundColor,
                AvailableForegroundColors,
                ChannelSession.AppSettings.ForegroundColor,
                (value) =>
                {
                    if (!string.Equals(ChannelSession.AppSettings.ForegroundColor, value))
                    {
                        ChannelSession.AppSettings.ForegroundColor = value;
                        ApplyCurrentTheme();
                    }
                });

            List<ThemeViewModel> themes = new List<ThemeViewModel>();
            foreach (var kvp in this.FullThemes)
            {
                themes.Add(new ThemeViewModel(kvp.Key, kvp.Value));
            }

            this.FullTheme = new GenericComboBoxSettingsOptionControlViewModel<ThemeViewModel>(
                MixItUp.Base.Resources.FullTheme,
                themes,
                themes.FirstOrDefault(t => t.Key.Equals(ChannelSession.AppSettings.FullThemeName)),
                (value) =>
                {
                    if (value != null && !string.Equals(ChannelSession.AppSettings.FullThemeName, value?.Key))
                    {
                        ChannelSession.AppSettings.FullThemeName = value?.Key;

                        this.RefreshControlEnabledStates();

                        ApplyCurrentTheme();
                    }
                });

            // The packaged default is pinned to the top of the list so a user who picks an unreadable
            // font can always get back to it. The list comes from IThemeService rather than
            // IFileService.GetInstalledFonts() because only the names WPF itself uses can actually be
            // rendered by the application chrome.
            List<string> fonts = new List<string>() { ApplicationSettingsV2Model.DefaultUIFontFamily };
            fonts.AddRange(ServiceManager.Get<IThemeService>().GetAvailableFonts()
                .Where(f => !string.Equals(f, ApplicationSettingsV2Model.DefaultUIFontFamily, StringComparison.OrdinalIgnoreCase)));

            // A saved font that is no longer installed falls back to the packaged default.
            string selectedFont = fonts.FirstOrDefault(f => string.Equals(f, ChannelSession.AppSettings.UIFontFamily, StringComparison.OrdinalIgnoreCase)) ?? ApplicationSettingsV2Model.DefaultUIFontFamily;

            this.UIFont = new GenericComboBoxSettingsOptionControlViewModel<string>(
                MixItUp.Base.Resources.UIFont,
                fonts,
                selectedFont,
                (value) =>
                {
                    if (!string.IsNullOrEmpty(value) && !string.Equals(ChannelSession.AppSettings.UIFontFamily, value))
                    {
                        ChannelSession.AppSettings.UIFontFamily = value;
                        ApplyCurrentFont();
                    }
                });
            // Font family names are proper nouns and must not be localized for display. Without this,
            // a font whose name collides with a resource key renders translated, for example the
            // "Symbol" font showing as "Simbolo" or "Symbole".
            this.UIFont.LocalizeItems = false;
            // Draw every entry in the font it names so the list previews the choice.
            this.UIFont.RenderItemsInOwnFont = true;

            this.UIScale = new GenericSliderSettingsOptionControlViewModel(
                MixItUp.Base.Resources.OverallFontSize,
                ChannelSession.AppSettings.UIScale,
                ApplicationSettingsV2Model.MinimumUIScale,
                ApplicationSettingsV2Model.MaximumUIScale,
                (value) =>
                {
                    ChannelSession.AppSettings.UIScale = value;
                    ApplyCurrentFontScale();
                });

            // The chat font size lives here rather than on the Chat settings page so that every font
            // control sits in one place. Note it is per-channel (SettingsV3Model), unlike the UI font
            // and scale above it which are per-installation, so it moves when the channel changes.
            this.ChatWindowFontSize = new GenericSliderSettingsOptionControlViewModel(
                MixItUp.Base.Resources.ChatWindowFontSize,
                ChannelSession.Settings.ChatFontSize,
                6,
                100,
                (value) =>
                {
                    ChannelSession.Settings.ChatFontSize = value;
                    ChatService.ChatVisualSettingsChanged();
                });

            this.SupportMode = new GenericToggleSettingsOptionControlViewModel(
                MixItUp.Base.Resources.SupportMode,
                false,
                (value) =>
                {
                    this.isSupportModeActive = value;
                    this.RefreshControlEnabledStates();

                    if (value)
                    {
                        // Applied to the live UI only. Nothing is written to settings, so whatever the
                        // user had configured is still intact when the toggle goes back off.
                        DispatcherHelper.Dispatcher.Invoke(() =>
                        {
                            ServiceManager.Get<IThemeService>().ApplyTheme(
                                ApplicationSettingsV2Model.DefaultColorScheme,
                                ApplicationSettingsV2Model.DefaultBackgroundColor,
                                ApplicationSettingsV2Model.DefaultForegroundColor,
                                ApplicationSettingsV2Model.DefaultFullThemeName);
                            ServiceManager.Get<IThemeService>().ApplyFont(ApplicationSettingsV2Model.DefaultUIFontFamily);
                            ServiceManager.Get<IThemeService>().ApplyFontScale(ApplicationSettingsV2Model.DefaultUIScale / 100.0);
                        });
                    }
                    else
                    {
                        ApplyCurrentTheme();
                        ApplyCurrentFont();
                        ApplyCurrentFontScale();
                    }
                },
                MixItUp.Base.Resources.SupportModeTooltip);

            this.ResetToDefaults = new GenericButtonSettingsOptionControlViewModel(
                MixItUp.Base.Resources.ResetThemeAndFontSettings,
                MixItUp.Base.Resources.ResetToDefaults,
                this.CreateCommand(async () =>
                {
                    if (await DialogHelper.ShowConfirmation(MixItUp.Base.Resources.ResetThemeAndFontSettingsConfirmation))
                    {
                        // Assigning through the option view models rather than the settings directly
                        // keeps the on-screen controls in step and reuses each setter's apply logic.
                        this.ColorScheme.Value = this.ColorScheme.Values.FirstOrDefault(c => c.Name.Equals(ApplicationSettingsV2Model.DefaultColorScheme));
                        this.BackgroundColor.Value = ApplicationSettingsV2Model.DefaultBackgroundColor;
                        this.ForegroundColor.Value = ApplicationSettingsV2Model.DefaultForegroundColor;
                        this.FullTheme.Value = this.FullTheme.Values.FirstOrDefault(t => string.IsNullOrEmpty(t.Key));
                        this.UIFont.Value = ApplicationSettingsV2Model.DefaultUIFontFamily;
                        this.UIScale.Value = ApplicationSettingsV2Model.DefaultUIScale;
                        this.ChatWindowFontSize.Value = SettingsV3Model.DefaultChatFontSize;

                        // The theme and font live per-installation, the chat font size per-login, so
                        // both stores have to be written for the reset to survive a restart.
                        await ChannelSession.AppSettings.Save();
                        await ChannelSession.SaveSettings();
                    }
                }));

            this.RefreshControlEnabledStates();
        }

        private void RefreshControlEnabledStates()
        {
            bool hasFullTheme = !string.IsNullOrEmpty(ChannelSession.AppSettings.FullThemeName);
            bool editable = !this.isSupportModeActive;

            this.IsColorSchemeEnabled = editable && !hasFullTheme;
            this.IsBackgroundColorEnabled = editable && !hasFullTheme;
            this.IsForegroundColorEnabled = editable && !hasFullTheme;
            this.AreThemeControlsEnabled = editable;
        }

        private void ApplyCurrentTheme()
        {
            DispatcherHelper.Dispatcher.Invoke(() =>
            {
                ServiceManager.Get<IThemeService>().ApplyTheme(
                    ChannelSession.AppSettings.ColorScheme ?? "Indigo",
                    ChannelSession.AppSettings.BackgroundColor ?? "Light",
                    ChannelSession.AppSettings.ForegroundColor ?? "Default",
                    ChannelSession.AppSettings.FullThemeName
                );
            });
        }

        // Matches the theme options: the value is only set in memory here and is written to
        // ApplicationSettings.json on the shared save points (main window close & the login flow).
        private void ApplyCurrentFont()
        {
            DispatcherHelper.Dispatcher.Invoke(() =>
            {
                ServiceManager.Get<IThemeService>().ApplyFont(ChannelSession.AppSettings.UIFontFamily);
            });
        }

        private void ApplyCurrentFontScale()
        {
            DispatcherHelper.Dispatcher.Invoke(() =>
            {
                ServiceManager.Get<IThemeService>().ApplyFontScale(ChannelSession.AppSettings.UIScale / 100.0);
            });
        }
    }
}