using VRage.Game.Components;
using VRage.Utils;
using static NavalPowerSystems.Communication.DefinitionDefs;

namespace NavalPowerSystems.Communication
{
    [MySessionComponentDescriptor(MyUpdateOrder.Simulation, int.MinValue)]
    internal class ModularDefinitionSender : MySessionComponentBase
    {
        internal ModularDefinitionContainer StoredDef;

        public override void LoadData()
        {
            MyLog.Default.WriteLineAndConsole(
                $"{ModContext.ModName}.ModularDefinition: Init new ModularAssembliesDefinition");

            // Init
            StoredDef = NavalPowerSystems.ModularDefinition.GetBaseDefinitions();

            // Send definitions over as soon as the API loads, and create the API before anything else can init.
            NavalPowerSystems.ModularDefinition.ModularApi.Init(ModContext, SendDefinitions);
        }

        protected override void UnloadData()
        {
            NavalPowerSystems.ModularDefinition.ModularApi.UnloadData();
        }

        private void SendDefinitions()
        {
            NavalPowerSystems.ModularDefinition.ModularApi.RegisterDefinitions(StoredDef);
        }
    }
}