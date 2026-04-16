using NavalPowerSystems.Common;
using NavalPowerSystems.Communication;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Localization;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.ModAPI;
using VRage.Network;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRage.Utils;
using VRageMath;

namespace NavalPowerSystems.Drivetrain_V2
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_FunctionalBlock), false,
            "NPS_Turbine_MT7",
            "NPS_Turbine_LM2500",
            "NPS_Turbine_LM2500Plus",
            "NPS_Turbine_LM2500PlusG4",
            "NPS_Turbine_MT30"
    )]
    public class EngineLogic_V2 : MyGameLogicComponent, IMyEventProxy
    {
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        private IMyFunctionalBlock EngineBlock;
        private IMyTerminalBlock EngineTerminal;
        private MyCubeBlock EngineCube;
        public EngineNode EngineNode;
        private int AssemblyId = -1;
        public GearboxNode ConnectedGearbox = null;
        public float RequestedThrottle { get; set; } = 0f;
        private int RequestedThrottleIndex = 0;
        private bool KeepThrottle = false;
        private bool HasFuel = false;
        public string CurrentStatus = "Null";
        private int StartupTicks = 0;
        private int TicksToStart = 900; //15 seconds at 60 ticks per second
        public double CurrentRPM = 0f;
        public double CurrentTorque = 0f;
        public double CurrentFuelUse = 0f;
        public double CurrentO2Use = 0f;
        private bool ControlsInitialized = false;
        private bool ActionsInitialized = false;
        private bool SinkInitialized = false;
        private MyResourceSinkComponent SinkFuel;
        private MyResourceSinkComponent SinkO2;
        private bool ClutchLocked = true;

        private IMyShipController EngineShipController;
        private MyCubeGrid EngineMyGrid;

        //Terminal and sync variables
        MySync<float, SyncDirection.BothWays> Terminal_Throttle;
        MySync<int, SyncDirection.BothWays> Terminal_ThrottleIndex;
        MySync<bool, SyncDirection.BothWays> Terminal_KeepThrottle;
        MySync<bool, SyncDirection.BothWays> Terminal_ClutchLocked;
        MySync<bool, SyncDirection.FromServer> Sync_HasFuel;

        //Start machine state variables
        private EngineState CurrentState = EngineState.Off;
        public enum EngineState
        {
            Off,
            Starting,
            Running,
            Stopping
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            EngineBlock = (IMyFunctionalBlock)Entity;
            EngineCube = (MyCubeBlock)Entity;
            EngineTerminal = (IMyTerminalBlock)Entity;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (EngineBlock == null || EngineCube == null)
                return;

            AssemblyId = ModularApi.GetContainingAssembly(EngineBlock, "Drivetrain_Definition");
            EngineTerminal.AppendingCustomInfo += AppendCustomInfo;

            UpdateSyncBeforeFrame();

            if (!ControlsInitialized)
            {
                CreateControls();
                ControlsInitialized = true;
            }
            if (!ActionsInitialized)
            {
                CreateActions();
                ActionsInitialized = true;
            }
            if (!SinkInitialized)
            {
                SetupResourceSinks();
                SinkInitialized = true;
            }

            LoadSavedProperties();

            NeedsUpdate =
                MyEntityUpdateEnum.EACH_FRAME
                | MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        private void SetupResourceSinks()
        {
            var sinkFuelInfo = new MyResourceSinkInfo()
            {
                MaxRequiredInput = 250f,
                RequiredInputFunc = () => (float)CurrentFuelUse,
                ResourceTypeId = MyDefinitionId.Parse("MyObjectBuilder_GasProperties/DieselFuel"),
            };
            var sinkO2Info = new MyResourceSinkInfo()
            {
                MaxRequiredInput = 1000f,
                RequiredInputFunc = () => (float)CurrentO2Use,
                ResourceTypeId = MyResourceDistributorComponent.OxygenId
            };

            var fakeController = new MyShipController() { SlimBlock = EngineCube.SlimBlock };

            SinkFuel = EngineCube.Components?.Get<MyResourceSinkComponent>();
            if (SinkFuel != null)
            {
                SinkFuel.AddType(ref sinkFuelInfo);
            }
            else
            {
                SinkFuel = new MyResourceSinkComponent();
                SinkFuel.Init(MyStringHash.GetOrCompute("Thrust"), sinkFuelInfo);
                EngineCube.Components?.Add(SinkFuel);
            }

            SinkO2 = EngineCube.Components?.Get<MyResourceSinkComponent>();
            if (SinkO2 != null)
            {
                SinkO2.AddType(ref sinkO2Info);
            }
            else
            {
                SinkO2 = new MyResourceSinkComponent();
                SinkO2.Init(MyStringHash.GetOrCompute("Thrust"), sinkO2Info);
                EngineCube.Components?.Add(SinkO2);
            }

            var distributor = fakeController.GridResourceDistributor;
            if (distributor != null)
            {
                distributor.AddSink(SinkFuel);
                distributor.AddSink(SinkO2);
            }
        }

        public void SetNode(EngineNode node)
        {
            EngineNode = node;
        }

        private void UpdateSyncBeforeFrame()
        {
            Terminal_Throttle.SetLocalValue(RequestedThrottle);
            Terminal_Throttle.ValueChanged += Terminal_Throttle_ValueChanged;

            Terminal_ThrottleIndex.SetLocalValue(RequestedThrottleIndex);
            Terminal_ThrottleIndex.ValueChanged += Terminal_ThrottleIndex_ValueChanged;

            Terminal_KeepThrottle.SetLocalValue(KeepThrottle);
            Terminal_KeepThrottle.ValueChanged += Terminal_KeepThrottle_ValueChanged;

            Terminal_ClutchLocked.SetLocalValue(ClutchLocked);
            Terminal_ClutchLocked.ValueChanged += Terminal_ClutchLocked_ValueChanged;

            Sync_HasFuel.SetLocalValue(HasFuel);
            Sync_HasFuel.ValueChanged += Sync_HasFuel_ValueChanged;
        }

        private void Terminal_Throttle_ValueChanged(MySync<float, SyncDirection.BothWays> obj)
        {
            RequestedThrottle = obj.Value;
            UpdateControls();
            SaveEngineState(EngineTerminal);
        }

        private void Terminal_ThrottleIndex_ValueChanged(MySync<int, SyncDirection.BothWays> obj)
        {
            RequestedThrottleIndex = obj.Value;

            if (RequestedThrottleIndex == -1) return;

            float target = 0f;
            switch (RequestedThrottleIndex)
            {
                case 0:
                    target = 0f;
                    break;
                case 1:
                    target = 0.15f;
                    break;
                case 2:
                    target = 0.5f;
                    break;
                case 3:
                    target = 0.8f;
                    break;
                case 4:
                    target = 1f;
                    break;
            }
            RequestedThrottle = target;
            UpdateControls();
            SaveEngineState(EngineTerminal);
        }

        private void Terminal_KeepThrottle_ValueChanged(MySync<bool, SyncDirection.BothWays> obj)
        {
            KeepThrottle = obj.Value;
            UpdateControls();
            SaveEngineState(EngineTerminal);
        }

        private void Terminal_ClutchLocked_ValueChanged(MySync<bool, SyncDirection.BothWays> obj)
        {
            ClutchLocked = obj.Value;
            if (EngineNode != null)
                EngineNode.ClutchLocked = obj.Value;
            UpdateControls();
            SaveEngineState(EngineTerminal);
        }

        private void Sync_HasFuel_ValueChanged(MySync<bool, SyncDirection.FromServer> obj)
        {
            HasFuel = obj.Value;
            UpdateControls();
            SaveEngineState(EngineTerminal);
        }

        public override void UpdateAfterSimulation()
        {
            if (EngineBlock == null || EngineCube == null)
                return;

            RecalculateController();
            GetThrustInput();
            UpdateEngineState();

            //UpdateSoundEffects(); //TODO: Implement sound effects based on engine state and RPM

            SinkFuel.Update();
            SinkO2.Update();

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

        public override void UpdateBeforeSimulation10()
        {
            
        }

        public override void UpdateBeforeSimulation100()
        {
            if (Sync_HasFuel != HasFuel)
                Sync_HasFuel.ValidateAndSet(HasFuel);
        }

        private void GetThrustInput()
        {
            if (!KeepThrottle) return;
            var throttleStep = 0.025f;
            var moveIndicator = Math.Clamp(-EngineShipController?.MoveIndicator.Z ?? 0f, -1f, 1f);
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
            else if (moveIndicator == 0)
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

        public void RecalculateController()
        {
            if (EngineShipController == null || !EngineShipController.IsWorking || !EngineShipController.IsMainCockpit)
            {
                var player = MyAPIGateway.Players.GetPlayerControllingEntity(EngineMyGrid);
                EngineShipController = null;

                if (player?.Controller?.ControlledEntity != null)
                    EngineShipController = player.Controller.ControlledEntity as IMyShipController;
            }
        }

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder info)
        {
            info.AppendLine($"Status: {CurrentStatus}");
            info.AppendLine($"RPM: {CurrentRPM:0}");
            info.AppendLine($"Torque: {CurrentTorque:0}");
            info.AppendLine($"Fuel Flow: {CurrentFuelUse:0.00} L/s");
            info.AppendLine($"Mass Air Flow: {CurrentO2Use:0.00} L/s");
        }

        private void CreateControls()
        {
            if (ControlsInitialized) return;
            ControlsInitialized = true;

            {
                var Control_ClutchLocked = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyTerminalBlock>("NPS_Engine_TerminalControl_ClutchLocked");
                Control_ClutchLocked.Title = MyStringId.GetOrCompute("Clutch Lockout");
                Control_ClutchLocked.Tooltip = MyStringId.GetOrCompute("Enables or Disables automatic clutch engagement.");
                Control_ClutchLocked.Visible = Control_Clutch_Visible;
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
                Control_Throttle.SetLimits(0f, 1.25f);
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

        private void CreateActions()
        {
            if (ActionsInitialized) return;
            ActionsInitialized = true;
        }

        public static void UpdateControls()
        {
            List<IMyTerminalControl> controls;

            MyAPIGateway.TerminalControls.GetControls<IMyFunctionalBlock>(out controls);

            foreach (IMyTerminalControl control in controls)
            {
                switch (control.Id)
                {
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

        private void UpdateEngineState()
        {
            bool canWork = EngineBlock != null && HasFuel && EngineBlock.IsWorking && EngineNode != null;

            if (!canWork && CurrentState == EngineState.Running)
            {
                CurrentStatus = "Shutting Down";
                EngineNode.CanTwerk = true;
                CurrentState = EngineState.Stopping;
                SaveEngineState(EngineTerminal);
            }

            switch (CurrentState)
            {
                case EngineState.Off:
                    if (canWork)
                    {
                        CurrentState = EngineState.Starting;
                        CurrentStatus = "Starting";
                        EngineNode.CanTwerk = true;
                        EngineNode.RequestedThrottle = 0.05f; //Initial throttle to induce RPM
                        SaveEngineState(EngineTerminal);
                    }
                    break;

                case EngineState.Starting:
                    if (!canWork)
                    {
                        CurrentState = EngineState.Off;
                        StartupTicks = 0;
                        EngineNode.CanTwerk = false;
                        CurrentStatus = "Off";
                        SaveEngineState(EngineTerminal);
                        return;
                    }

                    StartupTicks++;
                    EngineNode.RequestedThrottle = 0.05f; //Maintain small throttle during startup

                    if (StartupTicks >= TicksToStart)
                    {
                        CurrentState = EngineState.Running;
                        EngineNode.CanTwerk = true;
                        CurrentStatus = "Running";
                        SaveEngineState(EngineTerminal);
                    }
                    break;

                case EngineState.Running:
                    if (!canWork)
                    {
                        CurrentState = EngineState.Stopping;
                        StartupTicks = 0;
                        EngineNode.CanTwerk = true;
                        CurrentStatus = "Shutting Down";
                        SaveEngineState(EngineTerminal);
                    }
                    break;

                case EngineState.Stopping:
                    if (canWork)
                    {
                        CurrentState = EngineState.Running;
                        EngineNode.CanTwerk = true;
                        CurrentStatus = "Running";
                        SaveEngineState(EngineTerminal);
                        return;
                    }
                    EngineNode.RequestedThrottle = 0f;
                    if (CurrentRPM <= 0)
                    {
                        CurrentState = EngineState.Off;
                        StartupTicks = 0;
                        EngineNode.CanTwerk = false;
                        CurrentStatus = "Off";
                        SaveEngineState(EngineTerminal);
                    }
                    break;
            }
        }

        private void LoadSavedProperties()
        {
            EngineBlock.Enabled = ModularApi.GetAssemblyProperty<bool>(AssemblyId, EngineBlock.EntityId+"Enabled");
            CurrentState = ModularApi.GetAssemblyProperty<EngineState>(AssemblyId, EngineBlock.EntityId+"EngineState");
            ClutchLocked = ModularApi.GetAssemblyProperty<bool>(AssemblyId, EngineBlock.EntityId+"ClutchLocked");
            RequestedThrottle = ModularApi.GetAssemblyProperty<float>(AssemblyId, EngineBlock.EntityId+"RequestedThrottle");
            RequestedThrottleIndex = ModularApi.GetAssemblyProperty<int>(AssemblyId, EngineBlock.EntityId+"RequestedThrottleIndex");
            CurrentRPM = ModularApi.GetAssemblyProperty<double>(AssemblyId, EngineBlock.EntityId+"CurrentRPM");
        }

        private void SaveEngineState(IMyTerminalBlock block)
        {
            ModularApi.SetAssemblyProperty<bool>(AssemblyId, EngineBlock.EntityId+"Enabled", EngineBlock.Enabled);
            ModularApi.SetAssemblyProperty<EngineState>(AssemblyId, EngineBlock.EntityId+"EngineState", CurrentState);
            ModularApi.SetAssemblyProperty<bool>(AssemblyId, EngineBlock.EntityId+"ClutchLocked", ClutchLocked);
            ModularApi.SetAssemblyProperty<float>(AssemblyId, EngineBlock.EntityId+"RequestedThrottle", RequestedThrottle);
            ModularApi.SetAssemblyProperty<int>(AssemblyId, EngineBlock.EntityId+"RequestedThrottleIndex", RequestedThrottleIndex);
            ModularApi.SetAssemblyProperty<double>(AssemblyId, EngineBlock.EntityId+"CurrentRPM", CurrentRPM);
        }

        static EngineLogic_V2 GetLogic(IMyTerminalBlock engine) =>
                engine?.GameLogic?.GetAs<EngineLogic_V2>();

        static bool Control_Visible(IMyTerminalBlock engine)
        {
            return GetLogic(engine) != null;
        }

        static bool Control_Clutch_Visible(IMyTerminalBlock engine)
        {
            var logic = GetLogic(engine);
            return logic != null && logic.ConnectedGearbox != null;
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
    }
}