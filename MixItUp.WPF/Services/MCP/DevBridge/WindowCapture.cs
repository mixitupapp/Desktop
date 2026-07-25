#if !DEV_BRIDGE
#error The MCP dev bridge is being compiled without the DEV_BRIDGE constant. These files must only ever build under the Dev configuration. See Directory.Build.props.
#endif

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MixItUp.WPF.Services.MCP.DevBridge
{
    /// <summary>
    /// Captures a window the way the desktop compositor sees it, including the parts WPF cannot draw.
    /// </summary>
    /// <remarks>
    /// <b>Why a second capture path exists at all.</b> RenderTargetBitmap renders the WPF visual tree, and
    /// three things are genuinely not in it. A Popup is its own top-level window, so an open ComboBox
    /// dropdown, a context menu, and this app's PopupBox intellisense are invisible to it. Non-client
    /// chrome, the title bar and its buttons, is drawn by the shell rather than by WPF. And content drawn
    /// by another technology into its own child HWND is the airspace problem. None of that is a bug in the
    /// in-process path, it is what "render the visual tree" means.
    /// <para>
    /// <b>How this gets them.</b> PrintWindow with PW_RENDERFULLCONTENT asks the window to re-render
    /// itself into a device context through DWM rather than scraping pixels off the screen. The popups are
    /// then captured as the separate windows they are and composited back into place using their screen
    /// rects.
    /// </para>
    /// <para>
    /// <b>The property this deliberately keeps.</b> Going through DWM rather than the screen means this
    /// still works when the window is unfocused and when it is physically covered by another window, which
    /// is the same property that makes the in-process path usable from a background agent. Verified
    /// against a topmost window proven to own the pixels. What it cannot do is capture a minimized window,
    /// which has no surface to re-render, and that case is reported rather than returned as garbage.
    /// </para>
    /// </remarks>
    internal static class WindowCapture
    {
        /// <summary>Re-render the full window through DWM, including child and composed content.</summary>
        private const uint PW_RENDERFULLCONTENT = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width { get { return this.Right - this.Left; } }

            public int Height { get { return this.Bottom - this.Top; } }
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hwnd);

        /// <summary>
        /// The outcome of a composited window capture.
        /// </summary>
        internal sealed class CaptureOutcome
        {
            public byte[] Png;

            public int Width;

            public int Height;

            /// <summary>Popups that were found and drawn in, for reporting.</summary>
            public List<string> Overlays = new List<string>();

            /// <summary>Whether a popup spilled past the window edge and grew the image to fit.</summary>
            public bool ExtendedBeyondWindow;

            public string Error;
        }

        /// <summary>
        /// Captures <paramref name="window"/> and any popup windows sitting over it. Must run on the UI
        /// thread, because it reads the WPF window and the process's presentation sources.
        /// </summary>
        public static CaptureOutcome Capture(Window window, int maxWidth)
        {
            CaptureOutcome outcome = new CaptureOutcome();

            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                outcome.Error = "The window has no native handle yet, so there is nothing for the compositor to render. It has not finished opening.";
                return outcome;
            }

            if (IsIconic(hwnd))
            {
                outcome.Error = "The window is minimized, so it has no surface to re-render and this mode would return garbage. Restore it, or use the default mode, which renders the visual tree and does not care whether the window is on screen.";
                return outcome;
            }

            if (!GetWindowRect(hwnd, out RECT windowRect) || windowRect.Width < 1 || windowRect.Height < 1)
            {
                outcome.Error = "The window reports no on-screen rectangle.";
                return outcome;
            }

            List<IntPtr> overlays = FindOverlays(hwnd, windowRect);

            // The canvas is the union of the window and its popups, not just the window. A dropdown near
            // the bottom edge opens downwards past it, so sizing to the window would clip off most of the
            // thing the caller switched modes to see.
            RECT canvas = windowRect;
            foreach (IntPtr overlay in overlays)
            {
                if (GetWindowRect(overlay, out RECT overlayRect))
                {
                    canvas = Union(canvas, overlayRect);
                }
            }

            Bitmap composed = null;
            try
            {
                composed = new Bitmap(canvas.Width, canvas.Height, PixelFormat.Format32bppArgb);

                if (!Draw(composed, canvas, hwnd, windowRect))
                {
                    outcome.Error = "PrintWindow refused to render the window. This mode needs the desktop compositor, so it does not work over a session with no visible desktop. Use the default mode instead.";
                    return outcome;
                }

                foreach (IntPtr overlay in overlays)
                {
                    DrawOverlay(composed, canvas, overlay, outcome);
                }

                if (canvas.Width != windowRect.Width || canvas.Height != windowRect.Height)
                {
                    outcome.ExtendedBeyondWindow = true;
                }

                Encode(composed, maxWidth, outcome);
            }
            catch (Exception ex)
            {
                outcome.Error = $"{ex.GetType().Name}: {ex.Message}";
            }
            finally
            {
                composed?.Dispose();
            }

            return outcome;
        }

        /// <summary>
        /// Popup windows overlapping the target.
        /// </summary>
        /// <remarks>
        /// Sourced from <see cref="PresentationSource.CurrentSources"/> rather than by enumerating the
        /// desktop, which is both cheaper and more precise: every WPF Popup creates its own HwndSource, so
        /// this is the authoritative list of the windows WPF owns, and it cannot pick up anything from
        /// another process.
        /// <para>
        /// Real application windows are excluded. Without that, capturing the main window while a command
        /// editor happened to overlap it would composite the editor into the image, which is not an
        /// overlay on the main window, it is a different window that happens to be in front.
        /// </para>
        /// </remarks>
        private static List<IntPtr> FindOverlays(IntPtr target, RECT targetRect)
        {
            List<IntPtr> overlays = new List<IntPtr>();

            HashSet<IntPtr> applicationWindows = new HashSet<IntPtr>();
            try
            {
                foreach (Window open in Application.Current.Windows)
                {
                    if (open == null)
                    {
                        continue;
                    }
                    IntPtr handle = new WindowInteropHelper(open).Handle;
                    if (handle != IntPtr.Zero)
                    {
                        applicationWindows.Add(handle);
                    }
                }
            }
            catch (Exception)
            {
                // Worst case an application window is composited in. Better than failing the capture.
            }

            try
            {
                foreach (PresentationSource source in PresentationSource.CurrentSources)
                {
                    if (!(source is HwndSource hwndSource) || hwndSource.IsDisposed)
                    {
                        continue;
                    }

                    IntPtr candidate = hwndSource.Handle;
                    if (candidate == IntPtr.Zero || candidate == target || applicationWindows.Contains(candidate))
                    {
                        continue;
                    }
                    if (!IsWindowVisible(candidate) || !GetWindowRect(candidate, out RECT rect))
                    {
                        continue;
                    }
                    if (rect.Width < 1 || rect.Height < 1 || !Intersects(rect, targetRect))
                    {
                        continue;
                    }

                    overlays.Add(candidate);
                }
            }
            catch (Exception)
            {
                // A source collection mutating mid-enumeration costs the overlay, not the capture.
            }

            return overlays;
        }

        private static void DrawOverlay(Bitmap composed, RECT canvas, IntPtr overlay, CaptureOutcome outcome)
        {
            try
            {
                if (!GetWindowRect(overlay, out RECT rect) || rect.Width < 1 || rect.Height < 1)
                {
                    return;
                }

                if (Draw(composed, canvas, overlay, rect))
                {
                    outcome.Overlays.Add($"{rect.Width}x{rect.Height} at {rect.Left - canvas.Left},{rect.Top - canvas.Top}");
                }
            }
            catch (Exception)
            {
                // One popup failing to render is not a reason to lose the window behind it.
            }
        }

        /// <summary>
        /// Renders one window into its place on the composed canvas.
        /// </summary>
        /// <remarks>
        /// Always via a scratch bitmap, because PrintWindow only ever draws at the origin of the device
        /// context it is given and every layer here needs an offset.
        /// </remarks>
        private static bool Draw(Bitmap composed, RECT canvas, IntPtr hwnd, RECT rect)
        {
            Bitmap layer = null;
            try
            {
                layer = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);

                using (Graphics graphics = Graphics.FromImage(layer))
                {
                    IntPtr hdc = graphics.GetHdc();
                    try
                    {
                        if (!PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT))
                        {
                            return false;
                        }
                    }
                    finally
                    {
                        graphics.ReleaseHdc(hdc);
                    }
                }

                using (Graphics graphics = Graphics.FromImage(composed))
                {
                    graphics.DrawImage(layer, rect.Left - canvas.Left, rect.Top - canvas.Top);
                }

                return true;
            }
            finally
            {
                layer?.Dispose();
            }
        }

        private static RECT Union(RECT a, RECT b)
        {
            return new RECT()
            {
                Left = Math.Min(a.Left, b.Left),
                Top = Math.Min(a.Top, b.Top),
                Right = Math.Max(a.Right, b.Right),
                Bottom = Math.Max(a.Bottom, b.Bottom),
            };
        }

        private static void Encode(Bitmap composed, int maxWidth, CaptureOutcome outcome)
        {
            Bitmap scaled = null;
            try
            {
                Bitmap source = composed;

                if (composed.Width > maxWidth)
                {
                    int height = Math.Max(1, (int)Math.Round(composed.Height * (maxWidth / (double)composed.Width)));
                    scaled = new Bitmap(maxWidth, height, PixelFormat.Format32bppArgb);
                    using (Graphics graphics = Graphics.FromImage(scaled))
                    {
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.DrawImage(composed, 0, 0, maxWidth, height);
                    }
                    source = scaled;
                }

                using (MemoryStream stream = new MemoryStream())
                {
                    source.Save(stream, ImageFormat.Png);
                    outcome.Png = stream.ToArray();
                }

                outcome.Width = source.Width;
                outcome.Height = source.Height;
            }
            finally
            {
                scaled?.Dispose();
            }
        }

        private static bool Intersects(RECT a, RECT b)
        {
            return a.Left < b.Right && a.Right > b.Left && a.Top < b.Bottom && a.Bottom > b.Top;
        }
    }
}
