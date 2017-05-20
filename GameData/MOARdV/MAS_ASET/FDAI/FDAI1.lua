-- ASET_FDAI1.lua
--
-- MOARdV's Avionics Systems
-- Lua functions for FDAI1
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

-- We use a bunch of local variables here so we don't have to query
-- persistents for certain information.  It is much faster to keep
-- these values here, and not have to ask the flight computer for them
-- every time, since the only way we allow them to change is through the
-- functions in this module.
local fdai1ErrorScale = 1.2
--local fdai1RateScale = 1.0
local fdai1ErrorFlag = false
local fdai1OnFlag = true
local fdai1Power = false
local fdai1PortFlag = false
local fdai1SyncSAS = false
local fdai1SwitchNegative = false
local fdai1Initialized = false
local fdai1Mode = 2
-- fdaiSASMode translates the SAS mode + SAS Speed Mode into an equivalent
-- value for use in our lookup table
local fdai1SASMode = 0

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
function FDAI1_PitchError()
	local func = nil

	if fdai1SyncSAS == true then
		func = pitchValue[fdai1SASMode]
	else
		func = pitchValue[fdai1Mode]
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
function FDAI1_YawError()
	local func = nil

	if fdai1SyncSAS == true then
		func = yawValue[fdai1SASMode]
	else
		func = yawValue[fdai1Mode]
	end

	if func ~= nil then
		return func()
	else
		return 0
	end
end

-- Function to update the mode selector.  'local' because no one outside of
-- this script should be calling it.
local function FDAI1_UpdateModes(mode)

	if mode < 1 or mode > 7 then
		fc.LogMessage("FDAI1_UpdateModes called with invalid FDAI1_Mode of " .. mode)
		mode = 1
	end

	fdai1Mode = 2 * mode
	if fdai1SwitchNegative == false then
		fdai1Mode = fdai1Mode - 1
	end

end

-- When we first load a vessel, all of the variables above are set to the
-- defaults listed.  This is great if the vessel hasn't flown, but if we'readouts
-- already in flight, they may be incorrect.
local function FDAI1_Initialize()
	
	fdai1Initialized = true

	local errPersist = fc.GetPersistentAsNumber("MAS_FDAI1_Error")
	if errPersist > 0 then
		fdai1ErrorScale = errPersist * 6
	end
	fc.SetPersistent("MAS_FDAI1_ErrorScalar", 1 / fdai1ErrorScale)
	
	local ratePersist = fc.GetPersistentAsNumber("MAS_FDAI1_Rate")
	local fdai1RateScale = 1
	if ratePersist > 0 then
		fdai1RateScale = ratePersist * 5
	end
	fc.SetPersistent("MAS_FDAI1_RateScalar", 1 / fdai1RateScale)

	if fc.GetPersistentAsNumber("MAS_FDAI1_Positive") > 0 then
		fdai1SwitchNegative = false
	else
		fdai1SwitchNegative = true
	end

	if fc.GetPersistentAsNumber("MAS_FDAI1_SyncSAS") > 0 then
		fdai1SyncSAS = true
	else
		fdai1SyncSAS = false
	end

	if fc.GetPersistentAsNumber("MAS_FDAI1_Power") > 0 then
		fdai1Power = true
	else
		fdai1Power = false
	end

	FDAI1_UpdateModes(fc.GetPersistentAsNumber("MAS_FDAI1_Mode"))

	-- Sync to SAS behavior is restored automatically in FDAI1_OffFlag()
end

-- Mode Selection

function FDAI1_NextMode()
	local mode = fc.AddPersistentClamped("MAS_FDAI1_Mode", 1, 1, 7)

	FDAI1_UpdateModes(mode)
end

function FDAI1_PrevMode()
	local mode = fc.AddPersistentClamped("MAS_FDAI1_Mode", -1, 1, 7)

	FDAI1_UpdateModes(mode)
end

-- Toggle +/- switch
function FDAI1_TogglePositive()
	local positive = fc.TogglePersistent("MAS_FDAI1_Positive")

	if positive > 0 then
		fdai1SwitchNegative = false
	else
		fdai1SwitchNegative = true
	end

	FDAI1_UpdateModes(fc.GetPersistentAsNumber("MAS_FDAI1_Mode"))
end

