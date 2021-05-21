function MAS_ASET_COMM_Radio_Init()

	fc.InitializePersistent("ASET_COMM_RADIO_TURN_ON", 0)
	fc.TogglePersistent("ASET_COMM_RADIO_TURN_ON")
	fc.SetPersistent("ASET_COMM_RADIO_PTT_PRESS", 0)
	fc.SetPersistent("ASET_COMM_RADIO_STBY_FREQ", 121)
	fc.SetPersistent("ASET_COMM_RADIO_ACTIVE_FREQ", 121)
	fc.SetPersistent("ASET_COMM_RADIO_KKSC_TWR_ENABLED", 0)
	fc.SetPersistent("ASET_COMM_RADIO_KKSC_APPR_ENABLED", 0)
	fc.SetPersistent("ASET_COMM_RADIO_ATIS_ENABLED", 0)
	fc.SetPersistent("ASET_COMM_RADIO_RECOVERYTEAM_ENABLED", 0)
	fc.SetPersistent("ASET_COMM_RADIO_TWR_ONLINE", 0)
	fc.SetPersistent("ASET_COMM_RADIO_APP_ONLINE", 0)
	fc.SetPersistent("ASET_COMM_RADIO_ATC_ONLINE", 0)
	fc.SetPersistent("ASET_COMM_RADIO_ATIS_AVAILABLE", 0)
	fc.SetPersistent("ASET_COMM_RADIO_RECOVERYTEAM_ONLINE", 0)
	
	return 1
end

function MAS_ASET_COMM_Radio_XFER()

	local TempFreq
	TempFreq = fc.GetPersistentAsNumber("ASET_COMM_RADIO_ACTIVE_FREQ")

	fc.SetPersistent("ASET_COMM_RADIO_ACTIVE_FREQ", fc.GetPersistentAsNumber("ASET_COMM_RADIO_STBY_FREQ"))
	fc.SetPersistent("ASET_COMM_RADIO_STBY_FREQ", TempFreq)
	
	MAS_ASET_COMM_Radio_Tuning()
	MAS_ASET_COMM_NAV_Check()
	
	return 1
end

function MAS_ASET_COMM_Radio_Tuning()

	if (fc.GetPersistentAsNumber("ASET_COMM_RADIO_ACTIVE_FREQ") > 121.9 and fc.GetPersistentAsNumber("ASET_COMM_RADIO_ACTIVE_FREQ") < 122.1) or
		 (fc.GetPersistentAsNumber("ASET_COMM_RADIO_ACTIVE_FREQ") > 124.3 and fc.GetPersistentAsNumber("ASET_COMM_RADIO_ACTIVE_FREQ") < 124.5) then
		fc.SetPersistent("ASET_COMM_RADIO_KKSC_TWR_ENABLED", 1)
	else
		fc.SetPersistent("ASET_COMM_RADIO_KKSC_TWR_ENABLED", 0)
	end

	if (fc.GetPersistentAsNumber("ASET_COMM_RADIO_ACTIVE_FREQ") > 127.4 and fc.GetPersistentAsNumber("ASET_COMM_RADIO_ACTIVE_FREQ") < 127.6) or
	   (fc.GetPersistentAsNumber("ASET_COMM_RADIO_ACTIVE_FREQ") > 121.05 and fc.GetPersistentAsNumber("ASET_COMM_RADIO_ACTIVE_FREQ") < 121.25) then
		fc.SetPersistent("ASET_COMM_RADIO_KKSC_APPR_ENABLED", 1)
	else
		fc.SetPersistent("ASET_COMM_RADIO_KKSC_APPR_ENABLED", 0)
	end

	if (fc.GetPersistentAsNumber("ASET_COMM_RADIO_ACTIVE_FREQ") > 131.2 and fc.GetPersistentAsNumber("ASET_COMM_RADIO_ACTIVE_FREQ") < 131.4) or
		 (fc.GetPersistentAsNumber("ASET_COMM_RADIO_ACTIVE_FREQ") > 131.0 and fc.GetPersistentAsNumber("ASET_COMM_RADIO_ACTIVE_FREQ") < 131.2) then
		fc.SetPersistent("ASET_COMM_RADIO_ATIS_ENABLED", 1)
	else
		fc.SetPersistent("ASET_COMM_RADIO_ATIS_ENABLED", 0)
	end

	if fc.GetPersistentAsNumber("ASET_COMM_RADIO_ACTIVE_FREQ") > 131.9 and fc.GetPersistentAsNumber("ASET_COMM_RADIO_ACTIVE_FREQ") < 132.1 then
		fc.SetPersistent("ASET_COMM_RADIO_RECOVERYTEAM_ENABLED", 1)
	else
		fc.SetPersistent("ASET_COMM_RADIO_RECOVERYTEAM_ENABLED", 0)
	end

	return 1
