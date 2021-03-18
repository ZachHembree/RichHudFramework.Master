using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using ApiMemberAccessor = System.Func<object, int, object>;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework.UI.Server
{
    using ControlMembers = MyTuple<
        ApiMemberAccessor, // GetOrSetMember
        object // ID
    >;
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;

    /// <summary>
    /// Label for use within control tiles and vertical control categories
    /// </summary>
    public class TerminalLabel : TerminalControlBase
    {
        /// <summary>
        /// The name of the label as it appears in the terminal.
        /// </summary>
        public override string Name { get { return label.TextBoard.ToString(); } set { label.TextBoard.SetText(value); } }

        private readonly Label label;

        public TerminalLabel()
        {
            label = new Label() { Format = TerminalFormatting.ControlFormat.WithAlignment(TextAlignment.Center) };
            SetElement(label);
        }
    }
}