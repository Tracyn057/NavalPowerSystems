using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI.Network;
using VRage.ModAPI;
using VRage.Network;
using VRage.ObjectBuilders;
using VRage.Sync;

namespace NavalPowerSystems.Drivetrain
{
    public abstract class NavalEngineLogicBase : MyGameLogicComponent, IMyEventProxy
    {
        protected IMyFunctionalBlock _engineBlock;
        protected EngineStats _engineStats;
        public MySync<float, SyncDirection.BothWays> RequestedThrottleSync;
        public MySync<int, SyncDirection.BothWays> SelectedThrottleIndexSync;
        public bool _isEngaged { get; set; } = false;
        public float _currentOutputMW { get; protected set; } = 0f;
        public float _currentThrottle { get; protected set; } = 0f;
        
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _engineBlock = (IMyFunctionalBlock)Entity;
            _engineStats = Config.EngineSettings[_engineBlock.BlockDefinition.SubtypeName];
            if (RequestedThrottleSync != null)
                RequestedThrottleSync.ValueChanged += obj => OnRequestedThrottleChanged(obj.Value);
            if (SelectedThrottleIndexSync != null)
                SelectedThrottleIndexSync.ValueChanged += obj => OnSelectedThrottleIndexChanged(obj.Value);

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;

            SetupEngineReferences();
        }        

        //Run engine updates
        protected abstract void SetupEngineReferences();
        protected virtual void OnRequestedThrottleChanged(float value) { _engineBlock.RefreshCustomInfo(); } // Optional for children
        protected virtual void OnSelectedThrottleIndexChanged(int index) { _engineBlock.RefreshCustomInfo(); } // Optional for children
        public override void UpdateOnceBeforeFrame() { EngineInit(); }
        public override void UpdateBeforeSimulation() { EngineUpdate(); }
        public override void UpdateBeforeSimulation10() { EngineUpdate10(); }
        public override void UpdateBeforeSimulation100() { EngineUpdate100(); }

        //Base functions for engine logic
        protected virtual void EngineInit() { }    // Optional for children
        protected virtual void EngineUpdate() { }  // Optional for children (if you want to use UpdateBeforeSimulation instead of UpdateBeforeSimulation10)
        protected abstract void EngineUpdate10();  // Mandatory (Core logic)
        protected virtual void EngineUpdate100() { } // Optional (Fuel/Health/UI)
    }
}