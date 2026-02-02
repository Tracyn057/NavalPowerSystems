using NavalPowerSystems.Common;
using NavalPowerSystems.Communication;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.ModAPI;
using VRage.Network;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRage.Utils;
using static NavalPowerSystems.Config;

namespace NavalPowerSystems.DieselEngines
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
    public class NavalEngineLogic : MyGameLogicComponent, IMyEventProxy
    {
        #region Variables
        private static ModularDefinitionApi ModularApi => NavalPowerSystems.ModularDefinition.ModularApi;
        private IMyGasTank _engine;
        private EngineStats _engineStats;
        private EfficiencyPoint[] _engineEfficiency;
        private int _assemblyId;
        private string _status = "Idle";

        private float _requestedThrottle = 0f;
        private float _currentThrottle = 0f;
        private float _requestedMS = 0f;
        private float _currentOutputMW = 0f;
        private float _fuelBurn = 0f;
        private static bool _controlsInit = false;

        private long _selectedThrottleIndex = 0;
        private static Dictionary<long, float> _SpeedSettings = new Dictionary<long, float>
        {
            {0, 0f },
            {1, 0.15f },
            {2, 0.5f },
            {3, 0.8f },
            {4, 1 }
        };

        public MySync<float, SyncDirection.BothWays> RequestedThrottleSync;
        public MySync<int, SyncDirection.BothWays> SelectedThrottleIndexSync;

        #endregion

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            _engine = (IMyGasTank)Entity;
            _engineStats = Config.EngineSettings[_engine.BlockDefinition.SubtypeName];
            _engine.CubeGrid.OnGridChanged += ResetAssemblyId;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        #region Updates

        public override void UpdateOnceBeforeFrame()
        {
            _assemblyId = GetAssemblyId();

            SetInitialStats();

            if (_assemblyId != -1)
            {
                SetAssemblyStats(_assemblyId);
            }

            if (!_controlsInit)
            {
                CreateControls();
                _controlsInit = true;
            }

            _engine.AppendingCustomInfo += AppendCustomInfo;

            RequestedThrottleSync.ValueChanged += obj =>
            {
                _requestedThrottle = obj.Value;
                _engine.RefreshCustomInfo();
            };

            SelectedThrottleIndexSync.ValueChanged += obj =>
            {
                if (obj.Value == -1) return;

                float newTarget = 0f;
                switch (obj.Value)
                {
                    case 0: newTarget = 0f; break;
                    case 1: newTarget = 0.2f; break;
                    case 2: newTarget = 0.5f; break;
                    case 3: newTarget = 0.8f; break;
                    case 4: newTarget = 1.0f; break;
                }
                RequestedThrottleSync.Value = newTarget;
                _engine.RefreshCustomInfo();
            };

            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        public override void UpdateBeforeSimulation10()
        {
            if (_assemblyId != -1)
            {
                SetAssemblyStats(_assemblyId);
            }
            else
            {
                _assemblyId = GetAssemblyId();
                if (_assemblyId != -1) SetAssemblyStats(_assemblyId);
            }

            UpdateThrottle();
            UpdateFuel();
            UpdatePower();

            _currentOutputMW = _engineStats.MaxMW * _currentThrottle;
            _status = (_currentThrottle > 0.01f) ? "Running" : "Idle";
            
            _engine.RefreshCustomInfo();
        }

        public override void OnRemovedFromScene()
        {
            if (_engine.CubeGrid != null)
            {
                _engine.CubeGrid.OnGridChanged -= ResetAssemblyId;
            }
            if (_engine != null) _engine.AppendingCustomInfo -= AppendCustomInfo;
        }

        private void SetInitialStats()
        {
            _engine.Stockpile = true;

            if (_engineStats.Type == EngineType.Turbine)
            {
                _engineEfficiency = TurbineEngineConfigs.TurbineFuelTable;
            }
            else if (_engineStats.Type == EngineType.Diesel)
            {
                _engineEfficiency = DieselEngineConfigs.DieselFuelTable;
            }
        }

        #endregion

        #region Assembly Functions

        private int GetAssemblyId()
        {
            List<IMySlimBlock> clutches = new List<IMySlimBlock>();
            _engine.CubeGrid.GetBlocks(clutches, c =>
                c.FatBlock != null && c.FatBlock.BlockDefinition.SubtypeName == "NPSDrivetrainClutch");

            foreach (var clutch in clutches)
            {
                IMyCubeBlock[] connected = ModularApi.GetConnectedBlocks(clutch as IMyCubeBlock, "Drivetrain_Definition", true);
                if (connected == null) continue;
                foreach (var block in connected)
                {
                    if (block.EntityId == _engine.EntityId)
                    {
                        return ModularApi.GetContainingAssembly(block, "Drivetrain_Definition");
                    }
                }
            }
            return -1;
        }

        private void ResetAssemblyId(IMyCubeGrid grid)
        {
            _assemblyId = -1;
            //SetIdle("Disconnected");
        }

        private void SetAssemblyStats(int assemblyId)
        {
            ModularApi.SetAssemblyProperty<float>(_assemblyId, "CurrentOutMW_Engine_" + _engine.EntityId, _currentOutputMW);
            ModularApi.SetAssemblyProperty<float>(_assemblyId, "CurrentThrottle_Engine_" + _engine.EntityId, _currentThrottle);
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

        public void Spool(float target)
        {
            float rate10 = _engineStats.SpoolRate / 6;
            if (Math.Abs(_currentThrottle - target) < rate10)
                _currentThrottle = target;
            else
                _currentThrottle += (_currentThrottle < target) ? rate10 : -rate10;
        }

        private void UpdateThrottle()
        {
            if (_requestedThrottle == _currentThrottle) return;

            Spool(_requestedThrottle);
        }

        private void UpdateFuel()
        {
            if (!_engine.IsWorking) return;

            float fuelMult = GetFuelMultiplier(_engineEfficiency, (float)_currentThrottle);
            _fuelBurn = (_engineStats.FuelRate * fuelMult) / 6;

            Utilities.ChangeTankLevel(_engine, _fuelBurn);
        }

        private void UpdatePower()
        {
            _currentOutputMW = _engineStats.MaxMW * _currentThrottle;
        }

        #endregion

        #region UI and Controls

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine($"Status: {_status}");
            sb.AppendLine($"Output: {_currentOutputMW:F2} MW");
            sb.AppendLine($"Max Output: {_engineStats.MaxMW:F2} MW");
            sb.AppendLine($"Fuel Rate: {(_fuelBurn * 6):F2} l/s");
            sb.AppendLine($"Throttle: {(_currentThrottle * 100):F0}");
            sb.AppendLine($"Requested Throttle: {(RequestedThrottleSync.Value * 100):F0}");
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
                    (long)(block.GameLogic.GetAs<NavalEngineLogic>()?.SelectedThrottleIndexSync.Value ?? 0);
                throttleList.Setter = (block, key) => 
                    block.GameLogic.GetAs<NavalEngineLogic>().SelectedThrottleIndexSync.Value = (int)key;
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
                    block.GameLogic.GetAs<NavalEngineLogic>()?.RequestedThrottleSync.Value ?? 0f;
                throttleSlider.Setter = (block, value) =>
                {
                    var logic = block.GameLogic.GetAs<NavalEngineLogic>();
                    if (logic != null)
                    {
                        logic.SelectedThrottleIndexSync.Value = -1;
                        logic.RequestedThrottleSync.Value = value;
                    }
                };
                throttleSlider.Writer = (block, sb) =>
                {
                    var logic = block.GameLogic.GetAs<NavalEngineLogic>();
                    if(logic != null)
                    {
                        sb.Append(Math.Round(logic._requestedThrottle * 100)).Append("%");
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

        #endregion
    }
}
