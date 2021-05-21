function MAS_swRotary_V2(string, drag, sound)
	fc.AddPersistentClamped(string, drag, 0, 1)
	if fc.Conditioned(1) then
		fc.PlayAudio(sound, 0.5, true)
	end

	return 1
end