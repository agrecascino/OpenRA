#region Copyright & License Information
/*
 * Copyright 2007-2013 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.FileFormats;
using OpenRA.Mods.RA.Air;
using OpenRA.Mods.RA.Buildings;
using OpenRA.Mods.RA.Move;
using OpenRA.Traits;
using XRandom = OpenRA.Thirdparty.Random;

namespace OpenRA.Mods.RA.AI
{
	public class HackyAIInfo : IBotInfo, ITraitInfo
	{
		public readonly string Name = "Unnamed Bot";
		public readonly int SquadSize = 8;

		//intervals
		public readonly int AssignRolesInterval = 20;
		public readonly int RushInterval = 600;
		public readonly int AttackForceInterval = 30;

		public readonly string RallypointTestBuilding = "fact";		// temporary hack to maintain previous rallypoint behavior.
		public readonly string[] UnitQueues = { "Vehicle", "Infantry", "Plane", "Ship", "Aircraft" };
		public readonly bool ShouldRepairBuildings = true;

		string IBotInfo.Name { get { return this.Name; } }

		[FieldLoader.LoadUsing("LoadUnits")]
		public readonly Dictionary<string, float> UnitsToBuild = null;

		[FieldLoader.LoadUsing("LoadBuildings")]
		public readonly Dictionary<string, float> BuildingFractions = null;

		[FieldLoader.LoadUsing("LoadUnitsCommonNames")]
		public readonly Dictionary<string, string[]> UnitsCommonNames = null;

		[FieldLoader.LoadUsing("LoadBuildingsCommonNames")]
		public readonly Dictionary<string, string[]> BuildingCommonNames = null;

		[FieldLoader.LoadUsing("LoadBuildingLimits")]
		public readonly Dictionary<string, int> BuildingLimits = null;

		static object LoadActorList(MiniYaml y, string field)
		{
			return LoadList<float>(y, field);
		}

		static object LoadListList(MiniYaml y, string field)
		{
			return LoadList<string[]>(y,field);
		}

		static object LoadList<ValueType>(MiniYaml y, string field)
		{
			return y.NodesDict.ContainsKey(field)
				? y.NodesDict[field].NodesDict.ToDictionary(
					a => a.Key,
					a => FieldLoader.GetValue<ValueType>(field, a.Value.Value))
				: new Dictionary<string, ValueType>();
		}

		static object LoadUnits(MiniYaml y) { return LoadActorList(y, "UnitsToBuild"); }
		static object LoadBuildings(MiniYaml y) { return LoadActorList(y, "BuildingFractions"); }

		static object LoadUnitsCommonNames(MiniYaml y) { return LoadListList(y, "UnitsCommonNames"); }
		static object LoadBuildingsCommonNames(MiniYaml y) { return LoadListList(y, "BuildingCommonNames"); }

		static object LoadBuildingLimits(MiniYaml y) { return LoadList<int>(y, "BuildingLimits"); }

		public object Create(ActorInitializer init) { return new HackyAI(this); }
	}

	public class Enemy { public int Aggro; }

	public enum BuildingType { Building, Defense, Refinery }

	public class HackyAI : ITick, IBot, INotifyDamage
	{
		bool enabled;
		public int ticks;
		public Player p;
		public XRandom random;
		public CPos baseCenter;
		PowerManager playerPower;
		SupportPowerManager supportPowerMngr;
		PlayerResources playerResource;
		readonly BuildingInfo rallypointTestBuilding;		// temporary hack
		internal readonly HackyAIInfo Info;

		string[] resourceTypes;

		RushFuzzy rushFuzzy = new RushFuzzy();

		Cache<Player,Enemy> aggro = new Cache<Player, Enemy>( _ => new Enemy() );
		BaseBuilder[] builders;

		const int MaxBaseDistance = 40;
		public const int feedbackTime = 30;		// ticks; = a bit over 1s. must be >= netlag.

		public World world { get { return p.PlayerActor.World; } }
		IBotInfo IBot.Info { get { return this.Info; } }

		public HackyAI(HackyAIInfo Info)
		{
			this.Info = Info;
			// temporary hack.
			this.rallypointTestBuilding = Rules.Info[Info.RallypointTestBuilding].Traits.Get<BuildingInfo>();
		}

		public static void BotDebug(string s, params object[] args)
		{
			if (Game.Settings.Debug.BotDebug)
				Game.Debug(s, args);
		}

		/* called by the host's player creation code */
		public void Activate(Player p)
		{
			this.p = p;
			enabled = true;
			playerPower = p.PlayerActor.Trait<PowerManager>();
			supportPowerMngr = p.PlayerActor.Trait<SupportPowerManager>();
			playerResource = p.PlayerActor.Trait<PlayerResources>();
			builders = new BaseBuilder[] {
				new BaseBuilder( this, "Building", q => ChooseBuildingToBuild(q, false) ),
				new BaseBuilder( this, "Defense", q => ChooseBuildingToBuild(q, true) ) };

			random = new XRandom((int)p.PlayerActor.ActorID);

			resourceTypes = Rules.Info["world"].Traits.WithInterface<ResourceTypeInfo>()
				.Select(t => t.TerrainType).ToArray();
		}

		int GetPowerProvidedBy(ActorInfo building)
		{
			var bi = building.Traits.GetOrDefault<BuildingInfo>();
			if (bi == null) return 0;
			return bi.Power;
		}

		ActorInfo ChooseRandomUnitToBuild(ProductionQueue queue)
		{
			var buildableThings = queue.BuildableItems();
			if (!buildableThings.Any()) return null;
			var unit = buildableThings.ElementAtOrDefault(random.Next(buildableThings.Count()));
			if (HasAdequateAirUnits(unit))
				return unit;
			return null;
		}

		ActorInfo ChooseUnitToBuild(ProductionQueue queue)
		{
			var buildableThings = queue.BuildableItems();
			if (!buildableThings.Any()) return null;

			var myUnits = p.World
				.ActorsWithTrait<IPositionable>()
				.Where(a => a.Actor.Owner == p)
				.Select(a => a.Actor.Info.Name).ToArray();

			foreach (var unit in Info.UnitsToBuild)
				if (buildableThings.Any(b => b.Name == unit.Key))
					if (myUnits.Count(a => a == unit.Key) < unit.Value * myUnits.Length)
						if (HasAdequateAirUnits(Rules.Info[unit.Key]))
							return Rules.Info[unit.Key];

			return null;
		}

		int CountBuilding(string frac, Player owner)
		{
			return world.ActorsWithTrait<Building>().Where(a => a.Actor.Owner == owner && a.Actor.Info.Name == frac).Count();
		}

		int CountUnits(string unit, Player owner)
		{
			return world.ActorsWithTrait<IPositionable>().Where(a => a.Actor.Owner == owner && a.Actor.Info.Name == unit).Count();
		}

		int? CountBuildingByCommonName(string commonName, Player owner)
		{
			if(Info.BuildingCommonNames.ContainsKey(commonName))
				return world.ActorsWithTrait<Building>()
					.Where(a => a.Actor.Owner == owner && Info.BuildingCommonNames[commonName].Contains(a.Actor.Info.Name)).Count();
			return null;
		}

		ActorInfo GetBuildingInfoByCommonName(string commonName, Player owner)
		{
			if (commonName == "ConstructionYard")
				return Rules.Info.Where(k => Info.BuildingCommonNames[commonName].Contains(k.Key)).Random(random).Value;
			return GetInfoByCommonName(Info.BuildingCommonNames, commonName, owner);
		}

		ActorInfo GetUnitInfoByCommonName(string commonName, Player owner)
		{
			return GetInfoByCommonName(Info.UnitsCommonNames, commonName, owner);
		}

		ActorInfo GetInfoByCommonName(Dictionary<string, string[]> names, string commonName, Player owner)
		{
			if (!names.Any() || !names.ContainsKey(commonName)) return null;
			return Rules.Info.Where(k => names[commonName].Contains(k.Key) &&
				k.Value.Traits.Get<BuildableInfo>().Owner.Contains(owner.Country.Race)).Random(random).Value; //random is shit
		}

		bool HasAdequatePower()
		{
			/* note: CNC `fact` provides a small amount of power. don't get jammed because of that. */
			return playerPower.PowerProvided > 50 &&
				playerPower.PowerProvided > playerPower.PowerDrained * 1.2;
		}

		bool HasAdequateFact()
		{
			if (CountBuildingByCommonName("ConstructionYard", p) == 0 && CountBuildingByCommonName("VehiclesFactory", p) > 0)
				return false;
			return true;
		}

		bool HasAdequateProc()
		{
			if (CountBuildingByCommonName("Refinery", p) == 0 && CountBuildingByCommonName("Power", p) > 0)
				return false;
			return true;
		}

		bool HasMinimumProc()
		{
			if (CountBuildingByCommonName("Refinery", p) < 2 && CountBuildingByCommonName("Power", p) > 0 &&
				CountBuildingByCommonName("Barracks",p) > 0)
				return false;
			return true;
		}

		bool HasAdequateNumber(string frac, Player owner)
		{
			if (Info.BuildingLimits.ContainsKey(frac))
				if (CountBuilding(frac, owner) < Info.BuildingLimits[frac])
					return true;
				else
					return false;

			return true;
		}

		//for mods like RA (number of building must match the number of aircraft)
		bool HasAdequateAirUnits(ActorInfo actorInfo)
		{
			if (!actorInfo.Traits.Contains<ReloadsInfo>() && actorInfo.Traits.Contains<LimitedAmmoInfo>() 
				&& actorInfo.Traits.Contains<AircraftInfo>())
			{
				var countOwnAir = CountUnits(actorInfo.Name, p);
				var countBuildings = CountBuilding(actorInfo.Traits.Get<AircraftInfo>().RearmBuildings.FirstOrDefault(), p);
				if (countOwnAir >= countBuildings)
					return false;
			}
			return true;
		}

		ActorInfo ChooseBuildingToBuild(ProductionQueue queue, bool isDefense)
		{
			var buildableThings = queue.BuildableItems();

			if (!isDefense)
			{
				if (!HasAdequatePower())	/* try to maintain 20% excess power */
					/* find the best thing we can build which produces power */
					return buildableThings.Where(a => GetPowerProvidedBy(a) > 0)
						.OrderByDescending(a => GetPowerProvidedBy(a)).FirstOrDefault();

				if (playerResource.AlertSilo)
					return GetBuildingInfoByCommonName("Silo", p);

				if (!HasAdequateProc() || !HasMinimumProc())
					return GetBuildingInfoByCommonName("Refinery", p);
			}
			var myBuildings = p.World
				.ActorsWithTrait<Building>()
				.Where( a => a.Actor.Owner == p )
				.Select(a => a.Actor.Info.Name).ToArray();

			foreach (var frac in Info.BuildingFractions)
				if (buildableThings.Any(b => b.Name == frac.Key))
					if (myBuildings.Count(a => a == frac.Key) < frac.Value * myBuildings.Length && HasAdequateNumber(frac.Key, p) &&
						playerPower.ExcessPower >= Rules.Info[frac.Key].Traits.Get<BuildingInfo>().Power)
						return Rules.Info[frac.Key];

			return null;
		}

		bool NoBuildingsUnder(IEnumerable<CPos> cells)
		{
			var bi = world.WorldActor.Trait<BuildingInfluence>();
			return cells.All(c => bi.GetBuildingAt(c) == null);
		}

		CPos defenseCenter;
		public CPos? ChooseBuildLocation(string actorType, BuildingType type)
		{
			return ChooseBuildLocation(actorType, true, MaxBaseDistance, type);
		}

		public CPos? ChooseBuildLocation(string actorType, bool distanceToBaseIsImportant, int maxBaseDistance, BuildingType type)
		{
			var bi = Rules.Info[actorType].Traits.Get<BuildingInfo>();
			if (bi == null) return null;

			Func<WPos, CPos, CPos?> findPos = (WPos pos, CPos center) =>
			{
				for (var k = MaxBaseDistance; k >= 0; k--)
				{
					var tlist = world.FindTilesInCircle(center, k)
						.OrderBy(a => (a.CenterPosition - pos).LengthSquared);
					foreach (var t in tlist)
						if (world.CanPlaceBuilding(actorType, bi, t, null))
							if (bi.IsCloseEnoughToBase(world, p, actorType, t))
								if (NoBuildingsUnder(Util.ExpandFootprint(FootprintUtils.Tiles(actorType, bi, t), false)))
									return t;
				}
				return null;
			};

			switch(type)
			{
				case BuildingType.Defense:
					Actor enemyBase = FindEnemyBuildingClosestToPos(baseCenter.CenterPosition);
					return enemyBase != null ? findPos(enemyBase.CenterPosition, defenseCenter) : null;

				case BuildingType.Refinery:
					var tilesPos = world.FindTilesInCircle(baseCenter, MaxBaseDistance)
						.Where(a => resourceTypes.Contains(world.GetTerrainType(new CPos(a.X, a.Y))))
						.OrderBy(a => (a.CenterPosition - baseCenter.CenterPosition).LengthSquared);
					return tilesPos.Any() ? findPos(tilesPos.First().CenterPosition, baseCenter) : null;

				case BuildingType.Building:
					for (var k = 0; k < maxBaseDistance; k++)
						foreach (var t in world.FindTilesInCircle(baseCenter, k))
							if (world.CanPlaceBuilding(actorType, bi, t, null))
							{
								if (distanceToBaseIsImportant)
									if (!bi.IsCloseEnoughToBase(world, p, actorType, t))
										continue;
								if (NoBuildingsUnder(Util.ExpandFootprint(FootprintUtils.Tiles(actorType, bi, t), false)))
									return t;
							}
					break;
			}

			return null;		// i don't know where to put it.
		}

		public void Tick(Actor self)
		{
			if (!enabled)
				return;

			ticks++;

			if (ticks == 1)
				DeployMcv(self);

			if (ticks % feedbackTime == 0)
				ProductionUnits(self);
			
			AssignRolesToIdleUnits(self);
			SetRallyPointsForNewProductionBuildings(self);
			TryToUseSupportPower(self);

			foreach (var b in builders)
				b.Tick();
		}

		internal Actor ChooseEnemyTarget()
		{
			var liveEnemies = world.Players
				.Where(q => p != q && p.Stances[q] == Stance.Enemy)
				.Where(q => p.WinState == WinState.Undefined && q.WinState == WinState.Undefined);

			if (!liveEnemies.Any())
				return null;

			var leastLikedEnemies = liveEnemies
				.GroupBy(e => aggro[e].Aggro)
				.OrderByDescending(g => g.Key)
				.FirstOrDefault();

			Player enemy;
			if (leastLikedEnemies == null)
				enemy = liveEnemies.FirstOrDefault();
			else
				enemy = leastLikedEnemies.Random(random);

			/* pick something worth attacking owned by that player */
			var targets = world.Actors
				.Where(a => a.Owner == enemy && a.HasTrait<IOccupySpace>());
			Actor target = null;

			if (targets.Any())
				target = targets.ClosestTo(baseCenter.CenterPosition);

			if (target == null)
			{
				/* Assume that "enemy" has nothing. Cool off on attacks. */
				aggro[enemy].Aggro = aggro[enemy].Aggro / 2 - 1;
				Log.Write("debug", "Bot {0} couldn't find target for player {1}", this.p.ClientIndex, enemy.ClientIndex);

				return null;
			}

			/* bump the aggro slightly to avoid changing our mind */
			if (leastLikedEnemies.Count() > 1)
				aggro[enemy].Aggro++;

			return target;
		}

		internal Actor FindClosestEnemy(WPos pos)
		{
			var allEnemyUnits = world.Actors
				.Where(unit => p.Stances[unit.Owner] == Stance.Enemy && !unit.HasTrait<Husk>() &&
					unit.HasTrait<ITargetable>()).ToList();

			if (allEnemyUnits.Count > 0)
				return allEnemyUnits.ClosestTo(pos);
			return null;
		}

		internal Actor FindClosestEnemy(WPos pos, WRange radius)
		{
			var enemyUnits = world.FindActorsInCircle(pos, radius)
								.Where(unit => p.Stances[unit.Owner] == Stance.Enemy &&
									!unit.HasTrait<Husk>() && unit.HasTrait<ITargetable>()).ToList();

			if (enemyUnits.Count > 0)
				return enemyUnits.ClosestTo(pos);
			return null;
		}

		List<Actor> FindEnemyConstructionYards()
		{
			var bases = world.Actors.Where(a => p.Stances[a.Owner] == Stance.Enemy && !a.Destroyed
				&& a.HasTrait<BaseBuilding>() && !a.HasTrait<Mobile>()).ToList();
			return bases != null ? bases : new List<Actor>();
		}

		Actor FindEnemyBuildingClosestToPos(WPos pos)
		{
			var closestBuilding = world.Actors.Where(a => p.Stances[a.Owner] == Stance.Enemy
			   && !a.Destroyed && a.HasTrait<Building>()).ClosestTo(pos);
			return closestBuilding;
		}

		List<Squad> squads = new List<Squad>();

		List<Actor> unitsHangingAroundTheBase = new List<Actor>();
		//Units that the ai already knows about. Any unit not on this list needs to be given a role.
		List<Actor> activeUnits = new List<Actor>();

		void CleanSquads()
		{
			squads.RemoveAll(s => s.IsEmpty);
			foreach (Squad squad in squads)
				squad.units.RemoveAll(a => a.Destroyed || a.IsDead());
		}

		//use of this function requires that one squad of this type. Hence it is a piece of shit
		Squad GetSquadOfType(SquadType type)
		{
			return squads.Where(s => s.type == type).FirstOrDefault();
		}

		Squad RegisterNewSquad(SquadType type, Actor target = null)
		{
			var ret = new Squad(this, type, target);
			squads.Add(ret);
			return ret;
		}

		int assignRolesTicks = 0;
		int rushTicks = 0;
		int attackForceTicks = 0;

		void AssignRolesToIdleUnits(Actor self)
		{
			CleanSquads();
			activeUnits.RemoveAll(a => a.Destroyed || a.IsDead());
			unitsHangingAroundTheBase.RemoveAll(a => a.Destroyed || a.IsDead());

			if (--rushTicks <= 0)
			{
				rushTicks = Info.RushInterval;
				TryToRushAttack();
			}

			if (--attackForceTicks <= 0)
			{
				attackForceTicks = Info.AttackForceInterval;
				foreach (var s in squads)
					s.Update();
			}

			if (--assignRolesTicks > 0)
				return;
			else
				assignRolesTicks = Info.AssignRolesInterval;

			GiveOrdersToIdleHarvesters();
			FindNewUnits(self);
			CreateAttackForce();
			FindAndDeployMcv(self);
		}

		void GiveOrdersToIdleHarvesters()
		{
			// Find idle harvesters and give them orders:
			foreach (var a in activeUnits)
			{
				var harv = a.TraitOrDefault<Harvester>();
				if (harv == null) continue;

				if (!a.IsIdle)
				{
					Activity act = a.GetCurrentActivity();
					// A Wait activity is technically idle:
					if ((act.GetType() != typeof(OpenRA.Mods.RA.Activities.Wait)) &&
						(act.NextActivity == null || act.NextActivity.GetType() != typeof(OpenRA.Mods.RA.Activities.FindResources)))
						continue;
				}
				if (!harv.IsEmpty) continue;

				// Tell the idle harvester to quit slacking:
				world.IssueOrder(new Order("Harvest", a, false));
			}   
		}

		void FindNewUnits(Actor self)
		{
			var newUnits = self.World.ActorsWithTrait<IPositionable>()
				.Where(a => a.Actor.Owner == p && !a.Actor.HasTrait<BaseBuilding>()
			&& !activeUnits.Contains(a.Actor))
			.Select(a => a.Actor).ToArray();

			foreach (var a in newUnits)
			{
				BotDebug("AI: Found a newly built unit");
				if (a.HasTrait<Harvester>())
					world.IssueOrder(new Order("Harvest", a, false));
				else
					unitsHangingAroundTheBase.Add(a);
				if (a.HasTrait<Aircraft>() && a.HasTrait<AttackBase>())
				{
					var air = GetSquadOfType(SquadType.Air);
					if (air == null)
						air = RegisterNewSquad(SquadType.Air);

					air.units.Add(a);
				}
				activeUnits.Add(a);
			}  
		}

		void CreateAttackForce()
		{
			/* Create an attack force when we have enough units around our base. */
			// (don't bother leaving any behind for defense.)
			var randomizedSquadSize = Info.SquadSize + random.Next(30);

			if (unitsHangingAroundTheBase.Count >= randomizedSquadSize)
			{
				var attackForce = RegisterNewSquad(SquadType.Assault);

				foreach (var a in unitsHangingAroundTheBase)
					if (!a.HasTrait<Aircraft>())
						attackForce.units.Add(a);
				unitsHangingAroundTheBase.Clear();
			}
		}

		void TryToRushAttack()
		{
			var allEnemyBaseBuilder = FindEnemyConstructionYards();
			var ownUnits = activeUnits
				.Where(unit => unit.HasTrait<AttackBase>() && !unit.HasTrait<Aircraft>() && unit.IsIdle).ToList();
			if (!allEnemyBaseBuilder.Any() || (ownUnits.Count < Info.SquadSize)) return;
			foreach (var b in allEnemyBaseBuilder)
			{
				var enemys = world.FindActorsInCircle(b.CenterPosition, WRange.FromCells(15))
					.Where(unit => p.Stances[unit.Owner] == Stance.Enemy && unit.HasTrait<AttackBase>()).ToList();
				
				rushFuzzy.CalculateFuzzy(ownUnits, enemys);
				if (rushFuzzy.CanAttack)
				{
					var target = enemys.Any() ? enemys.Random(random) : b;
					var rush = GetSquadOfType(SquadType.Rush);
					if (rush == null)
						rush = RegisterNewSquad(SquadType.Rush, target);

					foreach (var a3 in ownUnits)
						rush.units.Add(a3);
		  
					return;
				}
			}
		}

		void ProtectOwn(Actor attacker)
		{
			var protectSq = GetSquadOfType(SquadType.Protection);
			if (protectSq == null)
				protectSq = RegisterNewSquad(SquadType.Protection, attacker);

			if (!protectSq.TargetIsValid)
				protectSq.Target = attacker;
			if (protectSq.IsEmpty)
			{
				var ownUnits = world.FindActorsInCircle(baseCenter.CenterPosition, WRange.FromCells(15))
									.Where(unit => unit.Owner == p && !unit.HasTrait<Building>()
										&& unit.HasTrait<AttackBase>()).ToList();
				foreach (var a in ownUnits)
					protectSq.units.Add(a);
			}
		}

		bool IsRallyPointValid(CPos x)
		{
			// this is actually WRONG as soon as HackyAI is building units with a variety of
			// movement capabilities. (has always been wrong)
			return world.IsCellBuildable(x, rallypointTestBuilding);
		}

		void SetRallyPointsForNewProductionBuildings(Actor self)
		{
			var buildings = self.World.ActorsWithTrait<RallyPoint>()
				.Where(rp => rp.Actor.Owner == p &&
					!IsRallyPointValid(rp.Trait.rallyPoint)).ToArray();

			if (buildings.Length > 0)
				BotDebug("Bot {0} needs to find rallypoints for {1} buildings.",
					p.PlayerName, buildings.Length);

			foreach (var a in buildings)
			{
				CPos newRallyPoint = ChooseRallyLocationNear(a.Actor.Location);
				world.IssueOrder(new Order("SetRallyPoint", a.Actor, false) { TargetLocation = newRallyPoint });
			}
		}

		//won't work for shipyards...
		CPos ChooseRallyLocationNear(CPos startPos)
		{
			var possibleRallyPoints = world.FindTilesInCircle(startPos, 8).Where(IsRallyPointValid).ToArray();
			if (possibleRallyPoints.Length == 0)
			{
				BotDebug("Bot Bug: No possible rallypoint near {0}", startPos);
				return startPos;
			}

			return possibleRallyPoints.Random(random);
		}

		void DeployMcv(Actor self)
		{
			/* find our mcv and deploy it */
			var mcv = self.World.Actors
				.FirstOrDefault(a => a.Owner == p && a.HasTrait<BaseBuilding>());

			if (mcv != null)
			{
				baseCenter = mcv.Location;
				defenseCenter = baseCenter;
				//Don't transform the mcv if it is a fact
				if (mcv.HasTrait<Mobile>())
					world.IssueOrder(new Order("DeployTransform", mcv, false));
			}
			else
				BotDebug("AI: Can't find BaseBuildUnit.");
		}

		void FindAndDeployMcv(Actor self)
		{
			var mcvs = self.World.Actors.Where(a => a.Owner == p && a.HasTrait<BaseBuilding>()).ToArray();
			if (!mcvs.Any())
				return;
			else
				foreach (var mcv in mcvs)
					if (mcv != null)
						//Don't transform the mcv if it is a fact
						if (mcv.HasTrait<Mobile>())
						{
							if (mcv.IsMoving()) return;
							var maxBaseDistance = world.Map.MapSize.X > world.Map.MapSize.Y ? world.Map.MapSize.X : world.Map.MapSize.Y;
							ActorInfo aInfo = GetUnitInfoByCommonName("Mcv",p);
							if (aInfo == null) return;
							string intoActor = aInfo.Traits.Get<TransformsInfo>().IntoActor;
							var desiredLocation = ChooseBuildLocation(intoActor, false, maxBaseDistance, BuildingType.Building);
							if (desiredLocation == null)
								return;
							world.IssueOrder(new Order("Move", mcv, false) { TargetLocation = desiredLocation.Value });
							world.IssueOrder(new Order("DeployTransform", mcv, false));
						}
		}

		void TryToUseSupportPower(Actor self)
		{
			if (supportPowerMngr == null) return;
			var powers = supportPowerMngr.Powers.Where(p => !p.Value.Disabled);

			foreach (var kv in powers)
			{
				var sp = kv.Value;
				if (sp.Ready)
				{
					var attackLocation = FindAttackLocationToSupportPower(5);
					if (attackLocation == null) return;

					world.IssueOrder(new Order(sp.Info.OrderName, supportPowerMngr.self, false) { TargetLocation = attackLocation.Value });
				}
			}
		}

		CPos? FindAttackLocationToSupportPower(int radiusOfPower)
		{
			CPos? resLoc = null;
			int countUnits = 0;

			int x = (world.Map.MapSize.X % radiusOfPower) == 0 ? world.Map.MapSize.X : world.Map.MapSize.X + radiusOfPower;
			int y = (world.Map.MapSize.Y % radiusOfPower) == 0 ? world.Map.MapSize.Y : world.Map.MapSize.Y + radiusOfPower;

			for (int i = 0; i < x; i += radiusOfPower * 2)
				for (int j = 0; j < y; j += radiusOfPower * 2)
				{
					CPos pos = new CPos(i, j);
					var targets = world.FindActorsInCircle(pos.CenterPosition, WRange.FromCells(radiusOfPower)).ToList();
					var enemys = targets.Where(unit => p.Stances[unit.Owner] == Stance.Enemy).ToList();
					var ally = targets.Where(unit => p.Stances[unit.Owner] == Stance.Ally || unit.Owner == p).ToList();

					if (enemys.Count < ally.Count || !enemys.Any())
						continue;
					if (enemys.Count > countUnits)
					{
						countUnits = enemys.Count;
						resLoc = enemys.Random(random).Location;
					}
				}
			return resLoc;
		}

		internal IEnumerable<ProductionQueue> FindQueues(string category)
		{
			return world.ActorsWithTrait<ProductionQueue>()
				.Where(a => a.Actor.Owner == p && a.Trait.Info.Type == category)
				.Select(a => a.Trait);
		}

		void ProductionUnits(Actor self)
		{
			if (!HasAdequateProc()) /* Stop building until economy is back on */
				return;
			if (!HasAdequateFact())
				if (!self.World.Actors.Where(a => a.Owner == p && a.HasTrait<BaseBuilding>() && a.HasTrait<Mobile>()).Any())
					BuildUnit("Vehicle", GetUnitInfoByCommonName("Mcv",p).Name);
			foreach (var q in Info.UnitQueues)
			{
				if (unitsHangingAroundTheBase.Count < 12)
				{
					BuildUnit(q, true);
					continue;
				}
				BuildUnit(q, false);
			}
		}

		void BuildUnit(string category, bool buildRandom)
		{
			// Pick a free queue
			var queue = FindQueues(category).FirstOrDefault( q => q.CurrentItem() == null );
			if (queue == null)
				return;

			ActorInfo unit;
			if(buildRandom)
				unit = ChooseRandomUnitToBuild(queue);
			else
				unit = ChooseUnitToBuild(queue);

			if (unit != null && Info.UnitsToBuild.Any(u => u.Key == unit.Name))
				world.IssueOrder(Order.StartProduction(queue.self, unit.Name, 1));
		}

		void BuildUnit(string category, string name)
		{
			var queue = FindQueues(category).FirstOrDefault( q => q.CurrentItem() == null );
			if (queue == null) return;
			if(Rules.Info[name] != null)
				world.IssueOrder(Order.StartProduction(queue.self, name, 1));
		}

		public void Damaged(Actor self, AttackInfo e)
		{
			if (!enabled) return;
			if (e.Attacker.Destroyed) return;
			if (!e.Attacker.HasTrait<ITargetable>()) return;

			if (Info.ShouldRepairBuildings && self.HasTrait<RepairableBuilding>())
				if (e.DamageState > DamageState.Light && e.PreviousDamageState <= DamageState.Light)
				{
					BotDebug("Bot noticed damage {0} {1}->{2}, repairing.",
						self, e.PreviousDamageState, e.DamageState);
					world.IssueOrder(new Order("RepairBuilding", self.Owner.PlayerActor, false)
						{ TargetActor = self });
				}

			if (e.Attacker != null && e.Damage > 0)
				aggro[e.Attacker.Owner].Aggro += e.Damage;

			//protected harvesters or building
			if ((self.HasTrait<Harvester>() || self.HasTrait<Building>()) &&
			    p.Stances[e.Attacker.Owner] == Stance.Enemy)
			{
				defenseCenter = e.Attacker.Location;
				ProtectOwn(e.Attacker);
			}
		}
	}
}
