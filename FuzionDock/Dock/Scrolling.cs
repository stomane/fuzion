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
          

            Application.Current.Dispatcher.Invoke(() =>
            {
                // Tell the scrollviewer to scroll
                if (IsDockHorizontal)
                    AppWindow.GridScrollViewer.ScrollToHorizontalOffset(interpolatedScrollTarget);
                else
                    AppWindow.GridScrollViewer.ScrollToVerticalOffset(interpolatedScrollTarget);

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
                    // Smooth scroll target should return to scroll limits so it doesn't bounce again
                    //ScrollTo(scrollViewerLerper);
                    if (SmoothScrollTarget > UpperScrollLimit)
                    {
                        ScrollTo(UpperScrollLimit);
                    }

                    if (SmoothScrollTarget < LowerScrollLimit)
                    {
                        ScrollTo(LowerScrollLimit);
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

                Console.WriteLine("Scroll increment " + increment);
            }

            Console.WriteLine("Scrollable Height " + AppWindow.GridScrollViewer.ScrollableHeight);
            Console.WriteLine("Current SST " + SmoothScrollTarget);
            Console.WriteLine("Scrollable Max " + ScrollableMax());

            canIssueGridPointUpdate = true;

            e.Handled = true;
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
        /// Set when a bounce settles against an edge, cleared once the user scrolls back inside
        /// the threshold band. The resting position is flush against the edge, which is past the
        /// trigger threshold, so without this the bounce immediately re-arms and repeats.
        /// </summary>
        static bool bounceSuppressedAtEdge;
        //static bool waitingForBounce;
        //static bool hasBounced;
        /// <summary>
        /// Runs every tick while scrollViewerLerper is above or below thresholds
        /// </summary>
        static void ScrollEdgeBounce()
        {
            if (LastInputSource != InputSource.Mouse)
                return;

            // Start bounce
            // (was "> UpperScrollLimit || < UpperScrollLimit", i.e. "!= UpperScrollLimit", which
            // is always true - so every tick bumped the lerp to the faster BounceLerpSpeed even
            // when nowhere near an edge, making all mouse scrolling run at 1.6x the configured
            // speed. The lower bound is meant to be LowerScrollLimit.)
            bool pastUpper = SmoothScrollTarget > UpperScrollLimit;
            bool pastLower = SmoothScrollTarget < LowerScrollLimit;

            // Re-arm only once the user has scrolled back inside the threshold band. A finished
            // bounce parks flush against the edge, which is itself past the threshold - without
            // this the next tick would immediately arm another bounce and it would fire on a
            // loop for as long as you sat at the end.
            if (!pastUpper && !pastLower)
            {
                bounceSuppressedAtEdge = false;
            }

            if ((pastUpper || pastLower) && !bounceSuppressedAtEdge)
            {
                if (bounceStage == BounceStage.Ready)
                {
                    // increase bounce speed
                    FinalScrollLerpSpeed = BounceLerpSpeed;

                    if (pastUpper)
                    {
                        ScrollTo(ScrollableMax());
                        LastBounceScrollDirection = CurrentScrollDirection;
                        bounceStage = BounceStage.Started;
                        System.Diagnostics.Debug.WriteLine($"[Bounce] Started at upper edge - target {SmoothScrollTarget:F1}, max {ScrollableMax():F1}");
                    }

                    if (pastLower)
                    {
                        ScrollTo(0);
                        LastBounceScrollDirection = CurrentScrollDirection;
                        bounceStage = BounceStage.Started;
                        System.Diagnostics.Debug.WriteLine($"[Bounce] Started at lower edge - target {SmoothScrollTarget:F1}");
                    }
                }

            }

            if (bounceStage == BounceStage.Started)
            {
                // has reached 1 - bounce
                if (interpolatedScrollTarget > ScrollableMax() - ScrollTargetOffset * 1d)
                {
                    Console.WriteLine("SVLerper reached 1 bouncing to " + UpperScrollLimit);
                    ScrollTo(UpperScrollLimit);
                    bounceStage = BounceStage.Bounced;
                    Console.WriteLine("Bounce stage " + bounceStage);

                }

                // has reached 0 - bounce
                if (interpolatedScrollTarget < 0 + ScrollTargetOffset * 1d)
                {
                    Console.WriteLine("SVLerper reached 0 bouncing to " + LowerScrollLimit);
                    ScrollTo(LowerScrollLimit);
                    bounceStage = BounceStage.Bounced;
                    Console.WriteLine("Bounce stage " + bounceStage);
                }
            }

            // waiting to return
            if (bounceStage == BounceStage.Bounced)
            {
                if (LastBounceScrollDirection == ScrollDirection.Up)
                {
                    // is again within threshold, finished full bounce
                    if (interpolatedScrollTarget >= LowerScrollLimit - ScrollTargetOffset)
                    {
                        bounceStage = BounceStage.Finished;
                        FinalScrollLerpSpeed = Settings.Default.ScrollLerpSpeed;

                        // Settle flush against the edge. The Upper/Lower limits are half a cell
                        // in from each end - fine as thresholds for detecting an edge push, but
                        // resting there leaves the first/last icon cropped in half.
                        bounceSuppressedAtEdge = true;
                        ScrollTo(0d);
                        System.Diagnostics.Debug.WriteLine("[Bounce] Finished - settled at lower edge (0)");
                    }
                }

                if (LastBounceScrollDirection == ScrollDirection.Down)
                {
                    if (interpolatedScrollTarget <= UpperScrollLimit + ScrollTargetOffset)
                    {
                        bounceStage = BounceStage.Finished;
                        FinalScrollLerpSpeed = Settings.Default.ScrollLerpSpeed;

                        bounceSuppressedAtEdge = true;
                        ScrollTo(ScrollableMax());
                        System.Diagnostics.Debug.WriteLine($"[Bounce] Finished - settled at upper edge ({ScrollableMax():F1})");
                    }
                }

            }
        }

        #endregion
    }
}
