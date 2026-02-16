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
        private MyEntitySubpart _propellerSubpart;
        public float _inputMW { get; set; }
        private float _outputMW = 0f;
        private float _rpmRatio = 0f;
        private float _inertia = 0f;
        public volatile float _currentAngle { get; private set; } = 0f;
        private Matrix _initialMatrix;
        private const float _maxRpm = 200;


        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _propeller = (IMyTerminalBlock)Entity;
            _myPropeller = (MyCubeBlock)Entity;
            _propellerStats = Config.PropellerSettings[_propeller.BlockDefinition.SubtypeName];
            Entity.TryGetSubpart("Propeller", out _propellerSubpart);

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            _propeller.AppendingCustomInfo += AppendCustomInfo;

            if (_propellerSubpart != null)
            {
                _initialMatrix = _propellerSubpart.PositionComp.LocalMatrix;
        
                _propellerSubpart.Render.AddRuntimeUpdate(MyRenderProxy.ObjectType.Entity, (renderEntity) => 
                {
                    renderEntity.LocalMatrix = Matrix.CreateRotationY(_currentAngle) * _initialMatrix;
                });
            }

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

            _outputMW = _inputMW;
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
            float outputMN = _outputMW * Config.mnPerMW;
            var grid = _myPropeller.CubeGrid as MyCubeGrid;
            float limit = _propellerStats.MaxMW;

            if (grid.IsPreview || grid.Physics == null || !grid.Physics.Enabled || grid.Physics.IsStatic)
                return;

            float cubicFactor = _inertia * _inertia * _inertia;
            Vector3D velocity = grid.Physics.LinearVelocity;
            double speed = velocity.Length();
            double efficiency = Math.Max(0.5, 1.0 - (speed / 40.0));
            float adjustedThrust = outputMN * cubicFactor * (float)efficiency;
            double finalThrust = limit * Math.Tanh(adjustedThrust / limit);

            double waste = adjustedThrust - finalThrust;
            if (waste > (limit * 0.2)) // If wasting more than 20% of max capacity
            {
                // Apply pitting damage to the propeller block
                // Trigger cavitation sound effects/particles

                float cavitationDmg = (float)waste * Config.cavitationDmgMult / 60f;

                if (cavitationDmg > 0)
                {
                    _propeller.SlimBlock.DoDamage(cavitationDmg, MyDamageType.Deformation, true);
                }
            }

            _rpmRatio = MathHelper.Clamp(finalThrust / (_maxRpm * Config.mnPerMW), 0f, 1.5f);

            if (_propellerSubpart != null)
            {
                _currentAngle += _maxRpm * _rpmRatio / 60 * 6f; // 6f is 360 degrees per second at max RPM
                _currentAngle %= 360f; // Keep angle within 0-360 degrees
            }

            finalThrust *= 1000000f; // Convert MN to N for physics application

            if (finalThrust >100)
            {
                Vector3D thrustVector = _myPropeller.WorldMatrix.Backward * (float)finalThrust;
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
            sb.AppendLine($"Output: {_outputMW:F4} MN");
        }

        public override void OnRemovedFromScene()
        {
            if (_propeller != null) _propeller.AppendingCustomInfo -= AppendCustomInfo;
        }
    }
}
