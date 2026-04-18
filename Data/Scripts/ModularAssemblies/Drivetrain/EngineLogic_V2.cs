using NavalPowerSystems.Communication;
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
            "NPS_Turbine_LM2500",
            "NPS_Turbine_LM2500Plus",
            "NPS_Turbine_LM2500PlusG4"
    )]
    public class EngineLogic_V2 : MyGameLogicComponent, IMyEventProxy, IDrivetrainNode
    {
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        private const float PhysicsStep = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        private MyCubeBlock EngineCube;
        private IMyFunctionalBlock EngineBlock;
        private IMyTerminalBlock EngineTerminal;
        private MyCubeGrid EngineMyGrid;
        private long EngineId;
        private IMyShipController EngineShipController;
        private EngineStats_V2 EngineStats;
        private double SystemInertia;
        private GearboxLogic_V2 ConnectedGearbox;

        private int AssemblyId = -1;
        private string CurrentStatus = "Null";
        private int StartupTicks = 0;
        private int TicksToStart = 900; //15 seconds at 60 ticks per second
        private double RPMVarianceMult = 0.02;
        private double FlutterTime = 0;
        private double CurrentRPM = 0;
        private double PreviousRPM = 0;
        private double CurrentTorque = 0;
        private double CurrentFuelUse = 0;
        private double CurrentO2Use = 0;
        private bool ControlsInitialized = false;
        private bool ActionsInitialized = false;
        private bool SinkInitialized = false;
        private MyResourceSinkComponent SinkFuel;
        private MyResourceSinkComponent SinkO2;
        private HashSet<IMyCubeBlock> ConnectedParts = new HashSet<IMyCubeBlock>();
        private List<DrivetrainPacket> PacketInbox = new List<DrivetrainPacket>();
        public void ReceivePacket(DrivetrainPacket packet) => PacketInbox.Add(packet);
        public double GetLoadWeight() => ClutchEngagement;
        private static EngineLogic_V2 GetLogic(IMyTerminalBlock engine) => engine?.GameLogic?.GetAs<EngineLogic_V2>();
        private double InputLoad = 0;
        public double InputRPM = 0;
        private double OutputRPM = 0;
        private double OutputTorque = 0;

        //Terminal and sync variables
        MySync<float, SyncDirection.BothWays> Terminal_Throttle;
        public float RequestedThrottle = 0f;
        MySync<int, SyncDirection.BothWays> Terminal_ThrottleIndex;
        private int RequestedThrottleIndex = 0;
        MySync<bool, SyncDirection.BothWays> Terminal_KeepThrottle;
        private bool KeepThrottle = false;
        MySync<bool, SyncDirection.BothWays> Terminal_ClutchLocked;
        private bool ClutchLocked = true;
        MySync<float, SyncDirection.BothWays> Terminal_ClutchEngagement;
        public float ClutchEngagement;
        MySync<bool, SyncDirection.FromServer> Sync_HasFuel;
        private bool HasFuel = false;

        //Start machine state variables
        private EngineState CurrentState = EngineState.Off;
        public enum EngineState { Off, Starting, Running, Stopping }

        #region Init and Updates
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            EngineBlock = (IMyFunctionalBlock)Entity;
            EngineCube = (MyCubeBlock)Entity;
            EngineTerminal = (IMyTerminalBlock)Entity;
            EngineId = Entity.EntityId;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
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

            Terminal_ClutchEngagement.SetLocalValue(ClutchEngagement);
            Terminal_ClutchEngagement.ValueChanged += Terminal_ClutchEngagement_ValueChanged;

            Sync_HasFuel.SetLocalValue(HasFuel);
            Sync_HasFuel.ValueChanged += Sync_HasFuel_ValueChanged;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (EngineBlock == null || EngineCube == null)
                return;

            AssemblyId = ModularApi.GetContainingAssembly(EngineBlock, "Drivetrain_Definition_V2");
            EngineTerminal.AppendingCustomInfo += AppendCustomInfo;
            EngineStats = Drivetrain_Config.EngineSettings_V2[EngineBlock.BlockDefinition.SubtypeId];

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

            LoadEngineProperties(EngineTerminal);
            SaveEngineProperties(EngineTerminal);

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

        public override void UpdateAfterSimulation()
        {
            if (EngineBlock == null || EngineCube == null)
                return;

            RecalculateController();
            GetControlInput();
            UpdateEngineState();

            //Get any load information from downstream first
            if (PacketInbox.Count > 0)
            {
                int tickNow = MyAPIGateway.Session.GameplayFrameCounter;
                double load = 0;

                //In theory should only ever be 1 incoming packet, but just in case
                for (int i = 0; i < PacketInbox.Count; i++)
                {
                    var packet = PacketInbox[i];
                    if (packet.TickSent != tickNow) continue;
                    if (IsLoadPacket(packet))
                        load += packet.DownstreamLoad;
                }

                PacketInbox.Clear();
                InputLoad = load;
            }

            //Update physics based on load
            UpdateEngineClutchState();
            CalculateTorqueOutput();

            //Send output information
            if (ConnectedParts.Count > 0)
            {
                var packet = new DrivetrainPacket
                {
                    SenderId = EngineId,
                    TickSent = MyAPIGateway.Session.GameplayFrameCounter,
                    DownstreamLoad = double.NaN,
                    UpstreamRPM = OutputRPM,
                    UpstreamTorque = OutputTorque
                };

                foreach (var part in ConnectedParts)
                {
                    var logic = part.GameLogic?.GetAs<IDrivetrainNode>();
                    if (logic != null)
                        logic.ReceivePacket(packet);
                }
            }

            //UpdateSoundEffects(); //TODO: Implement sound effects based on engine state and RPM

            CalculateResourceUse();
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

        public override void UpdateBeforeSimulation100()
        {
            if (Sync_HasFuel != HasFuel)
                Sync_HasFuel.ValidateAndSet(HasFuel);
        }
        #endregion

        #region ValueChanged and Housekeeping
        private void LoadEngineProperties(IMyTerminalBlock block)
        {
            EngineBlock.Enabled = ModularApi.GetAssemblyProperty<bool>(AssemblyId, EngineBlock.EntityId + "Enabled");
            CurrentState = ModularApi.GetAssemblyProperty<EngineState>(AssemblyId, EngineBlock.EntityId + "EngineState");
            ClutchLocked = ModularApi.GetAssemblyProperty<bool>(AssemblyId, EngineBlock.EntityId + "ClutchLocked");
            RequestedThrottle = ModularApi.GetAssemblyProperty<float>(AssemblyId, EngineBlock.EntityId + "RequestedThrottle");
            RequestedThrottleIndex = ModularApi.GetAssemblyProperty<int>(AssemblyId, EngineBlock.EntityId + "RequestedThrottleIndex");
            CurrentRPM = ModularApi.GetAssemblyProperty<double>(AssemblyId, EngineBlock.EntityId + "CurrentRPM");
        }

        private void SaveEngineProperties(IMyTerminalBlock block)
        {
            ModularApi.SetAssemblyProperty<bool>(AssemblyId, EngineBlock.EntityId + "Enabled", EngineBlock.Enabled);
            ModularApi.SetAssemblyProperty<EngineState>(AssemblyId, EngineBlock.EntityId + "EngineState", CurrentState);
            ModularApi.SetAssemblyProperty<bool>(AssemblyId, EngineBlock.EntityId + "ClutchLocked", ClutchLocked);
            ModularApi.SetAssemblyProperty<float>(AssemblyId, EngineBlock.EntityId + "RequestedThrottle", RequestedThrottle);
            ModularApi.SetAssemblyProperty<int>(AssemblyId, EngineBlock.EntityId + "RequestedThrottleIndex", RequestedThrottleIndex);
            ModularApi.SetAssemblyProperty<double>(AssemblyId, EngineBlock.EntityId + "CurrentRPM", CurrentRPM);
        }

        private static bool IsLoadPacket(DrivetrainPacket p)
        {
            return double.IsNaN(p.UpstreamRPM) && double.IsNaN(p.UpstreamTorque);
        }

        private static bool IsOutputPacket(DrivetrainPacket p)
        {
            return double.IsNaN(p.DownstreamLoad);
        }

        public void CleanAssembly()
        {
            ConnectedParts.Clear();
            foreach (IMyCubeBlock neighbor in ModularApi.GetConnectedBlocks(EngineBlock, "Drivetrain_Definition_V2", false))
            {
                ConnectedParts.Add(neighbor);
            }

            //Engine specific inertia totalling
            double systemInertia = 0;
            foreach (IMyCubeBlock block in ModularApi.GetMemberParts(AssemblyId))
            {
                var subtype = block.BlockDefinition.SubtypeId;
                var propLogic = block.GameLogic?.GetAs<PropellerLogic_V2>();
                var shaftLogic = block.GameLogic?.GetAs<DriveshaftLogic_V2>();
                if (propLogic != null)
                    systemInertia += Drivetrain_Config.PropellerSettings_V2[subtype].Inertia;
                if (shaftLogic != null)
                    systemInertia += Drivetrain_Config.DriveshaftInertiaPerBlock * Drivetrain_Config.ShaftSettings_V2[subtype].BlockLength;
            }

            SystemInertia = systemInertia;
        }

        private void Terminal_Throttle_ValueChanged(MySync<float, SyncDirection.BothWays> obj)
        {
            RequestedThrottle = obj.Value;
            UpdateControls();
            SaveEngineProperties(EngineTerminal);
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
            Terminal_Throttle.Value = target;
            UpdateControls();
            SaveEngineProperties(EngineTerminal);
        }

        private void Terminal_KeepThrottle_ValueChanged(MySync<bool, SyncDirection.BothWays> obj)
        {
            KeepThrottle = obj.Value;
            UpdateControls();
            SaveEngineProperties(EngineTerminal);
        }

        private void Terminal_ClutchLocked_ValueChanged(MySync<bool, SyncDirection.BothWays> obj)
        {
            ClutchLocked = obj.Value;
            UpdateControls();
            SaveEngineProperties(EngineTerminal);
        }

        private void Terminal_ClutchEngagement_ValueChanged(MySync<float, SyncDirection.BothWays> obj)
        {
            ClutchEngagement = obj.Value;
            UpdateControls();
            SaveEngineProperties(EngineTerminal);
        }

        private void Sync_HasFuel_ValueChanged(MySync<bool, SyncDirection.FromServer> obj)
        {
            HasFuel = obj.Value;
            UpdateControls();
            SaveEngineProperties(EngineTerminal);
        }
        #endregion

        #region Physics and operation
        private void CalculateTorqueOutput()
        {
            if (!HasFuel || !EngineBlock.IsWorking || CurrentState == EngineState.Off) return;

            double rpmFactor = Math.Max(CurrentRPM / EngineStats.PeakRPM, 0.2);
            //RPM oscillation
            var localThrottleRequest = RequestedThrottle < 0.04f ? 0.04f : RequestedThrottle;
            FlutterTime += PhysicsStep;
            double flutter = Math.Sin(FlutterTime * 2) * RPMVarianceMult * EngineStats.PeakRPM;
            double targetRPM = (localThrottleRequest * EngineStats.PeakRPM) + flutter;

            //Governor
            double minRPM = 700;
            double error = Math.Max(targetRPM, minRPM) - CurrentRPM;
            double proportionalGain = EngineStats.PeakTorque / (EngineStats.PeakRPM * 0.05) * rpmFactor;
            double rpmRate = (CurrentRPM - PreviousRPM) / PhysicsStep;
            PreviousRPM = CurrentRPM;
            double kd = 0.1;
            double torqueRequest = (error * proportionalGain) - (rpmRate * kd);

            //Engine capability
            double availableTorque = 0;
            double deviation = (CurrentRPM - EngineStats.PeakRPM) / EngineStats.PeakRPM;
            availableTorque = EngineStats.PeakTorque * (1- EngineStats.PowerCurveConstant * Math.Pow(deviation, 2));
            availableTorque = Math.Max(availableTorque, 0);
            CurrentTorque = MathHelper.Clamp(torqueRequest, -availableTorque, availableTorque);

            //Minimum idle load
            double baseLoad = EngineStats.PeakTorque * 0.02;
            double dynamicLoad = EngineStats.PeakTorque * 0.03 * rpmFactor * rpmFactor;
            double internalLoad = baseLoad + dynamicLoad;
            double effectiveLoad = InputLoad * ClutchEngagement;

            double netTorque = CurrentTorque - (effectiveLoad + internalLoad);
            double angularAcceleration = netTorque / (EngineStats.EngineInertia + SystemInertia);
            double changeInRPM = angularAcceleration * 9.5488 * PhysicsStep;

            CurrentRPM += changeInRPM;
            CurrentRPM = Math.Max(CurrentRPM, 0);

            OutputRPM = CurrentRPM * ClutchEngagement;
            OutputTorque = CurrentTorque * ClutchEngagement;
        }

        private void CalculateResourceUse()
        {
            if (CurrentRPM <= 0f)
            {
                CurrentFuelUse = 0f;
                CurrentO2Use = 0f;
                return;
            }

            double loadFactor = Math.Abs(CurrentTorque) / EngineStats.PeakTorque;
            double dynamicHeatRate = EngineStats.HeatRate * (1 + 0.3 * (1.0 - loadFactor));

            double powerKw = Math.Max(CurrentTorque, 0) * CurrentRPM / 9.5488; //KW
            double requiredEnergy = powerKw * dynamicHeatRate / 3600; //Convert kW to kJ/s
            CurrentFuelUse = (float)(requiredEnergy / Drivetrain_Config.DieselEnergyDensity * Config.globalFuelMult);
            CurrentO2Use = CurrentFuelUse * 3.5f; //Approximate O2 use based on fuel use
        }

        private void UpdateEngineState()
        {
            bool canWork = EngineBlock != null && HasFuel && EngineBlock.IsWorking;

            if (!canWork && CurrentState == EngineState.Running)
            {
                CurrentStatus = "Shutting Down";
                CurrentState = EngineState.Stopping;
                Terminal_Throttle.Value = 0f;
                SaveEngineProperties(EngineTerminal);
            }

            switch (CurrentState)
            {
                case EngineState.Off:
                    if (canWork)
                    {
                        CurrentState = EngineState.Starting;
                        CurrentStatus = "Starting";
                        Terminal_Throttle.Value = 0.1f;
                        SaveEngineProperties(EngineTerminal);
                    }
                    break;

                case EngineState.Starting:
                    if (!canWork)
                    {
                        CurrentState = EngineState.Off;
                        StartupTicks = 0;
                        CurrentStatus = "Off";
                        Terminal_Throttle.Value = 0f;
                        SaveEngineProperties(EngineTerminal);
                        return;
                    }

                    StartupTicks++;
                    Terminal_Throttle.Value = 0.1f; //Maintain small throttle during startup

                    if (StartupTicks >= TicksToStart)
                    {
                        CurrentState = EngineState.Running;
                        CurrentStatus = "Running";
                        SaveEngineProperties(EngineTerminal);
                    }
                    break;

                case EngineState.Running:
                    if (!canWork)
                    {
                        CurrentState = EngineState.Stopping;
                        StartupTicks = 0;
                        CurrentStatus = "Shutting Down";
                        Terminal_Throttle.Value = 0f;
                        SaveEngineProperties(EngineTerminal);
                    }
                    break;

                case EngineState.Stopping:
                    if (canWork)
                    {
                        CurrentState = EngineState.Running;
                        CurrentStatus = "Running";
                        SaveEngineProperties(EngineTerminal);
                        return;
                    }
                    RequestedThrottle = 0f;
                    if (CurrentRPM <= 0)
                    {
                        CurrentState = EngineState.Off;
                        StartupTicks = 0;
                        CurrentStatus = "Off";
                        Terminal_Throttle.Value = 0f;
                        SaveEngineProperties(EngineTerminal);
                    }
                    break;
            }
        }

        private void UpdateEngineClutchState()
        {
            if (ClutchLocked)
            {
                Terminal_ClutchEngagement.Value = 0f;
                return;
            }
            double viscousClutch = 0;
            double rpmDifference = Math.Abs(CurrentRPM - InputRPM);
            double engagementWindow = Math.Max(InputRPM * 0.075, 50);

            if (rpmDifference < engagementWindow)
                Terminal_ClutchEngagement.Value += PhysicsStep;
            else
                Terminal_ClutchEngagement.Value -= PhysicsStep;

            Terminal_ClutchEngagement.Value = MathHelper.Clamp(Terminal_ClutchEngagement.Value, 0f, 1f);

            if (RequestedThrottle > 0.15f && ClutchEngagement < 0.2f)
                viscousClutch = 0.05f;

            Terminal_ClutchEngagement.Value = (float)Math.Max(Terminal_ClutchEngagement.Value, viscousClutch);
        }

        private void GetControlInput()
        {
            var throttleStep = 0.025f;
            var moveIndicator = MathHelper.Clamp(-EngineShipController?.MoveIndicator.Z ?? 0f, -1f, 1f);
            
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
            if (EngineShipController == null || !EngineShipController.IsWorking || !EngineShipController.IsMainCockpit)
            {
                var player = MyAPIGateway.Players.GetPlayerControllingEntity(EngineMyGrid);
                EngineShipController = null;

                if (player?.Controller?.ControlledEntity != null)
                    EngineShipController = player.Controller.ControlledEntity as IMyShipController;
            }
        }
        #endregion

        #region UI and controls
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
        #endregion
    }
}