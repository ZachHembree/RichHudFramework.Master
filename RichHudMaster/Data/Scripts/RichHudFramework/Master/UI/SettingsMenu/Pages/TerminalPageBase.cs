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
        public abstract class TerminalPageBase : SelectionBoxEntryTuple<LabelElementBase, object>, ITerminalPage
        {
            /// <summary>
            /// Name of the <see cref="ITerminalPage"/> as it appears in the dropdown of the <see cref="IModControlRoot"/>.
            /// </summary>
            public string Name
            {
                get { return Element.TextBoard.ToString(); }
                set { Element.TextBoard.SetText(value); }
            }

            public object ID => this;

            protected string name;

            public TerminalPageBase(HudElementBase assocMember)
            {
                SetElement(new Label());
                AssocMember = assocMember;

                Name = "NewPage";
                Enabled = true;
            }

            public virtual void Reset()
            {
                Element.TextBoard.Clear();
                Enabled = false;
            }

            protected virtual object GetOrSetMember(object data, int memberEnum)
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
            public virtual ControlMembers GetApiData()
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