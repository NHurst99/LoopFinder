using System;
using System.Windows.Forms;

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

        // keep your OnOpen/OnAnalyze/OnPlayPause/OnUiTick/Seek/Cleanup here
    }
}
