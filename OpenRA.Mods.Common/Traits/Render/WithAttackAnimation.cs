#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.RA.Traits
{
	public class WithAttackAnimationInfo : ITraitInfo, Requires<WithFacingSpriteBodyInfo>, Requires<ArmamentInfo>, Requires<AttackBaseInfo>
	{
		[Desc("Armament name")]
		public readonly string Armament = "primary";

		[Desc("Displayed while attacking.")]
		public readonly string AttackSequence = null;

		[Desc("Displayed while targeting.")]
		public readonly string AimSequence = null;

		[Desc("Shown while reloading.")]
		public readonly string ReloadPrefix = null;

		public object Create(ActorInitializer init) { return new WithAttackAnimation(init, this); }
	}

	public class WithAttackAnimation : ITick, INotifyAttack
	{
		readonly WithAttackAnimationInfo info;
		readonly AttackBase attack;
		readonly Armament armament;
		readonly WithFacingSpriteBody wfsb;

		public WithAttackAnimation(ActorInitializer init, WithAttackAnimationInfo info)
		{
			this.info = info;
			attack = init.Self.Trait<AttackBase>();
			armament = init.Self.TraitsImplementing<Armament>()
				.Single(a => a.Info.Name == info.Armament);
			wfsb = init.Self.Trait<WithFacingSpriteBody>();
		}

		public void Attacking(Actor self, Target target, Armament a, Barrel barrel)
		{
			if (!string.IsNullOrEmpty(info.AttackSequence))
				wfsb.PlayCustomAnimation(self, info.AttackSequence);
		}

		public void Tick(Actor self)
		{
			if (string.IsNullOrEmpty(info.AimSequence) && string.IsNullOrEmpty(info.ReloadPrefix))
				return;

			var sequence = wfsb.Info.Sequence;
			if (!string.IsNullOrEmpty(info.AimSequence) && attack.IsAttacking)
				sequence = info.AimSequence;

			var prefix = (armament.IsReloading && !string.IsNullOrEmpty(info.ReloadPrefix)) ? info.ReloadPrefix : "";

			if (!string.IsNullOrEmpty(prefix) && sequence != (prefix + sequence))
				sequence = prefix + sequence;

			wfsb.DefaultAnimation.ReplaceAnim(sequence);
		}
	}
}
