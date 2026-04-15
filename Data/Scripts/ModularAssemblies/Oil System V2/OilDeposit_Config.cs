using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackGold
{
    public class OilSystem_Config
    {
        public float DetectorRangeMeters = 2500f;
        public float OilDepositDepthMin = 100f;
        public float OilDepositDepthMax = 1500f;
        public bool DepositIsFinite = false;
        public float FiniteDepositSizeMultiplier = 1f;
        public float FiniteDepositYieldLitersMinimum = 50000f;
        public float FiniteDepositYieldLitersMaximum = 5000000;
        public float DetectorGridSizeInMeters = 500f;
        public float DepositRarityThreshold = 0.8f;
    }
}