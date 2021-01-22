using System;
using VRage;

namespace RichHudFramework.Server
{
    using UI;
    using UI.Server;
    using UI.Rendering.Server;
    using Internal;
    using ClientData = MyTuple<string, Action<int, object>, Action, int>;
    using ServerData = MyTuple<Action, Func<int, object>, int>;
    using ApiMemberAccessor = System.Func<object, int, object>;

    public sealed class RichHudDebug : RichHudComponentBase
    {
        public static bool EnableDebug { get; set; }

        private static RichHudDebug instance;
        private readonly DemoPage demoPage;
        private readonly TextPage statsText;

        private RichHudDebug() : base(false, true)
        {
            if (instance == null)
                instance = this;
            else
                throw new Exception($"Only one instance of {GetType().Name} can exist at any given time.");

            demoPage = new DemoPage()
            {
                Name = "Demo",
                Enabled = false
            };

            statsText = new TextPage()
            {
                Name = "Statistics",
                Enabled = false
            };

            RichHudTerminal.Root.Add(demoPage);
            RichHudTerminal.Root.Add(statsText);
            EnableDebug = false;
        }

        public static void Init()
        {
            if (instance == null)
                new RichHudDebug();
        }

        public override void Close()
        {
            instance = null;
        }

        public override void Draw()
        {
            demoPage.Enabled = EnableDebug;
            statsText.Enabled = EnableDebug;
        }
    }
}