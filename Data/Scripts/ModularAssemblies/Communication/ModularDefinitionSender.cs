using NavalPowerSystems.Common;
using NavalPowerSystems.DieselEngines;
using Sandbox.ModAPI;
using System;
using VRage.Game;
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
            StoredDef = ModularDefinition.GetBaseDefinitions();

            ModularDefinition.ModularApi.Init(ModContext, SendDefinitions);
            MyAPIGateway.Utilities.InvokeOnGameThread(() => {
                Utilities.RemoveActions();
                Utilities.RemoveControls();
            });
        }

        protected override void UnloadData()
        {
            ModularDefinition.ModularApi.UnloadData();
        }

        private void SendDefinitions()
        {
            ModularDefinition.ModularApi.RegisterDefinitions(StoredDef);
        }
    }
}