-- Toggle Sync to SAS
function FDAI1_ToggleSyncSAS()
	local sync = fc.TogglePersistent("MAS_FDAI1_SyncSAS")

	if sync > 0 then
		fdai1SyncSAS = true
	else
		fdai1SyncSAS = false
	end
end

-- Toggle Power
function FDAI1_TogglePower()
	local power = fc.TogglePersistent("MAS_FDAI1_Power")

	if power > 0 then
		fdai1Power = true
	else
		fdai1Power = false
	end
end

-- Should the Off flag be displayed?
-- This function is also the "FixedUpdate" script that does all of the heavy lifting of processing modes.
function FDAI1_OffFlag()
	if fdai1Initialized == false then
		FDAI1_Initialize()
	end
	
	if fc.Conditioned(1) < 1 or fdai1Power == false then
		fdai1OnFlag = false
	else
		fdai1OnFlag = true
	end

	-- Save previous state so we can conditionally update persistent vars.
	local oldErrorFlag = fdai1ErrorFlag
	local oldPortFlag = fdai1PortFlag

	fdai1PortFlag = false

	-- We know this function is called one time per prop, so we can use it like
	-- a custom "fixed update" to manage some of the computations for selecting
	-- data for the error needle readouts.  Because Avionics Systems caches the
	-- answer from this function, we know it will only be called once even if
	-- there are multiple FDAI 1 props in a single IVA.
	if fdai1SyncSAS == true then
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
			fdai1SASMode = 0
			fdai1ErrorFlag = true
		elseif sasMode == 1 or sasMode == 2 then
			fdai1ErrorFlag = false

			local sasSpeedMode = fc.GetSASSpeedMode()
			if sasSpeedMode > 0 then
				fdai1SASMode = sasMode + 2
			elseif sasSpeedMode < 0 then
				fdai1SASMode = sasMode + 12
			else
				fdai1SASMode = sasMode
			end
		elseif sasMode == 3 or sasMode == 4 then
			fdai1ErrorFlag = false

			fdai1SASMode = sasMode + 4
		elseif sasMode == 5 or sasMode == 6 then
			fdai1ErrorFlag = false

			-- TODO: For some reason, Radial Out and Radial In are in a
			-- different order on the Valid Modes.
			fdai1SASMode = 11 - sasMode
		elseif sasMode == 7 then
			fdai1ErrorFlag = false

			fdai1SASMode = 11
		elseif sasMode == 8 then
			fdai1ErrorFlag = false

			fdai1SASMode = 15
		elseif sasMode == 9 then
			if fc.ManeuverNodeExists() > 0 then
				fdai1ErrorFlag = false
			else
				fdai1ErrorFlag = true
			end

			fdai1SASMode = 10
		else
			fdai1ErrorFlag = true
			fc.LogMessage("Invalid sasMode " .. sasMode)
		end
		
		if fdai1SASMode > 10 and fc.TargetType() < 1 then
			fdai1ErrorFlag = true
		end
	elseif fdai1Mode == 9 or fdai1Mode == 10 then
		if fc.ManeuverNodeExists() > 0 then
			fdai1ErrorFlag = false
		else
			fdai1ErrorFlag = true
		end
	elseif fdai1Mode == 12 then
		-- Target alignment mode is only valid with docking ports
		-- We also require our reference transform to be a docking
		-- port.
		if fc.TargetType() == 2 and fc.ReferenceTransformType() == 3 then
			fdai1ErrorFlag = false
			fdai1PortFlag = true
		else
			fdai1ErrorFlag = true
		end
	elseif fdai1Mode > 10 and fc.TargetType() < 1 then
		fdai1ErrorFlag = true
	else
		fdai1ErrorFlag = false
	end

	if fdai1ErrorFlag ~= oldErrorFlag then
		-- If the error flag state change, update the persistent var
		if fdai1ErrorFlag == true then
			fc.SetPersistent("MAS_FDAI1_ErrorFlag", 1)
		else
			fc.SetPersistent("MAS_FDAI1_ErrorFlag", 0)
		end
	end
	
	if fdai1PortFlag ~= oldPortFlag then
		if fdai1ErrorFlag == true then
			fc.SetPersistent("MAS_FDAI1_PortFlag", 1)
		else
			fc.SetPersistent("MAS_FDAI1_PortFlag", 0)
		end
	end
	
	if fdai1OnFlag == true then
		return 0
	else
		return 1
	end
