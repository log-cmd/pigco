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
        readonly RawInputHandler _rawInputHandler;
        readonly UdpClient _udpClient;
        readonly SwitchInput _input;

        readonly IPEndPoint _remote_wifi = new(IPAddress.Parse("192.168.0.83"), 12345);
        readonly IPEndPoint _remote_ap = new(IPAddress.Parse("192.168.4.1"), 12345);

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

        public MainWindow()
        {
            InitializeComponent();

            _rawInputHandler = new();
            _udpClient = new();
            _input = new();

            new Thread(Th)
            { IsBackground = true }.Start();
        }

        DateTime _LastSend = DateTime.MinValue;

        void Th()
        {
            while (true)
            {
                double dt = (DateTime.Now - _LastSend).TotalSeconds;
                if (dt > 1 / 120.0)
                {
                    dt = Math.Min(dt, 0.04);
                    SendTask(dt);
                    _LastSend = DateTime.Now;
                }
                Thread.Sleep(1);
            }
        }

        bool W = false;
        bool A = false;
        bool S = false;
        bool D = false;

        char LastV = '_';
        char LastH = '_';

        int MouseX = 0;
        int MouseY = 0;

        bool SenpukuNormal = false;
        bool SenpukuStealth = false;
        bool MiniMove = false;

        bool RapidFireZR = false;

        int LastX = 0;
        int LastY = 0;

        double Pitch = 0;
        double TotalTime = 0;
        bool Nice = false;
        bool SalmonMode = false;

        bool IkaRoll = false;

        bool USE_AP = true;

        double Sin(double hz)
        {
            return Math.Sin(2 * Math.PI * hz * TotalTime);
        }
        void ResetPitch()
        {
            Pitch = 7.5 * MathF.PI / 180;
        }

        class MacroRunner
        {
            private IEnumerator? _current;
            private double _wait = 0;
            public bool IsRunning => _current != null;

            /// <summary>
            /// マクロ開始。実行中は再実行しない
            /// </summary>
            public void Start(IEnumerator macro)
            {
                if (IsRunning) return;
                _current = macro;
                _wait = 0;
            }

            /// <summary>
            /// 進行処理。deltaTimeは秒
            /// </summary>
            public void Update(double deltaTime)
            {
                if (_current == null) return;

                if (_wait > 0)
                {
                    _wait -= deltaTime;
                    if (_wait > 0) return;
                }

                if (_current.MoveNext())
                {
                    if (_current.Current is double wait)
                        _wait = wait;
                    else
                        _wait = 0;
                }
                else
                {
                    _current = null;
                    _wait = 0;
                }
            }
        }

        readonly MacroRunner _ikaRollMacro = new();

        IEnumerator IkaRollMacro()
        {
            float LeftStickX = LastLSNonZero.X;
            float LeftStickY = LastLSNonZero.Y;

            DateTime begin = DateTime.Now;

            _input.LeftStickX = -LeftStickX;
            _input.LeftStickY = -LeftStickY;


            _input.B = true;

            while ((DateTime.Now - begin).TotalSeconds < 0.05)
            {
                _input.LeftStickX = -LeftStickX;
                _input.LeftStickY = -LeftStickY;
                yield return 0;
            }

            _input.B = false;

            while ((DateTime.Now - begin).TotalSeconds < 0.10)
            {
                _input.LeftStickX = -LeftStickX;
                _input.LeftStickY = -LeftStickY;
                yield return 0;
            }
        }

        (float X, float Y) LastLSNonZero = (0, 0);

        void SendTask(double deltaTime)
        {
            TotalTime += deltaTime;

            // ナイス連打
            _input.Down = Nice && Sin(15) > 0;

            // 移動
            {
                // 縦方向
                _input.LeftStickY = 0;
                if (LastV == 'W' && W) _input.LeftStickY = 1;
                else if (LastV == 'S' && S) _input.LeftStickY = -1;
                else if (W) _input.LeftStickY = 1;
                else if (S) _input.LeftStickY = -1;

                // 横方向
                _input.LeftStickX = 0;
                if (LastH == 'D' && D) _input.LeftStickX = 1;
                else if (LastH == 'A' && A) _input.LeftStickX = -1;
                else if (D) _input.LeftStickX = 1;
                else if (A) _input.LeftStickX = -1;

                // センプク
                _input.ZL = SenpukuNormal || SenpukuStealth;

                // しぶきが飛ばない調整
                if (SenpukuStealth || MiniMove)
                {
                    double len = Math.Sqrt(_input.LeftStickX * _input.LeftStickX + _input.LeftStickY * _input.LeftStickY);
                    double targetLen = MiniMove ? 0.25 : 0.84;
                    if (len > targetLen)
                    {
                        _input.LeftStickX = (float)(_input.LeftStickX * targetLen / len);
                        _input.LeftStickY = (float)(_input.LeftStickY * targetLen / len);
                    }
                }
            }

            if (IkaRoll)
            {
                IkaRoll = false;
                _ikaRollMacro.Start(IkaRollMacro());
            }

            _ikaRollMacro.Update(deltaTime);

            if (Math.Sqrt(_input.LeftStickX * _input.LeftStickX + _input.LeftStickY * _input.LeftStickY) > 0)
            {
                LastLSNonZero = (_input.LeftStickX, _input.LeftStickY);
            }


            int deltaX;
            int deltaY;
            {
                int mX = MouseX;
                int mY = MouseY;

                deltaX = mX - LastX;
                deltaY = mY - LastY;

                LastX = mX;
                LastY = mY;
            }

            float senseX = 0.3f;
            float senseY = 0.3f;

            _input.IMU[0].Gyro.X = (short)(Math.Clamp(-deltaX * 1 / deltaTime * senseX * Math.Sin(Pitch), short.MinValue, short.MaxValue));
            _input.IMU[0].Gyro.Y = (short)(Math.Clamp(deltaY * 0.5 / deltaTime * senseY, short.MinValue, short.MaxValue));
            _input.IMU[0].Gyro.Z = (short)(Math.Clamp(-deltaX * 1 / deltaTime * senseX * Math.Cos(Pitch), short.MinValue, short.MaxValue));

            if (_input.Y)
            {
                ResetPitch();
                _input.IMU[0] = new();
            }

            Pitch += senseY * -0.001 * deltaY;

            double radMax = 45 * Math.PI / 180;
            double radMin = -30 * Math.PI / 180;

            Pitch = Math.Clamp(Pitch, radMin, radMax);

            double Gravity = 4200;

            _input.IMU[0].Accel.X = (short)(Gravity * Math.Sin(Pitch));
            _input.IMU[0].Accel.Y = 0;
            _input.IMU[0].Accel.Z = (short)(Gravity * Math.Cos(Pitch));

            //連射
            if (RapidFireZR)
            {
                _input.ZR = Sin(15) > 0;
            }

            // タンサン
            if (_input.R)
            {
                _input.RightStickY = (float)Sin(10);
            }
            else
            {
                _input.RightStickY = 0;
            }

                // コピーで動くからコピーでいい
                _input.IMU[2] = _input.IMU[1];
            _input.IMU[1] = _input.IMU[0];

            byte[] buf = SwitchReport.MakeBuf(_input);

            using MemoryStream ms = new();
            ms.WriteByte(0x01);
            ms.WriteByte(0x02);
            ms.WriteByte(0x03);
            ms.WriteByte(0x04);
            ms.Write(buf, 0, buf.Length);

            try
            {
                IPEndPoint remote = USE_AP ? _remote_ap : _remote_wifi;
                _udpClient.Send(ms.ToArray(), remote);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UDP送信失敗: {ex.Message}");
            }
        }

        void OnKey(Keys key, bool isDown)
        {
            switch (key)
            {
                case Keys.Mouse_L:
                    _input.ZR = isDown;
                    break;
                case Keys.Mouse_R:
                    if (SalmonMode)
                    {
                        _input.A = isDown;
                    }
                    else
                    {
                        SenpukuStealth = isDown;
                    }
                    break;
                case Keys.Mouse_M:
                    _input.R = isDown;
                    break;
                case Keys.Mouse_X1:
                    Nice = isDown;
                    break;
                case Keys.Mouse_X2:
                    if (isDown)
                    {
                        RapidFireZR = true;
                    }
                    else
                    {
                        RapidFireZR = false;
                        _input.ZR = false; // 離した瞬間ZRもOFF
                    }
                    break;
                case Keys.F:
                    _input.A = isDown;
                    break;
                case Keys.Space:
                    _input.B = isDown;
                    break;
                case Keys.Tab:
                    _input.X = isDown;
                    break;
                case Keys.Q:
                    _input.Y = isDown;
                    break;
                case Keys.P:
                    _input.Plus = isDown;
                    break;
                case Keys.M:
                    _input.Minus = isDown;
                    break;
                case Keys.LeftShift:
                    SenpukuNormal = isDown;
                    break;
                case Keys.R:
                    _input.RS = isDown;
                    break;
                case Keys.Z:
                    _input.LS = isDown;
                    break;
                case Keys.H:
                    _input.Home = isDown;
                    break;
                case Keys.C:
                    if (isDown)
                    {
                        IkaRoll = true;
                    }
                    break;
                case Keys.D1:
                    _input.L = isDown;
                    break;
                case Keys.D2:
                    _input.R = isDown;
                    break;
                case Keys.D3:
                case Keys.UpArrow:
                    _input.Up = isDown;
                    break;
                case Keys.DownArrow:
                    _input.Down = isDown;
                    break;
                case Keys.LeftArrow:
                    _input.Left = isDown;
                    break;
                case Keys.RightArrow:
                    _input.Right = isDown;
                    break;
                case Keys.W:
                    W = isDown;
                    LastV = 'W';
                    break;
                case Keys.A:
                    A = isDown;
                    LastH = 'A';
                    break;
                case Keys.S:
                    S = isDown;
                    LastV = 'S';
                    break;
                case Keys.D:
                    D = isDown;
                    LastH = 'D';
                    break;
                case Keys.LeftCtrl:
                    MiniMove = isDown;
                    break;
                case Keys.F1:
                    if (isDown)
                    {
                        SalmonMode = !SalmonMode;
                    }
                    break;
                case Keys.B:
                    _input.Capture = isDown;
                    break;
                default:
                    break;
            }
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            _rawInputHandler.Hook(this);

            _rawInputHandler.MouseMoved += (dx, dy) =>
            {
                MouseX += dx;
                MouseY += dy;
            };

            _rawInputHandler.KeyChanged += OnKey;

            _rawInputHandler.MouseButtonChanged += OnKey;
        }
    }
}