using NavalPowerSystems.Communication;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

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
        public bool _needsRefresh { get; set; }
        private static bool _controlsInit = false;
        private int _outputCount;
        private float _inputMW;
        private float _outputMW;
        //private float _outputMWDebug;
        private List<IMyTerminalBlock> _clutches = new List<IMyTerminalBlock>();
        private List<IMyTerminalBlock> _propellers = new List<IMyTerminalBlock>();
        private static readonly List<GearboxLogic> _activeGearboxes = new List<GearboxLogic>();

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _gearbox = (IMyTerminalBlock)Entity;

            if (_gearbox == null) return;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            _gearbox.AppendingCustomInfo += AppendCustomInfo;
            

            if (!_controlsInit)
            {
                CreateControls();
                _controlsInit = true;
            }

            _needsRefresh = true;

            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateBeforeSimulation()
        {
            ApplyDrag();
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
            }
            UpdateIsEngaged();
        }

        //Validates the state of the drivetrain
        private void GetChildren()
        {
            _assemblyId = ModularApi.GetContainingAssembly(_gearbox, "Drivetrain_Definition");
            if (_assemblyId == -1)
            {
                _clutches.Clear();
                _propellers.Clear();
                _isComplete = false;
                return;
            }
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
            _needsRefresh = false;
            _isComplete = _clutches.Count > 0 && _propellers.Count > 0;
        }

        //Checks clutch state and determines if the gearbox should be engaged based on clutch throttle and other clutches in the system, with some hysteresis to prevent rapid toggling
        private void UpdateIsEngaged()
        {
            if (_clutches.Count == 0) return;

            float shaftSpeed = 0f;
            foreach (var clutch in _clutches)
            {
                var logic = clutch.GameLogic?.GetAs<ClutchLogic>();
                if (logic != null && logic._isEngaged)
                {
                    if (logic._currentThrottle > shaftSpeed)
                        shaftSpeed = logic._currentThrottle;
                }
            }

            foreach (var clutch in _clutches)
            {
                var logic = clutch.GameLogic?.GetAs<ClutchLogic>();
                if (logic == null) continue;

                if (logic._currentThrottle < 0.01f)
                {
                    logic._isEngaged = false;
                    continue;
                }

                if (!logic._isEngaged)
                {
                    if (shaftSpeed < 0.05f || logic._currentThrottle >= shaftSpeed - 0.01f)
                    {
                        logic._isEngaged = true;
                    }
                }
                else
                {
                    if (logic._currentThrottle < shaftSpeed - 0.05f)
                    {
                        logic._isEngaged = false;
                    }
                }
            }
        }

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.AppendLine($"Clutches: {_clutches.Count}");
            sb.AppendLine($"Propellers: {_propellers.Count}");
            sb.AppendLine($"Input: {_inputMW:F2} MW");
            //sb.AppendLine($"Debug Drag Output: {_outputMWDebug:F2}");
        }

        //Retrieve power information from the clutches
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

        //Send equal power to each propeller, with a reduction if in reverse
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

        public void TriggerRefresh()
        {
            _needsRefresh = true;
        }


        private void CreateControls()
        {
            if (_controlsInit) return;
            _controlsInit = true;

            {
                var gearboxSync = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>("GearboxSync");
                gearboxSync.Title = MyStringId.GetOrCompute("Reset Control");
                gearboxSync.Tooltip = MyStringId.GetOrCompute("Trigger detection.");
                gearboxSync.Action = block =>
                {
                    var logic = block.GameLogic?.GetAs<GearboxLogic>();
                    logic?.TriggerRefresh();
                    _gearbox.RefreshCustomInfo();
                };
                gearboxSync.Visible = block =>
                    block.BlockDefinition.SubtypeName.Contains("NPSDrivetrainMRG");
                gearboxSync.SupportsMultipleBlocks = true;
                gearboxSync.Enabled = block => block.BlockDefinition.SubtypeName.Contains("NPSDrivetrainMRG");

                MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(gearboxSync);
            }
        }

        //Secret drag application to simulate water resistance on the vessel
        private void ApplyDrag()
        {
            var block = Entity as MyCubeBlock;
            if (block == null) return;

            var grid = _gearbox.CubeGrid as MyCubeGrid;
            if (grid.IsPreview || grid.Physics == null || !grid.Physics.Enabled || grid.Physics.IsStatic)
                return;

            var leader = _activeGearboxes.FirstOrDefault(g => (g.Entity as MyCubeBlock)?.CubeGrid == grid);
            if (leader != this) return;

            Vector3D velocity = grid.Physics.LinearVelocity;
            double speed = velocity.Length();
            if (speed < 0.1) return;

            Vector3D dragDirection = -Vector3D.Normalize(velocity);

            float dragQuadCoeff = 0.00045f;
            float dragLinearCoeff = 0.0003f;
            float gridMass = (float)grid.Physics.Mass * 1.15f;

            //Quadratic drag formula
            double quadDrag = 0.5 * dragQuadCoeff * speed * speed * gridMass;
            //Add a linear drag component
            double linearDrag = speed * dragLinearCoeff * gridMass;
            //Total drag force
            double forceMagnitude = quadDrag + linearDrag;
            //Cap the drag force to prevent excessive deceleration
            double maxDrag = speed * grid.Physics.Mass / (1f / 60f);
            //If the calculated drag exceeds the max, limit it
            Vector3D finalForce = dragDirection * forceMagnitude;
            if (forceMagnitude > maxDrag) finalForce = dragDirection * maxDrag;

            double totalForceNewtons = finalForce.Length();

            grid.Physics.AddForce(
                MyPhysicsForceType.APPLY_WORLD_FORCE,
                finalForce,
                grid.Physics.CenterOfMassWorld,
                null
                );
        }

        public override void OnAddedToScene()
        {
            base.OnAddedToScene();
            if (!_activeGearboxes.Contains(this))
                _activeGearboxes.Add(this);
        }

        public override void OnRemovedFromScene()
        {
            if (_gearbox != null) _gearbox.AppendingCustomInfo -= AppendCustomInfo;
            base.OnRemovedFromScene();
            _activeGearboxes.Remove(this);
        }

    }
}
