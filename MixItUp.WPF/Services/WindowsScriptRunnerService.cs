using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.WPF.Services
{
    public class WindowsScriptRunnerService : IScriptRunnerService
    {
        public async Task<string> RunCSharpCode(CommandParametersModel parameters, string code)
        {
            Assembly assembly = await this.CompileCSharpCode(parameters, code);
            if (assembly == null)
            {
                return null;
            }

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            return await AsyncRunner.RunAsyncBackground(async (cancellationToken) =>
            {
                try
                {
                    object o = assembly.CreateInstance("CustomNamespace.CustomClass");
                    MethodInfo mi = o.GetType().GetMethod("Run");
                    object result = mi.Invoke(o, null);
                    if (result != null)
                    {
                        return result.ToString();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                    await ServiceManager.Get<ChatService>().SendMessage(string.Format(MixItUp.Base.Resources.ScriptActionFailedCompile, ex.ToString()), parameters.Platform);
                }
                return null;
            }, cancellationTokenSource.Token);
        }

        public async Task<string> RunVisualBasicCode(CommandParametersModel parameters, string code)
        {
            await Task.CompletedTask;
            return null;
        }

        private async Task<Assembly> CompileCSharpCode(CommandParametersModel parameters, string code)
        {
            try
            {
                var references = new MetadataReference[]
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                    MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                    MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location)
                };

                var compilation = CSharpCompilation.Create(
                    assemblyName: Path.GetRandomFileName(),
                    syntaxTrees: new[] { CSharpSyntaxTree.ParseText(code) },
                    references: references,
                    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                using (var memoryStream = new MemoryStream())
                {
                    EmitResult result = compilation.Emit(memoryStream);

                    if (!result.Success)
                    {
                        var errors = result.Diagnostics
                            .Where(d => d.Severity == DiagnosticSeverity.Error)
                            .Select(d => $"Line {d.Location.GetLineSpan().StartLinePosition.Line + 1}: {d.GetMessage()}");

                        string fullError = string.Join(Environment.NewLine, errors);
                        Logger.Log(LogLevel.Error, $"Script compilation failed: {fullError}");

                        await ServiceManager.Get<ChatService>().SendMessage(
                            string.Format(MixItUp.Base.Resources.ScriptActionFailedCompile, errors.First()),
                            parameters.Platform);
                        return null;
                    }

                    memoryStream.Seek(0, SeekOrigin.Begin);
                    return Assembly.Load(memoryStream.ToArray());
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return null;
        }
    }
}