using System.Collections.Generic;

namespace NavalPowerSystems
{
    public static class Config
    {
        //Constant variables
        public const float globalFuelMult = 62;         //Multiplier for fuel consumption
        public const float drivetrainLoss = 0.0467f;    //Percent loss of MW (MN) over the drivetrain
        public const float hpPerKGSteam = 0.52f;        //Horsepower per kg of steam, for display only
        public const float mwPerMN = 12.52f;            //MNs of force for every MW of input
        public const float hpPerMW = 1341.02f;          //Horsepower made per MW, for display only
        public const float kgFuelSteamRatio = 0.07f;    //Amount of fuel burned per kg of steam, multiplied by global fuel mult
        public const float crudeFuelOilRatio = 0.75f;   //Ratio of conversion from crude oil to fuel oil
        public const float fuelOilDieselRatio = 0.66f;  //Ratio of conversion from fuel oil to diesel fuel
        public const float baseRefineRate = 100;        //Base rate in liters for oil cracker and refinery

        //Oil extraction variables
        public const double rarityThreshold = 0.8;      //How much of the available oil spawn locations are empty. 1.0 = No oil ever 
        public const double gridSize = 1000;            //Size of grid to determine oil deposits
        public const double baseRadius = 150;           //Radius from center of grid for deposit size
        public const float derrickExtractRate = 15.0f;  //Base liters per second extraction rate
        public const float derrickOceanMult = 3.5f;     //Multiplier for extraction rate for deep sea drill platforms
        public const int minWaterDepth = 25;            //Distance in m to utilize OceanMult
        public const int scanSize = 500;                //Grid size for the LCD scan component

        //Viable component lists

        public static readonly HashSet<string> GasTurbines = new HashSet<string>()
        {
            "NPSDieselTurbine2MW",
            "NPSDieselTurbine5MW",
            "NPSDieselTurbine12MW",
            "NPSDieselTurbine25MW",
            "NPSDieselTurbine40MW"
        };

        public static readonly HashSet<string> DieselEngines = new HashSet<string>()
        {
            "NPSDieselEngine500KW",
            "NPSDieselEngine15MW",
            "NPSDieselEngine25MW"
        };

        public static readonly HashSet<string> Propellers = new HashSet<string>()
        {

        };

        //Component stats definition

        public class GasTurbineStats
        {
            public float GasTurbineMW;      //Max MW output - Mechanical only
            public float GasTurbineFuel;    //Fuel consumption at max output - Multiplied by globalFuelMult
        }

        public class DieselEngineStats
        {
            public float DieselEngineMW;    //Max MW output - Mechanical only
            public float DieselEngineFuel;  //Fuel consumption at max output - Multiplied by globalFuelMult
        }

        public class PropellerStats
        {
            public float PropellerMW;       //Max input MW the propeller can handle - More will damage
            public float PropellerMN;        //Max output without override
        }

        //Component stats assignment
        public static readonly Dictionary<string, GasTurbineStats> GasTurbineSettings = new Dictionary<string, GasTurbineStats>
        {
            
        };

        public static readonly Dictionary<string, DieselEngineStats> DieselSettings = new Dictionary<string, DieselEngineStats>
        {

        };

        public static readonly Dictionary<string, PropellerStats> PropellerSettings = new Dictionary<string, PropellerStats>
        {
            
        };
    }
}
