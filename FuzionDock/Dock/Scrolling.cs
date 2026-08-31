using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Fuzion.MainWindow;
using System.Threading;
using Fuzion.Extensions;
using System.Windows.Threading;
using Fuzion.Properties;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using Fuzion.Programs;

namespace Fuzion.Dock
{
    static class Scrolling
    {
        private static bool keepCenterOnStart = true;
        public static Timer ScrollTimer { get; private set; }
        private static double FinalScrollLerpSpeed { get; set; } = Settings.Default.ScrollLerpSpeed;
        private static double BounceLerpSpeed => Settings.Default.ScrollLerpSpeed * 1.6d;

        public static void EnableSmoothScrolling()
        {
            Console.WriteLine("SV Loaded "+AppWindow.GridScrollViewer.IsLoaded);

            StartScrollViewerTimer();
        }

        /// <summary>
        /// ~120Hz. The lerp below is delta-time corrected, so the motion is identical to the old
        /// 1ms tick while doing roughly an eighth of the work - each tick costs two blocking
        /// Dispatcher.Invoke round-trips onto the UI thread, which at 1ms was thousands per second.
        /// </summary>
        private const int ScrollTickIntervalMs = 8;

        private static void StartScrollViewerTimer()
        {
            if (ScrollTimer != null)
                return;

            ScrollTimer = new Timer(ScrollTimerTick, null, ScrollTickIntervalMs, Timeout.Infinite);
        }

