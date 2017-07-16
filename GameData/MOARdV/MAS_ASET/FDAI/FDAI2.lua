-- ASET_FDAI2.lua
--
-- MOARdV's Avionics Systems
-- Lua functions for FDAI2
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

-- We use a bunch of local variables here so we don't have to query
-- persistents for certain information.  It is much faster to keep
-- these values here, and not have to ask the flight computer for them
-- every time, since the only way we allow them to change is through the
-- functions in this module.
local fdai2ErrorScale = 1.2
--local fdai2RateScale = 1.0
local fdai2ErrorFlag = false
local fdai2OnFlag = true
local fdai2Power = false
local fdai2PortFlag = false
local fdai2SyncSAS = false
local fdai2SwitchNegative = false
local fdai2Initialized = false
local fdai2Mode = 2
-- fdaiSASMode translates the SAS mode + SAS Speed Mode into an equivalent
-- value for use in our lookup table
local fdai2SASMode = 0

-- Valid Modes:
--  1 = Surface Prograde
--  2 = Surface Retrograde
--  3 = Orbit Prograde
--  4 = Orbit Retrograde
--  5 = Radial Out
--  6 = Radial In
--  7 = Normal
--  8 = Anti-Normal
--  9 = Maneuver Node
-- 10 = Maneuver Node
-- 11 = Target +
-- 12 = Docking Port Alignment
-- 13 = Target Rel Prograde
-- 14 = Target Rel Retrograde
-- 15 = Target - (this mode is only available in Sync to SAS)

local pitchValue =
{
	fc.PitchSurfacePrograde,
	fc.PitchSurfaceRetrograde,
	fc.PitchPrograde,
	fc.PitchRetrograde,
	fc.PitchRadialOut,
	fc.PitchRadialIn,
	fc.PitchNormal,
	fc.PitchAntiNormal,
	fc.PitchManeuver,
	fc.PitchManeuver,
	fc.PitchTarget,
	fc.PitchDockingAlignment,
	fc.PitchTargetPrograde,
	fc.PitchTargetRetrograde,
	fc.PitchAntiTarget
}

-- Selects the pitch function from the table above and evaluates it, or returns
-- 0 if it's not a valid value.
--local function SelectPitch()
function FDAI2_PitchError()
	local func = nil

	if fdai2SyncSAS == true then
		func = pitchValue[fdai2SASMode]
	else
		func = pitchValue[fdai2Mode]
	end

	if func ~= nil then
		return func()
	else
		return 0
	end
end

local yawValue =
{
	fc.YawSurfacePrograde,
	fc.YawSurfaceRetrograde,
	fc.YawPrograde,
	fc.YawRetrograde,
	fc.YawRadialOut,
	fc.YawRadialIn,
	fc.YawNormal,
	fc.YawAntiNormal,
	fc.YawManeuver,
	fc.YawManeuver,
	fc.YawTarget,
	fc.YawDockingAlignment,
	fc.YawTargetPrograde,
	fc.YawTargetRetrograde,
	fc.YawAntiTarget
}

-- Selects the yaw function from the table above and evaluates it, or returns
-- 0 if it's not a valid value.
--local function SelectYaw()
function FDAI2_YawError()
	local func = nil

	if fdai2SyncSAS == true then
		func = yawValue[fdai2SASMode]
	else
		func = yawValue[fdai2Mode]
	end

	if func ~= nil then
		return func()
	else
		return 0
	end
end

-- Function to update the mode selector.  'local' because no one outside of
-- this script should be calling it.
local function FDAI2_UpdateModes(mode)

	if mode < 1 or mode > 7 then
		fc.LogMessage("FDAI2_UpdateModes called with invalid FDAI2_Mode of " .. mode)
		mode = 1
	end

	fdai2Mode = 2 * mode
	if fdai2SwitchNegative == false then
		fdai2Mode = fdai2Mode - 1
	end

end

