-- MAS_Clock.lua
--
-- MOARdV's Avionics Systems
-- Lua scripts for an onboard clock display similar to a Soyuz TM BChK (БЧК)
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

-- Return either UT (mode 1) or MET (mode 2).  Or 0 if the unit is off.
function MAS_Clock_Time()
	if fc.GetPersistentAsNumber("MAS_Clock_On") < 1 then
		return 0
	elseif fc.GetPersistentAsNumber("MAS_Clock_ClockMode") > 0 then
		-- MET
		local met = fc.MET()
		-- Clamp MET to display limits
		met = math.min(met, 3599999)
		return met
	else
		-- Only keep HH/MM/SS of UT
		return fc.TimeOfDay(fc.UT())
	end
end

-- Return either UT (mode 1) or MET (mode 2).  Or 0 if the unit is off.
function MAS_Clock_Time_Long()
	if fc.GetPersistentAsNumber("MAS_Clock_On") < 1 then
		return 0
	elseif fc.GetPersistentAsNumber("MAS_Clock_ClockMode") > 0 then
		-- return MET
		return fc.MET()
	else
		-- return UT  Add a day because we're using the MET formatter, not KDT.
		return fc.UT() + 3600 * fc.HoursPerDay()
	end
end

-- Return KAC time (if present), or StopWatch time (if active)
function MAS_Clock_Stopwatch()
	if fc.GetPersistentAsNumber("MAS_Clock_On") < 1 then
		fc.SetPersistent("MAS_Clock_StopwatchMode", 0)
		return 0
	else
		local alarmTime = kac.TimeToAlarm()
		if alarmTime > 0 then
			return alarmTime
		else
			local stopwatchMode = fc.GetPersistentAsNumber("MAS_Clock_StopwatchMode")
			if stopwatchMode == 0 then
				return 0
			elseif stopwatchMode == 1 then
				return math.min(fc.UT() - fc.GetPersistentAsNumber("MAS_Clock_StopwatchTime"), 3599999)
			else
				return fc.GetPersistentAsNumber("MAS_Clock_StopwatchTime")
			end
		end
	end
end

-- Return KAC time (if present), or StopWatch time (if active)
function MAS_Clock_Stopwatch_Long()
	if fc.GetPersistentAsNumber("MAS_Clock_On") < 1 then
		fc.SetPersistent("MAS_Clock_StopwatchMode", 0)
		return 0
	else
		local alarmTime = kac.TimeToAlarm()
		if alarmTime > 0 then
			return alarmTime
		else
			local stopwatchMode = fc.GetPersistentAsNumber("MAS_Clock_StopwatchMode")
			if stopwatchMode == 0 then
				return 0
			elseif stopwatchMode == 1 then
				return (fc.UT() - fc.GetPersistentAsNumber("MAS_Clock_StopwatchTime"))
			else
				return fc.GetPersistentAsNumber("MAS_Clock_StopwatchTime")
			end
		end
	end
end

function MAS_Clock_NextMode(autoId)
	-- We use this to give us a way to animate a push button.
	fc.TogglePersistent(autoId)
	local newMode = fc.AddPersistentWrapped("MAS_Clock_StopwatchMode", 1, 0, 3)

	if newMode == 1 then
		fc.SetPersistent("MAS_Clock_StopwatchTime", fc.UT())
	elseif newMode == 2 then
		local startTime = fc.GetPersistentAsNumber("MAS_Clock_StopwatchTime")
		fc.SetPersistent("MAS_Clock_StopwatchTime", math.min(fc.UT() - startTime, 3599999))
	end
end

function MAS_Clock_Apsis()
	if fc.GetPersistentAsNumber("MAS_Clock_ApsisMode") > 0 then
		return fc.TimeToAp()
	else
		return fc.TimeToPe()
	end
end

function MAS_Clock_SetAlarm()
	if fc.GetPersistentAsNumber("MAS_Clock_On") < 1 then
		return 0
	end
	
	local alarmTime = fc.GetPersistentAsNumber("MAS_Clock_TimerMins") * 60 + fc.GetPersistentAsNumber("MAS_Clock_TimerSecs")
	
	if alarmTime > 0 and kac.Available() > 0 then
		alarmTime = alarmTime + fc.UT()
		kac.DeleteAlarm(fc.GetPersistent("MAS_Clock_AlarmId"))
		local newAlarmId = kac.CreateAlarm("MAS User Alarm", alarmTime)
		fc.SetPersistent("MAS_Clock_AlarmId", newAlarmId)
		
		return 1
	end
	
	return 0
end
