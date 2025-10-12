namespace RichHudFramework.UI.Server
{
    /// <summary>
    /// Diagnostic counters and settings for the RHM text rendering
    /// </summary>
    public static class TextDiagnostics
    {
        public const int ResetIntervalTicks = 600;

        /// <summary>
        /// Enables or disables line text caching, which allows the state of a cleared line 
        /// to be retained for reuse if its contents are unchanged when next set.
        /// </summary>
        public static CacheStats LineTextCache = new CacheStats();

        /// <summary>
        /// Enables or disables intermediate glyph quad caching. Requires text caching.
        /// </summary>
        public static CacheStats GlyphCache = new CacheStats();

        /// <summary>
        /// Enables or disables glyph placement caching. Required by billboard cache. Requires 
        /// glyph cache.
        /// </summary>
        public static CacheStats TypesettingCache = new CacheStats();

        /// <summary>
        /// Enables or disables caching of finalized text billboard data. Requires typesetting, 
        /// glyph and text caching.
        /// </summary>
        public static CacheStats BBCache = new CacheStats();

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

        private static int tick = 0;

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