-- When we first load a vessel, all of the variables above are set to the
-- defaults listed.  This is great if the vessel hasn't flown, but if we'readouts
-- already in flight, they may be incorrect.
local function FDAI2_Initialize()
	
	fdai2Initialized = true

	local errPersist = fc.GetPersistentAsNumber("MAS_FDAI2_Error")
	if errPersist > 0 then
		fdai2ErrorScale = errPersist * 6
	end
	fc.SetPersistent("MAS_FDAI2_ErrorScalar", 1 / fdai2ErrorScale)
	
	local ratePersist = fc.GetPersistentAsNumber("MAS_FDAI2_Rate")
	local fdai2RateScale = 1
	if ratePersist > 0 then
		fdai2RateScale = ratePersist * 5
	end
	fc.SetPersistent("MAS_FDAI2_RateScalar", 1 / fdai2RateScale)

	if fc.GetPersistentAsNumber("MAS_FDAI2_Positive") > 0 then
		fdai2SwitchNegative = false
	else
		fdai2SwitchNegative = true
	end

	if fc.GetPersistentAsNumber("MAS_FDAI2_SyncSAS") > 0 then
		fdai2SyncSAS = true
	else
		fdai2SyncSAS = false
	end

	if fc.GetPersistentAsNumber("MAS_FDAI2_Power") > 0 then
		fdai2Power = true
	else
		fdai2Power = false
	end

	FDAI2_UpdateModes(fc.GetPersistentAsNumber("MAS_FDAI2_Mode"))

	-- Sync to SAS behavior is restored automatically in FDAI2_OffFlag()
end

-- Mode Selection

function FDAI2_NextMode()
	local mode = fc.AddPersistentClamped("MAS_FDAI2_Mode", 1, 1, 7)

	FDAI2_UpdateModes(mode)
end

function FDAI2_PrevMode()
	local mode = fc.AddPersistentClamped("MAS_FDAI2_Mode", -1, 1, 7)

	FDAI2_UpdateModes(mode)
end

-- Toggle +/- switch
function FDAI2_TogglePositive()
	local positive = fc.TogglePersistent("MAS_FDAI2_Positive")

	if positive > 0 then
		fdai2SwitchNegative = false
	else
		fdai2SwitchNegative = true
	end

	FDAI2_UpdateModes(fc.GetPersistentAsNumber("MAS_FDAI2_Mode"))
end

-- Toggle Sync to SAS
function FDAI2_ToggleSyncSAS()
	local sync = fc.TogglePersistent("MAS_FDAI2_SyncSAS")

	if sync > 0 then
		fdai2SyncSAS = true
	else
		fdai2SyncSAS = false
	end
end

-- Toggle Power
function FDAI2_TogglePower()
	local power = fc.TogglePersistent("MAS_FDAI2_Power")

	if power > 0 then
		fdai2Power = true
	else
		fdai2Power = false
	end
end