end

-- Scale controls

-- Update the error scalar
function FDAI1_NextErrorWrapped()
	local errPersist = fc.AddPersistentWrapped("MAS_FDAI1_Error", 1, 0, 3)

	if errPersist == 0 then
		fc.SetPersistent("MAS_FDAI1_ErrorScalar", 1)
		fdai1ErrorScale = 1
	else
		fc.SetPersistent("MAS_FDAI1_ErrorScalar", 0.166666667 / errPersist)
		fdai1ErrorScale = 6 * errPersist
	end
	
	-- So we can play some games with button press/release
	return 1
end

-- Increase the error scalar
-- function FDAI1_NextError()
	-- local errPersist = fc.AddPersistentClamped("MAS_FDAI1_Error", 1, 0, 2)

	-- -- errPersist is always greater than 0 here
	-- fdai1ErrorScale = errPersist * 6
	-- fc.SetPersistent("MAS_FDAI1_ErrorScalar", 1 / fdai1ErrorScale)
-- end

-- Decrease the error scalar
-- function FDAI1_PrevError()
	-- local errPersist = fc.AddPersistentClamped("MAS_FDAI1_Error", -1, 0, 2)

	-- if errPersist > 0 then
		-- -- Has to be 1
		-- fdai1ErrorScale = 6
	-- else
		-- fdai1ErrorScale = 1.2
	-- end
	-- fc.SetPersistent("MAS_FDAI1_ErrorScalar", 1 / fdai1ErrorScale)
-- end

-- Update the rate scalar
function FDAI1_NextRateWrapped()
	local ratePersist = fc.AddPersistentWrapped("MAS_FDAI1_Rate", 1, 0, 3)

	if ratePersist == 0 then
		fc.SetPersistent("MAS_FDAI1_RateScalar", 1)
	else
		fc.SetPersistent("MAS_FDAI1_RateScalar", 0.2 / ratePersist)
	end
	
	-- So we can play some games with button press/release
	return 1
end

-- Increase the rate scalar
-- function FDAI1_NextRate()
	-- local ratePersist = fc.AddPersistentClamped("MAS_FDAI1_Rate", 1, 0, 2)

	-- -- ratePersist is always greater than 0 here
	-- fc.SetPersistent("MAS_FDAI1_RateScalar", 0.2 / ratePersist)
-- end

-- Decrease the rate scalar
-- function FDAI1_PrevRate()
	-- local ratePersist = fc.AddPersistentClamped("MAS_FDAI1_Rate", -1, 0, 2)

	-- if ratePersist > 0 then
		-- -- Has to be 1
		-- fc.SetPersistent("MAS_FDAI1_RateScalar", 0.2)
	-- else
		-- fc.SetPersistent("MAS_FDAI1_RateScalar", 1)
	-- end
-- end

-- Error Needle readouts

-- Pitch
-- function FDAI1_PitchError()
	-- local pitchError = 0

	-- if fdai1OnFlag == true and fdai1ErrorFlag == false then
		-- pitchError = SelectPitch()
	-- end

	-- return pitchError / fdai1ErrorScale
-- end

-- Roll Error
-- function FDAI1_RollError()
	-- local rollError = 0

	-- if fdai1OnFlag == true and fdai1ErrorFlag == false then
		-- if fdai1SyncSAS == true and fdai1SASMode == 12 then
			-- rollError = fc.RollDockingAlignment()
		-- elseif fdai1Mode == 12 then
			-- rollError = fc.RollDockingAlignment()
		-- end
	-- end

	-- return rollError / fdai1ErrorScale
-- end
function FDAI1_RollError()
	--local rollError = 0

	--if fdai1OnFlag == true and fdai1ErrorFlag == false then
		if fdai1SyncSAS == true and fdai1SASMode == 12 then
			return fc.RollDockingAlignment()
		elseif fdai1Mode == 12 then
			return fc.RollDockingAlignment()
		else
			return 0
		end
	--end

	--return rollError / fdai1ErrorScale
end

-- Yaw
--function FDAI1_YawError()
--	local yawError = 0

--	if fdai1OnFlag == true and fdai1ErrorFlag == false then
		-- yawError = SelectYaw()
	-- end

	-- return yawError / fdai1ErrorScale
-- end
