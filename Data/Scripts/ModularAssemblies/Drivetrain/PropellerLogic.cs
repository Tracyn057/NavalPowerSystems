using Jakaria.API;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.ModAPI;
using System;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace NavalPowerSystems.Drivetrain
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), false,
            "NPSDrivetrainScrew3b3m"
    )]
    internal class PropellerLogic : MyGameLogicComponent
    {
        private IMyTerminalBlock _propeller;
        private IMyCubeBlock _myPropeller;
        private PropellerStats _propellerStats;
        public float _inputMW { get; set; }
        private float _outputMW = 0f;
        private float _inertia = 0f;


        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _propeller = (IMyTerminalBlock)Entity;
            _myPropeller = (MyCubeBlock)Entity;
            _propellerStats = Config.PropellerSettings[_propeller.BlockDefinition.SubtypeName];
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            _propeller.AppendingCustomInfo += AppendCustomInfo;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateBeforeSimulation10()
        {
            UpdatePower();

            _propeller.RefreshCustomInfo();
        }

        public override void UpdateBeforeSimulation()
        {
            SetSpool();
            ApplyForce();
        }

        private void UpdatePower()
        {
            if (Math.Abs(_inputMW) < 0.001f) 
            { 
                _outputMW = 0f;
                return; 
            }

            _outputMW = (_inputMW * 1000000f) * Config.mnPerMW;
        }

        private void SetSpool()
        {
            float spoolStep = 1f / (_propellerStats.SpoolTime * 60f);

            if (Math.Abs(_inputMW) > 0.01f)
                _inertia = Math.Min(_inertia + spoolStep, 1f);
            else
                _inertia = Math.Max(_inertia - spoolStep, 0f);
        }

        private void ApplyForce()
        {
            
            var grid = _myPropeller.CubeGrid as MyCubeGrid;

            if (grid.IsPreview || grid.Physics == null || !grid.Physics.Enabled || grid.Physics.IsStatic)
                return;

            float cubicFactor = _inertia * _inertia * _inertia;
            Vector3D velocity = grid.Physics.LinearVelocity;
            double speed = velocity.Length();
            double efficiency = Math.Max(0.5, 1.0 - (speed / 40.0));
            float adjustedThrust = _outputMW * cubicFactor * (float)efficiency;

            if (adjustedThrust >100)
            {
                Vector3D thrustVector = _myPropeller.WorldMatrix.Backward * adjustedThrust;
                var BlockPos = _myPropeller.PositionComp.GetPosition();
                grid.Physics.AddForce(
                MyPhysicsForceType.APPLY_WORLD_FORCE,
                thrustVector,
                BlockPos,
                null
                );
            }
        }

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.AppendLine($"Output: {(_outputMW / 1000000):F4} MN");
        }

        public override void OnRemovedFromScene()
        {
            if (_propeller != null) _propeller.AppendingCustomInfo -= AppendCustomInfo;
        }
    }
}
