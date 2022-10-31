function MAS_APEx_Science_Options(propId, scienceType)

  fc.SetPersistent(propId .. "_ScienceType", scienceType)
  fc.SetPersistent(propId, "MAS_APEx_MFD40x20_ScienceContainerOptions")
	
	return 1
	
end