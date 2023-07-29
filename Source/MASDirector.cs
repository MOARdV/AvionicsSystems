//#define DEBUG_REGISTERS
//#define ATMOSPHERE_AUTOPILOT
/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2023 TommyAtkins
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
using UnityEngine;

namespace AtmosphereAutopilot
{
    public sealed class MASDirector : StateController
    {
        internal MASDirector(Vessel v)
            : base(v, "MAS Director", 88431221)
        { }

        DirectorController dir_c;
        ProgradeThrustController thrust_c;

        public override void InitializeDependencies(Dictionary<Type, AutopilotModule> modules)
        {
            //imodel = modules[typeof(FlightModel)] as FlightModel;
            dir_c = modules[typeof(DirectorController)] as DirectorController;
            thrust_c = modules[typeof(ProgradeThrustController)] as ProgradeThrustController;
        }

        protected override void OnActivate()
        {
            dir_c.Activate();
            thrust_c.Activate();
            MessageManager.post_status_message("MAS Director enabled");
            target_direction = Vector3.zero;
        }

        protected override void OnDeactivate()
        {
            dir_c.Deactivate();
            thrust_c.Deactivate();
            MessageManager.post_status_message("MAS Director disabled");
        }

        public override void ApplyControl(FlightCtrlState cntrl)
        {
            if (vessel.LandedOrSplashed() || target_direction.sqrMagnitude < 0.9)
                return;

            //
            // follow programmed direction
            //
            dir_c.ApplyControl(cntrl, target_direction, Vector3d.zero);

            if (thrust_c.spd_control_enabled)
                thrust_c.ApplyControl(cntrl, thrust_c.setpoint.mps());
        }

        public Vector3 target_direction;

        protected override void _drawGUI(int id)
        {
        }
    }
}