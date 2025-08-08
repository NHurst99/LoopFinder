using System;
using System.IO;
using System.Windows.Forms;
using NAudio.Wave;
using LoopFinder.Lib;

namespace LoopFinder.WinForms
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            WireUpEvents();
        }

        private void WireUpEvents()
        {
            btnOpen.Click += (_, __) => OnOpen();
            btnAnalyze.Click += (_, __) => OnAnalyze();
            btnPlayPause.Click += (_, __) => OnPlayPause();
            uiTimer.Tick += (_, __) => OnUiTick();
            timeline.SeekRequested += (_, sec) => Seek(sec);
            FormClosing += (_, __) => Cleanup();
        }

        private void OnOpen()
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Audio Files|*.wav;*.mp3;*.flac;*.aiff;*.ogg;*.wma|All Files|*.*"
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            Cleanup();

            currentPath = ofd.FileName;
            lblFile.Text = Path.GetFileName(currentPath);
            points = null;
            debug = null;
            timeline.DurationSec = 0;
            timeline.PlayheadSec = 0;
            timeline.Invalidate();
        }

        private void OnAnalyze()
        {
            if (string.IsNullOrEmpty(currentPath)) return;

            var result = LoopFinder.FindLoopPointsWithDebug(
                currentPath,
                (int)numSR.Value,
                (int)numFrame.Value,
                (int)numHop.Value,
                (int)numMFCC.Value,
                (double)numMinSeg.Value,
                (double)numMinSim.Value,
                tailGuardSec: (double)numTail.Value);

            points = result.Points;
            debug = result.Debug;
            timeline.SetData(result.Debug, result.Points);
            timeline.PlayheadSec = 0;
        }

        private void OnPlayPause()
        {
            if (output == null || reader == null)
            {
                if (string.IsNullOrEmpty(currentPath)) return;

                reader = new AudioFileReader(currentPath);
                output = new WaveOutEvent();
                output.Init(reader);
                output.Play();
                btnPlayPause.Text = "Pause";
                uiTimer.Start();
            }
            else if (output.PlaybackState == PlaybackState.Playing)
            {
                output.Pause();
                btnPlayPause.Text = "Play";
                uiTimer.Stop();
            }
            else
            {
                output.Play();
                btnPlayPause.Text = "Pause";
                uiTimer.Start();
            }
        }

        private void OnUiTick()
        {
            if (reader != null)
            {
                var sec = reader.CurrentTime.TotalSeconds;
                if (chkLoop.Checked && points != null && sec >= points.RewindFromSec)
                {
                    reader.CurrentTime = TimeSpan.FromSeconds(points.RewindToSec);
                    sec = reader.CurrentTime.TotalSeconds;
                }
                timeline.PlayheadSec = sec;
            }
        }

        private void Seek(double sec)
        {
            if (reader != null)
            {
                reader.CurrentTime = TimeSpan.FromSeconds(sec);
            }
            timeline.PlayheadSec = sec;
        }

        private void Cleanup()
        {
            uiTimer.Stop();
            output?.Stop();
            output?.Dispose();
            reader?.Dispose();
            output = null;
            reader = null;
        }
    }
}
