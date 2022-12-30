function MAS_APEx_NAV_Radio_Init(radioId)

	nav.SetRadioFrequency(radioId, 0)
	if radioId == 1 then
	  fc.SetPersistent("GLOBAL_RADIO_1_STBY_FREQ", 120)
	  fc.TogglePersistent("GLOBAL_RADIO_1_TURN_ON")
  else
	  fc.SetPersistent("GLOBAL_RADIO_2_STBY_FREQ", 120)
	  fc.TogglePersistent("GLOBAL_RADIO_2_TURN_ON")
	end
	
	return 1
end

function MAS_APEx_NAV_Radio_XFER_Button(radioId, autoId, freq)
	local tempFreq = nav.GetRadioFrequency(radioId)
	local string1 = autoId.."_XFER_ON"
	
	fc.SetPersistent(string1, 1)
	nav.SetRadioFrequency(radioId, freq)

	if tempFreq == 0 then
		if radioId == 1 then
		  fc.SetPersistent("GLOBAL_RADIO_1_STBY_FREQ", 120)
		else
		  fc.SetPersistent("GLOBAL_RADIO_2_STBY_FREQ", 120)
		end
	else
		if radioId == 1 then
		  fc.SetPersistent("GLOBAL_RADIO_1_STBY_FREQ", tempFreq)
		else
		  fc.SetPersistent("GLOBAL_RADIO_2_STBY_FREQ", tempFreq)
		end
	end
	
	return 1
end