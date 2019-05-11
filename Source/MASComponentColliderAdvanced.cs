/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2019 MOARdV
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
    /// The COLLIDER_ADVANCED manages locational mouse clicks on colliders.
    /// </summary>
    class MASComponentColliderAdvanced : IMASSubComponent
    {
        private AdvancedButtonObject buttonObject;

        /// <summary>
        /// Self-contained monobehaviour to provide button click and release
        /// service to the ASActionColliderEvent.
        /// </summary>
        internal class AdvancedButtonObject : MonoBehaviour
        {
            internal MASComponentColliderAdvanced parent;
            internal Action<Vector2> onClick;
            internal Func<float, float, float, Vector2> hitTransformation;
            internal AudioSource audioSource;
            internal bool colliderEnabled = true;
            internal Collider collider = null;
            internal bool debugEnabled = false;
            private Camera camera = null;

            /// <summary>
            /// Mouse press handler.  Trigger the autorepeat event, if appropriate.
            /// </summary>
            public void OnMouseDown()
            {
                if (colliderEnabled)
                {
                    RaycastHit hit;
                    Camera ca = InternalCamera.Instance.gameObject.GetComponentCached<Camera>(ref camera);
                    if (ca != null)
                    {
                        Ray ray = ca.ScreenPointToRay(Input.mousePosition);
                        if (collider.Raycast(ray, out hit, Mathf.Infinity))
                        {
                            float x1 = Mathf.InverseLerp(collider.bounds.min.x, collider.bounds.max.x, hit.point.x);
                            float y1 = Mathf.InverseLerp(collider.bounds.min.y, collider.bounds.max.y, hit.point.y);
                            float z1 = Mathf.InverseLerp(collider.bounds.min.z, collider.bounds.max.z, hit.point.z);

                            if (debugEnabled)
                            {
                                Utility.LogMessage(this, "Normalized click at {0}, {1}, {2} for {3}",
                                    x1, y1, z1, parent.name);
                            }

                            Vector2 transformedHit = hitTransformation(x1, y1, z1);
                            onClick(transformedHit);
                        }
                        else
                        {
                            Utility.LogWarning(this, "Mouse event did not map to collider");
                        }
                    }
                    else
                    {
                        Utility.LogWarning(this, "Did not find an internal camera - cannot raycast collision");
                    }
                }
            }

            public void OnDestroy()
            {
                //if (activeCoroutine != null)
                //{
                //    StopCoroutine(activeCoroutine);
                //}
            }
        }

        internal MASComponentColliderAdvanced(ConfigNode config, InternalProp internalProp, MASFlightComputer comp)
            : base(config, internalProp, comp)
        {
            string collider = string.Empty;
            if (!config.TryGetValue("collider", ref collider))
            {
                throw new ArgumentException("Missing 'collider' in COLLIDER_ADVANCED " + name);
            }

            string monitorID = string.Empty;
            if (!config.TryGetValue("monitorID", ref monitorID))
            {
                throw new ArgumentException("Missing 'monitorID' in COLLIDER_ADVANCED " + name);
            }

            string clickX = string.Empty;
            if (!config.TryGetValue("clickX", ref clickX))
            {
                throw new ArgumentException("Missing 'clickX' in COLLIDER_ADVANCED " + name);
            }
            string clickY = string.Empty;
            if (!config.TryGetValue("clickY", ref clickY))
            {
                throw new ArgumentException("Missing 'clickY' in COLLIDER_ADVANCED " + name);
            }

            Transform tr = internalProp.FindModelTransform(collider.Trim());
            if (tr == null)
            {
                throw new ArgumentException("Unable to find transform '" + collider + "' in prop for COLLIDER_ADVANCED " + name);
            }

            float volume = -1.0f;
            if (config.TryGetValue("volume", ref volume))
            {
                volume = Mathf.Clamp01(volume);
            }
            else
            {
                volume = -1.0f;
            }

            string sound = string.Empty;
            if (!config.TryGetValue("sound", ref sound) || string.IsNullOrEmpty(sound))
            {
                sound = string.Empty;
            }

            AudioClip clip = null;
            if (string.IsNullOrEmpty(sound) == (volume >= 0.0f))
            {
                throw new ArgumentException("Only one of 'sound' or 'volume' found in COLLIDER_ADVANCED " + name);
            }

            if (volume >= 0.0f)
            {
                //Try Load audio
                clip = GameDatabase.Instance.GetAudioClip(sound);
                if (clip == null)
                {
                    throw new ArgumentException("Unable to load 'sound' " + sound + " in COLLIDER_ADVANCED " + name);
                }
            }

            buttonObject = tr.gameObject.AddComponent<AdvancedButtonObject>();
            buttonObject.parent = this;
            buttonObject.onClick = comp.GetHitAction(monitorID, internalProp);
            buttonObject.hitTransformation = comp.GetColliderTransformation(clickX, clickY, name, internalProp);
            Collider btnCollider = tr.gameObject.GetComponent<Collider>();
            if (btnCollider == null)
            {
                throw new ArgumentException("Unable to retrieve Collider from GameObject in COLLIDER_ADVANCED " + name);
            }
            buttonObject.collider = btnCollider;
            if (!config.TryGetValue("logHits", ref buttonObject.debugEnabled))
            {
                buttonObject.debugEnabled = false;
            }

            string variableName = string.Empty;
            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();

                buttonObject.colliderEnabled = false;
                comp.RegisterVariableChangeCallback(variableName, internalProp, VariableCallback);
            }
            else
            {
                variableEnabled = true;
            }
            comp.RegisterVariableChangeCallback("fc.CrewConscious(-1)", internalProp, KerbalCallback);

            if (clip != null)
            {
                AudioSource audioSource = tr.gameObject.AddComponent<AudioSource>();
                audioSource.clip = clip;
                audioSource.Stop();
                audioSource.volume = GameSettings.SHIP_VOLUME * volume;
                audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
                audioSource.maxDistance = 8.0f;
                audioSource.minDistance = 2.0f;
                audioSource.dopplerLevel = 0.0f;
                audioSource.panStereo = 0.0f;
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSource.pitch = 1.0f;
                buttonObject.audioSource = audioSource;
            }
        }

        private bool variableEnabled = false;
        private bool kerbalConscious = true;

        /// <summary>
        /// Variable callback used to enable the collider.
        /// </summary>
        /// <param name="newValue"></param>
        private void VariableCallback(double newValue)
        {
            variableEnabled = (newValue > 0.0);

            if ((variableEnabled && kerbalConscious) != buttonObject.colliderEnabled)
            {
                buttonObject.colliderEnabled = (variableEnabled && kerbalConscious);
            }
        }

        /// <summary>
        /// Variable callback used to handle kerbal blackouts.
        /// </summary>
        /// <param name="newValue"></param>
        private void KerbalCallback(double newValue)
        {
            kerbalConscious = (newValue > 0.0);

            if ((variableEnabled && kerbalConscious) != buttonObject.colliderEnabled)
            {
                buttonObject.colliderEnabled = (variableEnabled && kerbalConscious);
            }
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            if (buttonObject != null)
            {
                buttonObject.parent = null;
            }
            variableRegistrar.ReleaseResources();
        }
    }
}