        private static double interpolatedScrollTarget;
        private static double ScrollTargetOffset => Settings.Default.StartupIconSize * 0.05d; //now is % of icon size //0.01;
        public static bool IsRemovingGame { get; set; }
        /// <summary>
        /// Delta time from threading timer (not accurate but works well with scrolling)
        /// </summary>
        private static double deltaTime;
        /// <summary>
        /// Measures delta time for Lerping purposes
        /// </summary>
        private static System.Diagnostics.Stopwatch deltaTimeStopwatch = new System.Diagnostics.Stopwatch();
        private static bool _isDockPerfectlyFittingGrid;
        private static void ScrollTimerTick(object state)
        {
            // stop stopwatch
            if(deltaTimeStopwatch.IsRunning)
                // TotalSeconds, not ElapsedMilliseconds: the latter is a whole-number of ms, so
                // at this tick rate it was constantly rounding to 0 or 1 - a 0 meant the lerp
                // didn't advance at all that tick, and a 1 could be off by ~100%. That
                // quantisation is what made the smooth scrolling drift and stutter.
                deltaTime = deltaTimeStopwatch.Elapsed.TotalSeconds;
            //Console.WriteLine("SW TIME: " + swTime);
            deltaTimeStopwatch.Restart();
            //Handle database push from here
            if (CheckGameObjectDBReadyness)
            {
                CheckForDatabasePush();
            }

            // Thread safe call to static mainwindow member
            Application.Current.Dispatcher.Invoke(() =>
            {
                _isDockPerfectlyFittingGrid = IsDockPerfectlyFittingScreen;
            });

            if (!_isDockPerfectlyFittingGrid)
            {
                if (keepCenterOnStart)
                {
                    ScrollToCenter();
                }
            }
            else
            {
                if(keepCenterOnStart)
                    keepCenterOnStart = false;
            }
           
            if (_isDockPerfectlyFittingGrid)
            {
                // Check if dock fits screen and scroll to center continuosly
                interpolatedScrollTarget = MathExtensions.Lerp(interpolatedScrollTarget, ScrollableMax() / 2d, FinalScrollLerpSpeed * 62.5d * deltaTime);
            }
            else
            {
                if (IsRemovingGame)
                {
                    // Interpolate over time
                    //interpolatedScrollTarget = MathExtensions.LerpOverTime(interpolatedScrollTarget, SmoothScrollTarget, swTime, gridAnimationLength*10);

                    //Temporary solution, just speed up scrollspeed
                    interpolatedScrollTarget = MathExtensions.Lerp(interpolatedScrollTarget, SmoothScrollTarget, 0.2d * 62.5d * deltaTime); 
                }
                else
                {
                    // Interpolate value
                    interpolatedScrollTarget = MathExtensions.Lerp(interpolatedScrollTarget, SmoothScrollTarget, FinalScrollLerpSpeed * 62.5d * deltaTime); // 62.5 is to account for discrepancy between old unscaled setting scroll speed(default 0.1), now using delta time
                }
            }
          

            // Ease the rubber band overshoot towards its target alongside the scroll itself
            overshootOffset = MathExtensions.Lerp(overshootOffset, overshootTarget, FinalScrollLerpSpeed * 62.5d * deltaTime);

            Application.Current.Dispatcher.Invoke(() =>
            {
                // Tell the scrollviewer to scroll
                if (IsDockHorizontal)
                    AppWindow.GridScrollViewer.ScrollToHorizontalOffset(interpolatedScrollTarget);
                else
                    AppWindow.GridScrollViewer.ScrollToVerticalOffset(interpolatedScrollTarget);

                ApplyOvershootTransform();

                // Keep tooltip at game
                if (GameTooltip.IsOpen)
                {
                    System.Reflection.MethodInfo mi = typeof(System.Windows.Controls.Primitives.Popup).GetMethod("UpdatePosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    _ = mi.Invoke(GameTooltip, null);
                }
            });

            if(Settings.Default.BounceScroll && !keepCenterOnStart)
            {
                ScrollEdgeBounce();
            }

            // reset timer
            try
                {
                ScrollTimer.Change(ScrollTickIntervalMs, Timeout.Infinite);
            }
            catch (ObjectDisposedException)
            {
                // sometimes happens when exiting fuzion
            }
        }

        #region Smooth Scrolling Scroll Viewer

        public static double SmoothScrollTarget { get; set; } = ScrollableMax() / 2d;

        public static bool canIssueGridPointUpdate;

        public enum ScrollDirection { Down, Up, None }
        public static ScrollDirection LastBounceScrollDirection = ScrollDirection.None;
        public static ScrollDirection CurrentScrollDirection;

        private static double UpperScrollLimit => ScrollableMax() - ActualGameSize / 2d;// * 0.9d; //old percentage based

        private static double LowerScrollLimit => ActualGameSize / 2d;// * 0.1d; //old percentage based

        private static DispatcherTimer PhysicalScrollingTimer { get; } = InitializePhysicalScrollingTimer();

        private static DispatcherTimer InitializePhysicalScrollingTimer()
        {
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(200d);
            timer.Tick += PhysicalScrolling_Tick;
            return timer;
        }

        private static void PhysicalScrolling_Tick(object sender, EventArgs e)
        {
            if (bounceStage == BounceStage.Finished)
            {
                LastBounceScrollDirection = ScrollDirection.None;
                bounceStage = BounceStage.Ready;
                Console.WriteLine("Bounce stage " + bounceStage);
                PhysicalScrollingTimer.Stop();
                FinalScrollLerpSpeed = Settings.Default.ScrollLerpSpeed;
                Console.WriteLine("Stopped Physical Scrolling");
            }
        }

        public static void ScrollToCenter(bool instant = false)
        {
            double center = ScrollableMax() / 2d;
            if (SmoothScrollTarget != center)
                ScrollTo(center, instant);
        }

        public static void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Unlock scroll
            keepCenterOnStart = false;

            // Check if perfectly fits screen and disregard scrollwheel
            if (IsDockPerfectlyFittingScreen)
            {
                e.Handled = true;
                return;
            }

            SetCurrentScrollDirection(e);

            // Restart physical scrolling timer
            //if (!PhysicalScrollingTimer.IsEnabled)
                PhysicalScrollingTimer.Stop();
                PhysicalScrollingTimer.Start();

            if (CurrentScrollDirection != LastBounceScrollDirection)
            {
                // do only if currently bouncing
                if (bounceStage != BounceStage.Ready)
                {
                    Console.WriteLine("Cancelling bounce");
                    LastBounceScrollDirection = ScrollDirection.None;
                    bounceStage = BounceStage.Ready;
                    FinalScrollLerpSpeed = Settings.Default.ScrollLerpSpeed;

                    // Release the rubber band and pin back to the edge itself. This used to clamp
                    // to Upper/LowerScrollLimit, which sit half a cell in and left the end icon
                    // cropped when a bounce was interrupted.
                    overshootTarget = 0d;

                    if (SmoothScrollTarget > ScrollableMax())
                    {
                        ScrollTo(ScrollableMax());
                    }

                    if (SmoothScrollTarget < 0d)
                    {
                        ScrollTo(0d);
                    }
                }
            }

            if (CurrentScrollDirection == LastBounceScrollDirection)
            {
                if (bounceStage != BounceStage.Ready)
                {
                    e.Handled = true;
                    return;
                }
            }

            // 180 is max icon size right now
            //double scrollDist = (1d / (AppWindow.mainGrid.Children.Count - 1d)) * (1d + (1d - Settings.Default.StartupIconSize / 180d));
            double scrollDist = ActualGameSize * Settings.Default.DockScrollSpeed;//1 game grid cell width/height

            //const double scrollMultiplier = 1d;
            double screenMultiplier;
            if (IsDockHorizontal && AppWindow.GridScrollViewer.ScrollableWidth != 0) // scroll horizontally
            {
                screenMultiplier = Position.Monitors.ActiveScreen.WorkingArea.Width / 1040d;

                //double increment = scrollDist * screenMultiplier * Settings.Default.DockScrollSpeed * Math.Abs(e.Delta / 120d); // 120 is default delta on scroll wheel
                double increment = scrollDist * Math.Abs(e.Delta / 120d); // 120 is default delta on scroll wheel
                //Console.WriteLine("ScreenMultiplier is " + screenMultiplier);
                //Console.WriteLine("Scroll dist is " + scrollDist);

                if(SmoothScrollTarget < 0)
                {
                    SmoothScrollTarget = 0;
                }

                if (SmoothScrollTarget > AppWindow.GridScrollViewer.ScrollableWidth)
                {
                    SmoothScrollTarget = AppWindow.GridScrollViewer.ScrollableWidth;
                }

                if (e.Delta < 0 && SmoothScrollTarget < AppWindow.GridScrollViewer.ScrollableWidth) //delta is negative, add increment to scroll down
                {
                    SmoothScrollTarget += increment;
                }

                if (e.Delta > 0 && SmoothScrollTarget > 0) //delta is positive, remove increment to scroll up
                {
                    SmoothScrollTarget -= increment;
                }

                PushAgainstEdge(e, increment, AppWindow.GridScrollViewer.ScrollableWidth);
            }

            if (!IsDockHorizontal && AppWindow.GridScrollViewer.ScrollableHeight != 0) // scroll vertically
            {
                screenMultiplier = Position.Monitors.ActiveScreen.WorkingArea.Height / 1040d;

                //double increment = scrollDist * screenMultiplier * Settings.Default.DockScrollSpeed * Math.Abs(e.Delta / 120d);
                double increment = scrollDist * Math.Abs(e.Delta / 120d); // 120 is default delta on scroll wheel

                //Console.WriteLine("Increment is " + increment);
                //Console.WriteLine("ScreenMultiplier is " + screenMultiplier);
                //Console.WriteLine("Scroll dist is "+scrollDist);

                if (SmoothScrollTarget < 0)
                {
                    SmoothScrollTarget = 0;
                }

                if (SmoothScrollTarget > AppWindow.GridScrollViewer.ScrollableHeight)
                {
                    SmoothScrollTarget = AppWindow.GridScrollViewer.ScrollableHeight;
                }

                if (e.Delta < 0 && SmoothScrollTarget < AppWindow.GridScrollViewer.ScrollableHeight) //delta is negative
                {
                    SmoothScrollTarget += increment;
                }

                if (e.Delta > 0 && SmoothScrollTarget > 0) //delta is positive
                {
                    SmoothScrollTarget -= increment;
                }

                PushAgainstEdge(e, increment, AppWindow.GridScrollViewer.ScrollableHeight);

                Console.WriteLine("Scroll increment " + increment);
            }

            Console.WriteLine("Scrollable Height " + AppWindow.GridScrollViewer.ScrollableHeight);
            Console.WriteLine("Current SST " + SmoothScrollTarget);
            Console.WriteLine("Scrollable Max " + ScrollableMax());

            canIssueGridPointUpdate = true;

            e.Handled = true;
        }

