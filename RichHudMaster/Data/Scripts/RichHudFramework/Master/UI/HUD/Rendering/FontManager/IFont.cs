using System;
using System.Collections.Generic;
using VRage;
using ApiMemberAccessor = System.Func<object, int, object>;

namespace RichHudFramework
{
    using FontMembers = MyTuple<
        string, // Name
        int, // Index
        float, // PtSize
        float, // BaseScale
        Func<int, bool>, // IsStyleDefined
        ApiMemberAccessor
    >;

    namespace UI
    {
        namespace Rendering.Server
        {
            /// <summary>
            /// Expanded font interface. Used publicly by the HUD API.
            /// </summary>
            public interface IFont : IFontMin
            {
                /// <summary>
                /// Gets the style for the given font; returns null if the style isn't defined.
                /// </summary>
                IFontStyle this[FontStyles type] { get; }

                /// <summary>
                /// Gets the style for the given font; returns null if the style isn't defined.
                /// </summary>
                IFontStyle this[int index] { get; }

                /// <summary>
                /// Style list
                /// </summary>
                IReadOnlyList<IFontStyle> AtlasStyles { get; }

                /// <summary>
                /// Retrieves data needed to interact with IFont types via the Framework API.
                /// </summary>
                FontMembers GetApiData();
            }
        }
    }
}