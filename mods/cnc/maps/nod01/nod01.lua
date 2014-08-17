RifleInfantryReinforcements = { "e1", "e1" }
RocketInfantryReinforcements = { "e3", "e3", "e3" }

SendFirstInfantryReinforcements = function()
	Media.PlaySpeechNotification(nod, "Reinforce")
	Reinforcements.Reinforce(nod, RifleInfantryReinforcements, { StartSpawnPointRight.Location, StartRallyPoint.Location }, 15)
end

SendSecondInfantryReinforcements = function()
	Media.PlaySpeechNotification(nod, "Reinforce")
	Reinforcements.Reinforce(nod, RifleInfantryReinforcements, { StartSpawnPointLeft.Location, StartRallyPoint.Location }, 15)
end

SendLastInfantryReinforcements = function()
	Media.PlaySpeechNotification(nod, "Reinforce")
	Reinforcements.Reinforce(nod, RocketInfantryReinforcements, { VillageSpawnPoint.Location, VillageRallyPoint.Location }, 15)
end

WorldLoaded = function()
	nod = Player.GetPlayer("Nod")
	gdi = Player.GetPlayer("GDI")
	villagers = Player.GetPlayer("Villagers")

	NodObjective1 = nod.AddPrimaryObjective("Kill Nikoomba")
	NodObjective2 = nod.AddPrimaryObjective("Destroy the village")
	NodObjective3 = nod.AddSecondaryObjective("Destroy all GDI troops in the area")
	GDIObjective1 = gdi.AddPrimaryObjective("Eliminate all Nod forces")

	Trigger.OnObjectiveCompleted(nod, function() Media.DisplayMessage("Objective completed") end)
	Trigger.OnObjectiveFailed(nod, function() Media.DisplayMessage("Objective failed") end)

	Trigger.OnPlayerWon(nod, function()
		Media.PlaySpeechNotification(nod, "Win")
	end)

	Trigger.OnPlayerLost(nod, function()
		Media.PlaySpeechNotification(nod, "Lose")
		Trigger.AfterDelay(Utils.Seconds(1), function()
			Media.PlayMovieFullscreen("nodlose.vqa")
		end)
	end)

	Trigger.OnKilled(Nikoomba, function()
		nod.MarkCompletedObjective(NodObjective1)
		Trigger.AfterDelay(Utils.Seconds(1), function()
			SendLastInfantryReinforcements()
		end)
	end)

	Trigger.AfterDelay(Utils.Seconds(30), SendFirstInfantryReinforcements)
	Trigger.AfterDelay(Utils.Seconds(60), SendSecondInfantryReinforcements)

	Media.PlayMovieFullscreen("nod1pre.vqa", function() Media.PlayMovieFullscreen("nod1.vqa") end)
end

Tick = function()
	if nod.HasNoRequiredUnits() then
		gdi.MarkCompletedObjective(GDIObjective1)
	end
	if villagers.HasNoRequiredUnits() then
		nod.MarkCompletedObjective(NodObjective2)
	end
	if gdi.HasNoRequiredUnits() then
		nod.MarkCompletedObjective(NodObjective3)
	end
end
