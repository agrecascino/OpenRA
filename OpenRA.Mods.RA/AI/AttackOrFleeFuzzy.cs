﻿#region Copyright & License Information
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
using AI.Fuzzy.Library;
using OpenRA.Mods.RA.Move;
using OpenRA.Traits;

namespace OpenRA.Mods.RA.AI
{
	class AttackOrFleeFuzzy
	{
		MamdaniFuzzySystem fuzzyEngine;
		Dictionary<FuzzyVariable, double> result;

		public AttackOrFleeFuzzy()
		{
			InitializateFuzzyVariables();
		}

		public bool CanAttack
		{
			get
			{
				var attackChance = result[fuzzyEngine.OutputByName("AttackOrFlee")];
				return !double.IsNaN(attackChance) && attackChance < 30.0;
			}
		}

		protected void AddFuzzyRule(string rule)
		{
			fuzzyEngine.Rules.Add(fuzzyEngine.ParseRule(rule));
		}

		protected void InitializateFuzzyVariables()
		{
			fuzzyEngine = new MamdaniFuzzySystem();

			var playerHealthFuzzy = new FuzzyVariable("OwnHealth", 0.0, 100.0);
			playerHealthFuzzy.Terms.Add(new FuzzyTerm("NearDead", new TrapezoidMembershipFunction(0, 0, 20, 40)));
			playerHealthFuzzy.Terms.Add(new FuzzyTerm("Injured", new TrapezoidMembershipFunction(30, 50, 50, 70)));
			playerHealthFuzzy.Terms.Add(new FuzzyTerm("Normal", new TrapezoidMembershipFunction(50, 80, 100, 100)));
			fuzzyEngine.Input.Add(playerHealthFuzzy);

			var enemyHealthFuzzy = new FuzzyVariable("EnemyHealth", 0.0, 100.0);
			enemyHealthFuzzy.Terms.Add(new FuzzyTerm("NearDead", new TrapezoidMembershipFunction(0, 0, 20, 40)));
			enemyHealthFuzzy.Terms.Add(new FuzzyTerm("Injured", new TrapezoidMembershipFunction(30, 50, 50, 70)));
			enemyHealthFuzzy.Terms.Add(new FuzzyTerm("Normal", new TrapezoidMembershipFunction(50, 80, 100, 100)));
			fuzzyEngine.Input.Add(enemyHealthFuzzy);

			var relativeAttackPowerFuzzy = new FuzzyVariable("RelativeAttackPower", 0.0, 1000.0);
			relativeAttackPowerFuzzy.Terms.Add(new FuzzyTerm("Weak", new TrapezoidMembershipFunction(0, 0, 70, 90)));
			relativeAttackPowerFuzzy.Terms.Add(new FuzzyTerm("Equal", new TrapezoidMembershipFunction(85, 100, 100, 115)));
			relativeAttackPowerFuzzy.Terms.Add(new FuzzyTerm("Strong", new TrapezoidMembershipFunction(110, 150, 150, 1000)));
			fuzzyEngine.Input.Add(relativeAttackPowerFuzzy);

			var relativeSpeedFuzzy = new FuzzyVariable("RelativeSpeed", 0.0, 1000.0);
			relativeSpeedFuzzy.Terms.Add(new FuzzyTerm("Slow", new TrapezoidMembershipFunction(0, 0, 70, 90)));
			relativeSpeedFuzzy.Terms.Add(new FuzzyTerm("Equal", new TrapezoidMembershipFunction(85, 100, 100, 115)));
			relativeSpeedFuzzy.Terms.Add(new FuzzyTerm("Fast", new TrapezoidMembershipFunction(110, 150, 150, 1000)));
			fuzzyEngine.Input.Add(relativeSpeedFuzzy);

			var attackOrFleeFuzzy = new FuzzyVariable("AttackOrFlee", 0.0, 50.0);
			attackOrFleeFuzzy.Terms.Add(new FuzzyTerm("Attack", new TrapezoidMembershipFunction(0, 15, 15, 30)));
			attackOrFleeFuzzy.Terms.Add(new FuzzyTerm("Flee", new TrapezoidMembershipFunction(25, 35, 35, 50)));
			fuzzyEngine.Output.Add(attackOrFleeFuzzy);

			AddingRulesForNormalOwnHealth();
			AddingRulesForInjuredOwnHealth();
			AddingRulesForNearDeadOwnHealth();
		}

