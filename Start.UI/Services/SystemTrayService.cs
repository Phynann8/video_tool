using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Start.App.Services;
using Start.Core.Models;

namespace Start.UI.Services
{
    public interface ISystemTrayService
    {
        void Initialize(Window window);
        void ShowNotification(string title, string message);
        void RestoreWindow();
        void Dispose();
    }

    public sealed class SystemTrayService : ISystemTrayService, IDisposable
    {
        private const int WM_USER = 0x0400;
        private const int WM_TRAYICON = WM_USER + 1024;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;

        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;

        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;
        private const int NIF_INFO = 0x00000010;

        private const int NIIF_INFO = 0x00000001;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ExtractIconEx(string szFileName, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIcons);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private readonly IQueueManager _queueManager;
        private Window? _window;
        private HwndSource? _hwndSource;
        private IntPtr _hWnd;
        private IntPtr _hIcon = IntPtr.Zero;
        private bool _isAdded = false;
        private ContextMenu? _contextMenu;

        public SystemTrayService(IQueueManager queueManager)
        {
            _queueManager = queueManager;
            _queueManager.JobCompleted += OnJobCompleted;
        }

        public void Initialize(Window window)
        {
            _window = window;

            var helper = new WindowInteropHelper(window);
            _hWnd = helper.Handle;

            _hwndSource = HwndSource.FromHwnd(_hWnd);
            _hwndSource?.AddHook(WndProc);

            // Extract default application icon via Win32 API
            try
            {
                using var mainModule = Process.GetCurrentProcess().MainModule;
                if (mainModule?.FileName != null)
                {
                    ExtractIconEx(mainModule.FileName, 0, out _, out _hIcon, 1);
                }
            }
            catch
            {
                _hIcon = IntPtr.Zero;
            }

            if (_hIcon == IntPtr.Zero)
            {
                _hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION
            }

            CreateContextMenu();
            AddTrayIcon();

            _window.StateChanged += OnWindowStateChanged;
        }

        private void CreateContextMenu()
        {
            _contextMenu = new ContextMenu();

            var openItem = new MenuItem { Header = "Open Universal Media Downloader" };
            openItem.Click += (s, e) => RestoreWindow();
            _contextMenu.Items.Add(openItem);

            _contextMenu.Items.Add(new Separator());

            var exitItem = new MenuItem { Header = "Exit" };
            exitItem.Click += (s, e) =>
            {
                Dispose();
                Application.Current.Shutdown();
            };
            _contextMenu.Items.Add(exitItem);
        }

        private void AddTrayIcon()
        {
            if (_isAdded || _hWnd == IntPtr.Zero) return;

            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hWnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _hIcon,
                szTip = "Universal Media Downloader"
            };

            _isAdded = Shell_NotifyIcon(NIM_ADD, ref nid);
        }

        public void ShowNotification(string title, string message)
        {
            if (!_isAdded || _hWnd == IntPtr.Zero) return;

            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hWnd,
                uID = 1,
                uFlags = NIF_INFO,
                szInfoTitle = title.Length > 63 ? title.Substring(0, 60) + "..." : title,
                szInfo = message.Length > 255 ? message.Substring(0, 250) + "..." : message,
                dwInfoFlags = NIIF_INFO
            };

            Shell_NotifyIcon(NIM_MODIFY, ref nid);
        }

        private void OnJobCompleted(object? sender, DownloadJob job)
        {
            ShowNotification("Download Completed", $"{job.Title}");
        }

        private void OnWindowStateChanged(object? sender, EventArgs e)
        {
            if (_window?.WindowState == WindowState.Minimized)
            {
                _window.Hide();
            }
        }

        public void RestoreWindow()
        {
            if (_window == null) return;
            _window.Show();
            _window.WindowState = WindowState.Normal;
            _window.Activate();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_TRAYICON)
            {
                int mouseMsg = lParam.ToInt32() & 0xFFFF;
                if (mouseMsg == WM_LBUTTONUP)
                {
                    RestoreWindow();
                    handled = true;
                }
                else if (mouseMsg == WM_RBUTTONUP)
                {
                    if (_contextMenu != null)
                    {
                        _contextMenu.PlacementTarget = _window;
                        _contextMenu.IsOpen = true;
                        handled = true;
                    }
                }
            }

            return IntPtr.Zero;
        }

        public void Dispose()
        {
            _queueManager.JobCompleted -= OnJobCompleted;

            if (_isAdded && _hWnd != IntPtr.Zero)
            {
                var nid = new NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                    hWnd = _hWnd,
                    uID = 1
                };
                Shell_NotifyIcon(NIM_DELETE, ref nid);
                _isAdded = false;
            }

            if (_hIcon != IntPtr.Zero)
            {
                DestroyIcon(_hIcon);
                _hIcon = IntPtr.Zero;
            }

            _hwndSource?.RemoveHook(WndProc);

            if (_window != null)
            {
                _window.StateChanged -= OnWindowStateChanged;
            }
        }
    }
}
