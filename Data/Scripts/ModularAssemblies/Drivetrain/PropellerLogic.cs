using Sandbox.ModAPI;
using System;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRageMath;
using static Sandbox.Game.Components.MyRenderComponentThrust;

namespace NavalPowerSystems.Drivetrain
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_FunctionalBlock), false,
    "NPS_Propeller_4m3b",
    "NPS_Propeller_4m4b",
    "NPS_Propeller_4m5b"
    )]
    public class PropellerLogic : DrivetrainPart<IMyFunctionalBlock>
    {
        private static PropellerLogic GetLogic(IMyTerminalBlock terminalBlock) => terminalBlock?.GameLogic?.GetAs<PropellerLogic>();
        private PropellerStats MyStats => Drivetrain_Config.PropellerSettings[SubtypeName];
        private MyEntitySubpart MySubpart;
        private Matrix MySubpartMatrix;
        private IMyCubeGrid MyGrid => Entity.Parent as IMyCubeGrid;
        public override DrivetrainRole GetRole() => DrivetrainRole.Consumer;
        public override double GetLoad() => Load_Out;
        private bool ControlsInitialized = false;

        #region Operational Variables
        private float CurrentAngle;
        MySync<float, SyncDirection.BothWays> Terminal_PitchRatio;
        private float AE;
        private float PD;
        private float KQ_0;
        #endregion

        #region Updates
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            Entity.TryGetSubpart("Propeller", out MySubpart);
            if (MySubpart == null)
                ModularApi.Log($"{SubtypeName} subpart is null.");
            else
                MySubpartMatrix = MySubpart.PositionComp.LocalMatrixRef;

            Block.AppendingCustomInfo += AppendCustomInfo;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            if (MyStats.Blades == 3)
            {
                AE = 0.65f;
                PD = 1.0f;
                KQ_0 = 0.055f;
            }
            else if (MyStats.Blades == 4)
            {
                AE = 0.75f;
                PD = 1.1f;
                KQ_0 = 0.065f;
            }
            else if (MyStats.Blades == 5)
            {
                AE = 1.05f;
                PD = 1.2f;
                KQ_0 = 0.08f;
            }
            

            Terminal_PitchRatio.SetLocalValue(PD);
            Terminal_PitchRatio.ValueChanged += Terminal_PitchRatio_ValueChanged;

            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();

            var diameter = MyStats.Diameter;
            double RPS = RPM_In / 60;
            double velocity = MyGrid.LinearVelocity.Length();
            double j = (RPS > 0.01) ? velocity / (RPS * diameter) : 0;
            double kq = GetTorqueCoefficient(j);
            double kt = GetThrustCoefficient(j);

            //Calculate Load
            double hydroLoad = kq * 1024 * Math.Pow(RPS, 2) * Math.Pow(diameter, 5);
            double linearLoad = AE * RPS;
            double staticLoad = 0.05 * 0.25 * MyStats.Inertia;
            Load_Out = hydroLoad + linearLoad + staticLoad;

            //Calculate thrust
            Torque_Out = kt * 1024 * Math.Pow(RPS, 2) * Math.Pow(diameter, 4);

            //Apply thrust
            if (Math.Abs(Torque_Out) > 100)
            {
                Vector3D thrustVector = Block.WorldMatrix.Backward * Torque_Out;
                var thrustPos = Block.PositionComp.WorldVolume.Center;
                MyGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, thrustVector, thrustPos, null);
            }

            //Animate
            if (MySubpart != null && RPM_In != 0)
            {
                float degreesPerTick = (float)RPM_In * 360f / 3600f; // convert RPM → degrees/tick at 60 Hz
                CurrentAngle += degreesPerTick;
                CurrentAngle %= 360f;

                Matrix rotationMatrix = Matrix.CreateRotationZ(MathHelper.ToRadians(-CurrentAngle));
                Matrix finalMatrix = rotationMatrix * MySubpartMatrix;
                MySubpart.PositionComp.SetLocalMatrix(ref finalMatrix);
            }
        }
        #endregion

        #region Operation and Physics
        private double GetTorqueCoefficient(double j)
        {
            double kq = KQ_0 * (1 - (j / PD));
            return Math.Max(kq, 0.001);
        }

        private double GetThrustCoefficient(double j)
        {
            double kt_0 = 0.1 * PD;
            return Math.Max(kt_0 * (1 - (j / PD)), 0);
        }
        #endregion
    }
}
