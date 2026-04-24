using ProtoBuf;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Stats;
using VRage.Sync;
using VRage.Utils;
using VRageMath;

namespace NavalPowerSystems.Drivetrain.Consumers
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_FunctionalBlock), false,
    "NPS_Propeller_4m3b",
    "NPS_Propeller_4m4b",
    "NPS_Propeller_4m5b"
    )]
    public class PropellerLogic : DrivetrainPart<IMyFunctionalBlock>
    {
        private static PropellerLogic GetLogic(IMyTerminalBlock terminalBlock) => terminalBlock?.GameLogic?.GetAs<PropellerLogic>();
        public PropellerStats MyStats => Drivetrain_Config.PropellerSettings[SubtypeName];
        private MyEntitySubpart MySubpart;
        private Matrix MySubpartMatrix;
        private IMyCubeGrid MyGrid => Entity.Parent as IMyCubeGrid;
        public override DrivetrainRole GetRole() => DrivetrainRole.Consumer;
        public override double GetLoad() => Load_Out;
        private static bool ControlsInitialized = false;
        private PropellerSettings Settings;

        #region Operational Variables
        private float CurrentAngle;
        MySync<float, SyncDirection.BothWays> Terminal_PD;
        private float AE;
        private float PD;
        private float KQ_0;
        #endregion

        #region Updates
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            Block.AppendingCustomInfo += AppendCustomInfo;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            Entity.TryGetSubpart("Propeller", out MySubpart);
            if (MySubpart == null)
                ModularApi.Log($"{SubtypeName} subpart is null.");
            else
                MySubpartMatrix = MySubpart.PositionComp.LocalMatrixRef;

            LoadSettings();
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
            SaveSettings();

            if (!ControlsInitialized)
            {
                CreateControls<IMyFunctionalBlock>();
                CreateActions<IMyFunctionalBlock>();
                ControlsInitialized = true;
            }

            Terminal_PD.SetLocalValue(PD);
            Terminal_PD.ValueChanged += Terminal_PitchRatio_ValueChanged;

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
            if (MyStats.IsCRP)
            {
                //CRP Animation
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

        #region UI and Controls
        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder info)
        {
            info.AppendLine($"Current Torque: {Torque_Out:0.00}");
            info.AppendLine($"Current RPM: {RPM_In:0.00}");
        }

        private static void UpdateControls()
        {
            List<IMyTerminalControl> controls;

            MyAPIGateway.TerminalControls.GetControls<IMyFunctionalBlock>(out controls);

            foreach (IMyTerminalControl control in controls)
            {
                switch (control.Id)
                {
                    case "NPS_Propeller_TerminalControl_PitchRatio":
                        {
                            control.UpdateVisual();
                            break;
                        }
                }
            }
        }

        static void CreateControls<IMyFunctionalBlock>()
        {
            if (ControlsInitialized) return;
        }

        static void CreateActions<IMyFunctionalBlock>()
        {
            if (ControlsInitialized) return;
        }

        private void Terminal_PitchRatio_ValueChanged(MySync<float, SyncDirection.BothWays> obj)
        {
            PD = obj.Value;
            Settings.PitchRatio = obj.Value;
            UpdateControls();
            SaveSettings();
        }

        static bool Control_Visible(IMyTerminalBlock block)
        {
            var logic = GetLogic(block);
            if (logic != null && logic.MyStats.IsCRP)
                return true;

            return false;
        }

        static float Control_Terminal_PitchRatio_Getter(IMyTerminalBlock block)
        {
            var logic = GetLogic(block);
            return logic == null ? 0.01f : logic.Terminal_PD;
        }

        static void Control_Terminal_PitchRatio_Setter(IMyTerminalBlock block, float value)
        {
            var logic = GetLogic(block);
            if (logic != null)
                logic.Terminal_PD.ValidateAndSet(value);
        }

        static void Control_Terminal_PitchRatio_Writer(IMyTerminalBlock block, StringBuilder writer)
        {
            var logic = GetLogic(block);
            if (logic != null)
                writer.Append((int)(logic.Terminal_PD));
        }
        #endregion

        #region Settings Save and Load
        private void SetDefaultSettings()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            Settings.PitchRatio = 1.1f;
        }

        internal virtual bool LoadSettings()
        {
            if (Settings == null)
                Settings = new PropellerSettings();

            if (Block.Storage == null)
            {
                SetDefaultSettings();
                return false;
            }

            string rawData;
            if (!Block.Storage.TryGetValue(SettingsGuid, out rawData))
            {
                SetDefaultSettings();
                return false;
            }

            try
            {
                var loadedSettings =
                    MyAPIGateway.Utilities.SerializeFromBinary<PropellerSettings>(Convert.FromBase64String(rawData));

                if (loadedSettings != null)
                {
                    Settings.PitchRatio = loadedSettings.PitchRatio;

                    return true;
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole("Exception in loading Propeller settings: " + e);
                MyAPIGateway.Utilities.ShowMessage("Naval Power Systems", "Exception in loading Propeller settings: " + e);
                ModularApi.Log("Exception in loading Propeller settings: " + e);
            }

            return false;
        }

        private void SaveSettings()
        {
            if (Block == null || Settings == null)
            {
                ModularApi.Log($"Block or Settings null on Propeller.");
                return; // called too soon or after it was already closed, ignore
            }

            if (MyAPIGateway.Utilities == null)
                throw new NullReferenceException(
                    $"MyAPIGateway.Utilities == null; entId={Entity?.EntityId}; Test log 2");

            if (Block.Storage == null)
                Block.Storage = new MyModStorageComponent();

            Block.Storage.SetValue(SettingsGuid,
                Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(Settings)));
        }
        #endregion
    }

    [ProtoContract(UseProtoMembersOnly = true)]
    public class PropellerSettings
    {
        [ProtoMember(1)]
        public float PitchRatio;
    }
}
