using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Fuzion.WindowsManager
{
    static class EditGameWindowsManager
    {

        static double sWidth = SystemParameters.PrimaryScreenWidth;
        static double sHeight = SystemParameters.PrimaryScreenHeight;
        static double wHeight = MainWindow.AppWindow.ActualHeight;

        static int maxWindowCountPerRow;
        static int maxWindowCountPerColumn;

        static List<Vector> freeGridPositions = null;
        static List<Vector> takenGridPositions = new List<Vector>();
        static List<Window> windowRef = new List<Window>();

        public static Vector GetNextWindowPosition(Window wnd)
        {
            Vector res = new Vector(0, 0);

            maxWindowCountPerRow = Convert.ToInt32(sWidth / wnd.Width);

            maxWindowCountPerColumn = Convert.ToInt32((sHeight - wHeight) / wnd.Height);

            if(freeGridPositions == null || freeGridPositions.Count == 0 && takenGridPositions.Count > 0)
            {
                // reset and/or initialize

                freeGridPositions = new List<Vector>();
                takenGridPositions = new List<Vector>();

                for (int y = 0; y < maxWindowCountPerColumn; y++)
                {
                    for (int x = 0; x < maxWindowCountPerRow; x++)
                    {
                        freeGridPositions.Add(new Vector(wnd.Width * x, wnd.Height * y + MainWindow.AppWindow.Height));
                    }
                }
            }

            Console.WriteLine($"Max Rows: {maxWindowCountPerRow} Max Columns: {maxWindowCountPerColumn}");

            for (int i = 0; i < freeGridPositions.Count; i++)
            {
                Console.WriteLine($"Free pos {i} is {freeGridPositions[i]}");
            }

            res = freeGridPositions[0];
            windowRef.Add(wnd);
            Console.WriteLine($"Returning position of x: {freeGridPositions[0].X} and y: {freeGridPositions[0].Y} ");

            takenGridPositions.Add(freeGridPositions[0]);
            freeGridPositions.RemoveAt(0);

            return res;
        }

        public static void RemoveWndRef(Window wnd)
        {
            try
            {
                int index = windowRef.IndexOf(wnd);

                freeGridPositions.Insert(0, takenGridPositions[index]);

                takenGridPositions.RemoveAt(index);
            }
            catch (Exception)
            {

            }
           
        }
    }
}
