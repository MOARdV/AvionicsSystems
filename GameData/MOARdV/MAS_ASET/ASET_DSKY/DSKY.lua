-- DSKY.lua
--
-- MOARdV's Avionics Systems
-- Lua functions for the DSKY
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

-- Returns 1 if the selected combination of sourceMode and deltaVMode are invalid.
function dksyError(sourceMode, deltaVMode)

	if sourceMode == 0 then
		-- Orbit
		-- CUSTOM_DSKY_ERR_NOORBIT
		if fc.VesselLanded() > 0 then
			return 1
		end
	elseif sourceMode == 1 or sourceMode == 2 then
		-- CUSTOM_DSKY_ERR_NOTGT
		-- Target (mode 1)
		-- Approach / Rendezvous (mode 2)
		if fc.TargetType() == 0 then
			return 1
		end
	elseif sourceMode == 3 or deltaVMode == 2 then
		-- CUSTOM_DSKY_ERR_NOMNVR
		-- CUSTOM_DSKY_ERR_NOMNVRDELTAV
		-- Maneuver
		if fc.VesselLanded() > 0 or fc.ManeuverNodeExists() < 1 then
			return 1
		end
	else
		-- Invalid
		return 1
	end

	return 0
end

-- Returns 1 if the DSKY should be in Maneuver/Rendezvous mode, 2 if it is in Rendezvous mode,
-- 0 otherwise.
function dskyModeSelect(selectedMode)

	if selectedMode == 2 and fc.VesselLanded() < 1 and fc.TargetType() > 0 then
		if fc.ManeuverNodeExists() > 0 then
			return 1
		else
			return 2
		end
	end

	return 0
end

-- Various DSKY mode qualifiers

-- MNVR
function dskyModeManeuver(sourceMode)

	if sourceMode == 3 and fc.VesselLanded() < 1 and fc.ManeuverNodeExists() > 0 then
		return 1
	end
	
	return 0
end

-- MNVR/RNDZ
function dskyModeManeuverRendezvous(sourceMode)

	if sourceMode == 2 and fc.TargetType() > 0 and fc.VesselLanded() < 1 and fc.ManeuverNodeExists() > 0 then
		return 1
	end
	
	return 0
end

-- ORBIT
function dskyModeOrbit(sourceMode)

	if sourceMode == 0 and fc.VesselLanded() < 1 then
		return 1
	end

	return 0
end

-- RENDEZVOUS
function dskyModeRendezvous(sourceMode)

	if sourceMode == 2 and fc.TargetType() > 0 and fc.VesselLanded() < 1 and fc.ManeuverNodeExists() < 1 then
		return 1
	end

	return 0
end

-- TARGET
function dskyModeTarget(sourceMode)

	if sourceMode == 1 and fc.TargetType() > 0 then
		return 1
	end

	return 0
end

-- Is the current source mode valid?
function dskyOrbitValid(sourceMode)
	if dskyModeOrbit(sourceMode) > 0 then
		return 1
	elseif dskyModeTarget(sourceMode) > 0 then
		return 1
	elseif dskyModeManeuver(sourceMode) > 0 then
		return 1
	end
	
	return 0
end

-- Active Ap
function dskyApValue(sourceMode)
	if dskyModeOrbit(sourceMode) > 0 then
		return fc.Apoapsis()
	elseif dskyModeTarget(sourceMode) > 0 then
		return fc.TargetApoapsis()
	elseif dskyModeManeuver(sourceMode) > 0 then
		return fc.ManeuverNodeAp()
	end
	
	return 0
end

-- Active Pe
function dskyPeValue(sourceMode)
	if dskyModeOrbit(sourceMode) > 0 then
		return fc.Periapsis()
	elseif dskyModeTarget(sourceMode) > 0 then
		return fc.TargetPeriapsis()
	elseif dskyModeManeuver(sourceMode) > 0 then
		return fc.ManeuverNodePe()
	end
	
	return 0
end

