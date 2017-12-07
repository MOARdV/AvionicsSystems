-- MAS_ALCOR_MFD2.lua
--
-- MOARdV's Avionics Systems
-- Lua scripts for ALCOR Multi-Function Displays
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

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
end

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

local timeMode = 0
local periodCounter = 0

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
