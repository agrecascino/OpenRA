#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using OpenRA.Mods.Common.Effects;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("The player can disable the power individually on this actor.")]
	public class CanPowerDownInfo : UpgradableTraitInfo, Requires<PowerInfo>
	{
		[Desc("Restore power when this trait is disabled.")]
		public readonly bool CancelWhenDisabled = false;

		public readonly string IndicatorImage = "poweroff";
		public readonly string IndicatorSequence = "offline";

		public readonly string IndicatorPalette = "chrome";

		public override object Create(ActorInitializer init) { return new CanPowerDown(init.Self, this); }
	}

	public class CanPowerDown : UpgradableTrait<CanPowerDownInfo>, IPowerModifier, IResolveOrder, IDisable, INotifyOwnerChanged
	{
		[Sync] bool disabled = false;
		PowerManager power;

		public CanPowerDown(Actor self, CanPowerDownInfo info)
			: base(info)
		{
			power = self.Owner.PlayerActor.Trait<PowerManager>();
		}

		public bool Disabled { get { return disabled; } }

		public void ResolveOrder(Actor self, Order order)
		{
			if (!IsTraitDisabled && order.OrderString == "PowerDown")
			{
				disabled = !disabled;
				Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner, "Sounds", disabled ? "EnablePower" : "DisablePower", self.Owner.Faction.InternalName);
				power.UpdateActor(self);

				if (disabled)
					self.World.AddFrameEndTask(w => w.Add(new PowerdownIndicator(self)));
			}
		}

		public int GetPowerModifier()
		{
			return !IsTraitDisabled && disabled ? 0 : 100;
		}

		public void OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			power = newOwner.PlayerActor.Trait<PowerManager>();
		}

		protected override void UpgradeDisabled(Actor self)
		{
			if (!disabled || !Info.CancelWhenDisabled)
				return;
			disabled = false;
			Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner, "Sounds", "EnablePower", self.Owner.Faction.InternalName);
			power.UpdateActor(self);
		}
	}
}
