FirstAttackWaveUnits  = { "e1", "e1", "e2" }
SecondAttackWaveUnits = { "e1", "e1", "e1" }
ThirdAttackWaveUnits = { "e1", "e1", "e1", "e2" }

SendAttackWave = function(units, action)
	Reinforcements.Reinforce(enemy, units, { GDIBarracksSpawn.Location, WP0.Location, WP1.Location }, 15, action)
end

FirstAttackWave = function(soldier)
	soldier.Move(WP2.Location)
	soldier.Move(WP3.Location)
	soldier.Move(WP4.Location)
	soldier.AttackMove(PlayerBase.Location)
end

SecondAttackWave = function(soldier)
	soldier.Move(WP5.Location)
	soldier.Move(WP6.Location)
	soldier.Move(WP7.Location)
	soldier.Move(WP9.Location)
	soldier.AttackMove(PlayerBase.Location)
end

WorldLoaded = function()
	player = Player.GetPlayer("Nod")
	enemy = Player.GetPlayer("GDI")

	gdiObjective = enemy.AddPrimaryObjective("Eliminate all Nod forces in the area")
	nodObjective1 = player.AddPrimaryObjective("Capture the prison")
	nodObjective2 = player.AddSecondaryObjective("Destroy all GDI forces")

	Trigger.OnObjectiveCompleted(player, function() Media.DisplayMessage("Objective completed") end)
	Trigger.OnObjectiveFailed(player, function() Media.DisplayMessage("Objective failed") end)

	Trigger.AfterDelay(Utils.Seconds(40), function() SendAttackWave(FirstAttackWaveUnits, FirstAttackWave) end)
	Trigger.AfterDelay(Utils.Seconds(80), function() SendAttackWave(SecondAttackWaveUnits, SecondAttackWave) end)
	Trigger.AfterDelay(Utils.Seconds(140), function() SendAttackWave(ThirdAttackWaveUnits, FirstAttackWave) end)

	Trigger.OnCapture(TechCenter, function() player.MarkCompletedObjective(nodObjective1) end)
	Trigger.OnKilled(TechCenter, function() player.MarkFailedObjective(nodObjective1) end)

	Trigger.OnPlayerWon(player, function()
		Trigger.AfterDelay(Utils.Seconds(2), function()
			Media.PlaySpeechNotification(player, "Win")
			Media.PlayMovieFullscreen("desflees.vqa")
		end)
	end)

	Trigger.OnPlayerLost(player, function()
		Trigger.AfterDelay(Utils.Seconds(1), function()
			Media.PlaySpeechNotification(player, "Lose")
			Media.PlayMovieFullscreen("flag.vqa")
		end)
	end)

	Media.PlayMovieFullscreen("nod3.vqa")
end

Tick = function()
	if player.HasNoRequiredUnits() then
		enemy.MarkCompletedObjective(gdiObjective)
	end

	if enemy.HasNoRequiredUnits() then
		player.MarkCompletedObjective(nodObjective2)
	end
end
