using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MixItUp.WPF.Util
{
    public static class TaskbarFlashHelper
    {
        public static void Flash(Window window, bool stop = false)
        {
            if (window == null)
            {
                return;
            }

            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            FLASHWINFO flashInfo = new FLASHWINFO();
            flashInfo.cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(FLASHWINFO)));
            flashInfo.hwnd = handle;
            flashInfo.dwFlags = stop ? FlashWindowFlags.FLASHW_STOP : FlashWindowFlags.FLASHW_TRAY | FlashWindowFlags.FLASHW_TIMER;
            flashInfo.uCount = stop ? 0u : uint.MaxValue;
            flashInfo.dwTimeout = 0;

            FlashWindowEx(ref flashInfo);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public FlashWindowFlags dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        [Flags]
        private enum FlashWindowFlags : uint
        {
            FLASHW_STOP = 0,
            FLASHW_TRAY = 2,
            FLASHW_TIMER = 4,
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);
    }
}
