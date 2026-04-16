using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;

namespace NavalPowerSystems.Drivetrain_V2
{
    public interface IDrivetrainNode
    {
        double InputLoad { get; set; }
        double InputRPM { get; set; }
        void CalculateLoad(double downstreamDemand, double downstreamRPM);

        double OutputTorque { get; }
        double OutputRPM { get; }
        void CalculateOutput(double upstreamTorque, double upstreamRPM);
    }

    public class EngineNode : IDrivetrainNode
    {
        public EngineLogic_V2 EngineLogic;
        public IMyCubeBlock EngineBlock;
        public double PeakRPM;
        public double PeakTorque;
        public double HeatRate;
        public float PowerCurveConstant;
        public double EngineInertia;
        public double SystemInertia;
        public double CurrentFuelUse;
        public double CurrentO2Use;
        public bool CanTwerk;

        public GearboxNode ConnectedGearboxNode = null;
        public float RequestedThrottle { get; set; }
        public double CurrentRPM { get; private set; }
        public double CurrentTorque { get; private set; }
        public double TorqueLoad { get; set; }
        public double RPMVarianceMult = 0.02;
        public double ClutchEngagement = 0; //Variable stored locally for use by gearbox nodes in clutch logic, not an actual variable for the engine node
        public bool ClutchLocked { get; set; } = true; //Start with clutch locked. Gearbox Logic will handle lock and unlock.
        public double InputLoad { get; set; }
        public double InputRPM { get; set; }
        public double OutputRPM { get; private set; }
        public double OutputTorque { get; private set; }

        public void CalculateLoad(double downstreamDemand, double downstreamRPM) 
        {
            //End of the line for torque demand. This is here for completeness, but the engine doesn't pass this information at all.
            InputLoad = downstreamDemand;
            InputRPM = downstreamRPM;
        }

        public void CalculateOutput(double upstreamTorque, double upstreamRPM)
        {
            //Check if the engine is even running
            double availableTorque = 0;
            if (this.CanTwerk)
            {
                //Calculate available torque based on power curve
                double deviation = (CurrentRPM - PeakRPM) / PeakRPM;
                availableTorque = PeakTorque * (1 - PowerCurveConstant * Math.Pow(deviation, 2));
                availableTorque = Math.Max(availableTorque, 0);
            }

            //Set target RPM based on throttle, allowing for minimum idle speed and add some cool flutter
            float localThrottleRequest = 0f;
            if (RequestedThrottle < 0.04f) localThrottleRequest = 0.04f;
            else localThrottleRequest = RequestedThrottle;

            var variance = PeakRPM * RPMVarianceMult;
            var targetRPM = (localThrottleRequest * PeakRPM) + VRage.Utils.MyUtils.GetRandomDouble(-variance, variance);
            double governorRange = PeakRPM * 0.05; //RPM range the governor will adjust torque to try to reach target RPM
            double governorMult = (targetRPM - CurrentRPM) / governorRange;
            governorMult = MathHelper.Clamp(governorMult, 0, 1);
            CurrentTorque = availableTorque * governorMult;

            //Finish calculating RPM and Torque to send upstream
            var netTorque = CurrentTorque - InputLoad;
            var angularAcceleration = netTorque / (EngineInertia + SystemInertia);
            var changeInRPM = angularAcceleration * 9.5488 * (1.0 / 60.0);
            CurrentRPM += changeInRPM;
            CurrentRPM = Math.Max(CurrentRPM, 0);

            this.OutputTorque = CurrentTorque;
            this.OutputRPM = CurrentRPM;

            //Burninate
            CalculateResourceUse();
            //Send to logic for UI
            EngineLogic.CurrentRPM = CurrentRPM;
            EngineLogic.CurrentTorque = CurrentTorque;

            //Pass it on. An engine can only ever have on gearbox connection
            if (ConnectedGearboxNode != null)
            {
                ConnectedGearboxNode.CalculateOutput(OutputTorque, OutputRPM);
            }
        }

        private void CalculateResourceUse()
        {
            if (CurrentRPM <= 0 || CurrentTorque <= 0)
            {
                CurrentFuelUse = 0;
                CurrentO2Use = 0;
                return;
            }

            double powerKw = CurrentTorque * CurrentRPM / 9.5488; //KW
            double requiredEnergy = powerKw * HeatRate / 3600; //Convert kW to kJ/s
            CurrentFuelUse = (float)(requiredEnergy / Drivetrain_Config.DieselEnergyDensity * Config.globalFuelMult);
            CurrentO2Use = CurrentFuelUse * 3.5f; //Approximate O2 use based on fuel use

            if (EngineLogic != null)
            {
                EngineLogic.CurrentFuelUse = CurrentFuelUse;
                EngineLogic.CurrentO2Use = CurrentO2Use;
            }
        }
    }

