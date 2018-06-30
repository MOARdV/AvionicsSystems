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
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// MASNavBall provides an override to the InternalNavBall prop module that
    /// allows the navball's movement to be disabled by a controlling variable.
    /// The variable can be configured to operate in Threshold Mode or Boolean
    /// Mode like most actions of a MASComponent.
    /// </summary>
    class MASNavBall : InternalNavBall
    {
        /// <summary>
        /// Name of the enabling variable.  Required.
        /// </summary>
        [KSPField]
        public string variable = string.Empty;

        /// <summary>
        /// vec2 containing the 'enabled' range.  May be numeric or variables.  Optional.
        /// </summary>
        [KSPField]
        public string range = string.Empty;

        /// <summary>
        /// Maximum angle that the navball can change per second, in degrees.  Optional.  Defaults to 180.
        /// </summary>
        [KSPField]
        public float maxAngle = 180.0f;

        private VariableRegistrar variableRegistrar;
        private bool currentState = false;

        private Quaternion lastOrientation;

        /// <summary>
        /// Initialize the MASNavBall components.
        /// </summary>
        public void Start()
        {
            MASFlightComputer comp = MASFlightComputer.Instance(internalProp.part);
            if (comp == null)
            {
                throw new ArgumentNullException("Unable to find MASFlightComputer in part - please check part configs");
            }
            variableRegistrar = new VariableRegistrar(comp, null);

            lastOrientation = navBall.rotation;

            variableRegistrar.RegisterVariableChangeCallback(variable, (double newValue) => currentState = (newValue > 0.0));
        }

        /// <summary>
        /// Unregister the variable callback.
        /// </summary>
        public void OnDestroy()
        {
            try
            {
                // I've seen this get destroyed when the game was exiting, so
                // there was an instance floating around somewhere for some
                // reason.  So try/catch to suppress exceptions.
                variableRegistrar.ReleaseResources();
            }
            catch { }
        }

        /// <summary>
        /// Override the base navball behavior so we can prevent the ball from
        /// rotating if it the controlling variable says it's disabled.
        /// </summary>
        public override void OnUpdate()
        {
            if (currentState)
            {
                base.OnUpdate();
                Quaternion post = navBall.rotation;
                float deltaAngle = Quaternion.Angle(lastOrientation, post);
                float maxAngleThisUpdate = maxAngle * Time.deltaTime;

                // If the rotation angle exceeds what we can do, slow it down
                if (deltaAngle > maxAngleThisUpdate)
                {
                    Quaternion newRotation = Quaternion.Slerp(lastOrientation, post, maxAngleThisUpdate / deltaAngle);
                    lastOrientation = newRotation;
                    navBall.rotation = newRotation;
                }
                else
                {
                    lastOrientation = post;
                }
            }
        }
    }
}
