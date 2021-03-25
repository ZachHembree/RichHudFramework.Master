using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using ApiMemberAccessor = System.Func<object, int, object>;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework
{
    namespace UI
    {
        using Server;
        using Client;

        using ControlContainerMembers = MyTuple<
            ApiMemberAccessor, // GetOrSetMember,
            MyTuple<object, Func<int>>, // Member List
            object // ID
        >;

        public enum TerminalAccessors : int
        {
            ToggleMenu = 0,
            OpenMenu = 1,
            CloseMenu = 2,
            OpenToPage = 3,
            SetPage = 4,
            GetMenuOpen = 5
        }

        /// <summary>
        /// Used by the API to specify to request a given type of settings menu control
        /// </summary>
        public enum MenuControls : int
        {
            Checkbox = 1,
            ColorPicker = 2,
            OnOffButton = 3,
            SliderSetting = 4,
            TerminalButton = 5,
            TextField = 6,
            DropdownControl = 7,
            ListControl = 8,
            DragBox = 9,
        }

        public enum ControlContainers : int
        {
            Tile = 1,
            Category = 2,
        }

        public enum ModPages : int
        {
            ControlPage = 1,
            RebindPage = 2,
            TextPage = 3,
        }

        public enum TerminalPageCategoryAccessors : int
        {
            /// <summary>
            /// string
            /// </summary>
            Name = 2,

            /// <summary>
            /// bool
            /// </summary>
            Enabled = 3,

            /// <summary>
            /// out: ControlMembers
            /// </summary>
            Selection = 4,

            /// <summary>
            /// in: TerminalPageBase
            /// </summary>
            AddPage = 5,

            /// <summary>
            /// in: IReadOnlyList<TerminalPageBase>
            /// </summary>
            AddPageRange = 6,
        }

        public enum ModControlRootAccessors : int
        {
            /// <summary>
            /// Action
            /// </summary>
            GetOrSetCallback = 1,
        }

        public interface IModRootMember
        {
            /// <summary>
            /// Name of the member as it appears in the terminal
            /// </summary>
            string Name { get; set; }

            /// <summary>
            /// Determines whether or not the element will appear in the list.
            /// Disabled by default.
            /// </summary>
            bool Enabled { get; set; }
        }

        public interface ITerminalPageCategory : IEnumerable<ITerminalPage>, IModRootMember
        {
            /// <summary>
            /// Read only collection of <see cref="TerminalPageBase"/>s assigned to this object.
            /// </summary>
            IReadOnlyList<TerminalPageBase> Pages { get; }

            /// <summary>
            /// Used to allow the addition of category elements using collection-initializer syntax in
            /// conjunction with normal initializers.
            /// </summary>
            ITerminalPageCategory PageContainer { get; }

            /// <summary>
            /// Currently selected <see cref="TerminalPageBase"/>.
            /// </summary>
            TerminalPageBase SelectedPage { get; }

            /// <summary>
            /// Adds a terminal page to the category
            /// </summary>
            void Add(TerminalPageBase page);

            /// <summary>
            /// Adds a range of terminal pages to the category
            /// </summary>
            void AddRange(IReadOnlyList<TerminalPageBase> pages);
        }

        /// <summary>
        /// Indented dropdown list of terminal pages. Root UI element for all terminal controls
        /// associated with a given mod.
        /// </summary>
        public interface IModControlRoot : ITerminalPageCategory
        {
            /// <summary>
            /// Invoked when a new page is selected
            /// </summary>
            event EventHandler SelectionChanged;

            /// <summary>
            /// Page subcategories attached to the mod root
            /// </summary>
            IReadOnlyList<TerminalPageCategory> Subcategories { get; }

            /// <summary>
            /// Adds a page subcategory to the control root
            /// </summary>
            void Add(TerminalPageCategory subcategory);

            /// <summary>
            /// Adds a range of root members to the control root, either subcategories or pages.
            /// </summary>
            void AddRange(IReadOnlyList<IModRootMember> members);

            /// <summary>
            /// Retrieves data used by the Framework API
            /// </summary>
            ControlContainerMembers GetApiData();
        }
    }
}