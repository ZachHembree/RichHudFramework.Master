using System;
using System.Collections.Generic;
using VRageMath;
using HudNodeHookData = VRage.MyTuple<
	System.Func<object, int, object>, // 1 -  GetOrSetApiMemberFunc
	System.Action, // 2 - InputDepthAction
	System.Action, // 3 - InputAction
	System.Action, // 4 - SizingAction
	System.Action<bool>, // 5 - LayoutAction
	System.Action // 6 - DrawAction
>;
using HudSpaceOriginFunc = System.Func<VRageMath.Vector3D>;

namespace RichHudFramework
{
	namespace UI.Server
	{
		using static NodeConfigIndices;

		public partial class HudMain
		{
			/// <summary>
			/// Contains node configuration information required for updates
			/// </summary>
			public struct NodeState
			{
				public IReadOnlyList<uint> ParentConfig;
				public uint[] Config;
			}

			public struct NodeHook<T>
			{
				/// <summary>
				/// Callback to be invoked for a node on a given update stage
				/// </summary>
				public Action<T> Callback;

				/// <summary>
				/// Unique index corresponding to a NodeState. -1 if one is not assigned.
				/// </summary>
				public int NodeID;
			}

			public struct NodeHook
			{
				/// <summary>
				/// Callback to be invoked for a node on a given update stage
				/// </summary>
				public Action Callback;

				/// <summary>
				/// Unique index corresponding to a NodeState. -1 if one is not assigned.
				/// </summary>
				public int NodeID;
			}

			/// <summary>
			/// Contains callbacks for updating a UI node subtree
			/// </summary>
			public class NodeHooks
			{
				// Updates + node indices - -1 for no node data
				public List<NodeHook> SizingActions;
				public List<NodeHook<bool>> LayoutActions;
				public List<NodeHook> DrawActions;
				public List<NodeHook> InputDepthActions;
				public List<NodeHook> InputActions;

				public NodeHooks(int capacity)
				{
					SizingActions = new List<NodeHook>(capacity);
					LayoutActions = new List<NodeHook<bool>>(capacity);
					DrawActions = new List<NodeHook>(capacity);
					InputDepthActions = new List<NodeHook>(capacity);
					InputActions = new List<NodeHook>(capacity);
				}

				public void TrimExcess()
				{
					SizingActions.TrimExcess();
					LayoutActions.TrimExcess();
					DrawActions.TrimExcess();
					InputDepthActions.TrimExcess();
					InputActions.TrimExcess();
				}

				public void EnsureCapacity(int capacity)
				{
					SizingActions.EnsureCapacity(capacity);
					LayoutActions.EnsureCapacity(capacity);
					DrawActions.EnsureCapacity(capacity);
					InputDepthActions.EnsureCapacity(capacity);
					InputActions.EnsureCapacity(capacity);
				}

				public void Clear()
				{
					SizingActions.Clear();
					LayoutActions.Clear();
					DrawActions.Clear();
					InputDepthActions.Clear();
					InputActions.Clear();
				}
			}

			public class FlatSubtree
			{
				/// <summary>
				/// Set true if active, sorted members need to be rebuilt from inactive data
				/// </summary>
				public bool IsActiveStale;

				/// <summary>
				/// Returns the inner Z-sorting offset used for the subtree
				/// </summary>
				public byte InnerZLayer => (byte)((RootConfig?[FullZOffsetID] ?? GetLayerFuncOld()) >> 8);

				/// <summary>
				/// Configuration reference to subtree root
				/// </summary>
				public uint[] RootConfig;

				/// <summary>
				/// Legacy ZOffset delegate for subtree root
				/// </summary>
				public Func<ushort> GetLayerFuncOld;

				/// <summary>
				/// Delegate for retrieving the origin of the subtree in world space
				/// </summary>
				public HudSpaceOriginFunc GetOriginFunc;

				/// <summary>
				/// Reference to the tree client that owns the subtree
				/// </summary>
				public TreeClient Owner;

				public readonly FlatSubtreeData Inactive;
				public readonly SortedSubtreeData Active;

				public FlatSubtree(int capacity = 0)
				{
					Inactive = new FlatSubtreeData(capacity);
					Active = new SortedSubtreeData(capacity);
					IsActiveStale = true;
				}

				public void Clear()
				{			
					IsActiveStale = true;
					RootConfig = null;
					GetLayerFuncOld = null;
					GetOriginFunc = null;

					Inactive.Clear();
					Active.Clear();
					Owner = null;
				}
			}

			public class FlatSubtreeData
			{
				// Parallel - state data null for vID 12 and older
				public List<NodeState> StateData;
				public List<byte> OuterOffsets; // Sorting only
				public List<HudNodeHookData> HookData;

				public FlatSubtreeData(int capacity = 0)
				{
					StateData = new List<NodeState>(capacity);
					OuterOffsets = new List<byte>(capacity);
					HookData = new List<HudNodeHookData>(capacity);
				}

				public void TrimExcess()
				{
					StateData.TrimExcess();
					OuterOffsets.TrimExcess();
					HookData.TrimExcess();
				}

				/// <summary>
				/// Truncates the buffers to the given length
				/// </summary>
				public void Truncate(int newLength)
				{
					if (newLength >= StateData.Count)
						return;

					int start = newLength;
					int count = StateData.Count - newLength;

					StateData.RemoveRange(start, count);
					OuterOffsets.RemoveRange(start, count);
					HookData.RemoveRange(start, count);
				}

				public void EnsureCapacity(int capacity)
				{
					StateData.EnsureCapacity(capacity);
					OuterOffsets.EnsureCapacity(capacity);
					HookData.EnsureCapacity(capacity);
				}

				public void Clear()
				{
					StateData.Clear();
					OuterOffsets.Clear();
					HookData.Clear();
				}
			}

			public class SortedSubtreeData
			{
				public List<NodeState> StateData;
				public NodeHooks Hooks;

				public SortedSubtreeData(int capacity = 0)
				{
					StateData = new List<NodeState>(capacity);
					Hooks = new NodeHooks(capacity);
				}

				public void TrimExcess()
				{
					StateData.TrimExcess();
					Hooks.TrimExcess();
				}

				public void EnsureCapacity(int capacity)
				{
					StateData.EnsureCapacity(capacity);
					Hooks.EnsureCapacity(capacity);
				}

				public void Clear()
				{
					StateData.Clear();
					Hooks.Clear();
				}
			}
		}
	}
}