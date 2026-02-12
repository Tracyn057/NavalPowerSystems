using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace NavalPowerSystems.Drivetrain
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), false,
            ""
    )]
    internal class RudderLogic : MyGameLogicComponent
    {
        private IMyTerminalBlock _rudder;
        private IMyCubeBlock _myRudder;
        private GearboxLogic _gearboxLogic;

        private bool _isAutoCenter = true;

        private float _currentAngle = 0f;
        private float _targetAngle = 0f;
        private float _currentThrottle = 0f;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _rudder = (IMyTerminalBlock)Entity;
            _myRudder = (MyCubeBlock)Entity;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            _rudder.AppendingCustomInfo += AppendCustomInfo;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateBeforeSimulation10()
        {
            _rudder.RefreshCustomInfo();
        }

        public override void UpdateBeforeSimulation()
        {
            GetAngle();
            ApplyForce();
        }

        private void GetAngle()
        {
            
        }

        private void ApplyForce()
        {
            var grid = _myRudder.CubeGrid as MyCubeGrid;

            if (grid.IsPreview || grid.Physics == null || !grid.Physics.Enabled || grid.Physics.IsStatic)
                return;

            Vector3D velocity = grid.Physics.LinearVelocity;
            double speed = velocity.Length();

            finalThrust *= 1000000f; // Convert MN to N for physics application

            if (finalThrust >100)
            {
                Vector3D thrustVector = _myRudder.WorldMatrix.Right * (float)finalThrust;
                var BlockPos = _myRudder.PositionComp.GetPosition();
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
            sb.AppendLine($"Angle: {_currentAngle:F4} degrees");
        }

        public override void OnRemovedFromScene()
        {
            if (_rudder != null) _rudder.AppendingCustomInfo -= AppendCustomInfo;
        }

    }
}
