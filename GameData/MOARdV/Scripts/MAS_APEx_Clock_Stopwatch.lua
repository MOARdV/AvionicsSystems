function MAS_APEx_Clock_Stopwatch()
	if fc.GetPersistentAsNumber("MAS_Clock_On") < 1 then
		fc.SetPersistent("MAS_Clock_StopwatchMode", 0)
		return 0
	else
		local alarmTime = kac.TimeToAlarm()
		if alarmTime > 0 and fc.GetPersistentAsNumber("MAS_Clock_Select_KAC") > 1 then
			return alarmTime
		else
			local stopwatchMode = fc.GetPersistentAsNumber("MAS_Clock_StopwatchMode")
			if stopwatchMode == 0 then
				return (fc.GetPersistentAsNumber("MAS_Clock_TimerHours") * 3600 + fc.GetPersistentAsNumber("MAS_Clock_TimerMins") * 60 + fc.GetPersistentAsNumber("MAS_Clock_TimerSecs"))
			elseif stopwatchMode == 1 then
				return math.min(fc.UT() - fc.GetPersistentAsNumber("MAS_Clock_StopwatchTime"), 3599999)
			else
				return fc.GetPersistentAsNumber("MAS_Clock_StopwatchTime")
			end
		end
	end
end