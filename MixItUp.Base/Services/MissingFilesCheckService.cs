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
        public string ActionTypeName { get; set; }
        public string FilePath { get; set; }
        public MissingFilesCheckStatus Status { get; set; }
        public Action<string> UpdatePath { get; set; }

        public string CommandName => Command?.Name ?? MixItUp.Base.Resources.Unknown;

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

            foreach (CommandModelBase command in ChannelSession.Settings.Commands.Values)
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
                        AddReference(references, command, MixItUp.Base.Resources.Sound, soundAction.FilePath, p => soundAction.FilePath = p);
                        break;

                    case FileActionModel fileAction:
                        AddReference(references, command, MixItUp.Base.Resources.FileReadAndWrite, fileAction.FilePath, p => fileAction.FilePath = p);
                        break;

                    case ExternalProgramActionModel externalAction:
                        AddReference(references, command, MixItUp.Base.Resources.ExternalProgram, externalAction.FilePath, p => externalAction.FilePath = p);
                        break;

                    case StreamingSoftwareActionModel streamingAction:
                        AddReference(references, command, MixItUp.Base.Resources.StreamingSoftware, streamingAction.SourceURL, p => streamingAction.SourceURL = p);
                        AddReference(references, command, MixItUp.Base.Resources.StreamingSoftware, streamingAction.SourceTextFilePath, p => streamingAction.SourceTextFilePath = p);
                        break;

                    case MusicPlayerActionModel musicAction:
                        AddReference(references, command, MixItUp.Base.Resources.MusicPlayer, musicAction.FolderPath, p => musicAction.FolderPath = p);
                        break;

                    case DiscordActionModel discordAction:
                        if (discordAction.ActionType == DiscordActionTypeEnum.SendMessage)
                        {
                            AddReference(references, command, MixItUp.Base.Resources.Discord, discordAction.FilePath, p => discordAction.FilePath = p);
                        }
                        break;

                    case VTSPogActionModel vtsPogAction:
                        if (vtsPogAction.ActionType == VTSPogActionTypeEnum.PlayAudioFile)
                        {
                            AddReference(references, command, MixItUp.Base.Resources.VTSPog, vtsPogAction.AudioFilePath, p => vtsPogAction.AudioFilePath = p);
                        }
                        break;

                    case OverlayActionModel overlayAction:
                        if (overlayAction.OverlayItemV3 != null)
                        {
                            ScanOverlayItem(command, overlayAction.OverlayItemV3, references);
                        }
                        break;

                    case GroupActionModel groupAction:
                        // Group, Conditional, Random, and Repeat actions (they all inherit from GroupActionModel)
                        ScanActions(command, groupAction.Actions, references);
                        break;

                    // to do: find a better way instead of hardcoding each action model. 
                }
            }
        }

        private void ScanOverlayItem(CommandModelBase command, OverlayItemV3ModelBase overlayItem, List<MissingFilesCheckReference> references)
        {
            switch (overlayItem)
            {
                case OverlayVideoV3Model videoItem:
                    AddReference(references, command, MixItUp.Base.Resources.Overlay, videoItem.FilePath, p => videoItem.FilePath = p);
                    break;

                case OverlaySoundV3Model soundItem:
                    AddReference(references, command, MixItUp.Base.Resources.Overlay, soundItem.FilePath, p => soundItem.FilePath = p);
                    break;

                case OverlayImageV3Model imageItem:
                    AddReference(references, command, MixItUp.Base.Resources.Overlay, imageItem.FilePath, p => imageItem.FilePath = p);
                    break;
            }
        }

        private void AddReference(List<MissingFilesCheckReference> references, CommandModelBase command, string actionTypeName, string filePath, Action<string> updateAction)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                var reference = new MissingFilesCheckReference
                {
                    Command = command,
                    ActionTypeName = actionTypeName,
                    FilePath = filePath,
                    UpdatePath = updateAction
                };
                reference.Validate();
                references.Add(reference);
            }
        }
 
    }
}
