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
    /// The COLLIDER_EVENT manages mouse clicks on colliders.  It can support
    /// auto-repeating the onClick event, and it can
    /// support transient actions using onClick and onRelease.
    /// </summary>
    class MASActionColliderEvent : IMASSubComponent
    {
        private ButtonObject buttonObject;

        /// <summary>
        /// Self-contained monobehaviour to provide button click and release
        /// service to the ASActionColliderEvent.
        /// </summary>
        internal class ButtonObject : MonoBehaviour
        {
            internal MASActionColliderEvent parent;
            internal Action onClick;
            internal Action<double> onDragX;
            internal Action<double> onDragY;
            internal Action onRelease;
            internal AudioSource audioSource;
            private bool buttonState = false;
            internal bool autoRepeat = false;
            internal bool colliderEnabled = true;
            internal bool drag = false;
            internal Vector2 lastDragPosition;
            internal float normalizationScalar;
            internal float repeatRate = float.MaxValue;
            private float repeatCounter;

            /// <summary>
            /// Mouse press handler.  Trigger the autorepeat event, if appropriate.
            /// </summary>
            public void OnMouseDown()
            {
                if (colliderEnabled)
                {
                    if (onClick != null)
                    {
                        onClick();

                        if (audioSource != null && buttonState == false)
                        {
                            audioSource.Play();
                        }

                        if (autoRepeat)
                        {
                            buttonState = true;
                            repeatCounter = 0.0f;
                            StartCoroutine(AutoRepeat());
                        }
                    }
                    if (drag)
                    {
                        lastDragPosition = new Vector2(Input.mousePosition.x * normalizationScalar, Input.mousePosition.y * normalizationScalar);
                    }
                }
            }

            /// <summary>
            /// Mouse movement handler.  If we have a drag event handler, let's handle it.
            /// </summary>
            public void OnMouseDrag()
            {
                if (colliderEnabled && drag)
                {
                    Vector2 newDragPosition = new Vector2(Input.mousePosition.x * normalizationScalar, Input.mousePosition.y * normalizationScalar);

                    bool updated = false;
                    if (onDragX != null && !Mathf.Approximately(newDragPosition.x, lastDragPosition.x))
                    {
                        onDragX(Mathf.Clamp(newDragPosition.x - lastDragPosition.x, -1.0f, 1.0f));
                        updated = true;
                    }
                    if (onDragY != null && !Mathf.Approximately(newDragPosition.y, lastDragPosition.y))
                    {
                        onDragY(Mathf.Clamp(newDragPosition.y - lastDragPosition.y, -1.0f, 1.0f));
                        updated = true;
                    }
                    if (updated)
                    {
                        lastDragPosition = newDragPosition;
                    }
                }
            }

            /// <summary>
            /// AutoRepeat callback
            /// </summary>
            /// <returns></returns>
            public IEnumerator AutoRepeat()
            {
                yield return MASConfig.waitForFixedUpdate;

                while (colliderEnabled && buttonState)
                {
                    repeatCounter += TimeWarp.fixedDeltaTime;
                    if (repeatCounter > repeatRate)
                    {
                        repeatCounter -= repeatRate;
                        onClick();
                    }
                    yield return MASConfig.waitForFixedUpdate;
                }
            }

            /// <summary>
            /// Mouse release event, cancel the autorepeat event.
            /// </summary>
            public void OnMouseUp()
            {
                buttonState = false;
                if (colliderEnabled && onRelease != null)
                {
                    onRelease();
                }
            }
        }

        internal MASActionColliderEvent(ConfigNode config, InternalProp internalProp, MASFlightComputer comp)
            : base(config, internalProp, comp)
        {
            string collider = string.Empty;
            if (!config.TryGetValue("collider", ref collider))
            {
                throw new ArgumentException("Missing 'collider' in COLLIDER_EVENT " + name);
            }

            string clickEvent = string.Empty, releaseEvent = string.Empty, dragEventX = string.Empty, dragEventY = string.Empty;
            config.TryGetValue("onClick", ref clickEvent);
            config.TryGetValue("onRelease", ref releaseEvent);
            config.TryGetValue("onDragX", ref dragEventX);
            config.TryGetValue("onDragY", ref dragEventY);
            if (string.IsNullOrEmpty(clickEvent) && string.IsNullOrEmpty(releaseEvent) && string.IsNullOrEmpty(dragEventX) && string.IsNullOrEmpty(dragEventY))
            {
                throw new ArgumentException("None of 'onClick', 'onRelease', 'onDragX', nor 'onDragY' found in COLLIDER_EVENT " + name);
            }

            Transform tr = internalProp.FindModelTransform(collider.Trim());
            if (tr == null)
            {
                throw new ArgumentException("Unable to find transform '" + collider + "' in prop for COLLIDER_EVENT " + name);
            }

            float autoRepeat = 0.0f;
            if (!config.TryGetValue("autoRepeat", ref autoRepeat))
            {
                autoRepeat = 0.0f;
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
                throw new ArgumentException("Only one of 'sound' or 'volume' found in COLLIDER_EVENT " + name);
            }

            if (volume >= 0.0f)
            {
                //Try Load audio
                clip = GameDatabase.Instance.GetAudioClip(sound);
                if (clip == null)
                {
                    throw new ArgumentException("Unable to load 'sound' " + sound + " in COLLIDER_EVENT " + name);
                }
            }

            buttonObject = tr.gameObject.AddComponent<ButtonObject>();
            buttonObject.parent = this;
            buttonObject.autoRepeat = (autoRepeat > 0.0f);
            buttonObject.repeatRate = autoRepeat;

            string variableName = string.Empty;
            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();

                buttonObject.colliderEnabled = false;
                comp.RegisterVariableChangeCallback(variableName, internalProp, VariableCallback);
            }

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

            if (!string.IsNullOrEmpty(clickEvent))
            {
                buttonObject.onClick = comp.GetAction(clickEvent, internalProp);
            }
            if (!string.IsNullOrEmpty(releaseEvent))
            {
                buttonObject.onRelease = comp.GetAction(releaseEvent, internalProp);
            }
            if (!string.IsNullOrEmpty(dragEventX))
            {
                buttonObject.onDragX = comp.GetDragAction(dragEventX, name, internalProp);
                if (buttonObject.onDragX != null)
                {
                    buttonObject.drag = true;
                    float dragSensitivity = 1.0f;
                    if (!config.TryGetValue("dragSensitivity", ref dragSensitivity))
                    {
                        dragSensitivity = 1.0f;
                    }
                    buttonObject.normalizationScalar = 0.01f * dragSensitivity;
                }
                else
                {
                    throw new ArgumentException("Unable to create 'onDragX' event for COLLIDER_EVENT " + name);
                }
            }
            if (!string.IsNullOrEmpty(dragEventY))
            {
                buttonObject.onDragY = comp.GetDragAction(dragEventY, name, internalProp);
                if (buttonObject.onDragY != null)
                {
                    buttonObject.drag = true;
                    float dragSensitivity = 1.0f;
                    if (!config.TryGetValue("dragSensitivity", ref dragSensitivity))
                    {
                        dragSensitivity = 1.0f;
                    }
                    buttonObject.normalizationScalar = 0.01f * dragSensitivity;
                }
                else
                {
                    throw new ArgumentException("Unable to create 'onDragY' event for COLLIDER_EVENT " + name);
                }
            }
        }

        /// <summary>
        /// Variable callback used to enable the collider.
        /// </summary>
        /// <param name="newValue"></param>
        private void VariableCallback(double newValue)
        {
            bool newState = (newValue > 0.0);

            if (newState != buttonObject.colliderEnabled)
            {
                buttonObject.colliderEnabled = newState;
            }
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            if (buttonObject != null)
            {
                buttonObject.onClick = null;
                buttonObject.onRelease = null;
                buttonObject.parent = null;
            }
            variableRegistrar.ReleaseResources();
        }
    }
}
