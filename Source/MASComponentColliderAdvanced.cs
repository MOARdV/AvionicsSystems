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
        // Actions for on-prop control:
        private Action<Vector2> onClick;
        private Action<Vector2> onDrag;
        private Action<Vector2> onRelease;

        /// <summary>
        /// Self-contained monobehaviour to provide button handling
        /// service to the MASComponentColliderAdvanced.
        /// </summary>
        internal class AdvancedButtonObject : MonoBehaviour
        {
            internal MASComponentColliderAdvanced parent;
            internal Action<Vector2, EventType> onTouch;
            internal Func<float, float, float, Vector2> hitTransformation;
            internal AudioSource audioSource;
            internal bool colliderEnabled = true;
            private BoxCollider collider = null;
            private Vector3 hitCorner = Vector3.zero;
            private Vector3 invSize = Vector3.zero;
            internal bool debugEnabled = false;
            private bool mouseDown = false;
            private Camera camera = null;
            private Vector2 lastHit = Vector2.zero;
            private Matrix4x4 worldToLocal = Matrix4x4.identity;

            internal void InitBoxCollider(BoxCollider bc)
            {
                collider = bc;
                Vector3 size = collider.size;
                hitCorner = collider.center - 0.5f * size;
                invSize = new Vector3(1.0f / size.x, 1.0f / size.y, 1.0f / size.z);
                worldToLocal = collider.transform.worldToLocalMatrix;
            }

            private bool HitAt(out Vector2 hitLocation, bool isClick)
            {
                Camera ca = InternalCamera.Instance.gameObject.GetComponentCached<Camera>(ref camera);
                if (ca != null)
                {
                    RaycastHit hit;
                    Ray ray = ca.ScreenPointToRay(Input.mousePosition);
                    if (collider.Raycast(ray, out hit, Mathf.Infinity))
                    {
                        // hit.point reports the world space coordinate of the collision.  We need local space
                        // so we can determine where along the collider we actually hit.
                        Vector3 xFormedHit = worldToLocal.MultiplyPoint(hit.point) - hitCorner;

                        // Normalize the values
                        float x1 = xFormedHit.x * invSize.x;
                        float y1 = xFormedHit.y * invSize.y;
                        float z1 = xFormedHit.z * invSize.z;

                        hitLocation = hitTransformation(x1, y1, z1);
                        if (isClick && debugEnabled)
                        {
                            Utility.LogMessage(this, "Normalized click at {0}, {1}, {2} => {4}, {5} for {3}",
                                x1, y1, z1, parent.name, hitLocation.x, hitLocation.y);
                        }
                        return true;
                    }
                    // This happens when the mouse is dragged out of bounds
                    //else
                    //{
                    //    Utility.LogWarning(this, "Raycast failed: Mouse event did not intersect {0} collider?", parent.name);
                    //}
                }
                else
                {
                    Utility.LogWarning(this, "Did not find an internal camera - cannot raycast collision");
                }
                hitLocation = Vector2.zero;
                return false;
            }

            /// <summary>
            /// Mouse press handler.
            /// </summary>
            public void OnMouseDown()
            {
                Vector2 transformedHit;
                if (colliderEnabled && HitAt(out transformedHit, true))
                {
                    mouseDown = true;
                    lastHit = transformedHit;
                    onTouch(transformedHit, EventType.MouseDown);
                }
            }

            /// <summary>
            /// Mouse drag handler.
            /// </summary>
            public void OnMouseDrag()
            {
                if (mouseDown)
                {
                    Vector2 transformedHit;
                    // See if the mouse drag was in bounds.  If not, don't forward it to the callback.
                    if (HitAt(out transformedHit, false))
                    {
                        // If the movement was fairly small, don't spam the callback system with updates.
                        if (!Mathf.Approximately(transformedHit.x, lastHit.x) || !Mathf.Approximately(transformedHit.y, lastHit.y))
                        {
                            lastHit = transformedHit;
                            onTouch(transformedHit, EventType.MouseDrag);
                        }
                    }
                }
            }

            /// <summary>
            /// Mouse release handler.
            /// </summary>
            public void OnMouseUp()
            {
                if (mouseDown)
                {
                    Vector2 transformedHit;
                    if (HitAt(out transformedHit, false))
                    {
                        onTouch(transformedHit, EventType.MouseUp);
                    }
                    else
                    {
                        // If the mouse release was out of bounds, notify the callback using the last
                        // in-bounds hit location.
                        onTouch(lastHit, EventType.MouseUp);
                    }
                    mouseDown = false;
                }
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

            string clickAction = string.Empty;
            string dragAction = string.Empty;
            string releaseAction = string.Empty;
            string monitorID = string.Empty;
            if (!config.TryGetValue("monitorID", ref monitorID))
            {
                config.TryGetValue("onClick", ref clickAction);
                config.TryGetValue("onDrag", ref dragAction);
                config.TryGetValue("onRelease", ref releaseAction);

                if (string.IsNullOrEmpty(clickAction) && string.IsNullOrEmpty(dragAction) && string.IsNullOrEmpty(releaseAction))
                {
                    throw new ArgumentException("Missing 'monitorID', 'onClick', 'onDrag', or 'onRelease' in COLLIDER_ADVANCED " + name);
                }
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
            if (string.IsNullOrEmpty(monitorID))
            {
                if (!string.IsNullOrEmpty(clickAction))
                {
                    onClick = comp.GetColliderAction(clickAction, 0, "click", internalProp);
                }
                if (!string.IsNullOrEmpty(dragAction))
                {
                    onDrag = comp.GetColliderAction(dragAction, 0, "drag", internalProp);
                }
                if (!string.IsNullOrEmpty(releaseAction))
                {
                    onRelease = comp.GetColliderAction(releaseAction, 0, "release", internalProp);
                }

                buttonObject.onTouch = (Vector2 hitCoordinate, EventType eventType) =>
                    {
                        if (eventType == EventType.MouseDown)
                        {
                            if (onClick != null)
                            {
                                onClick(hitCoordinate);
                            }
                        }
                        else if (eventType == EventType.MouseDrag)
                        {
                            if (onDrag != null)
                            {
                                onDrag(hitCoordinate);
                            }
                        }
                        else if (eventType == EventType.MouseUp)
                        {
                            if (onRelease != null)
                            {
                                onRelease(hitCoordinate);
                            }
                        }
                    };
            }
            else
            {
                buttonObject.onTouch = comp.GetHitAction(monitorID, internalProp, comp.HandleTouchEvent);
            }
            buttonObject.hitTransformation = comp.GetColliderTransformation(clickX, clickY, name, internalProp);
            Collider btnCollider = tr.gameObject.GetComponent<Collider>();
            if (btnCollider == null)
            {
                throw new ArgumentException("Unable to retrieve Collider from GameObject in COLLIDER_ADVANCED " + name);
            }
            BoxCollider boxCollider = btnCollider as BoxCollider;
            if (boxCollider == null)
            {
                throw new ArgumentException("Collider for COLLIDER_ADVANCED " + name + " is not a BoxCollider");
            }

            buttonObject.InitBoxCollider(boxCollider);
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
