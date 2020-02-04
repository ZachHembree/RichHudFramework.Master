using RichHudFramework.Game;
using RichHudFramework.IO;
using RichHudFramework.UI;
using VRage.Input;
using System.Xml.Serialization;

namespace RichHudFramework.Server
{
    [XmlRoot, XmlType(TypeName = "RichHudSettings")]
    public sealed class MasterConfig : ConfigRoot<MasterConfig>
    {
        [XmlElement(ElementName = "InputSettings")]
        public BindsConfig binds;

        protected override MasterConfig GetDefaults()
        {
            return new MasterConfig()
            {
                VersionID = 1,
                binds = BindsConfig.Defaults,
            };
        }  
    }

    public class BindsConfig : Config<BindsConfig>
    {
        [XmlIgnore]
        public static BindDefinition[] DefaultBinds
        {
            get
            {
                BindDefinition[] copy = new BindDefinition[defaultBinds.Length];

                for (int n = 0; n < defaultBinds.Length; n++)
                    copy[n] = defaultBinds[n];

                return copy;
            }
        }

        private static readonly BindDefinition[] defaultBinds = new BindDefinition[]
        {
            new BindDefinition("ToggleTerminal", new string[] { "OemTilde" }),
        };

        [XmlArray("KeyBinds")]
        public BindDefinition[] bindData;

        protected override BindsConfig GetDefaults()
        {
            return new BindsConfig { bindData = DefaultBinds };
        }

        /// <summary>
        /// Checks any if fields have invalid values and resets them to the default if necessary.
        /// </summary>
        public override void Validate()
        {
            if (bindData == null || bindData.Length != defaultBinds.Length)
                bindData = DefaultBinds;
        }
    }
}