-- Should the Off flag be displayed?
-- This function is also the "FixedUpdate" script that does all of the heavy lifting of processing modes.
function FDAI2_OffFlag()
	if fdai2Initialized == false then
		FDAI2_Initialize()
	end
	
	if fc.Conditioned(1) < 1 or fdai2Power == false then
		fdai2OnFlag = false
	else
		fdai2OnFlag = true
	end

	-- Save previous state so we can conditionally update persistent vars.
	local oldErrorFlag = fdai2ErrorFlag
	local oldPortFlag = fdai2PortFlag

	fdai2PortFlag = false

	-- We know this function is called one time per prop, so we can use it like
	-- a custom "fixed update" to manage some of the computations for selecting
	-- data for the error needle readouts.  Because Avionics Systems caches the
	-- answer from this function, we know it will only be called once even if
	-- there are multiple FDAI 1 props in a single IVA.
	if fdai2SyncSAS == true then
		local sasMode = fc.GetSASMode()
		-- TODO: Does SAS automatically switch out of invalid modes, like
		-- Maneuver if there is no mode?  If not, I need to manually make
		-- changes here.

		-- SAS Modes (must be matched to SAS Speed Mode and converted to one of the
		-- valid modes above):
		--  0 = Stability Assist (no data / error)
		--  1 = [Mode] Prograde
		--  2 = [Mode] Retrograde
		--  3 = Normal
		--  4 = Anti-normal
		--  5 = Radial In
		--  6 = Radial Out
		--  7 = Target +
		--  8 = Target -
		--  9 = Maneuver
		-- If sasSpeedMode is 1, then we are in orbit-relative mode.
		-- If sasSpeedMode is 0, then we are in surface-relative mode.
		-- If sasSpeedMode is -1, then we are in target-relative mode.
		if sasMode == 0 then
			fdai2SASMode = 0
			fdai2ErrorFlag = true
		elseif sasMode == 1 or sasMode == 2 then
			fdai2ErrorFlag = false

			local sasSpeedMode = fc.GetSASSpeedMode()
			if sasSpeedMode > 0 then
				fdai2SASMode = sasMode + 2
			elseif sasSpeedMode < 0 then
				fdai2SASMode = sasMode + 12
			else
				fdai2SASMode = sasMode
			end
		elseif sasMode == 3 or sasMode == 4 then
			fdai2ErrorFlag = false

			fdai2SASMode = sasMode + 4
		elseif sasMode == 5 or sasMode == 6 then
			fdai2ErrorFlag = false

			-- TODO: For some reason, Radial Out and Radial In are in a
			-- different order on the Valid Modes.
			fdai2SASMode = 11 - sasMode
		elseif sasMode == 7 then
			fdai2ErrorFlag = false

			fdai2SASMode = 11
		elseif sasMode == 8 then
			fdai2ErrorFlag = false

			fdai2SASMode = 15
		elseif sasMode == 9 then
			if fc.ManeuverNodeExists() > 0 then
				fdai2ErrorFlag = false
			else
				fdai2ErrorFlag = true
			end

			fdai2SASMode = 10
		else
			fdai2ErrorFlag = true
			fc.LogMessage("Invalid sasMode " .. sasMode)
		end
		
		if fdai2SASMode > 10 and fc.TargetType() < 1 then
			fdai2ErrorFlag = true
		end
	elseif fdai2Mode == 9 or fdai2Mode == 10 then
		if fc.ManeuverNodeExists() > 0 then
			fdai2ErrorFlag = false
		else
			fdai2ErrorFlag = true
		end
	elseif fdai2Mode == 12 then
		-- Target alignment mode is only valid with docking ports
		-- We also require our reference transform to be a docking
		-- port.
		if fc.TargetType() == 2 and fc.ReferenceTransformType() == 3 then
			fdai2ErrorFlag = false
			fdai2PortFlag = true
		else
			fdai2ErrorFlag = true
		end
	elseif fdai2Mode > 10 and fc.TargetType() < 1 then
		fdai2ErrorFlag = true
	else
		fdai2ErrorFlag = false
	end

	if fdai2ErrorFlag ~= oldErrorFlag then
		-- If the error flag state change, update the persistent var
		if fdai2ErrorFlag == true then
			fc.SetPersistent("MAS_FDAI2_ErrorFlag", 1)
		else
			fc.SetPersistent("MAS_FDAI2_ErrorFlag", 0)
		end
	end
	
	if fdai2PortFlag ~= oldPortFlag then
		if fdai2ErrorFlag == true then
			fc.SetPersistent("MAS_FDAI2_PortFlag", 1)
		else
			fc.SetPersistent("MAS_FDAI2_PortFlag", 0)
		end
	end
	
	if fdai2OnFlag == true then
		return 0
	else
		return 1
	end
end

-- Scale controls

