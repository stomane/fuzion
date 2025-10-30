using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Fuzion.CustomUIElements
{
    public class DragAdorner : Adorner
    {
        Rect renderRect;

        public ImageSource AdornerImage { get; set; }
        public static Size TargetSize { get; set; }
        public static Point InitialPos { get; private set; }

        public DragAdorner(UIElement adornedElement, ImageSource img, Point startPos) : base(adornedElement)
        {
            InitialPos = startPos;
            AdornerImage = img;
            renderRect = new Rect(startPos, TargetSize);
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var b = new SolidColorBrush(Color.FromArgb(105,255,255,255));
            if(drawingContext != null)
            {
                drawingContext.DrawRoundedRectangle(b, null, renderRect, 10d, 10d);
                drawingContext.DrawImage(AdornerImage, new Rect( new Point(renderRect.Left + 5, renderRect.Top + 5) ,new Size(renderRect.Size.Width - 10d,renderRect.Size.Height - 10d)));
            }
        }
    }
}
