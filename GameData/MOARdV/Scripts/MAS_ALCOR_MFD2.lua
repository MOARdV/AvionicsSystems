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
	
	local result = fc.TrackResourceConverter(1, "ElectroPlasma")
	
	if result < 1 then
		fc.LogMessage("Error registering ElectroPlasma: " .. result)
	end
	
	result = fc.TrackResourceConverter(2, "GravityWaves")
	
	if result < 1 then
		fc.LogMessage("Error registering GravityWaves: " .. result)
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
-- MAS Maneuver planning.  Shows an action that branches to select function
-- based on the numeric function value.
local planCaptions =
{
	"Change Apoapsis to...",
	"Circularize at...",
	"Change Periapsis to...",
	"Return from moon to...",
	"Hohmann Transfer to...",
	"Match Velocity with...",
	"Match Inclination  ..."
}

function MAS_Mfd2_Plan_Init(propId)
	fc.SetPersistent(propId .. "-PlanCaption", planCaptions[1 + fc.GetPersistentAsNumber(propId .. "-PlanFunction")])
end

function MAS_Mfd2_Plan_Mode(modeId, captionId, direction)
	local newMode = fc.AddPersistentWrapped(modeId, direction, 0, 7)

	fc.SetPersistent(captionId, planCaptions[1 + newMode])
end

function MAS_Mfd2_Plan_Create(functionId, altitudeId)
	local fn = fc.GetPersistentAsNumber(functionId)

	if fn == 0 then
		transfer.ChangeApoapsis(1000 * fc.GetPersistentAsNumber(altitudeId))
	elseif fn == 1 then
		transfer.CircularizeAltitude(1000 * fc.GetPersistentAsNumber(altitudeId))
	elseif fn == 2 then
		transfer.ChangePeriapsis(1000 * fc.GetPersistentAsNumber(altitudeId))
	elseif fn == 3 then
		transfer.ReturnFromMoon(1000 * fc.GetPersistentAsNumber(altitudeId))
	elseif fn == 4 then
		transfer.HohmannTransfer()
	elseif fn == 5 then
		transfer.MatchVelocities()
	elseif fn == 6 then
		transfer.MatchPlane()
	end

end

local manualVariableNames =
{
	"-ManualPlanPrograde",
	"-ManualPlanNormal",
	"-ManualPlanRadial",
	"-ManualPlanTime"
}

function MAS_Mfd2_Manual_Plan_Create(propId)
	local when = fc.GetPersistentAsNumber(propId .. manualVariableNames[4])

	if when > 0 then
		return fc.AddManeuverNode(fc.GetPersistentAsNumber(propId .. manualVariableNames[1]),
			fc.GetPersistentAsNumber(propId .. manualVariableNames[2]),
			fc.GetPersistentAsNumber(propId .. manualVariableNames[3]),
			when + fc.UT())
	end

	return 0
end

function MAS_Mfd2_Manual_Plan_Change(propId, direction, scale, variable)
	local persistent = propId .. manualVariableNames[variable+1]
	local amount = direction * scale * 0.1

	if variable == 3 then
		if amount == 10 then amount = 60
		elseif amount == 100 then amount = 3600
		end

		fc.AddPersistentClamped(persistent, amount, 0, 604800)
	else
		fc.AddPersistent(persistent, amount)
	end

	return 1
end

function MAS_Mfd2_Manual_Plan_Clear(propId)
	fc.SetPersistent(propId .. manualVariableNames[1], 0)
	fc.SetPersistent(propId .. manualVariableNames[2], 0)
	fc.SetPersistent(propId .. manualVariableNames[3], 0)
	fc.SetPersistent(propId .. manualVariableNames[4], 0)
end

function MAS_Mfd2_WarpToManeuver()
	local burnTime = fc.ManeuverNodeBurnTime()

	if fc.ManeuverNodeExists() > 0 and burnTime > 0 then
		-- ManeuverNodeTime is negative if it is in the future, so we add to
		-- the base value.
		local timeToManeuver = fc.ManeuverNodeTime() + 0.5 * burnTime + 5

		--fc.LogMessage("Maneuver T = " .. fc.ManeuverNodeTime() .. ", burn = " .. burnTime .. ", timeToManeuver = " .. timeToManeuver)
		if timeToManeuver < 0 then
			fc.WarpTo(fc.UT() - timeToManeuver)
		end
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

