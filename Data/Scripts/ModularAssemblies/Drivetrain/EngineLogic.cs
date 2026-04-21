using NavalPowerSystems.Drivetrain_V2;
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
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRage.Utils;
using VRageMath;

namespace NavalPowerSystems.Drivetrain.Engine
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_FunctionalBlock), false,
            "NPS_Turbine_LM2500",
            "NPS_Turbine_LM2500Plus",
            "NPS_Turbine_LM2500PlusG4"
    )]
    public class EngineLogic : DrivetrainPart<IMyFunctionalBlock>
    {
        private static EngineLogic GetLogic(IMyTerminalBlock terminalBlock) => terminalBlock?.GameLogic?.GetAs<EngineLogic>();
        private MyCubeBlock MyBlock => Entity as MyCubeBlock;
        private IMyShipController MyShipController;
        private MyEntitySubpart MySubpart;
        private EngineStats_V2 MyStats => Drivetrain_Config.EngineSettings_V2[SubtypeName];
        #region Engine Control Variables
        private int StartupTicks = 0;
        private int TicksToStart = 900; //15 seconds
        private double RPMVarianceMult = 0.02;
        private double FlutterTime = 0;
        private double CurrentRPM = 0;
        private double PreviousRPM = 0;
        private string CurrentStatus = "Off";
        private double ClutchRPMToMatch = 0;
        private double RequestedLoad = 0;
        private double CurrentTorque = 0;
        private double SystemInertia = 0;
        private EngineState CurrentState = EngineState.Off;
        public enum EngineState { Off, Starting, Running, Stopping }
        #endregion
        #region Interface Variables
        public DrivetrainRole Role { get; private set; } = DrivetrainRole.Producer;
        public float EngagementMult { get; private set; }
        public float RPM_In { get; set; }
        public float RPM_Out { get; private set; }
        public double Load_In { get; set; }
        public double Torque_Out { get; private set; }
        #endregion
        #region Resource Sink Variables
        private double FuelResponseRate = 0.1;
        private double CurrentFuelUse = 0;
        private double CurrentO2Use = 0;
        private double FuelFlow = 0;
        private double MaxFuelFlow = 0;
        private MyResourceSinkComponent SinkFuel;
        private MyResourceSinkComponent SinkO2;
        #endregion
        #region Sync, Terminal and Settings Variables
        private static bool ControlsInitialized = false;
        MySync<bool, SyncDirection.BothWays> Terminal_RequestEngineOn;
        private bool RequestEngineOn;
        MySync<float, SyncDirection.BothWays> Terminal_Throttle;
        public float Throttle = 0f;
        MySync<int, SyncDirection.BothWays> Terminal_ThrottleIndex;
        private int ThrottleIndex = 0;
        MySync<bool, SyncDirection.BothWays> Terminal_KeepThrottle;
        private bool KeepThrottle = false;
        MySync<bool, SyncDirection.BothWays> Terminal_ClutchLocked;
        private bool ClutchLocked = true;
        MySync<float, SyncDirection.BothWays> Terminal_ClutchEngagement;
        public float ClutchEngagement;
        MySync<bool, SyncDirection.FromServer> Sync_HasFuel;
        private bool HasFuel = false;
        #endregion

        #region Base Update Cycle
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            Entity.TryGetSubpart("Signage_Gearbox", out MySubpart);
            Block.AppendingCustomInfo += AppendCustomInfo;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            UpdateSyncBeforeFrame();
            InitResourceSinks();
            ControlsDoOnce();

            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateAfterSimulation()
        {
            base.UpdateAfterSimulation();

            GetControlInput();

            if (MyAPIGateway.Session.IsServer)
            {
                if (
                    SinkFuel.ResourceAvailableByType(MyDefinitionId.Parse("MyObjectBuilder_GasProperties/DieselFuel")) <= 0
                    || SinkO2.ResourceAvailableByType(MyResourceDistributorComponent.OxygenId) <= 0
                )
                    HasFuel = false;
                else
                    HasFuel = true;
            }
        }

        public override void UpdateAfterSimulation100()
        {
            base.UpdateAfterSimulation100();

            if (Sync_HasFuel != HasFuel)
                Sync_HasFuel.ValidateAndSet(HasFuel);

            if (!MySubpart.IsPreview)
                MySubpart.Render.Visible = false;

            RecalculateController();
        }
        #endregion

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
            Terminal_RequestEngineOn.SetLocalValue(RequestEngineOn);
            Terminal_RequestEngineOn.ValueChanged += Terminal_RequestEngineOn_ValueChanged;

            Terminal_Throttle.SetLocalValue(Throttle);
            Terminal_Throttle.ValueChanged += Terminal_Throttle_ValueChanged;

            Terminal_ThrottleIndex.SetLocalValue(ThrottleIndex);
            Terminal_ThrottleIndex.ValueChanged += Terminal_ThrottleIndex_ValueChanged;

            Terminal_KeepThrottle.SetLocalValue(KeepThrottle);
            Terminal_KeepThrottle.ValueChanged += Terminal_KeepThrottle_ValueChanged;

            Terminal_ClutchLocked.SetLocalValue(ClutchLocked);
            Terminal_ClutchLocked.ValueChanged += Terminal_ClutchLocked_ValueChanged;

            Terminal_ClutchEngagement.SetLocalValue(ClutchEngagement);
            Terminal_ClutchEngagement.ValueChanged += Terminal_ClutchEngagement_ValueChanged;

            Sync_HasFuel.SetLocalValue(HasFuel);
            Sync_HasFuel.ValueChanged += Sync_HasFuel_ValueChanged;
        }

        private bool InitResourceSinks()
        {
            var kw = (MyStats.PeakTorque * MyStats.PeakRPM) / (9.5488 * 1000);
            MaxFuelFlow = (kw * MyStats.HeatRate) / (Drivetrain_Config.DieselEnergyDensity * 3600);
            var sinkFuelInfo = new MyResourceSinkInfo()
            {
                MaxRequiredInput = (float)MaxFuelFlow,
                RequiredInputFunc = () => (float)CurrentFuelUse,
                ResourceTypeId = MyDefinitionId.Parse("MyObjectBuilder_GasProperties/DieselFuel"),
            };
            var sinkO2Info = new MyResourceSinkInfo()
            {
                MaxRequiredInput = (float)MaxFuelFlow * 30000f,
                RequiredInputFunc = () => (float)CurrentO2Use,
                ResourceTypeId = MyResourceDistributorComponent.OxygenId
            };

            var fakeController = new MyShipController() { SlimBlock = MyBlock.SlimBlock };

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
        #region Physics and Operation
        private void UpdateSoundEffects()
        {
            if (MyAPIGateway.Utilities.IsDedicated) return;
        }
        #endregion
        #region UI and Controls
        private void GetControlInput()
        {
            var throttleStep = 0.005f;
            var moveIndicator = MathHelper.Clamp(-MyShipController?.MoveIndicator.Z ?? 0f, -1f, 1f);

            if (Math.Abs(moveIndicator) < 0.01f)
                moveIndicator = 0f;
            if (moveIndicator > 0.1f)
            {
                Terminal_Throttle.Value = Math.Min(Terminal_Throttle.Value + throttleStep, 1.25f);
            }
            else if (moveIndicator < -0.1f)
            {
                Terminal_Throttle.Value = Math.Max(Terminal_Throttle.Value - throttleStep, 0f);
            }
            if (!KeepThrottle)
            {
                if (moveIndicator == 0)
                {
                    //Gradually return to zero when no input is given
                    if (Terminal_Throttle.Value > 0.01f)
                        Terminal_Throttle.Value = Math.Max(Terminal_Throttle.Value - throttleStep, 0f);
                    else if (Terminal_Throttle.Value < -0.01f)
                        Terminal_Throttle.Value = Math.Min(Terminal_Throttle.Value + throttleStep, 0f);
                    else
                        Terminal_Throttle.Value = 0f;
                }
            }
        }

        public void RecalculateController()
        {
            if (MyShipController == null || !MyShipController.IsWorking || !MyShipController.IsMainCockpit)
            {
                var player = MyAPIGateway.Players.GetPlayerControllingEntity(MyShipController);
                MyShipController = null;

                if (player?.Controller?.ControlledEntity != null)
                    MyShipController = player.Controller.ControlledEntity as IMyShipController;
            }
        }

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder info)
        {
            info.AppendLine($"Status: {CurrentStatus}");
            info.AppendLine($"Clutch Engagement: {ClutchEngagement:0.00}");
            info.AppendLine($"Clutch RPM To Match: {ClutchRPMToMatch:0.00}");
            info.AppendLine($"RPM: {CurrentRPM:0.00}");
            info.AppendLine($"Torque: {CurrentTorque:0.00}");
            info.AppendLine($"Fuel Flow: {CurrentFuelUse:0.00} / {MaxFuelFlow:0.00} L/s");
            info.AppendLine($"Mass Air Flow: {CurrentO2Use:0.00} / {MaxFuelFlow * 30000:0.00} L/s");
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
                var Control_ClutchLocked = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyFunctionalBlock>("NPS_Engine_TerminalControl_ClutchLocked");
                Control_ClutchLocked.Title = MyStringId.GetOrCompute("Clutch Lockout");
                Control_ClutchLocked.Tooltip = MyStringId.GetOrCompute("Enables or Disables automatic clutch engagement.");
                Control_ClutchLocked.Visible = Control_Visible;
                Control_ClutchLocked.SupportsMultipleBlocks = true;
                Control_ClutchLocked.OnText = MySpaceTexts.SwitchText_On;
                Control_ClutchLocked.OffText = MySpaceTexts.SwitchText_Off;
                Control_ClutchLocked.Getter = Control_Terminal_ClutchLocked_Getter;
                Control_ClutchLocked.Setter = Control_Terminal_ClutchLocked_Setter;
                MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(Control_ClutchLocked);
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

            {
                var Control_ThrottleIndex = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, IMyFunctionalBlock>("NPS_Engine_TerminalControl_ThrottleIndex");
                Control_ThrottleIndex.Title = MyStringId.GetOrCompute("Throttle Preset");
                Control_ThrottleIndex.Tooltip = MyStringId.GetOrCompute("Select a preset throttle setting");
                Control_ThrottleIndex.ComboBoxContent = (list) =>
                {
                    list.Add(new MyTerminalControlComboBoxItem { Key = 0, Value = MyStringId.GetOrCompute("Stop") });
                    list.Add(new MyTerminalControlComboBoxItem { Key = 1, Value = MyStringId.GetOrCompute("Ahead Slow") });
                    list.Add(new MyTerminalControlComboBoxItem { Key = 2, Value = MyStringId.GetOrCompute("Ahead Standard") });
                    list.Add(new MyTerminalControlComboBoxItem { Key = 3, Value = MyStringId.GetOrCompute("Ahead Full") });
                    list.Add(new MyTerminalControlComboBoxItem { Key = 4, Value = MyStringId.GetOrCompute("Flank") });
                    list.Add(new MyTerminalControlComboBoxItem { Key = 5, Value = MyStringId.GetOrCompute("Emergency") });
                };
                Control_ThrottleIndex.Visible = Control_Visible;
                Control_ThrottleIndex.SupportsMultipleBlocks = true;
                Control_ThrottleIndex.Getter = Control_Terminal_ThrottleIndex_Getter;
                Control_ThrottleIndex.Setter = Control_Terminal_ThrottleIndex_Setter;
                MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(Control_ThrottleIndex);
            }
        }

        static void CreateActions<IMyFunctionalBlock>()
        {

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
                    case "NPS_Engine_TerminalControl_ClutchLocked":
                    case "NPS_Engine_TerminalControl_KeepThrottle":
                    case "NPS_Engine_TerminalControl_Throttle":
                    case "NPS_Engine_TerminalControl_ThrottleIndex":
                        {
                            control.UpdateVisual();
                            break;
                        }
                }
            }
        }

        private void Terminal_RequestEngineOn_ValueChanged(MySync<bool, SyncDirection.BothWays> obj)
        {
            RequestEngineOn = obj.Value;
            UpdateControls();
        }

        private void Terminal_Throttle_ValueChanged(MySync<float, SyncDirection.BothWays> obj)
        {
            Throttle = obj.Value;
            UpdateControls();
        }

        private void Terminal_ThrottleIndex_ValueChanged(MySync<int, SyncDirection.BothWays> obj)
        {
            ThrottleIndex = obj.Value;

            if (ThrottleIndex == -1) return;

            float target = 0f;
            switch (ThrottleIndex)
            {
                case 0:
                    target = 0f;
                    break;
                case 1:
                    target = 0.15f;
                    break;
                case 2:
                    target = 0.35f;
                    break;
                case 3:
                    target = 0.65f;
                    break;
                case 4:
                    target = 1f;
                    break;
            }
            Terminal_Throttle.Value = target;
            UpdateControls();
        }

        private void Terminal_KeepThrottle_ValueChanged(MySync<bool, SyncDirection.BothWays> obj)
        {
            KeepThrottle = obj.Value;
            UpdateControls();
        }

        private void Terminal_ClutchLocked_ValueChanged(MySync<bool, SyncDirection.BothWays> obj)
        {
            ClutchLocked = obj.Value;
            UpdateControls();
        }

        private void Terminal_ClutchEngagement_ValueChanged(MySync<float, SyncDirection.BothWays> obj)
        {
            ClutchEngagement = obj.Value;
            UpdateControls();
        }

        private void Sync_HasFuel_ValueChanged(MySync<bool, SyncDirection.FromServer> obj)
        {
            HasFuel = obj.Value;
            UpdateControls();
        }

        static bool Control_Visible(IMyTerminalBlock engine)
        {
            return GetLogic(engine) != null;
        }

        static bool Control_Terminal_ClutchLocked_Getter(IMyTerminalBlock engine)
        {
            var logic = GetLogic(engine);
            return logic == null ? false : logic.Terminal_ClutchLocked;
        }

        static void Control_Terminal_ClutchLocked_Setter(IMyTerminalBlock engine, bool value)
        {
            var logic = GetLogic(engine);
            if (logic != null)
                logic.Terminal_ClutchLocked.ValidateAndSet(value);
        }

        static bool Control_Terminal_KeepThrottle_Getter(IMyTerminalBlock engine)
        {
            var logic = GetLogic(engine);
            return logic == null ? false : logic.Terminal_KeepThrottle;
        }

        static void Control_Terminal_KeepThrottle_Setter(IMyTerminalBlock engine, bool value)
        {
            var logic = GetLogic(engine);
            if (logic != null)
                logic.Terminal_KeepThrottle.ValidateAndSet(value);
        }

        static void Control_Terminal_KeepThrottle_Action(IMyTerminalBlock engine)
        {
            var logic = GetLogic(engine);
            if (logic != null)
                logic.Terminal_KeepThrottle.ValidateAndSet(!logic.Terminal_KeepThrottle.Value);
        }

        static float Control_Terminal_Throttle_Getter(IMyTerminalBlock engine)
        {
            var logic = GetLogic(engine);
            return logic == null ? 0.01f : logic.Terminal_Throttle;
        }

        static void Control_Terminal_Throttle_Setter(IMyTerminalBlock engine, float value)
        {
            var logic = GetLogic(engine);
            if (logic != null)
                logic.Terminal_Throttle.ValidateAndSet(value);
        }

        static void Control_Terminal_Throttle_Writer(IMyTerminalBlock engine, StringBuilder writer)
        {
            var logic = GetLogic(engine);
            if (logic != null)
                writer.Append((int)(logic.Terminal_Throttle * 100f)).Append('%');
        }

        static long Control_Terminal_ThrottleIndex_Getter(IMyTerminalBlock engine)
        {
            var logic = GetLogic(engine);
            return logic == null ? 0 : logic.Terminal_ThrottleIndex.Value;
        }

        static void Control_Terminal_ThrottleIndex_Setter(IMyTerminalBlock engine, long value)
        {
            var logic = GetLogic(engine);
            if (logic != null)
                logic.Terminal_ThrottleIndex.ValidateAndSet((int)value);
        }

        static bool Control_Terminal_RequestEngineOnOff_Getter(IMyTerminalBlock engine)
        {
            var logic = GetLogic(engine);
            return logic == null ? false : logic.Terminal_RequestEngineOn;
        }

        static void Control_Terminal_RequestEngineOnOff_Setter(IMyTerminalBlock engine, bool value)
        {
            var logic = GetLogic(engine);
            if (logic != null)
                logic.Terminal_RequestEngineOn.ValidateAndSet(value);
        }
        #endregion
    }
}
