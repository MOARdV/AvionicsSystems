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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace AvionicsSystems
{
    class MASConfig : GameParameters.CustomParameterNode
    {
        public override string Title { get { return "MOARdV Avionics Systems Options"; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override string Section { get { return "MAS"; } }
        public override int SectionOrder { get { return 1; } }
        public override bool HasPresets { get { return false; } }

        [GameParameters.CustomParameterUI("Verbose Message Logging?", toolTip = "Enable to generate logging for debug purposes")]
        public bool VerboseLogging = true;

        [GameParameters.CustomParameterUI("Power Resource", toolTip = "Resource to use as power")]
        public string ElectricCharge = "ElectricCharge";

        [GameParameters.CustomIntParameterUI("Lua Update Priority", toolTip="Larger numbers generate garbage slower, but will feel less responsive", minValue = 1, maxValue=4, stepSize=1)]
        public int LuaUpdateDenominator = 1;

        [GameParameters.CustomStringParameterUI("Test String UI", autoPersistance = true, lines = 2, title = "Radio Navigation Settings", toolTip = "Tuning parameters for MAS Radio Navigation")]
        public string UIstring = "";

        [GameParameters.CustomFloatParameterUI("General Signal Propagation", minValue = 0.9f, maxValue = 1.6f, asPercentage = true, toolTip="Controls overall range of MAS Radio Nav signals.  Affects all nav beacons.")]
        public float GeneralPropagation = 1.0f;

        [GameParameters.CustomFloatParameterUI("NDB Signal Propagation", minValue = 0.9f, maxValue = 1.6f, asPercentage = true, toolTip="Controls range of NDB signals")]
        public float NDBPropagation = 1.0f;

        [GameParameters.CustomFloatParameterUI("VOR Signal Propagation", minValue = 0.9f, maxValue = 1.6f, asPercentage = true, toolTip="Controls range of VOR signals")]
        public float VORPropagation = 1.2f;

        [GameParameters.CustomFloatParameterUI("DME Signal Propagation", minValue = 0.9f, maxValue = 1.6f, asPercentage = true, toolTip="Controls range of DME signals")]
        public float DMEPropagation = 1.4f;

        public override IList ValidValues(MemberInfo member)
        {
            if (member.Name == "ElectricCharge")
            {
                List<string> myList = new List<string>();
                foreach (var thatResource in PartResourceLibrary.Instance.resourceDefinitions)
                {
                    myList.Add(thatResource.name);
                }
                IList myIlist = myList;
                return myIlist;
            }
            else
            {
                return null;
            }
        }
    }
}
