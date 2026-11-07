using System.Runtime.InteropServices;
using System.Text;

namespace WANG
{
    public record WindowInfo(
        nint Handle,
        uint ProcessId,
        Rectangle Bounds,
        Rectangle NormalBounds,
        string State,
        bool Focused,
        int Order,
        string Title);

    internal static class Native
    {
        internal delegate bool EnumProc(nint window, nint parameter);

        internal delegate void EventProc(nint hook, uint type, nint window, int objectId, int childId, uint thread, uint time);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect
        {
            public int Left, Top, Right, Bottom;

            public readonly Rectangle Rectangle => Rectangle.FromLTRB(Left, Top, Right, Bottom);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Point
        {
            public int X, Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Placement
        {
            public int Length, Flags, Show;
            public Point Min, Max;
            public Rect Normal;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Monitor
        {
            public int Size;
            public Rect Bounds, Work;
            public uint Flags;
        }

        [DllImport("user32.dll")]
        internal static extern bool EnumWindows(EnumProc callback, nint parameter);

        [DllImport("user32.dll")]
        internal static extern bool IsWindowVisible(nint window);

        [DllImport("user32.dll")]
        internal static extern bool IsWindow(nint window);

        [DllImport("user32.dll")]
        internal static extern bool IsIconic(nint window);

        [DllImport("user32.dll")]
        internal static extern bool IsZoomed(nint window);

        [DllImport("user32.dll")]
        internal static extern bool GetWindowRect(nint window, out Rect rect);

        [DllImport("user32.dll")]
        internal static extern bool GetWindowPlacement(nint window, ref Placement placement);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowText(nint window, StringBuilder text, int max);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetClassName(nint window, StringBuilder text, int max);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowTextLength(nint window);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        internal static extern nint GetWindowLongPtr(nint window, int index);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(nint window, out uint process);

        [DllImport("user32.dll")]
        internal static extern nint GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern nint MonitorFromWindow(nint window, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool GetMonitorInfo(nint monitor, ref Monitor info);

        [DllImport("user32.dll")]
        internal static extern nint SetWinEventHook(uint min, uint max, nint module, EventProc callback, uint process, uint thread, uint flags);

        [DllImport("user32.dll")]
        internal static extern bool UnhookWinEvent(nint hook);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmGetWindowAttribute(nint window, int attribute, out int value, int size);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(nint window, int command);
    }

    internal sealed class DesktopObserver : IDisposable
    {
        private readonly Native.EventProc callback;
        private readonly List<nint> hooks = [];
        public event Action<uint, nint>? Changed;
        public Exception? CallbackError { get; private set; }

        public DesktopObserver()
        {
            callback = (_, type, window, obj, child, _, _) =>
            {
                try
                {
                    if (window != 0 && child == 0 && (obj == 0 || type < 0x8000))
                    {
                        Changed?.Invoke(type, window);
                    }
                }
                catch (Exception error)
                {
                    CallbackError = error;
                }
            };
            foreach (var (min, max) in new (uint, uint)[]
            {
                (3, 3),
                (10, 11),
                (22, 23),
                (0x8000, 0x8004),
                (0x800A, 0x800B)
            })
            {
                var hook = Native.SetWinEventHook(min, max, 0, callback, 0, 0, 0);

                if (hook != 0)
                {
                    hooks.Add(hook);
                }
            }
        }

        public bool HooksAvailable => hooks.Count == 5;

        internal static bool ReadShape(nint window, out Rectangle normal, out string state)
        {
            normal = default;
            state = "Normal";
            if (!Native.GetWindowRect(window, out var rect))
            {
                return false;
            }

            normal = rect.Rectangle;
            state = Native.IsIconic(window) ? "Minimized" : Native.IsZoomed(window) ? "Maximized" : "Normal";
            var placement = new Native.Placement
            {
                Length = Marshal.SizeOf<Native.Placement>()
            };

            if (state != "Normal" && Native.GetWindowPlacement(window, ref placement))
            {
                normal = placement.Normal.Rectangle;
                if (((long)Native.GetWindowLongPtr(window, -20) & 0x80) == 0)
                {
                    var monitor = new Native.Monitor
                    {
                        Size = Marshal.SizeOf<Native.Monitor>()
                    };

                    if (Native.GetMonitorInfo(Native.MonitorFromWindow(window, 2), ref monitor))
                    {
                        normal.Offset(monitor.Work.Left - monitor.Bounds.Left, monitor.Work.Top - monitor.Bounds.Top);
                    }
                }
            }


            return true;
        }

        internal static void RestoreShape(nint window, Rectangle normal, string state)
        {
            if (Native.IsIconic(window) || Native.IsZoomed(window))
            {
                Native.ShowWindow(window, 4);
            }

            if (!Native.SetWindowPos(window, 0, normal.X, normal.Y, normal.Width, normal.Height, 0x14))
            {
                throw new InvalidOperationException($"Window restore failed ({Marshal.GetLastWin32Error()}).");
            }

            if (state == "Minimized")
            {
                Native.ShowWindow(window, 7);
            }

            if (state == "Maximized")
            {
                Native.ShowWindow(window, 3);
            }
        }

        public static List<WindowInfo> Snapshot(Func<nint, bool> includeOwn, bool readTitles)
        {
            List<WindowInfo> windows = [];
            var foreground = Native.GetForegroundWindow();

            Native.EnumWindows((window, _) =>
            {
                Native.GetWindowThreadProcessId(window, out var process);
                if (!Native.IsWindowVisible(window) || (process == Environment.ProcessId && !includeOwn(window)))
                {
                    return true;
                }

                if (process != Environment.ProcessId)
                {
                    var className = new StringBuilder(64);

                    Native.GetClassName(window, className, className.Capacity);
                    if (className.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
                    {
                        return true;
                    }

                    if (((long)Native.GetWindowLongPtr(window, -20) & 0x80) != 0 || Native.GetWindowTextLength(window) == 0)
                    {
                        return true;
                    }
                }

                if (Native.DwmGetWindowAttribute(window, 14, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
                {
                    return true;
                }

                if (!Native.GetWindowRect(window, out var rect))
                {
                    return true;
                }

                if (!ReadShape(window, out var normal, out var state))
                {
                    return true;
                }

                if (normal.Width < 20 || normal.Height < 20)
                {
                    return true;
                }

                var title = new StringBuilder(256);

                if (readTitles)
                {
                    Native.GetWindowText(window, title, title.Capacity);
                }

                windows.Add(new(window, process, rect.Rectangle, normal, state, foreground == window, windows.Count, title.ToString()));

                return true;
            }, 0);

            return windows;
        }

        public void Dispose()
        {
            foreach (var hook in hooks)
            {
                Native.UnhookWinEvent(hook);
            }

            GC.KeepAlive(callback);
        }
    }
}
