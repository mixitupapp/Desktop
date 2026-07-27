//------------------------------------------------------------------------------
// Hand-written replacement for the previous OverlayResources.resx +
// OverlayResources.Designer.cs strongly-typed resource class.
//
// Snippet content lives as real .html/.css/.js files under the
// MixItUp.Base/OverlayResources/ folder and is embedded into the
// assembly as ManifestResourceStream entries. The public API of this
// class (one static string property per snippet) is kept identical to
// the previous generated class so that all callsites continue to work.
//------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Resources;
using System.Text;

namespace MixItUp.Base
{
    public static class OverlayResources
    {
        private const string ResourceNamespacePrefix = "MixItUp.Base.OverlayResources.";

        private static readonly ConcurrentDictionary<string, string> Cache = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        private static string Load(string fileName)
        {
            return Cache.GetOrAdd(fileName, key =>
            {
                string resourceName = ResourceNamespacePrefix + key;
                var assembly = typeof(OverlayResources).Assembly;
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        throw new MissingManifestResourceException("Embedded overlay resource not found: " + resourceName);
                    }
                    using (var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: true))
                    {
                        return reader.ReadToEnd();
                    }
                }
            });
        }

        public static string OverlayActionDefaultJavascript => Load("OverlayActionDefaultJavascript.js");
        public static string OverlayChatDefaultCSS => Load("OverlayChatDefaultCSS.css");
        public static string OverlayChatDefaultHTML => Load("OverlayChatDefaultHTML.html");
        public static string OverlayChatDefaultJavascript => Load("OverlayChatDefaultJavascript.js");
        public static string OverlayCustomAnimationDefaultJavascript => Load("OverlayCustomAnimationDefaultJavascript.js");
        public static string OverlayCustomDefaultCSS => Load("OverlayCustomDefaultCSS.css");
        public static string OverlayCustomDefaultHTML => Load("OverlayCustomDefaultHTML.html");
        public static string OverlayCustomDefaultJavascript => Load("OverlayCustomDefaultJavascript.js");
        public static string OverlayDiscordReactiveVoiceDefaultCSS => Load("OverlayDiscordReactiveVoiceDefaultCSS.css");
        public static string OverlayDiscordReactiveVoiceDefaultHTML => Load("OverlayDiscordReactiveVoiceDefaultHTML.html");
        public static string OverlayDiscordReactiveVoiceDefaultJavascript => Load("OverlayDiscordReactiveVoiceDefaultJavascript.js");
        public static string OverlayEmoteEffectBaseDefaultJavascript => Load("OverlayEmoteEffectBaseDefaultJavascript.js");
        public static string OverlayEmoteEffectDefaultCSS => Load("OverlayEmoteEffectDefaultCSS.css");
        public static string OverlayEmoteEffectDefaultHTML => Load("OverlayEmoteEffectDefaultHTML.html");
        public static string OverlayEmoteEffectDefaultJavascript => Load("OverlayEmoteEffectDefaultJavascript.js");
        public static string OverlayEndCreditsDefaultCSS => Load("OverlayEndCreditsDefaultCSS.css");
        public static string OverlayEndCreditsDefaultHTML => Load("OverlayEndCreditsDefaultHTML.html");
        public static string OverlayEndCreditsDefaultJavascript => Load("OverlayEndCreditsDefaultJavascript.js");
        public static string OverlayEndCreditsDefaultJavascriptOld => Load("OverlayEndCreditsDefaultJavascriptOld.js");
        public static string OverlayEndCreditsSectionDefaultHTML => Load("OverlayEndCreditsSectionDefaultHTML.html");
        public static string OverlayEndpointDefaultCSS => Load("OverlayEndpointDefaultCSS.css");
        public static string OverlayEndpointDefaultHTML => Load("OverlayEndpointDefaultHTML.html");
        public static string OverlayEndpointDefaultHead => Load("OverlayEndpointDefaultHead.html");
        public static string OverlayEndpointDefaultJavascript => Load("OverlayEndpointDefaultJavascript.js");
        public static string OverlayEventListDefaultCSS => Load("OverlayEventListDefaultCSS.css");
        public static string OverlayEventListDefaultHTML => Load("OverlayEventListDefaultHTML.html");
        public static string OverlayEventListDefaultJavascript => Load("OverlayEventListDefaultJavascript.js");
        public static string OverlayGameQueueDefaultCSS => Load("OverlayGameQueueDefaultCSS.css");
        public static string OverlayGameQueueDefaultHTML => Load("OverlayGameQueueDefaultHTML.html");
        public static string OverlayGameQueueDefaultJavascript => Load("OverlayGameQueueDefaultJavascript.js");
        public static string OverlayGoalDefaultCSS => Load("OverlayGoalDefaultCSS.css");
        public static string OverlayGoalDefaultHTML => Load("OverlayGoalDefaultHTML.html");
        public static string OverlayGoalDefaultJavascript => Load("OverlayGoalDefaultJavascript.js");
        public static string OverlayHTMLWidgetDefaultJavascript => Load("OverlayHTMLWidgetDefaultJavascript.js");
        public static string OverlayHTMLWidgetDefaultJavascriptOld => Load("OverlayHTMLWidgetDefaultJavascriptOld.js");
        public static string OverlayHeaderTextDefaultCSS => Load("OverlayHeaderTextDefaultCSS.css");
        public static string OverlayImageDefaultCSS => Load("OverlayImageDefaultCSS.css");
        public static string OverlayImageDefaultHTML => Load("OverlayImageDefaultHTML.html");
        public static string OverlayImageWidgetDefaultJavascript => Load("OverlayImageWidgetDefaultJavascript.js");
        public static string OverlayItemIFrameHTML => Load("OverlayItemIFrameHTML.html");
        public static string OverlayLabelAddJavascript => Load("OverlayLabelAddJavascript.js");
        public static string OverlayLabelAmountDefaultFormat => Load("OverlayLabelAmountDefaultFormat.txt");
        public static string OverlayLabelDefaultCSS => Load("OverlayLabelDefaultCSS.css");
        public static string OverlayLabelDefaultHTML => Load("OverlayLabelDefaultHTML.html");
        public static string OverlayLabelDefaultJavascript => Load("OverlayLabelDefaultJavascript.js");
        public static string OverlayLabelDefaultJavascriptOld => Load("OverlayLabelDefaultJavascriptOld.js");
        public static string OverlayLabelDefaultJavascriptOld2 => Load("OverlayLabelDefaultJavascriptOld2.js");
        public static string OverlayLabelDefaultJavascriptOld3 => Load("OverlayLabelDefaultJavascriptOld3.js");
        public static string OverlayLeaderboardDefaultCSS => Load("OverlayLeaderboardDefaultCSS.css");
        public static string OverlayLeaderboardDefaultHTML => Load("OverlayLeaderboardDefaultHTML.html");
        public static string OverlayLeaderboardDefaultJavascript => Load("OverlayLeaderboardDefaultJavascript.js");
        public static string OverlayMainHTML => Load("OverlayMainHTML.html");
        public static string OverlayPersistentEmoteEffectDefaultJavascript => Load("OverlayPersistentEmoteEffectDefaultJavascript.js");
        public static string OverlayPersistentTimerDefaultJavascript => Load("OverlayPersistentTimerDefaultJavascript.js");
        public static string OverlayPollDefaultCSS => Load("OverlayPollDefaultCSS.css");
        public static string OverlayPollDefaultHTML => Load("OverlayPollDefaultHTML.html");
        public static string OverlayPollDefaultJavascript => Load("OverlayPollDefaultJavascript.js");
        public static string OverlayPositionedItemDefaultCSS => Load("OverlayPositionedItemDefaultCSS.css");
        public static string OverlayPositionedItemDefaultHTML => Load("OverlayPositionedItemDefaultHTML.html");
        public static string OverlaySoundDefaultHTML => Load("OverlaySoundDefaultHTML.html");
        public static string OverlaySoundDefaultJavascript => Load("OverlaySoundDefaultJavascript.js");
        public static string OverlayStreamBossDefaultCSS => Load("OverlayStreamBossDefaultCSS.css");
        public static string OverlayStreamBossDefaultHTML => Load("OverlayStreamBossDefaultHTML.html");
        public static string OverlayStreamBossDefaultJavascript => Load("OverlayStreamBossDefaultJavascript.js");
        public static string OverlayTextDefaultCSS => Load("OverlayTextDefaultCSS.css");
        public static string OverlayTextDefaultHTML => Load("OverlayTextDefaultHTML.html");
        public static string OverlayTextWidgetDefaultJavascript => Load("OverlayTextWidgetDefaultJavascript.js");
        public static string OverlayTimeoutWrapperJavascript => Load("OverlayTimeoutWrapperJavascript.js");
        public static string OverlayTimerDefaultHTML => Load("OverlayTimerDefaultHTML.html");
        public static string OverlayTimerDefaultJavascript => Load("OverlayTimerDefaultJavascript.js");
        public static string OverlayTwitchClipEmbedDefaultCSS => Load("OverlayTwitchClipEmbedDefaultCSS.css");
        public static string OverlayTwitchClipEmbedDefaultHTML => Load("OverlayTwitchClipEmbedDefaultHTML.html");
        public static string OverlayTwitchClipEmbedDefaultJavascript => Load("OverlayTwitchClipEmbedDefaultJavascript.js");
        public static string OverlayTwitchClipVideoDefaultCSS => Load("OverlayTwitchClipVideoDefaultCSS.css");
        public static string OverlayTwitchClipVideoDefaultHTML => Load("OverlayTwitchClipVideoDefaultHTML.html");
        public static string OverlayVideoActionDefaultJavascript => Load("OverlayVideoActionDefaultJavascript.js");
        public static string OverlayVideoDefaultCSS => Load("OverlayVideoDefaultCSS.css");
        public static string OverlayVideoDefaultHTML => Load("OverlayVideoDefaultHTML.html");
        public static string OverlayVideoWidgetDefaultJavascript => Load("OverlayVideoWidgetDefaultJavascript.js");
        public static string OverlayWebPageDefaultHTML => Load("OverlayWebPageDefaultHTML.html");
        public static string OverlayWheelDefaultCSS => Load("OverlayWheelDefaultCSS.css");
        public static string OverlayWheelDefaultHTML => Load("OverlayWheelDefaultHTML.html");
        public static string OverlayWheelDefaultJavascript => Load("OverlayWheelDefaultJavascript.js");
        public static string OverlayWheelDefaultJavascriptOld => Load("OverlayWheelDefaultJavascriptOld.js");
        public static string OverlayYouTubeDefaultCSS => Load("OverlayYouTubeDefaultCSS.css");
        public static string OverlayYouTubeDefaultHTML => Load("OverlayYouTubeDefaultHTML.html");
        public static string OverlayYouTubeDefaultJavascript => Load("OverlayYouTubeDefaultJavascript.js");
        public static string OverlayYouTubeWidgetDefaultJavascript => Load("OverlayYouTubeWidgetDefaultJavascript.js");
        public static string WoahCSS => Load("WoahCSS.css");
        public static string animateCSS => Load("animateCSS.css");
        public static string bootstrapCSS => Load("bootstrapCSS.css");
        public static string bootstrapJS => Load("bootstrapJS.js");
        public static string jqueryJS => Load("jqueryJS.js");
        public static string videoCSS => Load("videoCSS.css");
        public static string videoJS => Load("videoJS.js");
    }
}