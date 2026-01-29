using NavalPowerSystems.Communication;
using NavalPowerSystems.Utilities;
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
        private IMyFunctionalBlock _producerBlock;
        private int _assemblyId = -1;
        private IMyGasTank _inputTank;
        private IMyGasTank _outputTank;
        private float _activeRatio = 1.0f;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;

            var block = Entity as IMyCubeBlock;
            string subtype = block.BlockDefinition.SubtypeName;

            if (subtype == "NPSProductionOilCracker")
                _activeRatio = Config.crudeFuelOilRatio;
            else if (subtype == "NPSProductionFuelRefinery")
                _activeRatio = Config.fuelOilDieselRatio;
        }

        public override void UpdateBeforeSimulation100()
        {
            _refineryBlock = Entity as IMyCubeBlock;
            _producerBlock = Entity as IMyFunctionalBlock;
            if (_refineryBlock == null || !_refineryBlock.IsWorking) return;

            _assemblyId = ModularApi.GetContainingAssembly(_refineryBlock, "Production_Definition");

            if (_assemblyId == -1)
            {
                _assemblyId = ModularApi.GetContainingAssembly(Entity as IMyCubeBlock, "Production_Definition");
            }

            if (_assemblyId == -1) return;

            bool isActive = _refineryBlock.IsWorking;
            CommonUtilities.UpdatePowerConsumption(_producerBlock, isActive);

            ProductionSystem system;
            if (ProductionManager.ProductionSystems.TryGetValue(_assemblyId, out system))
            {
                if (!system.IsFunctional) return;

                _inputTank = system.CrudeInput ?? system.FuelInput;
                _outputTank = system.FuelOutput ?? system.DieselOutput;

                ProcessLinearConversion();
            }
        }

        private void ProcessLinearConversion()
        {
            ProductionSystem system;
            //MyAPIGateway.Utilities.ShowNotification($"Logic checking Assembly: {_assemblyId}", 1000);
            if (!ProductionManager.ProductionSystems.TryGetValue(_assemblyId, out system) || !system.IsFunctional)
                return;

            _inputTank = (_activeRatio == Config.crudeFuelOilRatio) ? system.CrudeInput : system.FuelInput;
            _outputTank = (_activeRatio == Config.crudeFuelOilRatio) ? system.FuelOutput : system.DieselOutput;

            if (_inputTank == null || _outputTank == null) return;

            double inputCurrentLiters = _inputTank.FilledRatio * _inputTank.Capacity;
            double amountToProcess = Math.Min(inputCurrentLiters, Config.baseRefineRate);
            double outputAmount = amountToProcess * _activeRatio;

            //MyAPIGateway.Utilities.ShowNotification($"Processing: {amountToProcess} In -> {outputAmount} Out", 2000, "Green");

            double outputSpace = _outputTank.Capacity - (_outputTank.FilledRatio * _outputTank.Capacity);

            if (outputSpace >= outputAmount && amountToProcess > 0)
            {
                CommonUtilities.ChangeTankLevel(_inputTank, -amountToProcess);
                CommonUtilities.ChangeTankLevel(_outputTank, outputAmount);
            }
        }

        
    }
}