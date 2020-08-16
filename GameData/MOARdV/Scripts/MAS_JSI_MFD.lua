-- MAS_JSI_MFD.lua
--
-- MOARdV's Avionics Systems
-- Lua scripts for JSI/RPM Basic Multi-Function Displays
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

-- What does this do?
--
-- This script sets defaults for soft keys that can toggle between multiple pages (the 
-- NavBall/HUD screen being a prime example), so that they'll be able to remember their page.

-- Initialization script for the JSI MFD.  This script may set up multiple variables if needed.
function MAS_JSI_MFD_Init(propId)

	fc.InitializePersistent(propId .. "-A", "MAS_JSI_BasicMFD_A_NavBall")
	fc.InitializePersistent(propId .. "-D", "MAS_JSI_BasicMFD_D_MechJeb")
	fc.InitializePersistent(propId .. "-2", "MAS_JSI_BasicMFD_2_Orbit")

end
