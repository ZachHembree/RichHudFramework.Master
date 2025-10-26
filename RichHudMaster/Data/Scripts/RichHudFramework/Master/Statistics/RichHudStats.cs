using RichHudFramework.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using VRage;
using VRageMath;

namespace RichHudFramework.Server
{
	public partial class RichHudStats : RichHudComponentBase
	{
		public const long UpdateIntervalMS = 50;
		
		private static RichHudStats instance;

		private RichHudStats() : base(false, true)
		{
			if (instance == null)
				instance = this;
			else
				throw new Exception($"Only one instance of {nameof(RichHudStats)} can exist at once.");

			UI.Init();
			Text.Init();
		}

		public static void Init()
		{
			if (instance == null)
				new RichHudStats();
		}

		public override void Draw()
		{
			if (RichHudDebug.EnableDebug)
			{
				UI.Update();
				Text.Update();
			}
		}

		public override void Close()
		{
			instance = null;
			UI.Close();
			Text.Close();
		}

		public class CacheStats
		{
			public bool Enabled = true;
			public ulong Hits = 0;
			public ulong Misses = 0;

			public float GetHitPct()
			{
				return (float)(100.0 * (Hits / (double)(Hits + Misses)));
			}

			public float GetMissPct()
			{
				return (float)(100.0 * (Misses / (double)(Hits + Misses)));
			}

			public void Reset()
			{
				ulong total = Hits + Misses;

				if (total < 1000)
					return;

				Hits = 1000 * Hits / total;
				Misses = 1000 * Misses / total;
			}
		}

		public class CounterStats
		{
			public double AvgCount { get; private set; }

			public long Pct1st { get; private set; }

			public long Pct50th { get; private set; }

			public long Pct99th { get; private set; }

			public long TareCount { get; private set; }

			private readonly long[] tickTimes;
			private readonly int windowSize;
			private readonly int outlierTrim;
			private int tick;

			public CounterStats(int windowSize = 120, int outlierTrim = 10)
			{
				this.windowSize = windowSize;
				this.outlierTrim = outlierTrim;
				tickTimes = new long[windowSize];
			}

			public void AddCount(long count)
			{
				tickTimes[tick] = count;
				tick++;
				tick %= windowSize;
			}

			/// <summary>
			/// Sets tare count equal to average count. Automatically corrects for excessive 
			/// tare over time.
			/// </summary>
			public void Tare() { TareCount = (long)AvgCount; }

			/// <summary>
			/// Clears tare
			/// </summary>
			public void ClearTare() { TareCount = 0; }

			public void Update(List<long> sortBuffer)
			{
				sortBuffer.Clear();
				sortBuffer.AddRange(tickTimes);
				sortBuffer.Sort();

				long totalTicks = 0;

				for (int n = outlierTrim; n < sortBuffer.Count - outlierTrim; n++)
					totalTicks += sortBuffer[n];

				// Autocorrect excessive tare
				TareCount = Math.Min(TareCount, Math.Min(Pct50th, (long)AvgCount));

				// Compute average and percentiles
				AvgCount = (totalTicks / (double)sortBuffer.Count);
				Pct1st = sortBuffer[(int)(sortBuffer.Count * 0.01d)];
				Pct50th = sortBuffer[(int)(sortBuffer.Count * 0.5d)];
				Pct99th = sortBuffer[(int)(sortBuffer.Count * 0.99d)];
			}
		}

		public class TickStats
		{
			public double AvgTime { get; private set; }

			public double Pct1st { get; private set; }

			public double Pct50th { get; private set; }

			public double Pct99th { get; private set; }

			public double TareTime { get; private set; }

			private readonly Stopwatch timer;
			private readonly long[] tickTimes;
			private readonly int windowSize;
			private readonly int outlierTrim;
			private int tick;

			public TickStats(int windowSize = 240, int outlierTrim = 10)
			{
				this.windowSize = windowSize;
				this.outlierTrim = outlierTrim;
				tickTimes = new long[windowSize];
				timer = new Stopwatch();
			}

			public void BeginTick()
			{
				timer.Restart();		
			}

			public void ResumeTick()
			{
				timer.Start();
			}

			public void PauseTick()
			{
				timer.Stop();
			}

			public void EndTick()
			{
				timer.Stop();
				tickTimes[tick] = timer.ElapsedTicks;

				tick++;
				tick %= windowSize;
			}

			/// <summary>
			/// Sets tare time equal to average time. Automatically self corrects over time
			/// based on average and 50th pct.
			/// </summary>
			public void Tare() { TareTime = AvgTime; }

			/// <summary>
			/// Clears tare
			/// </summary>
			public void ClearTare() { TareTime = 0d; }

			public void Update(List<long> sortBuffer)
			{
				const double rcpTPMS = 1d / TimeSpan.TicksPerMillisecond;

				sortBuffer.Clear();
				sortBuffer.AddRange(tickTimes);
				sortBuffer.Sort();

				long totalTicks = 0;

				for (int n = outlierTrim; n < sortBuffer.Count - outlierTrim; n++)
					totalTicks += sortBuffer[n];

				// Autocorrect excessive tare over time
				TareTime = Math.Min(TareTime, Math.Min(Pct50th, AvgTime));

				// Compute average and percentiles
				AvgTime = (totalTicks / (double)sortBuffer.Count) * rcpTPMS;
				Pct1st = sortBuffer[(int)(sortBuffer.Count * 0.01d)] * rcpTPMS;
				Pct50th = sortBuffer[(int)(sortBuffer.Count * 0.5d)] * rcpTPMS;
				Pct99th = sortBuffer[(int)(sortBuffer.Count * 0.99d)] * rcpTPMS;
			}
		}
	}
}