		protected virtual void AddingRulesForNormalOwnHealth()
		{
			AddFuzzyRule("if ((OwnHealth is Normal) " +
				"and ((EnemyHealth is NearDead) or (EnemyHealth is Injured) or (EnemyHealth is Normal)) " +
				"and ((RelativeAttackPower is Weak) or (RelativeAttackPower is Equal) or (RelativeAttackPower is Strong)) " +
				"and ((RelativeSpeed is Slow) or (RelativeSpeed is Equal) or (RelativeSpeed is Fast))) " +
				"then AttackOrFlee is Attack");   
		}

		protected virtual void AddingRulesForInjuredOwnHealth()
		{
			AddFuzzyRule("if ((OwnHealth is Injured) " +
				"and (EnemyHealth is NearDead) " +
				"and ((RelativeAttackPower is Weak) or (RelativeAttackPower is Equal) or (RelativeAttackPower is Strong)) " +
				"and ((RelativeSpeed is Slow) or (RelativeSpeed is Equal) or (RelativeSpeed is Fast))) " +
				"then AttackOrFlee is Attack");

			AddFuzzyRule("if ((OwnHealth is Injured) " +
				"and ((EnemyHealth is Injured) or (EnemyHealth is Normal)) " +
				"and ((RelativeAttackPower is Equal) or (RelativeAttackPower is Strong)) " +
				"and ((RelativeSpeed is Slow) or (RelativeSpeed is Equal) or (RelativeSpeed is Fast))) " +
				"then AttackOrFlee is Attack");

			AddFuzzyRule("if ((OwnHealth is Injured) " +
				"and ((EnemyHealth is Injured) or (EnemyHealth is Normal)) " +
				"and (RelativeAttackPower is Weak) " +
				"and (RelativeSpeed is Slow)) " +
				"then AttackOrFlee is Attack");

			AddFuzzyRule("if ((OwnHealth is Injured) " +
				"and ((EnemyHealth is Injured) or (EnemyHealth is Normal)) " +
				"and (RelativeAttackPower is Weak) " +
				"and ((RelativeSpeed is Equal) or (RelativeSpeed is Fast))) " +
				"then AttackOrFlee is Flee");

			AddFuzzyRule("if ((OwnHealth is Injured) " +
				"and ((EnemyHealth is NearDead) or (EnemyHealth is Injured) or (EnemyHealth is Normal)) " +
				"and ((RelativeAttackPower is Weak) or (RelativeAttackPower is Equal) or (RelativeAttackPower is Strong)) " +
				"and (RelativeSpeed is Slow)) " +
				"then AttackOrFlee is Attack");
		}

		protected virtual void AddingRulesForNearDeadOwnHealth()
		{
			AddFuzzyRule("if ((OwnHealth is NearDead) " +
				"and (EnemyHealth is Injured) " +
				"and (RelativeAttackPower is Equal) " +
				"and ((RelativeSpeed is Slow) or (RelativeSpeed is Equal))) " +
				"then AttackOrFlee is Attack");

			AddFuzzyRule("if ((OwnHealth is NearDead) " +
				"and (EnemyHealth is NearDead) " +
				"and (RelativeAttackPower is Weak) " +
				"and ((RelativeSpeed is Equal) or (RelativeSpeed is Fast))) " +
				"then AttackOrFlee is Flee");

			AddFuzzyRule("if ((OwnHealth is NearDead) " +
				"and (EnemyHealth is Injured) " +
				"and (RelativeAttackPower is Weak) " +
				"and ((RelativeSpeed is Equal) or (RelativeSpeed is Fast))) " +
				"then AttackOrFlee is Flee");

			AddFuzzyRule("if ((OwnHealth is NearDead) " +
				"and (EnemyHealth is Normal) " +
				"and (RelativeAttackPower is Weak) " +
				"and ((RelativeSpeed is Equal) or (RelativeSpeed is Fast))) " +
				"then AttackOrFlee is Flee");

			AddFuzzyRule("if (OwnHealth is NearDead) " +
				"and (EnemyHealth is Normal) " +
				"and (RelativeAttackPower is Equal) " +
				"and (RelativeSpeed is Fast) " +
				"then AttackOrFlee is Flee");

			AddFuzzyRule("if (OwnHealth is NearDead) " +
				"and (EnemyHealth is Normal) " +
				"and (RelativeAttackPower is Strong) " +
				"and (RelativeSpeed is Fast) " +
				"then AttackOrFlee is Flee");

			AddFuzzyRule("if (OwnHealth is NearDead) " +
				"and (EnemyHealth is Injured) " +
				"and (RelativeAttackPower is Equal) " +
				"and (RelativeSpeed is Fast) " +
				"then AttackOrFlee is Flee");
		}

