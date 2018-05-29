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
        private Action onActivate;

        [KSPField]
        public string deactivateAction = string.Empty;
        private Action onDeactivate;

        [KSPField]
        public string activateText = "Activate";

        [KSPField]
        public string deactivateText = "Deactivate";

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "MAS: ")]
        public string menuName = "MASContextMenu";

        [UI_Toggle(disabledText = "Do Activate", enabledText = "Do Deactivate")]
        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "MAS: ", isPersistant = true)]
        public bool actionState = false;

        private bool lastState;
        private bool initialized = false;

        MASFlightComputer comp = null;

        public void Start()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                lastState = actionState;

                comp = part.FindModuleImplementing<MASFlightComputer>();

                menuName = (lastState) ? deactivateText : activateText;
            }
        }

        public void FixedUpdate()
        {
            if (comp != null)
            {
                // This module could be called before MASFlightComputer is initialized,
                // so we need to wait for comp.isInitialized before creating our actions.
                // This does create a race condition, but I'd be surprised if someone could
                // get the menu open and clicked within about 1 FixedUpdate.
                if (!initialized && comp.initialized)
                {
                    if (!string.IsNullOrEmpty(activateAction))
                    {
                        onActivate = comp.GetAction(activateAction, null);

                        if (onActivate == null)
                        {
                            Utility.LogError(this, "Failed to initialize action '{1}' for {0}", menuName, activateAction);
                        }
                    }
                    if (!string.IsNullOrEmpty(deactivateAction))
                    {
                        onDeactivate = comp.GetAction(deactivateAction, null);

                        if (onDeactivate == null)
                        {
                            Utility.LogError(this, "Failed to initialize action '{1}' for {0}", menuName, deactivateAction);
                        }
                    }
                    initialized = true;
                }

                if (lastState != actionState)
                {
                    if (actionState && onActivate != null)
                    {
                        onActivate();
                    }
                    else if (onDeactivate != null)
                    {
                        onDeactivate();
                    }

                    lastState = actionState;

                    menuName = (lastState) ? deactivateText : activateText;
                }
            }
        }

        public override string GetInfo()
        {
            return "Supports MASContextMenu actions";
        }

        public void OnDestroy()
        {
            // Nothing to do?
        }

        [KSPAction("Deactivate MAS Action")]
        public void MASOffAction(KSPActionParam param)
        {
            actionState = false;
        }

        [KSPAction("Activate MAS Action")]
        public void MASOnAction(KSPActionParam param)
        {
            actionState = true;
        }

        [KSPAction("Toggle MAS Action")]
        public void ToggleMASAction(KSPActionParam param)
        {
            actionState = !actionState;
        }
    }
}
