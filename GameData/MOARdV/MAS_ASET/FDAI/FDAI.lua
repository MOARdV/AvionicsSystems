-- FDAI.lua
--
-- MOARdV's Avionics Systems
-- Lua functions for the FDAI
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

-- The idea behind this script is every FDAI is controlled the same way.  We
-- take advantage of Lua tables in order to keep all state here.  By using a
-- table as an array, we can support an arbitrary number of FDAI units with a
-- single script.
local fdaiState = { }

-- Initialize a table to track an arbitrary FDAI.  This function simply
-- initializes the variables in the table so that none of them are nil.
local function InitFDAI()
	
	local fdai = {}
	
	fdai.errorScale = 1
	fdai.errorFlag = false
	fdai.onFlag = true
	fdai.power = false
	fdai.portFlag = false
	fdai.syncSAS = false
	fdai.switchNegative = false
	fdai.initialized = false
	fdai.mode = 2
	fdai.sasMode = 0
	
	return fdai
end

-- Select the FDAI indexed by 'which'.  If that FDAI hasn't been instantiated,
-- create it here.  As a practical matter, the table is initialized when the
-- FDAI prop is initialized in the startupScript fdaiInitialize()
local function GetFDAI(which)
	if fdaiState[which] == nil then
		fdaiState[which] = InitFDAI()
	end
	
	return fdaiState[which]
end

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
function fdaiPitchError(which)
	local fdai = GetFDAI(which)
	local func = nil

	if fdai.syncSAS == true then
		func = pitchValue[fdai.sasMode]
	else
		func = pitchValue[fdai.mode]
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
function fdaiYawError(which)
	local fdai = GetFDAI(which)
	local func = nil

	if fdai.syncSAS == true then
		func = yawValue[fdai.sasMode]
	else
		func = yawValue[fdai.mode]
	end

	if func ~= nil then
		return func()
	else
		return 0
	end
end

-- Function to update the mode selector.  'local' because no one outside of
-- this script should be calling it.
local function fdaiUpdateModes(which, mode)

	if mode < 1 or mode > 7 then
		fc.LogMessage("fdaiUpdateModes(" .. which .. ") called with invalid mode of " .. mode)
		mode = 1
	end

	local fdai = GetFDAI(which)
	fdai.mode = 2 * mode
	if fdai.switchNegative == false then
		fdai.mode = fdai.mode - 1
	end

end

-- When we first load a vessel, all of the variables above are set to the
-- defaults listed.  This is great if the vessel hasn't flown, but if we're
-- already in flight, they may be incorrect.  This function restores values.
function fdaiInitialize(which)

	local fdai = GetFDAI(which)
	local persistentPrefix = "MAS_FDAI" .. which .. "_"
	
	fdai.initialized = true

	local errPersist = fc.GetPersistentAsNumber(persistentPrefix .. "Error")
	if errPersist > 0 then
		fdai.errorScale = errPersist * 6
	else
		fdai.errorScale = 1
	end
	fc.SetPersistent(persistentPrefix .. "ErrorScalar", 1 / fdai.errorScale)
	
	local ratePersist = fc.GetPersistentAsNumber(persistentPrefix .. "Rate")
	local fdaiRateScale = 1
	if ratePersist > 0 then
		fdaiRateScale = ratePersist * 5
	end
	fc.SetPersistent(persistentPrefix .. "RateScalar", 1 / fdaiRateScale)

	if fc.GetPersistentAsNumber(persistentPrefix .. "Positive") > 0 then
		fdai.switchNegative = false
	else
		fdai.switchNegative = true
	end

	if fc.GetPersistentAsNumber(persistentPrefix .. "SyncSAS") > 0 then
		fdai.syncSAS = true
	else
		fdai.syncSAS = false
	end

	if fc.GetPersistentAsNumber(persistentPrefix .. "Power") > 0 then
		fdai.power = true
	else
		fdai.power = false
	end

	local fdaiMode = fc.GetPersistentAsNumber(persistentPrefix .. "Mode")
	if fdaiMode < 1 or fdaiMode > 7 then
		fdaiMode = 1
		fc.SetPersistent(persistentPrefix .. "Mode", fdaiMode)
	end
	
	fdaiUpdateModes(which, fdaiMode)

	-- Sync to SAS behavior is restored automatically in fdaiOffFlag()
