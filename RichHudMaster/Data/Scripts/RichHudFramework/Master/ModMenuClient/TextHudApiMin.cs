using RichHudFramework.Game;
using Sandbox.ModAPI;
using System;
using System.Text;
using VRage;
using VRage.Input;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace RichHudFramework.UI.TextHudApi
{
    /// <summary>
    /// HudAPI client stripped down to only allow usage of mod menu categories and buttons.
    /// </summary>
    public class HudApiMin : ModBase.ComponentBase
    {
        public static HudApiMin Instance
        {
            get { Init(); return instance; }
            private set { instance = value; }
        }

        /// <summary>
        /// If Heartbeat is true you may call any constructor in this class. Do not call any constructor or set properties if this is false.
        /// </summary>
        public static bool Heartbeat => Instance.registered;

        private static HudApiMin instance;
        private static bool initializing = false;

        private const long REGISTRATIONID = 573804956;
        private bool registered = false;
        private Action m_onRegisteredAction;

        private Func<int, object> MessageFactory;
        private Action<object, int, object> MessageSetter;
        private Func<object, int, object> MessageGetter;

        /// <summary>
        /// Create a HudAPI Instance. Please only create one per mod. 
        /// </summary>
        /// <param name="onRegisteredAction">Callback once the HudAPI is active. You can Instantiate HudAPI objects in this Action</param>
        private HudApiMin() : base(false, true)
        {
            if (Instance != null)
                return;

            m_onRegisteredAction = null;
            MyAPIGateway.Utilities.RegisterMessageHandler(REGISTRATIONID, RegisterComponents);
        }

        private static void Init()
        {
            if (instance == null && !initializing)
            {
                initializing = true;
                instance = new HudApiMin();
                initializing = false;
            }
        }

        public override void Close()
        {
            Instance = null;
            Unload();
        }

        /// <summary>
        /// Unregisters mod and frees references. 
        /// </summary>
        public void Unload()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(REGISTRATIONID, RegisterComponents);
            MessageFactory = null;
            MessageSetter = null;
            MessageGetter = null;
            registered = false;
            m_onRegisteredAction = null;
            Instance = null;
        }
        private enum RegistrationEnum : int
        {
            OnScreenUpdate = 2000
        }
        private void RegisterComponents(object obj)
        {
            if (registered)
                return;
            if (obj is MyTuple<Func<int, object>, Action<object, int, object>, Func<object, int, object>, Action<object>>)
            {
                var Handlers = (MyTuple<Func<int, object>, Action<object, int, object>, Func<object, int, object>, Action<object>>)obj;
                MessageFactory = Handlers.Item1;
                MessageSetter = Handlers.Item2;
                MessageGetter = Handlers.Item3;

                registered = true;
                if (m_onRegisteredAction != null)
                    m_onRegisteredAction();
                MessageSet(null, (int)RegistrationEnum.OnScreenUpdate, new MyTuple<Action>(ScreenChangedHandle));
            }
        }

        #region Intercomm
        private object CreateMessage(MessageTypes type)
        {
            return MessageFactory((int)type);
        }

        private object MessageGet(object BackingObject, int Member)
        {
            return MessageGetter(BackingObject, Member);
        }

        private void MessageSet(object BackingObject, int Member, object Value)
        {
            MessageSetter(BackingObject, Member, Value);
        }

        private void RegisterCheck()
        {
            if (Instance.registered == false)
            {
                throw new InvalidOperationException("HudAPI: Failed to create backing object. Do not instantiate without checking if heartbeat is true.");
            }
        }

        private void ScreenChangedHandle()
        { }
        #endregion

        private enum MessageTypes : int
        {
            MenuItem = 20,
            MenuSubCategory = 21,
            MenuRootCategory = 22,
        }

        #region Menu

        public abstract class MenuItemBase
        {
            private enum MenuItemBaseMembers : int
            {
                Text = 0,
                Interactable
            }
            internal object BackingObject;

            public virtual MenuCategoryBase Parent { get; set; }

            /// <summary>
            /// Text displayed in the category list
            /// </summary>
            public string Text
            {
                get
                {
                    return (string)(Instance.MessageGet(BackingObject, (int)MenuItemBaseMembers.Text));
                }
                set
                {
                    Instance.MessageSet(BackingObject, (int)MenuItemBaseMembers.Text, value);
                }
            }
            /// <summary>
            /// User can select this item. true by default
            /// </summary>
            public bool Interactable
            {
                get
                {
                    return (bool)(Instance.MessageGet(BackingObject, (int)MenuItemBaseMembers.Interactable));
                }
                set
                {
                    Instance.MessageSet(BackingObject, (int)MenuItemBaseMembers.Interactable, value);
                }
            }
        }
        public class MenuItem : MenuItemBase
        {
            private enum MenuItemMembers : int
            {
                OnClickAction = 100,
                Parent
            }
            /// <summary>
            /// On click event that will be fired if the user selects this item.
            /// </summary>
            public Action OnClick
            {
                get
                {
                    return (Action)(Instance.MessageGet(BackingObject, (int)MenuItemMembers.OnClickAction));
                }
                set
                {
                    Instance.MessageSet(BackingObject, (int)MenuItemMembers.OnClickAction, value);
                }
            }
            /// <summary>
            /// Must be either a MenuRootCategory or MenuSubCategory object
            /// </summary>
            public override MenuCategoryBase Parent
            {
                set
                {
                    Instance.MessageSet(BackingObject, (int)MenuItemMembers.Parent, value.BackingObject);
                }
            }
            /// <summary>
            /// Basic toggle. You can use this to create on/off toggles, checkbox lists or option lists. 
            /// </summary>
            /// <param name="Text">Text displayed in the category list</param>
            /// <param name="Parent">Must be either a MenuRootCategory or MenuSubCategory object</param>
            /// <param name="OnClick">On click event that will be fired if the user selects this item.</param>
            /// <param name="Interactable">User can select this item. true by default</param>
            public MenuItem(string Text, MenuCategoryBase Parent, Action OnClick = null, bool Interactable = true)
            {
                Instance.RegisterCheck();
                BackingObject = Instance.CreateMessage(MessageTypes.MenuItem);

                this.Text = Text;
                this.Parent = Parent;
                this.OnClick = OnClick;
                this.Interactable = Interactable;
            }
        }

        public abstract class MenuCategoryBase : MenuItemBase
        {
            private enum MenuBaseCategoryMembers : int
            {
                Header = 100
            }
            /// <summary>
            /// Header text of the menu list.
            /// </summary>
            public string Header
            {
                get
                {
                    return (string)(Instance.MessageGet(BackingObject, (int)MenuBaseCategoryMembers.Header));
                }
                set
                {
                    Instance.MessageSet(BackingObject, (int)MenuBaseCategoryMembers.Header, value);
                }
            }
        }

        public class MenuRootCategory : MenuCategoryBase
        {
            public enum MenuFlag : int
            {
                None = 0,
                PlayerMenu = 1,
                AdminMenu = 2
            }
            private enum MenuRootCategoryMembers : int
            {
                MenuFlag = 200

            }
            /// <summary>
            /// Which menu to attach to, either Player or Admin menus. 
            /// </summary>
            public MenuFlag Menu
            {
                get
                {
                    return (MenuFlag)(Instance.MessageGet(BackingObject, (int)MenuRootCategoryMembers.MenuFlag));
                }
                set
                {
                    Instance.MessageSet(BackingObject, (int)MenuRootCategoryMembers.MenuFlag, (int)value);
                }
            }
            /// <summary>
            /// Create only one of these per mod. Automatically attaches to parent lists. 
            /// </summary>
            /// <param name="Text">Text displayed in the root menu list</param>
            /// <param name="AttachedMenu">Which menu to attach to, either Player or Admin menus. </param>
            /// <param name="HeaderText">Header text of this menu list.</param>
            public MenuRootCategory(string Text, MenuFlag AttachedMenu = MenuFlag.None, string HeaderText = "Default Header")
            {
                Instance.RegisterCheck();
                BackingObject = Instance.CreateMessage(MessageTypes.MenuRootCategory);
                this.Text = Text;
                Header = HeaderText;
                Menu = AttachedMenu;
            }
        }
        public class MenuSubCategory : MenuCategoryBase
        {
            private enum MenuSubCategoryMembers : int
            {
                Parent = 200
            }

            /// <summary>
            /// Must be either a MenuRootCategory or MenuSubCategory objectMust be either a MenuRootCategory or MenuSubCategory object
            /// </summary>
            public override MenuCategoryBase Parent
            {
                set
                {
                    Instance.MessageSet(BackingObject, (int)MenuSubCategoryMembers.Parent, value.BackingObject);
                }
            }

            /// <summary>
            /// Creates a sub category, must attach to either Root or another Sub Category.
            /// </summary>
            /// <param name="Text">Text displayed in the category list</param>
            /// <param name="Parent">Must be either a MenuRootCategory or MenuSubCategory objectMust be either a MenuRootCategory or MenuSubCategory object</param>
            /// <param name="HeaderText">Header text of this menu list.</param>
            public MenuSubCategory(string Text, MenuCategoryBase Parent, string HeaderText = "Default Header")
            {
                Instance.RegisterCheck();
                BackingObject = Instance.CreateMessage(MessageTypes.MenuSubCategory);
                this.Text = Text;
                this.Header = HeaderText;
                this.Parent = Parent;
            }
        }

        #endregion
    }
}