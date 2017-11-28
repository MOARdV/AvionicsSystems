-- MAS_ALCOR_MFD2.lua
--
-- MOARdV's Avionics Systems
-- Lua scripts for ALCOR Multi-Function Displays
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

function MAS_Mfd2_Init(propId)

	if fc.GetPersistentExists(propId .. "-Scalar") < 1 then
		fc.SetPersistent(propId .. "-Scalar", 1)
	end
end

function MAS_Mfd2_Prelaunch_Clear(propId)
	
	if fc.GetPersistentAsNumber(propId .. "-PrelaunchSelect") < 1 then
		mechjeb.SetDesiredLaunchAltitude(0)
	else
		mechjeb.SetDesiredLaunchInclination(0)
	end
	
end

function MAS_Mfd2_Prelaunch_Plus(propId)
	
	if fc.GetPersistentAsNumber(propId .. "-PrelaunchSelect") < 1 then
		mechjeb.SetDesiredLaunchAltitude(mechjeb.GetDesiredLaunchAltitude() + 1000 * fc.GetPersistentAsNumber(propId .. "-Scalar"))
	else
		mechjeb.SetDesiredLaunchInclination(fc.Clamp(mechjeb.GetDesiredLaunchInclination() + fc.GetPersistentAsNumber(propId .. "-Scalar"), -180, 180))
	end
	
end

function MAS_Mfd2_Prelaunch_Minus(propId)

	if fc.GetPersistentAsNumber(propId .. "-PrelaunchSelect") < 1 then
		mechjeb.SetDesiredLaunchAltitude(fc.Max(mechjeb.GetDesiredLaunchAltitude() - 1000 * fc.GetPersistentAsNumber(propId .. "-Scalar"), 0))
	else
		mechjeb.SetDesiredLaunchInclination(fc.Clamp(mechjeb.GetDesiredLaunchInclination() - fc.GetPersistentAsNumber(propId .. "-Scalar"), -180, 180))
	end

end
