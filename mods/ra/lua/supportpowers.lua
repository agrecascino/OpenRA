SupportPowers = { }

SupportPowers.Parabomb = function(owner, planeName, enterLocation, bombLocation)
	local facing = { Map.GetFacing(CPos.op_Subtraction(bombLocation, enterLocation), 0), "Int32" }
	local altitude = { Actor.TraitInfo(planeName, "AircraftInfo").CruiseAltitude, "Int32" }
	local plane = Actor.Create(planeName, { Location = enterLocation, Owner = owner, Facing = facing, Altitude = altitude })
	Actor.Trait(plane, "AttackBomber"):SetTarget(bombLocation.CenterPosition)
	Actor.Fly(plane, bombLocation.CenterPosition)
	Actor.FlyOffMap(plane)
	Actor.RemoveSelf(plane)
end

SupportPowers.Paradrop = function(owner, planeName, passengerNames, enterLocation, dropLocation)
	local facing = { Map.GetFacing(CPos.op_Subtraction(dropLocation, enterLocation), 0), "Int32" }
	local altitude = { Actor.TraitInfo(planeName, "AircraftInfo").CruiseAltitude, "Int32" }
	local plane = Actor.Create(planeName, { Location = enterLocation, Owner = owner, Facing = facing, Altitude = altitude })
	Actor.FlyAttackCell(plane, dropLocation)
	Actor.Trait(plane, "ParaDrop"):SetLZ(dropLocation)
	local cargo = Actor.Trait(plane, "Cargo")
	for i, passengerName in ipairs(passengerNames) do
		cargo:Load(plane, Actor.Create(passengerName, { AddToWorld = false, Owner = owner }))
	end
end