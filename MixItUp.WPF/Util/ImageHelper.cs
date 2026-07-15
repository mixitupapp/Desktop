using AnimatedImage.Wpf;
using CacheManager.Core;
using MixItUp.Base;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace MixItUp.WPF.Util
{
    public static class ImageHelper
    {
        private static ICacheManager<WriteableBitmap> bitmapCache = CacheFactory.Build<WriteableBitmap>(settings => settings
                                                                                                        .WithSystemRuntimeCacheHandle()
                                                                                                        .WithExpiration(ExpirationMode.Sliding, TimeSpan.FromMinutes(30)));

        // Shared per-URL carrier sources for animated emotes: AnimatedImage.Wpf caches its parsed
        // renderer by ImageSource instance, so every chat message using the same emote must receive
        // the same BitmapImage for that reuse to kick in.
        private static ICacheManager<BitmapImage> animatedSourceCache = CacheFactory.Build<BitmapImage>(settings => settings
                                                                                                        .WithSystemRuntimeCacheHandle()
                                                                                                        .WithExpiration(ExpirationMode.Sliding, TimeSpan.FromMinutes(30)));

        // URLs whose downloaded bytes were sniffed by the animated pipeline and found to be still
        // images (e.g. a Kick emote that is just a PNG). Only these may satisfy an animated render
        // from the static bitmap cache: entries the static pipeline cached while Emote Animation
        // was None must not, or those emotes would stay static after the setting is turned back on.
        private static readonly ConcurrentDictionary<string, bool> stillImagePaths = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        // In-flight downloads keyed by URL (UI thread only). A wall of chat messages can attach the
        // same emote dozens of times before its first download finishes; without this guard every
        // attach starts its own download AND (for animated emotes) its own full animation decode,
        // whose native-resolution transients dwarf everything else here. Followers re-enter the
        // setter once the winning load completes and land on pure cache hits.
        private static readonly Dictionary<string, Task> pendingImageLoads = new Dictionary<string, Task>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Task> pendingAnimatedImageLoads = new Dictionary<string, Task>(StringComparer.OrdinalIgnoreCase);

        public static void SetImageSource(Image image, string path, double width, double height, string tooltip = "")
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && path.Length > 0)
                {
                    WriteableBitmap writeableBitmap = ImageHelper.bitmapCache.Get(path);
                    if (writeableBitmap != null)
                    {
                        ImageHelper.SetImageSource(image, width, height, tooltip, writeableBitmap);
                        return;
                    }

                    if (path.StartsWith("http"))
                    {
                        if (ImageHelper.pendingImageLoads.TryGetValue(path, out Task pendingLoad))
                        {
                            pendingLoad.ContinueWith(t => Application.Current.Dispatcher.InvokeAsync(() => ImageHelper.SetImageSource(image, path, width, height, tooltip)));
                            return;
                        }

                        ImageHelper.pendingImageLoads[path] = Task.Run(async () =>
                        {
                            try
                            {
                                byte[] bytes = null;
                                using (AdvancedHttpClient client = new AdvancedHttpClient())
                                {
                                    bytes = await client.GetByteArrayAsync(path);
                                }
                                // WPF's WIC decoder cannot handle WebP (Velora assets); transcode to PNG off the UI thread.
                                bytes = WebPImageHelper.TranscodeToPngIfWebP(bytes);
                                await Application.Current.Dispatcher.InvokeAsync(() => ImageHelper.AddImageToCacheAndSetImageSourceFromBytes(image, path, width, height, tooltip, bytes));
                            }
                            finally
                            {
                                await Application.Current.Dispatcher.InvokeAsync(() => ImageHelper.pendingImageLoads.Remove(path));
                            }
                        });
                    }
                    else if (ServiceManager.Get<IFileService>().FileExists(path))
                    {
                        Task.Run(async () =>
                        {
                            byte[] bytes = await ServiceManager.Get<IFileService>().ReadFileAsBytes(path);
                            bytes = WebPImageHelper.TranscodeToPngIfWebP(bytes);
                            await Application.Current.Dispatcher.InvokeAsync(() => ImageHelper.AddImageToCacheAndSetImageSourceFromBytes(image, path, width, height, tooltip, bytes));
                        });
                    }
                    else
                    {
                        ImageHelper.AddImageToCacheAndSetImageSource(image, path, width, height, tooltip, BitmapFactory.FromResource(path));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(path + " - " + ex);
            }
        }

        // Animated emote frames are downscaled to this at decode time: chat renders emotes at
        // 2x the chat font size (~26px at the default size), and full-size Velora sources run
        // 512-1000px, so 48px keeps per-frame memory small (~9KB) while staying sharp in chat.
        private const int MaxAnimatedEmoteDimension = 48;

        /// <summary>
        /// Renders an animated emote into the Image - GIFs via AnimatedImage.Wpf, animated WebP via
        /// the standalone-frame WebPAnimationPlayer (AnimatedImage's libwebp compositing turns
        /// Velora's blend-flagged, white-background raid emotes into a white slab; see
        /// WebPImageHelper.DecodeAnimation) - falling back to the static pipeline when the bytes
        /// turn out not to be animated after all.
        /// </summary>
        public static void SetAnimatedImageSource(Image image, string path, double width, double height, string tooltip = "")
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !path.StartsWith("http"))
                {
                    ImageHelper.SetImageSource(image, path, width, height, tooltip);
                    return;
                }

                // Emote animation disabled: the static pipeline renders a representative frame,
                // with none of the animation decode or playback cost.
                if ((ChannelSession.Settings?.ChatEmoteAnimation ?? ChatEmoteAnimationEnum.None) == ChatEmoteAnimationEnum.None)
                {
                    ImageHelper.SetImageSource(image, path, width, height, tooltip);
                    return;
                }

                BitmapSource playingAnimation = WebPAnimationPlayer.Get(path);
                if (playingAnimation != null)
                {
                    ImageHelper.SetAnimatedWebPSource(image, width, height, tooltip, playingAnimation);
                    return;
                }

                BitmapImage cachedSource = ImageHelper.animatedSourceCache.Get(path);
                if (cachedSource != null)
                {
                    ImageHelper.SetAnimatedSource(image, width, height, tooltip, cachedSource);
                    return;
                }

                // A prior animated render sniffed this URL's bytes and found a still image; render
                // it via the static pipeline (cache hit or re-download). A static-cache entry alone
                // doesn't count - it may date from when Emote Animation was None, and that emote
                // should animate now that the setting allows it.
                if (ImageHelper.stillImagePaths.ContainsKey(path))
                {
                    ImageHelper.SetImageSource(image, path, width, height, tooltip);
                    return;
                }

                if (ImageHelper.pendingAnimatedImageLoads.TryGetValue(path, out Task pendingLoad))
                {
                    pendingLoad.ContinueWith(t => Application.Current.Dispatcher.InvokeAsync(() => ImageHelper.SetAnimatedImageSource(image, path, width, height, tooltip)));
                    return;
                }

                ImageHelper.pendingAnimatedImageLoads[path] = Task.Run(async () =>
                {
                    try
                    {
                        byte[] bytes = null;
                        using (AdvancedHttpClient client = new AdvancedHttpClient())
                        {
                            bytes = await client.GetByteArrayAsync(path);
                        }

                        bool isGif = bytes != null && bytes.Length > 3 && bytes[0] == (byte)'G' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F';

                        // Decode animated WebP off the UI thread; null means still/undecodable.
                        WebPAnimation webPAnimation = isGif ? null : WebPImageHelper.DecodeAnimation(bytes, MaxAnimatedEmoteDimension);

                        if (!isGif && webPAnimation == null)
                        {
                            ImageHelper.stillImagePaths.TryAdd(path, true);
                            byte[] staticBytes = WebPImageHelper.TranscodeToPngIfWebP(bytes);
                            await Application.Current.Dispatcher.InvokeAsync(() => ImageHelper.AddImageToCacheAndSetImageSourceFromBytes(image, path, width, height, tooltip, staticBytes));
                            return;
                        }

                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                if (webPAnimation != null)
                                {
                                    ImageHelper.SetAnimatedWebPSource(image, width, height, tooltip, WebPAnimationPlayer.GetOrCreate(path, webPAnimation));
                                    return;
                                }

                                // The BitmapImage is only a carrier: AnimatedImage.Wpf reads its raw
                                // StreamSource directly, and DelayCreation keeps WPF's WIC decoder from
                                // ever touching the bytes.
                                BitmapImage animatedSource = new BitmapImage();
                                animatedSource.BeginInit();
                                animatedSource.CreateOptions = BitmapCreateOptions.DelayCreation;
                                animatedSource.StreamSource = new MemoryStream(bytes, false);
                                animatedSource.EndInit();

                                ImageHelper.animatedSourceCache.Put(path, animatedSource);
                                ImageHelper.SetAnimatedSource(image, width, height, tooltip, animatedSource);
                            }
                            catch (Exception ex)
                            {
                                Logger.Log(path + " - " + ex);
                                ImageHelper.AddImageToCacheAndSetImageSourceFromBytes(image, path, width, height, tooltip, WebPImageHelper.TranscodeToPngIfWebP(bytes));
                            }
                        });
                    }
                    finally
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() => ImageHelper.pendingAnimatedImageLoads.Remove(path));
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Log(path + " - " + ex);
            }
        }

        private static void SetAnimatedWebPSource(Image image, double width, double height, string tooltip, BitmapSource playingAnimation)
        {
            image.Width = width;
            image.Height = height;
            if (!string.IsNullOrEmpty(tooltip))
            {
                image.ToolTip = tooltip;
            }
            image.Source = playingAnimation;
        }

        private static void SetAnimatedSource(Image image, double width, double height, string tooltip, BitmapImage animatedSource)
        {
            image.Width = width;
            image.Height = height;
            if (!string.IsNullOrEmpty(tooltip))
            {
                image.ToolTip = tooltip;
            }

            // GIFs animate per-Image via AnimatedImage.Wpf: Short plays ~5 seconds then holds the
            // frame it is on; Loop overrides any finite repeat count in the file's metadata.
            if ((ChannelSession.Settings?.ChatEmoteAnimation ?? ChatEmoteAnimationEnum.None) == ChatEmoteAnimationEnum.Short)
            {
                ImageBehavior.SetRepeatBehavior(image, new RepeatBehavior(TimeSpan.FromSeconds(5)));
            }
            else
            {
                ImageBehavior.SetRepeatBehavior(image, RepeatBehavior.Forever);
            }
            ImageBehavior.SetAnimatedSource(image, animatedSource);
        }

        private static void AddImageToCacheAndSetImageSourceFromBytes(Image image, string id, double width, double height, string tooltip, byte[] bytes)
        {
            try
            {
                if (bytes != null && bytes.Length > 0)
                {
                    using (MemoryStream stream = new MemoryStream(bytes))
                    {
                        BitmapImage bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.DecodePixelWidth = (int)width;   // disabling this fixes the blurry image, but it could increase memory
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.CreateOptions = BitmapCreateOptions.None;
                        bitmapImage.StreamSource = stream;
                        bitmapImage.EndInit();

                        if (bitmapImage.CanFreeze)
                        {
                            bitmapImage.Freeze();
                        }

                        ImageHelper.AddImageToCacheAndSetImageSource(image, id, width, height, tooltip, new WriteableBitmap(bitmapImage));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private static void AddImageToCacheAndSetImageSource(Image image, string id, double width, double height, string tooltip, WriteableBitmap writeableBitmap)
        {
            ImageHelper.bitmapCache.Put(id, writeableBitmap);
            ImageHelper.SetImageSource(image, width, height, tooltip, writeableBitmap);
        }

        private static void SetImageSource(Image image, double width, double height, string tooltip, WriteableBitmap writeableBitmap)
        {
            if (writeableBitmap != null)
            {
                image.Width = width;
                image.Height = height;
                image.Source = writeableBitmap;
                if (!string.IsNullOrEmpty(tooltip))
                {
                    image.ToolTip = tooltip;
                }
            }
        }
    }
}
