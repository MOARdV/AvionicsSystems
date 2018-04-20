-- ASET_IMP.lua
--
-- MOARdV's Avionics Systems
-- Lua functions for the ASET "Globus" IMP.
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

local IMP_mode = 0
local IMP_active = true

function IMP_Init()
	local mode = fc.GetPersistentAsNumber("MAS_IMP_Mode_Select")
	
	if mode < 1 or mode > 3 then
		fc.SetPersistent("MAS_IMP_Mode_Select", 1)
	end
end

-- Return the latitude for the given mode
function IMP_Latitude()
	if IMP_active == true then
		local latitude = 0

		-- Vessel
		if IMP_mode == 1 then
			latitude = fc.Latitude()
		-- Target
		elseif IMP_mode == 2 then
			latitude = fc.TargetLatitude()
		-- Landing
		else
			latitude = fc.LandingLatitude()
		end

		return fc.Conditioned(latitude)
	else
		return 0
	end
end

-- Return the longitude for the given mode
function IMP_Longitude()
	if IMP_active == true then
		local longitude = 0

		-- Vessel
		if IMP_mode == 1 then
			longitude = fc.Longitude()
		-- Target
		elseif IMP_mode == 2 then
			longitude = fc.TargetLongitude()
		-- Landing
		else
			longitude = fc.LandingLongitude()
		end

		return fc.Conditioned(longitude)
	else
		return 0
	end
end

-- Return the backlight setting.  Also manages updating mode settings for the
-- latitude / longitude processing.
function IMP_Backlight()
	IMP_mode = fc.GetPersistentAsNumber("MAS_IMP_Mode")

	IMP_active = false

	if IMP_mode == 1 then
		IMP_active = true
	elseif IMP_mode == 2 then
		IMP_active = (fc.TargetLatLonValid() * fc.TargetSameSoI() > 0)
	elseif IMP_mode == 3 then
		IMP_active = (fc.LandingPredictorActive() > 0)
	end

	if IMP_active == true then
		return fc.Conditioned(fc.GetPersistentAsNumber("Backlight"))
	else
		return 0
	end
end

-- Returns 1 if the IMP is enabled but its current mode is invalid.
function IMP_Error()
	if IMP_active == false and IMP_mode > 0 then
		-- We use fc.Conditioned so the flag will flicker with power disruptions
		return fc.Conditioned(1)
	else
		return 0
	end
end