end

-- Mode Selection

function fdaiNextMode(which)
	local mode = fc.AddPersistentClamped("MAS_FDAI" .. which .. "_Mode", 1, 1, 7)

	fdaiUpdateModes(which, mode)
end

function fdaiPrevMode(which)
	local mode = fc.AddPersistentClamped("MAS_FDAI" .. which .. "_Mode", -1, 1, 7)

	fdaiUpdateModes(which, mode)
end

-- Toggle +/- switch
function fdaiTogglePositive(which)
	local persistentPrefix = "MAS_FDAI" .. which .. "_"
	local positive = fc.TogglePersistent(persistentPrefix .. "Positive")
	local fdai = GetFDAI(which)

	if positive > 0 then
		fdai.switchNegative = false
	else
		fdai.switchNegative = true
	end

	fdaiUpdateModes(which, fc.GetPersistentAsNumber(persistentPrefix .. "Mode"))
end

-- Toggle Sync to SAS
function fdaiToggleSyncSAS(which)
	local sync = fc.TogglePersistent("MAS_FDAI" .. which .. "_SyncSAS")
	local fdai = GetFDAI(which)

	if sync > 0 then
		fdai.syncSAS = true
	else
		fdai.syncSAS = false
	end
end

-- Toggle Power
function fdaiTogglePower(which)
	local power = fc.TogglePersistent("MAS_FDAI" .. which .. "_Power")
	local fdai = GetFDAI(which)

	if power > 0 then
		fdai.power = true
	else
		fdai.power = false
	end
end

-- Should the Off flag be displayed?
-- This function is also the "FixedUpdate" script that does all of the heavy lifting of processing modes.
function fdaiOffFlag(which)

	local fdai = GetFDAI(which)
	
	if fc.Conditioned(1) < 1 or fdai.power == false then
		fdai.onFlag = false
	else
		fdai.onFlag = true
	end

	-- Save previous state so we can conditionally update persistent vars.
	local oldErrorFlag = fdai.errorFlag
	local oldPortFlag = fdai.portFlag

	fdai.portFlag = false

	-- We know this function is called one time per prop, so we can use it like
	-- a custom "fixed update" to manage some of the computations for selecting
	-- data for the error needle readouts.  Because Avionics Systems caches the
	-- answer from this function, we know it will only be called once even if
	-- there are multiple FDAI 1 props in a single IVA.
	if fdai.syncSAS == true then
		local sasMode = fc.GetSASMode()
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
			fdai.sasMode = 0
			fdai.errorFlag = true
		elseif sasMode == 1 or sasMode == 2 then
			fdai.errorFlag = false

			local sasSpeedMode = fc.GetSASSpeedMode()
			if sasSpeedMode > 0 then
				fdai.sasMode = sasMode + 2
			elseif sasSpeedMode < 0 then
				fdai.sasMode = sasMode + 12
			else
				fdai.sasMode = sasMode
			end
		elseif sasMode == 3 or sasMode == 4 then
			fdai.errorFlag = false

			fdai.sasMode = sasMode + 4
		elseif sasMode == 5 or sasMode == 6 then
			fdai.errorFlag = false

			-- TODO: For some reason, Radial Out and Radial In are in a
			-- different order on the Valid Modes.
			fdai.sasMode = 11 - sasMode
		elseif sasMode == 7 then
			fdai.errorFlag = false

			fdai.sasMode = 11
		elseif sasMode == 8 then
			fdai.errorFlag = false

			fdai.sasMode = 15
		elseif sasMode == 9 then
			if fc.ManeuverNodeExists() > 0 then
				fdai.errorFlag = false
			else
				fdai.errorFlag = true
			end

			fdai.sasMode = 10
		else
			fdai.errorFlag = true
			fc.LogMessage("Invalid sasMode " .. sasMode)
		end
		
		if fdai.sasMode > 10 and fc.TargetType() < 1 then
			fdai.errorFlag = true
		end
	elseif fdai.mode == 9 or fdai.mode == 10 then
		if fc.ManeuverNodeExists() > 0 then
			fdai.errorFlag = false
		else
			fdai.errorFlag = true
		end
	elseif fdai.mode == 12 then
		-- Target alignment mode is only valid with docking ports
		-- We also require our reference transform to be a docking
		-- port.
		if fc.TargetType() == 2 and fc.ReferenceTransformType() == 3 then
			fdai.errorFlag = false
			fdai.portFlag = true
		else
			fdai.errorFlag = true
		end
	elseif fdai.mode > 10 and fc.TargetType() < 1 then
		fdai.errorFlag = true
	else
		fdai.errorFlag = false
	end

	if fdai.errorFlag ~= oldErrorFlag then
		-- If the error flag state change, update the persistent var
		local persistentPrefix = "MAS_FDAI" .. which .. "_"
		if fdai.errorFlag == true then
			fc.SetPersistent(persistentPrefix .. "ErrorFlag", 1)
		else
			fc.SetPersistent(persistentPrefix .. "ErrorFlag", 0)
		end
	end
	
	if fdai.portFlag ~= oldPortFlag then
		local persistentPrefix = "MAS_FDAI" .. which .. "_"
		if fdai.errorFlag == true then
			fc.SetPersistent(persistentPrefix .. "PortFlag", 1)
		else
			fc.SetPersistent(persistentPrefix .. "PortFlag", 0)
		end
	end
	
	if fdai.onFlag == true then
		return 0
	else
		return 1
	end
