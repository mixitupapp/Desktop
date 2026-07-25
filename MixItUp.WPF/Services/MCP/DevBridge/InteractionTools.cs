#if !DEV_BRIDGE
#error The MCP dev bridge is being compiled without the DEV_BRIDGE constant. These files must only ever build under the Dev configuration. See Directory.Build.props.
#endif

using MixItUp.WPF.Services.MCP.Tools;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MixItUp.WPF.Services.MCP.DevBridge
{
    /// <summary>
    /// Pressing things, through the automation peer WPF already built for them.
    /// </summary>
    /// <remarks>
    /// <b>Why this exists.</b> 342 of this app's controls are wired with Command bindings and are
    /// reachable with ui_invoke, and another 247 are driven by a property a binding already watches, which
    /// ui_set can write. That leaves 191 Click handlers across 76 files that are private methods in
    /// code-behind, reachable by no other means -- and they are disproportionately the ones that matter,
    /// because every route to opening a command editor or a dialog in this app is one of them.
    /// <para>
    /// <b>Why no app change was needed.</b> WPF builds a default <see cref="AutomationPeer"/> for every
    /// control whether or not anyone asked for one, which is why a screen reader can already drive this
    /// app despite AutomationProperties appearing zero times in its 319 XAML files. Asking that peer to
    /// invoke raises the genuine Click routed event on the genuine control, so the existing private
    /// handler runs exactly as it would for a mouse click. Nothing here subclasses, reflects into, or
    /// modifies any control.
    /// </para>
    /// <para>
    /// <b>The honest caveat.</b> Peer invocation bypasses hit testing. It will press a control that is
    /// occluded, scrolled out of view, or sitting behind another element, which a person could not do. For
    /// a development loop that is a feature -- it is the same property that lets ui_screenshot render a
    /// window that is behind others -- but it means a successful press is not evidence that a user could
    /// reach the thing. IsOffscreen is reported for exactly that reason.
    /// </para>
    /// <para>
    /// <b>Presses are not synchronous and this tool is built around that.</b> WPF's peers do not run the
    /// click inline: ButtonAutomationPeer.Invoke posts to the dispatcher at
    /// <see cref="DispatcherPriority.Input"/> and returns. Observing side effects therefore happens in a
    /// second pass posted behind that one, after the gate has been released, which is also what keeps the
    /// gate from being held across a dialog the press opened.
    /// </para>
    /// </remarks>
    [McpServerToolType]
    public class InteractionTools
    {
        /// <summary>
        /// Words in a control's name or label that mean pressing it ends the app rather than driving it.
        /// </summary>
        /// <remarks>
        /// The counterpart to the member denylist on ui_invoke, and deliberately just as short. It is
        /// matched against the x:Name and the displayed text, which is all a peer press has to go on.
        /// <para>
        /// "Close" is <i>not</i> here, and that is a decision rather than an oversight. Closing an editor
        /// window is an ordinary part of a dev loop, and this app's main window has no in-tree close
        /// button at all -- its X is non-client chrome with no element behind it. The way a peer could
        /// close a window is the Window pattern's Close method, which this tool does not implement for
        /// that reason. Denying the word would only have cost false positives on things like
        /// CloseSettingsButton, which merely hides a panel.
        /// </para>
        /// </remarks>
        private static readonly string[] AppEndingTerms = new string[]
        {
            "shutdown", "exit", "quit", "restart",
        };

        [McpServerTool(Name = "ui_click", ReadOnly = false, Destructive = true)]
        [Description("Press a control in the running app through its UI Automation peer, which raises the real event so a private Click handler in code-behind runs exactly as it would for a mouse click. This is the only way to reach the 191 Click handlers in this app, including every route to opening a command editor or a dialog. Handles buttons, menu items, checkboxes, list and tab items, and expanders; the pattern is chosen automatically unless you name one. A disabled control comes back as 'cannot_execute' rather than being forced. Note that this bypasses hit testing, so it can press something occluded or scrolled out of view that a person could not reach, and a press succeeding is not evidence a user could do it. Windows and dialog overlays opened by the press are reported. Prefer ui_set for anything a binding already drives, notably main-menu navigation, which works even while the nav list is disabled.")]
        public static async Task<ClickResult> Click(
            [Description("What to press. Accepts a handle from a previous call (e12), an x:Name prefixed with # (#AddCommandButton), or a type name (IconButton). Omit to target the active window, which has nothing to press and is almost never what you want, so name the control.")] string element = null,
            [Description("Which automation pattern to use. 'auto' (default) picks the first the control supports, preferring invoke, then toggle, then select, then expand. Name one to disambiguate: a ToggleButton can be pressed either way and they are not the same thing. Values: auto, invoke, toggle, select, expand, collapse.")] string action = "auto",
            [Description("Report what the control supports and whether it could be pressed, without pressing it. Use this to ask why something is greyed out with no side effects.")] bool checkOnly = false)
        {
            ClickAction resolved = ParseAction(action);

            ClickResult result = await DevBridgeGate.RunOnUI("ui_click", () => Press(element, resolved, checkOnly));

            if (result.Status != DevBridgeStatus.Ok || !result.Pressed)
            {
                return result;
            }

            // Posted at Input priority, behind the click the peer just queued at the same priority, so
            // that by the time this runs the handler has had its turn. Deliberately a second gated call
            // rather than a nested pump inside the first: a press that opens a genuinely modal dialog
            // would otherwise hold the gate for as long as the dialog was up, and every other caller
            // would spend eight seconds waiting only to be told 'busy'.
            ObservationResult observed = await DevBridgeGate.RunOnUI(
                "ui_click",
                () =>
                {
                    ObservationResult observation = new ObservationResult();
                    SideEffects.Observe(observation, result.Snapshot);
                    return observation;
                },
                DispatcherPriority.Input);

            if (observed.Status == DevBridgeStatus.Ok)
            {
                result.WindowsOpened = observed.WindowsOpened;
                result.DialogsOpened = observed.DialogsOpened;
            }
            else
            {
                result.Message = $"The press was issued, but what it opened could not be read back: {observed.Status}: {observed.Message}";
            }

            result.Hint = BuildHint(result, observed.Status == DevBridgeStatus.Ok);
            result.Snapshot = null;

            return result;
        }

        /// <summary>
        /// Resolves the target, checks it can be pressed, and presses it. Runs on the UI thread.
        /// </summary>
        private static ClickResult Press(string element, ClickAction action, bool checkOnly)
        {
            ClickResult result = new ClickResult();

            DependencyObject target = UITools.ResolveTargetForInteraction(element, result);
            if (target == null)
            {
                return result;
            }

            result.Target = ValueRenderer.DescribeTarget(target);
            result.TargetHandle = HandleRegistry.Instance.HandleFor(target, "e");

            AutomationPeer peer = CreatePeer(target);
            if (peer == null)
            {
                result.Status = DevBridgeStatus.NotFound;
                result.Message = $"{result.Target} exists but WPF built no automation peer for it, so there is nothing to press. That is normal for a layout panel, a decoration, or a custom control that does not override OnCreateAutomationPeer. Press the control that owns it instead, or drive its view model with ui_invoke.";
                return result;
            }

            DescribePeer(result, peer);

            string denied = DeniedReason(target, result);
            if (denied != null)
            {
                result.Status = DevBridgeStatus.NotFound;
                result.Message = denied;
                return result;
            }

            // The pre-check that actually works in this app. Every CreateCommand call site here omits the
            // canExecute delegate, so ICommand.CanExecute is true everywhere and tells a caller nothing;
            // controls are disabled by XAML bindings to view model booleans instead, and IsEnabled on the
            // peer is what those bindings drive.
            if (result.IsEnabled != true)
            {
                result.Status = DevBridgeStatus.CannotExecute;
                result.Message = $"{result.Target} is disabled, so it was not pressed. This is the app declining rather than breaking. Nothing in this app disables a control through CanExecute, so the reason is a binding to a view model property: read the DataContext with ui_get and look for the flag it is bound to, usually something like IsNotLoading or a selection being empty.";
                return result;
            }

            if (!TrySelectPattern(result, peer, action))
            {
                return result;
            }

            if (checkOnly)
            {
                result.Message = $"The control is enabled and supports '{result.PatternUsed}', so it would be pressed. Not pressed, by request.";
                return result;
            }

            result.Snapshot = SideEffects.Capture();

            ToolHelpers.LogToolAction("ui_click", $"{result.Target} {result.PatternUsed}");

            try
            {
                InvokePattern(result.Pattern, result.PatternUsed);
                result.Pressed = true;
            }
            catch (System.Windows.Automation.ElementNotEnabledException)
            {
                // Backstop for the race between the IsEnabled check above and the press. A binding can
                // disable the control in between, and the peer throws rather than silently doing nothing.
                result.Status = DevBridgeStatus.CannotExecute;
                result.Message = $"{result.Target} was enabled when checked but disabled by the time it was pressed, so nothing happened. Something changed it in between. Re-read its state with ui_get.";
                return result;
            }
            catch (InvalidOperationException ex)
            {
                result.Status = DevBridgeStatus.Error;
                result.Message = $"The '{result.PatternUsed}' pattern refused: {ex.Message}. The control advertises the pattern but its parent would not accept the operation, which for a selection usually means the item is not in a selector that allows it.";
                return result;
            }

            return result;
        }

        /// <summary>
        /// The peer that can actually drive an element, or null when there is none.
        /// </summary>
        /// <remarks>
        /// Two factories, because they take different roots. Nearly everything pressable is a
        /// <see cref="UIElement"/>, but Hyperlink is a FrameworkContentElement and lives in the other
        /// half of the hierarchy, and a hyperlink is a real navigation control in this app rather than
        /// decoration.
        /// <para>
        /// <b>A row of a list needs the second lookup.</b> Asking a ListBoxItem, ListViewItem, TabItem or
        /// DataGridRow for its own peer does not get the peer that can select it: those types return a
        /// deliberately inert wrapper peer whose only job is to supply layout information to something
        /// else. The peer carrying SelectionItem is an <see cref="ItemAutomationPeer"/> that only the
        /// parent control's peer creates, and from outside it is reachable solely by asking that parent
        /// for its children. Without this a row reports no supported patterns at all, which reads as
        /// "nothing here to press" and is simply wrong.
        /// </para>
        /// </remarks>
        private static AutomationPeer CreatePeer(DependencyObject target)
        {
            AutomationPeer own = null;

            if (target is UIElement uiElement)
            {
                own = UIElementAutomationPeer.CreatePeerForElement(uiElement);
            }
            else if (target is System.Windows.ContentElement contentElement)
            {
                own = ContentElementAutomationPeer.CreatePeerForElement(contentElement);
            }

            // Only consulted when the element's own peer is inert, which keeps this off the path of
            // everything that already works. A MenuItem is both an item container and a real control with
            // a real peer, and it has to keep using its own.
            if (SupportsAnyPattern(own))
            {
                return own;
            }

            AutomationPeer asItem = TryCreateItemPeer(target);
            return SupportsAnyPattern(asItem) ? asItem : own;
        }

        /// <summary>
        /// The peer the parent items control builds for this row, or null when the element is not a row.
        /// </summary>
        private static AutomationPeer TryCreateItemPeer(DependencyObject target)
        {
            try
            {
                ItemsControl owner = ItemsControl.ItemsControlFromItemContainer(target);
                if (owner == null)
                {
                    return null;
                }

                object item = owner.ItemContainerGenerator.ItemFromContainer(target);
                if (item == DependencyProperty.UnsetValue)
                {
                    return null;
                }

                if (!(UIElementAutomationPeer.CreatePeerForElement(owner) is ItemsControlAutomationPeer ownerPeer))
                {
                    return null;
                }

                // Only realized rows appear here, which costs nothing: an unrealized one has no visual to
                // have been resolved from in the first place.
                List<AutomationPeer> children = ownerPeer.GetChildren();
                if (children == null)
                {
                    return null;
                }

                foreach (AutomationPeer child in children)
                {
                    if (child is ItemAutomationPeer itemPeer && ReferenceEquals(itemPeer.Item, item))
                    {
                        return itemPeer;
                    }
                }
            }
            catch (Exception)
            {
                // A peer that cannot be built is the same as not having one.
            }

            return null;
        }

        private static bool SupportsAnyPattern(AutomationPeer peer)
        {
            if (peer == null)
            {
                return false;
            }

            foreach (KeyValuePair<string, PatternInterface> known in KnownPatterns())
            {
                if (GetProvider(peer, known.Value) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void DescribePeer(ClickResult result, AutomationPeer peer)
        {
            result.ControlType = SafePeerRead(() => peer.GetAutomationControlType().ToString(), "Custom");

            // The peer's name is what a screen reader would announce. Worth reporting because this app
            // sets AutomationProperties nowhere, so it is whatever WPF inferred from the content, and a
            // control that comes back with an empty name here is one a screen reader cannot announce.
            result.AutomationName = SafePeerRead(() => peer.GetName(), null);
            result.IsEnabled = SafePeerRead(() => peer.IsEnabled() ? "y" : null, null) != null;
            result.IsOffscreen = SafePeerRead(() => peer.IsOffscreen() ? "y" : null, null) != null;

            List<string> supported = new List<string>();
            foreach (KeyValuePair<string, PatternInterface> known in KnownPatterns())
            {
                if (GetProvider(peer, known.Value) != null)
                {
                    supported.Add(known.Key);
                }
            }
            result.SupportedPatterns = supported;
        }

        /// <summary>
        /// Chooses the pattern to use and stashes the provider, or explains why there is none.
        /// </summary>
        private static bool TrySelectPattern(ClickResult result, AutomationPeer peer, ClickAction action)
        {
            if (action == ClickAction.Auto)
            {
                // Invoke first because a press is what a caller almost always means. Toggle before select
                // because a RadioButton reached directly is being set, not merely highlighted.
                foreach (KeyValuePair<string, PatternInterface> candidate in KnownPatterns())
                {
                    // Collapse shares its provider with expand and is never what 'auto' should pick: it
                    // would close an expander a caller was trying to open.
                    if (candidate.Key == "collapse")
                    {
                        continue;
                    }

                    object provider = GetProvider(peer, candidate.Value);
                    if (provider != null)
                    {
                        result.PatternUsed = candidate.Key;
                        result.Pattern = provider;
                        return true;
                    }
                }

                result.Status = DevBridgeStatus.NotFound;
                result.Message = $"{result.Target} has an automation peer but supports none of the patterns this tool can drive, so there is no press to make. A {result.ControlType} is usually a container or a display element rather than a control. Its supported patterns are: {DescribeSupported(result)}. Name the button inside it, or drive the underlying command with ui_invoke.";
                return false;
            }

            PatternInterface wanted = PatternFor(action);
            object requested = GetProvider(peer, wanted);
            if (requested == null)
            {
                result.Status = DevBridgeStatus.NotFound;
                result.Message = $"{result.Target} does not support the '{action.ToString().ToLowerInvariant()}' pattern. What it does support: {DescribeSupported(result)}. Pass one of those as 'action', or leave action unset to let it be chosen.";
                return false;
            }

            result.PatternUsed = action.ToString().ToLowerInvariant();
            result.Pattern = requested;
            return true;
        }

        private static void InvokePattern(object provider, string patternUsed)
        {
            switch (patternUsed)
            {
                case "invoke":
                    ((IInvokeProvider)provider).Invoke();
                    break;
                case "toggle":
                    ((IToggleProvider)provider).Toggle();
                    break;
                case "select":
                    ((ISelectionItemProvider)provider).Select();
                    break;
                case "expand":
                    ((IExpandCollapseProvider)provider).Expand();
                    break;
                case "collapse":
                    ((IExpandCollapseProvider)provider).Collapse();
                    break;
                default:
                    throw new InvalidOperationException($"'{patternUsed}' is not a pattern this tool drives.");
            }
        }

        /// <summary>
        /// The patterns this tool drives, in the order 'auto' prefers them.
        /// </summary>
        /// <remarks>
        /// <b>Value is absent on purpose.</b> IValueProvider would reach a TextBox's contents through a
        /// route that never names the property being read, which is what every redaction check in
        /// SecretGuard keys on. A PasswordBox does not expose the pattern, but plenty of API keys in this
        /// app are typed into ordinary TextBoxes, and a peer write echoed back would put one straight into
        /// a transcript. ui_set on Text does the same job through the guarded path, so the pattern buys
        /// nothing and costs a hole.
        /// </remarks>
        private static IEnumerable<KeyValuePair<string, PatternInterface>> KnownPatterns()
        {
            yield return new KeyValuePair<string, PatternInterface>("invoke", PatternInterface.Invoke);
            yield return new KeyValuePair<string, PatternInterface>("toggle", PatternInterface.Toggle);
            yield return new KeyValuePair<string, PatternInterface>("select", PatternInterface.SelectionItem);
            yield return new KeyValuePair<string, PatternInterface>("expand", PatternInterface.ExpandCollapse);
            yield return new KeyValuePair<string, PatternInterface>("collapse", PatternInterface.ExpandCollapse);
        }

        private static PatternInterface PatternFor(ClickAction action)
        {
            switch (action)
            {
                case ClickAction.Invoke: return PatternInterface.Invoke;
                case ClickAction.Toggle: return PatternInterface.Toggle;
                case ClickAction.Select: return PatternInterface.SelectionItem;
                default: return PatternInterface.ExpandCollapse;
            }
        }

        private static object GetProvider(AutomationPeer peer, PatternInterface pattern)
        {
            try
            {
                return peer.GetPattern(pattern);
            }
            catch (Exception)
            {
                // A peer whose GetPattern throws is a broken peer, not a supported pattern.
                return null;
            }
        }

        /// <summary>
        /// Why this control must not be pressed, or null when it may be.
        /// </summary>
        /// <remarks>
        /// Matched on the x:Name and the announced text, because a peer press has nothing else to go on:
        /// there is no member name to check the way ui_invoke can. Imprecise by nature, and it errs toward
        /// refusing, on the same reasoning as redaction -- a false positive costs one control that can be
        /// driven another way, a false negative ends the session the caller is working in.
        /// </remarks>
        private static string DeniedReason(DependencyObject target, ClickResult result)
        {
            string name = ElementDescriber.GetName(target);
            string label = result.AutomationName;

            foreach (string term in AppEndingTerms)
            {
                bool matched = (name != null && name.ToLowerInvariant().Contains(term))
                    || (label != null && label.ToLowerInvariant().Contains(term));

                if (matched)
                {
                    return $"{result.Target} reads as '{term}', which ends the app or the session rather than driving it, and would destroy your ability to observe the result. It was not pressed. Shut the app down from outside with CloseMainWindow instead, so settings are saved cleanly. If this is a false match on a control that does something else, drive it with ui_invoke or ui_set.";
                }
            }

            return null;
        }

        private static string DescribeSupported(ClickResult result)
        {
            return result.SupportedPatterns == null || result.SupportedPatterns.Count == 0
                ? "none"
                : string.Join(", ", result.SupportedPatterns);
        }

        private static string BuildHint(ClickResult result, bool observed)
        {
            // Worth saying unprompted. An agent that just opened a dropdown will reach for a screenshot
            // next, and the default capture mode structurally cannot show it, so the picture would come
            // back looking as though the press did nothing.
            if (result.PatternUsed == "expand")
            {
                return "The popup this opened is its own top-level window, so the default ui_screenshot mode cannot see it and the image would look unchanged. Pass mode='window' to capture it, or read the items with ui_get, which is cheaper.";
            }

            string opened = SideEffects.OpenedHint(result);
            if (opened != null)
            {
                return opened;
            }

            if (!observed)
            {
                return "The press was issued but its effect was not read back. Confirm with ui_screenshot or ui_list_windows.";
            }

            return "The press was delivered and nothing opened. A handler here can be async, so its work may still be in flight: confirm the effect with ui_screenshot or ui_get rather than assuming it finished.";
        }

        /// <summary>
        /// Reading a peer can throw for a control mid-teardown, and a failed description is never a reason
        /// to fail the press.
        /// </summary>
        private static string SafePeerRead(Func<string> read, string fallback)
        {
            try
            {
                return read();
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        private static ClickAction ParseAction(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                return ClickAction.Auto;
            }

            switch (action.Trim().ToLowerInvariant())
            {
                case "auto": return ClickAction.Auto;
                case "invoke": return ClickAction.Invoke;
                case "toggle": return ClickAction.Toggle;
                case "select": return ClickAction.Select;
                case "expand": return ClickAction.Expand;
                case "collapse": return ClickAction.Collapse;
                default:
                    throw new ModelContextProtocol.McpException($"'{action}' is not a known action. Use auto, invoke, toggle, select, expand, or collapse.");
            }
        }

        private enum ClickAction
        {
            Auto,
            Invoke,
            Toggle,
            Select,
            Expand,
            Collapse,
        }
    }

    /// <summary>
    /// The second pass, which only exists to carry what the press opened back out of the dispatcher.
    /// </summary>
    public class ObservationResult : ActionResult
    {
    }

    public class ClickResult : ActionResult
    {
        [Description("What the element reference resolved to.")]
        public string Target { get; set; }

        [Description("Handle for the pressed control, for a follow-up read.")]
        public string TargetHandle { get; set; }

        [Description("The control type UI Automation reports, for example Button, CheckBox, or TabItem.")]
        public string ControlType { get; set; }

        [Description("The name a screen reader would announce, which WPF infers from the control's content since this app sets AutomationProperties nowhere. Empty means the control is unannounceable, which is a real accessibility finding.")]
        public string AutomationName { get; set; }

        [Description("Whether the control was enabled. False means status is 'cannot_execute' and nothing was pressed. Absent when the element was never resolved, so it was never asked.")]
        public bool? IsEnabled { get; set; }

        [Description("Whether the control is scrolled out of view or otherwise not on screen. A peer press works anyway, which a real click would not, so treat this as a warning that a user could not have done what you just did. Absent when the element was never resolved.")]
        public bool? IsOffscreen { get; set; }

        [Description("Every pattern the control's peer offers that this tool can drive.")]
        public List<string> SupportedPatterns { get; set; }

        [Description("The pattern actually used. Worth reading: with action left at 'auto' this is not always the obvious one, and a ToggleButton pressed as 'invoke' and as 'toggle' do different things.")]
        public string PatternUsed { get; set; }

        [Description("Whether the press was issued. False with 'ok' means checkOnly was set.")]
        public bool Pressed { get; set; }

        /// <summary>
        /// Carried between the two passes rather than reported. Not serialized: the JSON is built from the
        /// declared properties, and this is cleared before returning regardless.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        internal UISnapshot Snapshot { get; set; }

        /// <summary>
        /// The chosen provider, held between selection and invocation within one dispatch.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        internal object Pattern { get; set; }
    }
}
