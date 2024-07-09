using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework
{
    using RichStringMembers = MyTuple<StringBuilder, GlyphFormatMembers>;
    using ControlMembers = MyTuple<
        ApiMemberAccessor, // GetOrSetMember
        object // ID
    >;
    using ControlContainerMembers = MyTuple<
        ApiMemberAccessor, // GetOrSetMember,
        MyTuple<object, Func<int>>, // Member List
        object // ID
    >;

    namespace UI.Server
    {
        /// <summary>
        /// Small collection of terminal controls organized into a single block. No more than 1-3
        /// controls should be added to a tile. If a group of controls can't fit on a tile, then they
        /// will be drawn outside its bounds.
        /// </summary>
        public class ControlTile : ScrollBoxEntry, IControlTile
        {
            /// <summary>
            /// Read only collection of <see cref="TerminalControlBase"/>s attached to the tile
            /// </summary>
            public IReadOnlyList<TerminalControlBase> Controls => tileElement.Controls;

            /// <summary>
            /// Used to allow the addition of controls to tiles using collection-initializer syntax in
            /// conjunction with normal initializers.
            /// </summary>
            public IControlTile ControlContainer => this;

            /// <summary>
            /// Unique identifier
            /// </summary>
            public object ID => this;

            private readonly TileElement tileElement;

            public ControlTile()
            {
                tileElement = new TileElement();
                SetElement(tileElement);
            }

            /// <summary>
            /// Adds an <see cref="TerminalControlBase"/> entry to the tile
            /// </summary>
            public void Add(TerminalControlBase newControl)
            {
                tileElement.Add(newControl);
            }

            IEnumerator<ITerminalControl> IEnumerable<ITerminalControl>.GetEnumerator() =>
                tileElement.Controls.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() =>
                tileElement.Controls.GetEnumerator();

            /// <summary>
            /// Retrieves information needed by the Framework API 
            /// </summary>
            public ControlContainerMembers GetApiData()
            {
                return new ControlContainerMembers()
                {
                    Item1 = GetOrSetMember,
                    Item2 = new MyTuple<object, Func<int>>
                    {
                        Item1 = (Func<int, ControlMembers>)(x => tileElement.Controls[x].GetApiData()),
                        Item2 = () => tileElement.Controls.Count
                    },
                    Item3 = this
                };
            }

            private object GetOrSetMember(object data, int memberEnum)
            {
                var member = (ControlTileAccessors)memberEnum;

                switch (member)
                {
                    case ControlTileAccessors.AddControl:
                        {
                            Add(data as TerminalControlBase);
                            break;
                        }
                    case ControlTileAccessors.Enabled:
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

            private class TileElement : HudElementBase
            {
                public IReadOnlyList<TerminalControlBase> Controls => controls.Collection;

                private readonly HudChain<TerminalControlBase> controls;
                private readonly TexturedBox background;

                public TileElement(HudParentBase parent = null) : base(parent)
                {
                    background = new TexturedBox(this)
                    {
                        DimAlignment = DimAlignments.Size,
                        Color = TerminalFormatting.Gunmetal,
                    };

                    var border = new BorderBox(this)
                    {
                        DimAlignment = DimAlignments.Size,
                        Color = new Color(58, 68, 77),
                        Thickness = 1f,
                    };

                    controls = new HudChain<TerminalControlBase>(true, this)
                    {
                        DimAlignment = DimAlignments.UnpaddedSize,
                        SizingMode = HudChainSizingModes.ClampMembersOffAxis | HudChainSizingModes.AlignMembersCenter,
                        Spacing = 12f,
                    };
                    
                    Padding = new Vector2(54f);
                    Size = new Vector2(300f, 250f);
                    IsMasking = true;
                }

                /// <summary>
                /// Adds an <see cref="TerminalControlBase"/> entry to the tile
                /// </summary>
                public void Add(TerminalControlBase newControl)
                {
                    controls.Add(newControl);
                }

                protected override void Layout()
                {
                    background.Color = background.Color.SetAlphaPct(HudMain.UiBkOpacity);
                }

                protected override void HandleInput(Vector2 cursorPos)
                {
                    for (int n = 0; n < controls.Collection.Count; n++)
                        controls.Collection[n].Update();
                }
            }
        }
    }
}