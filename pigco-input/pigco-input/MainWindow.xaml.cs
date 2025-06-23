using Reactive.Bindings;
using System.Diagnostics;
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

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetCursorPos(out POINT lpPoint);

        [LibraryImport("user32.dll", SetLastError = true)]
        private static partial int GetSystemMetrics(int nIndex);

        private const int SM_CXSIZE = 30;     // ボタンの幅
        private const int SM_CYSIZE = 31;     // ボタンの高さ
        private const int SM_CYCAPTION = 4;   // タイトルバーの高さ
        private const int SM_CXSIZEFRAME = 32; // フレーム幅

        // ウィンドウロード時に一度カーソル拘束を設定（必要に応じて）
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsActive)
            {
                SetCursorClip();
            }
        }

        // ウィンドウがアクティブになったときにクライアント領域へカーソルを拘束
        // ×ボタンで閉じる動作優先
        private void Window_Activated(object sender, EventArgs e)
        {
            if (!IsActive)
                return;

            // ウィンドウハンドル取得
            var hwnd = new WindowInteropHelper(this).Handle;

            // ウィンドウの位置とサイズ取得
            if (!GetWindowRect(hwnd, out RECT windowRect))
            {
                Debug.WriteLine("Failed to get window rectangle.");
                return;
            }

            // システムメトリクスからフレーム幅とボタンのサイズ取得
            int frameWidth = GetSystemMetrics(SM_CXSIZEFRAME);
            int buttonWidth = GetSystemMetrics(SM_CXSIZE);
            int buttonHeight = GetSystemMetrics(SM_CYSIZE);

            // ×ボタンの矩形を計算（右上からボタン幅分左にずらす）
            int closeRight = windowRect.Right - frameWidth;
            int closeLeft = closeRight - buttonWidth;
            int closeTop = windowRect.Top + frameWidth;
            int closeBottom = closeTop + buttonHeight;

            // マウスカーソルの位置取得
            GetCursorPos(out POINT cursor);

            // ×ボタンの上にマウスがあるか判定
            bool isOnClose =
                cursor.X >= closeLeft && cursor.X <= closeRight &&
                cursor.Y >= closeTop && cursor.Y <= closeBottom;

            Debug.WriteLine($"isOnClose={isOnClose}");

            if (!isOnClose)
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