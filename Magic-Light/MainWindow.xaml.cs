using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NAudio.Wave;

namespace MagicLight
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(ref Win32Point pt);

        readonly String DEFAULT_EYE = "•";
        private Random rand = new Random();
        private WasapiLoopbackCapture capture = new WasapiLoopbackCapture();
        private DispatcherTimer faceControlTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        private WaveBuffer buffer;
        double audioScale = 0;

        // For dragging
        private Point anchorPoint = new Point();
        private bool Dragging = false;

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        };

        Double AppScale = 0.5;

        public static Point GetMousePosition()
        {
            var w32Mouse = new Win32Point();
            GetCursorPos(ref w32Mouse);

            return new Point(w32Mouse.X, w32Mouse.Y);
        }

        public MainWindow()
        {
            InitializeComponent();
            this.Topmost = true;

            Storyboard seconds = (Storyboard)second.FindResource("sbseconds");
            seconds.Begin();
            seconds.Seek(new TimeSpan(0, 0, 0, DateTime.Now.Second, 0));

            Storyboard minutes = (Storyboard)minute.FindResource("sbminutes");
            minutes.Begin();
            minutes.Seek(new TimeSpan(0, 0, DateTime.Now.Minute, DateTime.Now.Second, 0));

            Storyboard hours = (Storyboard)hour.FindResource("sbhours");
            hours.Begin();
            hours.Seek(new TimeSpan(0, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, 0));

            //Eye Movement
            DispatcherTimer dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 200);
            dispatcherTimer.Start();

            //Eye Blink
            DispatcherTimer dispatcherBlinkTimer = new DispatcherTimer();
            dispatcherBlinkTimer.Tick += new EventHandler(blinkTimer);
            dispatcherBlinkTimer.Interval = TimeSpan.FromSeconds(rand.Next(2, 10));
            dispatcherBlinkTimer.Start();

            //Outter border light
            DispatcherTimer dispatcherLightTimer = new DispatcherTimer();
            dispatcherLightTimer.Tick += new EventHandler(lightTimer);
            dispatcherLightTimer.Interval = new TimeSpan(0, 0, 0, 1, 0);
            dispatcherLightTimer.Start();

            //Audio Visualizer
            DispatcherTimer dispatcherVisualizerTimer = new DispatcherTimer();
            dispatcherVisualizerTimer.Tick += new EventHandler(DrawVisualizerTimer);
            dispatcherVisualizerTimer.Interval = new TimeSpan(0, 0, 0, 0, 200);
            dispatcherVisualizerTimer.Start();

            //Main Scale
            mainScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(AppScale, TimeSpan.FromMilliseconds(200)));
            mainScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(AppScale, TimeSpan.FromMilliseconds(200)));

            //Audio capture
            capture.DataAvailable += DataAvailable;
            capture.RecordingStopped += (s, a) =>
            {
                capture.Dispose();
            };
            capture.StartRecording();
        }

        public void DataAvailable(object sender, WaveInEventArgs e)
        {
            buffer = new WaveBuffer(e.Buffer); // save the buffer in the class variable
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            //Clock control
            if (clock.Opacity == 0)
            {
                DoubleAnimation showAnim = new DoubleAnimation();
                showAnim.Duration = TimeSpan.FromSeconds(1);
                showAnim.EasingFunction = new PowerEase() { EasingMode = EasingMode.EaseIn };
                showAnim.To = 1;
                clock.BeginAnimation(Grid.OpacityProperty, showAnim);

                DispatcherTimer clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
                clockTimer.Start();
                clockTimer.Tick += (s, args) =>
                {
                    showAnim.To = 0;
                    clock.BeginAnimation(Grid.OpacityProperty, showAnim);
                };
            }

            Dragging = true;
            anchorPoint = Mouse.GetPosition(null);
        }

        private void Window_MouseMove(object sender, MouseEventArgs e) {
            if (Dragging) {
                Point cursorPos = Mouse.GetPosition(null);
                this.Left += cursorPos.X - anchorPoint.X;
                this.Top += cursorPos.Y - anchorPoint.Y;
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e) {
            Dragging = false;
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0 && AppScale < 1.2)
            {
                AppScale += 0.1;
            }
            else if(AppScale > 0.2)
            {
                AppScale -= 0.1;
            }
            mainScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(AppScale, TimeSpan.FromMilliseconds(200)));
            mainScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(AppScale, TimeSpan.FromMilliseconds(200)));
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            //Console.WriteLine(GetMousePosition());

            Point cursorPos = GetMousePosition();
            Point relativePoint = this.PointFromScreen(cursorPos);
            relativePoint.X -= this.Width / 2;
            relativePoint.Y -= this.Height / 2;

            int leftEyeX = relativePoint.X > -60 ? (int)relativePoint.X % 100 : (int)relativePoint.X % 40;
            int leftEyeY = (int)relativePoint.Y % 60;

            int rightEyeX = relativePoint.X < 60 ? (int)relativePoint.X % 100 : (int)relativePoint.X % 40;
            int rightEyeY = (int)relativePoint.Y % 60;

            int mouthX = (int)relativePoint.X % 40;
            int mouthY = (int)relativePoint.Y % 40;

            moveLeftEye(leftEyeX, leftEyeY);
            moveRightEye(rightEyeX, rightEyeY);
            moveMouth(mouthX, mouthY);
        }

        private void lightTimer(object sender, EventArgs e)
        {
            if (audioScale == 0)
            {
                outterFireScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 1.06, TimeSpan.FromMilliseconds(200)));
                outterFireScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 1.06, TimeSpan.FromMilliseconds(200)));
                outterFireScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.06, 1, TimeSpan.FromMilliseconds(200)));
                outterFireScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.06, 1, TimeSpan.FromMilliseconds(200)));
            }
        }

        private void blinkTimer(object sender, EventArgs e)
        {
            if (!faceControlTimer.IsEnabled)
            {
                String leftBefore = (String)leftEye.Content;
                String rightBefore = (String)rightEye.Content;

                leftEye.Content = "-";
                rightEye.Content = "-";

                faceControlTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                faceControlTimer.Start();
                faceControlTimer.Tick += (s, args) =>
                {
                    faceControlTimer.Stop();
                    leftEye.Content = leftBefore;
                    rightEye.Content = rightBefore;
                };
            }
        }

        public void DrawVisualizerTimer(object sender, EventArgs e)
        {
            if (buffer == null)
            {
                Console.WriteLine("No buffer available");
                return;
            }

            int len = buffer.FloatBuffer.Length / 8;
            int M = 6;

            // fft
            NAudio.Dsp.Complex[] values = new NAudio.Dsp.Complex[len];
            for (int i = 0; i < len; i++)
            {
                values[i].Y = 0;
                values[i].X = buffer.FloatBuffer[i];
            }
            NAudio.Dsp.FastFourierTransform.FFT(true, M, values);

            double scale = 0;
            for (int i = 0; i < Math.Pow(2, M) / 2; i++)
            {
                //Console.Write(" " + values[i].X.ToString("N2"));
                scale += values[i].X;
            }
            if (scale < 0) scale *= -1;
            audioScale = scale * 5;
            //Console.WriteLine(" > " + scale);

            if (audioScale > 0)
            {
                outterFireScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 1 + audioScale, TimeSpan.FromMilliseconds(100)));
                outterFireScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 1 + audioScale, TimeSpan.FromMilliseconds(100)));
                outterFireScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1 + audioScale, 1, TimeSpan.FromMilliseconds(100)));
                outterFireScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1 + audioScale, 1, TimeSpan.FromMilliseconds(100)));
            }

            if (audioScale > 0.05)
            {
                if (!faceControlTimer.IsEnabled)
                {
                    String leftBefore = (String)leftEye.Content;
                    String rightBefore = (String)rightEye.Content;

                    leftEye.Content = "^";
                    rightEye.Content = "^";

                    faceControlTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    faceControlTimer.Start();
                    faceControlTimer.Tick += (s, args) =>
                    {
                        faceControlTimer.Stop();
                        leftEye.Content = leftBefore;
                        rightEye.Content = rightBefore;
                    };
                }
            }
        }

        private void moveLeftEye(int xPos, int yPos)
        {

            DoubleAnimation anim1 = new DoubleAnimation(xPos, new Duration(new TimeSpan(0, 0, 0, 1, 0)));
            DoubleAnimation anim2 = new DoubleAnimation(yPos, new Duration(new TimeSpan(0, 0, 0, 1, 0)));
            leftEyeTransform.BeginAnimation(TranslateTransform.XProperty, anim1);
            leftEyeTransform.BeginAnimation(TranslateTransform.YProperty, anim2);
        }

        private void moveRightEye(int xPos, int yPos)
        {
            DoubleAnimation anim1 = new DoubleAnimation(xPos, new Duration(new TimeSpan(0, 0, 0, 1, 0)));
            DoubleAnimation anim2 = new DoubleAnimation(yPos, new Duration(new TimeSpan(0, 0, 0, 1, 0)));
            rightEyeTransform.BeginAnimation(TranslateTransform.XProperty, anim1);
            rightEyeTransform.BeginAnimation(TranslateTransform.YProperty, anim2);
        }

        private void moveMouth(int xPos, int yPos)
        {
            DoubleAnimation anim1 = new DoubleAnimation(xPos, new Duration(new TimeSpan(0, 0, 0, 1, 0)));
            DoubleAnimation anim2 = new DoubleAnimation(yPos, new Duration(new TimeSpan(0, 0, 0, 1, 0)));
            mouthTransform.BeginAnimation(TranslateTransform.XProperty, anim1);
            mouthTransform.BeginAnimation(TranslateTransform.YProperty, anim2);
        }
    }

}
