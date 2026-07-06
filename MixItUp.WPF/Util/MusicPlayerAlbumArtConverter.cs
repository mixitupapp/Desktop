using ATL;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MixItUp.WPF.Util
{
    public class MusicPlayerAlbumArtConverter : IValueConverter
    {
        private const int DefaultDecodePixelWidth = 64;

        private static readonly ConcurrentDictionary<string, ImageSource> cache = new ConcurrentDictionary<string, ImageSource>();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string filePath = null;
            if (value is MusicPlayerSong song)
            {
                filePath = song.FilePath;
            }
            else if (value is string path)
            {
                filePath = path;
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            int decodePixelWidth = DefaultDecodePixelWidth;
            if (parameter != null && int.TryParse(parameter.ToString(), out int width) && width > 0)
            {
                decodePixelWidth = width;
            }

            string cacheKey = $"{filePath}|{decodePixelWidth}";
            return cache.GetOrAdd(cacheKey, (key) => this.LoadAlbumArt(filePath, decodePixelWidth));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private ImageSource LoadAlbumArt(string filePath, int decodePixelWidth)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    Track track = new Track(filePath);
                    if (track.EmbeddedPictures != null && track.EmbeddedPictures.Count > 0)
                    {
                        byte[] pictureData = track.EmbeddedPictures[0].PictureData;
                        if (pictureData != null && pictureData.Length > 0)
                        {
                            BitmapImage image = new BitmapImage();
                            image.BeginInit();
                            image.CacheOption = BitmapCacheOption.OnLoad;
                            image.DecodePixelWidth = decodePixelWidth;
                            image.StreamSource = new MemoryStream(pictureData);
                            image.EndInit();
                            image.Freeze();
                            return image;
                        }
                    }
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
