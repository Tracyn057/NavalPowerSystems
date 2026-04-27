using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI;
using VRageMath;
using static NavalPowerSystems.Drivetrain.Producers.Steam.SteamTables;

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

        private readonly float[] _pSteps = { 1f, 15f, 30f, 45f, 60f, 75f, 90f, 105f, 125f, 145f };
        private readonly float[] _tSteps = { 300f, 350f, 400f, 450f, 500f, 550f, 600f, 650f, 750f, 850f };

        public SteamState GetSteamState(float temp, float pressure)
        {
            int t0 = 0;
            for (int i = 0; i < _tSteps.Length - 1; i++)
            {
                if (temp >= _tSteps[i]) t0 = i;
                else break;
            }

            int p0 = 0;
            for (int i = 0; i < _pSteps.Length - 1; i++)
            {
                if (pressure >= _pSteps[i]) p0 = i;
                else break;
            }

            t0 = MathHelper.Clamp(t0, 0, 8);
            p0 = MathHelper.Clamp(p0, 0, 8);

            float weightT = (temp - _tSteps[t0]) / (_tSteps[t0 + 1] - _tSteps[t0]);
            float weightP = (pressure - _pSteps[p0]) / (_pSteps[p0 + 1] - _pSteps[p0]);

            return new SteamState
            {
                Enthalpy = SmoothLookup(SteamTables.EnthalpyTable, t0, p0, weightT, weightP),
                SpecificVolume = SmoothLookup(SteamTables.SpecVolumeTable, t0, p0, weightT, weightP)
            };
        }

        public float SmoothLookup(float[,] table, int tIndex, int pIndex, float weightT, float weightP)
        {
            float bottomLeft = table[tIndex, pIndex];
            float bottomRight = table[tIndex, pIndex + 1];
            float topLeft = table[tIndex + 1, pIndex];
            float topRight = table[tIndex + 1, pIndex + 1];

            float bottomBlend = bottomLeft + (bottomRight - bottomLeft) * weightP;

            float topBlend = topLeft + (topRight - topLeft) * weightP;

            return bottomBlend + (topBlend - bottomBlend) * weightT;
        }
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