        /// <summary>
        /// Once the target is resting exactly on an edge the increment guards above stop moving
        /// it (0 is not &gt; 0), so nothing would ever read as "pushed past the end" again and the
        /// bounce could only ever play once. This nudges the target back out past the edge so
        /// another overshoot plays, re-confirming you're at the end.
        /// Only while the bounce is Ready - PhysicalScrollingTimer holds it in Finished for a
        /// grace period after each bounce, which is what stops it firing on every wheel notch.
        /// </summary>
        static void PushAgainstEdge(MouseWheelEventArgs e, double increment, double scrollableMax)
        {
            if (!Settings.Default.BounceScroll || bounceStage != BounceStage.Ready)
                return;

            if (e.Delta > 0 && SmoothScrollTarget <= 0d)
            {
                SmoothScrollTarget = -increment;
            }
            else if (e.Delta < 0 && SmoothScrollTarget >= scrollableMax)
            {
                SmoothScrollTarget = scrollableMax + increment;
            }
        }

        public static void ScrollTo(double target, bool instant = false)
        {
            Console.WriteLine("ScrollTo "+target);
            // Get call stack
            //var stackTrace = new System.Diagnostics.StackTrace();
            //// Get calling method name
            //Console.WriteLine(stackTrace.GetFrame(1).GetMethod().Name);

            if (instant)
            {
                if(IsDockHorizontal)
                {
                    AppWindow.GridScrollViewer.ScrollToHorizontalOffset(target);
                }
                else
                {
                    AppWindow.GridScrollViewer.ScrollToVerticalOffset(target);
                }
            }
            else
            {
                SmoothScrollTarget = target;
            }
        }

