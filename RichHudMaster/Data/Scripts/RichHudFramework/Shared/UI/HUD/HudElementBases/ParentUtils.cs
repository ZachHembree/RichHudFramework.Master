using System;
using System.Collections.Generic;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;
using HudLayerData = VRage.MyTuple<
	sbyte, // 1- zOffset
	byte, // 2 - zOffsetInner
	ushort // 3 - fullZOffset
>;
using HudNodeHookData = VRage.MyTuple<
	System.Func<object, int, object>, // 1 -  GetOrSetApiMemberFunc
	System.Action, // 2 - InputDepthAction
	System.Action, // 3 - InputAction
	System.Action, // 4 - SizingAction
	System.Action<bool>, // 5 - LayoutAction
	System.Action // 6 - DrawAction
>;
using HudNodeStateData = VRage.MyTuple<
	uint, // 1 - State
	uint, // 2 - NodeVisibleMask
	uint // 3 - NodeInputMask
>;
using HudSpaceFunc = System.Func<VRageMath.Vector3D>;

namespace RichHudFramework
{
	using HudNodeData = MyTuple<
		HudNodeStateData, // 1 - { 1.1 - State, 1.2 - NodeVisibleMask, 1.3 - NodeInputMask }
		HudSpaceFunc, // 2 - GetNodeOriginFunc
		HudLayerData, // 3 - { 3.1 - zOffset, 3.2 - zOffsetInner, 3.3 - fullZOffset }
		HudNodeHookData, // 4 - Main hooks
		object, // 5 - Parent as HudNodeDataHandle
		List<object> // 6 - Children as IReadOnlyList<HudNodeDataHandle>
	>;

	namespace UI
	{
		// Read-only length-1 array containing raw UI node data
		using HudNodeDataHandle = IReadOnlyList<HudNodeData>;
		using Client;
        using Server;
		using System.Collections.Generic;

        public abstract partial class HudParentBase
        {
            /// <summary>
            /// Utilities used internally to access parent node members
            /// </summary>
            protected static class ParentUtils
            {
				/// <summary>
				/// Used internally quickly register a list of child nodes to a parent.
				/// </summary>
				public static void RegisterNodes(HudParentBase newParent, IReadOnlyList<HudNodeBase> nodes, bool canPreload)
				{
					newParent.children.EnsureCapacity(newParent.children.Count + nodes.Count);

					for (int n = 0; n < nodes.Count; n++)
					{
						HudNodeBase node = nodes[n];
						node._dataHandle[0].Item3 = newParent.DataHandle;
						newParent.childHandles.Add(node.DataHandle);
						newParent.children.Add(node);
					}
				}

				/// <summary>
				/// Used internally quickly register a list of child nodes to a parent.
				/// </summary>
				public static void RegisterNodes<TCon, TNode>(HudParentBase newParent, IReadOnlyList<TCon> nodes, bool canPreload)
					where TCon : IHudElementContainer<TNode>, new()
					where TNode : HudNodeBase
				{
					newParent.children.EnsureCapacity(newParent.children.Count + nodes.Count);

					for (int n = 0; n < nodes.Count; n++)
					{
						HudNodeBase node = nodes[n].Element;
						node._dataHandle[0].Item3 = newParent.DataHandle;
						newParent.childHandles.Add(node.DataHandle);
						newParent.children.Add(node);
					}
				}

				/// <summary>
				/// Used internally to quickly unregister child nodes from their parent. Removes the range of nodes
				/// specified in the node list from the child list.
				/// </summary>
				public static void UnregisterNodes(HudParentBase parent, IReadOnlyList<HudNodeBase> nodes, int index, int count)
				{
					if (count > 0)
					{
						int conEnd = index + count - 1;

						if (!(index >= 0 && index < nodes.Count && conEnd <= nodes.Count))
							throw new Exception("Specified indices are out of range.");

						if (parent == null)
							throw new Exception("Parent cannot be null");

						for (int i = index; i <= conEnd; i++)
						{
							int start = 0;

							while (start < parent.children.Count && parent.children[start] != nodes[i])
								start++;

							if (parent.children[start] == nodes[i])
							{
								int j = start, end = start;

								while (j < parent.children.Count && i <= conEnd && parent.children[j] == nodes[i])
								{
									end = j;
									i++;
									j++;
								}

								parent.childHandles.RemoveRange(start, end - start + 1);
								parent.children.RemoveRange(start, end - start + 1);
							}
						}
					}
				}

				/// <summary>
				/// Used internally to quickly unregister child nodes from their parent. Removes the range of nodes
				/// specified in the node list from the child list.
				/// </summary>
				public static void UnregisterNodes<TCon, TNode>(HudParentBase parent, IReadOnlyList<TCon> nodes, int index, int count)
					where TCon : IHudElementContainer<TNode>, new()
					where TNode : HudNodeBase
				{
					if (count > 0)
					{
						int conEnd = index + count - 1;
						var children = parent.children;

						if (!(index >= 0 && index < nodes.Count && conEnd <= nodes.Count))
							throw new Exception("Specified indices are out of range.");

						if (parent == null)
							throw new Exception("Parent cannot be null");

						for (int i = index; i <= conEnd; i++)
						{
							int start = 0;

							while (start < children.Count && children[start] != nodes[i].Element)
								start++;

							if (children[start] == nodes[i].Element)
							{
								int j = start, end = start;

								while (j < children.Count && i <= conEnd && children[j] == nodes[i].Element)
								{
									end = j;
									i++;
									j++;
								}

								parent.childHandles.RemoveRange(start, end - start + 1);
								children.RemoveRange(start, end - start + 1);
							}
						}
					}
				}

				/// <summary>
				/// Calculates the full z-offset using the public offset and inner offset.
				/// </summary>
				public static ushort GetFullZOffset(int[] layerData, HudParentBase parent = null)
                {
                    byte outerOffset = (byte)(layerData[0] - sbyte.MinValue);
                    ushort innerOffset = (ushort)(layerData[1] << 8);

                    if (parent != null)
                    {
                        ushort parentFull = (ushort)parent.layerData[2];

                        outerOffset += (byte)((parentFull & 0x00FF) + sbyte.MinValue);
                        innerOffset += (ushort)(parentFull & 0xFF00);
                    }

                    return (ushort)(innerOffset | outerOffset);
                }
            }
        }
    }
}