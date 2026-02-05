using NavalPowerSystems.Communication;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace NavalPowerSystems.Drivetrain
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), false,
            "NPSDrivetrainMRG"
    )]
    internal class GearboxLogic : MyGameLogicComponent
    {

        internal static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        private int _assemblyId = -1;
        private IMyTerminalBlock _gearbox;
        private bool _isComplete;
        private bool _isReverse;
        private bool _needsRefresh = true;
        private bool _controlsInit = false;
        private int _outputCount;
        private float _inputMW;
        private float _outputMW;
        private List<IMyTerminalBlock> _clutches = new List<IMyTerminalBlock>();
        private List<IMyTerminalBlock> _propellers = new List<IMyTerminalBlock>();
        
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _gearbox = (IMyTerminalBlock)Entity;

            if (_gearbox == null) return;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            TriggerRefresh(_gearbox.SlimBlock);
            _gearbox.AppendingCustomInfo += AppendCustomInfo;
            _assemblyId = ModularApi.GetContainingAssembly(_gearbox, "Drivetrain_Definition");

            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateBeforeSimulation10()
        {
            GetPower();
            SetPower();
            _gearbox.RefreshCustomInfo();
        }

        public override void UpdateBeforeSimulation100()
        {
            if (_needsRefresh)
            {
                GetChildren();
                _needsRefresh = false;
            }
            UpdateIsEngaged();
        }

        private void GetChildren()
        {
            _clutches.Clear();
            _propellers.Clear();
            _outputCount = 0;

            var assemblyParts = ModularApi.GetMemberParts(_assemblyId);


            foreach (var part in assemblyParts)
            {
                var terminalBlock = part as IMyTerminalBlock;
                if (terminalBlock == null) continue;
                var subtype = part.BlockDefinition.SubtypeName;

                if (subtype == "NPSDrivetrainClutch" || subtype == "NPSDrivetrainDirectDrive")
                {
                    _clutches.Add(part as IMyTerminalBlock);
                }
                else if (Config.PropellerSubtypes.Contains(subtype))
                {
                    _propellers.Add(part as IMyTerminalBlock);
                }
                _outputCount = _propellers.Count;
            }

            _isComplete = (_clutches.Count > 0 && _propellers.Count > 0);
        }

        private void UpdateIsEngaged()
        {
            if (_clutches.Count == 0) return;

            float hiRequestedThrottle = 0f;
            float hiCurrentThrottle = 0f;

            if (_clutches.Count == 1)
            {
                foreach (var clutch in _clutches)
                {
                    var logic = clutch.GameLogic?.GetAs<ClutchLogic>();
                    logic._isEngaged = true;
                }
            }

            foreach (var clutch in _clutches)
            {
                var logic = clutch.GameLogic?.GetAs<ClutchLogic>();
                if (logic == null) continue;

                if (logic._requestedThrottle > hiRequestedThrottle) 
                    hiRequestedThrottle = logic._requestedThrottle;
            
                if (logic._currentThrottle > hiCurrentThrottle) 
                    hiCurrentThrottle = logic._currentThrottle;
            }

            float diff = Math.Abs(hiRequestedThrottle - hiCurrentThrottle);

            foreach (var clutch in _clutches)
            {
                var logic = clutch.GameLogic?.GetAs<ClutchLogic>();
                if (logic == null) continue;

                if (hiRequestedThrottle < 0.01f)
                {
                    logic._isEngaged = false;
                    continue;
                }

                if (logic._isEngaged)
                {
                    if (diff > 0.08f) logic._isEngaged = false;
                }
                else
                {
                    if (diff < 0.04f) logic._isEngaged = true;
                }
            }
        }
        
        private void TriggerRefresh(IMySlimBlock block)
        {
            _needsRefresh = true;
        }

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.AppendLine($"Clutches: {_clutches.Count}");
            sb.AppendLine($"Propellers: {_propellers.Count}");
            sb.AppendLine($"Input: {_inputMW:F2} MW");
        }

        private void GetPower()
        {
            if (_clutches == null) return;
            _inputMW = 0f;

            foreach (var clutch in _clutches)
            {
                var logic = clutch.GameLogic?.GetAs<ClutchLogic>();
                if (logic == null) continue;
                _inputMW += logic._outputMW;
            }
        }

        private void SetPower()
        {
            if (_outputCount <= 0 || _propellers == null) return;
            _outputMW = _inputMW / _outputCount;
            if (_isReverse)
                _outputMW *= -0.4f;

            foreach (var prop in _propellers)
            {
                var logic = prop.GameLogic?.GetAs<PropellerLogic>();
                if (logic == null) continue;
                logic._inputMW = _outputMW;
            }
        }

        public override void OnRemovedFromScene()
        {
            if (_gearbox != null) _gearbox.AppendingCustomInfo -= AppendCustomInfo;
        }

    }
}
