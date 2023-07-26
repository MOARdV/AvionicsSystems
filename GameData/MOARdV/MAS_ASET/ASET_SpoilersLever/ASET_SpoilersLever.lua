function Toggle_Spoilers()
	if far.GetSpoilerSetting() == 0 then
		far.SetSpoilers(true)
		return 1
	else
		far.SetSpoilers(false)
		return 0
	end
		
end