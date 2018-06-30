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
            internal Action onRelease;
            internal AudioSource audioSource;
            private bool buttonState = false;
            internal bool autoRepeat = false;
            internal bool colliderEnabled = true;
            internal float repeatRate = float.MaxValue;
            private float repeatCounter;

            /// <summary>
            /// Mouse press handler.  Trigger the autorepeat event, if appropriate.
            /// </summary>
            public void OnMouseDown()
            {
                if (colliderEnabled && onClick != null)
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

            string clickEvent = string.Empty, releaseEvent = string.Empty;
            config.TryGetValue("onClick", ref clickEvent);
            config.TryGetValue("onRelease", ref releaseEvent);
            if (string.IsNullOrEmpty(clickEvent) && string.IsNullOrEmpty(releaseEvent))
            {
                throw new ArgumentException("Neither 'onClick' nor 'onRelease' found in COLLIDER_EVENT " + name);
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
