-- ARRT.lua
--
-- MOARdV's Avionics Systems
-- Lua function for the Altitude / Range Rate indicator ARRT
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

-- Function to validate the current mode of the ARRT.
function ARRT_Validate(arrtEnable, arrtMode)

	local oldEnable = (arrtEnable > 0)
	local newEnable
	
	if arrtMode == 0 and fc.AltitudeBottom() <= 5000 then
		newEnable = true
	elseif arrtMode == 1 and fc.TargetType() > 0 then
		newEnable = true
	else
		newEnable = false
	end
	
	if oldEnable ~= newEnable then
		if newEnable == true then
			fc.SetPersistent("MAS_ARRT_Enable", 1)
		else
			fc.SetPersistent("MAS_ARRT_Enable", 0)
		end
	end
end
