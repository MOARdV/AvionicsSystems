-- MAS_ASET_Props.lua
--
-- MOARdV's Avionics Systems
-- One-off Lua functions for MAS ASET props
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

-- Function to control both RCS rotation and RCS translation from a single prop.
-- parameter 'mode': If 0, activate both modes.  If negative, set to translate only.
-- If positive, set to rotate only.
function RCSModeSelect(mode)

	if mode < 0 then
	
		fc.SetRCSRotate(false)
		fc.SetRCSTranslate(true)
		
	elseif mode > 0 then
	
		fc.SetRCSRotate(true)
		fc.SetRCSTranslate(false)
		
	else
	
		fc.SetRCSRotate(true)
		fc.SetRCSTranslate(true)
		
	end
	
	return 1
end