    public class GearboxNode : IDrivetrainNode
    {
        public GearboxLogic_V2 GearboxLogic;
        public IMyCubeBlock GearboxBlock;
        public HashSet<EngineNode> ConnectedEngineNodes = new HashSet<EngineNode>();
        public HashSet<GearboxNode> GearboxNodesTowardsEngines = new HashSet<GearboxNode>();
        public HashSet<GearboxNode> GearboxNodesTowardsPropellers = new HashSet<GearboxNode>();
        public HashSet<PropellerNode> ConnectedPropellerNodes = new HashSet<PropellerNode>();
        public double GearRatio;
        public float BrakeEngagement { get; set; } = 1f;

        public double InputLoad { get; set; }
        public double InputRPM { get; set; }
        public double OutputTorque { get; private set; }
        public double OutputRPM { get; private set; }

        public void CalculateLoad(double downstreamDemand, double downstreamRPM)
        {
            //Sending torque information up the line doesn't need any super fancy code
            this.InputLoad = downstreamDemand / GearRatio; //Reduce load by gear ratio
            this.InputRPM = downstreamRPM * GearRatio; //Increase RPM by gear ratio
            GearboxLogic.IncomingRPM = InputRPM;

            if (BrakeEngagement > 0f)
            {
                var brakeTorque = BrakeEngagement * GearboxLogic.GearboxStats.MaxBrakeTorque; //Calculate brake torque based on engagement and max brake torque
                this.InputLoad += brakeTorque / GearRatio; //Add brake torque to load, which will reduce engine
            }

            if (GearboxNodesTowardsEngines.Count > 0 && ConnectedEngineNodes.Count > 0)
            {
                InputLoad = InputLoad / (GearboxNodesTowardsEngines.Count + ConnectedEngineNodes.Count); //Split load between multiple upstream gearboxes and engines
            }
             else if (GearboxNodesTowardsEngines.Count > 0)
            {
                InputLoad = InputLoad / GearboxNodesTowardsEngines.Count; //Split load between multiple upstream gearboxes
            }
            else if (ConnectedEngineNodes.Count > 0)
            {
                InputLoad = InputLoad / ConnectedEngineNodes.Count; //Split load between multiple engines
            }

            //Pass it on towards the engines or upstream gearboxes
            if (GearboxNodesTowardsEngines.Count > 0)
            {
                foreach (var gearbox in GearboxNodesTowardsEngines)
                {
                    gearbox.CalculateLoad(InputLoad, InputRPM);
                }
            }
            if (ConnectedEngineNodes.Count > 0)
            {
                foreach (var engine in ConnectedEngineNodes)
                {
                    engine.CalculateLoad(InputLoad, InputRPM);
                }
            }
        }

        public void CalculateOutput(double upstreamTorque, double upstreamRPM)
        {
            double engineInputTorque = 0;
            double gearboxInputTorque = 0;
            double totalInputTorque = 0;

            //Clutch logic.
            if (ConnectedEngineNodes.Count > 0)
            {
                double targetSyncRPM = this.InputRPM * GearRatio;
                foreach (var engine in ConnectedEngineNodes)
                {
                    if (!engine.EngineBlock.IsWorking || engine.ClutchLocked) continue;
                    double rpmDifference = Math.Abs(engine.CurrentRPM - targetSyncRPM);
                    double engagementWindow = targetSyncRPM * 0.05; //RPM range within which clutch can engage

                    double clutched = 1 - MathHelper.Clamp(rpmDifference / engagementWindow, 0, 1);
                    engine.ClutchEngagement = Math.Pow(clutched, 2);

                    double viscousClutch = 0;
                    if (engine.RequestedThrottle > 0f)
                        viscousClutch = 0.05; //Small amount of clutch engagement at low throttle to help ease into motion
                    engine.ClutchEngagement = Math.Max(engine.ClutchEngagement, viscousClutch);
                    engineInputTorque += engine.OutputTorque * engine.ClutchEngagement;
                }
            }
            //Collect from upstream gearboxes
            if (GearboxNodesTowardsEngines.Count > 0)
            {
                foreach (var gearbox in GearboxNodesTowardsEngines)
                {
                    gearboxInputTorque += gearbox.OutputTorque;
                }
            }

            totalInputTorque = (engineInputTorque + gearboxInputTorque) * GearRatio; //Increase torque by gear ratio for output

            if (BrakeEngagement > 0f)
            {
                var brakeTorque = BrakeEngagement * GearboxLogic.GearboxStats.MaxBrakeTorque; //Calculate brake torque based on engagement and max brake torque
                totalInputTorque -= brakeTorque; //Add brake torque to total input torque, which will reduce output torque
            }

            this.OutputTorque = Math.Max(0, totalInputTorque)
            ;
            if (upstreamRPM > 0 || upstreamTorque > 0) 
                this.OutputRPM = upstreamRPM / GearRatio; //Decrease RPM by gear ratio
            else 
                this.OutputRPM = 0; //Divide by zero safety

            //Pass it on down the line
            var downstreamCount = GearboxNodesTowardsPropellers.Count + ConnectedPropellerNodes.Count;

            if (GearboxNodesTowardsPropellers.Count > 0)
                foreach (var gearbox in GearboxNodesTowardsPropellers)
                    gearbox.CalculateOutput(OutputTorque / downstreamCount, OutputRPM);

            if (ConnectedPropellerNodes.Count > 0)
                foreach (var propeller in ConnectedPropellerNodes)
                    propeller.CalculateOutput(OutputTorque / downstreamCount, OutputRPM);
        }
    }

