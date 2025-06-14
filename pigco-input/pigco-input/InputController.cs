using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Reactive.Bindings;
using System.Collections;
using System.Diagnostics;
using System.IO;

namespace pigco_input
{
    internal class InputController
    {
        readonly UdpClient _udpClient;
        readonly SwitchInput _input;

        enum PIGCO_Type
        {
            PICOW_AP,
            PICOW_STA,
            RP2350ETH,
        }

        readonly PIGCO_Type PIGCO = PIGCO_Type.RP2350ETH;

        readonly IPEndPoint _remote;

        readonly List<(Keys, string, Action<bool>)> _keyActions;

        public InputController()
        {
            string remoteIP = "";
            if (PIGCO == PIGCO_Type.PICOW_AP) remoteIP = "192.168.4.1";
            else if (PIGCO == PIGCO_Type.PICOW_STA) remoteIP = "192.168.0.83";
            else if (PIGCO == PIGCO_Type.RP2350ETH) remoteIP = "192.168.0.200";
            _remote = new IPEndPoint(IPAddress.Parse(remoteIP), 12345);

            _udpClient = new();
            _input = new();

            _keyActions =
            [
                (Keys.W, "LStickUp", b => { LS_U=b; LastLS_V=1; }),
                (Keys.S, "LStickDown", b => { LS_D=b; LastLS_V=-1; }),
                (Keys.A, "LStickLeft", b => { LS_L=b; LastLS_H=-1; }),
                (Keys.D, "LStickRight", b => { LS_R=b; LastLS_H=1; }),
                (Keys.Mouse_L, "ZR", b => _input.ZR = b),
                (Keys.Mouse_R, "Swim(Stealth)", b =>
                {
                    if(UseStealthSwim.Value){ SwimStealth = b; }
                    else{ Swim2 = b; }
                }),
                (Keys.Mouse_M, "R", b => Bomb.Value = b),
                (Keys.Mouse_X1, "Rapid Nice", b => Nice.Value = b),
                (Keys.Mouse_X2, "Rapid ZR", b => { RapidFireZR = b; if(!b){ _input.ZR = false; } }),
                (Keys.F, "A", b => _input.A = b),
                (Keys.Space, "B", b => _input.B = b),
                (Keys.Tab, "X", b => _input.X = b),
                (Keys.Q, "Y", b => _input.Y = b),
                (Keys.P, "Plus", b => _input.Plus = b),
                (Keys.M, "Minus", b => _input.Minus = b),
                (Keys.LeftShift, "Swim", b => Swim = b),
                (Keys.LeftCtrl, "MicroMove", b => MiniMove = b),
                (Keys.R, "RS", b => _input.RS = b),
                (Keys.Z, "LS", b => _input.LS = b),
                (Keys.H, "Home", b => _input.Home = b),
                (Keys.C, "Ika Roll", b => {if (b) { IkaRoll = true; } }),
                (Keys.D1, "L", b => _input.L = b),
                (Keys.D2, "R", b => _input.R = b),
                (Keys.D3, "Come/Help", b => _input.Up = b),
                (Keys.F1, "Toggle UseStealthSwim", b => { if (b) { UseStealthSwim.Value = !UseStealthSwim.Value; }}),
                (Keys.B, "Capture", b => _input.Capture = b),

                (Keys.UpArrow, "Up", b => _input.Up = b),
                (Keys.DownArrow, "Down", b => _input.Down = b),
                (Keys.LeftArrow, "Left", b => _input.Left = b),
                (Keys.RightArrow, "Right", b => _input.Right = b),

                (Keys.Numpad8, "RStickUp", b => { RS_U = b; LastRS_V = 1; }),
                (Keys.Numpad2, "RStickDown", b => { RS_D = b; LastRS_V = -1; }),
                (Keys.Numpad4, "RStickLeft", b => { RS_L = b; LastRS_H = -1; }),
                (Keys.Numpad6, "RStickRight", b => { RS_R = b; LastRS_H = 1; }),

                (Keys.D6, "ReturnBase", b => { if(b){ ReturnBase = true;  } }),
            ];

            UseStealthSwim.Subscribe(x =>
            {
                if (x)
                {
                    SwimStealth = false;
                    Swim = false;
                    MiniMove = false;
                }
            });

            Nice.Subscribe(x =>
            {
                if (!x)
                {
                    _input.Down = false;
                }
            });

            Bomb.Subscribe(x =>
            {
                if (!x)
                {
                    _input.R = false;
                    _input.RightStickY = 0;
                }
            });

            KeyListStr.Value = string.Join(", ", _keyActions.Select(x => $"[{x.Item1}] {x.Item2}"));
        }

        DateTime _LastSend = DateTime.MinValue;

        public void Start()
        {
            new Thread(Th) { IsBackground = true }.Start();
        }

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

        bool LS_U, LS_D, LS_L, LS_R;
        int LastLS_V = 0;
        int LastLS_H = 0;

        bool RS_U, RS_D, RS_L, RS_R;
        int LastRS_V = 0;
        int LastRS_H = 0;

        int MouseX = 0;
        int MouseY = 0;

        bool Swim = false;
        bool Swim2 = false;
        bool SwimStealth = false;
        bool MiniMove = false;

        bool RapidFireZR = false;

        int LastX = 0;
        int LastY = 0;

        double Pitch = 0;
        double TotalTime = 0;

        bool IkaRoll = false;
        bool ReturnBase = false;

        ReactiveProperty<bool> Nice { get; } = new(false);
        ReactiveProperty<bool> Bomb { get; } = new(false);

        public ReactiveProperty<bool> UseStealthSwim { get; } = new(true);
        public ReactiveProperty<string> KeyListStr { get; } = new();

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
        readonly MacroRunner _returnBaseMacro = new();