end

-- Scale controls

-- Update the error scalar
function fdaiNextErrorWrapped(which)
	local persistentPrefix = "MAS_FDAI" .. which .. "_"
	local errPersist = fc.AddPersistentWrapped(persistentPrefix .. "Error", 1, 0, 3)
	local fdai = GetFDAI(which)

	if errPersist == 0 then
		fc.SetPersistent(persistentPrefix .. "ErrorScalar", 1)
		fdai.errorScale = 1
	else
		fc.SetPersistent(persistentPrefix .. "ErrorScalar", 0.166666667 / errPersist)
		fdai.errorScale = 6 * errPersist
	end
	
	-- So we can play some games with button press/release
	return 1
end

-- Update the rate scalar
function fdaiNextRateWrapped(which)
	local persistentPrefix = "MAS_FDAI" .. which .. "_"
	local ratePersist = fc.AddPersistentWrapped(persistentPrefix .. "Rate", 1, 0, 3)
	---local fdai = GetFDAI(which)

	if ratePersist == 0 then
		fc.SetPersistent(persistentPrefix .. "RateScalar", 1)
	else
		fc.SetPersistent(persistentPrefix .. "RateScalar", 0.2 / ratePersist)
	end
	
	-- So we can play some games with button press/release
	return 1
end

function fdaiRateClamped(which, direction)
	local persistentPrefix = "MAS_FDAI" .. which .. "_"
	local ratePersist = fc.AddPersistentClamped(persistentPrefix .. "Rate", direction, 0, 2)
	---local fdai = GetFDAI(which)

	if ratePersist == 0 then
		fc.SetPersistent(persistentPrefix .. "RateScalar", 1)
	else
		fc.SetPersistent(persistentPrefix .. "RateScalar", 0.2 / ratePersist)
	end
	
	-- So we can play some games with button press/release
	return 1
end

-- Error Needle readouts

function fdaiRollError(which)
	local fdai = GetFDAI(which)
	
	if fdai.syncSAS == true and fdai.sasMode == 12 then
		return fc.RollDockingAlignment()
	elseif fdai.mode == 12 then
		return fc.RollDockingAlignment()
	else
		return 0
	end
end
