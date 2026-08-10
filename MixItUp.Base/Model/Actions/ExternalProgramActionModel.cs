using MixItUp.Base.Model.Commands;
using MixItUp.Base.Util;
using Newtonsoft.Json;
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

        public const int DefaultTimeoutSeconds = 30;

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
        [DataMember]
        public int TimeoutSeconds { get; set; }

        /// <summary>
        /// Actions saved before the timeout existed deserialize this as 0, so anything that is not a
        /// positive number falls back to the default rather than meaning no limit.
        /// </summary>
        [JsonIgnore]
        public int EffectiveTimeoutSeconds { get { return this.TimeoutSeconds > 0 ? this.TimeoutSeconds : DefaultTimeoutSeconds; } }

        public ExternalProgramActionModel(string filePath, string arguments, bool showWindow, bool shellExecute, bool waitForFinish, bool saveOutput, int timeoutSeconds)
            : base(ActionTypeEnum.ExternalProgram)
        {
            this.FilePath = filePath;
            this.Arguments = arguments;
            this.ShowWindow = showWindow;
            this.ShellExecute = shellExecute;
            this.WaitForFinish = waitForFinish;
            this.SaveOutput = saveOutput;
            this.TimeoutSeconds = timeoutSeconds;
        }

        [Obsolete]
        public ExternalProgramActionModel() { }

        protected override async Task PerformInternal(CommandParametersModel parameters)
        {
            List<string> output = new List<string>();
            // A timed out wait reads this while the readers are still running, so it cannot be touched
            // from one thread while another appends to it
            object outputLock = new object();

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
                        lock (outputLock)
                        {
                            output.Add(e.Data);
                        }
                    }
                };
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        lock (outputLock)
                        {
                            output.Add(e.Data);
                        }
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

                int timeoutSeconds = this.EffectiveTimeoutSeconds;
                DateTimeOffset giveUpAt = DateTimeOffset.Now.AddSeconds(timeoutSeconds);

                while (!process.HasExited && !parameters.ExitCommand)
                {
                    if (DateTimeOffset.Now >= giveUpAt)
                    {
                        // The process is left running on purpose. Killing someone's TTS engine or voice
                        // control app mid-sentence is worse than the wait giving up on it.
                        Logger.Log(LogLevel.Error, $"Command: {parameters.InitialCommandID} - External Program Action - {process.StartInfo.FileName} did not exit within {timeoutSeconds} seconds, continuing without it");
                        break;
                    }

                    await Task.Delay(500);
                }

                if (this.SaveOutput)
                {
                    if (process.HasExited)
                    {
                        // Exiting doesn't mean the readers have handed over everything they buffered.
                        // Only safe once it has actually exited, otherwise this waits forever.
                        await Task.Run(() => process.WaitForExit());
                    }

                    // Whatever was captured before giving up is still more use than nothing, and leaving
                    // the identifier unset would put a raw $externalprogramresult into whatever reads it
                    lock (outputLock)
                    {
                        parameters.SpecialIdentifiers[ExternalProgramActionModel.OutputSpecialIdentifier] = string.Join(Environment.NewLine, output);
                    }
                }
            }
        }
    }
}