    public class PropellerNode : IDrivetrainNode
    {
        public IMyCubeBlock PropellerBlock;
        public IMyCubeGrid PropellerGrid;
        public PropellerLogic_V2 PropellerLogic;
        public double Diameter;
        public double Inertia;
        public double ShaftInertia;
        public double TorqueCoefficient;
        public double ThrustCoefficient;
        public bool IsCRP;
        public double PitchRatio;

        public const double WaterDensity = 1024;
        public GearboxNode ConnectedGearboxNode = null;
        public double InputLoad { get; set; }
        public double InputRPM { get; set; } = 0;
        public double OutputTorque { get; private set; }
        public double OutputRPM { get; private set; } = 0;

        public void CalculateLoad(double downstreamDemand, double downstreamRPM)
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
            InputRPM = downstreamRPM;
            PropellerLogic.IncomingRPM = InputRPM;

            //Pass it on
            if (ConnectedGearboxNode != null)
            {
                ConnectedGearboxNode.CalculateLoad(InputLoad, OutputRPM);
            }
        }

        public void CalculateOutput(double upstreamTorque, double upstreamRPM)
        {
            //End of the line for incoming torque and RPM. Need to still update variables for force calculation and UI display.
            OutputTorque = upstreamTorque;
            OutputRPM = upstreamRPM;

            //Move thrust application logic from node system directly to block logic. Separates node system from game world.

            PropellerLogic.IncomingThrust = CalculateForce();
        }

        private double CalculateForce()
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

            return thrustForce;
        }

        private double GetPitchRatio()
        {
            //Placeholder for future CRP logic, for now just return a constant
            return PitchRatio = Drivetrain_Config.PitchRatio;
        }
    }

    public class TurbineNode : IDrivetrainNode
    {
        public double InputLoad { get; set; }
        public double InputRPM { get; set; }
        public double OutputTorque { get; private set; }
        public double OutputRPM { get; private set; }

        public void CalculateLoad(double downstreamDemand, double downstreamRPM)
        {
            //Turbines will operate similarly to engines, but using steam instead
            InputLoad = downstreamDemand;
            InputRPM = downstreamRPM;
        }

        public void CalculateOutput(double upstreamTorque, double upstreamRPM)
        {
            //Turbine output will be calculated based on a separate steam system that relays steam to the turbine based on demand
            OutputTorque = upstreamTorque;
            OutputRPM = upstreamRPM;
        }
    }

    public class MotorNode : IDrivetrainNode
    {
        public double InputLoad { get; set; }
        public double InputRPM { get; set; }
        public double OutputTorque { get; private set; }
        public double OutputRPM { get; private set; }

        public void CalculateLoad(double downstreamDemand, double downstreamRPM)
        {
            //Motors will still use input torque and rpm to calculate their power usage
            InputLoad = downstreamDemand;
            InputRPM = downstreamRPM;
        }

        public void CalculateOutput(double upstreamTorque, double upstreamRPM)
        {
            //Motor output will need to be calculated based on electrical input and motor characteristics
            OutputTorque = upstreamTorque;
            OutputRPM = upstreamRPM;
        }
    }

    public class GeneratorNode : IDrivetrainNode
    {
        public double InputLoad { get; set; }
        public double InputRPM { get; set; }
        public double OutputTorque { get; private set; }
        public double OutputRPM { get; private set; }

        public void CalculateLoad(double downstreamDemand, double downstreamRPM)
        {
            //Generators will take mechanical input and convert it to electrical output
            InputLoad = downstreamDemand;
            InputRPM = downstreamRPM;
        }

        public void CalculateOutput(double upstreamTorque, double upstreamRPM)
        {
            //Generator will operate similar to propeller nodes in that it's the end of the output line for torque and RPM, but will convert that to electrical output instead of thrust
            OutputTorque = upstreamTorque;
            OutputRPM = upstreamRPM;
        }
    }
}