using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI;

namespace NavalPowerSystems.Drivetrain.Producers.Steam
{
    public abstract class SteamSystemPart<T> : DrivetrainPart<T>, ISteamSystemPart
        where T : class, IMyCubeBlock
    {
        public float Bar_In { get; set; }
        public float Bar_Out { get; set; }
        public float Flow_In { get; set; }
        public float Flow_Out { get; set; }
        public float Enthalpy_In { get; set; }
        public float Enthalpy_Out { get; set; }
        public float SteamQuality { get; set; }
    }

    public interface ISteamSystemPart
    {
        float Bar_In { get; set; }
        float Bar_Out { get; set; }
        float Flow_In { get; set; }
        float Flow_Out { get; set; }
        float Enthalpy_In { get; set; }
        float Enthalpy_Out { get; set; }
        float SteamQuality { get; set; }
    }
}
