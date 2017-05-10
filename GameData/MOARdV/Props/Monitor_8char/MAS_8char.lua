-- Return either UT (mode 1) or MET (mode 2).  Or 0 if the unit is off.
function MAS_BChK_Time()
	if fc.GetPersistentAsNumber("MAS_BChK_On") < 1 then
		return 0
	elseif fc.GetPersistentAsNumber("MAS_BChK_ClockMode") > 0 then
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

-- Return KAC time (if present), or StopWatch time (if active)
function MAS_BChK_Stopwatch()
	if fc.GetPersistentAsNumber("MAS_BChK_On") < 1 then
		fc.SetPersistent("MAS_BChK_StopwatchMode", 0)
		return 0
	else
		local alarmTime = kac.TimeToAlarm()
		if alarmTime > 0 then
			return alarmTime
		else
			local stopwatchMode = fc.GetPersistentAsNumber("MAS_BChK_StopwatchMode")
			if stopwatchMode == 0 then
				return 0
			elseif stopwatchMode == 1 then
				return math.min(fc.UT() - fc.GetPersistentAsNumber("MAS_BChK_StopwatchTime"), 3599999)
			else
				return fc.GetPersistentAsNumber("MAS_BChK_StopwatchTime")
			end
		end
	end
end

function MAS_BChK_NextMode(autoId)
	-- We use this to give us a way to animate a push button.
	fc.TogglePersistent(autoId)
	local newMode = fc.AddPersistentWrapped("MAS_BChK_StopwatchMode", 1, 0, 3)
	
	if newMode == 1 then
		fc.SetPersistent("MAS_BChK_StopwatchTime", fc.UT())
	elseif newMode == 2 then
		local startTime = fc.GetPersistentAsNumber("MAS_BChK_StopwatchTime")
		fc.SetPersistent("MAS_BChK_StopwatchTime", math.min(fc.UT() - startTime, 3599999))
	end
end
