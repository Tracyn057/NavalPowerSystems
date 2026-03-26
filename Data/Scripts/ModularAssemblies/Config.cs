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
        public const float mnPerMW = 0.2f;             //MN produced per MW
        public const float hpPerMW = 1341.02f;          //Horsepower made per MW, for display only

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
            "NPSDieselTurbine2MW",
            "NPSDieselTurbine5MW",
            "NPSDieselTurbine12MW",
            "NPSDieselTurbine25MW",
            "NPSDieselTurbine40MW",
            "NPSDieselEngine500KW",
            "NPSDieselEngine15MW",
            "NPSDieselEngine25MW"
        };

        public static readonly HashSet<string> MotorSubtypes = new HashSet<string>
        {
            
        };

        public static readonly HashSet<string> PropellerSubtypes = new HashSet<string>
        {
            "NPSDrivetrainProp34",
            "NPSDrivetrainProp44",
            "NPSDrivetrainProp54",
            "NPSDrivetrainProp38",
            "NPSDrivetrainProp48",
            "NPSDrivetrainProp58"
        };

        public static readonly HashSet<string> DriveshaftSubtypes = new HashSet<string>
        {
            "NPSDrivetrainDriveshaft",
            "NPSDrivetrainDriveshaftMarked",
            "NPSDrivetrainDriveshaftLong",
            "NPSDrivetrainDriveshaftLongMarked",
            "NPSDrivetrainLinearGearbox1",
            "NPSDrivetrainLinearGearbox2",
            "NPSDrivetrainTubeSealEnclosed1",
            "NPSDrivetrainTubeSealSlope1",
            "NPSDrivetrainTubeSealSlope1Corner",
            "NPSDrivetrainTubeSealSlope2",
            "NPSDrivetrainTubeSealSlope2Corner",
            "NPSDrivetrainEndTubeV1",
        };

        public static readonly HashSet<string> GearboxSubtypes = new HashSet<string>
        {
            "NPSDrivetrainMRG",
            "NPSGearbox_DoublePlanetary"
        };

        public static readonly HashSet<string> RudderSubtypes = new HashSet<string>
        {
            "NPSDrivetrainRudderSmallCenteredV1",
            "NPSDrivetrainRudderSmallOffsetLeftV1",
            "NPSDrivetrainRudderSmallOffsetRightV1",
            "NPSDrivetrainRudderSmallCenteredV2",
            "NPSDrivetrainRudderSmallOffsetLeftV2",
            "NPSDrivetrainRudderSmallOffsetRightV2"
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
        public enum EngineType { Diesel, GasTurbine, SteamTurbine, Electric }
        public struct EfficiencyPoint
        {
            public float Throttle;
            public float Multiplier;

            public EfficiencyPoint(float t, float m)
            {
                Throttle = t;
                Multiplier = m;
            }
        }

        //Engine stats assignment
        public static readonly Dictionary<string, EngineStats> EngineSettings = new Dictionary<string, EngineStats>
        {
            //Gas Turbines
            {"NPSDieselTurbine2MW", new EngineStats { Type = EngineType.GasTurbine, MaxMW = 2, RequiredReduction = 2, FuelRate = 19.5f, SpoolTime = 28 } },
            {"NPSDieselTurbine5MW", new EngineStats { Type = EngineType.GasTurbine, MaxMW = 5, RequiredReduction = 2, FuelRate = 48.75f, SpoolTime = 32 } },
            {"NPSDieselTurbine12MW", new EngineStats { Type = EngineType.GasTurbine, MaxMW = 12, RequiredReduction = 2, FuelRate = 117.0f, SpoolTime = 36 } },
            {"NPSDieselTurbine25MW", new EngineStats { Type = EngineType.GasTurbine, MaxMW = 25, RequiredReduction = 2, FuelRate = 243.75f, SpoolTime = 40 } },
            {"NPSDieselTurbine40MW", new EngineStats { Type = EngineType.GasTurbine, MaxMW = 40, RequiredReduction = 2, FuelRate = 390.0f, SpoolTime = 44 } },
            //Internal Combustion Diesel
            {"NPSDieselEngine500KW", new EngineStats { Type = EngineType.Diesel, MaxMW = 0.5f, RequiredReduction = 1, FuelRate = 3.75f, SpoolTime = 4f } },
            {"NPSDieselEngine15MW", new EngineStats { Type = EngineType.Diesel, MaxMW = 1.5f, RequiredReduction = 1, FuelRate = 11.25f, SpoolTime = 6f } },
            {"NPSDieselEngine25MW", new EngineStats { Type = EngineType.Diesel, MaxMW = 2.5f, RequiredReduction = 1, FuelRate = 18.75f, SpoolTime = 8f } },
        };

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

        public static readonly Dictionary<string, PropellerStats> PropellerSettings = new Dictionary<string, PropellerStats>
        {
            {"NPSDrivetrainProp34", new PropellerStats { MaxMW = 3.5f, SpoolTime = 20f } },
            {"NPSDrivetrainProp44", new PropellerStats { MaxMW = 4.6f, SpoolTime = 22f } },
            {"NPSDrivetrainProp54", new PropellerStats { MaxMW = 5.4f, SpoolTime = 24f } },
            {"NPSDrivetrainProp38", new PropellerStats { MaxMW = 3.5f, SpoolTime = 32f } },
            {"NPSDrivetrainProp48", new PropellerStats { MaxMW = 4.6f, SpoolTime = 36f } },
            {"NPSDrivetrainProp58", new PropellerStats { MaxMW = 5.4f, SpoolTime = 40f } },
        };

        public static readonly Dictionary<string, GearboxStats> GearboxSettings = new Dictionary<string, GearboxStats>
        {
            {"NPSDrivetrainMRG", new GearboxStats { ReductionLevel = 2, IsClutched = true } },
            {"NPSGearbox_DoublePlanetary", new GearboxStats { ReductionLevel = 2, IsClutched = true } },
        };

        public static readonly float[,] EnthalpyTable = new float[5, 5] {
            { 2733, 0, 0, 0, 0 },           // 400K
            { 2926, 2883, 2828, 0, 0 },      // 500K
            { 3074, 3052, 3026, 2966, 2888 },// 600K
            { 3224, 3211, 3197, 3167, 3134 },// 700K
            { 3448, 3440, 3432, 3415, 3397 } // 800K
        };
    }

    public class EngineStats
    {
        public EngineType Type;
        public float MaxMW;         //Soft cap max output power - Mechanical only
        public int RequiredReduction; //Required level of reduction to not damage propeller
        public float FuelRate;      //Fuel consumption at max output in liters/second - Multiplied by globalFuelMult for actual consumption
        public float SpoolTime;       //How fast the engine responds to throttle changes at low throttle
        public int StartupTicks;    //Number of ticks to go from stopped to running
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

    public class PropellerStats
    {
        public float MaxMW;         //Soft cap max input power
        public float SpoolTime;       //Amount of time it takes to change output
    }

    public class GearboxStats
    {
        public int ReductionLevel;       //Number of reduction steps
        public bool IsClutched;           //Whether or not the gearbox has a clutch, allowing it to disconnect the engine from the drivetrain
    }

    public static class DieselEngineConfigs
    {
        public static readonly EfficiencyPoint[] DieselFuelTable = {
            new EfficiencyPoint(0.00f, 0.05f), // Idle
            new EfficiencyPoint(0.40f, 0.35f), // Cruising
            new EfficiencyPoint(1.00f, 1.00f), // Rated Max
            new EfficiencyPoint(1.25f, 1.80f)  // Emergency
            };
    }

    public static class TurbineEngineConfigs
    {
        public static readonly EfficiencyPoint[] TurbineFuelTable = {
            new EfficiencyPoint(0.00f, 0.20f), // Idle
            new EfficiencyPoint(0.30f, 0.45f), // Low Power
            new EfficiencyPoint(0.70f, 0.75f), // Cruising
            new EfficiencyPoint(1.00f, 1.00f), // Rated Max
            new EfficiencyPoint(1.25f, 2.25f)  // Emergency
            };
    }
}
