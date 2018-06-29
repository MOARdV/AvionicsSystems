/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016-2018 MOARdV
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
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// Provides an action that can control an animation by blending it between its start and end
    /// positions.
    /// </summary>
    class MASActionAnimation : IMASSubComponent
    {
        private string variableName = string.Empty;
        private string animationName = string.Empty;
        private MASFlightComputer comp;
        private Animation animation;
        private AnimationState animationState;
        private Variable range1, range2;
        private readonly bool rateLimited = false;
        private readonly float speed = 0.0f;
        private float currentBlend = -0.01f;
        private float goalBlend = 0.0f;
        private bool coroutineActive = false;

        internal MASActionAnimation(ConfigNode config, InternalProp prop, MASFlightComputer comp):base(config, prop, comp)
        {
            this.comp = comp;

            bool exterior = false;
            if (!config.TryGetValue("animation", ref animationName))
            {
                if (!config.TryGetValue("externalAnimation", ref animationName) || string.IsNullOrEmpty(animationName))
                {
                    throw new ArgumentException("Invalid or missing 'externalAnimation' or 'animation' in ANIMATION " + name);
                }

                exterior = true;
            }

            // Set up the animation.
            Animation[] animators = (exterior) ? prop.part.FindModelAnimators(animationName) : prop.FindModelAnimators(animationName);
            if (animators.Length == 0)
            {
                throw new ArgumentException("Unable to find" + ((exterior) ? " external " : " ") + "animation " + animationName + " for ANIMATION " + name);
            }
            animation = animators[0];
            animationState = animation[animationName];
            animationState.wrapMode = WrapMode.Once;
            animationState.speed = 0.0f;

            if (!config.TryGetValue("variable", ref variableName) || string.IsNullOrEmpty(variableName))
            {
                throw new ArgumentException("Invalid or missing 'variable' in ANIMATION " + name);
            }
            variableName = variableName.Trim();

            string range = string.Empty;
            if (config.TryGetValue("range", ref range))
            {
                string[] ranges = Utility.SplitVariableList(range);
                if (ranges.Length != 2)
                {
                    throw new ArgumentException("Incorrect number of values in 'range' in ANIMATION " + name);
                }
                range1 = comp.GetVariable(ranges[0], prop);
                range2 = comp.GetVariable(ranges[1], prop);

                if (config.TryGetValue("speed", ref speed) && speed > 0.0f)
                {
                    rateLimited = true;
                }
            }
            else
            {
                throw new ArgumentException("Invalid or missing 'range' in ANIMATION " + name);
            }

            variableRegistrar.RegisterNumericVariable(variableName, VariableCallback);
        }

        /// <summary>
        /// Variable callback used to update the animation when it is playing.
        /// </summary>
        /// <param name="newValue"></param>
        private void VariableCallback(double newValue)
        {
            float newBlend = Mathf.InverseLerp((float)range1.AsDouble(), (float)range2.AsDouble(), (float)newValue);

            if (!Mathf.Approximately(currentBlend, newBlend))
            {
                if (rateLimited)
                {
                    goalBlend = newBlend;

                    newBlend = RateLimitBlend(newBlend);
                }
                currentBlend = newBlend;
                animationState.normalizedTime = currentBlend;
                animation.Play(animationName);
            }
        }

        /// <summary>
        /// Apply rate limitations to the blend.
        /// </summary>
        /// <param name="newBlend">New intended blend position</param>
        /// <returns>Rate-limited blend position</returns>
        private float RateLimitBlend(float newBlend)
        {
            float difference = Mathf.Abs(newBlend - currentBlend);
            float maxDelta = TimeWarp.deltaTime * speed;

            if (difference > maxDelta)
            {
                if (newBlend < currentBlend)
                {
                    newBlend = currentBlend - maxDelta;
                }
                else
                {
                    newBlend = currentBlend + maxDelta;
                }

                if (!coroutineActive)
                {
                    comp.StartCoroutine(TimedRotationCoroutine());
                }
            }

            return newBlend;
        }

        /// <summary>
        /// Coroutine to manage rate-limited rotations (those that can't snap into
        /// position due to restraints in their configurations).
        /// </summary>
        /// <returns></returns>
        private IEnumerator TimedRotationCoroutine()
        {
            coroutineActive = true;
            while (!Mathf.Approximately(goalBlend, currentBlend))
            {
                yield return MASConfig.waitForFixedUpdate;

                currentBlend = RateLimitBlend(goalBlend);

                animationState.normalizedTime = currentBlend;
                animation.Play(animationName);
            }
            coroutineActive = false;
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void ReleaseResources(MASFlightComputer comp, InternalProp prop)
        {
            variableRegistrar.ReleaseResources();
            animationState = null;
            animation = null;
        }
    }
}
