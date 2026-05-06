using NavalPowerSystems.Common;
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
using VRage.Stats;
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

        #region Turbine
        private const double RPMToRadMult = 0.1047197551197, RadToRPMMult = 9.5493;
        private const double SpecificHeatAir = 1005, DieselEnergy = 42700000, WattToHP = 0.001341022;
        private const double GearRatio = 16.36;
        private static double MoI_GG = 30, MoI_PT = 125;
        private double UI_CurrentHP, UI_MaxHP, CurrentEGT;
        private bool StarterActive = false;
        private bool IgnitionActive = false;
        private double CurrentFuelKgs, TargetFuelKgs;
        private double RPMRatioGG, PressureRatio, CurrentAirKgs;
        private double CurrentFuelLps => CurrentFuelKgs / 0.85;
        private double CurrentAirLps => CurrentAirKgs * 816;
        private double T1 = 288.15;
        private double T2a = 288.15, T2s, T2a_Max;
        private double T3 = 288.15, T3_Max = 1500;
        private double T4a = 288.15, T4s, T4a_Max;
        private double TargetRPM_GG, CurrentRPM_GG, IdleRPM_GG = 4500, MaxRPM_GG = 10000;
        private double TargetRPM_PT, CurrentRPM_PT, MaxRPM_PT = 3600;
        #endregion

        #region Resources
        private MyResourceSinkComponent SinkFuel;
        private MyResourceSinkComponent SinkO2;
        #endregion

        #region PID
        private long PIDSelect = 0;
        private double GGkP = 2.5, GGkI = 0, GGkD = 0, GGiStore = 0, GGiMax = 1, GGeLast = 0;
        private double PTkP = 1.75, PTkI = 0, PTkD = 0, PTiStore = 0, PTiMax = 1, PTeLast = 0;
        private double FuelkP = 1.5, FuelkI = 0, FuelkD = 0, FueliStore = 0, FueliMax = 1, FueleLast = 0;
        private PIDController GGController = new PIDController(0.75, 0.25, 0, 2);
        private PIDController PTController = new PIDController(0.0025, 0.002, 0.001, 2);
        private PIDController FuelController = new PIDController(0.8, 0.4, 0, 0.75);
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

            T2a_Max = T1 + ((T1 * Math.Pow(MyStats.PressureRatio, 0.286)) - T1) / MyStats.CompressorEfficiency;
            T4a_Max = T3_Max - MyStats.TurbineEfficiency * (T3_Max - (T3_Max / Math.Pow(MyStats.PressureRatio, 0.286)));

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
            UpdateState();
            UpdateClutchStatus();
            UpdateControlInput();
            Controller();
            TurbineSimulation();
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

        private bool InitResourceSinks()
        {
            var sinkFuelInfo = new MyResourceSinkInfo()
            {
                MaxRequiredInput = float.MaxValue,
                RequiredInputFunc = () => (float)CurrentFuelLps,
                ResourceTypeId = MyDefinitionId.Parse("MyObjectBuilder_GasProperties/DieselFuel"),
            };
            var sinkO2Info = new MyResourceSinkInfo()
            {
                MaxRequiredInput = float.MaxValue,
                RequiredInputFunc = () => (float)CurrentAirLps,
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
            if (CurrentState == EngineState.Off)
            {
                Terminal_Throttle.Value = 0f;
                return;
            }

            if (IsGenSet)
                return;

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

        private void Controller()
        {
            if (CurrentState == EngineState.Off) return;

            double input = (Throttle * MaxRPM_PT) / MaxRPM_PT;
            double target = Math.Max(input, (IdleRPM_GG / MaxRPM_GG));
            double pidOutput = PIDUpdate(target, CurrentRPM_GG / MaxRPM_GG, GGkP, GGkI, GGkD, GGiStore, GGeLast, GGiMax, out GGiStore, out GGeLast);
            double request = pidOutput * MyStats.MaxFuelKgs;
            double controllerFuel = PIDUpdate(request, CurrentFuelKgs, FuelkP, FuelkI, FuelkD, FueliStore, FueleLast, FueliMax, out FueliStore, out FueleLast);
            TargetFuelKgs = MathHelper.Clamp(controllerFuel, 0, MyStats.MaxFuelKgs);
            FuelIntercept();
        }

        private void FuelIntercept()
        {
            double t3Max = 0;
            double airRatio = CurrentAirKgs / MyStats.MaxAirKgs;

            if (airRatio < 0.25)
                t3Max = 800;
            else if (airRatio < 0.5)
                t3Max = 1000;
            else if (airRatio < 0.75)
                t3Max = 1250;
            else
                t3Max = 1500;

            double headroom = t3Max - T2a;
            double allowedFuel = (headroom * SpecificHeatAir * Math.Max(CurrentAirKgs, 2)) / (DieselEnergy * 0.97);

            double fuelError = Math.Min(TargetFuelKgs, allowedFuel) - CurrentFuelKgs;
            double step = 0.05 * PhysicsStep;
            CurrentFuelKgs += MathHelper.Clamp(fuelError, -step, step);
        }

        private void ControllerGenSet()
        {
            double normPT = CurrentRPM_PT / MaxRPM_PT;
            double genRequest = PTController.Update(1, normPT);
            double normFuel = CurrentFuelKgs / MyStats.MaxFuelKgs;
            double fuelPct = FuelController.Update(genRequest, normFuel);

            CurrentFuelKgs = MathHelper.Clamp(fuelPct * MyStats.MaxFuelKgs, 0, MyStats.MaxFuelKgs);
        }

        private double PIDUpdate(double target, double current, double p, double i, double d, double iStore, double eLast, double iMax, out double iStoreOut, out double eLastOut)
        {
            var dT = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            double error = target - current;
            double pOut = p * error;

            iStore = MathHelper.Clamp(iStore + (error * dT), -iMax, iMax);
            double iOut = i * iStore;

            double rateOfChange = (error - eLast) / dT;
            eLast = error;
            double dOut = d * rateOfChange;

            iStoreOut = iStore;
            eLastOut = eLast;

            ModularDefinition.ModularApi.Log($"PID Update - Target: {target}, Current: {current}, Error: {error}, P: {pOut}, I: {iOut}, D: {dOut}");
            return pOut + iOut + dOut;
        }

        private void TurbineSimulation()
        {
            if (CurrentState == EngineState.Off)
            {
                CurrentRPM_GG = 0;
                CurrentRPM_PT = 0;
                UI_CurrentHP = 0;
                T2a += (T1 - T2a) * 0.1 * PhysicsStep;
                T3 += (T1 - T3) * 0.2 * PhysicsStep;
                T4a += (T1 - T4a) * 0.15 * PhysicsStep;

                return;
            }

            /* 
             * T1 = Ambient Temp - 288.15K
             * T2s = Ideal Compressor Exit Temp - T1 * PressureRatio^0.286
             * T2a = Actual Compressor Exit Temp - T1 + (T2s - T1) / CompressorEfficiency
             * T3 = Combustion Exit Temp - Idealized 1400-1600K
             * T4s = Ideal Exhaust Temp - T3 / PressureRatio^0.286
             * T4a = Actual Exhaust Temp - T3 - Turbine Efficiency * (T3 - T4s)
             * 
             * Nc = Compressor Efficiency
             * Nt = Turbine Efficiency
             * Cp = Specific Heat of Air - 1005 J/kg*K
             * Wc = Compressor Work - Cp * (T2a - T1) -- Load on engine
             * Wt = Turbine Work - Cp * (T3 - T4a) -- Power produced by engine
             * Wnet = Wt - Wc -- Net power output
             * Qin = Fuel Energy Input - Cp * (T3 - T2a) -- Energy added by fuel combustion
             */

            // Stage 0
            RPMRatioGG = CurrentRPM_GG / MaxRPM_GG;
            PressureRatio = (MyStats.PressureRatio * 0.85) * Math.Pow(RPMRatioGG, 2) + (MyStats.PressureRatio * 0.15);
            double airExp = 1 + 0.15 * (PressureRatio - 1);
            CurrentAirKgs = MyStats.MaxAirKgs * Math.Pow(RPMRatioGG, airExp);
            MoI_GG = Math.Max(35 * Math.Pow(RPMRatioGG, 1.1), 10);
            MoI_PT = Math.Max(125 * Math.Pow(RPMRatioGG, 1.1), 25);

            double Nc = MyStats.CompressorEfficiency;
            double Nt = MyStats.TurbineEfficiency;
            double airFlow = Math.Max(0.01, CurrentAirKgs);
            double thermalStep = 2.5;

            // Stage 1 - Compressor
            T2s = T1 * Math.Pow(PressureRatio, 0.286);
            double targetT2Aa = T1 + (T2s - T1) / Nc;
            T2a += (targetT2Aa - T2a) * (thermalStep) * PhysicsStep;
            T2a = Math.Min(T2a, T2a_Max);
            double Wc = (SpecificHeatAir * (T2a - T1)) * airFlow;

            // Stage 2 - Combustion
            double jitterMult = 2 + (5 * (CurrentFuelKgs / MyStats.MaxFuelKgs));
            double jitter = MyUtils.GetRandomDouble(-5, 5) * jitterMult;
            double fuelEnergy = DieselEnergy * CurrentFuelKgs * 0.97;
            double targetT3 = T2a + (fuelEnergy / (SpecificHeatAir * airFlow)) + jitter;

            if (CurrentFuelKgs < 0.01)
                T3 = T2a * 0.975;
            else
                T3 += (targetT3 - T3) * (thermalStep) * PhysicsStep;
            T3 = MathHelper.Clamp(T3, T1, T3_Max);

            //Stage 3 - Turbine
            T4s = T3 / Math.Pow(PressureRatio, 0.286);
            double targetT4Aa = T3 - Nt * (T3 - T4s);
            T4a += (targetT4Aa - T4a) * thermalStep * PhysicsStep;
            T4a = MathHelper.Clamp(T4a, T1, T4a_Max);
            double Wt = (SpecificHeatAir * (T3 - T4a)) * airFlow;

            double Wnet = Wt - Wc;
            double torqueC = (Wnet * RPMToRadMult) / Math.Max(CurrentRPM_GG, 1000);
            double inertialDrag = 0.00006 * CurrentRPM_GG * CurrentRPM_GG;

            if (CurrentState == EngineState.Starting)
            {
                torqueC += StarterActive ? 1250 : 0;
            }

            double accelC = ((torqueC - inertialDrag) / MoI_GG) * RadToRPMMult;
            CurrentRPM_GG += accelC * PhysicsStep;
            CurrentRPM_GG = MathHelper.Clamp(CurrentRPM_GG, 0, MaxRPM_GG);

            double torqueP = (Wnet * RPMToRadMult) / Math.Max(CurrentRPM_PT, 500);
            double stallMult = (2 - (CurrentRPM_PT / MaxRPM_GG));
            double wNetP = (torqueP * stallMult) - Load_In;
            double accelP = (wNetP / MoI_PT) * RadToRPMMult;
            CurrentRPM_PT += accelP * PhysicsStep;
            CurrentRPM_PT = MathHelper.Clamp(CurrentRPM_PT, 0, MaxRPM_PT);
        }

        private void UpdateState()
        {
            switch (CurrentState)
            {
                case EngineState.Off:
                    CurrentStateLabel = "Off";
                    if (RequestEngineOn)
                        Terminal_EngineState.Value = EngineState.Starting;
                    break;
                case EngineState.Starting:
                    CurrentStateLabel = "Starting";
                    if (CurrentRPM_GG < 4250)
                        StarterActive = true;
                    if (CurrentRPM_GG >= 4000)
                    {
                        Terminal_EngineState.Value = EngineState.Running;
                        GGController.Reset();
                        FuelController.Reset();
                        PTController.Reset();
                        StarterActive = false;
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
                    CurrentStateLabel = "Shutting Down";
                    if (CurrentRPM_GG < 1000)
                    {
                        Terminal_EngineState.Value = EngineState.Off;
                        GGController.Reset();
                        PTController.Reset();
                        FuelController.Reset();
                    }
                    break;
            }
        }

        private void UpdateClutchStatus()
        {
            if (ClutchLockout)
            {
                Terminal_ClutchRatio.Value = 0;
                return;
            }

            if (IsGenSet)
            {
                Terminal_ClutchRatio.Value = 1;
                return;
            }

            var engageRange = 250;
            var deltaRPM = CurrentRPM_PT - (RPM_In / GearRatio);
            if (deltaRPM <= Math.Abs(engageRange))
                Terminal_ClutchRatio.Value = 1;
            else
                Terminal_ClutchRatio.Value = 0;
        }

        //RPM out is divided by gear ratio
        public override float GetRPM()
        {
            if (IsGenSet)
                return (float)CurrentRPM_PT;
            else
                return (float)(CurrentRPM_PT / GearRatio * ClutchRatio);
        }

        //Load in is divided by gear ratio
        public override double GetLoad()
        {
            double externalLoad;
            if (IsGenSet)
                externalLoad = Load_In;
            else
                externalLoad = Load_In / GearRatio * ClutchRatio;

            return externalLoad;
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
            info.AppendLine($"Gas Generator RPM: {CurrentRPM_GG:0.00}");
            info.AppendLine($"Power Turbine RPM: {CurrentRPM_PT:0.00}");
            info.AppendLine($"Fuel Flow Target: {TargetFuelKgs}");
            info.AppendLine($"Fuel Flow: {CurrentFuelKgs}");
            info.AppendLine($"GGP:{GGkP:0.000} I:{GGkI:0.000} D:{GGkD:0.000}");
            info.AppendLine($"GG eLast:{GGeLast:0.000} iStore:{GGiStore:0.000}");
            info.AppendLine($"PTP:{PTkP:0.000} I:{PTkI:0.000} D:{PTkD:0.000}");
            info.AppendLine($"PT eLast:{PTeLast:0.000} iStore:{PTiStore:0.000}");
            info.AppendLine($"FuelP:{FuelkP:0.000} I:{FuelkI:0.000} D:{FuelkD:0.000}");
            info.AppendLine($"Fuel eLast:{FueleLast:0.000} iStore:{FueliStore:0.000}");
            //info.AppendLine($"Air Flow: {CurrentAirLps:0.00}");
            //info.AppendLine($"Ambient Temperature: {T1:0.00}");
            //info.AppendLine($"Compressor Exit Temperature: {T2a:0.00}");
            //info.AppendLine($"Combustion Exit Temperature: {T3:0.00}");
            //info.AppendLine($"Exhaust Temperature: {T4a:0.00}");

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
