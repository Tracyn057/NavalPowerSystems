using System.Collections.Generic;
using System.Linq;
using static NavalPowerSystems.Config;

namespace NavalPowerSystems
{
    public static class Config
    {
        //Global variables
        public const float globalFuelMult = 0.66f;          //Multiplier for fuel consumption
        public const bool requiresMaintenance = false;    //Whether or not to apply wear and tear to engines and propellers, causing them to lose efficiency and eventually fail without repairs
        public const float cavitationDmgMult = 0.1f;      //Multiplier for damage caused by cavitation, applied to propeller blocks
        public const float throttleVariance = 0.015f;    //Amount of random variance in throttle response

        //Fuel refining variables
        public const float crudeFuelOilRatio = 0.75f;   //Ratio of conversion from crude oil to fuel oil
        public const float fuelOilDieselRatio = 0.66f;  //Ratio of conversion from fuel oil to diesel fuel
        public const float baseRefineRate = 100;         //Base rate in liters for oil cracker and refinery

        //Oil extraction variables
        public const double rarityThreshold = 0.8;      //How much of the available oil spawn locations are empty. 1.0 = No oil ever 
        public const float gridSize = 250;            //Size of grid to determine oil deposits
        public const double baseRadius = 150;           //Radius from center of grid for deposit size
        public const int derrickExtractRate = 160;       //Base liters per second extraction rate
        public const float derrickOceanMult = 3.5f;     //Multiplier for extraction rate for deep sea drill platforms
        public const float minWaterDepth = 25;            //Depth in m to utilize OceanMult
        public const int scanSize = 100;                //Grid size for the LCD scan component

        //Viable component lists
        public static readonly HashSet<string> EngineSubtypes = new HashSet<string>
        {
            "NPS_Turbine_MT7",
            "NPS_Turbine_LM2500",
            "NPS_Turbine_LM2500Plus",
            "NPS_Turbine_LM2500PlusG4",
            "NPS_Turbine_MT30"
        };

        public static readonly HashSet<string> MotorSubtypes = new HashSet<string>
        {
            
        };

        public static readonly HashSet<string> PropellerSubtypes = new HashSet<string>
        {
            "NPS_Propeller_4m3b",
            "NPS_Propeller_4m4b",
            "NPS_Propeller_4m5b"
        };

        public static readonly HashSet<string> DriveshaftSubtypes = new HashSet<string>
        {
            "NPS_Driveshaft",
            "NPS_Driveshaft_Marked",
            "NPS_Driveshaft_Long",
            "NPS_Driveshaft_Long_Marked",
            "NPS_Driveshaft_LinearGearbox1",
            "NPS_Driveshaft_LinearGearbox2",
            "NPS_Driveshaft_TubeSealEnclosed1",
            "NPS_Driveshaft_TubeSealSlope1",
            "NPS_Driveshaft_TubeSealSlope1Corner",
            "NPS_Driveshaft_TubeSealSlope2",
            "NPS_Driveshaft_TubeSealSlope2Corner",
            "NPS_Driveshaft_EndTubeV1"
        };

        public static readonly HashSet<string> GearboxSubtypes = new HashSet<string>
        {
            "NPS_Gearbox_MRG",
            "NPS_Gearbox_DoublePlanetary"
        };

        public static readonly HashSet<string> RudderSubtypes = new HashSet<string>
        {
            "NPS_Rudder_Small_CenteredV1",
            "NPS_Rudder_Small_OffsetLeftV1",
            "NPS_Rudder_Small_OffsetRightV1",
            "NPS_Rudder_Small_CenteredV2",
            "NPS_Rudder_Small_OffsetLeftV2",
            "NPS_Rudder_Small_OffsetRightV2"
        };

        public static readonly HashSet<string> TransformerSubtypes = new HashSet<string>
        {
            
        };

        public static readonly HashSet<string> SwitchboardSubtypes = new HashSet<string>
        {
            
        };

        public static readonly HashSet<string> VFDSubtypes = new HashSet<string>
        {
            
        };

