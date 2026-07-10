using MixItUp.Base;
using MixItUp.Base.Model.Settings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MixItUp.WPF.Util
{
    /// <summary>
    /// Plays decoded animated WebP emotes onto shared WriteableBitmaps. Messages that show an
    /// emote while it is animating share ONE bitmap - one decode and one playback no matter how
    /// many copies are on screen - and each new sighting tops the Short play window back up to
    /// ~5 seconds. When the window elapses, Short freezes the bitmap on the animation's poster
    /// frame (its second-to-last frame, or its most detailed one when the ending is a blank
    /// fade-out) - reached in sequence, so there is no visual jump - and a frozen bitmap is never
    /// restarted: the NEXT sighting starts a fresh playback generation on a NEW bitmap that only
    /// the newer messages bind, so the frozen copies in older messages stay static. Loop instead
    /// resumes a frozen animation in place and runs forever. A single shared timer steps all
    /// animations, writes a frame only when its index changes, and stops entirely once everything
    /// is frozen. Only each emote's current generation is tracked (older frozen bitmaps are kept
    /// alive solely by the chat messages showing them), so per-emote cost stays one decoded frame
    /// set plus one small bitmap per playback burst.
    /// </summary>
    public static class WebPAnimationPlayer
    {
        private const int ShortPlayDurationMilliseconds = 5000;

        private class PlayingAnimation
        {
            public WebPAnimation Animation { get; set; }

            public WriteableBitmap Bitmap { get; set; }

            public long StartMilliseconds { get; set; }

            public long PlayUntilMilliseconds { get; set; }

            public int CurrentFrameIndex { get; set; } = -1;

            public bool Frozen { get; set; }
        }

        private static readonly Dictionary<string, PlayingAnimation> animations = new Dictionary<string, PlayingAnimation>(StringComparer.OrdinalIgnoreCase);
        private static readonly Stopwatch clock = Stopwatch.StartNew();
        private static DispatcherTimer timer;

        // UI thread only (all callers already marshal via the Dispatcher).

        public static BitmapSource Get(string key)
        {
            if (animations.TryGetValue(key, out PlayingAnimation playing))
            {
                return Attach(key, playing);
            }
            return null;
        }

        public static BitmapSource GetOrCreate(string key, WebPAnimation animation)
        {
            if (animations.TryGetValue(key, out PlayingAnimation existing))
            {
                return Attach(key, existing);
            }
            return StartGeneration(key, animation);
        }

        // A message showing an emote that is still animating joins its bitmap and tops the play
        // window back up. A frozen bitmap is never restarted (older messages bind it and must stay
        // static): in Short mode the sighting starts a NEW generation instead, and in Loop mode -
        // where every copy animating is the point - the frozen animation resumes in place from its
        // frozen frame (e.g. after switching modes mid-session).
        private static BitmapSource Attach(string key, PlayingAnimation playing)
        {
            if (!playing.Frozen)
            {
                playing.PlayUntilMilliseconds = clock.ElapsedMilliseconds + ShortPlayDurationMilliseconds;
                EnsureTimerRunning();
                return playing.Bitmap;
            }

            ChatEmoteAnimationEnum mode = ChannelSession.Settings != null ? ChannelSession.Settings.ChatEmoteAnimation : ChatEmoteAnimationEnum.Loop;
            if (mode == ChatEmoteAnimationEnum.Short)
            {
                return StartGeneration(key, playing.Animation);
            }
            if (mode == ChatEmoteAnimationEnum.Loop)
            {
                playing.Frozen = false;
                playing.StartMilliseconds = clock.ElapsedMilliseconds - FrameStartOffset(playing.Animation, Math.Max(playing.CurrentFrameIndex, 0));
                EnsureTimerRunning();
            }
            return playing.Bitmap;
        }

        private static BitmapSource StartGeneration(string key, WebPAnimation animation)
        {
            PlayingAnimation playing = new PlayingAnimation()
            {
                Animation = animation,
                Bitmap = new WriteableBitmap(animation.Width, animation.Height, 96, 96, PixelFormats.Pbgra32, null),
                StartMilliseconds = clock.ElapsedMilliseconds,
                PlayUntilMilliseconds = clock.ElapsedMilliseconds + ShortPlayDurationMilliseconds,
            };
            WriteFrame(playing, 0);
            animations[key] = playing;
            EnsureTimerRunning();
            return playing.Bitmap;
        }

        private static void EnsureTimerRunning()
        {
            if (timer == null)
            {
                timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(33) };
                timer.Tick += (sender, args) => Advance();
                timer.Start();
            }
            else if (!timer.IsEnabled)
            {
                timer.Start();
            }
        }

        private static void Advance()
        {
            ChatEmoteAnimationEnum mode = ChannelSession.Settings != null ? ChannelSession.Settings.ChatEmoteAnimation : ChatEmoteAnimationEnum.Loop;

            long now = clock.ElapsedMilliseconds;
            bool anyPlaying = false;
            foreach (PlayingAnimation playing in animations.Values)
            {
                if (playing.Frozen)
                {
                    continue;
                }

                int index = FrameIndexAt(playing.Animation, (int)((now - playing.StartMilliseconds) % playing.Animation.TotalDurationMilliseconds));

                // Short freezes on the representative frame once its window elapses (None, when
                // switched to mid-session, freezes at the first opportunity); Loop never stops.
                bool stopRequested = (mode == ChatEmoteAnimationEnum.None) || (mode == ChatEmoteAnimationEnum.Short && now > playing.PlayUntilMilliseconds);
                if (stopRequested && SteppedOnOrOver(playing, index, playing.Animation.RepresentativeFrameIndex))
                {
                    WriteFrame(playing, playing.Animation.RepresentativeFrameIndex);
                    playing.Frozen = true;
                    continue;
                }

                if (index != playing.CurrentFrameIndex && index < playing.Animation.Frames.Count)
                {
                    WriteFrame(playing, index);
                }
                anyPlaying = true;
            }

            if (!anyPlaying && timer != null)
            {
                timer.Stop();
            }
        }

        // Whether the animation, moving from its current frame to the newly computed one (ticks
        // can skip frames and the position wraps), lands on or passes the target frame.
        private static bool SteppedOnOrOver(PlayingAnimation playing, int newIndex, int target)
        {
            int from = playing.CurrentFrameIndex;
            if (newIndex == target || from == target)
            {
                return true;
            }
            if (from < 0)
            {
                return newIndex >= target;
            }
            if (from == newIndex)
            {
                return false;
            }

            int count = playing.Animation.Frames.Count;
            int steps = ((newIndex - from) % count + count) % count;
            int toTarget = ((target - from) % count + count) % count;
            return toTarget <= steps;
        }

        private static int FrameIndexAt(WebPAnimation animation, int positionMilliseconds)
        {
            int index = 0;
            foreach (WebPAnimationFrame frame in animation.Frames)
            {
                positionMilliseconds -= frame.DurationMilliseconds;
                if (positionMilliseconds < 0)
                {
                    break;
                }
                index++;
            }
            return Math.Min(index, animation.Frames.Count - 1);
        }

        private static int FrameStartOffset(WebPAnimation animation, int frameIndex)
        {
            int offset = 0;
            for (int i = 0; i < frameIndex && i < animation.Frames.Count; i++)
            {
                offset += animation.Frames[i].DurationMilliseconds;
            }
            return offset;
        }

        private static void WriteFrame(PlayingAnimation playing, int index)
        {
            WebPAnimationFrame frame = playing.Animation.Frames[index];
            playing.Bitmap.WritePixels(new Int32Rect(0, 0, playing.Animation.Width, playing.Animation.Height), frame.PremultipliedBgraPixels, playing.Animation.Width * 4, 0);
            playing.CurrentFrameIndex = index;
        }
    }
}
