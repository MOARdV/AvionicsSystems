function MAS_NAV_Radio_Init(radioId, autoId)
	local string1 = autoId.."_STBY_FREQ"
	local string2 = autoId.."_TURN_ON"
	
	nav.SetRadioFrequency(radioId, 0)
	fc.SetPersistent(string1, 120)
	fc.TogglePersistent(string2)
	
	return 1
end

function MAS_NAV_Radio_XFER_Button(radioId, autoId, freq)
	local tempFreq = nav.GetRadioFrequency(radioId)
	local string1 = autoId.."_STBY_FREQ"
	local string2 = autoId.."_XFER_ON"
	
	fc.SetPersistent(string2, 1)
	nav.SetRadioFrequency(radioId, freq)
	if tempFreq == 0 then
		fc.SetPersistent(string1, 120)
	else
		fc.SetPersistent(string1, tempFreq)
	end
	
	return 1
end