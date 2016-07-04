/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016 MOARdV
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
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Text;

namespace AvionicsSystems
{
    public class MASProxyAttribute : System.Attribute
    {
        private bool immutable;
        private bool uncacheable;

        public bool Immutable
        {
            get
            {
                return immutable;
            }
            set
            {
                immutable = value;
            }
        }

        public bool Uncacheable
        {
            get
            {
                return uncacheable;
            }
            set
            {
                uncacheable = value;
            }
        }
    }

    /// <summary>
    /// The flight computer proxy provides the interface between the flight
    /// computer module and the Lua environment.  It is a thin wrapper over
    /// the flight computer that prevents in-Lua access to some elements.
    /// 
    /// Note that this class must be stateless - it can not maintain variables
    /// between calls because there is no guarantee it'll exist next time it's
    /// called.
    /// 
    /// Also note that, while it is a wrapper for ASFlightComputer, not all
    /// values are plumbed through to the flight computer (for instance, the
    /// action group control and state are all handled in this class).
    /// </summary>
    internal class MASFlightComputerProxy
    {
        private MASFlightComputer fc;
        internal MASVesselComputer vc;

        [MoonSharpHidden]
        public MASFlightComputerProxy(MASFlightComputer fc)
        {
            this.fc = fc;
        }

        ~MASFlightComputerProxy()
        {
            fc = null;
        }

        #region Action Groups
        private static readonly KSPActionGroup[] ags = { KSPActionGroup.Custom10, KSPActionGroup.Custom01, KSPActionGroup.Custom02, KSPActionGroup.Custom03, KSPActionGroup.Custom04, KSPActionGroup.Custom05, KSPActionGroup.Custom06, KSPActionGroup.Custom07, KSPActionGroup.Custom08, KSPActionGroup.Custom09 };
        public double GetActionGroup(double groupID)
        {
            if (groupID < 0.0 || groupID > 9.0)
            {
                return 0.0;
            }
            else
            {
                return (fc.vessel.ActionGroups[ags[(int)groupID]]) ? 1.0 : 0.0;
            }
        }

        public void SetActionGroup(double groupID, bool active)
        {
            if (groupID >= 0.0 && groupID <= 9.0)
            {
                fc.vessel.ActionGroups.SetGroup(ags[(int)groupID], active);
            }
        }

        public void ToggleActionGroup(double groupID)
        {
            if (groupID >= 0.0 && groupID <= 9.0)
            {
                fc.vessel.ActionGroups.ToggleGroup(ags[(int)groupID]);
            }
        }
        #endregion

        #region Brakes
        public double GetBrakes()
        {
            return (fc.vessel.ActionGroups[KSPActionGroup.Brakes]) ? 1.0 : 0.0;
        }

        public void SetBrakes(bool active)
        {
            fc.vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, active);
        }

        public void ToggleBrakes()
        {
            fc.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Brakes);
        }
        #endregion

        #region Gear
        public double GetGear()
        {
            return (fc.vessel.ActionGroups[KSPActionGroup.Gear]) ? 1.0 : 0.0;
        }

        public void SetGear(bool active)
        {
            fc.vessel.ActionGroups.SetGroup(KSPActionGroup.Gear, active);
        }

        public void ToggleGear()
        {
            fc.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Gear);
        }
        #endregion

        #region Persistent Vars
        public object AddPersistent(string persistentName, double amount)
        {
            return fc.AddPersistent(persistentName, amount);
        }

        public object GetPersistent(string persistentName)
        {
            return fc.GetPersistent(persistentName);
        }

        public object SetPersistent(string persistentName, object value)
        {
            return fc.SetPersistent(persistentName, value);
        }

        public object TogglePersistent(string persistentName)
        {
            return fc.TogglePersistent(persistentName);
        }
        #endregion

        #region RCS
        public double GetRCS()
        {
            return (fc.vessel.ActionGroups[KSPActionGroup.RCS]) ? 1.0 : 0.0;
        }

        public void SetRCS(bool active)
        {
            fc.vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, active);
        }

        public void ToggleRCS()
        {
            fc.vessel.ActionGroups.ToggleGroup(KSPActionGroup.RCS);
        }
        #endregion RCS

        #region SAS
        public double GetSAS()
        {
            return (fc.vessel.ActionGroups[KSPActionGroup.SAS]) ? 1.0 : 0.0;
        }

        public void SetSAS(bool active)
        {
            fc.vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, active);
        }

        public void ToggleSAS()
        {
            fc.vessel.ActionGroups.ToggleGroup(KSPActionGroup.SAS);
        }
        #endregion
    }
}
