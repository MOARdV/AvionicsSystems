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

-- Function to return a specific electric output rate, depending on the mode.
-- Used by MAS_DigitalIndicator_Elec_Output and MAS_ASET_Elec_Output.
--
-- mode 0: Net output
-- mode 1: Alternator
-- mode 2: Fuel Cell
-- mode 3: Solar
-- mode 4: Generator
function SelectElectricOutput(mode)

	if mode == 1 then
		return fc.AlternatorOutput()
	elseif mode == 2 then
		return fc.FuelCellOutput()
	elseif mode == 3 then
		return fc.SolarPanelOutput()
	elseif mode == 4 then
		return fc.GeneratorOutput()
	else
		return fc.AlternatorOutput() + fc.FuelCellOutput() + fc.SolarPanelOutput() + fc.GeneratorOutput()
	end
end
