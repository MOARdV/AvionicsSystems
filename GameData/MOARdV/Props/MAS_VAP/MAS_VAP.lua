-- MAS_VAP.lua
--
-- MOARdV's Avionics Systems
-- Lua source for the MAS VAP (MAS Vessel Auto Pilot) systems
--
-- Author: MOARdV
--
-- This script is CC-BY-SA

function ToggleAscentAutopilot(apSelector, mjApActive, masApActive, launchAlt, launchInc)

	if mjApActive > 0 then
		mechjeb.ToggleAscentAutopilot()
	elseif apSelector > 0 then
		mechjeb.SetDesiredLaunchAltitude(launchAlt)
		mechjeb.SetDesiredLaunchInclination(launchInc)
		mechjeb.ToggleAscentAutopilot()
	end
	-- Currently, MAS AP doesn't exist.
	
	return 1
end

function ToggleManeuverAutopilot(apSelector, mjApActive, masApActive, nodeActive)
	
	if mjApActive > 0 then
		mechjeb.ToggleManeuverNodeExecutor()
	elseif masApActive > 0 then
		fc.ToggleManeuverPilot()
	elseif nodeActive > 0 then
		if apSelector > 0 then
			mechjeb.ToggleManeuverNodeExecutor()
		else
			fc.ToggleManeuverPilot()
		end
	end
	
	return 1
end

function ChangeAp(apSelector, newAp, currentPe)

	if newAp >= currentPe then
		if apSelector > 0 then
			mechjeb.ChangeApoapsis(newAp)
		else
			transfer.ChangeApoapsis(newAp)
		end
	end

	return 1
end

function ChangePe(apSelector, newPe, currentAp)

	if newPe <= currentAp then
		if apSelector > 0 then
			mechjeb.ChangePeriapsis(newPe)
		else
			transfer.ChangePeriapsis(newPe)
		end
	end

	return 1
end
