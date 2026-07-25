#if !DEV_BRIDGE
#error The MCP dev bridge is being compiled without the DEV_BRIDGE constant. These files must only ever build under the Dev configuration. See Directory.Build.props.
#endif

using MixItUp.Base.Util;
using MixItUp.WPF.Services.MCP.Tools;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MixItUp.WPF.Services.MCP.DevBridge
{
    /// <summary>
    /// Running a command or a method on something in the live app.
    /// </summary>
    /// <remarks>
    /// <b>CanExecute is checked first and a refusal is reported as its own status.</b> That distinction is
    /// most of the value here: "the button is greyed out" is visible in a screenshot, but the reason lives
    /// in a view model's canExecute closure, which UI Automation has no concept of and therefore
    /// structurally cannot report.
    /// <para>
    /// <b>This is where re-entrancy stops being theoretical.</b> An invoked command can open a modal
    /// dialog, that dialog pumps messages, and a nested pump will dispatch a second tool call while the
    /// first is still on the stack. DialogHelper.ShowMessage and ShowCustom are both reachable from the
    /// command paths this tool reaches. <see cref="DevBridgeGate"/> serializes calls for exactly this
    /// reason and is not bypassed here.
    /// </para>
    /// <para>
    /// <b>Nothing is awaited.</b> UIViewModelCommand.Execute is async void, so there is no task to wait on
    /// even in principle, and a command that opens a window would deadlock a caller that tried. The tool
    /// reports what it started and leaves the caller to observe the consequences with ui_screenshot or
    /// ui_list_windows, which is also the shape that keeps the gate from being held across a dialog.
    /// </para>
    /// </remarks>
    [McpServerToolType]
    public class InvokeTools
    {
        /// <summary>
        /// Members that end the process or the session rather than driving the app.
        /// </summary>
        /// <remarks>
        /// A deliberately short list. A broad "dangerous name" denylist would be wrong in both directions,
        /// blocking the navigation methods this exists to reach while giving no real protection, since the
        /// genuine gate on all of this is that the bridge is not compiled into any non-Dev build.
        /// <para>
        /// What these have in common is that they destroy the caller's own ability to observe the result,
        /// and that the app has a graceful shutdown path which none of them are. Killing the process
        /// abruptly also risks settings corruption, which is a real cost rather than a hypothetical one.
        /// </para>
        /// </remarks>
        private static readonly string[] DeniedMemberNames = new string[]
        {
            "Shutdown", "Restart", "Exit", "Kill", "Dispose", "Finalize", "TerminateProcess",
        };

        [McpServerTool(Name = "ui_invoke", ReadOnly = false, Destructive = true)]
        [Description("Run an ICommand or call a public method on something in the running app. CanExecute is checked first, and a refusal comes back as status 'cannot_execute' rather than as a failure, which is how you find out why a control is disabled. Nothing is awaited: commands here are async void, so this reports what it started and you observe the result with ui_screenshot or ui_list_windows afterwards. Windows that opened during the call are listed. Prefer ui_set for state that flows through a binding, which in this app is most of it.")]
        public static Task<InvokeResult> Invoke(
            [Description("What to invoke on. Accepts a handle from a previous call ('e12' for an element, 'd5' for a view model), an x:Name prefixed with # (#MenuItemsListBox), or a type name (ChatControl). Omit to use the active window, falling back to the main window. A 'd' view model handle is usually the right target, since that is where commands live.")] string element = null,
            [Description("What to run, as a dotted path. The last segment is either a property holding an ICommand or a public method name: 'SaveCommand', 'DataContext.DeleteCommand', 'ScrollIntoView'. Required.")] string member = null,
            [Description("Arguments, as text, converted to each parameter's declared type. For an ICommand this is the single Execute parameter. For a method, pass one entry per parameter. Omit for a parameterless call.")] string[] args = null,
            [Description("Report whether the command would run, without running it. Use this to ask why a control is disabled without side effects. Ignored for methods, which have no CanExecute to consult.")] bool checkOnly = false)
        {
            if (string.IsNullOrWhiteSpace(member))
            {
                throw new ModelContextProtocol.McpException("The 'member' parameter is required. Pass the name of an ICommand property or a public method, for example 'SaveCommand' or 'ScrollIntoView'.");
            }

            return DevBridgeGate.RunOnUI("ui_invoke", () =>
            {
                InvokeResult result = new InvokeResult();

                object target = UITools.ResolveObjectTarget(element, result);
                if (target == null)
                {
                    return result;
                }

                result.Target = ValueRenderer.DescribeTarget(target);
                result.Member = member.Trim();

                string finalName = FinalSegment(result.Member);
                if (IsDenied(finalName))
                {
                    result.Status = DevBridgeStatus.NotFound;
                    result.Message = $"'{finalName}' is not reachable through this tool. It ends the process or the session rather than driving the app, which would also destroy your ability to observe the result. Shut the app down from outside instead, with CloseMainWindow so settings are saved cleanly.";
                    return result;
                }

                // Resolve the owner first, so a method can be found even though it is not a property and
                // therefore not something the path walker can land on.
                object owner = target;
                string ownerPath = ParentPath(result.Member);

                if (ownerPath != null)
                {
                    PathResolution ownerResolution = PropertyPath.Resolve(target, ownerPath);
                    if (!ownerResolution.IsOk)
                    {
                        result.Status = ownerResolution.Status;
                        result.Message = ownerResolution.Message;
                        return result;
                    }
                    if (ownerResolution.Value == null)
                    {
                        result.Status = DevBridgeStatus.NotFound;
                        result.Message = $"'{ownerPath}' is null, so '{finalName}' cannot be reached on it.";
                        return result;
                    }
                    owner = ownerResolution.Value;
                    result.Owner = ValueRenderer.DescribeTarget(owner);
                }

                // A command is looked for before a method, because a view model that exposes both a
                // SaveCommand property and a Save method means the command: it is what the button is bound
                // to, so it is what a click would have run.
                MemberInfo commandMember = PropertyPath.FindMember(owner.GetType(), finalName);
                if (commandMember != null)
                {
                    object candidate = ValueRenderer.TryRead(commandMember, owner);
                    if (candidate is ICommand command)
                    {
                        return InvokeCommand(result, command, args, checkOnly);
                    }

                    if (candidate == null && LooksLikeCommandMember(commandMember))
                    {
                        result.Status = DevBridgeStatus.NotFound;
                        result.Message = $"'{owner.GetType().Name}.{finalName}' is an ICommand property but is null, so nothing is wired to it yet.";
                        return result;
                    }
                }

                return InvokeMethod(result, owner, finalName, args);
            });
        }

        private static InvokeResult InvokeCommand(InvokeResult result, ICommand command, string[] args, bool checkOnly)
        {
            result.MemberKind = "command";
            result.MemberType = command.GetType().Name;

            object parameter = args != null && args.Length > 0 ? args[0] : null;

            if (args != null && args.Length > 1)
            {
                result.Message = $"An ICommand takes a single parameter, so only the first of the {args.Length} arguments supplied was used.";
            }

            try
            {
                result.CanExecute = command.CanExecute(parameter);
            }
            catch (Exception ex)
            {
                result.Status = DevBridgeStatus.Error;
                result.Message = $"CanExecute threw {ex.GetType().Name}: {ex.Message}";
                return result;
            }

            if (!result.CanExecute)
            {
                result.Status = DevBridgeStatus.CannotExecute;
                result.Message = "The command refused to run because CanExecute returned false. This is the app declining, not a failure. The reason is in the view model's own condition, so read its properties with ui_get: the usual causes here are nothing selected, a required field empty, or another operation already running.";
                return result;
            }

            if (checkOnly)
            {
                result.Message = "CanExecute is true, so this command would run. Not executed, by request.";
                return result;
            }

            UISnapshot before = SideEffects.Capture();

            ToolHelpers.LogToolAction("ui_invoke", $"{result.Target} command {result.Member}{DescribeArgs(args)}");

            try
            {
                // Execute on UIViewModelCommand is async void, so this returns as soon as the command hits
                // its first await. Anything it does after that has not happened yet when this returns,
                // which is why the result reports what was started rather than what it produced.
                command.Execute(parameter);
                result.Executed = true;
            }
            catch (Exception ex)
            {
                result.Status = DevBridgeStatus.Error;
                result.Message = $"Execute threw {ex.GetType().Name}: {ex.Message}";
                return result;
            }

            result.IsRunning = ReadIsRunning(command);
            SideEffects.Observe(result, before);

            result.Hint = BuildHint(result);
            return result;
        }

        private static InvokeResult InvokeMethod(InvokeResult result, object owner, string name, string[] args)
        {
            int argumentCount = args?.Length ?? 0;

            MethodInfo method = FindMethod(owner.GetType(), name, argumentCount, out string findError);
            if (method == null)
            {
                result.Status = DevBridgeStatus.NotFound;
                result.Message = findError;
                return result;
            }

            // Close is allowed in general, because closing an editor window is an ordinary part of a dev
            // loop and it is the graceful path rather than a kill. Closing the main window is not: it ends
            // the app, and with it the caller's ability to see whether that was a good idea. Denied
            // specifically rather than by adding Close to the denylist, which would block the useful case.
            if (string.Equals(name, "Close", StringComparison.OrdinalIgnoreCase)
                && Application.Current != null
                && ReferenceEquals(owner, Application.Current.MainWindow))
            {
                result.Status = DevBridgeStatus.NotFound;
                result.Message = "Closing the main window would shut the app down and end this session. Close an editor window instead, or shut the app down from outside with CloseMainWindow so settings are saved cleanly.";
                return result;
            }

            result.MemberKind = "method";
            result.MemberType = ValueRenderer.DescribeType(method.ReturnType);

            ParameterInfo[] parameters = method.GetParameters();
            object[] values = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                if (i < argumentCount)
                {
                    if (!PropertyPath.TryCoerce(args[i], parameters[i].ParameterType, isNull: false, out object coerced, out string error))
                    {
                        result.Status = DevBridgeStatus.CoercionFailed;
                        result.Message = $"Argument {i} ('{parameters[i].Name}'): {error}";
                        return result;
                    }
                    values[i] = coerced;
                }
                else if (parameters[i].HasDefaultValue)
                {
                    values[i] = parameters[i].DefaultValue;
                }
                else
                {
                    result.Status = DevBridgeStatus.CoercionFailed;
                    result.Message = $"'{name}' needs a value for parameter {i} ('{parameters[i].Name}', {ValueRenderer.DescribeType(parameters[i].ParameterType)}), which has no default.";
                    return result;
                }
            }

            // A method has no CanExecute to consult, so there is nothing to refuse. Reported as true so a
            // caller reading the field uniformly is not misled into thinking it was blocked.
            result.CanExecute = true;

            UISnapshot before = SideEffects.Capture();

            ToolHelpers.LogToolAction("ui_invoke", $"{result.Target} method {result.Member}{DescribeArgs(args)}");

            object returned;
            try
            {
                returned = method.Invoke(owner, values);
                result.Executed = true;
            }
            catch (TargetInvocationException ex)
            {
                Exception inner = ex.InnerException ?? ex;
                result.Status = DevBridgeStatus.Error;
                result.Message = $"'{name}' threw {inner.GetType().Name}: {inner.Message}";
                return result;
            }
            catch (Exception ex)
            {
                result.Status = DevBridgeStatus.Error;
                result.Message = $"Invoking '{name}' threw {ex.GetType().Name}: {ex.Message}";
                return result;
            }

            if (returned is Task task)
            {
                result.ReturnedTask = true;

                // Not awaited, for the same reason a command is not: the gate is held for the duration of
                // this call and a method that opens a dialog would wedge it. An unobserved task exception
                // would otherwise be swallowed entirely, so it is at least logged.
                task.ContinueWith(
                    faulted => Logger.ForceLog(LogLevel.Warning, $"MCP ui_invoke: '{name}' faulted after returning: {faulted.Exception?.GetBaseException().Message}"),
                    TaskContinuationOptions.OnlyOnFaulted);

                result.ReturnValue = task.IsCompleted ? "Task (already completed)" : "Task (still running, not awaited)";
            }
            else if (method.ReturnType != typeof(void))
            {
                result.ReturnValue = SecretGuard.ShouldRedact(owner.GetType(), name)
                    ? SecretGuard.RedactedText
                    : ValueRenderer.Summarize(returned);

                if (ValueRenderer.DeservesHandle(returned))
                {
                    result.ReturnHandle = HandleRegistry.Instance.HandleFor(returned, ValueRenderer.HandlePrefix(returned));
                }
            }

            SideEffects.Observe(result, before);
            result.Hint = BuildHint(result);
            return result;
        }

        /// <summary>
        /// Finds a callable method, preferring an exact arity match.
        /// </summary>
        private static MethodInfo FindMethod(Type type, string name, int argumentCount, out string error)
        {
            error = null;

            List<MethodInfo> named = new List<MethodInfo>();
            foreach (MethodInfo candidate in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                // A property's own accessors are reachable as get_X and set_X, which are not what anyone
                // means and would bypass the property tools' redaction.
                if (candidate.IsSpecialName || candidate.IsGenericMethodDefinition)
                {
                    continue;
                }
                if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    named.Add(candidate);
                }
            }

            if (named.Count == 0)
            {
                error = $"'{type.Name}' has no public property, field, or method named '{name}'. Read the object with ui_get to list its properties, which is where commands live.";
                return null;
            }

            MethodInfo best = null;
            foreach (MethodInfo candidate in named)
            {
                ParameterInfo[] parameters = candidate.GetParameters();
                int required = 0;
                foreach (ParameterInfo parameter in parameters)
                {
                    if (!parameter.HasDefaultValue)
                    {
                        required++;
                    }
                }

                if (argumentCount >= required && argumentCount <= parameters.Length)
                {
                    // An exact match wins over one relying on defaults.
                    if (best == null || parameters.Length == argumentCount)
                    {
                        best = candidate;
                    }
                }
            }

            if (best == null)
            {
                List<string> signatures = new List<string>();
                foreach (MethodInfo candidate in named)
                {
                    List<string> parameterText = new List<string>();
                    foreach (ParameterInfo parameter in candidate.GetParameters())
                    {
                        parameterText.Add($"{ValueRenderer.DescribeType(parameter.ParameterType)} {parameter.Name}");
                    }
                    signatures.Add($"{candidate.Name}({string.Join(", ", parameterText)})");
                }

                error = $"'{name}' exists on {type.Name} but not with {argumentCount} argument(s). Overloads: {string.Join("; ", signatures)}.";
                return null;
            }

            return best;
        }

        /// <summary>
        /// Whether a member is declared as an ICommand, so a null value means unwired rather than absent.
        /// </summary>
        private static bool LooksLikeCommandMember(MemberInfo member)
        {
            Type declared = (member as PropertyInfo)?.PropertyType ?? (member as FieldInfo)?.FieldType;
            return declared != null && typeof(ICommand).IsAssignableFrom(declared);
        }

        /// <summary>
        /// UIViewModelCommand tracks whether its work is still in flight, which is the closest thing to
        /// completion feedback available for an async void command.
        /// </summary>
        private static bool? ReadIsRunning(ICommand command)
        {
            MemberInfo member = PropertyPath.FindMember(command.GetType(), "IsRunning");
            return ValueRenderer.TryRead(member, command) as bool?;
        }

        private static bool IsDenied(string name)
        {
            foreach (string denied in DeniedMemberNames)
            {
                if (string.Equals(name, denied, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static string BuildHint(InvokeResult result)
        {
            List<string> hints = new List<string>();

            string opened = SideEffects.OpenedHint(result);
            if (opened != null)
            {
                hints.Add(opened);
            }

            if (result.IsRunning == true)
            {
                hints.Add("The command is still running. Re-read state before acting on the result.");
            }
            else if (result.ReturnedTask)
            {
                hints.Add("The method returned a Task that was not awaited. Re-read state to see whether it has finished.");
            }
            else if (result.Executed)
            {
                hints.Add("Nothing here is awaited, so confirm the effect with ui_screenshot or ui_get rather than assuming it completed.");
            }

            return hints.Count > 0 ? string.Join(" ", hints) : null;
        }

        private static string DescribeArgs(string[] args)
        {
            return args == null || args.Length == 0 ? " (no args)" : $" ({args.Length} arg(s))";
        }

        private static string FinalSegment(string path)
        {
            int dot = path.LastIndexOf('.');
            return dot < 0 ? path : path.Substring(dot + 1).Trim();
        }

        private static string ParentPath(string path)
        {
            int dot = path.LastIndexOf('.');
            return dot <= 0 ? null : path.Substring(0, dot);
        }
    }

    public class InvokeResult : ActionResult
    {
        [Description("What the element reference resolved to.")]
        public string Target { get; set; }

        [Description("The member path that was invoked, echoed back.")]
        public string Member { get; set; }

        [Description("What the member was found on, when the path walked past the target to get there.")]
        public string Owner { get; set; }

        [Description("Whether this resolved to an 'command' or a 'method'.")]
        public string MemberKind { get; set; }

        [Description("The ICommand's implementing type, or the method's return type.")]
        public string MemberType { get; set; }

        [Description("What CanExecute returned. Always true for a method, which has none. When false the status is 'cannot_execute' and nothing ran.")]
        public bool CanExecute { get; set; }

        [Description("Whether the call was actually made. False with status 'cannot_execute' means the app declined; false with 'ok' means checkOnly was set.")]
        public bool Executed { get; set; }

        [Description("For a UIViewModelCommand, whether its work is still in flight. Null when the command does not report it. True means the effect has not landed yet.")]
        public bool? IsRunning { get; set; }

        [Description("Whether a method returned a Task. It was not awaited, so its work may still be in progress.")]
        public bool ReturnedTask { get; set; }

        [Description("A method's return value as one line. Absent for void and for commands, which return nothing.")]
        public string ReturnValue { get; set; }

        [Description("Handle for the returned value, when it is an object worth reaching into.")]
        public string ReturnHandle { get; set; }
    }
}
