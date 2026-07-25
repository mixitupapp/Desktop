#if !DEV_BRIDGE
#error The MCP dev bridge is being compiled without the DEV_BRIDGE constant. These files must only ever build under the Dev configuration. See Directory.Build.props.
#endif

using MixItUp.Base.Util;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MixItUp.WPF.Services.MCP.DevBridge
{
    /// <summary>
    /// Renders the running app to an image from inside the process.
    /// </summary>
    /// <remarks>
    /// This is the primary sense rather than a convenience. Given a picture of a screen, an agent can
    /// decide what to press the way a person does, which removes the need to enumerate controls at all
    /// for most navigation. The tree tools become a precision fallback for the questions a picture
    /// cannot answer, such as why a button is disabled.
    /// <para>
    /// Capture is in-process via <see cref="RenderTargetBitmap"/>, so it needs no screen access, works
    /// when the window is behind others, and cannot pick up anything outside the app. What it captures
    /// is what WPF draws, which has real consequences: see the limitations on
    /// <see cref="Capture"/>.
    /// </para>
    /// </remarks>
    [McpServerToolType]
    public class ScreenshotTools
    {
        /// <summary>
        /// Keeps a returned image to a sane size. A window at this width is still readable and costs
        /// roughly a thousand tokens, which is cheaper than the tree dump it replaces.
        /// </summary>
        private const int DefaultMaxWidth = 1400;

        [McpServerTool(Name = "ui_screenshot", ReadOnly = true)]
        [Description("Render the app, or one control inside it, to a PNG from within the process and return it as an image. Use this first when the question is what the app looks like or where to click: reading a screen is usually cheaper and more direct than dumping its element tree. Captures what WPF draws, so it works while the window is behind others, but cannot see popups, dropdowns, or embedded browsers, which are separate windows. Optionally also writes the file to disk.")]
        public static async Task<CallToolResult> Screenshot(
            [Description("What to capture. Accepts a handle from a previous call (e12), an x:Name prefixed with # (#ChatMessageTextBox), or a type name (ChatControl). Omit to capture the active window, falling back to the main window. Capturing a single control is the cheap way to check one change.")] string element = null,
            [Description("Downscale so the image is at most this many pixels wide. Clamped to 64-4096, default 1400. Lower it when you only need to confirm layout, raise it to read small text.")] int maxWidth = DefaultMaxWidth,
            [Description("Where to also write the PNG. Give a full file path ending in .png, or a directory, in which case a timestamped filename is generated. The directory is created if needed. Omit to only return the image.")] string saveToPath = null,
            [Description("Return the image in the tool result. Default true. Set false together with saveToPath when you only want the file on disk and do not want the image in context.")] bool includeImage = true)
        {
            ScreenshotResult result = await DevBridgeGate.RunOnUI("ui_screenshot", () => Capture(element, Clamp(maxWidth, 64, 4096)));

            List<ContentBlock> content = new List<ContentBlock>();

            if (result.Status != DevBridgeStatus.Ok)
            {
                content.Add(new TextContentBlock() { Text = $"{result.Status}: {result.Message}" });
                return new CallToolResult() { Content = content, IsError = true };
            }

            // Written outside the dispatch so the UI thread is not held for file IO.
            if (!string.IsNullOrWhiteSpace(saveToPath))
            {
                try
                {
                    result.SavedPath = WritePng(saveToPath, result.Png, result.CapturedType);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                    result.Message = $"The image was captured but could not be written to '{saveToPath}': {ex.GetType().Name}: {ex.Message}";
                }
            }

            if (includeImage)
            {
                // ImageContentBlock.Data is ReadOnlyMemory<byte>, but it wants the UTF-8 bytes OF the
                // base64 text, not the image bytes. Assigning the PNG directly compiles cleanly and
                // silently emits raw binary into a JSON string field, which the client cannot decode.
                content.Add(new ImageContentBlock()
                {
                    Data = Encoding.UTF8.GetBytes(Convert.ToBase64String(result.Png)),
                    MimeType = "image/png",
                });
            }

            content.Add(new TextContentBlock() { Text = Describe(result, includeImage) });

            return new CallToolResult()
            {
                Content = content,
                StructuredContent = JsonSerializer.SerializeToElement(new ScreenshotSummary()
                {
                    Status = result.Status,
                    CapturedType = result.CapturedType,
                    CapturedHandle = result.CapturedHandle,
                    Width = result.Width,
                    Height = result.Height,
                    ByteCount = result.Png.Length,
                    SavedPath = result.SavedPath,
                    Message = result.Message,
                }),
            };
        }

        /// <summary>
        /// Renders the target to PNG bytes. Must run on the UI thread.
        /// </summary>
        /// <remarks>
        /// Known blind spots, all following from capturing WPF's own drawing rather than the screen:
        /// <list type="bullet">
        /// <item>Popups are separate top-level windows, so an open ComboBox dropdown, a context menu, and
        /// this app's PopupBox intellisense do not appear in a window capture.</item>
        /// <item>Anything drawn by another technology in its own HWND, notably the WebView2 in the OAuth
        /// browser window, renders as a blank region. This is the airspace limitation and there is no way
        /// around it from inside WPF.</item>
        /// <item>A collapsed or zero-sized element has nothing to draw and is reported rather than
        /// returned as an empty image.</item>
        /// </list>
        /// </remarks>
        private static ScreenshotResult Capture(string element, int maxWidth)
        {
            ScreenshotResult result = new ScreenshotResult();

            DependencyObject target = UITools.ResolveTargetForCapture(element, result);
            if (target == null)
            {
                return result;
            }

            if (!(target is Visual visual))
            {
                result.Status = DevBridgeStatus.NotFound;
                result.Message = $"'{element}' resolved to a {target.GetType().Name}, which is not a Visual and cannot be rendered.";
                return result;
            }

            result.CapturedType = target.GetType().Name;
            result.CapturedHandle = HandleRegistry.Instance.HandleFor(target, "e");

            if (target is UIElement uiElement && uiElement.Visibility != Visibility.Visible)
            {
                result.Status = DevBridgeStatus.NotFound;
                result.Message = $"{result.CapturedType} is {uiElement.Visibility} so it draws nothing. Navigate to it first, or capture a visible ancestor.";
                return result;
            }

            Rect bounds = VisualTreeHelper.GetDescendantBounds(visual);
            if (bounds.IsEmpty || bounds.Width < 1 || bounds.Height < 1)
            {
                if (target is FrameworkElement frameworkElement && frameworkElement.ActualWidth >= 1 && frameworkElement.ActualHeight >= 1)
                {
                    bounds = new Rect(0, 0, frameworkElement.ActualWidth, frameworkElement.ActualHeight);
                }
                else
                {
                    result.Status = DevBridgeStatus.NotFound;
                    result.Message = $"{result.CapturedType} has no rendered area to capture, so it is laid out at zero size. Capture an ancestor instead.";
                    return result;
                }
            }

            // Render at the window's device scale so the image matches what is on screen, then downscale
            // only if it would exceed the requested width.
            double scale = VisualTreeHelper.GetDpi(visual).DpiScaleX;
            if (scale <= 0)
            {
                scale = 1.0;
            }

            if (bounds.Width * scale > maxWidth)
            {
                scale = maxWidth / bounds.Width;
            }

            int pixelWidth = Math.Max(1, (int)Math.Ceiling(bounds.Width * scale));
            int pixelHeight = Math.Max(1, (int)Math.Ceiling(bounds.Height * scale));

            RenderTargetBitmap bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96 * scale, 96 * scale, PixelFormats.Pbgra32);

            // Drawn through a VisualBrush rather than rendered directly, because rendering a child
            // element directly keeps its offset inside its parent and produces an image padded with
            // blank space. A VisualBrush's viewbox defaults to the visual's own content bounds, so
            // filling a rect of exactly that size captures it 1:1 with no offset.
            DrawingVisual drawing = new DrawingVisual();
            using (DrawingContext context = drawing.RenderOpen())
            {
                context.DrawRectangle(new VisualBrush(visual), null, new Rect(0, 0, bounds.Width, bounds.Height));
            }
            bitmap.Render(drawing);

            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using (MemoryStream stream = new MemoryStream())
            {
                encoder.Save(stream);
                result.Png = stream.ToArray();
            }

            result.Width = pixelWidth;
            result.Height = pixelHeight;
            return result;
        }

        private static string WritePng(string saveToPath, byte[] png, string capturedType)
        {
            string path = saveToPath.Trim();

            bool looksLikeFile = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
            string directory = looksLikeFile ? Path.GetDirectoryName(path) : path;

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!looksLikeFile)
            {
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                path = Path.Combine(path, $"{capturedType}-{stamp}.png");
            }

            File.WriteAllBytes(path, png);
            return Path.GetFullPath(path);
        }

        private static string Describe(ScreenshotResult result, bool includeImage)
        {
            string size = $"{result.Width}x{result.Height}px, {result.Png.Length / 1024}KB";
            string what = $"Captured {result.CapturedType} ({result.CapturedHandle}) at {size}.";

            if (!string.IsNullOrEmpty(result.SavedPath))
            {
                what += $" Saved to {result.SavedPath}.";
            }
            if (!includeImage)
            {
                what += " Image not included in this result by request.";
            }
            if (!string.IsNullOrEmpty(result.Message))
            {
                what += " " + result.Message;
            }

            return what;
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }

    /// <summary>
    /// The structured half of a screenshot result. Separate from the image content block so a caller can
    /// see what was captured, how big it was, and where it landed without decoding anything.
    /// </summary>
    public class ScreenshotSummary
    {
        public string Status { get; set; }

        public string CapturedType { get; set; }

        public string CapturedHandle { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int ByteCount { get; set; }

        public string SavedPath { get; set; }

        public string Message { get; set; }
    }

    public class ScreenshotResult : DevBridgeResult
    {
        internal byte[] Png { get; set; }

        public string CapturedType { get; set; }

        public string CapturedHandle { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public string SavedPath { get; set; }
    }
}
