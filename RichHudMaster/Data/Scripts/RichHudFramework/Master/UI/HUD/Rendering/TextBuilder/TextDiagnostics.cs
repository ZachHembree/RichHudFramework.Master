using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using RichHudFramework.UI.Server;

namespace RichHudFramework.UI.Server
{
    /// <summary>
    /// Diagnostic counters and settings for the RHM text rendering
    /// </summary>
    public class TextDiagnostics
    {
        public static TextDiagnostics Instance { get; private set; }

        /// <summary>
        /// Enables or disables line text caching, which allows the state of a cleared line 
        /// to be retained for reuse if its contents are unchanged when next set.
        /// </summary>
        public bool LineTextCacheEnabled = true;
        public ulong LineTextCacheHits = 0;
        public ulong LineTextCacheMisses = 0;

        /// <summary>
        /// Enables or disables intermediate glyph quad caching. Requires text caching.
        /// </summary>
        public bool GlyphCacheEnabled = true;
        public ulong GlyphCacheHits = 0;
        public ulong GlyphCacheMisses = 0;

        /// <summary>
        /// Enables or disables glyph placement caching. Required by billboard cache. Requires 
        /// glyph cache.
        /// </summary>
        public bool TypesettingCacheEnabled = true;
        public ulong TypesettingHits = 0;
        public ulong TypesettingMisses = 0;

        /// <summary>
        /// Enables or disables caching of finalized text billboard data. Requires typesetting, 
        /// glyph and text caching.
        /// </summary>
        public bool BBCacheEnabled = true;
        public ulong BBCacheHits = 0;
        public ulong BBCacheMisses = 0;

        public static float GetTextHitPercent()
        {
            ulong total = Instance.LineTextCacheHits + Instance.LineTextCacheMisses;
            return (float)(100.0 * (Instance.LineTextCacheHits / (double)total));
        }

        public static float GetGlyphHitPercent()
        {
            ulong total = Instance.GlyphCacheHits + Instance.GlyphCacheMisses;
            return (float)(100.0 * (Instance.GlyphCacheHits / (double)total));
        }

        public static float GetTypesettingHitPercent()
        {
            ulong total = Instance.TypesettingHits + Instance.TypesettingMisses;
            return (float)(100.0 * (Instance.TypesettingHits / (double)total));
        }

        public static float GetBBHitPercent()
        {
            ulong total = Instance.BBCacheHits + Instance.BBCacheMisses;
            return (float)(100.0 * (Instance.BBCacheHits / (double)total));
        }

        static TextDiagnostics()
        {
            Instance = new TextDiagnostics();
        }

        public static void Reset()
        {
            Instance = new TextDiagnostics();
        }
    }
}