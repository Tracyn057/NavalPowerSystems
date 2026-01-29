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
        public static readonly HashSet<string> Boilers = new HashSet<string>()
        {

        };

        public static readonly HashSet<string> SteamTurbines = new HashSet<string>()
        {

        };

        public static readonly HashSet<string> GasTurbines = new HashSet<string>()
        {

        };

        public static readonly HashSet<string> SSTGs = new HashSet<string>()
        {
            
        };

        //Component stats definition
        public class BoilerStats
        {
            public float BoilerBar;     //Max pressure boiler can produce
            public float BoilerFlow;    //Max flow the boiler can put out in kg of steam
            public float BoilerFuel;    //How much fuel the boiler uses per kg of steam - Multiplied by globalFuelMult
        }

        public class SteamTurbineStats
        {
            public float TurbineBar;    //Max pressure the turbine needs to operate at max capacity
            public float TurbineFlow;   //Required flow to operate at max capacity
            public float TurbineMW;     //Max MW production of the turbine at 100% - This gets converted to MN, not used for electricity
        }

        public class GasTurbineStats
        {
            public float GasTurbineMW;      //Max MW output of the turbine - Can be used for electricity or converted to MN
            public float GasTurbineFuel;    //Fuel consumption at max output - Multiplied by globalFuelMult
        }

        public class SSTGStats
        {
            public float SSTGMW;        //Max output MW of the generator - Only for electricity production
            public float SSTGPctSteam;  //Percent of steam siphoned from the steam line - 1.0=100%
        }

        public class PropellerStats
        {
            public float PropellerMW;       //Max input MW the propeller can handle - More will damage
            public float PropellerPctDmg;   //Percent block damage per second per MW above rated output, 1.0=100%
        }

        //Component stats assignment
        public static readonly Dictionary<string, BoilerStats> BoilerSettings = new Dictionary<string, BoilerStats>()
        {
            
        };

        public static readonly Dictionary<string, SteamTurbineStats> SteamTurbineSettings = new Dictionary<string, SteamTurbineStats>
        {
            
        };

        public static readonly Dictionary<string, GasTurbineStats> GasTurbineSettings = new Dictionary<string, GasTurbineStats>
        {
            
        };

        public static readonly Dictionary<string, SSTGStats> SSTGSettings = new Dictionary<string, SSTGStats>
        {
            
        };

        public static readonly Dictionary<string, PropellerStats> PropellerSettings = new Dictionary<string, PropellerStats>
        {
            
        };
    }
}
