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
        public override float GetRatio() => (float)ClutchRatio;
        public override double GetTorque() => CurrentTorque * GearRatio * ClutchRatio;
        private static GasTurbineLogic GetLogic(IMyTerminalBlock terminalBlock) => terminalBlock?.GameLogic?.GetAs<GasTurbineLogic>();

        #region Sync, Terminal and Settings Variables
        private string CurrentStateLabel = "Null";
        private static bool ControlsInitialized = false;
        MySync<bool, SyncDirection.BothWays> Terminal_RequestEngineOn;
        private bool RequestEngineOn;
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
        private double GearRatio = 16.36;
        private bool StarterActive = false;
        private bool IgnitionActive = false;
        private double LoadTorque = 0;
        private double CurrentTorque = 0;
        private double ShaftPower = 0;
        private const double IgnitionNg = 1200;
        private const double CooldownNg = 1500;
        #endregion

        #region Resources
        private MyResourceSinkComponent SinkFuel;
        private MyResourceSinkComponent SinkO2;
        private double CurrentFuelFlow = 0;
        #endregion

        #region PID
        private bool DGainInit_Gas;
        private bool DGainInit_Power;
        private double LastError_Gas = 0;
        private double LastError_Power = 0;
        private double LastValue_Gas = 0;
        private double LastValue_Power = 0;
        private double Kp_Gas = 0.005;
        private double Kp_Power = 0.005;
        private double Ki_Gas = 0.001;
        private double Ki_Power = 0.001;
        private double StoredIntegration_Gas;
        private double StoredIntegration_Power;
        private double IntegralSaturation_Gas;
        private double IntegralSaturation_Power;
        private double Kd_Gas;
        private double Kd_Power;
        private double RPM_Gas = 0;
        private double RPM_Power = 0;
        private enum DerivativeMeasurement { Velocity, Rate }
        private DerivativeMeasurement D_Measure_Gas;
        private DerivativeMeasurement D_Measure_Power;
        
        

        

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
            UpdateClutchStatus();
            //UpdateEngine();
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

            Terminal_Throttle.ValidateAndSet(Throttle);
            Terminal_Throttle.ValueChanged += Terminal_Throttle_ValueChanged;

            Sync_HasFuel.ValidateAndSet(HasFuel);
            Sync_HasFuel.ValueChanged += Sync_HasFuel_ValueChanged;
        }

        private void Terminal_RequestEngineOn_ValueChanged(MySync<bool, SyncDirection.BothWays> obj)
        {
            ModularApi.Log("Gas Turbine RequestOnOff change.");
            RequestEngineOn = obj.Value;
            Settings.EngineRequestOn = obj.Value;
            UpdateControls();
        }

        private void Terminal_Throttle_ValueChanged(MySync<float, SyncDirection.BothWays> obj)
        {
            Throttle = obj.Value;
            Settings.Throttle = obj.Value;
            UpdateControls();
        }

        private void Sync_HasFuel_ValueChanged(MySync<bool, SyncDirection.FromServer> obj)
        {
            HasFuel = obj.Value;
            UpdateControls();
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

        private double PIDController_Power(double targetRPM)
        {
            //P Term
            double error_Power = targetRPM - RPM_Power;
            double p_Power = Kp_Gas * error_Power;
            
            //I Term
            StoredIntegration_Power = MathHelper.Clamp(StoredIntegration_Power + error_Power * PhysicsStep, -IntegralSaturation_Power, IntegralSaturation_Power);
            double i_Power = Ki_Power * StoredIntegration_Power;

            //D Terms
            double rate_Power = (error_Power - LastError_Power) / PhysicsStep;
            LastError_Power = error_Power;

            double valueRate_Power = (RPM_Power - LastValue_Power) / PhysicsStep;
            LastValue_Power = RPM_Power;

            double deriveMeasure_Power = 0;
            if (DGainInit_Power)
            {
                if (D_Measure_Power == DerivativeMeasurement.Velocity)
                    deriveMeasure_Power = -valueRate_Power;
                else
                    deriveMeasure_Power = rate_Power;
            }
            else
                DGainInit_Power = true;

            double d_Power = Kd_Power * deriveMeasure_Power;
            double result = MathHelper.Clamp(p_Power + i_Power + d_Power, -1, 1);
            return result;
        }
        private void UpdateEngine()
        {
            if (CurrentState == EngineState.Running)
                TargetNp = Throttle * 3600;
            else TargetNp = 0;

            //Power Turbine loop
            double errorNp = TargetNp - CurrentNp;
            if (CurrentState == EngineState.Running)
                Integral_Np = MathHelper.Clamp(Integral_Np + errorNp * PhysicsStep, -250, 250);
            else Integral_Np = 0;

            double demandedNg = (Kp_Np * errorNp) + (Ki_Np * Integral_Np);
            demandedNg = MathHelper.Clamp(demandedNg, MyStats.IdleRPM_Ng, MyStats.MaxRPM_Ng);
            if (CurrentState == EngineState.Starting)
                demandedNg = IgnitionNg;                

            //Gas Generator loop
            double errorNg = demandedNg - CurrentNg;
            if (CurrentState != EngineState.Off && CurrentState != EngineState.Stopping)
                Integral_Ng = MathHelper.Clamp(Integral_Ng + errorNg * PhysicsStep, -1, 1);
            else Integral_Ng = 0;

            double fuelDemand = (Kp_Ng * errorNg) + (Ki_Ng * Integral_Ng);
            if (CurrentState == EngineState.Stopping || CurrentState == EngineState.Off)
                CurrentFuelFlow = 0;
            else
                CurrentFuelFlow = MathHelper.Clamp(fuelDemand, MyStats.MinFuelFlow, MyStats.MaxFuelFlow);

            //Fuel increases Gen speed
            double ngAcceleration = (CurrentFuelFlow * 800) - (CurrentNg * 0.8);
            CurrentNg += ngAcceleration * PhysicsStep;
            CurrentNg = Math.Max(CurrentNg, 0);

            //Torque gen
            double normNg = CurrentNg / MyStats.MaxRPM_Ng;
            double availPower = MyStats.MaxPowerWatts * Math.Pow(normNg, 3);
            double omega = Math.Max(CurrentNp, 1.0) * 0.1047;
            CurrentTorque = availPower / omega;

            LoadTorque = Load_In / GearRatio * ClutchRatio;
            double netTorque = CurrentTorque - LoadTorque;
            double npAccelRad = (netTorque / MyStats.MaxAccelRate) * PhysicsStep;
            double npAccelRPM = npAccelRad * 9.5493;
            CurrentNp += npAccelRPM * PhysicsStep;
            CurrentNp = MathHelper.Clamp(Math.Max(CurrentNp, 0), 0, 3600);

            Torque_Out = CurrentTorque;
            RPM_Out = CurrentNp;
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
                        CurrentState = EngineState.Starting;
                    }
                    break;
                case EngineState.Starting:
                    CurrentStateLabel = "Starting";
                    if (CurrentNg >= IgnitionNg)
                    {
                        IgnitionActive = true;
                    }
                    if (CurrentNg >= MyStats.IdleRPM_Ng)
                    {
                        StarterActive = false;
                        IgnitionActive = false;
                        CurrentState = EngineState.Running;
                    }
                    if (!RequestEngineOn) CurrentState = EngineState.Stopping;
                    break;
                case EngineState.Running:
                    CurrentStateLabel = "Running";
                    if (!RequestEngineOn)
                    {
                        CurrentState = EngineState.Stopping;
                    }
                    break;
                case EngineState.Stopping:
                    CurrentStateLabel = "Shutting Down";
                    StarterActive = false;
                    IgnitionActive = false;

                    if (CurrentNg <= CooldownNg)
                    {
                        CurrentState = EngineState.Off;
                    }
                    break;
            }
        }

        private void UpdateClutchStatus()
        {
            if (ClutchLockout)
                Terminal_ClutchRatio.Value = 0;

            var engageRange = 250;
            var deltaRPM = CurrentNp - (RPM_In / GearRatio);
            if (deltaRPM <= Math.Abs(engageRange))
                Terminal_ClutchRatio.Value = 1;
            else
                Terminal_ClutchRatio.Value = 0;
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
                Control_KeepThrottle.Visible = Control_Visible;
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
                Control_Throttle.Visible = Control_Visible;
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
            info.AppendLine($"Gas Generator RPM: {CurrentNg:0.00}");
            info.AppendLine($"Power Turbine RPM: {CurrentNp:0.00}");
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
