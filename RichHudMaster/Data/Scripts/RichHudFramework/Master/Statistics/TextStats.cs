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
		public class Text
		{
			public const int ResetIntervalTicks = 600;

			/// <summary>
			/// Caching stats for line text caching, which allows the state of a cleared line 
			/// to be retained for reuse if its contents are unchanged when next set.
			/// </summary>
			public static CacheStats LineTextCache { get; private set; }

			/// <summary>
			/// Caching stats for intermediate glyph quad caching. Requires text caching.
			/// </summary>
			public static CacheStats GlyphCache { get; private set; }

			/// <summary>
			/// Caching stats for glyph placement caching. Required by billboard cache. Requires 
			/// glyph cache.
			/// </summary>
			public static CacheStats TypesettingCache { get; private set; }

			/// <summary>
			/// Caching stats for finalized text billboard data. Requires typesetting, 
			/// glyph and text caching.
			/// </summary>
			public static CacheStats BBCache { get; private set; }

			private static Text instance;
			private static int tick;

			private Text()
			{
				if (instance == null)
					instance = this;
				else
					throw new Exception($"Only one instance of {nameof(Text)} can exist at once.");

				LineTextCache = new CacheStats();
				GlyphCache = new CacheStats();
				TypesettingCache = new CacheStats();
				BBCache = new CacheStats();

				tick = 0;
			}

			public static void Init()
			{
				if (instance == null)
					new Text();
			}

			public static void Close()
			{
				instance = null;
				LineTextCache = null;
				GlyphCache = null;
				TypesettingCache = null;
				BBCache = null;
			}

			public static void Update()
			{
				if (tick >= ResetIntervalTicks)
				{
					LineTextCache.Reset();
					GlyphCache.Reset();
					TypesettingCache.Reset();
					BBCache.Reset();
					tick = 0;
				}

				tick++;
			}			
		}
	}
}