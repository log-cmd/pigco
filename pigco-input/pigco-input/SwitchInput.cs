using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            byte[] buf = new byte[22];

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

                buf[0] = (byte)btn;
            }

            {
                int btn = 0x00;
                btn |= s.Minus ? 0x01 : 0;
                btn |= s.Plus ? 0x02 : 0;
                btn |= s.RS ? 0x04 : 0;
                btn |= s.LS ? 0x08 : 0;
                btn |= s.Home ? 0x10 : 0;
                btn |= s.Capture ? 0x20 : 0;

                buf[1] = (byte)btn;
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
                buf[2] = (byte)btn;
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

            buf[3] = (byte)(LS24);
            buf[4] = (byte)(LS24 >> 8);
            buf[5] = (byte)(LS24 >> 16);
            buf[6] = (byte)(RS24);
            buf[7] = (byte)(RS24 >> 8);
            buf[8] = (byte)(RS24 >> 16);

            buf[9] = 0; // vibration report

            {
                buf[10] = (byte)(s.IMU[0].Accel.X);
                buf[11] = (byte)(s.IMU[0].Accel.X >> 8);
                buf[12] = (byte)(s.IMU[0].Accel.Y);
                buf[13] = (byte)(s.IMU[0].Accel.Y >> 8);
                buf[14] = (byte)(s.IMU[0].Accel.Z);
                buf[15] = (byte)(s.IMU[0].Accel.Z >> 8);
                buf[16] = (byte)(s.IMU[0].Gyro.X);
                buf[17] = (byte)(s.IMU[0].Gyro.X >> 8);
                buf[18] = (byte)(s.IMU[0].Gyro.Y);
                buf[19] = (byte)(s.IMU[0].Gyro.Y >> 8);
                buf[20] = (byte)(s.IMU[0].Gyro.Z);
                buf[21] = (byte)(s.IMU[0].Gyro.Z >> 8);
            }

            // IMU1,2はマイコン側でコピーさせる

            return buf;
        }
    }
}
