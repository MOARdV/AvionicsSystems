-- Warnings.lua
--
-- MOARdV's Avionics Systems
-- Lua scripts for ASET Retro Warning Lights
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

function MAS_RWL_MasterCaution()

	-- Core resources: propellant, power, RCS, ablator
	--fc.PropellantStageThreshold(0.0, 0.1) + fc.PowerThreshold(0.0, 0.1) + fc.RcsStageThreshold(0.0, 0.1) + fc.ResourceThreshold("Ablator", 0.0, 0.1)
	if (fc.PropellantStageThreshold(0.0, 0.1) > 0) or (fc.PowerThreshold(0.0, 0.1) > 0) or (fc.RcsStageThreshold(0.0, 0.1) > 0) or (fc.ResourceThreshold("Ablator", 0.0, 0.1) > 0) then
		return 1
	end

	-- Descending, with a parachute, in the atmosphere, and we're in flight
	-- ((1 - parachute.GetParachuteArmedOrDeployed()) * fc.AtmosphereDepth() * (fc.VerticalSpeed() < 0) * fc.VesselFlying())
	if (parachute.ParachuteCount() > 0) and (fc.AtmosphereDepth() > 0) and (parachute.GetParachuteArmedOrDeployed() < 0) and (fc.MaxImpactSpeed() + fc.VerticalSpeed() > 0) then
		return 1
	end

	-- No cautions
	return 0
end

function MAS_RWL_MasterAlarm()

	-- flameout
	if (fc.EngineFlameout() > 0) then
		return 1
	end

	-- Damaged components
	if (fc.SolarPanelDamaged() > 0) then
		return 1
	end

	if (fc.AntennaDamaged() > 0) then
		return 1
	end

	-- No alarms
	return 0
end