        IEnumerator IkaRollMacro()
        {
            float LeftStickX = LastLSNonZero.X;
            float LeftStickY = LastLSNonZero.Y;

            DateTime begin = DateTime.Now;

            _input.LeftStickX = -LeftStickX;
            _input.LeftStickY = -LeftStickY;


            _input.B = true;

            while ((DateTime.Now - begin).TotalMilliseconds < 50)
            {
                _input.LeftStickX = -LeftStickX;
                _input.LeftStickY = -LeftStickY;
                yield return 0;
            }

            _input.B = false;

            while ((DateTime.Now - begin).TotalMilliseconds < 100)
            {
                _input.LeftStickX = -LeftStickX;
                _input.LeftStickY = -LeftStickY;
                yield return 0;
            }
        }

        IEnumerator ReturnBaseMacro()
        {
            DateTime begin = DateTime.Now;

            // X -> Down -> A

            // 押してない時間を確保
            while ((DateTime.Now - begin).TotalMilliseconds < 33)
            {
                _input.X = false;
                _input.Down = false;
                _input.A = false;
                yield return 0;
            }

            double pushTime = 1.0 / 20;

            _input.X = true;
            yield return pushTime;
            _input.X = false;
            yield return 0.2;
            _input.Down = true;
            yield return pushTime;
            _input.Down = false;
            yield return pushTime;
            _input.A = true;
            yield return pushTime;
            _input.A = false;
        }

        (float X, float Y) LastLSNonZero = (0, 0);

        void SendTask(double deltaTime)
        {
            TotalTime += deltaTime;

            // ナイス連打
            if (Nice.Value)
            {
                _input.Down = Sin(15) > 0;
            }

            // 移動
            {
                // 縦方向
                _input.LeftStickY = 0;
                if (LastLS_V == 1 && LS_U) _input.LeftStickY = 1;
                else if (LastLS_V == -1 && LS_D) _input.LeftStickY = -1;
                else if (LS_U) _input.LeftStickY = 1;
                else if (LS_D) _input.LeftStickY = -1;

                // 横方向
                _input.LeftStickX = 0;
                if (LastLS_H == 1 && LS_R) _input.LeftStickX = 1;
                else if (LastLS_H == -1 && LS_L) _input.LeftStickX = -1;
                else if (LS_R) _input.LeftStickX = 1;
                else if (LS_L) _input.LeftStickX = -1;

                // センプク
                _input.ZL = Swim || Swim2 || SwimStealth;

                // しぶきが飛ばない調整
                if (SwimStealth || MiniMove)
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

            // 右スティック
            {
                _input.RightStickY = 0;
                if (LastRS_V == 1 && RS_U) _input.RightStickY = 1;
                else if (LastRS_V == -1 && RS_D) _input.RightStickY = -1;
                else if (RS_U) _input.RightStickY = 1;
                else if (RS_D) _input.RightStickY = -1;

                _input.RightStickX = 0;
                if (LastRS_H == 1 && RS_R) _input.RightStickX = 1;
                else if (LastRS_H == -1 && RS_L) _input.RightStickX = -1;
                else if (RS_R) _input.RightStickX = 1;
                else if (RS_L) _input.RightStickX = -1;
            }

            if (IkaRoll)
            {
                IkaRoll = false;
                _ikaRollMacro.Start(IkaRollMacro());
            }

            _ikaRollMacro.Update(deltaTime);

            if (ReturnBase)
            {
                ReturnBase = false;
                _returnBaseMacro.Start(ReturnBaseMacro());
            }

            _returnBaseMacro.Update(deltaTime);

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
            if (Bomb.Value)
            {
                _input.R = true;
                _input.RightStickY = (float)Sin(10);
            }

            // コピーで動くからコピーでいい
            _input.IMU[2] = _input.IMU[1];
            _input.IMU[1] = _input.IMU[0];

            byte[] buf = SwitchReport.MakeBuf(_input);

            using MemoryStream ms = new();
            if (PIGCO != PIGCO_Type.RP2350ETH)
            {
                ms.WriteByte(0x01);
                ms.WriteByte(0x02);
                ms.WriteByte(0x03);
                ms.WriteByte(0x04);
                ms.Write(buf, 0, buf.Length);
            }
            else
            {
                ms.WriteByte(0x06); // magic
                ms.WriteByte(0x14); // magic2
                
                ushort crc = Crc16Ccitt.Compute(buf);
                ms.WriteByte((byte)(crc >> 8)); // CRC上位
                ms.WriteByte((byte)(crc & 0xFF)); // CRC下位

                ms.WriteByte((byte)buf.Length); // データ長
                ms.Write(buf, 0, buf.Length); // データ本体
            }

            try
            {
                IPEndPoint remote = _remote;
                _udpClient.Send(ms.ToArray(), remote);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UDP送信失敗: {ex.Message}");
            }
        }

        public static class Crc16Ccitt
        {
            public static ushort Compute(byte[] data)
            {
                ushort crc = 0xFFFF;
                foreach (byte b in data)
                {
                    crc ^= (ushort)(b << 8);
                    for (int i = 0; i < 8; i++)
                    {
                        if ((crc & 0x8000) != 0)
                            crc = (ushort)((crc << 1) ^ 0x1021);
                        else
                            crc <<= 1;
                    }
                }
                return crc;
            }
        }

        public void OnKey(Keys key, bool isDown)
        {
            _keyActions.Where(x => x.Item1 == key).FirstOrDefault().Item3?.Invoke(isDown);
        }

        public void OnMouseMoved(int x, int y)
        {
            MouseX += x;
            MouseY += y;
        }
    }
}
