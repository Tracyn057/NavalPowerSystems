using NavalPowerSystems.Common;
using NavalPowerSystems.Communication;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
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

        private IMyCubeBlock _refineryBlock;
        private int _assemblyId = -1;
        private IMyGasTank _inputTank;
        private bool _isCracker;
        private bool _needsRefresh { get; set; }
        private bool _isComplete = false;
        private bool _hasRefinery = false;
        private bool _hasRefineryTank = false;
        private string _itemSubtype = null;
        private float _activeRatio = 1.0f;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _refineryBlock = (IMyFunctionalBlock)Entity;
            if (_refineryBlock == null) return;
            _needsRefresh = true;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (_needsRefresh) ValidateRefinery();
            if (_refineryBlock != null)
                _assemblyId = ModularApi.GetContainingAssembly(_refineryBlock, "Production_Definition");

            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateBeforeSimulation100()
        {
            if (!MyAPIGateway.Session.IsServer) return;
            if (_refineryBlock == null) return;
            if (_assemblyId == -1) _assemblyId = ModularApi.GetContainingAssembly(Entity as IMyCubeBlock, "Production_Definition");
            if (_assemblyId == -1) return;
            if (_needsRefresh) ValidateRefinery();

            if (_refineryBlock.IsWorking && _isComplete)
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
                var inventory = _refineryBlock.GetInventory();

                if (inventory == null) return;

                if (inventory.CurrentVolume >= inventory.MaxVolume * 0.95f)
                {
                    _status = "Inventory Full";
                    _activeRatio = 0f;
                    return;
                }

                var refineItem = new MyDefinitionId(typeof(MyObjectBuilder_Ore), _itemSubtype);
            }
        }

        private void ValidateRefinery()
        {
            _isComplete = false;
            if (_assemblyId == -1 || _refineryBlock == null) return;

            _hasRefinery = false;
            _hasRefineryTank = false;

            bool isRefinery = (_refineryBlock.BlockDefinition.SubtypeName == "NPSProductionFuelRefinery");

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


    }
}