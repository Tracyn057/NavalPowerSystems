using Microsoft.CodeAnalysis;
using NavalPowerSystems.Common;
using NavalPowerSystems.Communication;
using NavalPowerSystems.Extraction;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using SpaceEngineers.Game.EntityComponents.Blocks;
using System;
using System.Runtime.InteropServices;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace NavalPowerSystems.Production
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OxygenGenerator), false, "NPSProductionOilCracker", "NPSProductionFuelRefinery")]
    public class ProductionLogic : MyGameLogicComponent
    {
        private IMyTerminalBlock _refinery;
        private IMyGasTank _inputTank;
        private MyObjectBuilder_Ore _dummyItem;
        private float _ratio = 0f;
        private int _assemblyId = -1;
        private ProductionSystem _system;
        private string _status = "Idle";
        internal static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        private bool _isComplete = false;
        private bool _isRefinery = false;
        private bool _timer = false;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _refinery = Entity as IMyTerminalBlock;
            if (_refinery == null) return;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            _assemblyId = ModularApi.GetContainingAssembly((IMyCubeBlock)Entity, "Production_Definition");
            if (_assemblyId != -1)
                ProductionManager.ProductionSystems.TryGetValue(_assemblyId, out _system);

            _refinery.AppendingCustomInfo += AppendCustomInfo;

            if (_refinery != null)
            {
                if (_refinery.BlockDefinition.SubtypeName.Contains("OilCracker"))
                {
                    _dummyItem = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Ore>("DummyItemFuel");
                    _ratio = Config.crudeFuelOilRatio;
                    _isRefinery = false;
                }
                else if (_refinery.BlockDefinition.SubtypeName.Contains("FuelRefinery"))
                {
                    _dummyItem = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Ore>("DummyItemDiesel");
                    _ratio = Config.fuelOilDieselRatio;
                    _isRefinery = true;
                }
            }

            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateBeforeSimulation100()
        {
            if (_system == null)
            {
                _status = "Searching for System...";
                _assemblyId = ModularApi.GetContainingAssembly((IMyCubeBlock)Entity, "Production_Definition");
                if (_assemblyId != -1)
                    ProductionManager.ProductionSystems.TryGetValue(_assemblyId, out _system);

                return;
            }

            if (_system._needsRefresh)
            {
                _status = "Refreshing System...";
                if (_assemblyId != -1)
                    ValidateRefinery();
            }
            else if (_isComplete)
                ProcessConversion();

            _timer = !_timer;
        }

        private void ValidateRefinery()
        {
            _isComplete = false;
            if (_assemblyId != -1 && _system != null)
            {
                _inputTank = _system.InputTank;
                if (_inputTank != null)
                {
                    if (_inputTank.BlockDefinition.SubtypeName == "NPSProductionCrudeInput" && !_isRefinery)
                    {
                        _isComplete = true;
                        _system._needsRefresh = false;
                    }
                    else if (_inputTank.BlockDefinition.SubtypeName == "NPSProductionFuelInput" && _isRefinery)
                    {
                        _isComplete = true;
                        _system._needsRefresh = false;
                    }
                    else
                    {
                        _status = "Invalid Input Tank";
                    }
                }
                else
                {
                    _status = "System Not Found";
                }
            }
        }

        private void ProcessConversion()
        {
            var inventory = _refinery.GetInventory(0);
            if (inventory == null)
            {
                _status = "No Inventory Found"; return;
            }

            float gasToRemove = Config.baseRefineRate * 1.6f;
            VRage.MyFixedPoint itemsToAdd = (VRage.MyFixedPoint)Config.baseRefineRate * 1.6f * _ratio;

            if (_inputTank.FilledRatio < gasToRemove / _inputTank.Capacity)
            {
                //MyAPIGateway.Utilities.ShowNotification("Not enough input resource to operate!", 2000, MyFontEnum.Red);
                _status = "Not enough input resource.";
                return;
            }
            _status = "Operating";
            Utilities.ChangeTankLevel(_inputTank, -gasToRemove);
            Utilities.AddNewItem(inventory, _dummyItem, itemsToAdd);
        }

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.AppendLine($"Status: {_status}");

            if (_timer)
                sb.AppendLine("||");
            else if (!_timer)
                sb.AppendLine("|");
        }

        public override void OnRemovedFromScene()
        {
            _system = null;
            _refinery.AppendingCustomInfo -= AppendCustomInfo;
        }

    }
}