using Sandbox.Game.EntityComponents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;

namespace NavalPowerSystems.Drivetrain_V2
{
    public class DrivetrainDistributor
    {
        public readonly int AssemblyId;
        public static DrivetrainDistributor Instance { get; private set; } = null;
        private float GearRatio_Diesel = 6.25f;
        private float GearRatio_Turbine = 10f;
        private readonly Dictionary<IMyCubeBlock, MyResourceSourceComponent> Sources = new Dictionary<IMyCubeBlock, MyResourceSourceComponent>();
        private readonly Dictionary<IMyCubeBlock, MyResourceSinkComponent> Sinks = new Dictionary<IMyCubeBlock, MyResourceSinkComponent>();

        public static readonly MyDefinitionId SHPId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "ShaftHorsepower"); //Produced by gearboxes, consumed by propellers
        public static readonly MyDefinitionId LoadId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Load"); //Produced by propellers, consumed by gearboxes

        public DrivetrainDistributor(int id)
        {
            AssemblyId = id;
            Instance = this;
        }

        public void AddSource(IMyCubeBlock block, MyResourceSourceComponent source) => Sources[block] = source;
        public void AddSink(IMyCubeBlock block, MyResourceSinkComponent sink) => Sinks[block] = sink;
        public void RemoveSource(IMyCubeBlock block, MyResourceSourceComponent source) => Sources.Remove(block);
        public void RemoveSink(IMyCubeBlock block, MyResourceSinkComponent sink) => Sinks.Remove(block);

        public void ComputeDistribution()
        {
            float totalLoad = SumSinks(LoadId);
            float totalAvailableSHP = SumSources(SHPId);


        }

        private float SumSources(MyDefinitionId typeId)
        {
            float total = 0;
            foreach (var source in Sources)
                total += source.Value.CurrentOutputByType(typeId);
            return total;
        }
        private float SumSinks(MyDefinitionId typeId)
        {
            float total = 0;
            foreach (var sink in Sinks)
                total += sink.Value.RequiredInputByType(typeId);
            return total;
        }
    }
}
