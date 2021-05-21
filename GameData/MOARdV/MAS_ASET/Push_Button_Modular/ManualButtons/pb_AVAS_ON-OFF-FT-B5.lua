function MAS_AVAS_Call(altitude)

	local SoundClip = "ASET/ASET_Props/Sounds/gpws/gpws"..altitude
	fc.PlayAudio(SoundClip, 1.0, true)
	
	return 1
	
end