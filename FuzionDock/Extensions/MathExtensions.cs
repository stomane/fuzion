using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fuzion.Extensions
{
    static class MathExtensions
    {
        public static float Lerp(float a, float b, float t)
        {
            //return a * (1 - t) + b * t;
            return a + (b - a) * t;
        }

        public static double Lerp(double a, double b, double t)
        {
            //return a * (1 - t) + b * t;
            return a + ((b - a) * Clamp(t, 0d, 1d));
        }

        //static double timeElapsed;
        public static void EnableLerpOverTime()
        {

        }
        static double timeElapsed;
        /// <summary>
        /// Lerp over a duration of time
        /// </summary>
        /// <param name="a">From</param>
        /// <param name="b">To</param>
        /// <param name="duration">Milliseconds</param>
        /// <returns></returns>
        public static double LerpOverTime(double a, double b, double deltaTime, double duration)
        {
            double res = 0;

            if (timeElapsed <= duration)
            {
                res = Lerp(a, b, timeElapsed / duration);
                Console.WriteLine("te/duration: "+timeElapsed/duration);
                Console.WriteLine("duration: "+duration);
                timeElapsed += deltaTime;
            }

            Console.WriteLine("LOT elapsed time " + timeElapsed);

            return res;
        }


        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }
    }
}
