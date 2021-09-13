using RichHudFramework.Internal;
using RichHudFramework.IO;
using RichHudFramework.UI;
using RichHudFramework.UI.Server;
using VRage.Input;

namespace RichHudFramework.Server
{
    public sealed class MasterBinds : RichHudComponentBase
    {
        public static BindsConfig Cfg
        {
            get { return new BindsConfig { bindData = BindGroup.GetBindDefinitions() }; }
            set { Instance.bindGroup.TryLoadBindData(value.bindData); }
        }

        public static IBind ToggleTerminalOld { get { Init(); return _instance.bindGroup[0]; } }

        public static IBind ToggleTerminal { get { Init(); return _instance.bindGroup[1]; } }

        public static IBindGroup BindGroup { get { return Instance.bindGroup; } }

        private static MasterBinds Instance
        {
            get { Init(); return _instance; }
            set { _instance = value; }
        }
        private static MasterBinds _instance;
        private static readonly string[] bindNames = new string[] { "ToggleTerminalOld", "ToggleTerminal" };

        private readonly IBindGroup bindGroup;

        private MasterBinds() : base(false, true)
        {
            bindGroup = BindManager.GetOrCreateGroup("Main");
            bindGroup.RegisterBinds(bindNames);
        }

        private static void Init()
        {
            if (_instance == null)
            {
                _instance = new MasterBinds();
                Cfg = MasterConfig.Current.binds;

                MasterConfig.OnConfigSave += _instance.UpdateConfig;
                MasterConfig.OnConfigLoad += _instance.UpdateBinds;
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