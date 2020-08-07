using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using RichHudFramework.UI.Rendering;
using ApiMemberAccessor = System.Func<object, int, object>;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework
{
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;
    using ControlMembers = MyTuple<
        ApiMemberAccessor, // GetOrSetMember
        object // ID
    >;

    namespace UI.Server
    {
        using ControlContainerMembers = MyTuple<
            ApiMemberAccessor, // GetOrSetMember,
            MyTuple<object, Func<int>>, // Member List
            object // ID
        >;

        /// <summary>
        /// Scrollable text page used by the terminal. Not implemented.
        /// </summary>
        public class TextPage : TerminalPageBase, ITextPage
        {
            public RichText Text { get { return textBox.Text; } set { textBox.Text = value; } }

            public ITextBuilder TextBuilder => textBox.TextBoard;

            private readonly ScrollableLabelBox textBox;

            public TextPage()
            {
                textBox = new ScrollableLabelBox();
                Element = textBox;
            }

            protected override object GetOrSetMember(object data, int memberEnum)
            {
                throw new Exception("Method not implemented.");
            }

            private class ScrollableLabelBox : LabelBox
            { 
                public ScrollableLabelBox(HudParentBase parent = null) : base(parent)
                {
                    Format = GlyphFormat.Blueish;
                    BuilderMode = TextBuilderModes.Wrapped;
                    Color = new Color(41, 54, 62, 230);
                    DimAlignment = DimAlignments.Both | DimAlignments.IgnorePadding;

                    var border = new BorderBox(this)
                    {
                        Color = new Color(53, 66, 75),
                        Thickness = 2f,
                        DimAlignment = DimAlignments.Both | DimAlignments.IgnorePadding,
                    };
                }
            }
        }
    }
}