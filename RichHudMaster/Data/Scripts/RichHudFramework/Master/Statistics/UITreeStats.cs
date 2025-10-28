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
	public partial class RichHudStats
	{
		public class UI
		{
			private const int CategoryCount = 4;

			private const int TimeWindowSize = 240;
			private const int TimeOutlierTrim = 0;
			private const int CounterWindowSize = 120;
			private const int CounterOutlierTrim = 0;

			// Usage counters

			/// <summary>
			/// Total number of node updates performed
			/// </summary>
			public static double AvgNodeUpdateCount { get; private set; }

			/// <summary>
			/// Total number of node updates performed
			/// </summary>
			public static double NodeUpdate50th { get; private set; }

			/// <summary>
			/// Total number of node updates performed
			/// </summary>
			public static double NodeUpdate99th { get; private set; }

			/// <summary>
			/// Number of unique HUD spaces registered
			/// </summary>
			public static int HudSpacesRegistered { get; private set; }

			/// <summary>
			/// Number of UI elements registered from all clients
			/// </summary>
			public static int ElementsRegistered { get; private set; }

			// Update times
			public static double DrawAvg { get; private set; }

			public static double Draw50th { get; private set; }

			public static double Draw99th { get; private set; }

			public static double InputAvg { get; private set; }

			public static double Input50th { get; private set; }

			public static double Input99th { get; private set; }

			public static double TotalAvg { get; private set; }

			public static double Total50th { get; private set; }

			public static double Total99th { get; private set; }

			public static double TreeAvg { get; private set; }

			public static double Tree50th { get; private set; }

			public static double Tree99th { get; private set; }

			public static TickStats Draw { get; private set; }

			public static TickStats Input { get; private set; }

			public static TickStats Tree { get; private set; }

			/// <summary>
			/// Internal update counters
			/// </summary>
			public static class InternalCounters
			{
				public static int HudSpacesRegistered = 0;

				public static int ElementsRegistered = 0;

				public static long SizingUpdates = 0;

				public static long LayoutUpdates = 0;

				public static long DrawUpdates = 0;

				public static long InputDepthUpdates = 0;

				public static long InputUpdates = 0;

				public static long GetTotal()
				{
					return SizingUpdates + LayoutUpdates + DrawUpdates + InputDepthUpdates + InputUpdates;
				}

				public static void Reset()
				{
					HudSpacesRegistered = 0;
					ElementsRegistered = 0;

					SizingUpdates = 0;
					LayoutUpdates = 0;
					DrawUpdates = 0;
					InputDepthUpdates = 0;
					InputUpdates = 0;
				}
			}

			private static UI instance;

			private readonly CounterStats nodeStats;
			private readonly List<long> sortBuffer;
			private static int updateTick;

			private readonly Stopwatch timer;

			private UI()
			{
				if (instance == null)
					instance = this;
				else
					throw new Exception($"Only one instance of {nameof(UI)} can exist at once.");

				Tree = new TickStats(TimeWindowSize, TimeOutlierTrim);
				Draw = new TickStats(TimeWindowSize, TimeOutlierTrim);
				Input = new TickStats(TimeWindowSize, TimeOutlierTrim);

				timer = new Stopwatch();
				timer.Start();

				nodeStats = new CounterStats(CounterWindowSize, CounterOutlierTrim);
				sortBuffer = new List<long>();
				updateTick = 0;

				InternalCounters.Reset();
			}

			public static void Init()
			{
				if (instance == null)
					new UI();
			}

			public static void Close()
			{
				instance = null;
				Tree = null;
				Draw = null;
				Input = null;
			}

			public static void Tare()
			{
				Tree.Tare();
				Draw.Tare();
				Input.Tare();
			}

			public static void ClearTare()
			{
				Tree.ClearTare();
				Draw.ClearTare();
				Input.ClearTare();
			}

			public static void Update()
			{
				HudSpacesRegistered = InternalCounters.HudSpacesRegistered;
				ElementsRegistered = InternalCounters.ElementsRegistered;
				instance.nodeStats.AddCount(InternalCounters.GetTotal());

				if (instance.timer.ElapsedMilliseconds > UpdateIntervalMS)
				{
					if (updateTick == 0)
					{
						Tree.Update(instance.sortBuffer);
						TreeAvg = Tree.AvgTime - Tree.TareTime;
						Tree50th = Tree.Pct50th - Tree.TareTime;
						Tree99th = Tree.Pct99th - Tree.TareTime;
					}
					else if (updateTick == 1)
					{
						Draw.Update(instance.sortBuffer);
						DrawAvg = Draw.AvgTime - Draw.TareTime;
						Draw50th = Draw.Pct50th - Draw.TareTime;
						Draw99th = Draw.Pct99th - Draw.TareTime;
					}
					else if (updateTick == 2)
					{
						Input.Update(instance.sortBuffer);
						InputAvg = Input.AvgTime - Input.TareTime;
						Input50th = Input.Pct50th - Input.TareTime;
						Input99th = Input.Pct99th - Input.TareTime;
					}
					else if (updateTick == 3)
					{
						instance.nodeStats.Update(instance.sortBuffer);
						AvgNodeUpdateCount = instance.nodeStats.AvgCount;
						NodeUpdate50th = instance.nodeStats.Pct50th;
						NodeUpdate99th = instance.nodeStats.Pct99th;
					}

					TotalAvg = DrawAvg + InputAvg;
					Total50th = Draw50th + Input50th;
					Total99th = Draw99th + Input99th;

					updateTick++;
					updateTick %= CategoryCount;

					instance.timer.Restart();		
				}

				InternalCounters.Reset();
			}
		}
	}
}