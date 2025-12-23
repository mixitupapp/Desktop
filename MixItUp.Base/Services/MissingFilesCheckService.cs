using MixItUp.Base.Model.Actions;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Overlay;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MixItUp.Base.Util;

namespace MixItUp.Base.Services
{
    public enum MissingFilesCheckStatus
    {
        Valid,
        Invalid,
        Warning
    }

    public class MissingFilesCheckReference
    {
        public CommandModelBase Command { get; set; }
        public ActionModelBase Action { get; set; }
        public string ActionTypeName { get; set; }
        public string PropertyName { get; set; }
        public string FilePath { get; set; }
        public MissingFilesCheckStatus Status { get; set; }
        public Action<string> UpdatePath { get; set; }

        public string CommandName => Command?.Name ?? "Unknown";

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(FilePath))
            {
                Status = MissingFilesCheckStatus.Invalid;
                return;
            }

            string path = FilePath;
            if (path.Contains("|"))
            {
                string[] paths = path.Split('|');
                foreach (string p in paths)
                {
                    var result = GetPathStatus(p.Trim());
                    if (result == MissingFilesCheckStatus.Invalid)
                    {
                        Status = MissingFilesCheckStatus.Invalid;
                        return;
                    }
                    if (result == MissingFilesCheckStatus.Warning)
                    {
                        Status = MissingFilesCheckStatus.Warning;
                    }
                }
                if (Status != MissingFilesCheckStatus.Warning)
                {
                    Status = MissingFilesCheckStatus.Valid;
                }
            }
            else
            {
                Status = GetPathStatus(path);
            }
        }

        private MissingFilesCheckStatus GetPathStatus(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return MissingFilesCheckStatus.Invalid;
            }

            if (path.Contains("$"))
            {
                return MissingFilesCheckStatus.Warning;
            }

            if (ServiceManager.Get<IFileService>().IsURLPath(path))
            {
                return MissingFilesCheckStatus.Valid;
            }

            if (Directory.Exists(path))
            {
                return MissingFilesCheckStatus.Valid;
            }

            if (File.Exists(path))
            {
                return MissingFilesCheckStatus.Valid;
            }

            return MissingFilesCheckStatus.Invalid;
        }
    }

    public class MissingFilesCheckService
    {
        public List<MissingFilesCheckReference> GetAllFilePaths()
        {
            List<MissingFilesCheckReference> references = new List<MissingFilesCheckReference>();

            foreach (CommandModelBase command in ServiceManager.Get<CommandService>().AllCommands)
            {
                ScanActions(command, command.Actions, references);
            }

            return references;
        }

        public Dictionary<string, string> GetFileLookup(string folderPath)
        {
            Dictionary<string, string> fileLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(folderPath))
            {
                try
                {
                    string[] foundFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
                    foreach (string file in foundFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        if (!fileLookup.ContainsKey(fileName))
                        {
                            fileLookup[fileName] = file;
                        }
                    }
                }
                catch (Exception ex) 
                { 
                    Logger.Log(ex); 
                }
            }
            return fileLookup;
        }

        private void ScanActions(CommandModelBase command, IEnumerable<ActionModelBase> actions, List<MissingFilesCheckReference> references)
        {
            foreach (ActionModelBase action in actions)
            {
                switch (action)
                {
                    case SoundActionModel soundAction:
                        AddReference(references, command, action, "Sound", "FilePath", soundAction.FilePath, p => soundAction.FilePath = p);
                        break;

                    case FileActionModel fileAction:
                        AddReference(references, command, action, "File", "FilePath", fileAction.FilePath, p => fileAction.FilePath = p);
                        break;

                    case ExternalProgramActionModel externalAction:
                        AddReference(references, command, action, "External Program", "FilePath", externalAction.FilePath, p => externalAction.FilePath = p);
                        break;

                    case StreamingSoftwareActionModel streamingAction:
                        AddReference(references, command, action, "Streaming Software", "SourceURL", streamingAction.SourceURL, p => streamingAction.SourceURL = p);
                        AddReference(references, command, action, "Streaming Software", "SourceTextFilePath", streamingAction.SourceTextFilePath, p => streamingAction.SourceTextFilePath = p);
                        break;

                    case MusicPlayerActionModel musicAction:
                        AddReference(references, command, action, "Music Player", "FolderPath", musicAction.FolderPath, p => musicAction.FolderPath = p);
                        break;

                    case DiscordActionModel discordAction:
                        if (discordAction.ActionType == DiscordActionTypeEnum.SendMessage)
                        {
                            AddReference(references, command, action, "Discord", "FilePath", discordAction.FilePath, p => discordAction.FilePath = p);
                        }
                        break;

                    case VTSPogActionModel vtsPogAction:
                        if (vtsPogAction.ActionType == VTSPogActionTypeEnum.PlayAudioFile)
                        {
                            AddReference(references, command, action, "VTS Pog", "AudioFilePath", vtsPogAction.AudioFilePath, p => vtsPogAction.AudioFilePath = p);
                        }
                        break;

                    case OverlayActionModel overlayAction:
                        if (overlayAction.OverlayItemV3 != null)
                        {
                            ScanOverlayItem(command, action, overlayAction.OverlayItemV3, references);
                        }
                        break;

                    case GroupActionModel groupAction:
                        ScanActions(command, groupAction.Actions, references);
                        break;
                }
            }
        }

        private void ScanOverlayItem(CommandModelBase command, ActionModelBase action, OverlayItemV3ModelBase overlayItem, List<MissingFilesCheckReference> references)
        {
            switch (overlayItem)
            {
                case OverlayVideoV3Model videoItem:
                    AddReference(references, command, action, "Overlay Video", "FilePath", videoItem.FilePath, p => videoItem.FilePath = p);
                    break;

                case OverlaySoundV3Model soundItem:
                    AddReference(references, command, action, "Overlay Sound", "FilePath", soundItem.FilePath, p => soundItem.FilePath = p);
                    break;

                case OverlayImageV3Model imageItem:
                    AddReference(references, command, action, "Overlay Image", "FilePath", imageItem.FilePath, p => imageItem.FilePath = p);
                    break;
            }
        }

        private void AddReference(List<MissingFilesCheckReference> references, CommandModelBase command, ActionModelBase action, string actionTypeName, string propertyName, string filePath, Action<string> updateAction)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                var reference = new MissingFilesCheckReference
                {
                    Command = command,
                    Action = action,
                    ActionTypeName = actionTypeName,
                    PropertyName = propertyName,
                    FilePath = filePath,
                    UpdatePath = updateAction
                };
                reference.Validate();
                references.Add(reference);
            }
        }
 
    }
}
