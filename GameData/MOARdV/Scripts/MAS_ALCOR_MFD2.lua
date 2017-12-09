-- MAS_ALCOR_MFD2.lua
--
-- MOARdV's Avionics Systems
-- Lua scripts for ALCOR Multi-Function Displays
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

------------------------------------------------------------------------------
-- Prop initialization: Make sure certain fields are initialized if they do
-- not already have values.
function MAS_Mfd2_Init(propId)

	if fc.GetPersistentExists(propId .. "-Scalar") < 1 then
		fc.SetPersistent(propId .. "-Scalar", 1)
	end

	if fc.GetPersistentExists("MFD2-TimeMode") < 1 then
		fc.SetPersistent("MFD2-TimeMode", " MET")
	end

	if fc.GetPersistentExists(propId .. "-PlanMode") < 1 then
		if mechjeb.Available() > 0 then
			fc.SetPersistent(propId .. "-PlanMode", "MAS_MFD2_Plan")
		else
			fc.SetPersistent(propId .. "-PlanMode", "MAS_MFD2_ManualPlan")
		end
	end
	
	if fc.GetPersistentExists(propId .. "-Att-ManualCaption") < 1 then
		fc.SetPersistent(propId .. "-Att-ManualCaption", "Srf Prograde")
	end
end

------------------------------------------------------------------------------
-- SAS mode icon control, using a texture included in the ASET SAS buttons
-- Demonstrates an application of dynamic texture shifting to select from
-- a texture atlas.  MAS_Mfd2_SAS_ShiftU() updates a persistent variable
-- for the V value to avoid having to make two calls into Lua (one for U,
-- one for V).
local SAS_ShiftU_value =
{
	0.00,
	0.00,
	0.25,
	0.00,
	0.25,
	0.25,
	0.00,
	0.50,
	0.75,
	0.25
}

local SAS_ShiftV_value =
{
	0.75,
	0.50,
	0.50,
	0.25,
	0.25,
	0.00,
	0.00,
	0.75,
	0.75,
	0.75
}

function MAS_Mfd2_SAS_ShiftU()
	-- return the U shift based on SAS mode, set MFD2-SAS-ShiftV as a
	-- persistent so we don't incur the overhead of two Lua calls for
	-- the shift.
	local mode = fc.GetSASMode()

	fc.SetPersistent("MFD2-SAS-ShiftV", SAS_ShiftV_value[mode+1])

	return SAS_ShiftU_value[mode+1]
end

------------------------------------------------------------------------------
-- MFD time display shows using fc.PeriodCount to cycle between multiple
-- options automatically.  MAS_Mfd2_Time also updates the mode caption
-- persistent string when the mode changes - not every time the function is
-- called.

local timeMode = 0
--local periodCounter = 0

local modeCaptions =
{
	" MET",
	"MNVR",
	" KAC"
}

function MAS_Mfd2_Time()

	local newMode
	local localTime

	if kac.AlarmCount() > 0 then

		if fc.ManeuverNodeExists() > 0 then
			local counter = fc.PeriodCount(0.4, 3)

			if counter == 0 then
				localTime = fc.MET()
			elseif counter == 1 then
				localTime = fc.ManeuverNodeTime()
			else
				localTime = kac.TimeToAlarm()
			end

			newMode = counter + 1
		else
			if fc.PeriodCount(0.4, 2) > 0 then
				newMode = 3
				localTime = kac.TimeToAlarm()
			else
				newMode = 1
				localTime = fc.MET()
			end
		end

	elseif fc.ManeuverNodeExists() > 0 then

		if fc.PeriodCount(0.4, 2) > 0 then
			newMode = 2
			localTime = fc.ManeuverNodeTime()
		else
			newMode = 1
			localTime = fc.MET()
		end

	else
		newMode = 1
		localTime = fc.MET()
	end

	if newMode ~= timeMode then
		timeMode = newMode
		fc.SetPersistent("MFD2-TimeMode", modeCaptions[timeMode])
	end

	return localTime
end

------------------------------------------------------------------------------
-- MechJeb autopilot conntrol.  Shows an onEnter page method, and updating a
-- numeric persistent and a string persistent concurrently, along with an
-- action that branches to select funciton based on the numeric function
-- value.
local planCaptions =
{
	"Change Apoapsis to...",
	"Circularize at...",
	"Change Periapsis to...",
	"Hohmann Transfer to...",
	"Match Velocity with..."
}

function MAS_Mfd2_Plan_Init(propId)
	fc.SetPersistent(propId .. "-PlanCaption", planCaptions[1 + fc.GetPersistentAsNumber(propId .. "-PlanFunction")])
end

function MAS_Mfd2_Plan_Mode(modeId, captionId)
	local newMode = fc.AddPersistentWrapped(modeId, 1, 0, 5)

	fc.SetPersistent(captionId, planCaptions[1 + newMode])
end

function MAS_Mfd2_Plan_Create(functionId, altitudeId)
	local fn = fc.GetPersistentAsNumber(functionId)

	if fn == 0 then
		mechjeb.ChangeApoapsis(1000 * fc.GetPersistentAsNumber(altitudeId))
	elseif fn == 1 then
		mechjeb.CircularizeAt(1000 * fc.GetPersistentAsNumber(altitudeId))
	elseif fn == 2 then
		mechjeb.ChangePeriapsis(1000 * fc.GetPersistentAsNumber(altitudeId))
	elseif fn == 3 then
		mechjeb.PlotTransfer()
	elseif fn == 4 then
		mechjeb.MatchVelocities()
	end