        public static void ScrollTo(Game game, bool instant = false)
        {
            // Every grid cell is one ActualGameSize (icon + both margins) across, so the game's
            // cell starts at index * ActualGameSize; offset by half a cell minus half a viewport
            // to centre it, then clamp into the scrollable range.
            //
            // This used to map the index proportionally onto ScrollableMax:
            //     ScrollableMax() * (index / (children - 2))
            // which is only correct when the viewport happens to be exactly one cell wide. For
            // any real viewport it under-scrolls, lining up only at the very first and last
            // game and drifting by up to half a viewport in between.
            double target = (game.Index * ActualGameSize) + (ActualGameSize / 2d) - (ViewportSize() / 2d);
            target = Math.Max(0d, Math.Min(target, ScrollableMax()));

            Console.WriteLine("ScrollTo Game " + target);

            if (instant)
            {
                if (Settings.Default.DockLocation <= 1)
                {
                    AppWindow.GridScrollViewer.ScrollToHorizontalOffset(target);
                }
                else
                {
                    AppWindow.GridScrollViewer.ScrollToVerticalOffset(target);
                }
            }
            else
            {
                SmoothScrollTarget = target;
            }
        }

        /// <summary>
        /// Smoothly scroll in increment from current position
        /// </summary>
        /// <param name="increment"></param>
        public static void ScrollToIncrement(double increment)
        {
            Console.WriteLine("ScrollTo Increment " + (SmoothScrollTarget+increment));
            //// Get call stack
            //var stackTrace = new System.Diagnostics.StackTrace();
            //// Get calling method name
            //Console.WriteLine(stackTrace.GetFrame(1).GetMethod().Name);
            SmoothScrollTarget += increment;
        }


        static void SetCurrentScrollDirection(MouseWheelEventArgs e)
        {
            // Current scroll direction
            if (e.Delta > 0)
            {
                CurrentScrollDirection = ScrollDirection.Up;
            }
            else
            {
                CurrentScrollDirection = ScrollDirection.Down;
            }
        }

        // FIX - Not bouncing back to suggested value if hasn't reached a full 1 or 0 (edge)
        /// <summary>
        /// Get scrollable max Width or Height depending on orientation
        /// </summary>
        /// <returns></returns>
        public static double ScrollableMax()
        {
            if(IsDockHorizontal)
            {
                return AppWindow.GridScrollViewer.ScrollableWidth;
            }
            else
            {
                return AppWindow.GridScrollViewer.ScrollableHeight;
            }
        }

