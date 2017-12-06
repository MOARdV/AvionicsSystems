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
using UnityEngine;

namespace AvionicsSystems
{
    class MASPageGroundTrack : IMASMonitorComponent
    {
        private string name = "anonymous";

        enum LineSegment
        {
            Vessel1,
            Vessel2,
            Maneuver1,
            Maneuver2,
            Target1,
            Target2
        };

        private GameObject[] lineOrigin = new GameObject[6];
        private Material[] lineMaterial = new Material[6];
        private LineRenderer[] lineRenderer = new LineRenderer[6];

        internal MASPageGroundTrack(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
        {
            if (!config.TryGetValue("name", ref name))
            {
                name = "anonymous";
            }
        }

        /// <summary>
        /// Enable / disable renderer components without disabling game objects.
        /// </summary>
        /// <param name="enable"></param>
        public void EnableRender(bool enable)
        {
            //lineRenderer.enabled = enable;
        }

        /// <summary>
        /// Enables / disables overall page rendering.
        /// </summary>
        /// <param name="enable"></param>
        public void EnablePage(bool enable)
        {

        }

        /// <summary>
        /// Handle a softkey event.
        /// </summary>
        /// <param name="keyId">The numeric ID of the key to handle.</param>
        /// <returns>true if the component handled the key, false otherwise.</returns>
        public bool HandleSoftkey(int keyId)
        {
            return false;
        }

        /// <summary>
        ///  Return the name of the action.
        /// </summary>
        /// <returns></returns>
        public string Name()
        {
            return name;
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            //lineRenderer = null;
            //UnityEngine.Object.Destroy(lineMaterial);
            //lineMaterial = null;
            //UnityEngine.Object.Destroy(lineOrigin);
            //lineOrigin = null;

            //int numVariables = registeredVariables.Count;
            //if (registeredCallbacks.Count != numVariables)
            //{
            //    throw new ArgumentOutOfRangeException("# registered variables != # registered callbacks in LINE_STRING " + name);
            //}
            //for (int i = 0; i < numVariables; ++i)
            //{
            //    comp.UnregisterNumericVariable(registeredVariables[i], internalProp, registeredCallbacks[i]);
            //}
            //registeredVariables.Clear();
            //registeredCallbacks.Clear();
        }
    }
}
