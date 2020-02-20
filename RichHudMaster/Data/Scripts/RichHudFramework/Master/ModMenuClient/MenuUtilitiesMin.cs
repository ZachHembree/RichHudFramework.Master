using RichHudFramework.Game;
using RichHudFramework.UI.TextHudApi;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRageMath;
using MenuFlag = RichHudFramework.UI.TextHudApi.HudApiMin.MenuRootCategory.MenuFlag;

namespace RichHudFramework.UI
{
    /// <summary>
    /// Collection of wrapper types and utilities used to simplify the creation of settings menu elements in the Text HUD API Mod Menu
    /// </summary>
    public sealed class MenuUtilities : RichHudComponentBase
    {
        public static bool CanAddElements => !wasClosed;

        private static MenuUtilities Instance
        {
            get { Init(); return _instance; }
            set { _instance = value; }
        }

        private static MenuUtilities _instance = null;
        private static bool wasClosed = false;
        private static readonly List<Action> menuUpdateActions;

        static MenuUtilities()
        {
            menuUpdateActions = new List<Action>();
        }

        public MenuUtilities() : base(false, true)
        { }

        private static void Init()
        {
            if (_instance == null)
            {
                _instance = new MenuUtilities();

                if (!wasClosed)
                    MenuRoot.Init(_instance.Parent.ModName, $"{_instance.Parent.ModName} Settings");
            }
        }

        public override void Close()
        {
            Instance = null;
            wasClosed = true;
        }

        public override void Update()
        {
            if (HudApiMin.Heartbeat && MyAPIGateway.Gui.ChatEntryVisible)
            {
                for (int n = 0; n < menuUpdateActions.Count; n++)
                    menuUpdateActions[n]();
            }
        }

        public static void AddMenuElement(IMenuElement newChild)
        {
            if (!wasClosed)
                MenuRoot.Instance.AddChild(newChild);
        }

        public static void AddMenuElements(IList<IMenuElement> newChildren)
        {
            if (!wasClosed)
                MenuRoot.Instance.AddChildren(newChildren);
        }

        /// <summary>
        /// Interface of all menu elements capable of serving as parents of other elements.
        /// </summary>
        public interface IMenuCategory
        {
            string Header { get; }
            HudApiMin.MenuCategoryBase CategoryBase { get; }
            bool IsRoot { get; }
        }

        /// <summary>
        /// Interface for all menu elements based on HudAPIv2.MenuItemBase
        /// </summary>
        public interface IMenuElement
        {
            string Name { get; }
            IMenuCategory Parent { get; set; }
            void InitElement();
        }

        /// <summary>
        /// Base class for all wrapper types that instantiate HudAPIv2.MenuItemBase mod menu elements
        /// </summary>
        public abstract class MenuElement<T> : IMenuElement where T : HudApiMin.MenuItemBase
        {
            public virtual IMenuCategory Parent
            {
                get { return parent; }
                set
                {
                    parent = value;

                    if (Element != null && parent != null && parent.CategoryBase != null)
                        Element.Parent = parent.CategoryBase;
                }
            }
            private IMenuCategory parent;

            public virtual string Name
            {
                get { return GetName(); }
                set
                {
                    GetName = () => value;

                    if (Element != null)
                        Element.Text = GetName();
                }
            }

            public virtual Func<string> GetName { get; set; }
            public virtual T Element { get; protected set; }

            public MenuElement(Func<string> GetName, IMenuCategory parent = null)
            {
                Init();
                this.GetName = GetName;
                Parent = parent;

                if (CanAddElements)
                    menuUpdateActions.Add(Update);
            }

            public MenuElement(string name, IMenuCategory parent = null) : this(() => name, parent)
            { }

            /// <summary>
            /// Used to instantiate HudAPIv2.MenuItemBase elements upon initialization of the Text Hud API
            /// </summary>
            public abstract void InitElement();

            /// <summary>
            /// Used to continuously update menu elements
            /// </summary>
            protected virtual void Update()
            {
                if (Element != null)
                    Element.Text = Name;
            }
        }

        /// <summary>
        /// Base class for all menu elements capable of containing other menu elements.
        /// </summary>
        public abstract class MenuCategoryBase<T> : MenuElement<T>, IMenuCategory where T : HudApiMin.MenuCategoryBase
        {
            public virtual string Header
            {
                get { return header; }
                set
                {
                    header = value;

                    if (Element != null)
                        Element.Header = header;
                }
            }

