using MixItUp.Base.Model.Commands;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Actions
{
    [DataContract]
    public class ExternalProgramActionModel : ActionModelBase
    {
        public const string OutputSpecialIdentifier = "externalprogramresult";

        [DataMember]
        public string FilePath { get; set; }

        [DataMember]
        public string Arguments { get; set; }

        [DataMember]
        public bool ShowWindow { get; set; }
        [DataMember]
        public bool ShellExecute { get; set; }
        [DataMember]
        public bool WaitForFinish { get; set; }
        [DataMember]
        public bool SaveOutput { get; set; }

        public ExternalProgramActionModel(string filePath, string arguments, bool showWindow, bool shellExecute, bool waitForFinish, bool saveOutput)
            : base(ActionTypeEnum.ExternalProgram)
        {
            this.FilePath = filePath;
            this.Arguments = arguments;
            this.ShowWindow = showWindow;
            this.ShellExecute = shellExecute;
            this.WaitForFinish = waitForFinish;
            this.SaveOutput = saveOutput;
        }

        [Obsolete]
        public ExternalProgramActionModel() { }

        protected override async Task PerformInternal(CommandParametersModel parameters)
        {
            List<string> output = new List<string>();

            Process process = new Process();
            process.StartInfo.FileName = await ReplaceStringWithSpecialModifiers(this.FilePath, parameters);
            process.StartInfo.Arguments = await ReplaceStringWithSpecialModifiers(this.Arguments, parameters);
            process.StartInfo.WorkingDirectory = Path.GetDirectoryName(process.StartInfo.FileName);
            process.StartInfo.CreateNoWindow = !this.ShowWindow;
            process.StartInfo.WindowStyle = (!this.ShowWindow) ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal;
            process.StartInfo.UseShellExecute = this.ShellExecute;
            if (this.WaitForFinish && this.SaveOutput)
            {
                process.StartInfo.RedirectStandardOutput = true;
                // Without an explicit encoding, redirected output is decoded with the system ANSI code page,
                // which garbles anything non-ASCII that the program wrote as UTF-8.
                process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.Add(e.Data);
                    }
                };
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.Add(e.Data);
                    }
                };
            }

            process.Start();
            if (this.WaitForFinish)
            {
                if (this.SaveOutput)
                {
                    process.BeginOutputReadLine();
                    // Error output has to be drained too, otherwise a program that writes enough of it fills the
                    // pipe buffer and blocks waiting for someone to read.
                    process.BeginErrorReadLine();
                }

                while (!process.HasExited)
                {
                    await Task.Delay(500);
                }

                if (this.SaveOutput)
                {
                    // Exiting doesn't mean the readers have handed over everything they buffered.
                    await Task.Run(() => process.WaitForExit());

                    parameters.SpecialIdentifiers[ExternalProgramActionModel.OutputSpecialIdentifier] = string.Join(Environment.NewLine, output);
                }
            }
        }
    }
}
