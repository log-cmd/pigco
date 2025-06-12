using Reactive.Bindings;
using System.Collections;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace pigco_input
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly InputController _controller;

        readonly RawInputHandler _rawInputHandler;

        #region ClipCursor

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ClipCursor(ref RECT lpRect);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ClipCursor(IntPtr lpRect);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);


        // ウィンドウロード時に一度カーソル拘束を設定（必要に応じて）
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsActive)
            {
                SetCursorClip();
            }
        }

        // ウィンドウがアクティブになったときにクライアント領域へカーソルを拘束
        private void Window_Activated(object sender, EventArgs e)
        {
            if (IsActive)
            {
                SetCursorClip();
            }
        }

        // ウィンドウが非アクティブになったときは、カーソルの拘束解除（Alt+Tab 時など）
        private void Window_Deactivated(object sender, EventArgs e)
        {
            ClipCursor(IntPtr.Zero);
        }

        // ウィンドウが閉じられる際にも必ず解放する
        private void Window_Closed(object sender, EventArgs e)
        {
            ClipCursor(IntPtr.Zero);
        }

        // マウスがクライアント領域に入った時
        private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (IsActive)
            {
                SetCursorClip();
            }
        }

        private void SetCursorClip()
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            // クライアント領域のRECTを取得
            GetClientRect(hwnd, out RECT clientRect);

            // 左上・右下をスクリーン座標に変換
            POINT topLeft = new() { X = clientRect.Left, Y = clientRect.Top };
            POINT bottomRight = new() { X = clientRect.Right, Y = clientRect.Bottom };
            ClientToScreen(hwnd, ref topLeft);
            ClientToScreen(hwnd, ref bottomRight);

            RECT rect = new()
            {
                Left = topLeft.X,
                Top = topLeft.Y,
                Right = bottomRight.X,
                Bottom = bottomRight.Y
            };

            ClipCursor(ref rect);
        }
        #endregion

        public ReactiveProperty<bool> UseStealthSwim => _controller.UseStealthSwim;
        public ReactiveProperty<string> KeyListStr => _controller.KeyListStr;

        public MainWindow()
        {
            InitializeComponent();

            _rawInputHandler = new();

            _controller = new();
            _controller.Start();

            this.DataContext = _controller;
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            _rawInputHandler.Hook(this);

            _rawInputHandler.MouseMoved += _controller.OnMouseMoved;
            _rawInputHandler.KeyChanged += _controller.OnKey;
            _rawInputHandler.MouseButtonChanged += _controller.OnKey;
        }
    }
}