function MAS_APEx_MFD40x20_Init(propid)

	fc.InitializePersistent(propid .. "_NumberBuffer", 0)
	fc.InitializePersistent(propid .. "_Scale", 1)
	fc.InitializePersistent(propid .. "_GS_Angle", 5)
	fc.InitializePersistent(propid .. "_BRG_Select", 0)
	fc.InitializePersistent(propid .. "_Button_WP_Toggle", -1)
	fc.InitializePersistent(propid .. "_WP_Select", -1)
	fc.InitializePersistent("Global_NumberBuffer_WP_LAT", "")
	fc.InitializePersistent("Global_NumberBuffer_WP_LON", "")
	fc.InitializePersistent("0_Global_LATNAVValue_WP", "-91")
	fc.InitializePersistent("0_Global_LONNAVValue_WP", "-181")
	fc.InitializePersistent("1_Global_LATNAVValue_WP", "-91")
	fc.InitializePersistent("1_Global_LONNAVValue_WP", "-181")
	fc.InitializePersistent("2_Global_LATNAVValue_WP", "-91")
	fc.InitializePersistent("2_Global_LONNAVValue_WP", "-181")
	fc.InitializePersistent("3_Global_LATNAVValue_WP", "-91")
	fc.InitializePersistent("3_Global_LONNAVValue_WP", "-181")
	fc.InitializePersistent("4_Global_LATNAVValue_WP", "-91")
	fc.InitializePersistent("4_Global_LONNAVValue_WP", "-181")
	fc.InitializePersistent("5_Global_LATNAVValue_WP", "-91")
	fc.InitializePersistent("5_Global_LONNAVValue_WP", "-181")
	fc.InitializePersistent("6_Global_LATNAVValue_WP", "-91")
	fc.InitializePersistent("6_Global_LONNAVValue_WP", "-181")
	fc.InitializePersistent("7_Global_LATNAVValue_WP", "-91")
	fc.InitializePersistent("7_Global_LONNAVValue_WP", "-181")
	fc.InitializePersistent("8_Global_LATNAVValue_WP", "-91")
	fc.InitializePersistent("8_Global_LONNAVValue_WP", "-181")
	fc.InitializePersistent("9_Global_LATNAVValue_WP", "-91")
	fc.InitializePersistent("9_Global_LONNAVValue_WP", "-181")
	fc.InitializePersistent(propid .. "-B", "MAS_APEx_MFD40x20_PG_Target")
	fc.InitializePersistent(propid .. "-C", "MAS_APEx_MFD40x20_PG_Nav1")
	fc.InitializePersistent(propid .. "-D", "MAS_APEx_MFD40x20_PG_MechJeb")
	fc.InitializePersistent(propid .. "-E", "MAS_APEx_MFD40x20_PG_Graphs1")
	fc.InitializePersistent(propid .. "-G", "MAS_APEx_MFD40x20_PG_Resources_Stage")
	fc.InitializePersistent(propid .. "-R1", "MAS_APEx_MFD40x20_PG_Flight")
	fc.InitializePersistent(propid .. "-R2", "MAS_APEx_MFD40x20_PG_Orbit")
	fc.InitializePersistent(propid .. "-R3", "MAS_APEx_MFD40x20_PG_Dock")
	fc.InitializePersistent(propid .. "-R4", "MAS_APEx_MFD40x20_PG_NavOptions")
	
	return 1
end

