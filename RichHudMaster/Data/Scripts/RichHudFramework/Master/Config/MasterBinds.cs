using RichHudFramework.Game;
using RichHudFramework.IO;
using RichHudFramework.UI;
using RichHudFramework.UI.Server;
using VRage.Input;

namespace RichHudFramework.Server
{
    internal sealed class MasterBinds : ModBase.ComponentBase
    {
        public static BindsConfig Cfg
        {
            get { return new BindsConfig { bindData = BindGroup.GetBindDefinitions() }; }
            set { Instance.bindGroup.TryLoadBindData(value.bindData); }
        }

        public static IBind ToggleTerminal { get { return BindGroup[0]; } }
        public static IBindGroup BindGroup { get { return Instance.bindGroup; } }

        private static MasterBinds Instance
        {
            get { Init(); return instance; }
            set { instance = value; }
        }
        private static MasterBinds instance;
        private static readonly string[] bindNames = new string[] { "ToggleTerminal" };

        private readonly IBindGroup bindGroup;

        private MasterBinds() : base(false, true)
        {
            bindGroup = BindManager.GetOrCreateGroup("BvMain");
            bindGroup.RegisterBinds(bindNames);
        }

        private static void Init()
        {
            if (instance == null)
            {
                instance = new MasterBinds();
                Cfg = BindsConfig.Defaults;

                MasterConfig.OnConfigSave += instance.UpdateConfig;
                MasterConfig.OnConfigLoad += instance.UpdateBinds;
            }
        }

        public override void Close()
        {
            MasterConfig.OnConfigSave -= UpdateConfig;
            MasterConfig.OnConfigLoad -= UpdateBinds;
            Instance = null;
        }

        private void UpdateConfig() =>
            MasterConfig.Current.binds = Cfg;

        private void UpdateBinds() =>
            Cfg = MasterConfig.Current.binds;
    }
}