-- Update the error scalar
function FDAI2_NextErrorWrapped()
	local errPersist = fc.AddPersistentWrapped("MAS_FDAI2_Error", 1, 0, 3)

	if errPersist == 0 then
		fc.SetPersistent("MAS_FDAI2_ErrorScalar", 1)
		fdai2ErrorScale = 1
	else
		fc.SetPersistent("MAS_FDAI2_ErrorScalar", 0.166666667 / errPersist)
		fdai2ErrorScale = 6 * errPersist
	end
	
	-- So we can play some games with button press/release
	return 1
end

-- Increase the error scalar
-- function FDAI2_NextError()
	-- local errPersist = fc.AddPersistentClamped("MAS_FDAI2_Error", 1, 0, 2)

	-- -- errPersist is always greater than 0 here
	-- fdai2ErrorScale = errPersist * 6
	-- fc.SetPersistent("MAS_FDAI2_ErrorScalar", 1 / fdai2ErrorScale)
-- end

-- Decrease the error scalar
-- function FDAI2_PrevError()
	-- local errPersist = fc.AddPersistentClamped("MAS_FDAI2_Error", -1, 0, 2)

	-- if errPersist > 0 then
		-- -- Has to be 1
		-- fdai2ErrorScale = 6
	-- else
		-- fdai2ErrorScale = 1.2
	-- end
	-- fc.SetPersistent("MAS_FDAI2_ErrorScalar", 1 / fdai2ErrorScale)
-- end

-- Update the rate scalar
function FDAI2_NextRateWrapped()
	local ratePersist = fc.AddPersistentWrapped("MAS_FDAI2_Rate", 1, 0, 3)

	if ratePersist == 0 then
		fc.SetPersistent("MAS_FDAI2_RateScalar", 1)
	else
		fc.SetPersistent("MAS_FDAI2_RateScalar", 0.2 / ratePersist)
	end
	
	-- So we can play some games with button press/release
	return 1
end

-- Increase the rate scalar
-- function FDAI2_NextRate()
	-- local ratePersist = fc.AddPersistentClamped("MAS_FDAI2_Rate", 1, 0, 2)

	-- -- ratePersist is always greater than 0 here
	-- fc.SetPersistent("MAS_FDAI2_RateScalar", 0.2 / ratePersist)
-- end

-- Decrease the rate scalar
-- function FDAI2_PrevRate()
	-- local ratePersist = fc.AddPersistentClamped("MAS_FDAI2_Rate", -1, 0, 2)

	-- if ratePersist > 0 then
		-- -- Has to be 1
		-- fc.SetPersistent("MAS_FDAI2_RateScalar", 0.2)
	-- else
		-- fc.SetPersistent("MAS_FDAI2_RateScalar", 1)
	-- end
-- end

-- Error Needle readouts

-- Pitch
-- function FDAI2_PitchError()
	-- local pitchError = 0

	-- if fdai2OnFlag == true and fdai2ErrorFlag == false then
		-- pitchError = SelectPitch()
	-- end

	-- return pitchError / fdai2ErrorScale
-- end

-- Roll Error
-- function FDAI2_RollError()
	-- local rollError = 0

	-- if fdai2OnFlag == true and fdai2ErrorFlag == false then
		-- if fdai2SyncSAS == true and fdai2SASMode == 12 then
			-- rollError = fc.RollDockingAlignment()
		-- elseif fdai2Mode == 12 then
			-- rollError = fc.RollDockingAlignment()
		-- end
	-- end

	-- return rollError / fdai2ErrorScale
-- end
function FDAI2_RollError()
	--local rollError = 0

	--if fdai2OnFlag == true and fdai2ErrorFlag == false then
		if fdai2SyncSAS == true and fdai2SASMode == 12 then
			return fc.RollDockingAlignment()
		elseif fdai2Mode == 12 then
			return fc.RollDockingAlignment()
		else
			return 0
		end
	--end

	--return rollError / fdai2ErrorScale
end

-- Yaw
--function FDAI2_YawError()
--	local yawError = 0

--	if fdai2OnFlag == true and fdai2ErrorFlag == false then
		-- yawError = SelectYaw()
	-- end

	-- return yawError / fdai2ErrorScale
-- end
