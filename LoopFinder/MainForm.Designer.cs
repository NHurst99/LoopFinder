using LoopFinder.Lib;
using NAudio.Wave;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace LoopFinder.WinForms
{
    public partial class MainForm : Form
    {
        private Button btnOpen;
        private Button btnAnalyze;
        private Button btnPlayPause;
        private CheckBox chkLoop;

        private NumericUpDown numMinSeg;
        private NumericUpDown numMinSim;
        private NumericUpDown numSR;
        private NumericUpDown numFrame;
        private NumericUpDown numHop;
        private NumericUpDown numMFCC;
        private NumericUpDown numTail;

        private Label lblFile;
        private TimelineControl timeline;

        private WaveOutEvent? output;
        private AudioFileReader? reader;
        private System.Windows.Forms.Timer uiTimer;

        private LoopPoints? points;
        private DebugInfo? debug;
        private string? currentPath;

        private void InitializeComponent()
        {
            Text = "Loop Finder Demo";
            Width = 1100; Height = 600;

            btnOpen = new() { Text = "Open..." };
            btnAnalyze = new() { Text = "Analyze" };
            btnPlayPause = new() { Text = "Play" };
            chkLoop = new() { Text = "Loop between detected segments", AutoSize = true };

            numMinSeg = new() { Minimum = 1, Maximum = 60, DecimalPlaces = 1, Increment = 0.5M, Value = 6 };
            numMinSim = new() { Minimum = 0, Maximum = 1, DecimalPlaces = 2, Increment = 0.05M, Value = 0.85M };
            numSR = new() { Minimum = 8000, Maximum = 48000, Increment = 1000, Value = 22050 };
            numFrame = new() { Minimum = 256, Maximum = 8192, Increment = 256, Value = 4096 };
            numHop = new() { Minimum = 128, Maximum = 8192, Increment = 128, Value = 2048 };
            numMFCC = new() { Minimum = 8, Maximum = 40, Increment = 1, Value = 20 };
            numTail = new() { Minimum = 0, Maximum = 5, DecimalPlaces = 2, Increment = 0.1M, Value = 1.0M };

            lblFile = new() { AutoSize = true, Text = "No file" };
            timeline = new() { Dock = DockStyle.Fill };

            uiTimer = new System.Windows.Forms.Timer { Interval = 33 };

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 80, AutoScroll = true };
            top.Controls.AddRange(new Control[] {
                btnOpen, btnAnalyze, btnPlayPause, chkLoop,
                new Label{Text=" minSeg(s):"}, numMinSeg,
                new Label{Text=" minSim:"},    numMinSim,
                new Label{Text=" SR:"},        numSR,
                new Label{Text=" frame:"},     numFrame,
                new Label{Text=" hop:"},       numHop,
                new Label{Text=" mfcc:"},      numMFCC,
                new Label{Text=" tail:"},      numTail,
                lblFile
            });

            Controls.Add(timeline);
            Controls.Add(top);
        }
    }
}
