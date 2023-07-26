function MAS_APEx_NAV_Display(digit, autoId)

	fc.TogglePersistent(autoId .. "_Button_" .. digit)
	fc.AppendPersistentDigit(autoId .. "_NumberBuffer", digit, 5)

end

function MAS_APEx_NAV_XFER(radioId, freq, autoId)

	if fc.GetPersistentAsNumber(autoId .. "_NumberBuffer") > 9999 then
	  if radioId == 1 then
	    fc.TogglePersistent(autoId .. "_Button_XFER_Left")
	    fc.SetPersistent(autoId .. "_NumberBuffer", nav.GetRadioFrequency(1) * 100)
    else
      fc.TogglePersistent(autoId .. "_Button_XFER_Right")
      fc.SetPersistent(autoId .. "_NumberBuffer", nav.GetRadioFrequency(2) * 100)
    end

    nav.SetRadioFrequency(radioId, freq * 0.01)
  else
    if radioId == 1 then
      fc.TogglePersistent(autoId .. "_Button_XFER_Left_Error")
    else
      fc.TogglePersistent(autoId .. "_Button_XFER_Right_Error")
    end
  end
end