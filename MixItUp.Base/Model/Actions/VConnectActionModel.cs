using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Actions
{
    public enum VConnectActionTypeEnum
    {
        ActivateTrigger,
        SendCustomMessage,
        LookupAsset,
    }

    [DataContract]
    public class VConnectActionModel : ActionModelBase
    {
        public const string AssetUidSpecialIdentifier = "vconnectassetuid";
        public const string AssetNameSpecialIdentifier = "vconnectassetname";
        public const string AssetTypeSpecialIdentifier = "vconnectassettype";
        public const string AssetScreenshotFilePathSpecialIdentifier = "vconnectassetscreenshotfilepath";
        public const string AssetSuccessSpecialIdentifier = "vconnectassetsuccess";

        public static VConnectActionModel CreateForTrigger(string triggerUid, string triggerName)
        {
            return new VConnectActionModel(VConnectActionTypeEnum.ActivateTrigger) { TriggerUid = triggerUid, TriggerName = triggerName };
        }

        public static VConnectActionModel CreateForCustomMessage(string channel, string arguments)
        {
            return new VConnectActionModel(VConnectActionTypeEnum.SendCustomMessage) { MessageChannel = channel, MessageArguments = arguments };
        }

        public static VConnectActionModel CreateForAssetLookup(string assetUid, string assetName, string screenshotFilePath)
        {
            return new VConnectActionModel(VConnectActionTypeEnum.LookupAsset) { AssetUid = assetUid, AssetName = assetName, ScreenshotFilePath = screenshotFilePath };
        }

        [DataMember]
        public VConnectActionTypeEnum ActionType { get; set; }

        /// <summary>
        /// Triggers are stored by uid, which is what VConnect matches on first. The name is carried
        /// alongside so the editor can still show something readable when VConnect is closed.
        /// </summary>
        [DataMember]
        public string TriggerUid { get; set; }
        [DataMember]
        public string TriggerName { get; set; }

        [DataMember]
        public string MessageChannel { get; set; }

        /// <summary>One argument per line, in the order they are sent.</summary>
        [DataMember]
        public string MessageArguments { get; set; }

        [DataMember]
        public string AssetUid { get; set; }
        [DataMember]
        public string AssetName { get; set; }

        /// <summary>Where the asset's preview image is written, or empty to skip fetching one.</summary>
        [DataMember]
        public string ScreenshotFilePath { get; set; }

        public VConnectActionModel(VConnectActionTypeEnum actionType)
            : base(ActionTypeEnum.VConnect)
        {
            this.ActionType = actionType;
        }

        [Obsolete]
        public VConnectActionModel() { }

        protected override async Task PerformInternal(CommandParametersModel parameters)
        {
            if (ChannelSession.Settings.VConnectEnabled && !ServiceManager.Get<VConnectService>().IsConnected)
            {
                Result connection = await ServiceManager.Get<VConnectService>().Connect();
                if (!connection.Success)
                {
                    return;
                }
            }

            if (!ServiceManager.Get<VConnectService>().IsConnected)
            {
                return;
            }

            if (this.ActionType == VConnectActionTypeEnum.ActivateTrigger)
            {
                string triggerUid = await ReplaceStringWithSpecialModifiers(this.TriggerUid, parameters);
                string triggerName = await ReplaceStringWithSpecialModifiers(this.TriggerName, parameters);

                Result result = await ServiceManager.Get<VConnectService>().ActivateTrigger(triggerUid, triggerName);
                if (!result.Success)
                {
                    Logger.Log(LogLevel.Error, "VConnect Action - Trigger " + (triggerName ?? triggerUid) + " did not activate: " + result.Message);
                }
            }
            else if (this.ActionType == VConnectActionTypeEnum.SendCustomMessage)
            {
                string channel = await ReplaceStringWithSpecialModifiers(this.MessageChannel, parameters);

                List<JToken> arguments = new List<JToken>();
                if (!string.IsNullOrEmpty(this.MessageArguments))
                {
                    foreach (string line in this.MessageArguments.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None))
                    {
                        arguments.Add(VConnectService.ParseCustomMessageArgument(await ReplaceStringWithSpecialModifiers(line, parameters)));
                    }
                }

                await ServiceManager.Get<VConnectService>().SendCustomMessage(channel, arguments);
            }
            else if (this.ActionType == VConnectActionTypeEnum.LookupAsset)
            {
                await this.PerformAssetLookup(parameters);
            }
        }

        private async Task PerformAssetLookup(CommandParametersModel parameters)
        {
            string assetUid = await ReplaceStringWithSpecialModifiers(this.AssetUid, parameters);
            string screenshotFilePath = await ReplaceStringWithSpecialModifiers(this.ScreenshotFilePath, parameters);
            bool includeScreenshot = !string.IsNullOrWhiteSpace(screenshotFilePath);

            VConnectAsset asset = null;
            if (!string.IsNullOrEmpty(assetUid))
            {
                asset = await ServiceManager.Get<VConnectService>().GetAssetInfo(assetUid, includeScreenshot);
            }
            else if (!string.IsNullOrEmpty(this.AssetName))
            {
                // Nothing in the API looks an asset up by name, so the library listing stands in for it.
                string assetName = await ReplaceStringWithSpecialModifiers(this.AssetName, parameters);
                IEnumerable<VConnectAsset> assets = await ServiceManager.Get<VConnectService>().GetAssets();
                VConnectAsset match = assets.FirstOrDefault(a => string.Equals(a.name, assetName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    asset = await ServiceManager.Get<VConnectService>().GetAssetInfo(match.uid, includeScreenshot);
                }
            }

            string savedScreenshotPath = string.Empty;
            if (asset != null && includeScreenshot && !string.IsNullOrEmpty(asset.screenshot))
            {
                try
                {
                    await ServiceManager.Get<IFileService>().SaveFile(screenshotFilePath, Convert.FromBase64String(asset.screenshot));
                    savedScreenshotPath = screenshotFilePath;
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }

            // Always assigned, even on a miss, so a failed lookup leaves blanks instead of the raw
            // special identifier text sitting in whatever the next action sends.
            parameters.SpecialIdentifiers[AssetUidSpecialIdentifier] = asset?.uid ?? string.Empty;
            parameters.SpecialIdentifiers[AssetNameSpecialIdentifier] = asset?.name ?? string.Empty;
            parameters.SpecialIdentifiers[AssetTypeSpecialIdentifier] = asset?.type ?? string.Empty;
            parameters.SpecialIdentifiers[AssetScreenshotFilePathSpecialIdentifier] = savedScreenshotPath;
            parameters.SpecialIdentifiers[AssetSuccessSpecialIdentifier] = (asset != null).ToString();
        }
    }
}
