/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016 - 2017 MOARdV
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
    internal class MASActionAnimationPlayer : IMASSubComponent
    {
        private string name = "anonymous";
        private string variableName = string.Empty;
        private float animationSpeed = 1.0f;
        private string animationName = string.Empty;
        private Animation animation;
        private AnimationState animationState;
        private MASFlightComputer.Variable range1, range2;
        private readonly bool rangeMode = false;
        private bool playedOnce = false;
        private bool currentState = false;

        // TODO: Support 'reverse'?
        internal MASActionAnimationPlayer(ConfigNode config, InternalProp prop, MASFlightComputer comp)
        {
            if (!config.TryGetValue("name", ref name))
            {
                name = "anonymous";
            }

            if (!config.TryGetValue("animation", ref animationName) || string.IsNullOrEmpty(animationName))
            {
                throw new ArgumentException("Invalid or missing 'animation' in ANIMATION_PLAYER " + name);
            }

            // Set up the animation.
            Animation[] animators = prop.FindModelAnimators(animationName);
            if (animators.Length == 0)
            {
                throw new ArgumentException("Unable to find animation " + animationName + " for ANIMATION_PLAYER " + name);
            }
            animation = animators[0];
            animationState = animation[animationName];
            animationState.wrapMode = WrapMode.Once;

            config.TryGetValue("animationSpeed", ref animationSpeed);

            if (!config.TryGetValue("variable", ref variableName) || string.IsNullOrEmpty(variableName))
            {
                throw new ArgumentException("Invalid or missing 'variable' in ANIMATION_PLAYER " + name);
            }
            variableName = variableName.Trim();

            string range = string.Empty;
            if (config.TryGetValue("range", ref range))
            {
                string[] ranges = range.Split(',');
                if (ranges.Length != 2)
                {
                    throw new ArgumentException("Incorrect number of values in 'range' in ANIMATION_PLAYER " + name);
                }
                range1 = comp.GetVariable(ranges[0], prop);
                range2 = comp.GetVariable(ranges[1], prop);
                rangeMode = true;
            }
            else
            {
                rangeMode = false;
            }

            comp.RegisterNumericVariable(variableName, prop, VariableCallback);
        }

        /// <summary>
        /// Variable callback used to update the animation when it is playing.
        /// </summary>
        /// <param name="newValue"></param>
        private void VariableCallback(double newValue)
        {
            if (playedOnce)
            {
                if (rangeMode)
                {
                    newValue = (newValue.Between(range1.SafeValue(), range2.SafeValue())) ? 1.0 : 0.0;
                }

                bool newState = (newValue > 0.0);

                if (newState != currentState)
                {
                    currentState = newState;
                    if (newState)
                    {
                        animationState.normalizedTime = 0.0f;
                        animationState.speed = 1.0f * animationSpeed;
                    }
                    else
                    {
                        animationState.normalizedTime = 1.0f;
                        animationState.speed = -1.0f * animationSpeed;
                    }
                    animation.Play(animationName);
                }
            }
            else
            {
                if (rangeMode)
                {
                    newValue = (newValue.Between(range1.SafeValue(), range2.SafeValue())) ? 1.0 : 0.0;
                }

                if (newValue > 0.0)
                {
                    animationState.speed = float.MaxValue;
                    animationState.normalizedTime = 0.0f;
                    currentState = true;
                }
                else
                {
                    animationState.speed = float.MinValue;
                    animationState.normalizedTime = 1.0f;
                    currentState = false;
                }

                animation.Play(animationName);
                playedOnce = true;
            }
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
        public void ReleaseResources(MASFlightComputer comp, InternalProp prop)
        {
            comp.UnregisterNumericVariable(variableName, prop, VariableCallback);
            animationState = null;
            animation = null;
        }
    }
}
