function MAS_DME_Init(autoID)
	local string1 = autoID.."_DME_SOURCE_SELECTOR"
	fc.SetPersistent(string1, -1)
	fc.SetPersistent("DME_1_HSI_SOURCE_SELECTOR", 0)
	fc.SetPersistent("DME_NAV_Source", 1)
	
	return 1
end

function MAS_DME_Source_Selector(autoID)
	local string1 = autoID.."_DME_SOURCE_SELECTOR"
	if fc.GetPersistentAsNumber(string1) == 1 then
		fc.SetPersistent(string1, -1)
	else
		fc.SetPersistent(string1, fc.GetPersistentAsNumber(string1) + 1)
	end

	if fc.GetPersistentAsNumber(string1) == -1 then
		fc.SetPersistent("DME_NAV_Source", 1)
	elseif fc.GetPersistentAsNumber(string1) == 0 then
		fc.SetPersistent("DME_NAV_Source", 2)
	elseif fc.GetPersistentAsNumber(string1) == 1 then
		fc.SetPersistent("DME_NAV_Source", 1)
	end
	return 1
end