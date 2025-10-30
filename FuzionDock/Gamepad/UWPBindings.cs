using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Globalization;

namespace Fuzion.Gamepad
{
    class UWPBindings
    {
        [DllImport("xinput1_3.dll", EntryPoint = "#100")]
        static extern int Secret_get_gamepad(int playerIndex, out XINPUT_GAMEPAD_SECRET struc);

        public struct XINPUT_GAMEPAD_SECRET
        {
            public UInt32 eventCount;
            public ushort wButtons;
            public Byte bLeftTrigger;
            public Byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        public static XINPUT_GAMEPAD_SECRET xgs;

        public static bool TestHomeButton()
        {
            int stat;
            bool value;


            stat = Secret_get_gamepad(0, out xgs);
            //Console.WriteLine("Checking Guide controller " + i);

            //if (stat != 0)
            //    continue;

            value = (xgs.wButtons & 0x0400) != 0; // 0x0400 for home button - supposedly

            if (value)
                return true;

            //for (int i = 0; i < 9999; i++)
            //{
            //    stat = Secret_get_gamepad(0, out xgs);
            //    //int bit = ;

            //    value = (xgs.wButtons & FormattedBitCode(i)) != 0; // 0x0400 for home button - supposedly
            //    //Console.WriteLine("Testing bit: "+ bit);

            //    if (value)
            //    {
            //        Console.WriteLine(FormattedBitCode(i) + " RETURNED TRUE");
            //        return true;
            //    }

            //}

            return false;
        }

        static int FormattedBitCode(int index)
        {
            string template = "0x0000";
            Console.WriteLine("Index to string length "+index.ToString().Length);
            string replaced = template.Remove(template.Length - 1 - index.ToString().Length, index.ToString().Length) + index.ToString();
            Console.WriteLine("Replaced string is "+replaced);

            //return int.Parse(replaced, NumberStyles.AllowHexSpecifier);
            return (int)new System.ComponentModel.Int32Converter().ConvertFromString(replaced);
        }

        public static string ToHex(int value)
        {
            var res = String.Format("0x{0:X}", value);

            Console.WriteLine("ToHex method "+res);
            return res;
        }

        static void ConvertStringToHex(string str)
        {
            string decString = "0123456789";
            var hexString = string.Join("",
                decString.Select(c => String.Format("{0:X2}", Convert.ToInt32(c))));
            Console.WriteLine(hexString);
        }

        static DispatcherTimer dt;

        public static void StartTimer()
        {
            dt = new DispatcherTimer();
            dt.Tick += Dt_Tick;
            dt.Interval = TimeSpan.FromTicks(1);
            dt.Start();
        }

        private static void Dt_Tick(object sender, EventArgs e)
        {
            Console.WriteLine("Button is pressed: " + TestHomeButton());
        }
    }
}
