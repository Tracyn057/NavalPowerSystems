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
        }

        protected override void OnRequestedThrottleChanged(float value)
        {
            base.OnRequestedThrottleChanged(value);
        }

        protected override void EngineUpdate10()
        {
            UpdateThrottle();
            UpdateFuel();
            UpdatePower();

            _currentOutputMW = _engineStats.MaxMW * _currentThrottle;
            _status = (_currentThrottle > 0.01f) ? "Running" : "Idle";
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
            sb.AppendLine($"Throttle: {(_currentThrottle * 100):F0}");
            sb.AppendLine($"Requested Throttle: {(RequestedThrottleSync.Value * 100):F0}");
            sb.AppendLine($"Clutch Engaged: {_isEngaged} ");
        }

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
                    block.GameLogic.GetAs<CombustionEngineLogic>().SelectedThrottleIndexSync.Value = (int)key;
                throttleList.Visible = (block) =>
                    block.BlockDefinition.SubtypeName.Contains("NPSDieselTurbine") ||
                    block.BlockDefinition.SubtypeName.Contains("NPSDieselEngine");
                throttleList.SupportsMultipleBlocks = true;
                throttleList.Enabled = (block) => true;

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
                throttleSlider.Enabled = (block) => true;

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
                throttleActions.Enabled = block => Config.EngineSubtypes.Contains(block.BlockDefinition.SubtypeId);

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
                increaseThrottle.Enabled = block => Config.EngineSubtypes.Contains(block.BlockDefinition.SubtypeId);
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
                decreaseThrottle.Enabled = block => Config.EngineSubtypes.Contains(block.BlockDefinition.SubtypeId);
                decreaseThrottle.Writer = (b, sb) => sb.Append($"{(b?.GameLogic?.GetAs<CombustionEngineLogic>()?.RequestedThrottleSync.Value ?? 0) * 100:F0}%");

                MyAPIGateway.TerminalControls.AddAction<IMyGasTank>(decreaseThrottle);
            }
        }

        public override void OnRemovedFromScene()
        {
            if (_engine != null) _engine.AppendingCustomInfo -= AppendCustomInfo;
        }

        #endregion
    }
}
