function RMI_2_Needle_Select_Heading()
	if fc.GetPersistentAsNumber("ASET_RMI2N_1_HDG_MODE") == 0 then
		return 0
	else
		return fc.Heading()
	
	end
end

function RMI_2_Needle_Select_Yellow_Green(radioID)
	if fc.GetPersistentAsNumber("ASET_RMI2N_1_HDG_MODE") == 0 then
		return nav.GetNavAidBearing(radioID, false)
	else
		return nav.GetNavAidBearing(radioID, true)
	
	end
end
