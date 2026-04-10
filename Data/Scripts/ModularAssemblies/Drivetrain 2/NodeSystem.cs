


using NavalPowerSystems.Drivetrain;

namespace NavalPowerSystems.Drivetrain_V2
{
    public interface IDrivetrainNode
    {
        double InputLoad { get; set; }
        void CalculateLoad(double downstreamDemand);

        double OutputTorque { get; }
        double OutputRPM { get; }
        void CalculateOutput(double upstreamTorque, double upstreamRPM);
    }

    public class EngineNode : IDrivetrainNode
    {
        public IMyCubeBlock EngineBlock;
        public NewEngineLogic EngineLogic;
        public double PeakRPM;
        public double PeakTorque;
        public double HeatRate;
        public float PowerCurveConstant;
        public double EngineInertia;
        public double SystemInertia;

        public float RequestedThrottle { get; set; }
        public double CurrentRPM { get; private set; }
        public double CurrentTorque { get; private set; }
        public double TorqueLoad { get; set; }
        public double RPMVarianceMult = 0.02;

        public double InputLoad { get; set; }
        public double OutputTorque => CurrentTorque;
        public double OutputRPM => CurrentRPM;

        public void CalculateLoad(double downstreamDemand) 
        {
            InputLoad = downstreamDemand; 
        }

        public void CalculateOutput(double upstreamTorque, double upstreamRPM)
        {
            if (RequestedThrottle == 0f)
            {
                RequestedThrottle = 0.08f;
            }
            // Induce engine RPM flutter
            var variance = PeakRPM * RPMVarianceMult;
            var targetRPM = (int)(RequestedThrottle * (PeakRPM * 1.15)) + VRage.Utils.MyUtils.GetRandomDouble(-variance, variance); // Allow for some overspeed

            double deviation = (CurrentRPM - PeakRPM) / PeakRPM;
            double availableTorque = PeakTorque * (1 - PowerCurveConstant * Math.Pow(deviation, 2));
            availableTorque = Math.Max(availableTorque, 0);

            double governorRange = PeakRPM * 0.05; // RPM range over which the governor will adjust torque to try to reach target RPM
            double governorMult = (targetRPM - CurrentRPM) / governorRange;
            governorMult = MathHelper.Clamp(governorMult, 0, 1);
            CurrentTorque = availableTorque * governorMult;

            TorqueLoad = 0;
            var netTorque = CurrentTorque - TorqueLoad;
            var angularAcceleration = netTorque / (EngineInertia+SystemInertia);
            var changeInRPM = angularAcceleration * 9.5488 * (1f / 60f);
            CurrentRPM += changeInRPM;

            EngineLogic.CurrentRPM = CurrentRPM;
            EngineLogic.CurrentTorque = CurrentTorque;
        }
    }

    public class GearboxNode : IDrivetrainNode
    {
        public IMyCubeBlock GearboxBlock;
        public double GearRatio;

        public double InputLoad { get; set; }
        public double OutputTorque { get; private set; }
        public double OutputRPM { get; private set; }

        public void CalculateLoad(double downstreamDemand)
        {
            InputLoad = downstreamDemand / GearRatio;
        }

        public void CalculateOutput(double upstreamTorque, double upstreamRPM)
        {
            OutputTorque = upstreamTorque * GearRatio;
            OutputRPM = upstreamRPM / GearRatio;
        }
    }

    public class PropellerNode : IDrivetrainNode
    {
        public IMyCubeBlock PropellerBlock;
        public IMyCubeGrid PropellerGrid;
        public double Diameter;
        public double Inertia;
        public double ShaftInertia;
        public double TorqueCoefficient;
        public double ThrustCoefficient;
        public bool IsCRP;
        public double PitchRatio;

        public const double WaterDensity = 1024;
        public double InputLoad { get; set; }
        public double OutputTorque { get; private set; }
        public double OutputRPM { get; private set; }

        public void CalculateLoad(double downstreamDemand)
        {
            //Get grid velocity to calculate advance ratio
            double velocity = PropellerBlock.CubeGrid.Physics?.LinearVelocity.Length() ?? 0;

            //Rippums to Rippis
            double RPS = OutputRPM / 60;
            double advanceRatio = (RPS > 0.1) ? velocity / (RPS * Diameter) : 0;

            if (IsCRP)
            {
                PitchRatio = GetPitchRatio();
            }

            //Calculate torque
            double currentTorque = TorqueCoefficient * MathHelper.Clamp(1 - (advanceRatio / PitchRatio), 0.1, 1.0);
            double torqueDemand = currentTorque * PitchRatio * Math.Pow(RPS, 2) * Math.Pow(Diameter, 5);

            InputLoad = torqueDemand;
        }

        public void CalculateOutput(double upstreamTorque, double upstreamRPM)
        {
            OutputTorque = upstreamTorque;
            OutputRPM = upstreamRPM;

            ApplyForce();
        }

        private void ApplyForce()
        {
            //Get grid velocity for advance ratio
            double velocity = PropellerBlock.CubeGrid.Physics?.LinearVelocity.Length() ?? 0;
            //Rippums to Rippis
            double RPS = OutputRPM / 60;
            double advanceRatio = (RPS > 0.1) ? velocity / (RPS * Diameter) : 0;

            if (IsCRP)
            {
                PitchRatio = GetPitchRatio();
            }

            double currentThrust = ThrustCoefficient * MathHelper.Clamp(1 - (advanceRatio / PitchRatio), 0.1, 1.0);
            double thrustForce = currentThrust * WaterDensity * Math.Pow(RPS, 2) * Math.Pow(Diameter, 4);

            if (Math.Abs(thrustForce) >100)
            {
                Vector3D thrustVector = PropellerBlock.WorldMatrix.Backward * (float)thrustForce;
                var BlockPos = PropellerBlock.PositionComp.GetPosition();
                PropellerGrid.Physics.AddForce(
                MyPhysicsForceType.APPLY_WORLD_FORCE,
                thrustVector,
                BlockPos,
                null
                );
            }
        }

        private void GetPitchRatio()
        {
            //Placeholder for future CRP logic, for now just return a constant
            return PitchRatio = Drivetrain_Config.PitchRatio;
        }
    }
}