using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using RichHudFramework.UI.Rendering;
using ApiMemberAccessor = System.Func<object, int, object>;
using GlyphFormatMembers = VRage.MyTuple<VRageMath.Vector2I, int, VRageMath.Color, float>;

namespace RichHudFramework
{
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;
    using ControlMembers = MyTuple<
        ApiMemberAccessor, // GetOrSetMember
        object // ID
    >;

    namespace UI.Server
    {
        public abstract class TerminalPageBase : HudElementBase, ITerminalPage, IListBoxEntry
        {
            public RichText Name
            {
                get
                {
                    if (NameBuilder != null)
                        return NameBuilder.GetText();
                    else
                        return name;
                }

                set
                {
                    if (NameBuilder != null)
                        NameBuilder.SetText(value);
                    else
                        name = value;
                }
            }

            public ITextBoard NameBuilder { get; set; }
            public bool Enabled { get; set; }

            protected RichText name;

            public TerminalPageBase(IHudParent parent) : base(parent)
            {
                Name = new RichText("NewPage", GlyphFormat.White);
                Enabled = true;
                Visible = false;
            }

            protected override object GetOrSetMember(object data, int memberEnum)
            {
                switch ((TerminalPageAccessors)memberEnum)
                {
                    case TerminalPageAccessors.Name:
                        {
                            if (data == null)
                                return Name.GetApiData();
                            else
                                Name = new RichText(data as RichStringMembers[]);

                            break;
                        }
                    case TerminalPageAccessors.Enabled:
                        {
                            if (data == null)
                                return Enabled;
                            else
                                Enabled = (bool)data;

                            break;
                        }
                }

                return null;
            }

            public virtual new ControlMembers GetApiData()
            {
                return new ControlMembers()
                {
                    Item1 = GetOrSetMember,
                    Item2 = this
                };
            }
        }
    }
}