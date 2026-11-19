using System;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace CyanPreview
{
    // Opt-in timing only; normal builds do not collect or log frame timings.
    public static class DesktopFrameProfile
    {
        public enum Stage { Windows, Motion, Effects, Draw, Mesh, Bounds, Buffers, Camera, Readback, Pack, Present, Frame }
        public static readonly bool Enabled = Environment.GetEnvironmentVariable("CYAN_DESKTOP_PROFILE") == "1";
        static readonly long[] ticks = new long[12];
        static readonly int[] calls = new int[12];
        static long started = Stopwatch.GetTimestamp(), pixelCount;
        static readonly double[] intervals=new double[1024];
        static int intervalCount;
        static long lastFrame;
        static int frames, resizes, lastGc = GC.CollectionCount(0);
        public struct Scope : IDisposable
        {
            readonly Stage stage;
            readonly long start;
            public Scope(Stage stage) { this.stage = stage; start = Enabled ? Stopwatch.GetTimestamp() : 0; }
            public void Dispose() { if (Enabled) { ticks[(int)stage] += Stopwatch.GetTimestamp() - start; calls[(int)stage]++; } }
        }
        public static Scope Measure(Stage stage) => new Scope(stage);
        public static void Resize() { if (Enabled) resizes++; }
        public static void Pixels(int count) { if (Enabled) pixelCount += count; }
        public static void EndFrame()
        {
            if (!Enabled) return;
            long now=Stopwatch.GetTimestamp();
            if(lastFrame!=0 && intervalCount<intervals.Length) intervals[intervalCount++]=(now-lastFrame)*1000.0/Stopwatch.Frequency;
            lastFrame=now;
            frames++;
            double seconds = (Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency;
            if (seconds < 5) return;
            var text = new StringBuilder("DESKTOP_PROFILE frames=" + frames + " fps=" + (frames / seconds).ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
            for (int i = 0; i < ticks.Length; i++)
            {
                double ms = calls[i] == 0 ? 0 : ticks[i] * 1000.0 / Stopwatch.Frequency / calls[i];
                text.Append(" ").Append((Stage)i).Append("Ms=").Append(ms.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                ticks[i] = 0; calls[i] = 0;
            }
            if(intervalCount>0)
            {
                Array.Sort(intervals,0,intervalCount);
                text.Append(" intervalP95Ms=").Append(intervals[(int)((intervalCount-1)*.95)].ToString("F2",System.Globalization.CultureInfo.InvariantCulture));
                text.Append(" intervalMaxMs=").Append(intervals[intervalCount-1].ToString("F2",System.Globalization.CultureInfo.InvariantCulture));
                intervalCount=0;
            }
            text.Append(" managedMB=").Append((GC.GetTotalMemory(false)/1048576.0).ToString("F1",System.Globalization.CultureInfo.InvariantCulture));
            text.Append(" resizes=").Append(resizes).Append(" avgPixels=").Append(pixelCount / Math.Max(1, frames)).Append(" gc0=").Append(GC.CollectionCount(0) - lastGc);
            UnityEngine.Debug.Log(text.ToString());
            frames = 0; resizes = 0; pixelCount = 0; lastGc = GC.CollectionCount(0); started = Stopwatch.GetTimestamp();
        }
    }
}
