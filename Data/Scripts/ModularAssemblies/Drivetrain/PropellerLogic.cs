using Jakaria.API;
using Sandbox.ModAPI;
using System;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
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
        private PropellerStats _propellerStats;
        public float _inputMW { get; set; }
        private float _outputMW = 0f;


        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _propeller = (IMyTerminalBlock)Entity;
            _propellerStats = Config.PropellerSettings[_propeller.BlockDefinition.SubtypeName];
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            _propeller.AppendingCustomInfo += AppendCustomInfo;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        public override void UpdateBeforeSimulation10()
        {
                UpdatePower();
                SetSpool();
                ApplyForce();
                _propeller.RefreshCustomInfo();
        }

        private void UpdatePower()
        {
            if (Math.Abs(_inputMW) < 0.001f) 
            { 
                _outputMW = 0f;
                return; 
            }

            float submergence = WaterModAPI.Entity_PercentUnderwater((MyEntity)_propeller);

            _outputMW = (_inputMW * 1000000f) * Config.mnPerMW * submergence;
        }

        private void SetSpool()
        {
            float spoolStep = 1f / (_propellerStats.SpoolTime * 6f);
            float inertia = 0f;

            if (inertia > 0.8f)
                spoolStep *= 2f;

            if (Math.Abs(_inputMW) > 0.1f)
                inertia = Math.Min(inertia + spoolStep, 1f);
            else
                inertia = Math.Max(inertia - spoolStep, 0f);

            float cubicFactor = inertia * inertia * inertia;

            _outputMW *= cubicFactor;

            float noise = 1f + MyUtils.GetRandomFloat(-Config.throttleVariance, Config.throttleVariance);
            
            _outputMW *= noise;
        }

        private void ApplyForce()
        {
            if (_propeller?.Physics == null) return;

            Vector3D forceDirection = Entity.WorldMatrix.Backward;
            Vector3D totalForceVector = forceDirection * _outputMW;

            float stabilityFactor = 0.7f; 
            Vector3D stableForce = totalForceVector * stabilityFactor;
            Vector3D torqueForce = totalForceVector * (1.0f - stabilityFactor);

            _propeller.Physics.AddForce(
                MyPhysicsForceType.APPLY_WORLD_FORCE, 
                stableForce,
                _propeller.Physics.CenterOfMassWorld, 
                null
            );

            _propeller.Physics.AddForce(
                MyPhysicsForceType.APPLY_WORLD_FORCE, 
                torqueForce, 
                Entity.WorldMatrix.Translation, 
                null
            );
        }

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.AppendLine($"Output: {_outputMW}");
        }

        public override void OnRemovedFromScene()
        {
            if (_propeller != null) _propeller.AppendingCustomInfo -= AppendCustomInfo;
        }
    }
}
