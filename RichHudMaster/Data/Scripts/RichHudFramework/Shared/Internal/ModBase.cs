using RichHudFramework.IO;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace RichHudFramework.Internal
{
    /// <summary>
    /// Extends <see cref="MySessionComponentBase"/> to include built-in exception handling, logging and a component
    /// system.
    /// </summary>
    public abstract partial class ModBase : MySessionComponentBase
    {
        /// <summary>
        /// Determines whether or not the main class will be allowed to run on a dedicated server.
        /// </summary>
        public bool RunOnServer { get; protected set; }

        /// <summary>
        /// If true, then the mod will be allowed to run on a client.
        /// </summary>
        public bool RunOnClient { get; protected set; }

        /// <summary>
        /// If true, the mod is currently loaded.
        /// </summary>
        public new bool Loaded { get; private set; }

        /// <summary>
        /// If true, then the session component will be allowed to update.
        /// </summary>
        public bool CanUpdate
        {
            get { return _canUpdate; }

            set
            {
                if ((RunOnClient && ExceptionHandler.IsClient) || (RunOnServer && ExceptionHandler.IsDedicated))
                    _canUpdate = value;
            }
        }

        private readonly List<ComponentBase> clientComponents, serverComponents;
        private bool _canUpdate, closing;

        protected ModBase(bool runOnServer, bool runOnClient)
        {
            clientComponents = new List<ComponentBase>();
            serverComponents = new List<ComponentBase>();
            RunOnServer = runOnServer;
            RunOnClient = runOnClient;
        }

        public sealed override void LoadData()
        {
            if (!Loaded && !ExceptionHandler.Unloading && !closing)
            {
                CanUpdate = true;
                ExceptionHandler.RegisterClient(this);

                if (CanUpdate)
                    ExceptionHandler.Run(AfterLoadData);
            }
        }

        protected new virtual void AfterLoadData() { }

        public sealed override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            if (!Loaded && !ExceptionHandler.Unloading && !closing)
            {
                if (CanUpdate)
                    AfterInit();

                Loaded = true;
            }
        }

        protected virtual void AfterInit() { }

        public void ManualStart()
        {
            if (!Loaded && !closing)
            {
                CanUpdate = true;
                LoadData();
                Init(null);
            }
        }

        public override void Draw()
        {
            if (Loaded && CanUpdate)
            {
                ExceptionHandler.Run(() =>
                {
                    for (int n = 0; n < serverComponents.Count; n++)
                        serverComponents[n].Draw();

                    for (int n = 0; n < clientComponents.Count; n++)
                        clientComponents[n].Draw();
                });
            }
        }

        public override void HandleInput()
        {
            if (Loaded && CanUpdate)
            {
                ExceptionHandler.Run(() =>
                {
                    for (int n = 0; n < serverComponents.Count; n++)
                        serverComponents[n].HandleInput();

                    for (int n = 0; n < clientComponents.Count; n++)
                        clientComponents[n].HandleInput();
                });
            }
        }

        public sealed override void UpdateBeforeSimulation() =>
            BeforeUpdate();

        public sealed override void Simulate() =>
            BeforeUpdate();

        public sealed override void UpdateAfterSimulation() =>
            BeforeUpdate();

        /// <summary>
        /// The update function used (Before/Sim/After) is determined by the settings used by
        /// the MySessionComponentDescriptorAttribute applied to the child class.
        /// </summary>
        protected void BeforeUpdate()
        {
            if (Loaded && CanUpdate)
                ExceptionHandler.Run(UpdateComponents);
        }

        private void UpdateComponents()
        {
            for (int n = 0; n < serverComponents.Count; n++)
                serverComponents[n].Update();

            for (int n = 0; n < clientComponents.Count; n++)
                clientComponents[n].Update();

            Update();
        }

        /// <summary>
        /// Sim update.
        /// </summary>
        protected virtual void Update() { }

        /// <summary>
        /// Called before close and only if the session component is loaded.
        /// </summary>
        public virtual void BeforeClose() { }

        public virtual void Close()
        {
            if (Loaded && !closing)
            {
                Loaded = false;
                CanUpdate = false;
                closing = true;

                CloseComponents(clientComponents);
                CloseComponents(serverComponents);

                clientComponents.Clear();
                serverComponents.Clear();
                closing = false;
            }
        }

        private void CloseComponents(IReadOnlyList<ComponentBase> components)
        {
            string typeName = GetType().Name;

            for (int n = components.Count - 1; n >= 0; n--)
            {
                var module = components[n];
                bool success = false;

                ExceptionHandler.Run(() =>
                {
                    ExceptionHandler.WriteToLog($"[{typeName}] Closing {module.GetType().Name} module...", true);
                    module.Close();
                    success = true;
                });

                if (success)
                    ExceptionHandler.WriteToLog($"[{typeName}] Closed {module.GetType().Name} module.", true);
                else
                    ExceptionHandler.WriteToLog($"[{typeName}] Failed to close {module.GetType().Name} module.");

                module.UnregisterComponent(n);
            }
        }

        protected sealed override void UnloadData()
        { }

        /// <summary>
        /// Base class for ModBase components.
        /// </summary>
        public abstract class ComponentBase
        {
            protected ModBase Parent { get; private set; }

            /// <summary>
            /// Determines whether or not this component will run on a dedicated server and/or client.
            /// </summary>
            public readonly bool runOnServer, runOnClient;

            protected ComponentBase(bool runOnServer, bool runOnClient, ModBase parent)
            {
                this.runOnServer = runOnServer;
                this.runOnClient = runOnClient;

                RegisterComponent(parent);
            }

            public void RegisterComponent(ModBase parent)
            {
                if (Parent == null)
                {
                    if (!ExceptionHandler.IsDedicated && runOnClient)
                        parent.clientComponents.Add(this);
                    else if (ExceptionHandler.IsDedicated && runOnServer)
                        parent.serverComponents.Add(this);

                    Parent = parent;
                    ExceptionHandler.WriteToLog($"[{Parent.GetType().Name}] Registered {GetType().Name} module.", true);
                }
            }

            /// <summary>
            /// Used to manually remove object from update queue. This should only be used for objects that
            /// need to be closed while the mod is running.
            /// </summary>
            public void UnregisterComponent()
            {
                if (Parent != null)
                {
                    if (!ExceptionHandler.IsDedicated && runOnClient)
                        Parent.clientComponents.Remove(this);
                    else if (ExceptionHandler.IsDedicated && runOnServer)
                        Parent.serverComponents.Remove(this);

                    ExceptionHandler.WriteToLog($"[{Parent.GetType().Name}] Unregistered {GetType().Name} module.", true);
                    Parent = null;
                }
            }

            /// <summary>
            /// Used to manually remove object from update queue. This should only be used for objects that
            /// need to be closed while the mod is running.
            /// </summary>
            public void UnregisterComponent(int index)
            {
                if (Parent != null)
                {
                    if (!ExceptionHandler.IsDedicated && runOnClient)
                    {
                        if (index < Parent.clientComponents.Count && Parent.clientComponents[index] == this)
                        {
                            Parent.clientComponents.RemoveAt(index);

                            ExceptionHandler.WriteToLog($"[{Parent.GetType().Name}] Unregistered {GetType().Name} module.", true);
                            Parent = null;
                        }
                    }
                    else if (ExceptionHandler.IsDedicated && runOnServer)
                    {
                        if (index < Parent.serverComponents.Count && Parent.serverComponents[index] == this)
                        {
                            Parent.serverComponents.RemoveAt(index);

                            ExceptionHandler.WriteToLog($"[{Parent.GetType().Name}] Unregistered {GetType().Name} module.", true);
                            Parent = null;
                        }
                    }
                }
            }

            public virtual void Draw() { }

            public virtual void HandleInput() { }

            public virtual void Update() { }

            public virtual void Close() { }
        }

        /// <summary>
        /// Extension of <see cref="ComponentBase"/> that includes a task pool.
        /// </summary>
        public abstract class ParallelComponentBase : ComponentBase
        {
            private readonly TaskPool taskPool;

            protected ParallelComponentBase(bool runOnServer, bool runOnClient, ModBase parent) : base(runOnServer, runOnClient, parent)
            {
                taskPool = new TaskPool(ErrorCallback);
            }

            /// <summary>
            /// Called in the event an exception occurs in one of the component's tasks with a list of <see cref="KnownException"/>s
            /// and a single aggregate exception of all other exceptions.
            /// </summary>
            protected abstract void ErrorCallback(List<KnownException> knownExceptions, AggregateException aggregate);

            /// <summary>
            /// Enqueues an action to run in parallel. Not thread safe; must be called from the main thread.
            /// </summary>
            protected void EnqueueTask(Action action) =>
                taskPool.EnqueueTask(action);

            /// <summary>
            /// Enqueues an action to run on the main thread. Meant to be used by threads other than the main.
            /// </summary>
            protected void EnqueueAction(Action action) =>
                taskPool.EnqueueAction(action);
        }
    }
}
