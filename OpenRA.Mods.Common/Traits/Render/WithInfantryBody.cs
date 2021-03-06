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
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public class WithInfantryBodyInfo : UpgradableTraitInfo, IRenderActorPreviewSpritesInfo, Requires<IMoveInfo>, Requires<RenderSpritesInfo>
	{
		public readonly int MinIdleWaitTicks = 30;
		public readonly int MaxIdleWaitTicks = 110;

		[SequenceReference] public readonly string MoveSequence = "run";
		[SequenceReference] public readonly string AttackSequence = null;
		[SequenceReference] public readonly string[] IdleSequences = { };
		[SequenceReference] public readonly string[] StandSequences = { "stand" };

		public override object Create(ActorInitializer init) { return new WithInfantryBody(init, this); }

		public IEnumerable<IActorPreview> RenderPreviewSprites(ActorPreviewInitializer init, RenderSpritesInfo rs, string image, int facings, PaletteReference p)
		{
			var facing = 0;
			var ifacing = init.Actor.TraitInfoOrDefault<IFacingInfo>();
			if (ifacing != null)
				facing = init.Contains<FacingInit>() ? init.Get<FacingInit, int>() : ifacing.GetInitialFacing();

			var anim = new Animation(init.World, image, () => facing);
			anim.PlayRepeating(RenderSprites.NormalizeSequence(anim, init.GetDamageState(), StandSequences.First()));
			yield return new SpriteActorPreview(anim, WVec.Zero, 0, p, rs.Scale);
		}
	}

	public class WithInfantryBody : UpgradableTrait<WithInfantryBodyInfo>, ITick, INotifyAttack, INotifyIdle, INotifyCreated
	{
		readonly IMove move;
		protected readonly Animation DefaultAnimation;

		bool dirty;
		string idleSequence;
		int idleDelay;
		AnimationState state;
		IRenderInfantrySequenceModifier rsm;

		bool IsModifyingSequence { get { return rsm != null && rsm.IsModifyingSequence; } }
		bool wasModifying;

		public WithInfantryBody(ActorInitializer init, WithInfantryBodyInfo info)
			: base(info)
		{
			var self = init.Self;
			var rs = self.Trait<RenderSprites>();

			DefaultAnimation = new Animation(init.World, rs.GetImage(self), RenderSprites.MakeFacingFunc(self));
			rs.Add(new AnimationWithOffset(DefaultAnimation, null, () => IsTraitDisabled));
			PlayStandAnimation(self);

			state = AnimationState.Waiting;
			move = init.Self.Trait<IMove>();
		}

		public void PlayStandAnimation(Actor self)
		{
			var sequence = DefaultAnimation.GetRandomExistingSequence(Info.StandSequences, Game.CosmeticRandom);
			if (sequence != null)
				DefaultAnimation.PlayFetchIndex(NormalizeInfantrySequence(self, sequence), () => 0);
		}

		public void Created(Actor self)
		{
			rsm = self.TraitOrDefault<IRenderInfantrySequenceModifier>();
		}

		protected virtual string NormalizeInfantrySequence(Actor self, string baseSequence)
		{
			var prefix = IsModifyingSequence ? rsm.SequencePrefix : "";

			if (DefaultAnimation.HasSequence(prefix + baseSequence))
				return prefix + baseSequence;
			else
				return baseSequence;
		}

		protected virtual bool AllowIdleAnimation(Actor self)
		{
			return !IsModifyingSequence;
		}

		public void Attacking(Actor self, Target target)
		{
			if (!string.IsNullOrEmpty(Info.AttackSequence) && DefaultAnimation.HasSequence(NormalizeInfantrySequence(self, Info.AttackSequence)))
			{
				state = AnimationState.Attacking;
				DefaultAnimation.PlayThen(NormalizeInfantrySequence(self, Info.AttackSequence), () => state = AnimationState.Idle);
			}
		}

		public void Attacking(Actor self, Target target, Armament a, Barrel barrel)
		{
			Attacking(self, target);
		}

		public virtual void Tick(Actor self)
		{
			if (rsm != null)
			{
				if (wasModifying != rsm.IsModifyingSequence)
					dirty = true;

				wasModifying = rsm.IsModifyingSequence;
			}

			if ((state != AnimationState.Moving || dirty) && move.IsMoving)
			{
				state = AnimationState.Moving;
				DefaultAnimation.PlayRepeating(NormalizeInfantrySequence(self, Info.MoveSequence));
			}
			else if (((state == AnimationState.Moving || dirty) && !move.IsMoving)
				|| ((state == AnimationState.Idle || state == AnimationState.IdleAnimating) && !self.IsIdle))
			{
				state = AnimationState.Waiting;
				PlayStandAnimation(self);
			}

			dirty = false;
		}

		public void TickIdle(Actor self)
		{
			if (state != AnimationState.Idle && state != AnimationState.IdleAnimating && state != AnimationState.Attacking)
			{
				PlayStandAnimation(self);
				state = AnimationState.Idle;

				if (Info.IdleSequences.Length > 0)
				{
					idleSequence = Info.IdleSequences.Random(self.World.SharedRandom);
					idleDelay = self.World.SharedRandom.Next(Info.MinIdleWaitTicks, Info.MaxIdleWaitTicks);
				}
			}
			else if (AllowIdleAnimation(self))
			{
				if (idleSequence != null && DefaultAnimation.HasSequence(idleSequence))
				{
					if (idleDelay > 0 && --idleDelay == 0)
					{
						state = AnimationState.IdleAnimating;
						DefaultAnimation.PlayThen(idleSequence, () =>
						{
							PlayStandAnimation(self);
							state = AnimationState.Waiting;
						});
					}
				}
				else
				{
					PlayStandAnimation(self);
					state = AnimationState.Waiting;
				}
			}
		}

		enum AnimationState
		{
			Idle,
			Attacking,
			Moving,
			Waiting,
			IdleAnimating
		}
	}
}
