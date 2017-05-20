-- AltitudeDisplay.lua
--
-- MOARdV's Avionics Systems
-- Lua functions for the Retro Altitude display
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

function RAD_Altitude(mode)

	if mode > 0 then
		return 0.01 * fc.AltitudeTerrain(false)
	else
		return 0.01 * fc.Altitude()
	end
end
