using RichHudFramework.Internal;
using RichHudFramework.UI.Rendering;
using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework
{
    namespace UI.Server
    {
        public sealed partial class RichHudTerminal : RichHudComponentBase
        {
            private class ModControlRoot : TerminalPageCategory, IModControlRoot
            {
                /// <summary>
                /// Invoked when a new page is selected
                /// </summary>
                public event EventHandler SelectionChanged;

                /// <summary>
                /// Currently selected <see cref="TerminalPageBase"/>.
                /// </summary>
                public override TerminalPageBase SelectedPage
                {
                    get
                    {
                        var selection = treeBox.Selection;

                        if (selection != null)
                        {
                            var category = selection as TerminalPageCategory;

                            if (category != null)
                                return category.SelectedPage;
                            else
                                return selection.AssocMember as TerminalPageBase;
                        }
                        else
                            return null;
                    }
                }

                /// <summary>
                /// Currently selected <see cref="TerminalPageCategory"/>.
                /// </summary>
                public TerminalPageCategory SelectedSubcategory => treeBox.Selection as TerminalPageCategory;

                /// <summary>
                /// Page subcategories attached to the mod root
                /// </summary>
                public IReadOnlyList<TerminalPageCategory> Subcategories => subcategories;

                /// <summary>
                /// TreeBox member list
                /// </summary>
                public IReadOnlyList<SelectionBoxEntryTuple<LabelElementBase, object>> ListEntries => treeBox.EntryList;

                private Action ApiCallbackAction;
                protected readonly List<TerminalPageCategory> subcategories;

                public ModControlRoot() : base()
                {
                    Enabled = false;
                    subcategories = new List<TerminalPageCategory>();
                    treeBox.SelectionChanged += InvokeCallback;
                }

                /// <summary>
                /// Adds a page subcategory to the control root
                /// </summary>
                public void Add(TerminalPageCategory subcategory)
                {
                    treeBox.Add(subcategory);
                    subcategories.Add(subcategory);
                }

                public void AddRange(IReadOnlyList<IModRootMember> members) =>
                    AddRangeInternal(members);

                protected override void AddRangeInternal(IReadOnlyList<object> members)
                {
                    foreach (object member in members)
                    {
                        var page = member as TerminalPageBase;

                        if (page != null)
                            Add(page);
                        else
                        {
                            var subcategory = member as TerminalPageCategory;

                            if (subcategory != null)
                                Add(subcategory);
                        }
                    }
                }

                public void OnSelectionChanged(object sender, EventArgs args)
                {
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                    ApiCallbackAction?.Invoke();
                }

                protected override object GetOrSetMember(object data, int memberEnum)
                {
                    switch ((ModControlRootAccessors)memberEnum)
                    {
                        case ModControlRootAccessors.GetOrSetCallback:
                            {
                                if (data == null)
                                    return ApiCallbackAction;
                                else
                                    ApiCallbackAction = data as Action;

                                return null;
                            }
                        case ModControlRootAccessors.GetCategoryAccessors:
                            return new MyTuple<object, Func<int>>()
                            {
                                Item1 = new Func<int, ControlContainerMembers>(x => subcategories[x].GetApiData()),
                                Item2 = () => subcategories.Count
                            };
                    }

                    return base.GetOrSetMember(data, memberEnum);
                }
            }
        }
    }
}