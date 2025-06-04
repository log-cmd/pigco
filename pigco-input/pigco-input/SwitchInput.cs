using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace pigco_input
{
    struct IMUVector3
    {
        public short X;
        public short Y;
        public short Z;
    }

    struct IMUData
    {
        public IMUVector3 Accel;
        public IMUVector3 Gyro;
    }

    class SwitchInput
    {
        public SwitchInput()
        {
            IMU = new IMUData[3];
            Neutral();
        }

        public bool A;
        public bool B;
        public bool X;
        public bool Y;

        public bool Left;
        public bool Right;
        public bool Up;
        public bool Down;

        public bool R;
        public bool ZR;
        public bool L;
        public bool ZL;

        public bool RS;
        public bool LS;

        public bool Plus;
        public bool Minus;
        public bool Home;
        public bool Capture;

        public float LeftStickX;
        public float LeftStickY;

        public float RightStickX;
        public float RightStickY;

        public IMUData[] IMU;

        public void Neutral()
        {
            A = B = X = Y = false;
            Left = Right = Up = Down = false;
            R = ZR = L = ZL = false;
            RS = LS = false;
            Plus = Minus = Home = Capture = false;
            LeftStickX = LeftStickY = 0;
            RightStickX = RightStickY = 0;
            for(int i = 0; i < 3; i++)
            {
                IMU[i].Accel.X = IMU[i].Accel.Y = IMU[i].Accel.Z = 0;
                IMU[i].Gyro.X = IMU[i].Gyro.Y = IMU[i].Gyro.Z = 0;
            }
        }
    }

    class SwitchReport()
    {
        public static byte[] MakeBuf(in SwitchInput s)
        {
            byte[] buf = new byte[64];

            buf[0] = 0x30;
            buf[1] = 0x00; // timestamp
            buf[2] = 0x00; // battery, connection

            {
                int btn = 0x00;
                btn |= s.Y ? 0x01 : 0;
                btn |= s.X ? 0x02 : 0;
                btn |= s.B ? 0x04 : 0;
                btn |= s.A ? 0x08 : 0;
                // right SL
                // right SR
                btn |= s.R ? 0x40 : 0;
                btn |= s.ZR ? 0x80 : 0;

                buf[3] = (byte)btn;
            }

            {
                int btn = 0x00;
                btn |= s.Minus ? 0x01 : 0;
                btn |= s.Plus ? 0x02 : 0;
                btn |= s.RS ? 0x04 : 0;
                btn |= s.LS ? 0x08 : 0;
                btn |= s.Home ? 0x10 : 0;
                btn |= s.Capture ? 0x20 : 0;

                buf[4] = (byte)btn;
            }

            {
                int btn = 0x00;
                btn |= s.Down ? 0x01 : 0;
                btn |= s.Up ? 0x02 : 0;
                btn |= s.Right ? 0x04 : 0;
                btn |= s.Left ? 0x08 : 0;
                // left SR
                // left SL
                btn |= s.L ? 0x40 : 0;
                btn |= s.ZL ? 0x80 : 0;
                buf[5] = (byte)btn;
            }

            // 12ビットに変換
            int LX = (int)((s.LeftStickX + 1) * 0.5f * 0xFFF);
            int LY = (int)((s.LeftStickY + 1) * 0.5f * 0xFFF);
            int RX = (int)((s.RightStickX + 1) * 0.5f * 0xFFF);
            int RY = (int)((s.RightStickY + 1) * 0.5f * 0xFFF);

            LX = Math.Max(0, Math.Min(LX, 0xFFF));
            LY = Math.Max(0, Math.Min(LY, 0xFFF));
            RX = Math.Max(0, Math.Min(RX, 0xFFF));
            RY = Math.Max(0, Math.Min(RY, 0xFFF));

            int LS24 = LX | (LY << 12);
            int RS24 = RX | (RY << 12);

            buf[6] = (byte)(LS24);
            buf[7] = (byte)(LS24 >> 8);
            buf[8] = (byte)(LS24 >> 16);
            buf[9] = (byte)(RS24);
            buf[10] = (byte)(RS24 >> 8);
            buf[11] = (byte)(RS24 >> 16);

            buf[12] = 0; // vibration report

            const int IMU_begin = 13;

            for (int i = 0; i < 3; i++)
            {
                int j = IMU_begin + 12 * i;
                buf[j + 0] = (byte)(s.IMU[i].Accel.X);
                buf[j + 1] = (byte)(s.IMU[i].Accel.X >> 8);
                buf[j + 2] = (byte)(s.IMU[i].Accel.Y);
                buf[j + 3] = (byte)(s.IMU[i].Accel.Y >> 8);
                buf[j + 4] = (byte)(s.IMU[i].Accel.Z);
                buf[j + 5] = (byte)(s.IMU[i].Accel.Z >> 8);
                buf[j + 6] = (byte)(s.IMU[i].Gyro.X);
                buf[j + 7] = (byte)(s.IMU[i].Gyro.X >> 8);
                buf[j + 8] = (byte)(s.IMU[i].Gyro.Y);
                buf[j + 9] = (byte)(s.IMU[i].Gyro.Y >> 8);
                buf[j + 10] = (byte)(s.IMU[i].Gyro.Z);
                buf[j + 11] = (byte)(s.IMU[i].Gyro.Z >> 8);
            }

            return buf;
        }
    }
}
