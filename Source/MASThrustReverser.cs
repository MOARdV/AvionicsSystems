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
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// This module, which I originally wrote for RasterPropMonitor, is a simple
    /// wrapper class to interact with an animation installed on the same part.
    /// The assumption is that this module is added to an engine that uses a
    /// generic animation to act as a thrust reverser.  One enhancement I've
    /// added since adding it to MAS is to put wrapper functions here, instead
    /// of only using it to find the animation it's tracking.  I've also added
    /// the requirement that the attached animation's name be provided.
    /// </summary>
    public class MASThrustReverser : PartModule
    {
        [KSPField]
        public string animationName;

        private ModuleAnimateGeneric thrustReverserAnimation;

        /// <summary>
        /// Look for the animation we want control.
        /// </summary>
        public void Start()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (!string.IsNullOrEmpty(animationName))
                {
                    var thrustReverserAnimations = part.Modules.GetModules<ModuleAnimateGeneric>();
                    for (int i = thrustReverserAnimations.Count - 1; i >= 0; --i)
                    {
                        if (thrustReverserAnimations[i].animationName == animationName)
                        {
                            thrustReverserAnimation = thrustReverserAnimations[i];
                            break;
                        }
                    }
                }

                if (thrustReverserAnimation == null)
                {
                    isEnabled = false;
                }
            }
        }

        /// <summary>
        /// Return the position of the thrust reverser.
        /// </summary>
        /// <returns>Normalized position [0, 1]; 0 if this is an invalid TR module.</returns>
        public float Position()
        {
            if (thrustReverserAnimation != null)
            {
                return thrustReverserAnimation.Progress;
            }
            else
            {
                return 0.0f;
            }
        }

        /// <summary>
        /// Toggle the reverser position.
        /// </summary>
        public void ToggleReverser()
        {
            if (thrustReverserAnimation != null)
            {
                thrustReverserAnimation.Toggle();
            }
        }

        /// <summary>
        /// Null our reference.  Probably not really neccessary.
        /// </summary>
        public void OnDestroy()
        {
            thrustReverserAnimation = null;
        }
    }
}