end

function MAS_ASET_COMM_NAV_Check()

	if fc.GetPersistent("ASET_COMM_RADIO_KKSC_TWR_ENABLED") == 1 and fc.GetPersistent("ASET_COMM_RADIO_TURN_ON") == 1 and fc.BodyName(fc.CurrentBodyIndex()) == "Kerbin" then
		fc.SetPersistent("ASET_COMM_RADIO_TWR_ONLINE", 1)
	else
		fc.SetPersistent("ASET_COMM_RADIO_TWR_ONLINE", 0)
	end

	if fc.GetPersistent("ASET_COMM_RADIO_KKSC_APPR_ENABLED") == 1 and fc.GetPersistent("ASET_COMM_RADIO_TURN_ON") == 1 and fc.BodyName(fc.CurrentBodyIndex()) == "Kerbin" then
		fc.SetPersistent("ASET_COMM_RADIO_APP_ONLINE", 1)
	else
		fc.SetPersistent("ASET_COMM_RADIO_APP_ONLINE", 0)
	end

	if fc.GetPersistent("ASET_COMM_RADIO_TWR_ONLINE") == 1 or fc.GetPersistent("ASET_COMM_RADIO_APP_ONLINE") == 1 then
		fc.SetPersistent("ASET_COMM_RADIO_ATC_ONLINE", 1)
	else
		fc.SetPersistent("ASET_COMM_RADIO_ATC_ONLINE", 0)
	end

	if fc.GetPersistent("ASET_COMM_RADIO_ATIS_ENABLED") == 1 and fc.GetPersistent("ASET_COMM_RADIO_TURN_ON") == 1 and fc.BodyName(fc.CurrentBodyIndex()) == "Kerbin" then
		fc.SetPersistent("ASET_COMM_RADIO_ATIS_AVAILABLE", 1)
	else
		fc.SetPersistent("ASET_COMM_RADIO_ATIS_AVAILABLE", 0)
	end

	if fc.GetPersistent("ASET_COMM_RADIO_RECOVERYTEAM_ENABLED") == 1 and fc.GetPersistent("ASET_COMM_RADIO_TURN_ON") == 1 and fc.BodyName(fc.CurrentBodyIndex()) == "Kerbin" then
		fc.SetPersistent("ASET_COMM_RADIO_RECOVERYTEAM_ONLINE", 1)
	else
		fc.SetPersistent("ASET_COMM_RADIO_RECOVERYTEAM_ONLINE", 0)
	end
	
	return 1
end

function MAS_ASET_COMM_PTT_In()

	fc.SetPersistent("ASET_COMM_RADIO_PTT_PRESS", 1)
	if fc.GetPersistent("ASET_COMM_RADIO_RECOVERYTEAM_ONLINE") == 1 then
		fc.RecoverVessel()
	end
	if fc.GetPersistent("ASET_COMM_RADIO_TWR_ONLINE") == 1 then
		local value = math.random (1, 3)
		local FileString = "ASET/ASET_Avionics/ModernPack/Sounds/PTT_Chat0"..value
		fc.PlayAudio(FileString, fc.GetPersistentAsNumber("ASET_COMM_RADIO_VOLUME"), false)
	end
	return 1
end

function MAS_ASET_COMM_PTT_Out()
	
	fc.SetPersistent("ASET_COMM_RADIO_PTT_PRESS", 0)
	return 1
end

function MAS_ASET_COMM_Select_ATC_Sound()

	local value = math.random (1, 20)
	local FileString = "ASET/ASET_Avionics/ModernPack/Sounds/ATC_0"..string.format("%02d", value)
	fc.PlayAudio(FileString, fc.GetPersistentAsNumber("ASET_COMM_RADIO_VOLUME"), false)
	
	return 1
end