            public virtual HudApiMin.MenuCategoryBase CategoryBase { get { return Element as HudApiMin.MenuCategoryBase; } }
            public virtual bool IsRoot { get; protected set; }

            protected string header;
            protected Queue<IMenuElement> children;

            public MenuCategoryBase(Func<string> GetName, string header, IMenuCategory parent = null, IList<IMenuElement> children = null, bool isRoot = false) : base(GetName, parent)
            {
                this.Header = header;
                this.IsRoot = isRoot;
                this.children = new Queue<IMenuElement>();

                if (children != null)
                    AddChildren(children);                    
            }

            public MenuCategoryBase(string name, string header, IMenuCategory parent = null, IList<IMenuElement> children = null, bool isRoot = false) : this(() => name, header, parent, children, isRoot)
            { }

            public virtual void AddChild(IMenuElement child)
            {
                child.Parent = this;
                children.Enqueue(child);
            }

            public virtual void AddChildren(IList<IMenuElement> newChildren)
            {
                for (int n = 0; n < newChildren.Count; n++)
                {
                    newChildren[n].Parent = this;
                    children.Enqueue(newChildren[n]);
                }
            }

            protected override void Update()
            {
                if (Element != null)
                {
                    IMenuElement child;

                    while (children.Count > 0)
                    {
                        if (children.TryDequeue(out child))
                            child.InitElement();
                    }
                }

                base.Update();
            }
        }

        /// <summary>
        /// Contains all settings menu elements for a given mod; singleton. Must be initalized before any other menu elements.
        /// </summary>
        private sealed class MenuRoot : MenuCategoryBase<HudApiMin.MenuRootCategory>
        {
            public static MenuRoot Instance { get; private set; }

            /// <summary>
            /// This does nothing; it's only here because I couldn't be bothered to remove it from this type's base classes.
            /// </summary>
            public override IMenuCategory Parent { get { return this; } set { } }

            private MenuRoot(string name, string header) : base(name, header, null, null, true)
            { }

            public static void Init(string name, string header)
            {
                if (Instance == null)
                    Instance = new MenuRoot(name, header);
            }

            public override void InitElement() =>
                Element = new HudApiMin.MenuRootCategory(Name, MenuFlag.PlayerMenu, Header);

            protected override void Update()
            {
                if (Element == null)
                    InitElement();

                base.Update();
            }
        }

        /// <summary>
        /// Collapsable submenu that can contain other elements, including other submenus
        /// </summary>
        public class MenuCategory : MenuCategoryBase<HudApiMin.MenuSubCategory>
        {
            public MenuCategory(Func<string> GetName, string header, List<IMenuElement> children = null, IMenuCategory parent = null) : base(GetName, header, parent, children)
            { }

            public MenuCategory(string name, string header, List<IMenuElement> children = null, IMenuCategory parent = null) : base(name, header, parent, children)
            { }

            public override void InitElement() =>
                Element = new HudApiMin.MenuSubCategory(Name, Parent.CategoryBase, Header);
        }

        /// <summary>
        /// Wrapper base for HudAPIv2.MenuItem based controls (buttons, sliders, text boxes, etc.)
        /// </summary>
        public abstract class MenuSetting<T> : MenuElement<T> where T : HudApiMin.MenuItemBase
        {
            public MenuSetting(Func<string> GetName, IMenuCategory parent = null) : base(GetName, parent)
            {
                if (typeof(T) == typeof(HudApiMin.MenuCategoryBase))
                    throw new Exception("Types of HudAPIv2.MenuCategoryBase cannot be used to create MenuSettings.");
            }

            public MenuSetting(string name, IMenuCategory parent = null) : this(() => name, parent)
            { }
        }

        /// <summary>
        /// Creates a clickable menu button
        /// </summary>
        public class MenuButton : MenuSetting<HudApiMin.MenuItem>
        {
            private readonly Action OnClickAction;

            public MenuButton(Func<string> GetName, Action OnClick, IMenuCategory parent = null) : base(GetName, parent)
            {
                OnClickAction = OnClick;
            }

            public MenuButton(string name, Action OnClick, IMenuCategory parent = null) : base(name, parent)
            {
                OnClickAction = OnClick;
            }

            private void OnClick() =>
                RichHudMain.Instance.RunSafeAction(OnClickAction);

            public override void InitElement() =>
                Element = new HudApiMin.MenuItem(Name, Parent.CategoryBase, OnClick);
        }
    }
}