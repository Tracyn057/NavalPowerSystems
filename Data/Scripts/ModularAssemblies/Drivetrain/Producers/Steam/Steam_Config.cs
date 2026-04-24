using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavalPowerSystems.Drivetrain.Producers.Steam
{
    public class Steam_Config
    {
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
