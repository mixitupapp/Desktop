using MaterialDesignThemes.Wpf;
using System.Collections.Generic;

namespace MixItUp.WPF.Controls.MainControls
{
    public class IconMapEntry
    {
        public string KindName { get; private set; }

        public PackIconKind Kind { get; private set; }

        public string Primary { get; private set; }

        public List<string> Alternates { get; private set; }

        public string Usage { get; set; }

        public IconMapEntry(string kindName, PackIconKind kind, string primary, params string[] alternates)
        {
            this.KindName = kindName;
            this.Kind = kind;
            this.Primary = primary;
            this.Alternates = new List<string>(alternates);
        }
    }

    /// <summary>
    /// Debug-only page previewing the PackIcon (mdi) → Material Symbols Sharp icon migration,
    /// row by row with the proposed replacement and alternates.
    /// </summary>
    public partial class IconMapControl : MainControlBase
    {
        /// <summary>The blessed PackIcon (mdi) → Material Symbols mapping; also used to translate legacy mdi icon names at runtime.</summary>
        internal static readonly List<IconMapEntry> Entries = new List<IconMapEntry>()
        {
            new IconMapEntry(nameof(PackIconKind.AccessPoint), PackIconKind.AccessPoint, "sensors", "wifi_tethering", "cell_tower"),
            new IconMapEntry(nameof(PackIconKind.AccountEdit), PackIconKind.AccountEdit, "manage_accounts", "edit_note"),
            new IconMapEntry(nameof(PackIconKind.AccountKey), PackIconKind.AccountKey, "key", "passkey", "admin_panel_settings"),
            new IconMapEntry(nameof(PackIconKind.AccountMultiple), PackIconKind.AccountMultiple, "group", "people", "groups"),
            new IconMapEntry(nameof(PackIconKind.Add), PackIconKind.Add, "add"),
            new IconMapEntry(nameof(PackIconKind.AddBox), PackIconKind.AddBox, "add_box"),
            new IconMapEntry(nameof(PackIconKind.AlertCircle), PackIconKind.AlertCircle, "error", "warning"),
            new IconMapEntry(nameof(PackIconKind.AlertCircleOutline), PackIconKind.AlertCircleOutline, "error", "warning"),
            new IconMapEntry(nameof(PackIconKind.AlertOutline), PackIconKind.AlertOutline, "warning", "error"),
            new IconMapEntry(nameof(PackIconKind.ArrowBack), PackIconKind.ArrowBack, "arrow_back"),
            new IconMapEntry(nameof(PackIconKind.ArrowBackCircle), PackIconKind.ArrowBackCircle, "arrow_circle_left"),
            new IconMapEntry(nameof(PackIconKind.ArrowDown), PackIconKind.ArrowDown, "arrow_downward", "south"),
            new IconMapEntry(nameof(PackIconKind.ArrowDownBold), PackIconKind.ArrowDownBold, "arrow_downward", "south"),
            new IconMapEntry(nameof(PackIconKind.ArrowForward), PackIconKind.ArrowForward, "arrow_forward"),
            new IconMapEntry(nameof(PackIconKind.ArrowRight), PackIconKind.ArrowRight, "arrow_forward", "arrow_right_alt"),
            new IconMapEntry(nameof(PackIconKind.ArrowRightBold), PackIconKind.ArrowRightBold, "arrow_forward", "east"),
            new IconMapEntry(nameof(PackIconKind.ArrowUp), PackIconKind.ArrowUp, "arrow_upward", "north"),
            new IconMapEntry(nameof(PackIconKind.ArrowUpBold), PackIconKind.ArrowUpBold, "arrow_upward", "north"),
            new IconMapEntry(nameof(PackIconKind.Bell), PackIconKind.Bell, "notifications"),
            new IconMapEntry(nameof(PackIconKind.Biohazard), PackIconKind.Biohazard, "dangerous", "skull", "coronavirus"),
            new IconMapEntry(nameof(PackIconKind.BlockHelper), PackIconKind.BlockHelper, "block"),
            new IconMapEntry(nameof(PackIconKind.Broom), PackIconKind.Broom, "mop", "cleaning_services"),
            new IconMapEntry(nameof(PackIconKind.Calendar), PackIconKind.Calendar, "calendar_month", "event"),
            new IconMapEntry(nameof(PackIconKind.Cancel), PackIconKind.Cancel, "cancel", "block"),
            new IconMapEntry(nameof(PackIconKind.Cash100), PackIconKind.Cash100, "payments", "paid", "attach_money"),
            new IconMapEntry(nameof(PackIconKind.CheckboxMarkedCircle), PackIconKind.CheckboxMarkedCircle, "check_circle", "task_alt"),
            new IconMapEntry(nameof(PackIconKind.CheckCircleOutline), PackIconKind.CheckCircleOutline, "check_circle", "task_alt"),
            new IconMapEntry(nameof(PackIconKind.ChevronDown), PackIconKind.ChevronDown, "expand_more", "keyboard_arrow_down"),
            new IconMapEntry(nameof(PackIconKind.ChevronLeft), PackIconKind.ChevronLeft, "chevron_left"),
            new IconMapEntry(nameof(PackIconKind.ChevronRight), PackIconKind.ChevronRight, "chevron_right"),
            new IconMapEntry(nameof(PackIconKind.ChevronUp), PackIconKind.ChevronUp, "expand_less", "keyboard_arrow_up"),
            new IconMapEntry(nameof(PackIconKind.Clock), PackIconKind.Clock, "schedule", "timer"),
            new IconMapEntry(nameof(PackIconKind.Close), PackIconKind.Close, "close"),
            new IconMapEntry(nameof(PackIconKind.CloseCircleOutline), PackIconKind.CloseCircleOutline, "cancel", "close"),
            new IconMapEntry(nameof(PackIconKind.CodeJson), PackIconKind.CodeJson, "data_object", "code"),
            new IconMapEntry(nameof(PackIconKind.Cog), PackIconKind.Cog, "settings"),
            new IconMapEntry(nameof(PackIconKind.ContentCopy), PackIconKind.ContentCopy, "content_copy"),
            new IconMapEntry(nameof(PackIconKind.ContentSave), PackIconKind.ContentSave, "save"),
            new IconMapEntry(nameof(PackIconKind.Delete), PackIconKind.Delete, "delete"),
            new IconMapEntry(nameof(PackIconKind.DeleteForever), PackIconKind.DeleteForever, "delete_forever"),
            new IconMapEntry(nameof(PackIconKind.DeleteSweep), PackIconKind.DeleteSweep, "delete_sweep"),
            new IconMapEntry(nameof(PackIconKind.DotsGrid), PackIconKind.DotsGrid, "drag_indicator", "apps"),
            new IconMapEntry(nameof(PackIconKind.DotsVertical), PackIconKind.DotsVertical, "more_vert"),
            new IconMapEntry(nameof(PackIconKind.Download), PackIconKind.Download, "download", "file_download"),
            new IconMapEntry(nameof(PackIconKind.Edit), PackIconKind.Edit, "edit"),
            new IconMapEntry(nameof(PackIconKind.Export), PackIconKind.Export, "output", "upload", "exit_to_app"),
            new IconMapEntry(nameof(PackIconKind.FileExportOutline), PackIconKind.FileExportOutline, "upload_file", "output"),
            new IconMapEntry(nameof(PackIconKind.FileMultiple), PackIconKind.FileMultiple, "file_copy", "folder_copy"),
            new IconMapEntry(nameof(PackIconKind.FindReplace), PackIconKind.FindReplace, "find_replace"),
            new IconMapEntry(nameof(PackIconKind.Folder), PackIconKind.Folder, "folder"),
            new IconMapEntry(nameof(PackIconKind.FolderMusic), PackIconKind.FolderMusic, "library_music", "audio_file"),
            new IconMapEntry(nameof(PackIconKind.FolderOpen), PackIconKind.FolderOpen, "folder_open"),
            new IconMapEntry(nameof(PackIconKind.FolderSearch), PackIconKind.FolderSearch, "manage_search", "folder_open"),
            new IconMapEntry(nameof(PackIconKind.FormatAlignCenter), PackIconKind.FormatAlignCenter, "format_align_center"),
            new IconMapEntry(nameof(PackIconKind.FormatAlignJustify), PackIconKind.FormatAlignJustify, "format_align_justify"),
            new IconMapEntry(nameof(PackIconKind.FormatAlignLeft), PackIconKind.FormatAlignLeft, "format_align_left"),
            new IconMapEntry(nameof(PackIconKind.FormatAlignRight), PackIconKind.FormatAlignRight, "format_align_right"),
            new IconMapEntry(nameof(PackIconKind.FormatBold), PackIconKind.FormatBold, "format_bold"),
            new IconMapEntry(nameof(PackIconKind.FormatItalic), PackIconKind.FormatItalic, "format_italic"),
            new IconMapEntry(nameof(PackIconKind.FormatListNumbered), PackIconKind.FormatListNumbered, "format_list_numbered"),
            new IconMapEntry(nameof(PackIconKind.FormatUnderline), PackIconKind.FormatUnderline, "format_underlined"),
            new IconMapEntry(nameof(PackIconKind.FormTextbox), PackIconKind.FormTextbox, "terminal", "text_fields", "input"),
            new IconMapEntry(nameof(PackIconKind.Help), PackIconKind.Help, "help"),
            new IconMapEntry(nameof(PackIconKind.HelpCircle), PackIconKind.HelpCircle, "help", "contact_support"),
            new IconMapEntry(nameof(PackIconKind.History), PackIconKind.History, "history"),
            new IconMapEntry(nameof(PackIconKind.HumanHandsup), PackIconKind.HumanHandsup, "emoji_people", "accessibility_new", "waving_hand"),
            new IconMapEntry(nameof(PackIconKind.ImageFilterCenterFocus), PackIconKind.ImageFilterCenterFocus, "filter_center_focus", "center_focus_strong"),
            new IconMapEntry(nameof(PackIconKind.Import), PackIconKind.Import, "input", "download", "exit_to_app"),
            new IconMapEntry(nameof(PackIconKind.Information), PackIconKind.Information, "info"),
            new IconMapEntry(nameof(PackIconKind.KeyVariant), PackIconKind.KeyVariant, "key", "vpn_key"),
            new IconMapEntry(nameof(PackIconKind.LanDisconnect), PackIconKind.LanDisconnect, "cloud_off", "link_off", "signal_disconnected"),
            new IconMapEntry(nameof(PackIconKind.Launch), PackIconKind.Launch, "launch", "open_in_new"),
            new IconMapEntry(nameof(PackIconKind.Link), PackIconKind.Link, "link"),
            new IconMapEntry(nameof(PackIconKind.LockOpenOutline), PackIconKind.LockOpenOutline, "lock_open"),
            new IconMapEntry(nameof(PackIconKind.LockOutline), PackIconKind.LockOutline, "lock"),
            new IconMapEntry(nameof(PackIconKind.MessageText), PackIconKind.MessageText, "chat", "sms", "forum"),
            new IconMapEntry(nameof(PackIconKind.MusicNote), PackIconKind.MusicNote, "music_note"),
            new IconMapEntry(nameof(PackIconKind.MusicNotePlus), PackIconKind.MusicNotePlus, "music_note_add", "playlist_add"),
            new IconMapEntry(nameof(PackIconKind.OpenInNew), PackIconKind.OpenInNew, "open_in_new"),
            new IconMapEntry(nameof(PackIconKind.Pencil), PackIconKind.Pencil, "edit"),
            new IconMapEntry(nameof(PackIconKind.Pin), PackIconKind.Pin, "push_pin", "keep"),
            new IconMapEntry(nameof(PackIconKind.PinOff), PackIconKind.PinOff, "keep_off"),
            new IconMapEntry(nameof(PackIconKind.Play), PackIconKind.Play, "play_arrow"),
            new IconMapEntry(nameof(PackIconKind.PlaylistMusic), PackIconKind.PlaylistMusic, "queue_music"),
            new IconMapEntry(nameof(PackIconKind.PlaylistPlus), PackIconKind.PlaylistPlus, "playlist_add"),
            new IconMapEntry(nameof(PackIconKind.PlayPause), PackIconKind.PlayPause, "play_pause"),
            new IconMapEntry(nameof(PackIconKind.Plus), PackIconKind.Plus, "add"),
            new IconMapEntry(nameof(PackIconKind.Refresh), PackIconKind.Refresh, "refresh"),
            new IconMapEntry(nameof(PackIconKind.ReorderHorizontal), PackIconKind.ReorderHorizontal, "drag_handle", "reorder"),
            new IconMapEntry(nameof(PackIconKind.RepeatOff), PackIconKind.RepeatOff, "repeat", "repeat_on"),
            new IconMapEntry(nameof(PackIconKind.RepeatVariant), PackIconKind.RepeatVariant, "repeat_on", "repeat"),
            new IconMapEntry(nameof(PackIconKind.Replay), PackIconKind.Replay, "replay"),
            new IconMapEntry(nameof(PackIconKind.SatelliteUplink), PackIconKind.SatelliteUplink, "broadcast_on_personal", "satellite_alt", "cell_tower"),
            new IconMapEntry(nameof(PackIconKind.Search), PackIconKind.Search, "search"),
            new IconMapEntry(nameof(PackIconKind.SecurityAccount), PackIconKind.SecurityAccount, "admin_panel_settings", "shield_person", "verified_user"),
            new IconMapEntry(nameof(PackIconKind.Settings), PackIconKind.Settings, "settings"),
            new IconMapEntry(nameof(PackIconKind.ShieldOff), PackIconKind.ShieldOff, "remove_moderator", "gpp_bad", "shield"),
            new IconMapEntry(nameof(PackIconKind.ShieldStar), PackIconKind.ShieldStar, "local_police", "verified_user", "add_moderator"),
            new IconMapEntry(nameof(PackIconKind.Shuffle), PackIconKind.Shuffle, "shuffle_on", "shuffle"),
            new IconMapEntry(nameof(PackIconKind.ShuffleDisabled), PackIconKind.ShuffleDisabled, "shuffle", "trending_flat"),
            new IconMapEntry(nameof(PackIconKind.SkipNext), PackIconKind.SkipNext, "skip_next"),
            new IconMapEntry(nameof(PackIconKind.SkipPrevious), PackIconKind.SkipPrevious, "skip_previous"),
            new IconMapEntry(nameof(PackIconKind.Star), PackIconKind.Star, "star"),
            new IconMapEntry(nameof(PackIconKind.Stop), PackIconKind.Stop, "stop"),
            new IconMapEntry(nameof(PackIconKind.Sync), PackIconKind.Sync, "sync"),
            new IconMapEntry(nameof(PackIconKind.Television), PackIconKind.Television, "tv"),
            new IconMapEntry(nameof(PackIconKind.Timer), PackIconKind.Timer, "timer"),
            new IconMapEntry(nameof(PackIconKind.TooltipEdit), PackIconKind.TooltipEdit, "rate_review", "edit_note"),
            new IconMapEntry(nameof(PackIconKind.TreasureChest), PackIconKind.TreasureChest, "inventory_2", "redeem", "savings"),
            new IconMapEntry(nameof(PackIconKind.Warning), PackIconKind.Warning, "warning"),
            new IconMapEntry(nameof(PackIconKind.WindowRestore), PackIconKind.WindowRestore, "select_window", "open_in_full", "aspect_ratio"),
        };