end

function MAS_Mfd2_Plan_SetMode(propId, modeId, mode)
	if mode < 1 then
		fc.SetPersistent(modeId, "MAS_MFD2_Plan")
		fc.SetPersistent(propId, "MAS_MFD2_Plan")
	else
		fc.SetPersistent(modeId, "MAS_MFD2_ManualPlan")
		fc.SetPersistent(propId, "MAS_MFD2_ManualPlan")
	end
end

------------------------------------------------------------------------------
-- Preflight MechJeb configuration.  Unlike some other configuration inputs,
-- the input values are read from mechjeb directly, so we know the result is
-- correct, and that it has been fed into the mechjeb ascent autopilot.
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

------------------------------------------------------------------------------
-- Attitude Indicator display.  Player may select SAS-auto mode, which shows
-- error needles for the current SAS mode, or manual mode, which shows error
-- based on the player-selected mode.  The pitch value is updated into a
-- persistent value to allow us to avoid two calls into Lua.

-- WARNING: If you reorder this list, you *must* copy the first text field into the
-- init method above.
local manualAttManualCaption =
{
	"Srf Prograde",
	"Srf Retrograde",
	"Obt Prograde",
	"Obt Retrograde",
	"Obt Radial Out",
	"Obt Radial In",
	"Obt Normal",
	"Obt Anti-Normal",
	"Maneuver Node",
	"Waypoint",
	"Target",
	"Anti-Target",
	"Tgt Rel Prograde",
	"Tgt Rel Retrograde",
	"Docking Alignment"
}

local manualPitchValue =
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
	fc.PitchWaypoint,
	fc.PitchTarget,
	fc.PitchAntiTarget,
	fc.PitchTargetPrograde,
	fc.PitchTargetRetrograde,
	fc.PitchDockingAlignment
}

local manualYawValue =
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
	fc.YawWaypoint,
	fc.YawTarget,
	fc.YawAntiTarget,
	fc.YawTargetPrograde,
	fc.YawTargetRetrograde,
	fc.YawDockingAlignment
}

function MAS_Mfd2_NextManualMode(modeId, captionId, direction)
	local mode = fc.AddPersistentWrapped(modeId, direction, 0, 14)
	fc.SetPersistent(captionId, manualAttManualCaption[mode+1])
end

function MAS_Mfd2_Att_Yaw(propId)

	local autoMode = fc.GetPersistentAsNumber(propId .. "-Att-Auto")

	local yaw = 0
	
	if autoMode > 0 then
		
		fc.SetPersistent(propId .. "-Att-ManualError", 0)
		fc.SetPersistent(propId .. "-Att-Pitch", 0)
		
	else
		local manualMode = fc.GetPersistentAsNumber(propId .. "-Att-ManualMode") + 1
		
		-- Validation of special modes (remember I added +1 to them)
		if manualMode == 9 then
			-- Manuever Node
			fc.SetPersistent(propId .. "-Att-ManualError", 1 - fc.ManeuverNodeExists())
		elseif manualMode == 10 then
			-- Waypoint
			fc.SetPersistent(propId .. "-Att-ManualError", 1 - nav.WaypointActive())
		elseif manualMode > 10 then
			-- Target Modes
			if manualMode == 15 then
				-- Docking alignment
				if fc.TargetType() == 2 then
					fc.SetPersistent(propId .. "-Att-ManualError", 0)
				else
					fc.SetPersistent(propId .. "-Att-ManualError", 1)
				end
			else
				-- Other modes
				fc.SetPersistent(propId .. "-Att-ManualError", 1 - fc.TargetType())
			end
		else
			fc.SetPersistent(propId .. "-Att-ManualError", 0)
		end
		
		yaw = manualYawValue[manualMode]()
		fc.SetPersistent(propId .. "-Att-Pitch", manualPitchValue[manualMode]())
	end

	return yaw
end

------------------------------------------------------------------------------
-- Resource display.  Allows the user to select between three gauges, and
-- change what each one is displaying.

function MAS_Mfd2_Rsrc_Plus(propId, rowId)
	local row = fc.GetPersistentAsNumber(rowId)

	if row == 0 then
		fc.AddPersistentWrapped(propId .. "-RsrcUser1", 1, 0, fc.ResourceCount())
	elseif row == 1 then
		fc.AddPersistentWrapped(propId .. "-RsrcUser2", 1, 0, fc.ResourceCount())
	elseif row == 2 then
		fc.AddPersistentWrapped(propId .. "-RsrcUser3", 1, 0, fc.ResourceCount())
	end
end

function MAS_Mfd2_Rsrc_Minus(propId, rowId)
	local row = fc.GetPersistentAsNumber(rowId)

	if row == 0 then
		fc.AddPersistentWrapped(propId .. "-RsrcUser1", -1, 0, fc.ResourceCount())
	elseif row == 1 then
		fc.AddPersistentWrapped(propId .. "-RsrcUser2", -1, 0, fc.ResourceCount())
	elseif row == 2 then
		fc.AddPersistentWrapped(propId .. "-RsrcUser3", -1, 0, fc.ResourceCount())
	end
end
