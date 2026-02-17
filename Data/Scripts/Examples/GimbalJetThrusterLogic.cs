using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Sandbox.Game.Localization;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game.Components;
using VRage.Game.ModAPI.Network;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRage.Utils;
using VRageMath;

using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;


namespace Humanoid.GimbalJetThruster
{

	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_FunctionalBlock), false, "GimbalJetEngine", "GimbalJetEngine3x3")] // Block SubtypeID
	internal class GimbalJetThruster : MyGameLogicComponent
	{
		private const bool EnableDebugDraw = false;                 // enable debug draw for damage (red line) and force direction (blue line, points in the opposite direction of actual force)

		// Base Stats
		public const double JETGimbalAngleDegrees = 10;                 // gimbal angle in degrees, affects force direction
		public const double JETNozzleAngleDegrees = 10;                 // angle for nozzle animation in degrees (the one which changes the aperture size), is purely visual
		private float JETDamageLength = 4.5f;                       // length of damage raycast of JET flame (starts from empty position, doesn't move with gimbal for performance reasons)
		private float JETDamageStrength = 100f;                     // Amount of damage done
		private float JETForceMagnitude = 99000f;                   // maximum output of a thruster
		private float FuelConsumption = 20f;                        // consumption per second at full thrust
		private float OxygenConsumption = 200f;

		private const bool NeedsAtmosphereToWork = true;                // when true, thruster wont work outside atmo
		private const float MinAtmoPressure = 0.1f;                 // thruster will stop working when atmo pressure gets lower than this

		// Particle Effect for 3x3 version
		private const string JETLargeParticle = "GimbalJetThrusterParticle";        // SubtypeID of particle effect
		private const float JETLargeParticleScale = 1f;                 // Thruster particle scale multiplier

		// Particle Effect for 1x1 version
		private const string JETSmallParticle = "GimbalJetThrusterParticle";
		private const float JETSmallParticleScale = 0.375f;

		// SFX
		private const string JETSoundID = "GimbalJetEngineSound";           // SubtypeID of main sound effect
		private const string JETSoundFarID = "GimbalJetEngineSoundFar";         // SubtypeID of distant sound effect. Is used to avoid interacting with bugged keen DistantSounds
		private const float JETSoundCrossFadeDist = 50f;                // distance at which the distant sound effect starts playing
		private const float JETSoundFarMaxVolDist = 250f;               // distance at which the distant sound effect reaches its max volume

		// Base Stats Multipliers
		private const float JETLargeMultiplier = 2.5f;                  // multiplier for stats of 3x3 version, doesn't affect gimbal angle
		private const float JETSmallMultiplier = 1f;                    // multiplier for stats of 1x1 version, doesn't affect gimbal angle


		// CONFIGURATION ENDS HERE


		private MyStringId Material = MyStringId.GetOrCompute("Square");

		public const string CONTROLS_PREFIX = "GimbalJetThruster.";
		public readonly Guid SETTINGS_GUID = new Guid("d9bb2ae2-4731-4fcf-b65b-898a13e599ee");

		private static bool TerminalControlsDone = false;

		private IMyFunctionalBlock IMyJet;
		private MyCubeBlock MyJet;
		private long IMyJetCubeGridId = 0;

		private MyResourceSinkComponent SinkFuel;
		private MyResourceSinkComponent SinkOxy;

		private double JETGimbalAngle = 0;
		private double JETNozzleAngle = 0;
		private float ThrustLR = 0;
		private float ThrustUD = 0;
		private float ThrustMain = 0;
		private float CurrentThrust = 0;
		private bool HasFuel = false;

		private float CurrentThrust10 = 0;

		private float OldGridMass = 0;
		Vector3D RotationMomentumLR = Vector3.Zero;
		Vector3D RotationMomentumUD = Vector3.Zero;
		Vector3D DirectionRelativeToGridLR = Vector3.Zero;
		Vector3D DirectionRelativeToGridUD = Vector3.Zero;

		MatrixD particleF_matrix;

		private string particleName = "ShipWelderArc";
		private float particleScale = 1f;
		MyParticleEffect particleThrust;
		MatrixD particleThrust_matrix = MatrixD.Identity;
		Vector3D particleThrust_position = Vector3D.Zero;

		private static MySoundPair audio;
		private static MySoundPair audioFar;
		private MyEntity3DSoundEmitter soundThrust;
		private MyEntity3DSoundEmitter soundThrustDistant;

		private float DistanceToCamera = 0;

		private MyEntitySubpart SubpartX;
		private float OldSubpartXAngle = 0;
		private MyEntitySubpart SubpartY;
		private float OldSubpartYAngle = 0;
		private float OldnSubpartAngle = 0;

		private readonly Dictionary<int, MyEntitySubpart> NozzleSubparts = new Dictionary<int, MyEntitySubpart>();

		private bool OldOverride = false;

		static readonly Dictionary<string, IMyModelDummy> TempDummies = new Dictionary<string, IMyModelDummy>();

		MySync<float, SyncDirection.BothWays> Terminal_OverrideLR;
		MySync<float, SyncDirection.BothWays> Terminal_OverrideUD;
		MySync<float, SyncDirection.BothWays> Terminal_SliderOverrideThrottle;
		MySync<float, SyncDirection.BothWays> Terminal_ThrustLimiter;

		MySync<bool, SyncDirection.BothWays> Terminal_KeepThrottle;
		MySync<bool, SyncDirection.BothWays> Terminal_OverrideThrottle;
		MySync<bool, SyncDirection.BothWays> Terminal_EnableAngularDampening;
		MySync<bool, SyncDirection.BothWays> Terminal_OverrideControl;

		MySync<float, SyncDirection.FromServer> Synced_Thrust;
		MySync<bool, SyncDirection.FromServer> Synced_HasFuel;

		private bool m_KeepThrottle = true;
		private bool m_OverrideThrottle = false;
		private bool m_EnableAngularDampening = true;

		private float m_ThrustLimiter = 1f;

		private bool m_OverrideControl = false;

		private float m_OverrideLR = 0;
		private float m_OverrideUD = 0;
		private float m_SliderOverrideThrottle = 0;

		private MyPlanet NearestPlanet;

		public override void Init(MyObjectBuilder_EntityBase objectBuilder)
		{
			audio = new MySoundPair(JETSoundID);
			audioFar = new MySoundPair(JETSoundFarID);

			IMyJet = (IMyFunctionalBlock)Entity;
			MyJet = (MyCubeBlock)Entity;

			NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
		}

		private void UpdateMySyncOnceBeforeFrame()
		{
			Terminal_OverrideLR.SetLocalValue(OverrideLR);
			Terminal_OverrideLR.ValueChanged += Terminal_OverrideLR_ValueChanged;

			Terminal_OverrideUD.SetLocalValue(OverrideUD);
			Terminal_OverrideUD.ValueChanged += Terminal_OverrideUD_ValueChanged;

			Terminal_SliderOverrideThrottle.SetLocalValue(SliderOverrideThrottle);
			Terminal_SliderOverrideThrottle.ValueChanged += Terminal_SliderOverrideThrottle_ValueChanged;

			Terminal_ThrustLimiter.SetLocalValue(ThrustLimiter);
			Terminal_ThrustLimiter.ValueChanged += Terminal_ThrustLimiter_ValueChanged;

			Terminal_KeepThrottle.SetLocalValue(KeepThrottle);
			Terminal_KeepThrottle.ValueChanged += Terminal_KeepThrottle_ValueChanged;

			Terminal_OverrideThrottle.SetLocalValue(Settings.OverrideThrottle);
			Terminal_OverrideThrottle.ValueChanged += Terminal_OverrideThrottle_ValueChanged;

			Terminal_EnableAngularDampening.SetLocalValue(EnableAngularDampening);
			Terminal_EnableAngularDampening.ValueChanged += Terminal_EnableAngularDampening_ValueChanged;

			Terminal_OverrideControl.SetLocalValue(OverrideControl);
			Terminal_OverrideControl.ValueChanged += Terminal_OverrideControl_ValueChanged;

			Synced_Thrust.SetLocalValue(ThrustMain);
			Synced_Thrust.ValueChanged += Synced_Thrust_ValueChanged;

			Synced_HasFuel.SetLocalValue(HasFuel);
			Synced_HasFuel.ValueChanged += Synced_HasFuel_ValueChanged;
		}

		private void Terminal_OverrideLR_ValueChanged(MySync<float, SyncDirection.BothWays> obj)
		{
			OverrideLR = obj.Value;
		}

		private void Terminal_OverrideUD_ValueChanged(MySync<float, SyncDirection.BothWays> obj)
		{
			OverrideUD = obj.Value;
		}

		private void Terminal_SliderOverrideThrottle_ValueChanged(MySync<float, SyncDirection.BothWays> obj)
		{
			SliderOverrideThrottle = obj.Value;
		}

		private void Terminal_ThrustLimiter_ValueChanged(MySync<float, SyncDirection.BothWays> obj)
		{
			ThrustLimiter = obj.Value;
		}

		private void Terminal_KeepThrottle_ValueChanged(MySync<bool, SyncDirection.BothWays> obj)
		{
			KeepThrottle = obj.Value;
		}

		private void Terminal_OverrideThrottle_ValueChanged(MySync<bool, SyncDirection.BothWays> obj)
		{
			OverrideThrottle = obj.Value;
		}

		private void Terminal_EnableAngularDampening_ValueChanged(MySync<bool, SyncDirection.BothWays> obj)
		{
			EnableAngularDampening = obj.Value;
		}

		private void Terminal_OverrideControl_ValueChanged(MySync<bool, SyncDirection.BothWays> obj)
		{
			OverrideControl = obj.Value;
		}

		private void Synced_Thrust_ValueChanged(MySync<float, SyncDirection.FromServer> obj)
		{
			ThrustMain = obj.Value;
			CurrentThrust = ThrustMain * Terminal_ThrustLimiter;
		}

		private void Synced_HasFuel_ValueChanged(MySync<bool, SyncDirection.FromServer> obj)
		{
			HasFuel = obj.Value;
		}

		private float AtmoPressure
		{
			get
			{
				var pos = IMyJet.PositionComp.WorldMatrixRef.Translation;
				NearestPlanet = MyGamePruningStructure.GetClosestPlanet(pos);
				if (NearestPlanet == null)
					return 0;
				else
					return NearestPlanet.GetAirDensity(IMyJet.PositionComp.WorldMatrixRef.Translation);
			}
		}

		private bool CheckAtmo
		{
			get
			{
				if (NeedsAtmosphereToWork)
				{
					if (AtmoPressure < MinAtmoPressure)
						return false;
					else
						return true;
				}
				else
					return true;
			}
		}

		public override void UpdateOnceBeforeFrame()
		{

			if (IMyJet?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
				return;

			if (IMyJet.BlockDefinition.SubtypeId == "GimbalJetEngine3x3")
			{
				JETDamageLength = JETDamageLength * JETLargeMultiplier;
				JETDamageStrength = JETDamageStrength * JETLargeMultiplier;
				JETForceMagnitude = JETForceMagnitude * JETLargeMultiplier;
				FuelConsumption = FuelConsumption * JETLargeMultiplier;
				OxygenConsumption = OxygenConsumption * JETLargeMultiplier;
				particleName = JETLargeParticle;
				particleScale = JETLargeParticleScale;
			}
			else if (IMyJet.BlockDefinition.SubtypeId == "GimbalJetEngine")
			{
				JETDamageLength = JETDamageLength * JETSmallMultiplier;
				JETDamageStrength = JETDamageStrength * JETSmallMultiplier;
				JETForceMagnitude = JETForceMagnitude * JETSmallMultiplier;
				FuelConsumption = FuelConsumption * JETSmallMultiplier;
				OxygenConsumption = OxygenConsumption * JETLargeMultiplier;
				particleName = JETSmallParticle;
				particleScale = JETSmallParticleScale;
			}

			TerminalControls.DoOnce();
			GetSubparts();
			GetDummies();
			SinkInit();

			// SinkFuel.Update();
			// SinkOxy.Update();

			JETGimbalAngle = Math.PI * JETGimbalAngleDegrees / 180.0;
			JETNozzleAngle = Math.PI * JETNozzleAngleDegrees / 180.0;

			UpdateMySyncOnceBeforeFrame();
			MyJet.OnModelRefresh += BlockModelChanged;
			BlockModelChanged(MyJet);

			if (!LoadSettings())
			{
				LoadDefaults();
			}
			Save();

			NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_10TH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;

		}

		public override void Close()
		{
			StopAllParticles();
			NozzleSubparts.Clear();

			if (MyJet != null)
				MyJet = null;

			if (IMyJet != null)
				IMyJet = null;

			if (SinkFuel != null)
				SinkFuel = null;

			if (SinkOxy != null)
				SinkOxy = null;

			// base.Close();
		}

		void BlockModelChanged(MyEntity MyJet)
		{
			StopAllParticles();
			GetDummies();
			GetSubparts();
		}

		void GridChanged()
		{
			StopAllParticles();
			GetDummies();
		}

		private void RecalculatePosInGrid()
		{

			if (IMyJet?.CubeGrid?.Physics == null || MyJet.CubeGrid.IsPreview || MyJet.CubeGrid.Physics == null || !MyJet.CubeGrid.Physics.Enabled || Terminal_OverrideControl)
				return;

			var GridMass = MyJet.CubeGrid.Mass;
			if (OldGridMass != GridMass || !MyAPIGateway.Utilities.IsDedicated)
			{
				// MyAPIGateway.Utilities.ShowNotification("[JET] Recalculate Thruster orientation");
				var PosRelative = MyJet.CubeGrid.WorldToGridInteger(MyJet.CubeGrid.Physics.CenterOfMassWorld) - MyJet.Position;
				DirectionRelativeToGridLR = Base6Directions.GetVector(MyJet.Orientation.Left);
				DirectionRelativeToGridUD = Base6Directions.GetVector(MyJet.Orientation.Up);
				RotationMomentumLR = Vector3.Cross(PosRelative, DirectionRelativeToGridLR);
				RotationMomentumUD = Vector3.Cross(PosRelative, DirectionRelativeToGridUD);
				OldGridMass = GridMass;
			}
		}

		public override void UpdateBeforeSimulation10()
		{

			if (OldOverride != Terminal_OverrideControl)
			{
				TerminalControls.UpdateControls();
			}
			OldOverride = Terminal_OverrideControl;

			UpdateDistanceToCamera();

			JETDamage();

			CurrentThrust10 = 0;

		}

		private void UpdateDistanceToCamera()
		{
			if (MyAPIGateway.Utilities.IsDedicated)
				return;

			var dist = Vector3D.Distance(MyJet.WorldMatrix.Translation, MyAPIGateway.Session.Camera.WorldMatrix.Translation);
			DistanceToCamera = (float)dist;
		}

		public override void UpdateBeforeSimulation100()
		{
			if (Synced_Thrust != ThrustMain)
				Synced_Thrust.ValidateAndSet(ThrustMain);

			if (Synced_HasFuel != HasFuel)
				Synced_HasFuel.ValidateAndSet(HasFuel);

			// if (DistanceToCamera <= 100f)
			// MyAPIGateway.Utilities.ShowNotification("[JET] Oxy" + SinkOxy.ResourceAvailableByType(MyResourceDistributorComponent.OxygenId));
		}

		public override void UpdateAfterSimulation100()
		{
			RecalculatePosInGrid();
		}

		public override void UpdateAfterSimulation()
		{

			UpdateParticleEffects();
			UpdateSoundEffects();

			NozzleAnimation();
			GimbalAnimation();

			SinkFuel.Update();
			SinkOxy.Update();

			if (IMyJetCubeGridId != MyJet.CubeGrid.EntityId)
			{
				IMyJetCubeGridId = MyJet.CubeGrid.EntityId;
				GridChanged();
				OldGridMass = 0;
			}

			if (MyAPIGateway.Session.IsServer)
			{
				if (SinkFuel.ResourceAvailableByType(MyResourceDistributorComponent.HydrogenId) <= 0 || SinkOxy.ResourceAvailableByType(MyResourceDistributorComponent.OxygenId) <= 0)
					HasFuel = false;
				else
					HasFuel = true;
			}

			// if(!IMyJet.IsWorking || (SinkFuel.ResourceAvailableByType(MyResourceDistributorComponent.HydrogenId) <= 0 && SinkFuel.CurrentInputByType(MyResourceDistributorComponent.HydrogenId) <= 0) || (SinkOxy.ResourceAvailableByType(MyResourceDistributorComponent.OxygenId) <= 0 && SinkOxy.CurrentInputByType(MyResourceDistributorComponent.OxygenId) <= 0) || !CheckAtmo)

			if (!IMyJet.IsWorking || !HasFuel || !CheckAtmo)
			{
				ThrustMain = MathHelper.Clamp(ThrustMain - 0.02f, 0, 1);
				CurrentThrust = ThrustMain * Terminal_ThrustLimiter;
				return;
			}

			var grid = MyJet.CubeGrid;

			if (grid.IsPreview || grid.Physics == null || !grid.Physics.Enabled)
				return;

			var groupProperties = MyGridPhysicalGroupData.GetGroupSharedProperties(grid);

			float RotationLR = 0;
			float RotationUD = 0;
			float MovementMain = 0;

			if (!Terminal_OverrideControl || !Terminal_OverrideThrottle)
			{
				var movementIndic = Vector3.Zero;
				var RotationInputVector = Vector3.Zero;

				var RelDampVelocity = Vector3.Zero;

				float ThrustPitch = 0;
				float ThrustRoll = 0;
				float ThrustYaw = 0;

				var GridMass = grid.Mass;
				if (OldGridMass != GridMass || !MyAPIGateway.Utilities.IsDedicated)
				{
					// MyAPIGateway.Utilities.ShowNotification("[JET] Recalculate Thruster orientation");
					var PosRelative = grid.WorldToGridInteger(grid.Physics.CenterOfMassWorld) - MyJet.Position;
					DirectionRelativeToGridLR = Base6Directions.GetVector(MyJet.Orientation.Left);
					DirectionRelativeToGridUD = Base6Directions.GetVector(MyJet.Orientation.Up);
					RotationMomentumLR = Vector3.Cross(PosRelative, DirectionRelativeToGridLR);
					RotationMomentumUD = Vector3.Cross(PosRelative, DirectionRelativeToGridUD);
					OldGridMass = GridMass;
				}

				// getting controller stuff

				var player = MyAPIGateway.Players.GetPlayerControllingEntity(IMyJet.CubeGrid);
				IMyShipController ShipController = null;

				if (player?.Controller?.ControlledEntity != null)
				{
					ShipController = player.Controller.ControlledEntity as IMyShipController;
				}

				if (ShipController != null)
				{
					var MatrixTransposeToCockpit = Matrix.Transpose(ShipController.LocalMatrix.GetOrientation());

					IMyEntity RelDampEntity = null;
					RelDampEntity = ShipController.RelativeDampeningTarget;

					if (RelDampEntity != null)
					{
						RelDampVelocity = RelDampEntity.Physics.LinearVelocity;
					}

					// Movement Inputs
					if (!Terminal_OverrideThrottle)
					{
						movementIndic = ShipController.MoveIndicator;
						MovementMain = MathHelper.Clamp(-movementIndic.Z, -1f, 1f);
					}

					// Rotation Inputs

					var rotateIndic = Vector2.ClampToSphere(ShipController.RotationIndicator, 1f);
					var rollIndic = MathHelper.Clamp(ShipController.RollIndicator, -1f, 1f);
					RotationInputVector = new Vector3(rotateIndic, rollIndic); // X yaw Y Pitch Z Roll

					if (!Terminal_OverrideControl)
					{
						var CockpitRelativeRotationMomentumLR = Vector3.Transform(RotationMomentumLR, MatrixTransposeToCockpit);
						var CockpitRelativeRotationMomentumUD = Vector3.Transform(RotationMomentumUD, MatrixTransposeToCockpit);

						RotationLR = MathHelper.Clamp(Vector3.Dot(RotationInputVector, CockpitRelativeRotationMomentumLR), -1f, 1f);
						RotationUD = MathHelper.Clamp(Vector3.Dot(RotationInputVector, CockpitRelativeRotationMomentumUD), -1f, 1f);
					}

				}


				if (RotationInputVector == Vector3.Zero && Terminal_EnableAngularDampening)
				{

					// Rotation Cancelling

					var GridAngularSpeed = grid.Physics.AngularVelocity;
					var RotationDampeningAggresiveness = 0.1f;

					if (GridAngularSpeed.Length() > RotationDampeningAggresiveness)
					{

						ThrustPitch = Vector3.Dot(GridAngularSpeed, grid.PositionComp.WorldMatrixRef.Up);
						ThrustRoll = -Vector3.Dot(GridAngularSpeed, grid.PositionComp.WorldMatrixRef.Forward);
						ThrustYaw = -Vector3.Dot(GridAngularSpeed, grid.PositionComp.WorldMatrixRef.Left);

						if (Math.Abs(ThrustPitch) < RotationDampeningAggresiveness) ThrustPitch = 0;
						if (Math.Abs(ThrustRoll) < RotationDampeningAggresiveness) ThrustRoll = 0;
						if (Math.Abs(ThrustYaw) < RotationDampeningAggresiveness) ThrustYaw = 0;

						RotationInputVector = new Vector3(ThrustYaw, ThrustPitch, ThrustRoll);

						RotationLR = MathHelper.Clamp(Vector3.Dot(RotationInputVector, RotationMomentumLR), -1f, 1f);
						RotationUD = MathHelper.Clamp(Vector3.Dot(RotationInputVector, RotationMomentumUD), -1f, 1f);

					}

				}

			}

			if (Terminal_OverrideThrottle)
				MovementMain = Terminal_SliderOverrideThrottle;

			if (Terminal_OverrideControl)
			{
				RotationLR = Terminal_OverrideLR;
				RotationUD = Terminal_OverrideUD;
			}

			if (ThrustLR < RotationLR)
				ThrustLR = MathHelper.Clamp(ThrustLR + 0.1f, -1, Math.Abs(RotationLR));
			if (ThrustLR > RotationLR)
				ThrustLR = MathHelper.Clamp(ThrustLR - 0.1f, -Math.Abs(RotationLR), 1);

			if (ThrustUD < RotationUD)
				ThrustUD = MathHelper.Clamp(ThrustUD + 0.1f, -1, Math.Abs(RotationUD));
			if (ThrustUD > RotationUD)
				ThrustUD = MathHelper.Clamp(ThrustUD - 0.1f, -Math.Abs(RotationUD), 1);

			if (ThrustMain < MovementMain)
				ThrustMain = MathHelper.Clamp(ThrustMain + 0.01f, 0, MovementMain);
			if (!Terminal_OverrideThrottle)
			{
				if (MovementMain < 0 && Terminal_KeepThrottle)
					ThrustMain = MathHelper.Clamp(ThrustMain - 0.01f, 0, 1);
				if (MovementMain <= 0 && !Terminal_KeepThrottle)
					ThrustMain = MathHelper.Clamp(ThrustMain - 0.01f, 0, 1);
			}
			else
				if (ThrustMain > MovementMain)
					ThrustMain = MathHelper.Clamp(ThrustMain - 0.01f, MovementMain, 1);

			CurrentThrust = ThrustMain * Terminal_ThrustLimiter;

			if (CurrentThrust10 <= Math.Abs(CurrentThrust))
				CurrentThrust10 = CurrentThrust;

			// apply force

			if (grid.Physics.IsStatic)
				return;

			if (ThrustMain != 0)
			{
				var BlockPos = MyJet.PositionComp.WorldVolume.Center;
				Vector3D force = Vector3.ClampToSphere(CurrentThrust * JETForceMagnitude * (MyJet.WorldMatrix.Backward * Math.Cos(JETGimbalAngle) + MyJet.WorldMatrix.Left * ThrustLR * Math.Sin(JETGimbalAngle) + MyJet.WorldMatrix.Up * ThrustUD * Math.Sin(JETGimbalAngle)), JETForceMagnitude);

				grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, force, BlockPos, null);

				if (EnableDebugDraw)
				{
					Color color = Color.Blue * 0.5f;
					BlendTypeEnum blendType = BlendTypeEnum.Standard;
					MyTransparentGeometry.AddLineBillboard(Material, color, BlockPos, -Vector3.Normalize(force), CurrentThrust * JETDamageLength, 0.05f, blendType);
				}


			}

		}

		private bool SinkInit()
		{
			var sinkInfoFuel1 = new MyResourceSinkInfo()
			{
				MaxRequiredInput = FuelConsumption,
				RequiredInputFunc = FuelInputFunc,
				ResourceTypeId = MyResourceDistributorComponent.HydrogenId
			};

			var sinkInfoFuel2 = new MyResourceSinkInfo()
			{
				MaxRequiredInput = OxygenConsumption,
				RequiredInputFunc = OxygenInputFunc,
				ResourceTypeId = MyResourceDistributorComponent.OxygenId
			};

			var fakeController = new MyShipController()
			{
				SlimBlock = MyJet.SlimBlock
			};

			SinkFuel = MyJet.Components?.Get<MyResourceSinkComponent>();
			if (SinkFuel != null)
			{
				SinkFuel.AddType(ref sinkInfoFuel1);
			}
			else
			{
				SinkFuel = new MyResourceSinkComponent();
				SinkFuel.Init(MyStringHash.GetOrCompute("Thrust"), sinkInfoFuel1);
				MyJet.Components.Add(SinkFuel);
			}

			SinkOxy = MyJet.Components?.Get<MyResourceSinkComponent>();
			if (SinkOxy != null)
			{
				SinkOxy.AddType(ref sinkInfoFuel2);
			}
			else
			{
				SinkOxy = new MyResourceSinkComponent();
				SinkOxy.Init(MyStringHash.GetOrCompute("Thrust"), sinkInfoFuel1);
				MyJet.Components.Add(SinkOxy);
			}

			var distributor = fakeController.GridResourceDistributor;
			if (distributor != null)
			{
				distributor.AddSink(SinkFuel);
				distributor.AddSink(SinkOxy);
				return true;
			}
			return false;
		}

		private float OxygenInputFunc()
		{
			if (!MyJet.IsWorking)
				return 0;

			if (CurrentThrust != 0)
				return CurrentThrust * OxygenConsumption;

			return 0;
		}

		private float FuelInputFunc()
		{
			if (!MyJet.IsWorking)
				return 0;

			if (CurrentThrust != 0)
				return CurrentThrust * FuelConsumption;

			return 0;
		}


		private void JETDamage()
		{

			if (!MyAPIGateway.Session.IsServer)
				return;

			if (CurrentThrust10 <= 0.01f)
				return;

			var raycastPosition = Vector3.Zero;
			var raycastForward = Vector3.Zero;
			MatrixD EmptyMatrix = MatrixD.Identity;

			var position = IMyJet.GetPosition();
			var PartMatrix = IMyJet.PositionComp.WorldMatrixRef;

			if (CurrentThrust10 > 0.01f)
			{
				EmptyMatrix = particleF_matrix;

				var localPos = EmptyMatrix.Translation;
				var EmptyForward = Vector3D.Normalize(EmptyMatrix.Forward);

				raycastPosition = Vector3D.Transform(localPos, PartMatrix);
				raycastForward = Vector3D.TransformNormal(EmptyForward, PartMatrix);
			}


			IHitInfo hitInfo = null;

			var RayCastEndPos = raycastPosition + raycastForward * JETDamageLength * CurrentThrust10;

			MyAPIGateway.Physics.CastRay(raycastPosition, RayCastEndPos, out hitInfo, 9);

			// debug lines for damage

			if (EnableDebugDraw)
			{
				Color color = Color.Red * 0.5f;
				BlendTypeEnum blendType = BlendTypeEnum.Standard;
				MyTransparentGeometry.AddLineBillboard(Material, color, raycastPosition, raycastForward, JETDamageLength * CurrentThrust10, 0.05f, blendType);
			}

			if (hitInfo?.HitEntity != null)
			{
				var entity = hitInfo.HitEntity;
				var hitPos = hitInfo.Position;
				if (entity is IMyCharacter)
				{
					DealCharacterDamage((IMyCharacter)entity);
				}

				else if (entity is IMyCubeGrid)
				{
					DealGridDamage((IMyCubeGrid)entity, hitPos, RayCastEndPos, JETDamageStrength * Math.Abs(CurrentThrust10));
				}
			}

		}

		private void DealGridDamage(IMyCubeGrid cubeGrid, Vector3D startCoords, Vector3D endCoords, float damageAmount)
		{
			LineD line = new LineD(startCoords, endCoords);
			double dist;
			IMySlimBlock block = null;
			cubeGrid.GetLineIntersectionExactAll(ref line, out dist, out block);
			if (block != null)
			{
				block.DoDamage(damageAmount, MyStringHash.GetOrCompute("Thruster"), true, null, IMyJet.EntityId);
			}

		}

		private void DealCharacterDamage(IMyCharacter Character)
		{
			Character.DoDamage(75f, MyStringHash.GetOrCompute("Thruster"), true, null, IMyJet.EntityId);
		}

		private void UpdateSoundEffects()
		{
			if (!MyAPIGateway.Utilities.IsDedicated)
			{

				if (Math.Abs(CurrentThrust) > 0.01f)
				{
					if (soundThrust == null)
					{
						soundThrust = new MyEntity3DSoundEmitter((MyEntity)MyJet);
					}

					if (!soundThrust.IsPlaying)
					{
						soundThrust.PlaySound(audio, true, false, false, false, false, null, false);
					}
					else
					{
						soundThrust.VolumeMultiplier = CurrentThrust;
						soundThrust.FastUpdate(false);
					}

				}

				if (Math.Abs(CurrentThrust) <= 0.01f)
				{
					if (soundThrust != null)
					{
						soundThrust.StopSound(false, true);
					}
				}

				// This is a dumb fix but it works. distant sounds are very cursed in SE

				if (Math.Abs(CurrentThrust) > 0.01f && DistanceToCamera >= JETSoundCrossFadeDist)
				{
					if (soundThrustDistant == null)
					{
						soundThrustDistant = new MyEntity3DSoundEmitter((MyEntity)MyJet);
					}

					if (!soundThrustDistant.IsPlaying)
					{
						soundThrustDistant.PlaySound(audioFar, true, false, false, false, false, null, false);
					}
					else
					{
						soundThrustDistant.VolumeMultiplier = CurrentThrust * Math.Min((DistanceToCamera - JETSoundCrossFadeDist) / (JETSoundFarMaxVolDist - JETSoundCrossFadeDist), 1f);
						soundThrustDistant.FastUpdate(false);
					}

				}

				if (Math.Abs(CurrentThrust) <= 0.01f || DistanceToCamera < 50f)
				{
					if (soundThrustDistant != null)
					{
						soundThrustDistant.StopSound(false, true);
					}
				}

			}
		}


		private void UpdateParticleEffects()
		{

			if (!MyAPIGateway.Utilities.IsDedicated && SubpartX != null && SubpartY != null)
			{
				var block_position = IMyJet.GetPosition();
				uint parentId = SubpartX.Render.GetRenderObjectID();

				if (Math.Abs(CurrentThrust) > 0.01f)
				{

					particleThrust_matrix = SubpartY.PositionComp.LocalMatrixRef;

					if (particleThrust == null)
					{
						particleThrust_position = block_position;
						MyParticlesManager.TryCreateParticleEffect(particleName, ref particleThrust_matrix, ref particleThrust_position, parentId, out particleThrust);
					}
					else
					{
						particleThrust.WorldMatrix = particleThrust_matrix;
						particleThrust.UserRadiusMultiplier = 1f;
						particleThrust.UserBirthMultiplier = 1f;
						particleThrust.UserScale = MathHelper.Clamp(Math.Abs(CurrentThrust), 0.5f, 1f) * particleScale;
						particleThrust.UserLifeMultiplier = 1f;

						particleThrust.Play();
					}
				}

				if (Math.Abs(CurrentThrust) <= 0.01f || SubpartX == null || SubpartY == null)
				{

					if (particleThrust != null)
					{
						particleThrust.StopLights();
						particleThrust.StopEmitting();
					}

				}


			}

		}


		private void StopAllParticles()
		{

			if (particleThrust != null)
			{
				particleThrust.StopLights();
				particleThrust.Stop();
				particleThrust = null;

			}


			if (soundThrust != null)
			{
				soundThrust.StopSound(true, true);
				soundThrust = null;
			}

			if (soundThrustDistant != null)
			{
				soundThrustDistant.StopSound(true, true);
				soundThrustDistant = null;
			}

		}


		void GetDummies()
		{
			var ent = (IMyFunctionalBlock)Entity;
			TempDummies.Clear();
			ent.Model.GetDummies(TempDummies);
			if (TempDummies.Count == 0)
				return;

			foreach (IMyModelDummy dummy in TempDummies.Values)
			{

				// MyAPIGateway.Utilities.ShowNotification("[JET] Found a dummy " + dummy.Name);

				switch (dummy.Name)
				{

					case "thruster_flame_1":
						particleF_matrix = dummy.Matrix;
						break;

				}

			}
			TempDummies.Clear();
		}


		void GetSubparts()
		{
			var ent = (IMyFunctionalBlock)Entity;
			NozzleSubparts.Clear();

			if (ent.TryGetSubpart("GimbalX", out SubpartX))
				if (SubpartX.TryGetSubpart("GimbalY", out SubpartY))
				{
					MyEntitySubpart NozzleSubpart = null;
					SubpartY.TryGetSubpart("NozzlePart0", out NozzleSubpart);
					NozzleSubparts.Add(360, NozzleSubpart);

					SubpartY.TryGetSubpart("NozzlePart30", out NozzleSubpart);
					NozzleSubparts.Add(30, NozzleSubpart);

					SubpartY.TryGetSubpart("NozzlePart60", out NozzleSubpart);
					NozzleSubparts.Add(60, NozzleSubpart);

					SubpartY.TryGetSubpart("NozzlePart90", out NozzleSubpart);
					NozzleSubparts.Add(90, NozzleSubpart);

					SubpartY.TryGetSubpart("NozzlePart120", out NozzleSubpart);
					NozzleSubparts.Add(120, NozzleSubpart);

					SubpartY.TryGetSubpart("NozzlePart150", out NozzleSubpart);
					NozzleSubparts.Add(150, NozzleSubpart);

					SubpartY.TryGetSubpart("NozzlePart180", out NozzleSubpart);
					NozzleSubparts.Add(180, NozzleSubpart);

					SubpartY.TryGetSubpart("NozzlePart210", out NozzleSubpart);
					NozzleSubparts.Add(210, NozzleSubpart);

					SubpartY.TryGetSubpart("NozzlePart240", out NozzleSubpart);
					NozzleSubparts.Add(240, NozzleSubpart);

					SubpartY.TryGetSubpart("NozzlePart270", out NozzleSubpart);
					NozzleSubparts.Add(270, NozzleSubpart);

					SubpartY.TryGetSubpart("NozzlePart300", out NozzleSubpart);
					NozzleSubparts.Add(300, NozzleSubpart);

					SubpartY.TryGetSubpart("NozzlePart330", out NozzleSubpart);
					NozzleSubparts.Add(330, NozzleSubpart);
				}

			OldSubpartXAngle = 0;
			OldSubpartYAngle = 0;
			OldnSubpartAngle = 0;
		}

		private void GimbalAnimation()
		{
			if (SubpartX == null || SubpartY == null || MyAPIGateway.Utilities.IsDedicated)
				return;

			Vector2 gimbal = new Vector2(ThrustUD, ThrustLR);

			if (DistanceToCamera >= 1000f)
				gimbal = Vector2.Zero;

			float XAngle = gimbal.X - OldSubpartXAngle;
			if (XAngle != 0)
			{
				Matrix SubpartXMatrix = SubpartX.PositionComp.LocalMatrixRef;
				SubpartXMatrix = Matrix.CreateRotationX(-XAngle * (float)JETGimbalAngle) * SubpartXMatrix;
				SubpartX.PositionComp.SetLocalMatrix(ref SubpartXMatrix);
				OldSubpartXAngle = gimbal.X;
			}

			float YAngle = gimbal.Y - OldSubpartYAngle;
			if (YAngle != 0)
			{
				Matrix SubpartYMatrix = SubpartY.PositionComp.LocalMatrixRef;
				SubpartYMatrix = Matrix.CreateRotationY(-YAngle * (float)JETGimbalAngle) * SubpartYMatrix;
				SubpartY.PositionComp.SetLocalMatrix(ref SubpartYMatrix);
				OldSubpartYAngle = gimbal.Y;
			}
		}

		private void NozzleAnimation()
		{
			if (NozzleSubparts.Count == 0 || MyAPIGateway.Utilities.IsDedicated)
				return;

			var thrust = CurrentThrust;

			if (DistanceToCamera >= 1000f)
				thrust = 0;

			float nAngle = thrust - OldnSubpartAngle;

			if (nAngle != 0)
			{
				foreach (MyEntitySubpart nsubpart in NozzleSubparts.Values)
				{
					Matrix nSubpartMatrix = nsubpart.PositionComp.LocalMatrixRef;
					nSubpartMatrix = Matrix.CreateRotationX(nAngle * (float)JETNozzleAngle) * nSubpartMatrix;
					nsubpart.PositionComp.SetLocalMatrix(ref nSubpartMatrix);
				}
				OldnSubpartAngle = thrust;
			}

		}

		// save and load

		public bool KeepThrottle
		{
			get
			{
				return Settings.KeepThrottle;
			}
			set
			{
				Settings.KeepThrottle = value;
				Save();
			}
		}

		public bool OverrideThrottle
		{
			get
			{
				return Settings.OverrideThrottle;
			}
			set
			{
				Settings.OverrideThrottle = value;
				Save();
			}
		}

		public bool EnableAngularDampening
		{
			get
			{
				return Settings.EnableAngularDampening;
			}
			set
			{
				Settings.EnableAngularDampening = value;
				Save();
			}
		}

		public bool OverrideControl
		{
			get
			{
				return Settings.OverrideControl;
			}
			set
			{
				Settings.OverrideControl = value;
				Save();
			}
		}

		public float ThrustLimiter
		{
			get
			{
				return Settings.ThrustLimiter;
			}
			set
			{

				Settings.ThrustLimiter = MathHelper.Clamp(value, 0, 1f);

				Save();
			}
		}

		public float OverrideLR
		{
			get
			{
				return Settings.OverrideLR;
			}
			set
			{

				Settings.OverrideLR = MathHelper.Clamp(value, -1f, 1f);

				Save();
			}
		}

		public float OverrideUD
		{
			get
			{
				return Settings.OverrideUD;
			}
			set
			{

				Settings.OverrideUD = MathHelper.Clamp(value, -1f, 1f);

				Save();
			}
		}

		public float SliderOverrideThrottle
		{
			get
			{
				return Settings.SliderOverrideThrottle;
			}
			set
			{

				Settings.SliderOverrideThrottle = MathHelper.Clamp(value, 0, 1f);

				Save();
			}
		}



		public readonly GimbalJetThrusterSettings Settings = new GimbalJetThrusterSettings();
		GimbalJetThrusterMod Mod => GimbalJetThrusterMod.Instance;

		public override bool IsSerialized()
		{
			// called when the game iterates components to check if they should be serialized, before they're actually serialized.
			// this does not only include saving but also streaming and blueprinting.
			// NOTE for this to work reliably the MyModStorageComponent needs to already exist in this block with at least one element.

			try
			{
				Save();
			}
			catch (Exception e)
			{
				Log.Error(e);
			}

			return base.IsSerialized();

		}

		public void Save()
		{

			// MyAPIGateway.Utilities.ShowNotification("[JET] Save Called");

			if (IMyJet == null)
				return; // called too soon or after it was already closed, ignore

			if (Settings == null)
				throw new NullReferenceException($"Settings == null on entId={Entity?.EntityId}; modInstance={GimbalJetThrusterMod.Instance != null}");

			if (MyAPIGateway.Utilities == null)
				throw new NullReferenceException($"MyAPIGateway.Utilities == null; entId={Entity?.EntityId}; modInstance={GimbalJetThrusterMod.Instance != null}");

			if (IMyJet.Storage == null)
			{
				IMyJet.Storage = new MyModStorageComponent();
				// LoadDefaults();
				// MyAPIGateway.Utilities.ShowNotification("[JET] Storage Created");
			}

			IMyJet.Storage.SetValue(SETTINGS_GUID, Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(Settings)));

			// MyAPIGateway.Utilities.ShowNotification(Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(Settings)));


		}

		void LoadDefaults()
		{
			Terminal_KeepThrottle.ValidateAndSet(true);
			Terminal_OverrideThrottle.ValidateAndSet(false);
			Terminal_OverrideControl.ValidateAndSet(false);
			Terminal_EnableAngularDampening.ValidateAndSet(true);
			Terminal_ThrustLimiter.ValidateAndSet(1f);
			Terminal_OverrideLR.ValidateAndSet(0f);
			Terminal_OverrideUD.ValidateAndSet(0f);
			Terminal_SliderOverrideThrottle.ValidateAndSet(0f);
			// MyAPIGateway.Utilities.ShowNotification("[JET] Default Settings Loaded");

			return;
		}


		bool LoadSettings()
		{
			if (IMyJet.Storage == null)
				return false;

			string rawData;
			if (!IMyJet.Storage.TryGetValue(SETTINGS_GUID, out rawData))
				return false;

			try
			{
				var loadedSettings = MyAPIGateway.Utilities.SerializeFromBinary<GimbalJetThrusterSettings>(Convert.FromBase64String(rawData));

				if (loadedSettings != null)
				{
					Terminal_KeepThrottle.ValidateAndSet(loadedSettings.KeepThrottle);
					Terminal_OverrideThrottle.ValidateAndSet(loadedSettings.OverrideThrottle);
					Terminal_OverrideControl.ValidateAndSet(loadedSettings.OverrideControl);
					Terminal_EnableAngularDampening.ValidateAndSet(loadedSettings.EnableAngularDampening);
					Terminal_ThrustLimiter.ValidateAndSet(loadedSettings.ThrustLimiter);
					Terminal_OverrideLR.ValidateAndSet(loadedSettings.OverrideLR);
					Terminal_OverrideUD.ValidateAndSet(loadedSettings.OverrideUD);
					Terminal_SliderOverrideThrottle.ValidateAndSet(loadedSettings.SliderOverrideThrottle);

					return true;
				}
			}
			catch (Exception e)
			{
				Log.Error($"Error loading settings!\n{e}");
			}

			return false;
		}


		// Terminal Controls stuff

		private static class TerminalControls
		{

			public static void DoOnce()
			{
				if (TerminalControlsDone)
					return;

				SetupTerminalControls<IMyFunctionalBlock>();

				TerminalControlsDone = true;
			}

			public static void UpdateControls()
			{
				List<IMyTerminalControl> controls;

				MyAPIGateway.TerminalControls.GetControls<IMyFunctionalBlock>(out controls);

				foreach (IMyTerminalControl c in controls)
				{
					switch (c.Id)
					{
						case CONTROLS_PREFIX + "Terminal_OverrideLR":
						case CONTROLS_PREFIX + "Terminal_OverrideUD":
						case CONTROLS_PREFIX + "Terminal_SliderOverrideThrottle":
							{
								c.UpdateVisual();
								break;
							}
					}
				}
			}




			static void SetupTerminalControls<T>()

			{

				// Controls

				var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyFunctionalBlock>(""); // separators don't store the id
				c.SupportsMultipleBlocks = true;
				c.Visible = Control_Visible;
				MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(c);

				var KeepThrottle = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyFunctionalBlock>(CONTROLS_PREFIX + "Terminal_KeepThrottle");
				KeepThrottle.Title = MyStringId.GetOrCompute("Keep Throttle on key release");
				KeepThrottle.Tooltip = MyStringId.GetOrCompute("When On engine will keep throttle level when W key is released and decrease it only when S key is pressed");
				KeepThrottle.Visible = Control_Visible;
				KeepThrottle.SupportsMultipleBlocks = true;
				KeepThrottle.OnText = MySpaceTexts.SwitchText_On;
				KeepThrottle.OffText = MySpaceTexts.SwitchText_Off;
				KeepThrottle.Getter = Control_Terminal_KeepThrottle_Getter;
				KeepThrottle.Setter = Control_Terminal_KeepThrottle_Setter;
				MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(KeepThrottle);

				var ControlAngularDampening = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyFunctionalBlock>(CONTROLS_PREFIX + "Terminal_EnableAngularDampening");
				ControlAngularDampening.Title = MyStringId.GetOrCompute("Enable Angular Dampeners");
				ControlAngularDampening.Tooltip = MyStringId.GetOrCompute("When On thruster will gimbal itself to compensate angular velocity");
				ControlAngularDampening.Visible = Control_Visible;
				ControlAngularDampening.SupportsMultipleBlocks = true;
				ControlAngularDampening.Getter = Control_Terminal_EnableAngularDampening_Getter;
				ControlAngularDampening.Setter = Control_Terminal_EnableAngularDampening_Setter;
				MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(ControlAngularDampening);

				c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyFunctionalBlock>("");
				c.SupportsMultipleBlocks = true;
				c.Visible = Control_Visible;
				MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(c);

				var SliderThrustLimiter = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyFunctionalBlock>(CONTROLS_PREFIX + "Terminal_ThrustLimiter");
				SliderThrustLimiter.Title = MyStringId.GetOrCompute("Thrust Limiter");
				SliderThrustLimiter.Tooltip = MyStringId.GetOrCompute("Limits the maximum thrust output");
				SliderThrustLimiter.Visible = Control_Visible;
				SliderThrustLimiter.SupportsMultipleBlocks = true;
				SliderThrustLimiter.SetLimits(0.01f, 1f);
				SliderThrustLimiter.Getter = Control_Terminal_ThrustLimiter_Getter;
				SliderThrustLimiter.Setter = Control_Terminal_ThrustLimiter_Setter;
				SliderThrustLimiter.Writer = Control_Terminal_ThrustLimiter_Writer;
				MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(SliderThrustLimiter);

				var OverrideControl = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyFunctionalBlock>(CONTROLS_PREFIX + "Terminal_OverrideControl");
				OverrideControl.Title = MyStringId.GetOrCompute("Override Gimbal");
				OverrideControl.Tooltip = MyStringId.GetOrCompute("Manual gimbal override. Gimbal will ignore cockpit inputs and dampeners settings");
				OverrideControl.Visible = Control_Visible;
				OverrideControl.SupportsMultipleBlocks = true;
				OverrideControl.Getter = Control_Terminal_OverrideControl_Getter;
				OverrideControl.Setter = Control_Terminal_OverrideControl_Setter;
				MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(OverrideControl);

				var SliderOverrideLR = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyFunctionalBlock>(CONTROLS_PREFIX + "Terminal_OverrideLR");
				SliderOverrideLR.Title = MyStringId.GetOrCompute("Gimbal X");
				SliderOverrideLR.Tooltip = MyStringId.GetOrCompute("Sets gimbal override on X axis. Is relative to block orientation");
				SliderOverrideLR.Visible = Control_Visible;
				SliderOverrideLR.Enabled = Control_Override_Enabled;
				SliderOverrideLR.SupportsMultipleBlocks = true;
				SliderOverrideLR.SetLimits(-1f, 1f);
				SliderOverrideLR.Getter = Control_Terminal_OverrideLR_Getter;
				SliderOverrideLR.Setter = Control_Terminal_OverrideLR_Setter;
				SliderOverrideLR.Writer = Control_Terminal_OverrideLR_Writer;
				MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(SliderOverrideLR);

				var SliderOverrideUD = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyFunctionalBlock>(CONTROLS_PREFIX + "Terminal_OverrideUD");
				SliderOverrideUD.Title = MyStringId.GetOrCompute("Gimbal Y");
				SliderOverrideUD.Tooltip = MyStringId.GetOrCompute("Sets gimbal override on Y axis. Is relative to block orientation");
				SliderOverrideUD.Visible = Control_Visible;
				SliderOverrideUD.Enabled = Control_Override_Enabled;
				SliderOverrideUD.SupportsMultipleBlocks = true;
				SliderOverrideUD.SetLimits(-1f, 1f);
				SliderOverrideUD.Getter = Control_Terminal_OverrideUD_Getter;
				SliderOverrideUD.Setter = Control_Terminal_OverrideUD_Setter;
				SliderOverrideUD.Writer = Control_Terminal_OverrideUD_Writer;
				MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(SliderOverrideUD);

				var ControlOverrideThrottle = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyFunctionalBlock>(CONTROLS_PREFIX + "Terminal_OverrideThrottle");
				ControlOverrideThrottle.Title = MyStringId.GetOrCompute("Override Throttle");
				ControlOverrideThrottle.Tooltip = MyStringId.GetOrCompute("Manual throttle override. Thruster will ignore cockpit W/S inputs and will use throttle override slider instead");
				ControlOverrideThrottle.Visible = Control_Visible;
				ControlOverrideThrottle.SupportsMultipleBlocks = true;
				ControlOverrideThrottle.Getter = Control_Terminal_OverrideThrottle_Getter;
				ControlOverrideThrottle.Setter = Control_Terminal_OverrideThrottle_Setter;
				MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(ControlOverrideThrottle);

				var SliderSOverrideThrottle = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyFunctionalBlock>(CONTROLS_PREFIX + "Terminal_SliderOverrideThrottle");
				SliderSOverrideThrottle.Title = MyStringId.GetOrCompute("Throttle override");
				SliderSOverrideThrottle.Tooltip = MyStringId.GetOrCompute("Sets throttle override");
				SliderSOverrideThrottle.Visible = Control_Visible;
				SliderSOverrideThrottle.Enabled = Control_OverrideThrottle_Enabled;
				SliderSOverrideThrottle.SupportsMultipleBlocks = true;
				SliderSOverrideThrottle.SetLimits(0, 1f);
				SliderSOverrideThrottle.Getter = Control_Terminal_SliderOverrideThrottle_Getter;
				SliderSOverrideThrottle.Setter = Control_Terminal_SliderOverrideThrottle_Setter;
				SliderSOverrideThrottle.Writer = Control_Terminal_SliderOverrideThrottle_Writer;
				MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(SliderSOverrideThrottle);

				// Actions

				var ControlAngularDampeningAction = MyAPIGateway.TerminalControls.CreateAction<IMyFunctionalBlock>(CONTROLS_PREFIX + "EnableAngularDampeningAction");
				ControlAngularDampeningAction.Name = new StringBuilder("Toggle Angular Dampeners");
				ControlAngularDampeningAction.ValidForGroups = true;
				ControlAngularDampeningAction.Icon = @"Textures\GUI\Icons\Actions\NeutralToggle.dds";
				ControlAngularDampeningAction.Action = Control_Terminal_EnableAngularDampening_Action;
				ControlAngularDampeningAction.Writer = Control_Terminal_EnableAngularDampening_Writer;
				ControlAngularDampeningAction.Enabled = Control_Visible;
				MyAPIGateway.TerminalControls.AddAction<IMyFunctionalBlock>(ControlAngularDampeningAction);

				var KeepThrottleAction = MyAPIGateway.TerminalControls.CreateAction<IMyFunctionalBlock>(CONTROLS_PREFIX + "KeepThrottleAction");
				KeepThrottleAction.Name = new StringBuilder("Keep Throttle on key release On/Off");
				KeepThrottleAction.ValidForGroups = true;
				KeepThrottleAction.Icon = @"Textures\GUI\Icons\Actions\SmallShipToggle.dds";
				KeepThrottleAction.Action = Control_Terminal_KeepThrottle_Action;
				KeepThrottleAction.Writer = Control_Terminal_KeepThrottle_Writer;
				KeepThrottleAction.Enabled = Control_Visible;
				MyAPIGateway.TerminalControls.AddAction<IMyFunctionalBlock>(KeepThrottleAction);

				var IncreaseThrustLimiterAction = MyAPIGateway.TerminalControls.CreateAction<IMyFunctionalBlock>(CONTROLS_PREFIX + "IncreaseThrustLimiterAction");
				IncreaseThrustLimiterAction.Name = new StringBuilder("Increase Thrust Limiter");
				IncreaseThrustLimiterAction.ValidForGroups = true;
				IncreaseThrustLimiterAction.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
				IncreaseThrustLimiterAction.Action = Control_Terminal_ThrustLimiter_Increase_Action;
				IncreaseThrustLimiterAction.Writer = Control_Terminal_ThrustLimiter_Action_Writer;
				IncreaseThrustLimiterAction.Enabled = Control_Visible;
				MyAPIGateway.TerminalControls.AddAction<IMyFunctionalBlock>(IncreaseThrustLimiterAction);

				var DecreaseThrustLimiterAction = MyAPIGateway.TerminalControls.CreateAction<IMyFunctionalBlock>(CONTROLS_PREFIX + "DecreaseThrustLimiterAction");
				DecreaseThrustLimiterAction.Name = new StringBuilder("Decrease Thrust Limiter");
				DecreaseThrustLimiterAction.ValidForGroups = true;
				DecreaseThrustLimiterAction.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
				DecreaseThrustLimiterAction.Action = Control_Terminal_ThrustLimiter_Decrease_Action;
				DecreaseThrustLimiterAction.Writer = Control_Terminal_ThrustLimiter_Action_Writer;
				DecreaseThrustLimiterAction.Enabled = Control_Visible;
				MyAPIGateway.TerminalControls.AddAction<IMyFunctionalBlock>(DecreaseThrustLimiterAction);

				var OverrideControlAction = MyAPIGateway.TerminalControls.CreateAction<IMyFunctionalBlock>(CONTROLS_PREFIX + "OverrideControlAction");
				OverrideControlAction.Name = new StringBuilder("Override Gimbal");
				OverrideControlAction.ValidForGroups = true;
				OverrideControlAction.Icon = @"Textures\GUI\Icons\Actions\NeutralToggle.dds";
				OverrideControlAction.Action = Control_Terminal_OverrideControl_Action;
				OverrideControlAction.Writer = Control_Terminal_OverrideControl_Writer;
				OverrideControlAction.Enabled = Control_Visible;
				MyAPIGateway.TerminalControls.AddAction<IMyFunctionalBlock>(OverrideControlAction);

				var IncreaseOverrideLRAction = MyAPIGateway.TerminalControls.CreateAction<IMyFunctionalBlock>(CONTROLS_PREFIX + "IncreaseOverrideLRAction");
				IncreaseOverrideLRAction.Name = new StringBuilder("Increase Override Gimbal Y");
				IncreaseOverrideLRAction.ValidForGroups = true;
				IncreaseOverrideLRAction.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
				IncreaseOverrideLRAction.Action = Control_Terminal_OverrideLR_Increase_Action;
				IncreaseOverrideLRAction.Writer = Control_Terminal_OverrideLR_Action_Writer;
				IncreaseOverrideLRAction.Enabled = Control_Visible;
				MyAPIGateway.TerminalControls.AddAction<IMyFunctionalBlock>(IncreaseOverrideLRAction);

				var DecreaseOverrideLRAction = MyAPIGateway.TerminalControls.CreateAction<IMyFunctionalBlock>(CONTROLS_PREFIX + "DecreaseOverrideLRAction");
				DecreaseOverrideLRAction.Name = new StringBuilder("Decrease Override Gimbal Y");
				DecreaseOverrideLRAction.ValidForGroups = true;
				DecreaseOverrideLRAction.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
				DecreaseOverrideLRAction.Action = Control_Terminal_OverrideLR_Decrease_Action;
				DecreaseOverrideLRAction.Writer = Control_Terminal_OverrideLR_Action_Writer;
				DecreaseOverrideLRAction.Enabled = Control_Visible;
				MyAPIGateway.TerminalControls.AddAction<IMyFunctionalBlock>(DecreaseOverrideLRAction);

				var ResetOverrideLRAction = MyAPIGateway.TerminalControls.CreateAction<IMyFunctionalBlock>(CONTROLS_PREFIX + "ResetOverrideLRAction");
				ResetOverrideLRAction.Name = new StringBuilder("Reset Override Gimbal Y");
				ResetOverrideLRAction.ValidForGroups = true;
				ResetOverrideLRAction.Icon = @"Textures\GUI\Icons\Actions\NeutralToggle.dds";
				ResetOverrideLRAction.Action = Control_Terminal_OverrideLR_Reset_Action;
				ResetOverrideLRAction.Writer = Control_Terminal_OverrideLR_Action_Writer;
				ResetOverrideLRAction.Enabled = Control_Visible;
				MyAPIGateway.TerminalControls.AddAction<IMyFunctionalBlock>(ResetOverrideLRAction);

				var IncreaseOverrideUDAction = MyAPIGateway.TerminalControls.CreateAction<IMyFunctionalBlock>(CONTROLS_PREFIX + "IncreaseOverrideUDAction");
				IncreaseOverrideUDAction.Name = new StringBuilder("Increase Override Gimbal X");
				IncreaseOverrideUDAction.ValidForGroups = true;
				IncreaseOverrideUDAction.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
				IncreaseOverrideUDAction.Action = Control_Terminal_OverrideUD_Increase_Action;
				IncreaseOverrideUDAction.Writer = Control_Terminal_OverrideUD_Action_Writer;
				IncreaseOverrideUDAction.Enabled = Control_Visible;
				MyAPIGateway.TerminalControls.AddAction<IMyFunctionalBlock>(IncreaseOverrideUDAction);

				var DecreaseOverrideUDAction = MyAPIGateway.TerminalControls.CreateAction<IMyFunctionalBlock>(CONTROLS_PREFIX + "DecreaseOverrideUDAction");
				DecreaseOverrideUDAction.Name = new StringBuilder("Decrease Override Gimbal X");
				DecreaseOverrideUDAction.ValidForGroups = true;
				DecreaseOverrideUDAction.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
				DecreaseOverrideUDAction.Action = Control_Terminal_OverrideUD_Decrease_Action;
				DecreaseOverrideUDAction.Writer = Control_Terminal_OverrideUD_Action_Writer;
				DecreaseOverrideUDAction.Enabled = Control_Visible;
				MyAPIGateway.TerminalControls.AddAction<IMyFunctionalBlock>(DecreaseOverrideUDAction);

				var ResetOverrideUDAction = MyAPIGateway.TerminalControls.CreateAction<IMyFunctionalBlock>(CONTROLS_PREFIX + "ResetOverrideUDAction");
				ResetOverrideUDAction.Name = new StringBuilder("Reset Override Gimbal X");
				ResetOverrideUDAction.ValidForGroups = true;
				ResetOverrideUDAction.Icon = @"Textures\GUI\Icons\Actions\NeutralToggle.dds";
				ResetOverrideUDAction.Action = Control_Terminal_OverrideUD_Reset_Action;
				ResetOverrideUDAction.Writer = Control_Terminal_OverrideUD_Action_Writer;
				ResetOverrideUDAction.Enabled = Control_Visible;
				MyAPIGateway.TerminalControls.AddAction<IMyFunctionalBlock>(ResetOverrideUDAction);

				var ControlOverrideThrottleAction = MyAPIGateway.TerminalControls.CreateAction<IMyFunctionalBlock>(CONTROLS_PREFIX + "OverrideThrottleAction");
				ControlOverrideThrottleAction.Name = new StringBuilder("Toggle Throttle Override");
				ControlOverrideThrottleAction.ValidForGroups = true;
				ControlOverrideThrottleAction.Icon = @"Textures\GUI\Icons\Actions\NeutralToggle.dds";
				ControlOverrideThrottleAction.Action = Control_Terminal_OverrideThrottle_Action;
				ControlOverrideThrottleAction.Writer = Control_Terminal_OverrideThrottle_Writer;
				ControlOverrideThrottleAction.Enabled = Control_Visible;
				MyAPIGateway.TerminalControls.AddAction<IMyFunctionalBlock>(ControlOverrideThrottleAction);

				var IncreaseSliderOverrideThrottleAction = MyAPIGateway.TerminalControls.CreateAction<IMyFunctionalBlock>(CONTROLS_PREFIX + "IncreaseSliderOverrideThrottleAction");
				IncreaseSliderOverrideThrottleAction.Name = new StringBuilder("Increase Throttle Override");
				IncreaseSliderOverrideThrottleAction.ValidForGroups = true;
				IncreaseSliderOverrideThrottleAction.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
				IncreaseSliderOverrideThrottleAction.Action = Control_Terminal_SliderOverrideThrottle_Increase_Action;
				IncreaseSliderOverrideThrottleAction.Writer = Control_Terminal_SliderOverrideThrottle_Action_Writer;
				IncreaseSliderOverrideThrottleAction.Enabled = Control_Visible;
				MyAPIGateway.TerminalControls.AddAction<IMyFunctionalBlock>(IncreaseSliderOverrideThrottleAction);

				var DecreaseSliderOverrideThrottleAction = MyAPIGateway.TerminalControls.CreateAction<IMyFunctionalBlock>(CONTROLS_PREFIX + "DecreaseSliderOverrideThrottleAction");
				DecreaseSliderOverrideThrottleAction.Name = new StringBuilder("Decrease Throttle Override");
				DecreaseSliderOverrideThrottleAction.ValidForGroups = true;
				DecreaseSliderOverrideThrottleAction.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
				DecreaseSliderOverrideThrottleAction.Action = Control_Terminal_SliderOverrideThrottle_Decrease_Action;
				DecreaseSliderOverrideThrottleAction.Writer = Control_Terminal_SliderOverrideThrottle_Action_Writer;
				DecreaseSliderOverrideThrottleAction.Enabled = Control_Visible;
				MyAPIGateway.TerminalControls.AddAction<IMyFunctionalBlock>(DecreaseSliderOverrideThrottleAction);

				var ResetSliderOverrideThrottleAction = MyAPIGateway.TerminalControls.CreateAction<IMyFunctionalBlock>(CONTROLS_PREFIX + "ResetSliderOverrideThrottleAction");
				ResetSliderOverrideThrottleAction.Name = new StringBuilder("Reset Throttle Override");
				ResetSliderOverrideThrottleAction.ValidForGroups = true;
				ResetSliderOverrideThrottleAction.Icon = @"Textures\GUI\Icons\Actions\NeutralToggle.dds";
				ResetSliderOverrideThrottleAction.Action = Control_Terminal_SliderOverrideThrottle_Reset_Action;
				ResetSliderOverrideThrottleAction.Writer = Control_Terminal_SliderOverrideThrottle_Action_Writer;
				ResetSliderOverrideThrottleAction.Enabled = Control_Visible;
				MyAPIGateway.TerminalControls.AddAction<IMyFunctionalBlock>(ResetSliderOverrideThrottleAction);

				// properties

				// var ThrustLRProperty = MyAPIGateway.TerminalControls.CreateProperty<float, IMyJet>(CONTROLS_PREFIX + "ThrustLR");
				// ThrustLRProperty.Getter = ThrustLRProperty_Getter;
				// ThrustLRProperty.Setter = ThrustLRProperty_Setter;
				// MyAPIGateway.TerminalControls.AddControl<IMyJet>(ThrustLRProperty);

				// var ThrustUDProperty = MyAPIGateway.TerminalControls.CreateProperty<float, IMyJet>(CONTROLS_PREFIX + "ThrustUD");
				// ThrustUDProperty.Getter = ThrustUDProperty_Getter;
				// ThrustUDProperty.Setter = ThrustUDProperty_Setter;
				// MyAPIGateway.TerminalControls.AddControl<IMyJet>(ThrustUDProperty);

			}

			static GimbalJetThruster GetLogic(IMyTerminalBlock IMyJet) => IMyJet?.GameLogic?.GetAs<GimbalJetThruster>();

			static bool Control_Visible(IMyTerminalBlock IMyJet)
			{
				return GetLogic(IMyJet) != null;
			}

			static bool Control_Hidden(IMyTerminalBlock IMyJet)
			{
				if (GetLogic(IMyJet) != null)
				{
					return false;
				}
				else
				{
					return true;
				}
			}

			static bool Control_Override_Enabled(IMyTerminalBlock IMyJet)
			{
				if (GetLogic(IMyJet) != null)
				{
					return GetLogic(IMyJet).Terminal_OverrideControl;
				}
				else
				{
					return false;
				}
			}

			static bool Control_OverrideThrottle_Enabled(IMyTerminalBlock IMyJet)
			{
				if (GetLogic(IMyJet) != null)
				{
					return GetLogic(IMyJet).Terminal_OverrideThrottle;
				}
				else
				{
					return false;
				}
			}

			static bool Control_Terminal_OverrideThrottle_Getter(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				return (logic == null ? false : logic.Terminal_OverrideThrottle);
			}

			static void Control_Terminal_OverrideThrottle_Setter(IMyTerminalBlock IMyJet, bool value)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_OverrideThrottle.ValidateAndSet(value);
			}

			static void Control_Terminal_OverrideThrottle_Action(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_OverrideThrottle.ValidateAndSet(!logic.Terminal_OverrideThrottle.Value);
			}

			static void Control_Terminal_OverrideThrottle_Writer(IMyTerminalBlock IMyJet, StringBuilder writer)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					if (logic.Terminal_OverrideThrottle)
					{
						writer.Append("OvrThr \nON");
					}
					else
					{
						writer.Append("OvrThr \nOFF");
					}
			}

			static bool Control_Terminal_EnableAngularDampening_Getter(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				return (logic == null ? false : logic.Terminal_EnableAngularDampening);
			}

			static void Control_Terminal_EnableAngularDampening_Setter(IMyTerminalBlock IMyJet, bool value)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_EnableAngularDampening.ValidateAndSet(value);
			}

			static void Control_Terminal_EnableAngularDampening_Action(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_EnableAngularDampening.ValidateAndSet(!logic.Terminal_EnableAngularDampening.Value);
			}

			static void Control_Terminal_EnableAngularDampening_Writer(IMyTerminalBlock IMyJet, StringBuilder writer)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					if (logic.Terminal_EnableAngularDampening)
					{
						writer.Append("AngDamp \nON");
					}
					else
					{
						writer.Append("AngDamp \nOFF");
					}
			}

			static bool Control_Terminal_KeepThrottle_Getter(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				return (logic == null ? false : logic.Terminal_KeepThrottle);
			}

			static void Control_Terminal_KeepThrottle_Setter(IMyTerminalBlock IMyJet, bool value)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_KeepThrottle.ValidateAndSet(value);
			}

			static void Control_Terminal_KeepThrottle_Action(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_KeepThrottle.ValidateAndSet(!logic.Terminal_KeepThrottle.Value);
			}

			static void Control_Terminal_KeepThrottle_Writer(IMyTerminalBlock IMyJet, StringBuilder writer)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					if (logic.Terminal_KeepThrottle)
					{
						writer.Append("KeepThr\nON");
					}
					else
					{
						writer.Append("KeepThr\nOFF");
					}
			}

			static bool Control_Terminal_OverrideControl_Getter(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				return (logic == null ? false : logic.Terminal_OverrideControl);
			}

			static void Control_Terminal_OverrideControl_Setter(IMyTerminalBlock IMyJet, bool value)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_OverrideControl.ValidateAndSet(value);
			}

			static void Control_Terminal_OverrideControl_Action(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_OverrideControl.ValidateAndSet(!logic.Terminal_OverrideControl.Value);
			}

			static void Control_Terminal_OverrideControl_Writer(IMyTerminalBlock IMyJet, StringBuilder writer)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					if (logic.Terminal_OverrideControl)
					{
						writer.Append("Override\nON");
					}
					else
					{
						writer.Append("Override\nOFF");
					}
			}

			static float Control_Terminal_ThrustLimiter_Getter(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				return (logic == null ? 0.01f : logic.Terminal_ThrustLimiter);
			}

			static void Control_Terminal_ThrustLimiter_Setter(IMyTerminalBlock IMyJet, float value)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_ThrustLimiter.ValidateAndSet(value);
			}

			static void Control_Terminal_ThrustLimiter_Writer(IMyTerminalBlock IMyJet, StringBuilder writer)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					writer.Append((int)(logic.Terminal_ThrustLimiter * 100f)).Append('%');
			}

			static void Control_Terminal_ThrustLimiter_Action_Writer(IMyTerminalBlock IMyJet, StringBuilder writer)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					writer.Append((int)(logic.Terminal_ThrustLimiter * 100f)).Append("%\nThrLim");
			}

			static void Control_Terminal_ThrustLimiter_Increase_Action(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_ThrustLimiter.ValidateAndSet(MathHelper.Clamp(logic.Terminal_ThrustLimiter.Value + 0.1f, 0.01f, 1f));
			}

			static void Control_Terminal_ThrustLimiter_Decrease_Action(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_ThrustLimiter.ValidateAndSet(MathHelper.Clamp(logic.Terminal_ThrustLimiter.Value - 0.1f, 0.01f, 1f));
			}

			static float Control_Terminal_OverrideLR_Getter(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					return logic.Terminal_OverrideLR;
				return 0f;
			}

			static void Control_Terminal_OverrideLR_Setter(IMyTerminalBlock IMyJet, float value)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_OverrideLR.ValidateAndSet(value);
			}

			static void Control_Terminal_OverrideLR_Writer(IMyTerminalBlock IMyJet, StringBuilder writer)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					writer.Append((int)(logic.Terminal_OverrideLR * (float)JETGimbalAngleDegrees)).Append('°');
			}

			static void Control_Terminal_OverrideLR_Action_Writer(IMyTerminalBlock IMyJet, StringBuilder writer)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					writer.Append((int)(logic.Terminal_OverrideLR * (float)JETGimbalAngleDegrees)).Append("°\nOvrY");
			}

			static void Control_Terminal_OverrideLR_Increase_Action(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_OverrideLR.ValidateAndSet(MathHelper.Clamp(logic.Terminal_OverrideLR.Value + 0.1f, -1f, 1f));
			}

			static void Control_Terminal_OverrideLR_Decrease_Action(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_OverrideLR.ValidateAndSet(MathHelper.Clamp(logic.Terminal_OverrideLR.Value - 0.1f, -1f, 1f));
			}

			static float Control_Terminal_OverrideUD_Getter(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					return logic.Terminal_OverrideUD;
				return 0f;
			}

			static void Control_Terminal_OverrideUD_Setter(IMyTerminalBlock IMyJet, float value)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_OverrideUD.ValidateAndSet(value);
			}

			static void Control_Terminal_OverrideUD_Writer(IMyTerminalBlock IMyJet, StringBuilder writer)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					writer.Append((int)(logic.Terminal_OverrideUD * (float)JETGimbalAngleDegrees)).Append('°');
			}

			static void Control_Terminal_OverrideUD_Action_Writer(IMyTerminalBlock IMyJet, StringBuilder writer)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					writer.Append((int)(logic.Terminal_OverrideUD * (float)JETGimbalAngleDegrees)).Append("°\nOvrX");
			}

			static void Control_Terminal_OverrideUD_Increase_Action(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_OverrideUD.ValidateAndSet(MathHelper.Clamp(logic.Terminal_OverrideUD.Value + 0.1f, -1f, 1f));
			}

			static void Control_Terminal_OverrideUD_Decrease_Action(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_OverrideUD.ValidateAndSet(MathHelper.Clamp(logic.Terminal_OverrideUD.Value - 0.1f, -1f, 1f));
			}

			static void Control_Terminal_OverrideLR_Reset_Action(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_OverrideLR.ValidateAndSet(0);
			}

			static void Control_Terminal_OverrideUD_Reset_Action(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_OverrideUD.ValidateAndSet(0);
			}




			static float Control_Terminal_SliderOverrideThrottle_Getter(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					return logic.Terminal_SliderOverrideThrottle;
				return 0f;
			}

			static void Control_Terminal_SliderOverrideThrottle_Setter(IMyTerminalBlock IMyJet, float value)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_SliderOverrideThrottle.ValidateAndSet(value);
			}

			static void Control_Terminal_SliderOverrideThrottle_Writer(IMyTerminalBlock IMyJet, StringBuilder writer)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					writer.Append((int)(logic.Terminal_SliderOverrideThrottle * 100f)).Append('%');
			}

			static void Control_Terminal_SliderOverrideThrottle_Action_Writer(IMyTerminalBlock IMyJet, StringBuilder writer)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					writer.Append((int)(logic.Terminal_SliderOverrideThrottle * 100f)).Append("%\nOvr Thr");
			}

			static void Control_Terminal_SliderOverrideThrottle_Increase_Action(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_SliderOverrideThrottle.ValidateAndSet(MathHelper.Clamp(logic.Terminal_SliderOverrideThrottle.Value + 0.1f, 0, 1f));
			}

			static void Control_Terminal_SliderOverrideThrottle_Decrease_Action(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_SliderOverrideThrottle.ValidateAndSet(MathHelper.Clamp(logic.Terminal_SliderOverrideThrottle.Value - 0.1f, 0, 1f));
			}

			static void Control_Terminal_SliderOverrideThrottle_Reset_Action(IMyTerminalBlock IMyJet)
			{
				var logic = GetLogic(IMyJet);
				if (logic != null)
					logic.Terminal_SliderOverrideThrottle.ValidateAndSet(0);
			}



		}
	}
}
