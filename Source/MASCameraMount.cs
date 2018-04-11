/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2018 MOARdV
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
    class MASCameraMount : PartModule
    {
        /// <summary>
        /// Defines the minimum and maximum pan angle (left-right camera rotation)
        /// of the camera lens in degrees.  Automatically clamps between
        /// -180 and +180.  Negative values indicate to the left of the center
        /// position. Positive values indicate to the right.
        /// </summary>
        [KSPField]
        public Vector2 panRange = new Vector2(0.0f, 0.0f);

        /// <summary>
        /// Used internally to allow desired pan angle to persist.  Should only
        /// be changed programmatically through AddPan() and SetPan() to
        /// manage the pan limits.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float goalPan = 0.0f;

        /// <summary>
        /// Current pan position.  May differ from goalPan if panRate != 0
        /// </summary>
        [KSPField(isPersistant = true)]
        public float currentPan = 0.0f;

        /// <summary>
        /// Maximum rate (degrees/sec) for pan.  0 indicates instant.
        /// </summary>
        [KSPField]
        public float panRate = 0.0f;

        /// <summary>
        /// Defines the minimum and maximum tilt angle (up-down camera rotation)
        /// of the camera lens in degrees.  Automatically clamps between
        /// -180 and +180.  Negative values indicate (direction), positive
        /// values indicate (other direction).
        /// </summary>
        [KSPField]
        public Vector2 tiltRange = new Vector2(0.0f, 0.0f);

        /// <summary>
        /// Used internally to allow desired tilt angle to persist.  Should only
        /// be changed programmatically through AddTilt() and SetTilt() to
        /// manage the tilt limits.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float goalTilt = 0.0f;

        /// <summary>
        /// Current tilt position.  May differ from goalTilt if tiltRate != 0
        /// </summary>
        [KSPField(isPersistant = true)]
        public float currentTilt = 0.0f;

        /// <summary>
        /// Maximum rate (degrees/sec) for tilt.  0 indicates instant.
        /// </summary>
        [KSPField]
        public float tiltRate = 0.0f;

        /// <summary>
        /// Name of an optional transform to physically pan the model.
        /// </summary>
        [KSPField]
        public string panTransformName = string.Empty;
        private Transform panTransform = null;
        private Quaternion panRotation = Quaternion.identity;

        /// <summary>
        /// Name of an optional transform to physically tilt the model.
        /// </summary>
        [KSPField]
        public string tiltTransformName = string.Empty;
        private Transform tiltTransform = null;
        private Quaternion tiltRotation = Quaternion.identity;

        /// <summary>
        /// Initialize.
        /// </summary>
        public void Start()
        {
            if (!(HighLogic.LoadedScene == GameScenes.EDITOR || HighLogic.LoadedScene == GameScenes.FLIGHT))
            {
                return;
            }

            if (panRange.y < panRange.x)
            {
                panRange = new Vector2(panRange.y, panRange.x);
            }
            panRange.x = Mathf.Clamp(panRange.x, -180.0f, 180.0f);
            panRange.y = Mathf.Clamp(panRange.y, -180.0f, 180.0f);
            currentPan = Mathf.Clamp(currentPan, panRange.x, panRange.y);
            goalPan = Mathf.Clamp(goalPan, panRange.x, panRange.y);
            panRate = Mathf.Abs(panRate);

            if (tiltRange.y < tiltRange.x)
            {
                tiltRange = new Vector2(tiltRange.y, tiltRange.x);
            }
            tiltRange.x = Mathf.Clamp(tiltRange.x, -180.0f, 180.0f);
            tiltRange.y = Mathf.Clamp(tiltRange.y, -180.0f, 180.0f);
            currentTilt = Mathf.Clamp(currentTilt, tiltRange.x, tiltRange.y);
            goalTilt = Mathf.Clamp(goalTilt, tiltRange.x, tiltRange.y);
            tiltRate = Mathf.Abs(tiltRate);


            if (HighLogic.LoadedScene == GameScenes.FLIGHT)
            {
                if (!string.IsNullOrEmpty(panTransformName))
                {
                    panTransform = part.FindModelTransform(panTransformName);
                    if (panTransform == null)
                    {
                        Utility.LogErrorMessage(this, "Unable to find a pan transform named \"{0}\"", panTransformName);
                    }
                    else
                    {
                        panRotation = panTransform.localRotation;
                        panTransform.localRotation = panRotation * Quaternion.Euler(0.0f, currentPan, 0.0f);
                    }
                }

                if (!string.IsNullOrEmpty(tiltTransformName))
                {
                    tiltTransform = part.FindModelTransform(tiltTransformName);
                    if (tiltTransform == null)
                    {
                        Utility.LogErrorMessage(this, "Unable to find a tilt transform named \"{0}\"", tiltTransformName);
                    }
                    else
                    {
                        tiltRotation = tiltTransform.localRotation;
                        tiltTransform.localRotation = tiltRotation * Quaternion.Euler(-currentTilt, 0.0f, 0.0f);
                    }
                }
            }
        }

        /// <summary>
        /// Change the current field of view by `deltaFoV` degrees, remaining within
        /// camera FoV limits.
        /// </summary>
        /// <param name="deltaPan">The amount to add or subtract to FoV in degrees.</param>
        /// <returns>The adjusted pan.</returns>
        public float AddPan(float deltaPan)
        {
            goalPan = Mathf.Clamp(goalPan + deltaPan, panRange.x, panRange.y);

            return goalPan;
        }

        /// <summary>
        /// Change the current field of view by `deltaFoV` degrees, remaining within
        /// camera FoV limits.
        /// </summary>
        /// <param name="deltaTilt">The amount to add or subtract to FoV in degrees.</param>
        /// <returns>The adjusted tilt.</returns>
        public float AddTilt(float deltaTilt)
        {
            goalTilt = Mathf.Clamp(goalTilt + deltaTilt, tiltRange.x, tiltRange.y);

            return goalTilt;
        }

        /// <summary>
        /// Set the current pan location within the pan limits.
        /// </summary>
        /// <param name="pan"></param>
        /// <returns>The adjusted pan setting.</returns>
        public float SetPan(float pan)
        {
            goalPan = Mathf.Clamp(pan, panRange.x, panRange.y);

            return goalPan;
        }

        /// <summary>
        /// Set the current pan location within the pan limits.
        /// </summary>
        /// <param name="tilt"></param>
        /// <returns>The current tilt position</returns>
        public float SetTilt(float tilt)
        {
            goalTilt = Mathf.Clamp(tilt, tiltRange.x, tiltRange.y);

            return goalTilt;
        }

        /// <summary>
        /// Update camera pan/tilt positions
        /// </summary>
        public void Update()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (!Mathf.Approximately(goalPan, currentPan))
                {
                    if (panRate > 0.0f)
                    {
                        float panDelta = Mathf.Min(Mathf.Abs(goalPan - currentPan), panRate * TimeWarp.deltaTime);
                        if (goalPan > currentPan)
                        {
                            currentPan += panDelta;
                        }
                        else
                        {
                            currentPan -= panDelta;
                        }
                    }
                    else
                    {
                        currentPan = goalPan;
                    }

                    if (panTransform != null)
                    {
                        panTransform.localRotation = panRotation * Quaternion.Euler(0.0f, currentPan, 0.0f);
                    }
                }

                if (!Mathf.Approximately(goalTilt, currentTilt))
                {
                    if (tiltRate > 0.0f)
                    {
                        float tiltDelta = Mathf.Min(Mathf.Abs(goalTilt - currentTilt), tiltRate * TimeWarp.deltaTime);
                        if (goalTilt > currentTilt)
                        {
                            currentTilt += tiltDelta;
                        }
                        else
                        {
                            currentTilt -= tiltDelta;
                        }
                    }
                    else
                    {
                        currentTilt = goalTilt;
                    }

                    if (tiltTransform != null)
                    {
                        tiltTransform.localRotation = tiltRotation * Quaternion.Euler(-currentTilt, 0.0f, 0.0f);
                    }
                }
            }
        }
    }
}
