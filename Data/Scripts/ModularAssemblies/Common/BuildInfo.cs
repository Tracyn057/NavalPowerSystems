
namespace NavalPowerSystems.Common
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class NPSBuildInfoIntegration : MySessionComponentBase
    {
        private const long BuildInfoChannel = 11612;
        private const long BuildInfoModID = 514062285;
        private string NPS = "NavalPowerSystems";

        public override void BeforeStart()
        {
            if (MyAPIGateway.Utilities.IsDedicated) return;

            foreach (var engine in Config.EngineSubtypes)
            {
                MyAPIGateway.Utilities.SendModMessage(BuildInfoModID, new MyTuple<string, string, MyDefinitionId>(NPS, "NoDetailInfo", new MyDefinitionId(typeof(MyObjectBuilder_OxygenTank), engine)));
            }
            
        }
    }
}