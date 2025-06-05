using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace pigco_input
{
    public class RawInputHandler
    {
        // 定数定義
        private const uint RID_INPUT = 0x10000003; // Raw Input Command
        private const ushort RIM_TYPEMOUSE = 0x00; // Raw Input Type: Mouse
        private const ushort RIM_TYPEKEYBOARD = 0x01; // Raw Input Type: Keyboard

        private const ushort HID_USAGE_PAGE_GENERIC = 0x01;
        private const ushort HID_USAGE_GENERIC_MOUSE = 0x02;
        private const ushort HID_USAGE_GENERIC_KEYBOARD = 0x06;
        private const uint RIDEV_INPUTSINK = 0x00000100;

        // キーボードフラグ
        private const ushort RI_KEY_MAKE = 0x00; // キーが押された
        private const ushort RI_KEY_BREAK = 0x01; // キーが離された
        private const ushort RI_KEY_E0 = 0x02; // E0
        private const ushort RI_KEY_E1 = 0x04; // E1

        // マウスボタンフラグ
        private const ushort RI_MOUSE_LEFT_BUTTON_DOWN = 0x0001;
        private const ushort RI_MOUSE_LEFT_BUTTON_UP = 0x0002;
        private const ushort RI_MOUSE_RIGHT_BUTTON_DOWN = 0x0004;
        private const ushort RI_MOUSE_RIGHT_BUTTON_UP = 0x0008;
        private const ushort RI_MOUSE_MIDDLE_BUTTON_DOWN = 0x0010;
        private const ushort RI_MOUSE_MIDDLE_BUTTON_UP = 0x0020;
        private const ushort RI_MOUSE_BUTTON_4_DOWN = 0x0040; // X1 ボタン押下
        private const ushort RI_MOUSE_BUTTON_4_UP = 0x0080;   // X1 ボタン解放
        private const ushort RI_MOUSE_BUTTON_5_DOWN = 0x0100; // X2 ボタン押下
        private const ushort RI_MOUSE_BUTTON_5_UP = 0x0200;   // X2 ボタン解放
        private const ushort RI_MOUSE_WHEEL = 0x0400;         // マウスホイール

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort UsagePage;
            public ushort Usage;
            public uint Flags;
            public IntPtr Target;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint Type;
            public uint Size;
            public IntPtr Device;
            public IntPtr WParam;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct RAWMOUSE
        {
            [FieldOffset(0)]
            public ushort Flags;

            [FieldOffset(2)]
            public ushort Reserved; // パディング用

            // union
            [FieldOffset(4)]
            public uint Buttons;

            [FieldOffset(4)]
            public ushort ButtonFlags;
            [FieldOffset(6)]
            public ushort ButtonData;

            [FieldOffset(8)]
            public uint RawButtons;
            [FieldOffset(12)]
            public int LastX;
            [FieldOffset(16)]
            public int LastY;
            [FieldOffset(20)]
            public uint ExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWKEYBOARD
        {
            public ushort MakeCode;
            public ushort Flags;
            public ushort Reserved;
            public ushort VKey;
            public uint Message;
            public uint ExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUT
        {
            public RAWINPUTHEADER Header;
            public UnionData Data;

            [StructLayout(LayoutKind.Explicit)]
            public struct UnionData
            {
                [FieldOffset(0)]
                public RAWMOUSE Mouse;

                [FieldOffset(0)]
                public RAWKEYBOARD Keyboard;
            }
        }

        private enum RawInputType
        {
            Mouse = RIM_TYPEMOUSE,
            Keyboard = RIM_TYPEKEYBOARD
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(
            [In] RAWINPUTDEVICE[] pRawInputDevices,
            uint uiNumDevices,
            uint cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(
            IntPtr hRawInput,
            uint uiCommand,
            IntPtr pData,
            ref uint pcbSize,
            uint cbSizeHeader);

        public event Action<int, int>? MouseMoved;
        public event Action<Keys, bool>? MouseButtonChanged;
        public event Action<int>? MouseWheelMoved;
        public event Action<Keys, bool>? KeyChanged;

        public void Hook(Window window)
        {
            if (HwndSource.FromVisual(window) is HwndSource source)
            {
                source.AddHook(WndProc);
                RegisterDevices(source.Handle);
            }
        }

        private void RegisterDevices(IntPtr windowHandle)
        {
            var devices = new RAWINPUTDEVICE[2];

            // マウス
            devices[0].UsagePage = HID_USAGE_PAGE_GENERIC;
            devices[0].Usage = HID_USAGE_GENERIC_MOUSE;
            devices[0].Flags = 0;
            devices[0].Target = windowHandle;

            // キーボード
            devices[1].UsagePage = HID_USAGE_PAGE_GENERIC;
            devices[1].Usage = HID_USAGE_GENERIC_KEYBOARD;
            devices[1].Flags = 0;
            devices[1].Target = windowHandle;

            if (!RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_INPUT = 0x00FF;

            if (msg == WM_INPUT)
            {
                ProcessInput(lParam);
                handled = true;
            }

            return IntPtr.Zero;
        }

        public void ProcessInput(IntPtr lParam)
        {
            uint dataSize = 0;
            GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dataSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));

            IntPtr data = Marshal.AllocHGlobal((int)dataSize);

            try
            {
                if (GetRawInputData(lParam, RID_INPUT, data, ref dataSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER))) == dataSize)
                {
                    RAWINPUT raw = Marshal.PtrToStructure<RAWINPUT>(data);

                    switch ((RawInputType)raw.Header.Type)
                    {
                        case RawInputType.Mouse:
                            HandleMouseInput(raw.Data.Mouse);
                            break;

                        case RawInputType.Keyboard:
                            HandleKeyboardInput(raw.Data.Keyboard);
                            break;
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(data);
            }
        }

        private void HandleMouseInput(RAWMOUSE mouse)
        {
            // マウスの移動
            if (mouse.LastX != 0 || mouse.LastY != 0)
            {
                MouseMoved?.Invoke(mouse.LastX, mouse.LastY);
            }

            // マウスボタンの状態
            ushort buttonFlags = mouse.ButtonFlags;
            if (buttonFlags != 0)
            {
                if ((buttonFlags & RI_MOUSE_LEFT_BUTTON_DOWN) != 0)
                    MouseButtonChanged?.Invoke(Keys.Mouse_L, true);
                if ((buttonFlags & RI_MOUSE_LEFT_BUTTON_UP) != 0)
                    MouseButtonChanged?.Invoke(Keys.Mouse_L, false);

                if ((buttonFlags & RI_MOUSE_RIGHT_BUTTON_DOWN) != 0)
                    MouseButtonChanged?.Invoke(Keys.Mouse_R, true);
                if ((buttonFlags & RI_MOUSE_RIGHT_BUTTON_UP) != 0)
                    MouseButtonChanged?.Invoke(Keys.Mouse_R, false);

                if ((buttonFlags & RI_MOUSE_MIDDLE_BUTTON_DOWN) != 0)
                    MouseButtonChanged?.Invoke(Keys.Mouse_M, true);
                if ((buttonFlags & RI_MOUSE_MIDDLE_BUTTON_UP) != 0)
                    MouseButtonChanged?.Invoke(Keys.Mouse_M, false);

                if ((buttonFlags & RI_MOUSE_BUTTON_4_DOWN) != 0)
                    MouseButtonChanged?.Invoke(Keys.Mouse_X1, true);
                if ((buttonFlags & RI_MOUSE_BUTTON_4_UP) != 0)
                    MouseButtonChanged?.Invoke(Keys.Mouse_X1, false);

                if ((buttonFlags & RI_MOUSE_BUTTON_5_DOWN) != 0)
                    MouseButtonChanged?.Invoke(Keys.Mouse_X2, true);
                if ((buttonFlags & RI_MOUSE_BUTTON_5_UP) != 0)
                    MouseButtonChanged?.Invoke(Keys.Mouse_X2, false);

                // マウスホイール
                if ((buttonFlags & RI_MOUSE_WHEEL) != 0)
                {
                    int wheelDelta = (short)mouse.ButtonData;
                    MouseWheelMoved?.Invoke(wheelDelta);
                }
            }
        }

        private void HandleKeyboardInput(RAWKEYBOARD keyboard)
        {
            // 下位2ビットだけで判定
            bool isDown = (keyboard.Flags & RI_KEY_BREAK) == RI_KEY_MAKE;
            bool isReleased = (keyboard.Flags & RI_KEY_BREAK) == RI_KEY_BREAK;

            if (isDown || isReleased)
            {
                ushort scan = keyboard.MakeCode;
                if ((keyboard.Flags & RI_KEY_E0) != 0)
                {
                    scan |= 0xE000; // E0 フラグがセットされている場合
                }
                else if ((keyboard.Flags & RI_KEY_E1) != 0)
                {
                    scan |= 0xE100; // E1 フラグがセットされている場合
                }
                KeyChanged?.Invoke((Keys)scan, isDown);
            }
        }
    }
}