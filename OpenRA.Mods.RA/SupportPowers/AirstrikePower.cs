﻿#region Copyright & License Information
/*
 * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.RA.Activities;
using OpenRA.Mods.RA.Air;
using OpenRA.Mods.RA.Effects;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.RA
{
	class AirstrikePowerInfo : SupportPowerInfo
	{
		[ActorReference]
		public readonly string UnitType = "badr.bomber";
		public readonly int SquadSize = 1;
		public readonly WVec SquadOffset = new WVec(-1536, 1536, 0);

		public readonly int QuantizedFacings = 32;
		public readonly WRange Cordon = new WRange(5120);

		[ActorReference]
		[Desc("Actor to spawn when the aircraft first enter the map")]
		public readonly string FlareActor = null;

		[Desc("Amount of time to keep the flare alive after the aircraft have finished attacking")]
		public readonly int FlareRemoveDelay = 25;

		[ActorReference]
		[Desc("Actor to spawn when the aircraft start attacking")]
		public readonly string CameraActor = null;

		[Desc("Amount of time to keep the camera alive after the aircraft have finished attacking")]
		public readonly int CameraRemoveDelay = 25;

		[Desc("Weapon range offset to apply during the beacon clock calculation")]
		public readonly WRange BeaconDistanceOffset = WRange.FromCells(6);

		public override object Create(ActorInitializer init) { return new AirstrikePower(init.self, this); }
	}

	class AirstrikePower : SupportPower
	{
		public AirstrikePower(Actor self, AirstrikePowerInfo info)
			: base(self, info) { }

		public override void Activate(Actor self, Order order, SupportPowerManager manager)
		{
			base.Activate(self, order, manager);

			var info = Info as AirstrikePowerInfo;
			var attackFacing = Util.QuantizeFacing(self.World.SharedRandom.Next(256), info.QuantizedFacings) * (256 / info.QuantizedFacings);
			var attackRotation = WRot.FromFacing(attackFacing);
			var delta = new WVec(0, -1024, 0).Rotate(attackRotation);

			var altitude = self.World.Map.Rules.Actors[info.UnitType].Traits.Get<PlaneInfo>().CruiseAltitude.Range;
			var target = order.TargetLocation.CenterPosition + new WVec(0, 0, altitude);
			var startEdge = target - (self.World.Map.DistanceToEdge(target, -delta) + info.Cordon).Range * delta / 1024;
			var finishEdge = target + (self.World.Map.DistanceToEdge(target, delta) + info.Cordon).Range * delta / 1024;

			Actor flare = null;
			Actor camera = null;
			Beacon beacon = null;
			Dictionary<Actor, bool> aircraftInRange = new Dictionary<Actor, bool>();

			Action<Actor> onEnterRange = a =>
			{
				// Spawn a camera and remove the beacon when the first plane enters the target area
				if (info.CameraActor != null && !aircraftInRange.Any(kv => kv.Value))
				{
					self.World.AddFrameEndTask(w =>
					{
						camera = w.CreateActor(info.CameraActor, new TypeDictionary
						{
							new LocationInit(order.TargetLocation),
							new OwnerInit(self.Owner),
						});
					});
				}

				if (beacon != null)
				{
					self.World.AddFrameEndTask(w =>
					{
						w.Remove(beacon);
						beacon = null;
					});
				}

				aircraftInRange[a] = true;
			};

			Action<Actor> onExitRange = a =>
			{
				aircraftInRange[a] = false;

				// Remove the camera and flare when the final plane leaves the target area
				if (!aircraftInRange.Any(kv => kv.Value))
				{
					if (camera != null)
					{
						camera.QueueActivity(new Wait(info.CameraRemoveDelay));
						camera.QueueActivity(new RemoveSelf());
					}

					if (flare != null)
					{
						flare.QueueActivity(new Wait(info.FlareRemoveDelay));
						flare.QueueActivity(new RemoveSelf());
					}

					camera = flare = null;
				}
			};

			self.World.AddFrameEndTask(w =>
			{
				if (info.FlareActor != null)
				{
					flare = w.CreateActor(info.FlareActor, new TypeDictionary
					{
						new LocationInit(order.TargetLocation),
						new OwnerInit(self.Owner),
					});
				}

				var notification = self.Owner.IsAlliedWith(self.World.RenderPlayer) ? Info.LaunchSound : Info.IncomingSound;
				Sound.Play(notification);

				Actor distanceTestActor = null;
				for (var i = -info.SquadSize / 2; i <= info.SquadSize / 2; i++)
				{
					// Even-sized squads skip the lead plane
					if (i == 0 && (info.SquadSize & 1) == 0)
						continue;

					// Includes the 90 degree rotation between body and world coordinates
					var so = info.SquadOffset;
					var spawnOffset = new WVec(i * so.Y, -Math.Abs(i) * so.X, 0).Rotate(attackRotation);
					var targetOffset = new WVec(i * so.Y, 0, 0).Rotate(attackRotation);

					var a = w.CreateActor(info.UnitType, new TypeDictionary
					{
						new CenterPositionInit(startEdge + spawnOffset),
						new OwnerInit(self.Owner),
						new FacingInit(attackFacing),
					});

					var attack = a.Trait<AttackBomber>();
					attack.SetTarget(target + targetOffset);
					attack.OnEnteredAttackRange += onEnterRange;
					attack.OnExitedAttackRange += onExitRange;
					attack.OnRemovedFromWorld += onExitRange;

					a.QueueActivity(new Fly(a, Target.FromPos(finishEdge + spawnOffset)));
					a.QueueActivity(new RemoveSelf());
					aircraftInRange.Add(a, false);
					distanceTestActor = a;
				}

				if (Info.DisplayBeacon)
				{
					var distance = (target - startEdge).HorizontalLength;

					beacon = new Beacon(
						order.Player,
						order.TargetLocation.CenterPosition,
						Info.BeaconPalettePrefix,
						Info.BeaconPoster,
						Info.BeaconPosterPalette,
						() => 1 - ((distanceTestActor.CenterPosition - target).HorizontalLength - info.BeaconDistanceOffset.Range) * 1f / distance
					);

					w.Add(beacon);
				}
			});
		}
	}
}
