-- ARRT.lua
--
-- MOARdV's Avionics Systems
-- Lua functions for the Altitude / Range Rate indicator ARRT
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

local arrtInitialized = false

local arrtOn = false
local arrtRangeMode = false
local arrtEnable = false

-- Returns 1 if there is an error in the ARRT settings, otherwise returns
-- 0.  Also takes care of processing local variables each update.
-- The ARRT will not function if this method is not called.
function ARRT_Error()
	if arrtInitialized == false then
		arrtInitialized = true

		-- Externally controlled power switch persistent
		local switchOn = fc.GetPersistentAsNumber("MAS_ARRT_On")
		if switchOn > 0 then
			arrtOn = true
		end

		-- Externally controlled mode switch persistent
		local modeSetting = fc.GetPersistentAsNumber("MAS_ARRT_Mode")
		if modeSetting > 0 then
			arrtRangeMode = true
		end

		-- Internally controlled / externally read "Is this thing on?" persistent.
		local enableSetting = fc.GetPersistentAsNumber("MAS_ARRT_Enable")
		if enableSetting > 0 then
			arrtEnable = true
		end
	end

	local oldArrtEanble = arrtEnable

	if arrtOn == true then

		arrtEnable = true
		if arrtRangeMode == true then
			-- Range / Range-Rate mode requires a vessel / docking port
			if fc.TargetIsVessel() < 1 then
				arrtEnable = false
			end
		else
			-- Altitude / Altitude-Rate mode requires an altitude < 6500m.
			if fc.AltitudeTerrain(false) > 6500 then
				arrtEnable = false
			end
		end

	else
		arrtEnable = false
	end

	if oldArrtEanble ~= arrtEnable then
		if arrtEnable == false then
			fc.SetPersistent("MAS_ARRT_Enable", 0)
		else
			fc.SetPersistent("MAS_ARRT_Enable", 1)
		end
	end

	if arrtEnable == false and arrtOn == true then
		return 1
	else
		return 0
	end
end

-- Return the current range
function ARRT_Range()
	if arrtEnable == true then
		if arrtRangeMode == true then
			return fc.TargetDistance()
		else
			return fc.AltitudeTerrain(false)
		end
	else
		return 0
	end
end

-- Return the current range-rate
function ARRT_RangeRate()
	if arrtEnable == true then
		if arrtRangeMode == true then
			return fc.TargetSpeed()
		else
			return fc.VerticalSpeed()
		end
	else
		return 0
	end
end

-- Update the power setting
function ARRT_TogglePower()
	local switchOn = fc.TogglePersistent("MAS_ARRT_On")

	if switchOn > 0 then
		arrtOn = true
	else
		arrtOn = false
	end
end

-- Update the mode setting
function ARRT_ToggleMode()
	local modeSetting = fc.TogglePersistent("MAS_ARRT_Mode")

	if modeSetting > 0 then
		arrtRangeMode = true
	else
		arrtRangeMode = false
	end
end
