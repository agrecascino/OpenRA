#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Drawing;
using OpenRA.Effects;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Effects
{
	[Desc("Not a sprite, but an engine effect.")]
	class LaserZapInfo : IProjectileInfo
	{
		public readonly int BeamWidth = 2;
		public readonly int BeamDuration = 10;
		public readonly bool UsePlayerColor = false;
		[Desc("Laser color in (A,)R,G,B.")]
		public readonly Color Color = Color.Red;
		[Desc("Impact animation.")]
		public readonly string HitAnim = null;
		[Desc("Sequence of impact animation to use.")]
		public readonly string HitAnimSequence = "idle";
		public readonly string HitAnimPalette = "effect";

		public IEffect Create(ProjectileArgs args)
		{
			var c = UsePlayerColor ? args.SourceActor.Owner.Color.RGB : Color;
			return new LaserZap(args, this, c);
		}
	}

	class LaserZap : IEffect
	{
		readonly ProjectileArgs args;
		readonly LaserZapInfo info;
		readonly Animation hitanim;
		int ticks = 0;
		Color color;
		bool doneDamage;
		bool animationComplete;
		WPos target;

		public LaserZap(ProjectileArgs args, LaserZapInfo info, Color color)
		{
			this.args = args;
			this.info = info;
			this.color = color;
			this.target = args.PassiveTarget;

			if (!string.IsNullOrEmpty(info.HitAnim))
				this.hitanim = new Animation(args.SourceActor.World, info.HitAnim);
		}

		public void Tick(World world)
		{
			// Beam tracks target
			if (args.GuidedTarget.IsValidFor(args.SourceActor))
				target = args.GuidedTarget.CenterPosition;

			if (!doneDamage)
			{
				if (hitanim != null)
					hitanim.PlayThen(info.HitAnimSequence, () => animationComplete = true);

				args.Weapon.Impact(Target.FromPos(target), args.SourceActor, args.DamageModifiers);
				doneDamage = true;
			}

			if (hitanim != null)
				hitanim.Tick();

			if (++ticks >= info.BeamDuration && animationComplete)
				world.AddFrameEndTask(w => w.Remove(this));
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			if (wr.World.FogObscures(target) &&
				wr.World.FogObscures(args.Source))
				yield break;

			if (ticks < info.BeamDuration)
			{
				var rc = Color.FromArgb((info.BeamDuration - ticks) * 255 / info.BeamDuration, color);
				yield return new BeamRenderable(args.Source, 0, target - args.Source, info.BeamWidth, rc);
			}

			if (hitanim != null)
				foreach (var r in hitanim.Render(target, wr.Palette(info.HitAnimPalette)))
					yield return r;
		}
	}
}
