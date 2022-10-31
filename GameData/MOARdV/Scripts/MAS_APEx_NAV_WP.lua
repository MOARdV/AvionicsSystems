function MAS_APEx_NAV_WP_INPUT(digit, autoId)

  fc.TogglePersistent(autoId .. "_Button_" .. digit)
	
	if fc.GetPersistentAsNumber(autoId .. "_Button_ModeToggle") == 0 then
    if digit == 10 then
      fc.SetPersistent("Global_NumberBuffer_WP_LAT", -1 * fc.GetPersistentAsNumber("Global_NumberBuffer_WP_LAT"))
      fc.AppendPersistent("Global_NumberBuffer_WP_LAT", 0, 30)
      MAS_APEx_NAV_WP_CLR(autoId)
    elseif digit == 11 then
      fc.AppendPersistent("Global_NumberBuffer_WP_LAT", ".", 30)
    else
      fc.AppendPersistent("Global_NumberBuffer_WP_LAT", digit, 30)
    end
  else
    if digit == 10 then
      fc.SetPersistent("Global_NumberBuffer_WP_LON", -1 * fc.GetPersistentAsNumber("Global_NumberBuffer_WP_LON"))
    elseif digit == 11 then
      fc.AppendPersistent("Global_NumberBuffer_WP_LON", ".", 30)
    else
      fc.AppendPersistent("Global_NumberBuffer_WP_LON", digit, 30)
    end
  end
end

function MAS_APEx_NAV_WP_CLR(autoId)
  
  fc.TogglePersistent(autoId .. "_Button_CLR")
  
  if fc.GetPersistentAsNumber(autoId .. "_Button_ModeToggle") == 0 then
    string = tostring(fc.GetPersistent("Global_NumberBuffer_WP_LAT"))
    if string:len() > 0 then
      string = string:sub(1, -2)
      fc.SetPersistent("Global_NumberBuffer_WP_LAT", string)
    end
  else
    string = tostring(fc.GetPersistent("Global_NumberBuffer_WP_LON"))
    if string:len() > 0 then
      string = string:sub(1, -2)
      fc.SetPersistent("Global_NumberBuffer_WP_LON", string)
    end
  end
end

function MAS_APEx_NAV_WP_FULL_CLR(autoId)
  
  if fc.GetPersistentAsNumber(autoId .. "_Button_ModeToggle") == 0 then
    fc.SetPersistent("Global_NumberBuffer_WP_LAT", "")
  else
    fc.SetPersistent("Global_NumberBuffer_WP_LON", "")
  end
end

function MAS_APEx_NAV_WP_OK(autoId, WPIndex)
  
  local stringLAT = WPIndex .. "_Global_LATNAVValue_WP"
  local stringLON = WPIndex .. "_Global_LONNAVValue_WP"
  local stringSet = autoId .. "_" .. WPIndex .. "_Button_WP_Set"
  local valueLAT = fc.GetPersistentAsNumber("Global_NumberBuffer_WP_LAT")
  local valueLON = fc.GetPersistentAsNumber("Global_NumberBuffer_WP_LON")
  
	if fc.GetPersistentAsNumber("Global_NumberBuffer_WP_LON") < 180 and fc.GetPersistentAsNumber("Global_NumberBuffer_WP_LON") > -180 and fc.GetPersistentAsNumber("Global_NumberBuffer_WP_LAT") < 90 and fc.GetPersistentAsNumber("Global_NumberBuffer_WP_LAT") > -90 and fc.GetPersistentAsNumber(autoId .. "_Button_WP_Toggle") > -1 then
    fc.SetPersistent(stringLAT, valueLAT)
    fc.SetPersistent(stringLON, valueLON)
    fc.SetPersistent("Global_NumberBuffer_WP_LAT", "")
    fc.SetPersistent("Global_NumberBuffer_WP_LON", "")
    fc.SetPersistent(stringSet, 1)
    fc.TogglePersistent(autoId .. "_Button_OK")
  else
    fc.TogglePersistent(autoId .. "_Button_OK_Error")
  end
end

function MAS_APEx_NAV_WP_DEL(autoId, WPIndex)

	local stringSet = autoId .. "_" .. WPIndex .. "_Button_WP_Set"
	
	if fc.GetPersistentAsNumber(autoId .. "_Button_WP_Toggle") > -1 then
    fc.SetPersistent(WPIndex .. "_Global_LATNAVValue_WP", -91)
    fc.SetPersistent(WPIndex .. "_Global_LONNAVValue_WP", -181)
    fc.SetPersistent(stringSet, 0)
  end
end

function MAS_APEx_NAV_WP_DATA_SEL(WPIndex, LatLon)

  if WPIndex == -1 then
    return -999
  end

  if LatLon == 0 then
    return fc.GetPersistentAsNumber(WPIndex .. "_Global_LATNAVValue_WP")
  else
    return fc.GetPersistentAsNumber(WPIndex .. "_Global_LONNAVValue_WP")
  end
end

function MAS_APEx_NAV_WP_SET(X, Y, autoId)

  local stringLon = fc.Remap(X, 0, 640, -14.68961413 * fc.GetPersistentAsNumber(autoId .. "_Scale"), 14.68961413 * fc.GetPersistentAsNumber(autoId .. "_Scale"))
  local stringLat = fc.Remap(Y, 640, 0, -11.70634050 * fc.GetPersistentAsNumber(autoId .. "_Scale"), 17.20097380 * fc.GetPersistentAsNumber(autoId .. "_Scale"))
  
  fc.SetPersistent("Global_NumberBuffer_WP_LAT", fc.Latitude() + stringLat)
  fc.SetPersistent("Global_NumberBuffer_WP_LON", fc.Longitude() + stringLon)

end

function MAS_APEx_NAV_WP_POLY(autoId, WPIndex, offsetValue)

  local stringLon = WPIndex .. "_Global_LONNAVValue_WP"
  local stringLat = WPIndex .. "_Global_LATNAVValue_WP"
  
  local convertedValue = fc.InverseLerp(nav.GroundDistanceFromVessel(fc.GetPersistentAsNumber(stringLat), fc.GetPersistentAsNumber(stringLon)), 0, fc.GetPersistentAsNumber(autoId .. "_Scale") * 125500) * -265
  return offsetValue + convertedValue
  
end

function MAS_APEx_NAV_WP_POLY_EDIT(amount)

  if fc.GetPersistentAsNumber("Global_Poly_Select") == 0 then
    fc.AddPersistent("Global_Poly1_X", amount)
  elseif fc.GetPersistentAsNumber("Global_Poly_Select") == 1 then
    fc.AddPersistent("Global_Poly2_X", amount)
  elseif fc.GetPersistentAsNumber("Global_Poly_Select") == 2 then
    fc.AddPersistent("Global_Poly3_X", amount)
  elseif fc.GetPersistentAsNumber("Global_Poly_Select") == 3 then
    fc.AddPersistent("Global_Poly4_X", amount)
  elseif fc.GetPersistentAsNumber("Global_Poly_Select") == 4 then
    fc.AddPersistent("Global_Poly1_Y", amount)
  elseif fc.GetPersistentAsNumber("Global_Poly_Select") == 5 then
    fc.AddPersistent("Global_Poly2_Y", amount)
  elseif fc.GetPersistentAsNumber("Global_Poly_Select") == 6 then
    fc.AddPersistent("Global_Poly3_Y", amount)
  elseif fc.GetPersistentAsNumber("Global_Poly_Select") == 7 then
    fc.AddPersistent("Global_Poly4_Y", amount)
  end
end