-- ASET_IMP.lua
--
-- MOARdV's Avionics Systems
-- Lua functions for the ASET "Globus" IMP.
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

-- Validate that the current IMP mode should return results.
function IMP_Validate(impEnable, impMode)
	local oldEnable = (impEnable > 0)
	local newEnable
	
	if impMode == 0 then
		newEnable = true
	elseif impMode == -1 and fc.TargetLatLonValid() > 0 and fc.TargetSameSoI() > 0 then
		newEnable = true
	elseif impMode == 1 and (fc.LandingPredictorActive() > 0)then
		newEnable = true
	else
		newEnable = false
	end
	
	if newEnable ~= oldEnable then
		if newEnable == true then
			fc.SetPersistent("MAS_IMP_Enable", 1)
		else
			fc.SetPersistent("MAS_IMP_Enable", 0)
		end
	end
end

-- Return the latitude for the given mode
function IMP_Latitude(impMode)
	local latitude = 0

	if impMode == 0 then
		-- Vessel
		latitude = fc.Latitude()
	elseif impMode == -1 then
		-- Target
		latitude = fc.TargetLatitude()
	else
		-- Landing
		latitude = fc.LandingLatitude()
	end

	return latitude
end

-- Return the longitude for the given mode
function IMP_Longitude(impMode)
	local longitude

	if impMode == 0 then
		-- Vessel
		longitude = fc.Longitude()
	elseif impMode == -1 then
		-- Target
		longitude = fc.TargetLongitude()
	else
		-- Landing
		longitude = fc.LandingLongitude()
	end

	return longitude
end
