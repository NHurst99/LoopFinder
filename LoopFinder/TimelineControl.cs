using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LoopFinder.Lib;

namespace LoopFinder.WinForms
{
    public class TimelineControl : Control
    {
        public double DurationSec { get; set; }
        public double PlayheadSec { get { return _playheadSec; } set { _playheadSec = value; Invalidate(); } }

        public event EventHandler<double>? SeekRequested;

        private DebugInfo? _debug;
        private LoopPoints? _points;
        private double _playheadSec;
        public TimelineControl()
        {
            DoubleBuffered = true;
            BackColor = Color.Black;
            ForeColor = Color.White;
            Height = 300;
            MouseDown += OnMouseDown;
        }

        public void SetData(DebugInfo dbg, LoopPoints pts)
        {
            _debug = dbg; _points = pts; DurationSec = dbg.DurationSec; Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var rect = ClientRectangle;
            g.Clear(BackColor);

            // draw grid
            using var gridPen = new Pen(Color.FromArgb(40, 255, 255, 255));
            int gridLines = 10;
            for (int i = 1; i < gridLines; i++)
            {
                int x = rect.Left + i * rect.Width / gridLines;
                g.DrawLine(gridPen, x, rect.Top, x, rect.Bottom);
            }

            // waveform
            if (_debug?.WaveformMinMax != null && _debug.WaveformMinMax.Length > 0)
            {
                var midY = rect.Top + rect.Height / 2;
                int N = _debug.WaveformMinMax.Length;
                for (int i = 0; i < N; i++)
                {
                    float x = rect.Left + (float)i / (N - 1) * rect.Width;
                    float y1 = midY - _debug.WaveformMinMax[i].Max * rect.Height * 0.45f;
                    float y2 = midY - _debug.WaveformMinMax[i].Min * rect.Height * 0.45f;
                    using var pen = new Pen(Color.FromArgb(120, 100, 200, 255));
                    g.DrawLine(pen, x, y1, x, y2);
                }
            }

            // segments
            if (_points != null)
            {
                DrawSegment(g, rect, _points.StartEarlySec, _points.SegmentDurationSec, Color.FromArgb(80, 0, 200, 0));
                DrawSegment(g, rect, _points.StartLateSec, _points.SegmentDurationSec, Color.FromArgb(80, 200, 120, 0));
            }

            // playhead
            if (DurationSec > 0)
            {
                int x = rect.Left + (int)(rect.Width * (PlayheadSec / DurationSec));
                using var headPen = new Pen(Color.FromArgb(255, 255, 255, 255), 2);
                g.DrawLine(headPen, x, rect.Top, x, rect.Bottom);
            }
        }

        private void DrawSegment(Graphics g, Rectangle rect, double startSec, double durSec, Color fill)
        {
            if (DurationSec <= 0 || durSec <= 0) return;
            int x = rect.Left + (int)(rect.Width * (startSec / DurationSec));
            int w = Math.Max(1, (int)(rect.Width * (durSec / DurationSec)));
            var r = new Rectangle(x, rect.Top + 5, w, rect.Height - 10);
            using var b = new SolidBrush(fill);
            using var p = new Pen(Color.FromArgb(180, fill.R, fill.G, fill.B));
            g.FillRectangle(b, r);
            g.DrawRectangle(p, r);
        }

        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            if (DurationSec <= 0) return;
            double sec = (double)e.X / Width * DurationSec;
            SeekRequested?.Invoke(this, sec);
        }
    }
}