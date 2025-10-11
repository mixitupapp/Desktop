using System;
using System.IO;
using Newtonsoft.Json;

namespace MixItUp.Distribution.Core
{
    public static class LauncherConfigService
    {
        public static LauncherConfigModel Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<LauncherConfigModel>(json);
            }
            catch (IOException ex)
            {
                throw new DistributionException($"Failed to read Launcher configuration at '{path}': {ex.Message}", ex)
                {
                    Endpoint = path,
                };
            }
            catch (JsonException ex)
            {
                throw new DistributionException($"Launcher configuration at '{path}' was malformed: {ex.Message}", ex)
                {
                    Endpoint = path,
                };
            }
        }

        public static void Save(string path, LauncherConfigModel config)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Launcher configuration path is required.", nameof(path));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string serialized = JsonConvert.SerializeObject(
                config,
                Formatting.Indented,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                }
            );

            try
            {
                File.WriteAllText(path, serialized);
            }
            catch (IOException ex)
            {
                throw new DistributionException($"Failed to write Launcher configuration to '{path}': {ex.Message}", ex)
                {
                    Endpoint = path,
                };
            }
        }
    }
}


