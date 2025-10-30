using Fuzion.Programs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Fuzion.Extensions
{
    class GameExtensions
    {
        Transform cachedTransform;
        Game animatedGame;

        public void AnimateGameMovement(Game target, double newX, double newY, int milliseconds, bool reset = false)
        {
            animatedGame = target;
            cachedTransform = target.RenderTransform;

            TranslateTransform trans = new TranslateTransform();
            target.RenderTransform = trans;

            //Transform trans = target.RenderTransform;

            if (!reset)
            {
                DoubleAnimation anim1 = new DoubleAnimation(0, 0, new System.Windows.Duration(new TimeSpan(0, 0, 0, 0, milliseconds)));
                DoubleAnimation anim2 = new DoubleAnimation(0, newY, new System.Windows.Duration(new TimeSpan(0, 0, 0, 0, milliseconds)));
                anim1.Completed += Anim1_Completed;
                trans.BeginAnimation(TranslateTransform.XProperty, anim1);
                trans.BeginAnimation(TranslateTransform.YProperty, anim2);
            }
            else
            {
                trans.X = 0;
                trans.Y = 0;
            }

        }

        private void Anim1_Completed(object sender, EventArgs e)
        {
            //animatedGame.RenderTransform = cachedTransform;
        }
    }
}