        /// <summary>
        /// Get the visible Width or Height of the scroll viewer depending on orientation
        /// </summary>
        public static double ViewportSize()
        {
            if (IsDockHorizontal)
            {
                return AppWindow.GridScrollViewer.ViewportWidth;
            }
            else
            {
                return AppWindow.GridScrollViewer.ViewportHeight;
            }
        }
        enum BounceStage { Ready, Started, Bounced, Finished }
        static BounceStage bounceStage = BounceStage.Ready;

        /// <summary>
        /// How far past the end the rubber band pulls - half an icon.
        /// </summary>
        private static double OvershootDistance => ActualGameSize / 2d;

        /// <summary>
        /// Current and target rubber band displacement, in DIPs. A ScrollViewer clamps its offset
        /// to [0, ScrollableMax], so scrolling alone can't show anything past the end - the
        /// overshoot is a render transform on the scrolled content instead.
        /// Positive pulls the content towards the end it started from (down/right at the top or
        /// left edge), negative the other way.
        /// </summary>
        static double overshootOffset;
        static double overshootTarget;
        static TranslateTransform overshootTransform;

        /// <summary>
        /// Pushes the current overshoot onto the scrolled content. UI thread only.
        /// </summary>
        static void ApplyOvershootTransform()
        {
            if (AppWindow?.GridScrollOffsetParent == null)
                return;

            if (overshootTransform == null)
            {
                overshootTransform = new TranslateTransform();
                AppWindow.GridScrollOffsetParent.RenderTransform = overshootTransform;
            }

            if (IsDockHorizontal)
            {
                overshootTransform.X = overshootOffset;
                overshootTransform.Y = 0d;
            }
            else
            {
                overshootTransform.X = 0d;
                overshootTransform.Y = overshootOffset;
            }
        }
        //static bool waitingForBounce;
        //static bool hasBounced;
        /// <summary>
        /// One rubber band per edge push: pull half an icon past the end, then ease back so the
        /// last icon sits fully visible. The scroll offset itself is pinned to the edge the whole
        /// time - only the render transform moves - so this can't leave an icon cropped.
        /// </summary>
        static void ScrollEdgeBounce()
        {
            if (LastInputSource != InputSource.Mouse)
                return;

            // The wheel handler lets the target run past the ends before clamping, so a target
            // outside [0, max] is exactly "the user pushed against this edge". Using that rather
            // than a half-cell threshold band means resting at an edge can't re-arm the bounce.
            bool pushedPastUpper = SmoothScrollTarget > ScrollableMax();
            bool pushedPastLower = SmoothScrollTarget < 0d;

            if (bounceStage == BounceStage.Ready && (pushedPastUpper || pushedPastLower))
            {
                FinalScrollLerpSpeed = BounceLerpSpeed;
                LastBounceScrollDirection = CurrentScrollDirection;
                bounceStage = BounceStage.Started;

                if (pushedPastUpper)
                {
                    // Pin the scroll to the end, and pull the content up to reveal space past it
                    ScrollTo(ScrollableMax());
                    overshootTarget = -OvershootDistance;
                }
                else
                {
                    ScrollTo(0d);
                    overshootTarget = OvershootDistance;
                }

                System.Diagnostics.Debug.WriteLine($"[Bounce] Started - overshooting {overshootTarget:F1} at {(pushedPastUpper ? "upper" : "lower")} edge");
            }

            // Overshoot has stretched far enough, let it spring back
            if (bounceStage == BounceStage.Started
                && Math.Abs(overshootOffset) >= Math.Abs(overshootTarget) - ScrollTargetOffset)
            {
                overshootTarget = 0d;
                bounceStage = BounceStage.Bounced;
                System.Diagnostics.Debug.WriteLine($"[Bounce] Peak reached at {overshootOffset:F1}, returning");
            }

            // Back home - the last icon is fully visible again
            if (bounceStage == BounceStage.Bounced && Math.Abs(overshootOffset) <= 0.5d)
            {
                overshootOffset = 0d;
                overshootTarget = 0d;
                bounceStage = BounceStage.Finished;
                FinalScrollLerpSpeed = Settings.Default.ScrollLerpSpeed;
                System.Diagnostics.Debug.WriteLine("[Bounce] Finished - settled flush against the edge");
            }
        }

        #endregion
    }
}
