using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MixItUp.Base.Services
{
    public interface IAudioService
    {
        string DefaultAudioDevice { get; }
        string MixItUpOverlay { get; }

        ISet<string> ApplicableAudioFileExtensions { get; }

        Task Play(string filePath, int volume, string deviceName, bool waitForFinish = false);

        Task PlayMP3Stream(Stream stream, int volume, string deviceName, bool waitForFinish = false);

        Task PlayPCMStream(Stream stream, int volume, string deviceName, bool waitForFinish = false);

        Task PlayOnOverlay(string filePath, double volume, bool waitForFinish = false);

        Task PlayNotification(string filePath, int volume);

        Task StopAllSounds();

        void OverlaySoundFinished(Guid id);

        /// <summary>
        /// Gets the audio devices that can be picked in the UI. Pass the currently saved device as
        /// <paramref name="retainedDevice"/> so that a device which is not present right now (unplugged,
        /// disabled, or renamed) still appears in the list, otherwise the bound ComboBox drops the
        /// selection and writes the resulting null back over the saved value.
        /// </summary>
        IEnumerable<string> GetSelectableAudioDevices(bool includeOverlay = false, string retainedDevice = null);

        IEnumerable<string> GetOutputDevices();

        string GetOutputDeviceName(string deviceName);
    }
}