        // Where each icon currently appears in the app (generated by scanning Kind=/Icon=/PackIconKind usages)
        private static readonly Dictionary<string, string> usages = new Dictionary<string, string>()
        {
            { "AccessPoint", "OBSStudio, Poly Pop" },
            { "AccountEdit", "User Dialog" },
            { "AccountKey", "Requirements Set, Users" },
            { "AccountMultiple", "Requirements Set" },
            { "Add", "Role Requirement" },
            { "AddBox", "Command Action Editor" },
            { "AlertCircle", "Login, Update" },
            { "AlertCircleOutline", "Missing Files Check" },
            { "AlertOutline", "Update" },
            { "ArrowBack", "Community Commands" },
            { "ArrowBackCircle", "Community Commands" },
            { "ArrowDown", "Game Queue, Game Queue Dashboard" },
            { "ArrowDownBold", "Action Editor Container, Overlay End Credits V3, Overlay Goal V3, Overlay Wheel V3" },
            { "ArrowForward", "Community Commands" },
            { "ArrowRight", "Update" },
            { "ArrowRightBold", "Community Commands" },
            { "ArrowUp", "Game Queue, Game Queue Dashboard" },
            { "ArrowUpBold", "Action Editor Container, Overlay End Credits V3, Overlay Goal V3, Overlay Wheel V3" },
            { "Bell", "Main Menu" },
            { "Biohazard", "User Dialog" },
            { "BlockHelper", "User Dialog" },
            { "Broom", "User Dialog" },
            { "Calendar", "Currency, Quote, Stream Pass" },
            { "Cancel", "Command History" },
            { "Cash100", "Redemption Store, Redemption Store Dashboard, Requirements Set, User Dialog, Users" },
            { "CheckboxMarkedCircle", "Redemption Store, Redemption Store Dashboard, Service Container" },
            { "CheckCircleOutline", "Missing Files Check" },
            { "ChevronDown", "Service Category" },
            { "ChevronLeft", "Overlay Editor Popout" },
            { "ChevronRight", "Overlay Editor Popout, Service Category" },
            { "ChevronUp", "Chat" },
            { "Clock", "Quote, Users" },
            { "Close", "Login, Overlay Editor Popout, Update, User Dialog, Webhooks" },
            { "CloseCircleOutline", "Main Menu, Missing Files Check, Music Player" },
            { "CodeJson", "Webhook Command Editor Details" },
            { "Cog", "Quote" },
            { "ContentCopy", "Action Editor Container, Overlay, Overlay Endpoints Update Dialog, Overlay Settings, Stream Pass, Sub Action Container" },
            { "ContentSave", "Command Editor, Currency, Game Command Editor, Inventory, Main Menu, Overlay Endpoint V3 Editor +3 more" },
            { "Delete", "Action Editor Container, Arguments Requirement, Bet Game Command Editor Details, Command Listing Buttons, Conditional Action Editor, Cooldown Requirement +35 more" },
            { "DeleteForever", "Quote" },
            { "DeleteSweep", "Music Player" },
            { "DotsGrid", "Main Menu" },
            { "DotsVertical", "Main Menu" },
            { "Download", "Community Command Listing Large, Community Command Listing Small, Community Commands" },
            { "Edit", "Command Action Editor" },
            { "Export", "Command Editor, Overlay Widget V3 Editor, Quote, Sub Action Container" },
            { "FileExportOutline", "Music Player" },
            { "FileMultiple", "Missing Files Check" },
            { "FindReplace", "Missing Files Check" },
            { "Folder", "Login" },
            { "FolderMusic", "Music Player" },
            { "FolderOpen", "Missing Files Check" },
            { "FolderSearch", "Missing Files Check" },
            { "FormatAlignCenter", "Overlay Discord Reactive Voice V3, Overlay End Credits V3, Overlay Label V3, Overlay Persistent Timer V3, Overlay Text V3, Overlay Timer V3" },
            { "FormatAlignJustify", "Overlay Discord Reactive Voice V3, Overlay End Credits V3, Overlay Label V3, Overlay Persistent Timer V3, Overlay Text V3, Overlay Timer V3" },
            { "FormatAlignLeft", "Overlay Discord Reactive Voice V3, Overlay End Credits V3, Overlay Label V3, Overlay Persistent Timer V3, Overlay Text V3, Overlay Timer V3" },
            { "FormatAlignRight", "Overlay Discord Reactive Voice V3, Overlay End Credits V3, Overlay Label V3, Overlay Persistent Timer V3, Overlay Text V3, Overlay Timer V3" },
            { "FormatBold", "Overlay Chat V3, Overlay Custom V3, Overlay Discord Reactive Voice V3, Overlay End Credits V3, Overlay Event List V3, Overlay Game Queue V3 +8 more" },
            { "FormatItalic", "Overlay Chat V3, Overlay Custom V3, Overlay Discord Reactive Voice V3, Overlay End Credits V3, Overlay Event List V3, Overlay Game Queue V3 +8 more" },
            { "FormatListNumbered", "Quote" },
            { "FormatUnderline", "Overlay Chat V3, Overlay Custom V3, Overlay Discord Reactive Voice V3, Overlay End Credits V3, Overlay Event List V3, Overlay Game Queue V3 +8 more" },
            { "FormTextbox", "Requirements Set" },
            { "Help", "Action Editor Container, Currency, Inventory, Main Menu, New User Wizard, Requirements Set +1 more" },
            { "HelpCircle", "Login" },
            { "History", "Overlay Widget V3 Editor" },
            { "HumanHandsup", "User Dialog" },
            { "ImageFilterCenterFocus", "Overlay Position V3" },
            { "Import", "Command Editor, Quote, Sub Action Container" },
            { "Information", "Login, Webhooks" },
            { "KeyVariant", "OBSStudio" },
            { "LanDisconnect", "Main Menu" },
            { "Launch", "Overlay, Overlay Endpoints Update Dialog, Overlay Settings" },
            { "Link", "Community Commands, Overlay Widgets, User Dialog" },
            { "LockOpenOutline", "Chat List" },
            { "LockOutline", "Chat List" },
            { "MessageText", "Command History" },
            { "MusicNote", "Music Player" },
            { "MusicNotePlus", "Music Player" },
            { "OpenInNew", "Kick Channel Points, Notification Center, Overlay Action Editor, Overlay Widget V3 Editor, Streamloots Cards, Twitch Channel Points" },
            { "Pencil", "Main Menu" },
            { "Pin", "Dashboard, Notification Center" },
            { "PinOff", "Dashboard" },
            { "Play", "Action Editor Container, Chat Commands, Command Editor, Command Listing Buttons, Notifications Settings, Overlay Widget V3 Editor" },
            { "PlaylistMusic", "Music Player" },
            { "PlaylistPlus", "Sub Action Container" },
            { "PlayPause", "Music Player" },
            { "Plus", "Command Editor, Sub Action Container" },
            { "Refresh", "Discord Action Editor, Missing Files Check, Mtion Studio Action Editor, TITSAction Editor, VTube Studio Action Editor" },
            { "ReorderHorizontal", "Music Player" },
            { "RepeatOff", "Music Player" },
            { "RepeatVariant", "Music Player" },
            { "Replay", "Command History" },
            { "SatelliteUplink", "Channel" },
            { "Search", "Users" },
            { "SecurityAccount", "User Dialog" },
            { "Settings", "Main Menu, Requirements Set" },
            { "ShieldOff", "User Dialog" },
            { "ShieldStar", "Requirements Set, User Dialog, Users" },
            { "Shuffle", "Music Player" },
            { "ShuffleDisabled", "Music Player" },
            { "SkipNext", "Music Player" },
            { "SkipPrevious", "Music Player" },
            { "Star", "Community Command Listing Large, Community Command Listing Small, Community Commands" },
            { "Stop", "Command Editor, Command Listing Buttons, Music Player" },
            { "Sync", "Overlay Widgets" },
            { "Television", "Channel" },
            { "Timer", "Chat Commands, Requirements Set, User Dialog" },
            { "TooltipEdit", "Command Listing Buttons, Currency Rank Inventory, Inventory, Overlay Settings, Overlay Widgets, Quick Commands Dashboard +3 more" },
            { "TreasureChest", "Requirements Set" },
            { "Warning", "Login" },
            { "WindowRestore", "Login" },
        };

        public IconMapControl()
        {
            InitializeComponent();

            foreach (IconMapEntry entry in Entries)
            {
                usages.TryGetValue(entry.KindName, out string usage);
                entry.Usage = usage;
            }

            this.MapItemsControl.ItemsSource = Entries;
        }
    }
}
