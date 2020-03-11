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
        public class ControlTile : HudElementBase, IListBoxEntry, IControlTile
        {
            /// <summary>
            /// Read only collection of <see cref="TerminalControlBase"/>s attached to the tile
            /// </summary>
            public IReadOnlyCollection<ITerminalControl> Controls { get; }

            public IControlTile ControlContainer => this;

            /// <summary>
            /// Determines whether or not the tile will be rendered in the list.
            /// </summary>
            public bool Enabled { get; set; }

            public override float Width { get { return controls.Width + Padding.X; } set { controls.Width = value - Padding.X; } }

            private readonly HudChain<TerminalControlBase> controls;
            private readonly TexturedBox background;

            public ControlTile(IHudParent parent = null) : base(parent)
            {
                background = new TexturedBox(this)
                {
                    Color = RichHudTerminal.TileColor,
                    DimAlignment = DimAlignments.Both,
                };

                var border = new BorderBox(this)
                {
                    DimAlignment = DimAlignments.Both,
                    Color = new Color(58, 68, 77),
                    Thickness = 1f,
                };

                controls = new HudChain<TerminalControlBase>(this)
                {
                    AutoResize = true,
                    AlignVertical = true,
                    Spacing = 20f,
                };

                Controls = new ReadOnlyCollectionData<ITerminalControl>(x => controls.ChainMembers[x], () => controls.ChainMembers.Count);

                Padding = new Vector2(16f);
                Size = new Vector2(300f, 250f);
                Enabled = true;
            }

            protected override void Draw()
            {
                background.Color = background.Color.SetAlphaPct(HudMain.UiBkOpacity);

                base.Draw();
            }

            /// <summary>
            /// Adds a <see cref="TerminalControlBase"/> to the tile
            /// </summary>
            public void Add(TerminalControlBase newControl)
            {
                controls.Add(newControl);
            }

            IEnumerator<ITerminalControl> IEnumerable<ITerminalControl>.GetEnumerator() =>
                Controls.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() =>
                Controls.GetEnumerator();

            /// <summary>
            /// Retrieves information needed by the Framework API 
            /// </summary>
            public new ControlContainerMembers GetApiData()
            {
                return new ControlContainerMembers()
                {
                    Item1 = GetOrSetMember,
                    Item2 = new MyTuple<object, Func<int>>
                    {
                        Item1 = (Func<int, ControlMembers>)(x => controls.ChainMembers[x].GetApiData()),
                        Item2 = () => controls.ChainMembers.Count
                    },
                    Item3 = this
                };
            }

            private new object GetOrSetMember(object data, int memberEnum)
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
        }
    }
}