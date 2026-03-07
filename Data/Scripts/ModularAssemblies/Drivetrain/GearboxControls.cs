using NavalPowerSystems.Communication;
using ProtoBuf;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.ModAPI;
using VRage.Network;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRage.Utils;

namespace NavalPowerSystems.Drivetrain
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), false,
            "NPSDrivetrainMRG"
    )]
    public class GearboxControls : MyGameLogicComponent, IMyEventProxy
    {
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        private DrivetrainSystem MySystem;
        private IMyTerminalBlock _gearbox;
        private int _assemblyId = -1;
        private string _gear = "Forward";
        private static bool _controlsInit = false;
        private static bool _actionsInit = false;
        public MySync<bool, SyncDirection.BothWays> TargetReverseSync;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _gearbox = Entity as IMyTerminalBlock;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (!_controlsInit)
            {
                CreateControls();
                _controlsInit = true;
                CreateActions();
                _actionsInit = true;
            }

            LoadSettings();
            _assemblyId = ModularApi.GetContainingAssembly((IMyCubeBlock)_gearbox, "Drivetrain_Definition");
            if (_assemblyId != -1)
            {
                MySystem = DrivetrainManager.Instance.GetDrivetrainSystem(_assemblyId);
                if (MySystem != null)
                    MySystem.SetShiftStateLoad(Settings.GearboxState, Settings.GearboxState, Settings.TargetReverse);
            }
            SaveSettings();

            _gearbox.AppendingCustomInfo += AppendCustomInfo;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        public override void UpdateAfterSimulation10()
        {
            _gearbox.RefreshCustomInfo();
        }

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.AppendLine($"Current Gear: {_gear}");
        }

        public void CreateControls()
        {
            if (_controlsInit) return;
            _controlsInit = true;

            {
                var reverseControl = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyTerminalBlock>("NPSReverseSwitch");
                reverseControl.Title = MyStringId.GetOrCompute("Set Reverse");
                reverseControl.Tooltip = MyStringId.GetOrCompute("Toggle to shift control direction.");
                reverseControl.Getter = (block) => block.GameLogic.GetAs<GearboxControls>().Settings.TargetReverse;
                reverseControl.Setter = (block, value) =>
                {
                    var logic = block.GameLogic.GetAs<GearboxControls>();
                    if (logic != null)
                    {
                        logic.Settings.TargetReverse = value;
                        if (value)
                        {
                            logic._gear = "Reverse";
                        }
                        else
                        {
                            logic._gear = "Forward";
                        }
                        logic.MySystem.SetShiftStateLoad(3, -1f, value);
                        logic.SaveSettings();
                    }
                };
                reverseControl.Visible = (block) => block.GameLogic.GetAs<GearboxControls>() != null;
                reverseControl.SupportsMultipleBlocks = true;
                reverseControl.Enabled = (block) => block.GameLogic.GetAs<GearboxControls>() != null;

                MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(reverseControl);
            }
        }

        public void CreateActions()
        {
            if (_actionsInit) return;
            _actionsInit = true;

            {
                var reverseAction = MyAPIGateway.TerminalControls.CreateAction<IMyTerminalBlock>("NPSReverseAction");
                reverseAction.Name = new StringBuilder("Toggle Reverse");
                reverseAction.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
                reverseAction.Action = (block) =>
                {
                    var logic = block.GameLogic.GetAs<GearboxControls>();
                    if (logic != null)
                    {
                        logic.Settings.TargetReverse = !logic.Settings.TargetReverse;
                        if (Settings.TargetReverse)
                        {
                            logic._gear = "Reverse";
                            logic.MySystem.SetShiftStateLoad(3, -1f, logic.Settings.TargetReverse);
                        }
                        else
                        {
                            logic._gear = "Forward";
                            logic.MySystem.SetShiftStateLoad(3, -1f, logic.Settings.TargetReverse);
                        }
                        logic.SaveSettings();
                    }
                };
                reverseAction.Writer = (block, sb) =>
                {
                    var logic = block.GameLogic.GetAs<GearboxControls>();
                    if (logic != null)
                    {
                        if (logic.Settings.TargetReverse)
                        {
                            sb.Append("Reverse");
                            logic._gear = "Reverse";
                        }
                        else if (!logic.Settings.TargetReverse)
                        {
                            sb.Append("Forward");
                            logic._gear = "Forward";
                        }
                    }
                };
                reverseAction.Enabled = (block) => block.GameLogic.GetAs<GearboxControls>() != null;

                MyAPIGateway.TerminalControls.AddAction<IMyTerminalBlock>(reverseAction);
            }
        }

        public static readonly Guid SettingsGuid = new Guid("ff61eeb4-2728-4deb-9a8e-76b0a8ca1f93");
        internal GearboxSettings Settings;

        internal void SaveSettings()
        {
            if (_gearbox == null || Settings == null)
            {
                ModularApi.Log($"Save block null or settings null for {typeof(GearboxControls).Name}");
                return;
            }

            if (_gearbox.Storage == null)
                _gearbox.Storage = new MyModStorageComponent();

            _gearbox.Storage.SetValue(SettingsGuid, Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(Settings)));
            ModularApi.Log($"{_gearbox.BlockDefinition.SubtypeName} saved settings: GearboxState={Settings.GearboxState}, ShiftProgress={Settings.ShiftProgress}, TargetReverse={Settings.TargetReverse}");
        }

        internal virtual void LoadDefaultSettings()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            Settings.GearboxState = 0;
            Settings.ShiftProgress = 0f;
            Settings.TargetReverse = false;
            ModularApi.Log($"{_gearbox.BlockDefinition.SubtypeName} loaded default settings.");
        }

        internal virtual bool LoadSettings()
        {
            if (Settings == null)
                Settings = new GearboxSettings();

            if (_gearbox.Storage == null)
            {
                LoadDefaultSettings();
                return false;
            }

            string rawData;
            if (!_gearbox.Storage.TryGetValue(SettingsGuid, out rawData))
            {
                LoadDefaultSettings();
                return false;
            }

            try
            {
                var loadedSettings =
                    MyAPIGateway.Utilities.SerializeFromBinary<GearboxSettings>(Convert.FromBase64String(rawData));

                if (loadedSettings != null)
                {
                    Settings.GearboxState = loadedSettings.GearboxState;
                    Settings.ShiftProgress = loadedSettings.ShiftProgress;
                    Settings.TargetReverse = loadedSettings.TargetReverse;

                    return true;
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole("Exception in loading Gearbox settings: " + e);
                MyAPIGateway.Utilities.ShowMessage("Naval Power Systems", "Exception in loading Gearbox settings: " + e);
                ModularApi.Log("Exception in loading Gearbox settings: " + e);
            }

            return false;
        }

        public override void OnRemovedFromScene()
        {
            SaveSettings();
            if (_gearbox != null) _gearbox.AppendingCustomInfo -= AppendCustomInfo;
        }
    }

    [ProtoContract(UseProtoMembersOnly = true)]
    internal class GearboxSettings
    {
        [ProtoMember(1)] public int GearboxState;
        [ProtoMember(2)] public float ShiftProgress;
        [ProtoMember(3)] public bool TargetReverse;
    }
}
