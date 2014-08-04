#region Copyright & License Information
/*
 * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Effects;
using OpenRA.GameRules;
using OpenRA.Traits;
using OpenRA.Mods.RA.Effects;

namespace OpenRA.Mods.RA
{
	public class CreateEffectWarhead : Warhead
	{
		[Desc("Explosion effect to use.")]
		public readonly string Explosion = null;

		[Desc("Palette to use for explosion effect.")]
		public readonly string ExplosionPalette = "effect";

		[Desc("Sound to play on impact.")]
		public readonly string ImpactSound = null;

		[Desc("What diplomatic stances are affected.")]
		public readonly ImpactType[] ValidImpactTypes = { ImpactType.Ground, ImpactType.Water, ImpactType.Air, ImpactType.GroundHit, ImpactType.WaterHit, ImpactType.AirHit };

		[Desc("What diplomatic stances are affected.")]
		public readonly ImpactType[] InvalidImpactTypes = { };

		public override void DoImpact(Target target, Actor firedBy, float firepowerModifier)
		{
			DoImpact(target.CenterPosition, firedBy, firepowerModifier);
		}

		public static ImpactType GetImpactType(World world, CPos cell, WPos pos)
		{
			var isAir = pos.Z > 0;
			var isWater = pos.Z <= 0 && world.Map.GetTerrainInfo(cell).IsWater;
			var isDirectHit = GetDirectHit(world, cell, pos);

			if (isAir && !isDirectHit)
				return ImpactType.Air;

			else if (isWater && !isDirectHit)
				return ImpactType.Water;

			else if (isAir && isDirectHit)
				return ImpactType.AirHit;

			else if (isWater && isDirectHit)
				return ImpactType.WaterHit;

			else if (isDirectHit)
				return ImpactType.GroundHit;

			return ImpactType.Ground;
		}

		public static bool GetDirectHit(World world, CPos cell, WPos pos)
		{
			foreach (var unit in world.ActorMap.GetUnitsAt(cell))
			{
				var healthInfo = unit.Info.Traits.GetOrDefault<HealthInfo>();
				if (healthInfo == null)
					continue;

				// If the impact position is within any actor's health radius, we have a direct hit
				if ((unit.CenterPosition - pos).LengthSquared <= healthInfo.Radius.Range * healthInfo.Radius.Range)
					return true;
			}

			return false;
		}

		public void DoImpact(WPos pos, Actor firedBy, float firepowerModifier)
		{
			var world = firedBy.World;
			var targetTile = world.Map.CellContaining(pos);

			if (!world.Map.Contains(targetTile))
				return;
			
			var isValid = IsValidImpact(pos, firedBy);

			if ((Explosion != null) && isValid)
				world.AddFrameEndTask(w => w.Add(new Explosion(w, pos, Explosion, ExplosionPalette)));

			if ((ImpactSound != null) && isValid)
				Sound.Play(ImpactSound, pos);
		}

		public override float EffectivenessAgainst(ActorInfo ai) { return 1f; }

		public bool IsValidImpact(WPos pos, Actor firedBy)
		{
			var world = firedBy.World;
			var targetTile = world.Map.CellContaining(pos);
			var impactType = GetImpactType(world, targetTile, pos);
			if ((!ValidImpactTypes.Contains(impactType)) || (InvalidImpactTypes.Contains(impactType)))
				return false;

			return true;
		}
	}
}
