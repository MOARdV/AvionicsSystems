/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2017 MOARdV
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to
 * deal in the Software without restriction, including without limitation the
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
 * sell copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 * 
 ****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace AvionicsSystems
{
    /// <summary>
    /// The MASContextMenu provides a way to access MAS functionality from an external location.
    /// It adds a context menu entry with configurable text and actions that are all tied in
    /// to the MASFlightComputer system.
    /// 
    /// Note that a MASFlightComputer *must* be installed on the same part to enable this ability.
    /// </summary>
    class MASContextMenu : PartModule
    {
        [KSPField]
        public string activateAction = string.Empty;
        
        [KSPField]
        public string deactivateAction = string.Empty;

        [KSPField]
        public string passiveText = "Activate";

        [KSPField]
        public string activeText = "Deactivate";

        [KSPField]
        public string variable = string.Empty;

        [UI_Toggle(disabledText = "Activate", enabledText = "Deactivate")]
        [KSPField(guiActive = true, guiActiveEditor = false, isPersistant = true)]
        public bool actionState = false;

        public bool lastState;
        
        MASFlightComputer comp = null;

        public void Start()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                Utility.LogMessage(this, "Start()");
                lastState = actionState;
            }
        }

        public void FixedUpdate()
        {
            if (comp != null)
            {
                if (lastState != actionState)
                {

                }
            }
        }

        public override string GetInfo()
        {
            return "MASContextMenu doesn't work yet.";
        }
        
        public void OnDestroy()
        {
            if (comp != null)
            {
                Utility.LogMessage(this, "OnDestroy()");
            }
        }
    }
}
