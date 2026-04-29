using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Localization;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI.Network;
using VRage.ModAPI;
using VRage.Sync;
using VRage.Utils;
using VRageMath;

namespace NavalPowerSystems.Drivetrain.Producers.Combustion
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_FunctionalBlock), false,
            "NPS_Turbine_LM2500",
            "NPS_Turbine_LM2500Plus",
            "NPS_Turbine_LM2500PlusG4"
    )]
    public class GasTurbineLogic : DrivetrainPart<IMyFunctionalBlock>
    {
        private GasTurbineSettings MyStats => Config_GasTurbine.GasTurbineStats[SubtypeName];
        private MyEntitySubpart MySubpart_Signage;
        public GasTurbineSaveSettings Settings;
        public override DrivetrainRole GetRole() => DrivetrainRole.Producer;
        public override float GetRatio() => (float)GearRatio;
        public override float GetEngagement() => (float)ClutchRatio;
        private static GasTurbineLogic GetLogic(IMyTerminalBlock terminalBlock) => terminalBlock?.GameLogic?.GetAs<GasTurbineLogic>();

        #region Sync, Terminal and Settings Variables
        private string CurrentStateLabel;
        private static bool ControlsInitialized = false;
        MySync<bool, SyncDirection.BothWays> Terminal_RequestEngineOn;
        private bool RequestEngineOn;
        MySync<EngineState, SyncDirection.BothWays> Terminal_EngineState;
        private enum EngineState { Off, Starting, Running, Stopping }
        private EngineState CurrentState = EngineState.Off;
        MySync<float, SyncDirection.BothWays> Terminal_Throttle;
        public float Throttle = 0f;
        MySync<bool, SyncDirection.BothWays> Terminal_KeepThrottle;
        private bool KeepThrottle = false;
        MySync<bool, SyncDirection.BothWays> Terminal_ClutchLockout;
        private bool ClutchLockout = false;
        MySync<double, SyncDirection.BothWays> Terminal_ClutchRatio;
        private double ClutchRatio = 0;
        MySync<bool, SyncDirection.FromServer> Sync_HasFuel;
        private bool HasFuel = false;
        #endregion

        #region Sound Effects
        private const string TurbineSoundId = "NPS_LargeGasTurbine";
        private const string TurbineSoundFarId = "";
        private const float SoundCrossFadeDist = 50f;
        private const float SoundFarMaxVolDist = 250f;
        private static MySoundPair Audio;
        private static MySoundPair AudioFar;
        private MyEntity3DSoundEmitter AudioEmitter;
        private MyEntity3DSoundEmitter AudioFarEmitter;
        #endregion

        #region Turbine Specific
        private static double MoI_GG, MoI_PT;
        private double GearRatio = 16.36;
        private bool StarterActive = false;
        private bool IgnitionActive = false;
        private double GG_TargetRPM, GG_CurrentRPM;
        private double PT_TargetRPM, PT_CurrentRPM;
        private const double GG_MaxRPM = 10000, GG_MinRPM = 4500;
        private const double PT_MaxRPM = 3600, PT_MinRPM = 0;
        #endregion

        #region Resources
        private MyResourceSinkComponent SinkFuel;
        private MyResourceSinkComponent SinkO2;
        private double CurrentFuelFlow = 0;
        private double TargetFuelFlow = 0;
        private double CurrentAirFlow = 0;
        #endregion

        #region PID
        private double p1, i1, d1;
        private double p2, i2, d2;
        private double kP1 = 0.5, kI1 = 0.125, kD1 = 0;
        private double kP2 = 0.1, kI2 = 0.005, kD2 = 0;
        private double PrevError1;
        private double PrevError2;
        private double StoredInt1;
        private double StoredInt2;
        #endregion

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            if (Block.CubeGrid?.Physics == null)
                return;
            Block.AppendingCustomInfo += AppendCustomInfo;

            Entity.TryGetSubpart("Signage_Gearbox", out MySubpart_Signage);
            ControlsDoOnce();
            UpdateSyncBeforeFrame();
            InitResourceSinks();
            MoI_GG = CalculateMomentOfInertia(GG_MaxRPM);
            MoI_PT = CalculateMomentOfInertia(PT_MaxRPM);

            LoadSettings();
            RequestEngineOn = Settings.EngineRequestOn;
            Throttle = Settings.Throttle;
            KeepThrottle = Settings.KeepThrottle;
            SaveSettings();

            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateAfterSimulation()
        {
            base.UpdateAfterSimulation();
            if (!Block.IsWorking) return;
            UpdateControlInput();
            UpdateState();
            if (!RequestEngineOn) return;
            PIDController_Stage1();
            PIDController_Stage2();
            CalculatePower();
            UpdateClutchStatus();
        }

        public override void UpdateAfterSimulation100()
        {
            base.UpdateAfterSimulation100();

            if (Sync_HasFuel != HasFuel)
                Sync_HasFuel.ValidateAndSet(HasFuel);

            if (!MySubpart_Signage.IsPreview)
                MySubpart_Signage.Render.Visible = false;
        }

        #region Initial Setup
        private static void ControlsDoOnce()
        {
            if (ControlsInitialized) return;

            CreateControls<IMyFunctionalBlock>();
            CreateActions<IMyFunctionalBlock>();

            ControlsInitialized = true;
        }
        private void UpdateSyncBeforeFrame()
        {
            Terminal_RequestEngineOn.ValidateAndSet(RequestEngineOn);
            Terminal_RequestEngineOn.ValueChanged += Terminal_RequestEngineOn_ValueChanged;

            Terminal_EngineState.ValidateAndSet(CurrentState);
            Terminal_EngineState.ValueChanged += Terminal_EngineState_ValueChanged;

            Terminal_KeepThrottle.ValidateAndSet(KeepThrottle);
            Terminal_KeepThrottle.ValueChanged += Terminal_KeepThrottle_ValueChanged;

            Terminal_Throttle.ValidateAndSet(Throttle);
            Terminal_Throttle.ValueChanged += Terminal_Throttle_ValueChanged;

            Sync_HasFuel.ValidateAndSet(HasFuel);
            Sync_HasFuel.ValueChanged += Sync_HasFuel_ValueChanged;
        }

        private void Terminal_RequestEngineOn_ValueChanged(MySync<bool, SyncDirection.BothWays> obj)
        {
            RequestEngineOn = obj.Value;
            Settings.EngineRequestOn = obj.Value;
            UpdateControls();
        }

        private void Terminal_EngineState_ValueChanged(MySync<EngineState, SyncDirection.BothWays> obj)
        {
            CurrentState = obj.Value;
        }

        private void Terminal_Throttle_ValueChanged(MySync<float, SyncDirection.BothWays> obj)
        {
            Throttle = obj.Value;
            Settings.Throttle = obj.Value;
            UpdateControls();
        }

        private void Terminal_KeepThrottle_ValueChanged(MySync<bool, SyncDirection.BothWays> obj)
        {
            KeepThrottle = obj.Value;
            UpdateControls();
        }

        private void Sync_HasFuel_ValueChanged(MySync<bool, SyncDirection.FromServer> obj)
        {
            HasFuel = obj.Value;
            UpdateControls();
        }

        private double CalculateMomentOfInertia(double maxRPM)
        {
            double omega = (2.0 * Math.PI * maxRPM) / 60.0;
            return (2.0 * 6.5 * MyStats.MaxPowerWatts) / (omega * omega);
        }

        private bool InitResourceSinks()
        {
            var sinkFuelInfo = new MyResourceSinkInfo()
            {
                MaxRequiredInput = (float)MyStats.MaxFuelFlow,
                RequiredInputFunc = () => (float)CurrentFuelFlow * Config.globalFuelMult,
                ResourceTypeId = MyDefinitionId.Parse("MyObjectBuilder_GasProperties/DieselFuel"),
            };
            var sinkO2Info = new MyResourceSinkInfo()
            {
                MaxRequiredInput = (float)MyStats.MaxFuelFlow * 30000f,
                RequiredInputFunc = () => (float)CurrentFuelFlow * 30000 * Config.globalFuelMult,
                ResourceTypeId = MyResourceDistributorComponent.OxygenId
            };

            var myBlock = Entity as MyCubeBlock;
            var fakeController = new MyShipController() { SlimBlock = myBlock.SlimBlock };

            SinkFuel = Block.Components?.Get<MyResourceSinkComponent>();
            if (SinkFuel != null)
            {
                SinkFuel.AddType(ref sinkFuelInfo);
            }
            else
            {
                SinkFuel = new MyResourceSinkComponent();
                SinkFuel.Init(MyStringHash.GetOrCompute("Thrust"), sinkFuelInfo);
                Block.Components?.Add(SinkFuel);
            }

            SinkO2 = Block.Components?.Get<MyResourceSinkComponent>();
            if (SinkO2 != null)
            {
                SinkO2.AddType(ref sinkO2Info);
            }
            else
            {
                SinkO2 = new MyResourceSinkComponent();
                SinkO2.Init(MyStringHash.GetOrCompute("Thrust"), sinkO2Info);
                Block.Components?.Add(SinkO2);
            }

            var distributor = fakeController.GridResourceDistributor;
            if (distributor != null)
            {
                distributor.AddSink(SinkFuel);
                distributor.AddSink(SinkO2);
                return true;
            }
            return false;
        }
        #endregion

        private void UpdateControlInput()
        {
            if (CurrentState == EngineState.Running)
            {
                if (IsGenSet)
                {
                    
                }
                else
                {
                    var throttleStepUp = 0.005f;
                    var throttleStepDn = 0.0025f;
                    var moveIndicator = MyGridManager?.ForwardInput ?? 0f;

                    if (Math.Abs(moveIndicator) < 0.01f)
                        moveIndicator = 0f;
                    if (moveIndicator > 0.1f)
                    {
                        Terminal_Throttle.Value = Math.Min(Terminal_Throttle.Value + throttleStepUp, 1f);
                    }
                    else if (moveIndicator < -0.1f)
                    {
                        Terminal_Throttle.Value = Math.Max(Terminal_Throttle.Value - throttleStepUp, 0f);
                    }
                    if (!KeepThrottle)
                    {
                        if (moveIndicator == 0)
                        {
                            //Gradually return to zero when no input is given
                            if (Terminal_Throttle.Value > 0.01f)
                                Terminal_Throttle.Value = Math.Max(Terminal_Throttle.Value - throttleStepDn, 0f);
                            else if (Terminal_Throttle.Value < -0.01f)
                                Terminal_Throttle.Value = Math.Min(Terminal_Throttle.Value + throttleStepDn, 0f);
                            else
                                Terminal_Throttle.Value = 0f;
                        }
                    }
                }
            }
        }

        //Control fuel flow
        private void PIDController_Stage1()
        {
            double error = GG_TargetRPM - GG_CurrentRPM;
            double p = kP1 * error;

            StoredInt1 = (StoredInt1 + error) * PhysicsStep;
            double i = kI1 * StoredInt1;

            double rateValue = (GG_CurrentRPM - PrevError1) / PhysicsStep;
            PrevError1 = GG_CurrentRPM;

            double d = kD1 * -rateValue;

        }

        private void PIDController_Stage2()
        {
            double error = TargetFuelFlow - CurrentFuelFlow;
            double p = Kp_Gas * error;

            StoredIntegration_Gas = MathHelper.Clamp(StoredIntegration_Gas + error * PhysicsStep, -IntegralSaturation_Gas, IntegralSaturation_Gas);
            double i = Ki_Gas * StoredIntegration_Gas;

            double rate = (error - LastError_Gas) / PhysicsStep;
            LastError_Gas = error;

            double valueRate = (CurrentFuelFlow - LastValue_Gas) / PhysicsStep;
            LastValue_Gas = CurrentFuelFlow;

            double deriveMeasure = 0;
            if (DGainInit_Gas)
            {
                if (D_Measure_Gas == DerivativeMeasurement.Velocity)
                    deriveMeasure = -valueRate;
                else
                    deriveMeasure = rate;
            }
            else
                DGainInit_Gas = true;

            double d = Kd_Gas * deriveMeasure;
            CurrentFuelFlow = MathHelper.Clamp(p + i + d, MyStats.MinFuelFlow, MyStats.MaxFuelFlow);
            
        }

        private void CalculatePower()
        {
            double thermalPower = CurrentFuelFlow * DieselHeatEnergy;
            double turbinePower = thermalPower * MyStats.ThermalEfficiency;
            double compressorLoad = MyStats.MaxPowerWatts * Math.Pow(RPM_Gas / MaxRPM_Gas, 3);
            double netPowerGG = turbinePower - compressorLoad;
            double netTorqueGG = netPowerGG / Math.Max(MathHelper.ToRadians(RPM_Gas), 1);
            double ggAccel = netTorqueGG / MomentOfInertia_Gas * 9.549;
            RPM_Gas += ggAccel * PhysicsStep;
            MathHelper.Clamp(RPM_Gas, 0, MaxRPM_Gas);

            double externalLoad = GetLoad();
            double netTorque = netTorqueGG - externalLoad;
            double currentOmega = MathHelper.ToRadians(RPM_Power);
            double ptAccel = netTorque / MomentOfInertia_Power * 9.549;
            RPM_Power += ptAccel * PhysicsStep;
            MathHelper.Clamp(RPM_Power, 0, MaxRPM_Power);
        }

        private void UpdateState()
        {
            switch (CurrentState)
            {
                case EngineState.Off:
                    CurrentStateLabel = "Off";
                    if (RequestEngineOn)
                    {
                        StarterActive = true;
                        Terminal_EngineState.Value = EngineState.Starting;
                    }
                    break;
                case EngineState.Starting:
                    EngineStart();

                    if (RPM_Gas >= IdleRPM_Gas && RPM_Power >= IdleRPM_Power)
                    {
                        Terminal_EngineState.Value = EngineState.Running;

                        StoredIntegration_Gas = 0;
                        StoredIntegration_Power = 0;
                    }
                    if (!RequestEngineOn) Terminal_EngineState.Value = EngineState.Stopping;
                    break;
                case EngineState.Running:
                    CurrentStateLabel = "Running";
                    if (!RequestEngineOn)
                    {
                        Terminal_EngineState.Value = EngineState.Stopping;
                    }
                    break;
                case EngineState.Stopping:
                    EngineShutdown();
                    CurrentStateLabel = "Shutting Down";
                    StarterActive = false;
                    IgnitionActive = false;

                    if (RPM_Gas <= CooldownRPM_Gas)
                    {
                        DGainInit_Power = false;
                        DGainInit_Gas = false;
                        Terminal_EngineState.Value = EngineState.Off;
                    }
                    break;
            }
        }

        private void EngineStart()
        {
            if (StarterActive)
            {
                double rpmFactor = MathHelper.Clamp(1 - (RPM_Gas / IgnitionRPM_Gas * 0.9), 0, 1);
                double starterTorque = 4000 * rpmFactor;

                double omega = MathHelper.ToRadians(RPM_Gas);
                double accel = starterTorque / MomentOfInertia_Gas;
                omega += accel * PhysicsStep;
                RPM_Gas = MathHelper.ToDegrees(omega);
                //Init starter sounds

                if (RPM_Gas >= IgnitionRPM_Gas)
                    IgnitionActive = true;

                if (RPM_Gas >= IdleRPM_Gas * 0.9)
                    StarterActive = false;
            }
        }

        private void EngineShutdown()
        {

        }

        private void UpdateClutchStatus()
        {
            if (ClutchLockout)
                Terminal_ClutchRatio.Value = 0;

            var engageRange = 250;
            var deltaRPM = RPM_Power - (RPM_In / GearRatio);
            if (deltaRPM <= Math.Abs(engageRange))
                Terminal_ClutchRatio.Value = 1;
            else
                Terminal_ClutchRatio.Value = 0;
        }

        //RPM out is divided by gear ratio
        public override float GetRPM()
        {
            if (IsGenSet)
                return (float)RPM_Power;
            else
                return (float)(RPM_Power / GearRatio * ClutchRatio);
        }

        //Load in is divided by gear ratio
        public override double GetLoad()
        {
            if (IsGenSet)
                return Load_In;
            else
                return Load_In / GearRatio * ClutchRatio;
        }

        static void CreateControls<IMyFunctionalBlock>()
        {
            {
                var Control_RequestEngineOnOff = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyFunctionalBlock>("NPS_Engine_TerminalControl_EngineOnOff");
                Control_RequestEngineOnOff.Title = MyStringId.GetOrCompute("Engine On/Off");
                Control_RequestEngineOnOff.Visible = Control_Visible;
                Control_RequestEngineOnOff.SupportsMultipleBlocks = true;
                Control_RequestEngineOnOff.OnText = MySpaceTexts.SwitchText_On;
                Control_RequestEngineOnOff.OffText = MySpaceTexts.SwitchText_Off;
                Control_RequestEngineOnOff.Getter = Control_Terminal_RequestEngineOnOff_Getter;
                Control_RequestEngineOnOff.Setter = Control_Terminal_RequestEngineOnOff_Setter;
                MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(Control_RequestEngineOnOff);
            }

            {
                var Control_KeepThrottle = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyFunctionalBlock>("NPS_Engine_TerminalControl_KeepThrottle");
                Control_KeepThrottle.Title = MyStringId.GetOrCompute("Keep Throttle");
                Control_KeepThrottle.Tooltip = MyStringId.GetOrCompute("Currently Unfinished");
                Control_KeepThrottle.Visible = Control_Visible_GenSet;
                Control_KeepThrottle.SupportsMultipleBlocks = true;
                Control_KeepThrottle.OnText = MySpaceTexts.SwitchText_On;
                Control_KeepThrottle.OffText = MySpaceTexts.SwitchText_Off;
                Control_KeepThrottle.Getter = Control_Terminal_KeepThrottle_Getter;
                Control_KeepThrottle.Setter = Control_Terminal_KeepThrottle_Setter;
                MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(Control_KeepThrottle);
            }

            {
                var Control_Throttle = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyFunctionalBlock>("NPS_Engine_TerminalControl_Throttle");
                Control_Throttle.Title = MyStringId.GetOrCompute("Throttle Override");
                Control_Throttle.Visible = Control_Visible_GenSet;
                Control_Throttle.SetLimits(0f, 1f);
                Control_Throttle.SupportsMultipleBlocks = true;
                Control_Throttle.Getter = Control_Terminal_Throttle_Getter;
                Control_Throttle.Setter = Control_Terminal_Throttle_Setter;
                Control_Throttle.Writer = Control_Terminal_Throttle_Writer;
                MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(Control_Throttle);
            }
        }

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder info)
        {
            info.AppendLine($"Status: {CurrentStateLabel}");
            info.AppendLine($"Gas Generator RPM: {RPM_Gas:0.00}");
            info.AppendLine($"Power Turbine RPM: {RPM_Power:0.00}");
            info.AppendLine($"Current Load: {Load_In:0.00}");
            info.AppendLine($"Current Torque: {Torque_Out:0.00}");
            info.AppendLine($"Net: {Torque_Out - Load_In:0.00}");
            info.AppendLine($"Fuel Flow: {CurrentFuelFlow:0.00}");
        }

        static void CreateActions<IMyFunctionalBlock>()
        {

        }

        static bool Control_Visible(IMyTerminalBlock block)
        {
            return GetLogic(block) != null;
        }
        static bool Control_Visible_GenSet(IMyTerminalBlock block)
        {
            var logic = GetLogic(block);
            if (logic != null && !logic.IsGenSet)
                return true;
            else
                return false;
        }

        static bool Control_Terminal_RequestEngineOnOff_Getter(IMyTerminalBlock block)
        {
            var logic = GetLogic(block);
            return logic == null ? false : logic.Terminal_RequestEngineOn;
        }

        static void Control_Terminal_RequestEngineOnOff_Setter(IMyTerminalBlock block, bool value)
        {
            var logic = GetLogic(block);
            if (logic != null)
                logic.Terminal_RequestEngineOn.ValidateAndSet(value);
        }

        static bool Control_Terminal_KeepThrottle_Getter(IMyTerminalBlock block)
        {
            var logic = GetLogic(block);
            return logic == null ? false : logic.Terminal_KeepThrottle;
        }

        static void Control_Terminal_KeepThrottle_Setter(IMyTerminalBlock block, bool value)
        {
            var logic = GetLogic(block);
            if (logic != null)
                logic.Terminal_KeepThrottle.ValidateAndSet(value);
        }

        static float Control_Terminal_Throttle_Getter(IMyTerminalBlock block)
        {
            var logic = GetLogic(block);
            return logic == null ? 0.01f : logic.Terminal_Throttle;
        }

        static void Control_Terminal_Throttle_Setter(IMyTerminalBlock block, float value)
        {
            var logic = GetLogic(block);
            if (logic != null)
                logic.Terminal_Throttle.ValidateAndSet(value);
        }

        static void Control_Terminal_Throttle_Writer(IMyTerminalBlock block, StringBuilder writer)
        {
            var logic = GetLogic(block);
            if (logic != null)
                writer.Append((int)(logic.Terminal_Throttle * 100f)).Append('%');
        }

        public static void UpdateControls()
        {
            List<IMyTerminalControl> controls;
            MyAPIGateway.TerminalControls.GetControls<IMyFunctionalBlock>(out controls);
            foreach (IMyTerminalControl control in controls)
            {
                switch (control.Id)
                {
                    case "NPS_Engine_TerminalControl_EngineOnOff":
                    case "NPS_Engine_TerminalControl_KeepThrottle":
                    case "NPS_Engine_TerminalControl_Throttle":
                        {
                            control.UpdateVisual();
                            break;
                        }
                }
            }
        }

        #region Settings and Saving
        private void SetDefaultSettings()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            Settings.EngineRequestOn = false;
            Settings.EngineState = 0;
            Settings.Throttle = 0f;
            Settings.KeepThrottle = false;
            ModularApi.Log($"{SubtypeName} default settings loaded.");
        }

        internal virtual bool LoadSettings()
        {
            if (Settings == null)
                Settings = new GasTurbineSaveSettings();

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
                    MyAPIGateway.Utilities.SerializeFromBinary<GasTurbineSaveSettings>(Convert.FromBase64String(rawData));

                if (loadedSettings != null)
                {
                    Settings.EngineRequestOn = loadedSettings.EngineRequestOn;
                    Settings.EngineState = loadedSettings.EngineState;
                    Settings.Throttle = loadedSettings.Throttle;
                    Settings.KeepThrottle = loadedSettings.KeepThrottle;
                    ModularApi.Log($"{SubtypeName} settings loaded.");
                    return true;
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole("Exception in loading Gas Turbine settings: " + e);
                MyAPIGateway.Utilities.ShowMessage("Naval Power Systems", "Exception in loading Gas Turbine settings: " + e);
                ModularApi.Log("Exception in loading Gas Turbine settings: " + e);
            }

            return false;
        }

        private void SaveSettings()
        {
            if (Block == null || Settings == null)
            {
                ModularApi.Log($"Block or Settings null on Gas Turbine.");
                return; // called too soon or after it was already closed, ignore
            }

            if (MyAPIGateway.Utilities == null)
                throw new NullReferenceException(
                    $"MyAPIGateway.Utilities == null; entId={Entity?.EntityId}; Test log 2");

            if (Block.Storage == null)
                Block.Storage = new MyModStorageComponent();

            Block.Storage.SetValue(SettingsGuid,
                Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(Settings)));
            ModularApi.Log($"{SubtypeName} settings saved.");
        }
        #endregion
    }

    [ProtoContract(UseProtoMembersOnly = true)]
    public class GasTurbineSaveSettings
    {
        [ProtoMember(1)]
        public bool EngineRequestOn;
        [ProtoMember(2)]
        public float Throttle;
        [ProtoMember(3)]
        public bool KeepThrottle;
        [ProtoMember(4)]
        public int EngineState;
    }
}
