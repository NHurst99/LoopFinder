using LoopFinder.Lib;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NWaves.FeatureExtractors;
using NWaves.FeatureExtractors.Options;
using NWaves.Signals;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LoopFinder.Lib
{
    public record LoopPoints(
        double RewindFromSec,
        double RewindToSec,
        double SegmentDurationSec,
        double StartEarlySec,
        double StartLateSec,
        double FramesPerSecond
    );

    public record MatchSpan(int EarlyStartFrame, int LateStartFrame, int Length);

    public struct MinMax
    {
        public float Min;
        public float Max;
    }


    public static class LoopFinder
    {
        // Back-compat API
        public static LoopPoints FindLoopPoints(
            string path,
            int targetSampleRate = 22050,
            int frameSize = 4096,
            int hopSize = 2048,
            int mfccCount = 20,
            double minSegSec = 6.0,
            double minSimilarity = 0.85,
            bool preferSecondHalf = true,
            double tailGuardSec = 1.0)
            => FindLoopPointsWithDebug(path, targetSampleRate, frameSize, hopSize, mfccCount,
                                       minSegSec, minSimilarity, preferSecondHalf, tailGuardSec).Points;

        // New debug-friendly API
        public static (LoopPoints Points, DebugInfo Debug) FindLoopPointsWithDebug(
            string path,
            int targetSampleRate = 22050,
            int frameSize = 4096,
            int hopSize = 2048,
            int mfccCount = 20,
            double minSegSec = 6.0,
            double minSimilarity = 0.85,
            bool preferSecondHalf = true,
            double tailGuardSec = 1.0,
            int waveformBins = 2000)
        {
            // 1) Decode, resample, mono
            float[] samples;
            int sr;
            using (var afr = new AudioFileReader(path))
            {
                ISampleProvider provider = afr;

                if (afr.WaveFormat.Channels == 2)
                {
                    provider = new StereoToMonoSampleProvider(provider);
                }

                if (provider.WaveFormat.SampleRate != targetSampleRate)
                {
                    provider = new WdlResamplingSampleProvider(provider, targetSampleRate);
                }

                var buff = new List<float>(Math.Max(1024, (int)(afr.Length / sizeof(float))));
                var tmp = new float[provider.WaveFormat.SampleRate * provider.WaveFormat.Channels];
                int read;
                while ((read = provider.Read(tmp, 0, tmp.Length)) > 0)
                {
                    for (int i = 0; i < read; i++) buff.Add(tmp[i]);
                }
                samples = buff.ToArray();
                sr = provider.WaveFormat.SampleRate;
            }

            double duration = (double)samples.Length / sr;
            double fps = (double)sr / hopSize;
            int minSegFrames = Math.Max(1, (int)Math.Round(minSegSec * fps));

            // 2) MFCC features with NWaves
            var opts = new MfccOptions
            {
                SamplingRate = sr,
                FeatureCount = mfccCount,
                FrameSize = frameSize,
                HopSize = hopSize,
                Window = NWaves.Windows.WindowType.Hamming,
                FilterBankSize = 40,
                PreEmphasis = 0.97f
            };
            var extractor = new MfccExtractor(opts);
            var signal = new DiscreteSignal(sr, samples);
            var mfccList = extractor.ComputeFrom(signal);   // List<float[]>
            var feats = mfccList.Select(v => v.ToArray()).ToArray();

            int T = feats.Length;
            if (T < minSegFrames * 2) throw new Exception("Audio too short for requested min segment length.");

            // L2 normalize frames
            for (int t = 0; t < T; t++)
            {
                double norm = Math.Sqrt(feats[t].Sum(x => x * x)) + 1e-10;
                for (int d = 0; d < feats[t].Length; d++) feats[t][d] = (float)(feats[t][d] / norm);
            }

            double CosSim(int i, int j)
            {
                var a = feats[i];
                var b = feats[j];
                double s = 0;
                for (int d = 0; d < a.Length; d++) s += a[d] * b[d];
                return s; // vectors are unit norm
            }

            // 3) Cross-halves similarity and capture top spans for debug
            int h = T / 2;
            int W = T - h;
            var mask = new bool[h, W];
            var thresholds = new[] { minSimilarity, minSimilarity - 0.05, minSimilarity - 0.10, 0.75, 0.70, 0.65, 0.60 };

            (int Lbest, int iBest, int jBest) = (0, 0, 0);
            string strategy = "cross";
            double usedSim = thresholds[0];
            List<MatchSpan> topSpans = new();

            foreach (var thr in thresholds)
            {
                for (int i = 0; i < h; i++)
                    for (int j = 0; j < W; j++)
                        mask[i, j] = CosSim(i, h + j) >= thr;

                // scan diagonals and also collect top N spans
                (Lbest, iBest, jBest) = LongestRunOnDiagonals(mask, out var spans);
                if (spans.Count > 0) topSpans = spans;
                if (Lbest >= minSegFrames) { usedSim = thr; break; }
            }

            if (Lbest < minSegFrames)
            {
                // Fallback global (no spans collection here to keep it reasonable)
                strategy = "global";
                var Sfull = new double[T, T];
                for (int i = 0; i < T; i++)
                {
                    for (int j = i + 1; j < T; j++)
                    {
                        var s = CosSim(i, j);
                        // mask out near-diagonal within minSeg
                        if (Math.Abs(j - i) < minSegFrames) s = double.NegativeInfinity;
                        Sfull[i, j] = s;
                    }
                }

                int Lglob = 0, ig = 0, jg = 0;
                for (int k = 1; k < T; k++)
                {
                    int len = Math.Min(T - 0, T - k);
                    int run = 0;
                    for (int s = 0; s < len; s++)
                    {
                        bool ok = Sfull[s, s + k] >= (minSimilarity - 0.1);
                        if (ok)
                        {
                            run++;
                            if (run > Lglob)
                            {
                                Lglob = run; ig = s - run + 1; jg = s - run + 1 + k;
                            }
                        }
                        else run = 0;
                    }
                }
                if (Lglob < minSegFrames)
                    throw new Exception("No sufficiently long repeated segment found. Try lowering minSimilarity or minSegSec.");

                Lbest = Lglob; iBest = ig; jBest = jg; usedSim = minSimilarity - 0.1;
            }

            int iStart = iBest;
            int jStart = (strategy == "cross") ? h + jBest : jBest;
            double startEarlySec = (double)iStart * hopSize / sr;
            double startLateSec = (double)jStart * hopSize / sr;
            double segDurSec = (double)Lbest * hopSize / sr;

            double rewindToSec = startEarlySec;
            double rewindFromSec = Math.Min(startLateSec + segDurSec, duration - tailGuardSec);

            var points = new LoopPoints(
                RewindFromSec: rewindFromSec,
                RewindToSec: rewindToSec,
                SegmentDurationSec: segDurSec,
                StartEarlySec: startEarlySec,
                StartLateSec: startLateSec,
                FramesPerSecond: fps
            );

            var debug = new DebugInfo
            {
                Strategy = strategy,
                MinSimilarityUsed = usedSim,
                Frames = T,
                MinSegFrames = minSegFrames,
                MatchFrames = Lbest,
                EarlyStartFrame = iStart,
                LateStartFrame = jStart,
                DurationSec = duration,
                SampleRate = sr,
                HopSize = hopSize,
                FramesPerSecond = fps,
                TopDiagSpans = topSpans.Take(64).ToList(),
                WaveformMinMax = ComputeMinMax(samples, waveformBins)
            };

            return (points, debug);
        }

        private static (int Lbest, int iBest, int jBest) LongestRunOnDiagonals(bool[,] mask, out List<MatchSpan> spans)
        {
            int h = mask.GetLength(0);
            int w = mask.GetLength(1);
            int bestLen = 0, bestI = 0, bestJ = 0;
            spans = new List<MatchSpan>();

            for (int k = -h + 1; k < w; k++)
            {
                int iBase = k >= 0 ? 0 : -k;
                int jBase = k >= 0 ? k : 0;
                int len = Math.Min(h - iBase, w - jBase);
                int run = 0; int startIdx = 0;
                for (int s = 0; s < len; s++)
                {
                    if (mask[iBase + s, jBase + s])
                    {
                        if (run == 0) startIdx = s;
                        run++;
                        if (run > bestLen)
                        {
                            bestLen = run;
                            bestI = iBase + s - run + 1;
                            bestJ = jBase + s - run + 1;
                        }
                    }
                    else if (run > 0)
                    {
                        spans.Add(new MatchSpan(iBase + startIdx, jBase + startIdx, run));
                        run = 0;
                    }
                }
                if (run > 0) spans.Add(new MatchSpan(iBase + startIdx, jBase + startIdx, run));
            }

            spans.Sort((a, b) => b.Length.CompareTo(a.Length));
            return (bestLen, bestI, bestJ);
        }

        private static MinMax[] ComputeMinMax(float[] samples, int bins)
        {
            if (bins <= 0) bins = 1000;
            var mm = new MinMax[bins];
            int n = samples.Length;
            for (int i = 0; i < bins; i++)
            {
                int start = (int)((long)i * n / bins);
                int end = (int)((long)(i + 1) * n / bins);
                float mn = 1f, mx = -1f;
                for (int j = start; j < end && j < n; j++)
                {
                    float v = samples[j];
                    if (v < mn) mn = v; if (v > mx) mx = v;
                }
                if (end <= start) { mn = 0; mx = 0; }
                mm[i] = new MinMax { Min = mn, Max = mx };
            }
            return mm;
        }
    }
}