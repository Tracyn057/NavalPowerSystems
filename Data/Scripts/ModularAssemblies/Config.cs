using System.Collections.Generic;
using System.Linq;
using static NavalPowerSystems.Config;

namespace NavalPowerSystems
{
    public static class Config
    {
        //Constant variables
        public const float globalFuelMult = 0.75f;          //Multiplier for fuel consumption
        public const bool requiresMaintenance = true;    //Whether or not to apply wear and tear to engines and propellers, causing them to lose efficiency and eventually fail without repairs
        public const float cavitationDmgMult = 0.1f;      //Multiplier for damage caused by cavitation, applied to propeller blocks
        public const float throttleVariance = 0.015f;    //Amount of random variance in throttle response
        public const float mnPerMW = 0.2f;             //MN produced per MW
        public const float hpPerMW = 1341.02f;          //Horsepower made per MW, for display only
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
        public static string[] EngineSubtypes = new string[]
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

        public static readonly string[] PropellerSubtypes = new string[]
        {
            "NPSDrivetrainProp33",
        };

        public static string[] DriveshaftSubtypes = new string[]
        {
            "NPSDrivetrainShaftTubeEndVertical"
        };



        //Component stats definition
        public enum EngineType { Diesel, Turbine }
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
            {"NPSDieselTurbine2MW", new EngineStats { Type = EngineType.Turbine, MaxMW = 2, FuelRate = 19.5f, SpoolTime = 28 } },
            {"NPSDieselTurbine5MW", new EngineStats { Type = EngineType.Turbine, MaxMW = 5, FuelRate = 48.75f, SpoolTime = 32 } },
            {"NPSDieselTurbine12MW", new EngineStats { Type = EngineType.Turbine, MaxMW = 12, FuelRate = 117.0f, SpoolTime = 36 } },
            {"NPSDieselTurbine25MW", new EngineStats { Type = EngineType.Turbine, MaxMW = 25, FuelRate = 243.75f, SpoolTime = 40 } },
            {"NPSDieselTurbine40MW", new EngineStats { Type = EngineType.Turbine, MaxMW = 40, FuelRate = 390.0f, SpoolTime = 44 } },
            //Internal Combustion Diesel
            {"NPSDieselEngine500KW", new EngineStats { Type = EngineType.Diesel, MaxMW = 0.5f, FuelRate = 3.75f, SpoolTime = 4f } },
            {"NPSDieselEngine15MW", new EngineStats { Type = EngineType.Diesel, MaxMW = 1.5f, FuelRate = 11.25f, SpoolTime = 6f } },
            {"NPSDieselEngine25MW", new EngineStats { Type = EngineType.Diesel, MaxMW = 2.5f, FuelRate = 18.75f, SpoolTime = 8f } },
        };

        public static readonly Dictionary<string, PropellerStats> PropellerSettings = new Dictionary<string, PropellerStats>
        {
            {"NPSDrivetrainProp33", new PropellerStats { MaxMW = 3.5f, SpoolTime = 18f } },
        };
    }

    public class EngineStats
    {
        public EngineType Type;
        public float MaxMW;         //Soft cap max output power - Mechanical only
        public float FuelRate;      //Fuel consumption at max output in liters/second
        public float SpoolTime;       //How fast the engine responds to throttle changes at low throttle
    }

    public class PropellerStats
    {
        public float MaxMW;         //Soft cap max input power
        public float SpoolTime;       //Amount of time it takes to change output
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
