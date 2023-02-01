function MAS_APEx_RMI_2_Needle_Select_Heading()
	if fc.GetPersistentAsNumber("ASET_RMI2N_1_HDG_MODE") == 0 then
		return 0
	else
		return fc.Heading()
	
	end
end

function MAS_APEx_RMI_2_Needle_Select_Yellow_Green(radioID)
	if fc.GetPersistentAsNumber("MAS_RMI_Mode") == 0 then
	  if fc.GetPersistentAsNumber("ASET_RMI2N_1_HDG_MODE") == 0 then
  		return nav.GetNavAidBearing(radioID, false)
  	else
		  return nav.GetNavAidBearing(radioID, true)
		end
  else
    if fc.GetPersistentAsNumber("ASET_RMI2N_1_HDG_MODE") == 1 then
      return nav.WaypointBearing(-1)
    else
      return fc.NormalizeAngle(nav.WaypointBearing(-1) - fc.Heading())
	  end
	end
end