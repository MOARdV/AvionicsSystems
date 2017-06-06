/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016-2017 MOARdV
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
using System.Text;

namespace AvionicsSystems
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames | ScenarioCreationOptions.AddToExistingGames, GameScenes.SPACECENTER)]
    class MASConfig : ScenarioModule
    {
        /// <summary>
        /// User-configurable parameters related to radio signal propagation.
        /// </summary>
        public struct Navigation
        {
            /// <summary>
            /// Overall scalar to change general signal propagation.  The small radius of Kerbin makes
            /// values swing wildly on altitude.  Defaults to 1.0.
            /// </summary>
            public float generalPropagation;

            /// <summary>
            /// Propagation scalar for NDB stations.  Defaults to 1.0.
            /// </summary>
            public float NDBPropagation;

            /// <summary>
            /// Propagation scalar of VOR stations.  Defaults to 1.2.
            /// </summary>
            public float VORPropagation;

            /// <summary>
            /// Propagation scalar of DME stations.  Defaults to 1.4.
            /// </summary>
            public float DMEPropagation;
        };

        static internal bool VerboseLogging = true;
        static internal string ElectricCharge = "ElectricCharge";
        static internal int LuaUpdatePriority = 1;

        static internal Navigation navigation = new Navigation();

        /// <summary>
        /// Initialize the static structure.
        /// </summary>
        MASConfig()
        {
            navigation.generalPropagation = 1.0f;
            navigation.NDBPropagation = 1.0f;
            navigation.VORPropagation = 1.2f;
            navigation.DMEPropagation = 1.4f;
        }

        /// <summary>
        /// Read our config settings, setting defaults where needed.
        /// </summary>
        /// <param name="node"></param>
        public override void OnLoad(ConfigNode node)
        {
            if (!node.TryGetValue("VerboseLogging", ref VerboseLogging))
            {
                VerboseLogging = true;
            }

            if (!node.TryGetValue("ElectricCharge", ref ElectricCharge))
            {
                ElectricCharge = "ElectricCharge";
            }

            if (!node.TryGetValue("LuaUpdatePriority", ref LuaUpdatePriority))
            {
                LuaUpdatePriority = 1;
            }

            if (!node.TryGetValue("GeneralPropagation", ref navigation.generalPropagation))
            {
                navigation.generalPropagation = 1.0f;
            }

            if (!node.TryGetValue("NDBPropagation", ref navigation.NDBPropagation))
            {
                navigation.NDBPropagation = 1.0f;
            }

            if (!node.TryGetValue("VORPropagation", ref navigation.VORPropagation))
            {
                navigation.VORPropagation = 1.2f;
            }

            if (!node.TryGetValue("DMEPropagation", ref navigation.DMEPropagation))
            {
                navigation.DMEPropagation = 1.4f;
            }
        }

        /// <summary>
        /// Save the config values to the persistent file.
        /// </summary>
        /// <param name="node">The node to which we write.</param>
        public override void OnSave(ConfigNode node)
        {
            node.AddValue("VerboseLogging", VerboseLogging);
            node.AddValue("ElectricCharge", ElectricCharge);
            node.AddValue("LuaUpdatePriority", LuaUpdatePriority);
            node.AddValue("GeneralPropagation", navigation.generalPropagation);
            node.AddValue("NDBPropagation", navigation.NDBPropagation);
            node.AddValue("VORPropagation", navigation.VORPropagation);
            node.AddValue("DMEPropagation", navigation.DMEPropagation);
        }
    }
}
