using Microsoft.CodeAnalysis;
using NavalPowerSystems.Common;
using NavalPowerSystems.Communication;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
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
        internal static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;

        private IMyTerminalBlock _refinery;
        private int _assemblyId = -1;
        private IMyGasTank _inputTank;
        public bool _needsRefresh { get; set; }
        private bool _isComplete = false;
        private bool _hasRefinery = false;
        private bool _hasRefineryTank = false;
        private string _itemSubtype = null;
        private float _activeRatio = 1.0f;
        private float _conversionRate = 0f;

        private string _status = "Idle";

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _refinery = Entity as IMyTerminalBlock;
            if (_refinery == null) return;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            _needsRefresh = true;
            if (_refinery != null)
                _assemblyId = ModularApi.GetContainingAssembly(_refinery, "Production_Definition");

            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateBeforeSimulation100()
        {
            if (_needsRefresh)
            {
                ValidateRefinery();
                _needsRefresh = false;
            }
            if (_refinery == null) return;
            if (_assemblyId == -1) _assemblyId = ModularApi.GetContainingAssembly(Entity as IMyCubeBlock, "Production_Definition");
            if (_assemblyId == -1) return;
            

            if (_refinery.IsWorking && _isComplete)
            {
                ProcessConversion();
            }
            else
            {
                _activeRatio = 0f;
            }

        }

        private void ProcessConversion()
        {
            if (_isComplete)
            {
                IMyInventory inventory = _refinery.GetInventory(0);

                if (inventory == null) return;

                if (inventory.CurrentVolume >= inventory.MaxVolume * 0.95f)
                {
                    _status = "Inventory Full";
                    _activeRatio = 0f;
                    return;
                }
                MyObjectBuilder_PhysicalObject oilItem = new MyObjectBuilder_PhysicalObject
            {
                TypeId = "MyObjectBuilder_Ore",
                SubtypeName = "DummyItemCrude"
            };
            float baseRate = Config.derrickExtractRate * 1.6f * logic._oilYield;
            float oceanRate = baseRate * Config.derrickOceanMult;

            VRage.MyFixedPoint count = (VRage.MyFixedPoint)baseRate;
            VRage.MyFixedPoint countOcean = (VRage.MyFixedPoint)oceanRate;


            }
        }

        private void ValidateRefinery()
        {
            _isComplete = false;
            if (_assemblyId == -1 || _refinery == null) return;

            _hasRefinery = false;
            _hasRefineryTank = false;

            bool isRefinery = _refinery.BlockDefinition.SubtypeName == "NPSProductionFuelRefinery";

            if (!isRefinery)
            {
                _itemSubtype = "Fuel";
                _activeRatio = Config.crudeFuelOilRatio;
            }
            else
            {
                _itemSubtype = "Diesel";
                _activeRatio = Config.fuelOilDieselRatio;
            }


            foreach (IMyCubeBlock block in ModularApi.GetMemberParts(_assemblyId))
            {
                if (block == null) return;
                var subtype = block.BlockDefinition.SubtypeName;
                if (subtype == "NPSProductionCrudeInput")
                {
                    if (!isRefinery)
                    {
                        _hasRefineryTank = true;
                        _inputTank = (IMyGasTank)block;
                    }
                    else
                    {
                        _status = "Invalid Input Tank";
                        _hasRefineryTank = false;
                        return;
                    }
                }
                else if (subtype == "NPSProductionFuelInput")
                {
                    if (!isRefinery)
                    {
                        _status = "Invalid Input Tank";
                        _hasRefineryTank = false;
                        return;
                    }
                    else
                    {
                        _hasRefineryTank = true;
                        _inputTank = (IMyGasTank)block;

                    }
                }
            }
            if (_hasRefineryTank == true) _isComplete = true;
        }

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.AppendLine($"Status: {_status}");
            sb.AppendLine($"Production Rate: {(_conversionRate + " l/s")}");
        }

        public override void OnRemovedFromScene()
        {
            if (_refinery != null) _refinery.AppendingCustomInfo -= AppendCustomInfo;
        }

    }
}