        //Component stats definition   

        public static readonly Dictionary<string, SteamTurbineStats> SteamTurbineSettings = new Dictionary<string, SteamTurbineStats>
        {
            {"NPSSteamTurbineDestroyerHP", new SteamTurbineStats { MinFlow = 0.05f, MaxFlow = 0.6f } },
            {"NPSSteamTurbineDestroyerLP", new SteamTurbineStats { MinFlow = 0.05f, MaxFlow = 0.6f } },
            {"NPSSteamTurbineCruiserHP", new SteamTurbineStats { MinFlow = 0.15f, MaxFlow = 1.5f } },
            {"NPSSteamTurbineCruiserLP", new SteamTurbineStats { MinFlow = 0.15f, MaxFlow = 1.5f } },
            {"NPSSteamTurbineCapitalHP", new SteamTurbineStats { MinFlow = 0.5f, MaxFlow = 5f } },
            {"NPSSteamTurbineCapitalLP", new SteamTurbineStats { MinFlow = 0.5f, MaxFlow = 5f } }
        };

        public static readonly Dictionary<string, BoilerStats> BoilerSettings = new Dictionary<string, BoilerStats>
        {
            {"NPSBoilerBnWExpress", new BoilerStats { OperatingBar = 41.4f, OperatingTemp = 727.6f, MassFlow = 0.1157f, FuelFlow = 0.0172f, ThermalMass = 78000f, Capacity = 1006f } },
            {"NPSBoilerBnWMType", new BoilerStats { OperatingBar = 41.4f, OperatingTemp = 727.6f, MassFlow = 0.243f, FuelFlow = 0.0202f, ThermalMass = 175500f, Capacity = 2658f } },
            {"NPSBoilerAdmiralty", new BoilerStats { OperatingBar = 20.7f, OperatingTemp = 588.7f, MassFlow = 0.1042f, FuelFlow = 0.0132f, ThermalMass = 78000f, Capacity = 772f } },
            {"NPSBoilerAdmiraltyHP", new BoilerStats { OperatingBar = 24.1f, OperatingTemp = 672f, MassFlow = 0.1505f, FuelFlow = 0.0149f, ThermalMass = 156000f, Capacity = 1743f } },
            {"NPSBoilerWagner", new BoilerStats { OperatingBar = 68.7f, OperatingTemp = 732.2f, MassFlow = 0.3472f, FuelFlow = 0.0169f, ThermalMass = 312000f, Capacity = 3954f } },
            {"NPSBoilerBenson", new BoilerStats { OperatingBar = 107.9f, OperatingTemp = 783.2f, MassFlow = 0.0926f, FuelFlow = 0.0222f, ThermalMass = 78000f, Capacity = 1298f } },
            {"NPSBoilerKampon", new BoilerStats { OperatingBar = 39.4f, OperatingTemp = 673.2f, MassFlow = 0.3889f, FuelFlow = 0.0264f, ThermalMass = 243000f, Capacity = 4633f } },
        };

        public static readonly float[,] EnthalpyTable = new float[5, 5] {
            { 2733, 0, 0, 0, 0 },           // 400K
            { 2926, 2883, 2828, 0, 0 },      // 500K
            { 3074, 3052, 3026, 2966, 2888 },// 600K
            { 3224, 3211, 3197, 3167, 3134 },// 700K
            { 3448, 3440, 3432, 3415, 3397 } // 800K
        };
    }

    public class SteamTurbineStats
    {
        public float MinFlow;
        public float MaxFlow;
    }

    public class BoilerStats
    {
        public float OperatingBar;  //Operating pressure in bar
        public float OperatingTemp; //Operating temperature in Kelvin
        public float MassFlow;  //Seam mass flow in kg/tick
        public float FuelFlow; //Max fuel flow in liters/tick
        public float ThermalMass;   //Thermal mass of the boiler, affecting how quickly it heats up and cools down
        public float Capacity;  //Water capacity in liters. Internal use to not read from SBC
    }
}
