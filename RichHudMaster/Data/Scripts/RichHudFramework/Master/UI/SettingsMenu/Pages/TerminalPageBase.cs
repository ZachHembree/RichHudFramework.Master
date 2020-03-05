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
        /// <summary>
        /// Base class for named pages attached to a mod control root.
        /// </summary>
        public abstract class TerminalPageBase : HudElementBase, ITerminalPage, IListBoxEntry
        {
            /// <summary>
            /// Name of the <see cref="ITerminalPage"/> as it appears in the dropdown of the <see cref="IModControlRoot"/>.
            /// </summary>
            public string Name
            {
                get
                {
                    if (NameBuilder != null)
                        return NameBuilder.ToString();
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

            /// <summary>
            /// Determines whether or not the <see cref="ITerminalPage"/> will be visible in the mod root.
            /// </summary>
            public bool Enabled { get; set; }

            protected string name;

            public TerminalPageBase(IHudParent parent) : base(parent)
            {
                Name = "NewPage";
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
                                return Name;
                            else
                                Name = data as string;

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

            /// <summary>
            /// Retrieves information used by the Framework API
            /// </summary>
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