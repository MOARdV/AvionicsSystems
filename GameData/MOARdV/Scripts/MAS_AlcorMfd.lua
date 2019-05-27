-- MAS_AlcorMfd.lua
--
-- MOARdV's Avionics Systems
-- Lua scripts for ALCOR Multi-Function Displays
--
-- Author: MOARdV
--
-- This script is public domain (although acknowledgement that MOARdV wrote it would be nice).

-- What does this do?
--
-- For the sake of illustration, button A on the MFD can toggle between LAUNCH and LAND
-- pages when the player presses either NEXT or PREV.  For this to work, we must
-- initialize a couple of persistent variables.  One of them is used to automatically
-- update the top-row button legend.  The other is used to control which page
--  button A displays.
--
-- When the player changes pages using NEXT or PREV, we need to do three things: we
-- need to update the currently displayed page, we need to remember which page button
-- A should display in the future, and we need to update what the caption displays.
--
-- MAS_AlcorMfdInit is the initialization script executed at load time.  If the 
-- persistent %AUTOID%-Acapt has not been created, we set it to LAUNCH.  This
-- persistent is used to label the first button on the top of the MFD.  We also
-- set %AUTOID%-A to the launch page, so that when button A is pressed, we'll
-- know what page to show.
--
-- MAS_AlcorMfdPageA takes care of the complex updates required to support button A
-- being able to select more than one page.  It changes the page.  It also remembers
-- which page it's selecting, so that the next time the player presses button A,
-- we will select the correct page.  We also update the caption to the new caption.

-- Initialization script for the ALCOR MFD.  This script may set up multiple variables if needed.
function MAS_AlcorMfdInit(propId)

	fc.InitializePersistent(propId .. "-A", "MAS_ALCOR_MFD_A_Launch")
	fc.InitializePersistent(propId .. "-Acapt", "LAUNCH")

end

-- Toggle button A mode.
function MAS_AlcorMfdPageA(propId, pageName, caption)
	-- Change page.
	fc.SetPersistent(propId, pageName)
	-- Remember what button A will do
	fc.SetPersistent(propId .. "-A", pageName)
	-- Update the caption for button A
	fc.SetPersistent(propId .. "-Acapt", caption)
end
