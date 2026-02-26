using NavalPowerSystems.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Text;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Utils;
using static NavalPowerSystems.Config;

namespace NavalPowerSystems.Drivetrain
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OxygenTank), false,
            "NPSDieselTurbine2MW",
            "NPSDieselTurbine5MW",
            "NPSDieselTurbine12MW",
            "NPSDieselTurbine25MW",
            "NPSDieselTurbine40MW",
            "NPSDieselEngine500KW",
            "NPSDieselEngine15MW",
            "NPSDieselEngine25MW"
    )]
    public class CombustionEngineLogic : NavalEngineLogicBase
    {
        #region Variables
        private IMyGasTank _engine;
        private EfficiencyPoint[] _engineEfficiency;
        private string _status = "Idle";
        private float _requestedMS = 0f;
        private float _inertia = 0f;
        private float _fuelBurn = 0f;
        private static bool _controlsInit = false;
        private static bool _actionsInit = false;
        public bool _isLinkedToGenerator { get; set; } = false;

        #endregion

        protected override void SetupEngineReferences()
        {
            _engine = _engineBlock as IMyGasTank;
            if (_engine == null) return;
            _engineStats = Config.EngineSettings[_engine.BlockDefinition.SubtypeId];
        }

        #region Updates

        protected override void EngineInit()
        {

            _engine.Stockpile = true;
            _engine.Enabled = false;
            _state = EngineState.Off;

            LoadSettings();
            _state = (EngineState)Settings.EngineState;
            _engine.Enabled = Settings.Enabled;
            _currentThrottle = Settings.CurrentThrottle;
            RequestedThrottleSync.Value = Settings.RequestedThrottle;
            SaveSettings();

            if (_engineStats.Type == EngineType.GasTurbine)
            {
                _engineEfficiency = TurbineEngineConfigs.TurbineFuelTable;
            }
            else if (_engineStats.Type == EngineType.Diesel)
            {
                _engineEfficiency = DieselEngineConfigs.DieselFuelTable;
            }

            if (!_controlsInit)
            {
                CreateControls();
                _controlsInit = true;
                CreateActions();
                _actionsInit = true;
            }

            _engine.AppendingCustomInfo += AppendCustomInfo;
        }

        protected override void OnSelectedThrottleIndexChanged(int index)
        {
            if (index == -1) return;

            float newTarget = 0f;
            switch (index)
            {
                case 0: newTarget = 0f; break;
                case 1: newTarget = 0.15f; break;
                case 2: newTarget = 0.5f; break;
                case 3: newTarget = 0.8f; break;
                case 4: newTarget = 1.0f; break;
            }
            RequestedThrottleSync.Value = newTarget;
            SaveSettings();
        }

        protected override void OnRequestedThrottleChanged(float value)
        {
            base.OnRequestedThrottleChanged(value);
        }

        protected override void EngineUpdate10()
        {
            UpdateEngineState();

            if (EngineState == EngineState.Running)
            {
                UpdateThrottle();
                UpdateFuel();
                UpdatePower();

                _currentOutputMW = _engineStats.MaxMW * _currentThrottle;
            }
            else
            {
                _currentOutputMW = 0f;
            }
        }

        #endregion

        #region Fuel and Throttle


        private static float GetFuelMultiplier(EfficiencyPoint[] table, float currentThrottle)
        {
            if (currentThrottle <= table[0].Throttle) return table[0].Multiplier;

            if (currentThrottle >= table[table.Length - 1].Throttle)
                return table[table.Length - 1].Multiplier;

            for (int i = 0; i < table.Length - 1; i++)
            {
                if (currentThrottle <= table[i + 1].Throttle)
                {
                    EfficiencyPoint start = table[i];
                    EfficiencyPoint end = table[i + 1];

                    float percentage = (currentThrottle - start.Throttle) / (end.Throttle - start.Throttle);

                    return start.Multiplier + (end.Multiplier - start.Multiplier) * percentage;
                }
            }
            return 1.0f;
        }

        private void Spool(float target)
        {
            float spoolStep = 1f / (_engineStats.SpoolTime * 6f);

            if (_engineStats.Type == EngineType.GasTurbine && _inertia > 0.8f)
                spoolStep *= 1f;
            else if (_engineStats.Type == EngineType.Diesel && _inertia > 0.65f)
                spoolStep *= 1f;

            if (Math.Abs(target) > 0.1f)
                _inertia = Math.Min(_inertia + spoolStep, 1f);
            else
                _inertia = Math.Max(_inertia - spoolStep, 0f);

            float cubicFactor;

            if (_engineStats.Type == EngineType.GasTurbine)
                cubicFactor = (float)Math.Pow(_inertia, 4.5f);
            else

                cubicFactor = _inertia * _inertia * _inertia;

            _currentThrottle = target * cubicFactor;

            float noise = 1f + MyUtils.GetRandomFloat(-Config.throttleVariance, Config.throttleVariance);
            
            _currentThrottle *= noise;
        }

        private void UpdateThrottle()
        {
            if (RequestedThrottleSync.Value == _currentThrottle) return;

            Spool(RequestedThrottleSync.Value);
        }

        private void UpdateFuel()
        {
            if (!_engine.IsWorking) return;

            float fuelMult = GetFuelMultiplier(_engineEfficiency, (float)_currentThrottle);
            _fuelBurn = ((_engineStats.FuelRate * fuelMult) / 6 ) * Config.globalFuelMult;

            if (_engine.FilledRatio <= 0.01f)
            {
                _currentThrottle = 0f;
                _fuelBurn = 0f;
                _status = "Out of Fuel";
                return;
            }
            Utilities.ChangeTankLevel(_engine, -_fuelBurn);
        }

        private void UpdatePower()
        {
            if (_isEngaged)
                _currentOutputMW = _engineStats.MaxMW * _currentThrottle;
            else
                _currentOutputMW = 0f;
        }

        #endregion

        #region UI and Controls

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.AppendLine($"Status: {_status}");
            sb.AppendLine($"Output: {_currentOutputMW:F2} MW");
            sb.AppendLine($"Max Output: {_engineStats.MaxMW:F2} MW");
            sb.AppendLine($"Fuel Rate: {(_fuelBurn * 6):F2} l/s");
            sb.AppendLine($"Throttle: {(_currentThrottle):P0}");
            sb.AppendLine($"Requested Throttle: {(RequestedThrottleSync.Value):P0}");
            sb.AppendLine($"Clutch Engaged: {_isEngaged} ");
        }

        //Future function for when setting target speed by m/s or knots is added
        public void ParseSpeedInput(string input)
        {
            float parsedValue;
            string cleanInput = input.ToLower().Trim();

            if (cleanInput.Contains("kn") || cleanInput.Contains("kts"))
            {
                string numericPart = cleanInput.Replace("kn", "").Replace("kts", "").Trim();
                if (float.TryParse(numericPart, out parsedValue))
                    _requestedMS = parsedValue * 0.514444f;
            }
            else if (float.TryParse(cleanInput, out parsedValue))
            {
                _requestedMS = parsedValue;
            }
        }

        private static void CreateControls()
        {
            if (_controlsInit) return;
            _controlsInit = true;

            {
                var throttleList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, IMyGasTank>("NPSThrottleSet");
                throttleList.Title = MyStringId.GetOrCompute("Throttle Setting");
                throttleList.Tooltip = MyStringId.GetOrCompute("Preset Speed Percentage");
                throttleList.ComboBoxContent = (list) =>
                {
                    list.Add(new MyTerminalControlComboBoxItem { Key = 0, Value = MyStringId.GetOrCompute("Stop") });
                    list.Add(new MyTerminalControlComboBoxItem { Key = 1, Value = MyStringId.GetOrCompute("Ahead Slow") });
                    list.Add(new MyTerminalControlComboBoxItem { Key = 2, Value = MyStringId.GetOrCompute("Ahead Standard") });
                    list.Add(new MyTerminalControlComboBoxItem { Key = 3, Value = MyStringId.GetOrCompute("Ahead Full") });
                    list.Add(new MyTerminalControlComboBoxItem { Key = 4, Value = MyStringId.GetOrCompute("Flank") });
                };
                throttleList.Getter = (block) => 
                    (long)(block.GameLogic.GetAs<CombustionEngineLogic>()?.SelectedThrottleIndexSync.Value ?? 0);
                throttleList.Setter = (block, key) =>
                {
                    block.GameLogic.GetAs<CombustionEngineLogic>().SelectedThrottleIndexSync.Value = (int)key;
                };
                throttleList.Visible = (block) =>
                    block.BlockDefinition.SubtypeName.Contains("NPSDieselTurbine") ||
                    block.BlockDefinition.SubtypeName.Contains("NPSDieselEngine");
                throttleList.SupportsMultipleBlocks = true;
                throttleList.Enabled = (block) => 
                {
                    var logic = block.GameLogic.GetAs<CombustionEngineLogic>();
                    if (logic != null)
                    {
                        return Config.EngineSubtypes.Contains(block.BlockDefinition.SubtypeId) && !logic._isLinkedToGenerator;
                    }
                };

                MyAPIGateway.TerminalControls.AddControl<IMyGasTank>(throttleList);
            }
            {
                var throttleSlider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyGasTank>("NPSThrottleSlider");
                throttleSlider.Title = MyStringId.GetOrCompute("Throttle Override");
                throttleSlider.Tooltip = MyStringId.GetOrCompute("");
                throttleSlider.SetLimits(0.0f, 1.25f);
                throttleSlider.Getter = (block) =>
                    block.GameLogic.GetAs<CombustionEngineLogic>()?.RequestedThrottleSync.Value ?? 0f;
                throttleSlider.Setter = (block, value) =>
                {
                    var logic = block.GameLogic.GetAs<CombustionEngineLogic>();
                    if (logic != null)
                    {
                        logic.SelectedThrottleIndexSync.Value = -1;
                        logic.RequestedThrottleSync.Value = value;
                    }
                };
                throttleSlider.Writer = (block, sb) =>
                {
                    var logic = block.GameLogic.GetAs<CombustionEngineLogic>();
                    if(logic != null)
                    {
                        sb.Append(Math.Round(logic.RequestedThrottleSync.Value * 100)).Append("%");
                    }
                };
                throttleSlider.Visible = (block) =>
                    block.BlockDefinition.SubtypeName.Contains("NPSDieselTurbine") ||
                    block.BlockDefinition.SubtypeName.Contains("NPSDieselEngine");
                throttleSlider.SupportsMultipleBlocks = true;
                throttleSlider.Enabled = (block) => 
                {
                    var logic = block.GameLogic.GetAs<CombustionEngineLogic>();
                    if (logic != null)
                    {
                        return Config.EngineSubtypes.Contains(block.BlockDefinition.SubtypeId) && !logic._isLinkedToGenerator;
                    }
                };;

                MyAPIGateway.TerminalControls.AddControl<IMyGasTank>(throttleSlider);
            }
        }

        private static void CreateActions()
        {
            if (_actionsInit) return;
            _actionsInit = true;
            string[] throttleNames = { "Stop", "Slow", "Std", "Full", "Flank" };

            {
                var throttleActions = MyAPIGateway.TerminalControls.CreateAction<IMyGasTank>("NPSSetThrottle");
                throttleActions.Name = new StringBuilder("Cycle Throttle Settings");
                throttleActions.Icon = @"Textures\GUI\Icons\Actions\Cycle.dds";
                throttleActions.Action = (block) =>
                {
                    var logic = block.GameLogic.GetAs<CombustionEngineLogic>();
                    if (logic != null)
                    {
                        int current = logic.SelectedThrottleIndexSync.Value;
                        logic.SelectedThrottleIndexSync.Value = (current + 1) % 5;
                    }
                };
                throttleActions.Writer = (block, sb) =>
                {
                    var logic = block.GameLogic.GetAs<CombustionEngineLogic>();
                    if (logic != null)
                    {
                        int val = logic.SelectedThrottleIndexSync.Value;
                        if (val >= 0 && val < throttleNames.Length)
                            sb.Append(throttleNames[val]);
                    }
                };
                throttleActions.Enabled = block =>
                {
                    var logic = block.GameLogic.GetAs<CombustionEngineLogic>();
                    if (logic != null)
                    {
                        return Config.EngineSubtypes.Contains(block.BlockDefinition.SubtypeId) && !logic._isLinkedToGenerator;
                    }
                };

                MyAPIGateway.TerminalControls.AddAction<IMyGasTank>(throttleActions);
            }
            {
                var increaseThrottle = MyAPIGateway.TerminalControls.CreateAction<IMyGasTank>("NPSIncreaseThrottle");
                increaseThrottle.Name = new StringBuilder("Increase Throttle");
                increaseThrottle.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
                increaseThrottle.Action = block =>
                {
                    var logic = block.GameLogic.GetAs<CombustionEngineLogic>();
                    if (logic != null)
                    {
                        logic.SelectedThrottleIndexSync.Value = -1;
                        logic.RequestedThrottleSync.Value = Math.Min(logic.RequestedThrottleSync.Value + 0.05f, 1.25f);
                    }
                };
                increaseThrottle.Enabled = block => 
                {
                    var logic = block.GameLogic.GetAs<CombustionEngineLogic>();
                    if (logic != null)
                    {
                        return Config.EngineSubtypes.Contains(block.BlockDefinition.SubtypeId) && !logic._isLinkedToGenerator;
                    }
                };
                increaseThrottle.Writer = (b, sb) => sb.Append($"{(b?.GameLogic?.GetAs<CombustionEngineLogic>()?.RequestedThrottleSync.Value ?? 0) * 100:F0}%");

                MyAPIGateway.TerminalControls.AddAction<IMyGasTank>(increaseThrottle);
            }
            {
                var decreaseThrottle = MyAPIGateway.TerminalControls.CreateAction<IMyGasTank>("NPSDecreaseThrottle");
                decreaseThrottle.Name = new StringBuilder("Decrease Throttle");
                decreaseThrottle.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
                decreaseThrottle.Action = block =>
                {
                    var logic = block.GameLogic.GetAs<CombustionEngineLogic>();
                    if (logic != null)
                    {
                        logic.SelectedThrottleIndexSync.Value = -1;
                        logic.RequestedThrottleSync.Value = Math.Max(logic.RequestedThrottleSync.Value - 0.05f, 0f);
                    }
                };
                decreaseThrottle.Enabled = block => 
                {
                    var logic = block.GameLogic.GetAs<CombustionEngineLogic>();
                    if (logic != null)
                    {
                        return Config.EngineSubtypes.Contains(block.BlockDefinition.SubtypeId) && !logic._isLinkedToGenerator;
                    }
                };
                decreaseThrottle.Writer = (b, sb) => sb.Append($"{(b?.GameLogic?.GetAs<CombustionEngineLogic>()?.RequestedThrottleSync.Value ?? 0) * 100:F0}%");

                MyAPIGateway.TerminalControls.AddAction<IMyGasTank>(decreaseThrottle);
            }
        }

        public override void OnRemovedFromScene()
        {
            SaveSettings();
            if (_engine != null) _engine.AppendingCustomInfo -= AppendCustomInfo;
        }

        #endregion

        #region Startup and Running

        public enum EngineState { Off, Starting, Running, Stopping }

        private EngineState _state = EngineState.Off;
        private int _startupTicks = 0;
        private const int STARTUP_TIME_TICKS = 180; //This is in Update10 calls, so 180 = 30 seconds

        private void UpdateEngineState()
        {   
            bool canWork = _engine != null && _engine.IsWorking;

            switch (_state)
            {
                case EngineState.Off:
                    if (canWork) 
                    {
                        _state = EngineState.Starting;
                        _status = "Starting";
                    }
                    break;

                case EngineState.Starting:
                    if (!canWork)
                    { 
                        _state = EngineState.Off; 
                        _startupTicks = 0; 
                        _currentOutputMW = 0f;
                        _status = "Starting";
                        return; 
                    }
                    
                    _startupTicks++;
                    
                    if (_startupTicks >= STARTUP_TIME_TICKS)
                    {
                        _state = EngineState.Running;
                        _status = "Running";
                    }
                        
                    break;

                case EngineState.Running:
                    if (!canWork) 
                    {  
                        _state = EngineState.Stopping;
                        _startupTicks = 0; 
                        _status = "Shutting Down";
                    }
                    break;

                case EngineState.Stopping:
                    if (canWork) 
                    {
                        _state = EngineState.Running;
                        _status = "Running";
                        return;
                    }
                    RequestedThrottleSync.Value = 0f;
                    _currentOutputMW = 0f;
                    if (_currentThrottle <= 0.01f)
                    {
                        _state = EngineState.Off;
                        _startupTicks = 0;
                        _status = "Off";
                    }
                    break;
            }
        }

        #endregion

        #region Settings

        public static readonly Guid SettingsGuid = new Guid("ff61eeb4-2728-4deb-9a8e-76b0a8ca1f93");
        public CombustionEngineSettings Settings;

        internal void SaveSettings()
        {
            if (Block == null || Settings == null)
            {
                ModularApi.Log($"Save block null or settings null for {typeof(CombustionEngineLogic).Name}");
                return;
            }

            if (MyAPIGateway.Utilities == null)
                throw new NullReferenceException(
                    $"MyAPIGateway.Utilities == null; entId={Entity?.EntityId}; Test log 2");

            if (Block.Storage == null)
                Block.Storage = new MyModStorageComponent();

            Settings.EngineState = (int)_state;
            Settings.Enabled = _engine.Enabled;
            Settings.CurrentThrottle = _currentThrottle;
            Settings.RequestedThrottle = RequestedThrottleSync.Value;

            Block.Storage.SetValue(SettingsGuid,
                Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(Settings)));
        }

        internal virtual void LoadDefaultSettings()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            Settings.EngineState = (int)EngineState.Off;
            Settings.Enabled = false;
            Settings.CurrentThrottle = 0f;
            Settings.RequestedThrottle = 0f;
        }

        internal virtual bool LoadSettings()
        {
            if (Settings == null)
                Settings = new CombustionEngineSettings();

            if (Block.Storage == null)
            {
                LoadDefaultSettings();
                return false;
            }

            string rawData;
            if (!Block.Storage.TryGetValue(SettingsGuid, out rawData))
            {
                LoadDefaultSettings();
                return false;
            }

            try
            {
                var loadedSettings =
                    MyAPIGateway.Utilities.SerializeFromBinary<CombustionEngineSettings>(Convert.FromBase64String(rawData));

                if (loadedSettings != null)
                {
                    if (Enum.IsDefined(typeof(EngineState), loadedSettings.EngineState))
                    {
                        _state = (EngineState)loadedSettings.EngineState;
                    }
                    else
                    {
                        _state = EngineState.Off;
                    }

                    Settings.Enabled = loadedSettings.Enabled;
                    Settings.CurrentThrottle = loadedSettings.CurrentThrottle;
                    Settings.RequestedThrottle = loadedSettings.RequestedThrottle;

                    return true;
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole("Exception in loading Combustion Engine settings: " + e);
                MyAPIGateway.Utilities.ShowMessage("Naval Power Systems", "Exception in loading Combustion Engine settings: " + e);
                ModularApi.Log("Exception in loading Combustion Engine settings: " + e);
            }

            return false;
        }

        #endregion
    }

    [ProtoContract(UseProtoMembersOnly = true)]
    internal class CombustionEngineSettings
    {
        [ProtoMember(4)] public int EngineState;
        [ProtoMember(3)] public bool Enabled;
        [ProtoMember(2)] public float CurrentThrottle;

        [ProtoMember(1)] public float RequestedThrottle;
    }
}
