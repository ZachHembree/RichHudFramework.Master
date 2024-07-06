﻿using System.Xml.Serialization;
using VRage;
using BindDefinitionDataOld = VRage.MyTuple<string, string[]>;

namespace RichHudFramework
{
    using BindDefinitionData = MyTuple<string, string[], string[][]>;

    namespace UI
    {
        [XmlType(TypeName = "Alias")]
        public struct BindAliasDefinition
        {
            [XmlArray("Controls")]
            public string[] controlNames;

            public BindAliasDefinition(string[] controlNames)
            {
                this.controlNames = controlNames;
            }

            public static implicit operator string[](BindAliasDefinition aliasDef)
            {
                return aliasDef.controlNames;
            }

            public static implicit operator BindAliasDefinition(string[] alias)
            {
                return new BindAliasDefinition(alias);
            }
        }

        /// <summary>
        /// Stores data for serializing individual key binds to XML.
        /// </summary>
        [XmlType(TypeName = "Bind")]
        public struct BindDefinition
        {
            [XmlAttribute]
            public string name;

            [XmlArray("Controls")]
            public string[] controlNames;

            [XmlArray("Aliases")]
            public BindAliasDefinition[] aliases;

            public BindDefinition(string name, string[] controlNames, BindAliasDefinition[] aliases = null)
            {
                this.name = name;
                this.controlNames = controlNames;
                this.aliases = aliases;
            }

            public static implicit operator BindDefinition(BindDefinitionDataOld value)
            {
                return new BindDefinition(value.Item1, value.Item2);
            }

            public static explicit operator BindDefinition(BindDefinitionData value)
            {
                var aliases = new BindAliasDefinition[value.Item3.Length];

                for (int i = 0; i < aliases.Length; i++)
                    aliases[i] = value.Item3[i];

                return new BindDefinition(value.Item1, value.Item2, aliases);
            }

            public static explicit operator BindDefinitionData(BindDefinition value)
            {
                var aliasData = new string[value.aliases.Length][];

                for (int i = 0; i < aliasData.Length; i++)
                    aliasData[i] = value.aliases[i];

                return new BindDefinitionData(value.name, value.controlNames, aliasData);
            }
        }
    }
}