-- Active Inc
function dskyIncValue(sourceMode)
	if dskyModeOrbit(sourceMode) > 0 then
		return fc.Inclination()
	elseif dskyModeTarget(sourceMode) > 0 then
		return fc.TargetRelativeInclination()
	elseif dskyModeManeuver(sourceMode) > 0 then
		return fc.ManeuverNodeInc()
	end
	
	return 0
end

-- TIMER ---------------------------------------------------------------------

function dskyTimerModeLaunch(sourceMode, timerMode)
	return 0
end

function dskyTimerModeAp(sourceMode, timerMode)
	if timerMode == 0 and fc.VesselLanded() < 1 and dskyModeRendezvous(sourceMode) < 1 then
		return 1
	end

	return 0
end

function dskyTimerModePe(sourceMode, timerMode)
	if timerMode == 1 and fc.VesselLanded() < 1 and dskyModeRendezvous(sourceMode) < 1 then
		return 1
	end

	return 0
end

function dskyTimerModeAn(sourceMode, timerMode)
	if timerMode == 2 and fc.VesselLanded() < 1 then
		if sourceMode == 0 then
			return 1
		elseif sourceMode == 1 and fc.TargetType() > 0 then
			return 1
		end
	end
	
	return 0
end

function dskyTimerModeDn(sourceMode, timerMode)
	if timerMode == 3 and fc.VesselLanded() < 1 then
		if sourceMode == 0 then
			return 1
		elseif sourceMode == 1 and fc.TargetType() > 0 then
			return 1
		end
	end

	return 0
end

function dskyTimerModeMnvr(sourceMode, timerMode)

	if timerMode == 4 and fc.VesselLanded() < 1 and dskyModeRendezvous(sourceMode) < 1 and fc.ManeuverNodeExists() > 0 then
		return 1
	end

	return 0
end

-- Returns 1 when the timer data is valid; returns 0 otherwise.
function dskyTimerValid(sourceMode, timerMode)
	
	if dskyTimerModeLaunch(sourceMode, timerMode) > 0 then
		return 1
	elseif dskyTimerModeAp(sourceMode, timerMode) > 0 then
		return 1
	elseif dskyTimerModePe(sourceMode, timerMode) > 0 then
		return 1
	elseif dskyTimerModeAn(sourceMode, timerMode) > 0 then
		return 1
	elseif dskyTimerModeDn(sourceMode, timerMode) > 0 then
		return 1
	elseif dskyTimerModeMnvr(sourceMode, timerMode) > 0 then
		return 1
	end
	
	return 0
end

-- Retuns the time appropriate to the current mode combination.
function dskyTime(sourceMode, timerMode)
	
	if dskyTimerModeLaunch(sourceMode, timerMode) > 0 then
		return 0
	elseif dskyTimerModeAp(sourceMode, timerMode) > 0 then
		return fc.TimeToAp()
	elseif dskyTimerModePe(sourceMode, timerMode) > 0 then
		return fc.TimeToPe()
	elseif dskyTimerModeAn(sourceMode, timerMode) > 0 then
		if sourceMode == 0 then
			return fc.TimeToANEq()
		elseif sourceMode == 1 then
			return fc.TimeToANTarget()
		end
	elseif dskyTimerModeDn(sourceMode, timerMode) > 0 then
		if sourceMode == 0 then
			return fc.TimeToDNEq()
		elseif sourceMode == 1 then
			return fc.TimeToDNTarget()
		end
	elseif dskyTimerModeMnvr(sourceMode, timerMode) > 0 then
		return fc.ManeuverNodeTime()
	end
	
	return 0
end

-- DELTA-V -------------------------------------------------------------------

-- Returns a deltaV value appropriate to the current mode combination.
function dskyDeltaV(deltaVMode)

	if deltaVMode == 0 then
		return fc.DeltaVStage()
	elseif deltaVMode == 1 then
		return fc.DeltaV()
	elseif deltaVMode == 2 then
		return fc.ManeuverNodeDV()
	elseif deltaVMode == 3 then
		return fc.Abs(transfer.DeltaVInitial())
	end
	
	return 0
end
