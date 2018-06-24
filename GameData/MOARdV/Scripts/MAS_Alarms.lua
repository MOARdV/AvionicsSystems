-- MAS_Alarms.lua
--
-- MOARdV's Avionics Systems
-- Lua scripts for various alarms and warnings
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

function MAS_GearDownCaution()

	if fc.GearHasActions() > 0 then
	
		-- Gear up during descent
		if (fc.AltitudeBottom() < 500) and (fc.VerticalSpeed() < 1) and (fc.GetGear() < 1) then
			return 1
		end

		-- Gear down during ascent or high speed
		if (fc.GetGear() > 0) then
			if (fc.AltitudeBottom() > 500) and (fc.VerticalSpeed() > 1) then
				return 1
			elseif (fc.DynamicPressure() > 0.01) and (fc.SurfaceSpeed() > fc.TerminalVelocity()) then
				return 1
			end
		end
	end
	
	return 0
end