		public void CalculateFuzzy(List<Actor> ownUnits, List<Actor> enemyUnits)
		{
			var inputValues = new Dictionary<FuzzyVariable, double>();
			inputValues.Add(fuzzyEngine.InputByName("OwnHealth"), (double)NormalizedHealth(ownUnits, 100));
			inputValues.Add(fuzzyEngine.InputByName("EnemyHealth"), (double)NormalizedHealth(enemyUnits, 100));
			inputValues.Add(fuzzyEngine.InputByName("RelativeAttackPower"), (double)RelativePower(ownUnits, enemyUnits));
			inputValues.Add(fuzzyEngine.InputByName("RelativeSpeed"), (double)RelativeSpeed(ownUnits, enemyUnits));

			result = fuzzyEngine.Calculate(inputValues);
		}

		protected float NormalizedHealth(List<Actor> actors, float normalizeByValue)
		{
			var sumOfMaxHp = 0;
			var sumOfHp = 0;
			foreach (var a in actors)
			{
				if (a.HasTrait<Health>())
				{
					sumOfMaxHp += a.Trait<Health>().MaxHP;
					sumOfHp += a.Trait<Health>().HP;
				}
			}

			if (sumOfMaxHp == 0)
				return 0.0f;

			return (sumOfHp * normalizeByValue) / sumOfMaxHp;
		}

		protected float RelativePower(List<Actor> own, List<Actor> enemy)
		{
			return RelativeValue(own, enemy, 100, SumOfValues<AttackBase>, (Actor a) =>
			{
				int sumOfDamage = 0;
				var arms = a.TraitsImplementing<Armament>();
				foreach (var arm in arms)
					if (arm.Weapon.Warheads[0] != null)
						sumOfDamage += arm.Weapon.Warheads[0].Damage;
				return sumOfDamage;
			});
		}

		protected float RelativeSpeed(List<Actor> own, List<Actor> enemy)
		{
			return RelativeValue(own, enemy, 100, Average<Mobile>, (Actor a) => a.Trait<Mobile>().Info.Speed);
		}

		protected float RelativeValue(List<Actor> own, List<Actor> enemy, float normalizeByValue, 
					Func<List<Actor>, Func<Actor, int>, float> relativeFunc, Func<Actor, int> getValue)
		{
			if (enemy.Count == 0)
				return 999.0f;

			if (own.Count == 0)
				return 0.0f;

			var relative = (relativeFunc(own, getValue) / relativeFunc(enemy, getValue)) * normalizeByValue;
			return relative.Clamp(0.0f, 999.0f);
		}

		protected float SumOfValues<Trait>(List<Actor> actors, Func<Actor, int> getValue)
		{
			var sum = 0;
			foreach (var a in actors)
				if (a.HasTrait<Trait>())
					sum += getValue(a);

			return sum;
		}

		protected float Average<Trait>(List<Actor> actors, Func<Actor, int> getValue)
		{
			var sum = 0;
			var countActors = 0;
			foreach (var a in actors)
			{
				if (a.HasTrait<Trait>())
				{
					sum += getValue(a);
					countActors++;
				}
			}

			if (countActors == 0)
				return 0.0f;

			return sum / countActors;
		}
	}
}
