using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoopFinder.Lib
{
    public class DebugInfo
    {
        public string Strategy { get; init; } = "";
        public double MinSimilarityUsed { get; init; }
        public int Frames { get; init; }
        public int MinSegFrames { get; init; }
        public int MatchFrames { get; init; }
        public int EarlyStartFrame { get; init; }
        public int LateStartFrame { get; init; }
        public double DurationSec { get; init; }
        public int SampleRate { get; init; }
        public int HopSize { get; init; }
        public double FramesPerSecond { get; init; }
        public IReadOnlyList<MatchSpan>? TopDiagSpans { get; init; }
        public MinMax[]? WaveformMinMax { get; init; }  // Downsampled for fast drawing
    }

}
