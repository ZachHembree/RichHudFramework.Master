using System;
using System.Collections.Generic;
using VRageMath;

namespace RichHudFramework.UI
{
    using Rendering;

    public class Dropdown<T> : HudElementBase, IListBoxEntry
    {
        public event Action OnSelectionChanged { add { list.OnSelectionChanged += value; } remove { list.OnSelectionChanged -= value; } }
        public ReadOnlyCollection<ListBoxEntry<T>> List => list.List;

        public float LineHeight { get { return list.LineHeight; } set { list.LineHeight = value; } }

        /// <summary>
        /// Default format for member text;
        /// </summary>
        public GlyphFormat Format { get { return list.Format; } set { list.Format = value; } }

        /// <summary>
        /// Current selection. Null if empty.
        /// </summary>
        public ListBoxEntry<T> Selection => list.Selection;

        /// <summary>
        /// Indicates whether or not the element will appear in the list
        /// </summary>
        public bool Enabled { get { return list.Enabled; } set { list.Enabled = value; } }

        public IClickableElement MouseInput => display.MouseInput;

        protected readonly DropdownDisplay display;
        protected readonly ListBox<T> list;

        public Dropdown(IHudParent parent = null) : base(parent)
        {
            display = new DropdownDisplay(this)
            {
                Padding = new Vector2(10f, 0f),
                DimAlignment = DimAlignments.Both | DimAlignments.IgnorePadding,
            };

            list = new ListBox<T>(display)
            {
                TabColor = new Color(0, 0, 0, 0),
                MinimumVisCount = 4,
                Offset = new Vector2(0f, -1f),
                DimAlignment = DimAlignments.Width | DimAlignments.IgnorePadding,
                ParentAlignment = ParentAlignments.Bottom,
                Visible = false,
            };

            Size = new Vector2(331f, 43f);
            display.Text = "Empty";

            display.MouseInput.OnLeftClick += ToggleList;
            OnSelectionChanged += UpdateDisplay;
        }

        protected override void HandleInput()
        {
            if (SharedBinds.LeftButton.IsNewPressed && !(display.IsMousedOver || list.IsMousedOver))
            {
                CloseList();
            }
        }

        private void UpdateDisplay()
        {
            if (Selection != null)
            {
                display.Text = Selection.TextBoard.GetText();
                CloseList();
            }
        }

        private void ToggleList()
        {
            if (!list.Visible)
                OpenList();
            else
                CloseList();
        }

        private void OpenList()
        {
            GetFocus();
            list.Visible = true;
        }

        private void CloseList()
        {
            list.Visible = false;
        }

        /// <summary>
        /// Adds a new member to the list box with the given name and associated
        /// object.
        /// </summary>
        public ListBoxEntry<T> Add(string name, T assocMember) =>
            list.Add(name, assocMember);

        /// <summary>
        /// Adds a new member to the list box with the given name and associated
        /// object.
        /// </summary>
        public ListBoxEntry<T> Add(RichString name, T assocMember) =>
            list.Add(name, assocMember);

        /// <summary>
        /// Adds a new member to the list box with the given name and associated
        /// object.
        /// </summary>
        public ListBoxEntry<T> Add(RichText name, T assocMember) =>
            list.Add(name, assocMember);

        /// <summary>
        /// Removes the given member from the list box.
        /// </summary>
        public void Remove(ListBoxEntry<T> member) =>
            list.Remove(member);

        /// <summary>
        /// Clears the current contents of the list.
        /// </summary>
        public void Clear() =>
            list.Clear();

        /// <summary>
        /// Sets the selection to the member associated with the given object.
        /// </summary>
        public void SetSelection(T assocMember) =>
            list.SetSelection(assocMember);

        public void SetSelection(ListBoxEntry<T> member) =>
            list.SetSelection(member);

        public new object GetOrSetMember(object data, int memberEnum) =>
            list.GetOrSetMember(data, memberEnum);

        protected class DropdownDisplay : HudElementBase
        {
            private static readonly Material arrowMat = new Material("RichHudDownArrow", new Vector2(64f, 64f));

            public RichText Text { get { return name.Text; } set { name.Text = value; } }
            public GlyphFormat Format { get { return name.Format; } set { name.Format = value; } }
            public Color Color { get { return background.Color; } set { background.Color = value; } }
            public override bool IsMousedOver => mouseInput.IsMousedOver;
            public IClickableElement MouseInput => mouseInput;

            public readonly Label name;
            public readonly TexturedBox arrow, divider, background;
            private readonly ClickableElement mouseInput;
            private readonly HudChain<HudElementBase> layout;

            public DropdownDisplay(IHudParent parent = null) : base(parent)
            {
                name = new Label()
                {
                    AutoResize = false,   
                };

                arrow = new TexturedBox()
                {
                    Width = 38f,
                    Color = new Color(227, 230, 233),
                    MatAlignment = MaterialAlignment.FitVertical,
                    Material = arrowMat,
                };

                divider = new TexturedBox()
                {
                    Padding = new Vector2(0f, 17f),
                    Size = new Vector2(2f, 39f),
                    Color = new Color(104, 113, 120),
                    DimAlignment = DimAlignments.Height | DimAlignments.IgnorePadding,
                };

                background = new TexturedBox(this)
                {
                    DimAlignment = DimAlignments.Both,
                };

                layout = new HudChain<HudElementBase>(this)
                {
                    AlignVertical = false,
                    AutoResize = true,
                    DimAlignment = DimAlignments.Height | DimAlignments.IgnorePadding,
                    ChildContainer = { name, divider, arrow }
                };

                mouseInput = new ClickableElement(this) 
                { 
                    DimAlignment = DimAlignments.Both
                };

                Color = new Color(41, 54, 62);
                Format = GlyphFormat.White;
                Text = "NewDropdown";
            }

            protected override void Draw()
            {
                name.Width = (Width - Padding.X) - divider.Width - arrow.Width;
            }
        }
    }
}