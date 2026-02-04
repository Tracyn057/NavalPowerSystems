using NavalPowerSystems.DieselEngines;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace NavalPowerSystems.Drivetrain
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), false,
            "NPSDrivetrainClutch",
            "NPSDrivetrainDirectDrive"
    )]
    internal class ClutchLogic : MyGameLogicComponent
    {
        private IMyTerminalBlock _clutch;
        private IMyGasTank _engine;
        private NavalEngineLogic _engineLogic;
        private bool _isDirectDrive = false;
        private long _engineId = -1;
        private bool _needsRefresh = true;
        private float _inputMW = 0;
        public float _currentThrottle { get; private set; }
        public float _requestedThrottle { get; private set; }
        public float _outputMW { get; private set; }
        public bool _isEngaged { get; set; }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _clutch = (IMyTerminalBlock)Entity;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            TriggerEngineSearch(_clutch.SlimBlock);
            _clutch.CubeGrid.OnBlockAdded += TriggerEngineSearch;
            _clutch.CubeGrid.OnBlockRemoved += TriggerEngineSearch;

            if (_clutch.BlockDefinition.SubtypeName == "NPSDrivetrainDirectDrive")
            {
                _isDirectDrive = true;
            }
            _clutch.AppendingCustomInfo += AppendCustomInfo;

            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        public override void UpdateBeforeSimulation10()
        {
            if (_needsRefresh)
            {
                UpdateEngine();
            }
            UpdatePower();

            _clutch.RefreshCustomInfo();
        }
            
        private void UpdatePower()
        {
            if (_engineLogic == null)
            {
                _outputMW = 0;
                _currentThrottle = 0;
                _requestedThrottle = 0;
                TriggerEngineSearch(_clutch.SlimBlock);
                return;
            }

            _inputMW = _engineLogic._currentOutputMW;
            _currentThrottle = _engineLogic._currentThrottle;
            _requestedThrottle = _engineLogic._requestedThrottle;

            if (_clutch.IsWorking && (_isEngaged || _isDirectDrive))
            {
                _outputMW = _inputMW;
            }
            else
            {
                _outputMW = 0;
            }
        }

        private long GetEngineId()
        {
            List<IMySlimBlock> engines = new List<IMySlimBlock>();
            _clutch.SlimBlock.GetNeighbours(engines);

            foreach (var engine in engines)
            {
                if (engine.FatBlock == null) continue;
                if (Config.EngineSubtypes.Contains(engine.FatBlock.BlockDefinition.SubtypeName))
                {
                    _engine = (IMyGasTank)engine.FatBlock;
                    return engine.FatBlock.EntityId;
                }
            }
            return -1;
        }

        private void TriggerEngineSearch(IMySlimBlock block)
        {
            _needsRefresh = true;
        }

        private void UpdateEngine()
        {
            _engineId = GetEngineId();
            if (_engineId != -1)
            {
                _engineLogic = _engine.GameLogic.GetAs<NavalEngineLogic>();
                _needsRefresh = false;
            }
            else
            {
                _needsRefresh = true;
                _engineLogic = null;
            }
        }

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.AppendLine($"Attached Engine: {_engineId}");
            sb.AppendLine($"Input: {_inputMW:F2} MW");
            sb.AppendLine($"Output: {_outputMW:F2} MW");
            sb.AppendLine($"Is Engaged: {_isEngaged}");
        }

    }
}
