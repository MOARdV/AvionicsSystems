function MAS_APEx_HSI_Select_CDI_Source(radioId)
	if nav.GetILSLocalizerValid(radioId) == 1 then
		return fc.Conditioned(nav.GetILSLocalizerError(radioId))
	else
		return fc.Conditioned(nav.GetVORDeviation(radioId), fc.GetPersistentAsNumber("MAS_CRS_INPUT"))
	
	end
end