------------------------------------------------------------------------------
--
function MAS_Mfd2_NextCameraMode(cameraId)
	
	local modeCount = fc.GetCameraModeCount(cameraId)
	
	if modeCount > 1 then
		local activeMode = fc.GetCameraMode(cameraId)
		
		if activeMode < (modeCount - 1) then
			fc.SetCameraMode(cameraId, activeMode + 1)
		else
			fc.SetCameraMode(cameraId, 0)
		end
	end
end

------------------------------------------------------------------------------
-- Verify that the WBI VTOL Manager is available.
function MAS_Mfd2_Vtol_IfValid(propId)
	if vtol.Available() == 1 then
		fc.SetPersistent(propId, "MAS_MFD2_VtolManager")
	end
end

------------------------------------------------------------------------------
-- Conditional R7 softkey
function MAS_Mfd2_Flight_R7Softkey(panel4Mode)
	
	if panel4Mode == 0 then
		local activeMode = vtol.GetThrustMode()
		
		if activeMode == 1 then
			vtol.SetThrustMode(-1)
		else
			vtol.SetThrustMode(activeMode + 1)
		end
	end

end

------------------------------------------------------------------------------
-- Conditional R9 softkey
function MAS_Mfd2_Flight_R9Softkey(cameraId, panel5Mode)
	
	if panel5Mode == 1 then
		local modeCount = fc.GetCameraModeCount(cameraId)
		
		if modeCount > 1 then
			local activeMode = fc.GetCameraMode(cameraId)
			
			if activeMode < (modeCount - 1) then
				fc.SetCameraMode(cameraId, activeMode + 1)
			else
				fc.SetCameraMode(cameraId, 0)
			end
		end
	end

end

------------------------------------------------------------------------------
-- Conditional R10 softkey
function MAS_Mfd2_Flight_R10Softkey(propId, panel5Mode)
	
	if panel5Mode == 1 then
		fc.AddPersistentWrapped(propId .. "-CameraSelect", 1, 0, fc.CameraCount())
	end

end

------------------------------------------------------------------------------
-- Conditional HOME softkey
function MAS_Mfd2_Flight_HomeSoftkey(propId, panel6Mode)
	
	if panel6Mode == 1 then
		nav.SetWaypoint(fc.AddPersistentWrapped(propId .. "-NavWaypoint", 1, 0, nav.WaypointCount()))
	end

end


------------------------------------------------------------------------------
-- Flight Instrumentation page configuration
function MAS_Mfd2_Flight_Select_Instrument(propId, panel, direction)
	-- panel is a number from 0 to 6 (inclusive) that tells me which panel is
	-- selected by the player.  If it's 0, I ignore this click.  Otherwise, I
	-- update the persistent that is tied to the selected panel.
	if panel == 1 then
		fc.AddPersistentWrapped(propId .. "-FlightPanel1", direction, 0, 4)
	elseif panel == 2 then
		fc.AddPersistentWrapped(propId .. "-FlightPanel2", direction, 0, 2)
	elseif panel == 3 then
		fc.AddPersistentWrapped(propId .. "-FlightPanel3", direction, 0, 2)
	elseif panel == 4 then
		fc.AddPersistentWrapped(propId .. "-FlightPanel4", direction, 0, 2)
	elseif panel == 5 then
		fc.AddPersistentWrapped(propId .. "-FlightPanel5", direction, 0, 3)
	elseif panel == 6 then
		fc.AddPersistentWrapped(propId .. "-FlightPanel6", direction, 0, 2)
	end
end

------------------------------------------------------------------------------
-- System Menus
function MAS_Mfd2_System_Menu_Select(propId, activeRow)
	if activeRow == 0 then
		fc.SetPersistent(propId, "MAS_MFD2_ActionGroup")
	end
end
