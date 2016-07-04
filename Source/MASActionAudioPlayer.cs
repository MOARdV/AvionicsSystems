/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016 MOARdV
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
using System.Linq;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    internal class MASActionAudioPlayer : IMASAction
    {
        private string name = "(anonymous)";
        private string variableName = string.Empty;
        private MASFlightComputer.Variable range1, range2;
        private AudioSource audioSource;
        private readonly bool rangeMode = false;
        private bool currentState = false;
        private readonly PlaybackMode playbackTrigger = PlaybackMode.ON;

        private enum PlaybackMode
        {
            ON,
            OFF,
            BOTH,
            LOOP
        };

        internal MASActionAudioPlayer(ConfigNode config, InternalProp prop, MASFlightComputer comp)
        {
            if (!config.TryGetValue("name", ref name))
            {
                name = "(anonymous)";
            }

            float volume = 1.0f;
            if (config.TryGetValue("volume", ref volume))
            {
                volume = Mathf.Clamp01(volume);
            }

            string sound = string.Empty;
            if (!config.TryGetValue("sound", ref sound) || string.IsNullOrEmpty(sound))
            {
                throw new ArgumentException("Missing or invalid parameter 'sound' in AUDIO_PLAYER " + name);
            }

            //Try Load audio
            AudioClip clip = GameDatabase.Instance.GetAudioClip(sound);
            if(clip == null)
            {
                throw new ArgumentException("Unable to load 'sound' "+sound+" in AUDIO_PLAYER " + name);
            }

            string playbackTrigger = string.Empty;
            config.TryGetValue("trigger", ref playbackTrigger);
            if (string.IsNullOrEmpty(playbackTrigger))
            {
                throw new ArgumentException("Missing parameter 'trigger' in AUDIO_PLAYER " + name);
            }
            else
            {
                playbackTrigger = playbackTrigger.Trim();
                if (playbackTrigger == PlaybackMode.ON.ToString())
                {
                    this.playbackTrigger = PlaybackMode.ON;
                }
                else if (playbackTrigger == PlaybackMode.OFF.ToString())
                {
                    this.playbackTrigger = PlaybackMode.OFF;
                }
                else if (playbackTrigger == PlaybackMode.BOTH.ToString())
                {
                    this.playbackTrigger = PlaybackMode.BOTH;
                }
                else if (playbackTrigger == PlaybackMode.LOOP.ToString())
                {
                    this.playbackTrigger = PlaybackMode.LOOP;
                }
                else
                {
                    throw new ArgumentException("Unrecognized parameter 'trigger = " + playbackTrigger + "' in AUDIO_PLAYER " + name);
                }
            }

            audioSource = prop.gameObject.AddComponent<AudioSource>();
            audioSource.clip = clip;
            audioSource.Stop();
            audioSource.volume = GameSettings.SHIP_VOLUME * volume;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.maxDistance = 8.0f;
            audioSource.minDistance = 2.0f;
            audioSource.dopplerLevel = 0.0f;
            audioSource.panStereo = 0.0f;
            audioSource.playOnAwake = false;
            audioSource.loop = (this.playbackTrigger == PlaybackMode.LOOP);
            audioSource.pitch = 1.0f;

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
                    throw new ArgumentException("Incorrect number of values in 'range' in AUDIO_PLAYER " + name);
                }
                range1 = comp.GetVariable(ranges[0]);
                range2 = comp.GetVariable(ranges[1]);
                rangeMode = true;
            }
            else
            {
                rangeMode = false;
            }

            audioSource.mute = (CameraManager.Instance.currentCameraMode != CameraManager.CameraMode.IVA);

            GameEvents.OnCameraChange.Add(OnCameraChange);

            comp.RegisterNumericVariable(variableName, VariableCallback);
        }

        /// <summary>
        /// Callback used when camera switches (so I can mute audio when not in craft).
        /// </summary>
        /// <param name="newCameraMode"></param>
        private void OnCameraChange(CameraManager.CameraMode newCameraMode)
        {
            audioSource.mute = (newCameraMode != CameraManager.CameraMode.IVA);
        }

        /// <summary>
        /// Variable callback used to update the animation when it is playing.
        /// </summary>
        /// <param name="newValue"></param>
        private void VariableCallback(double newValue)
        {
            if (rangeMode)
            {
                newValue = (newValue.Between(range1.SafeValue(), range2.SafeValue())) ? 1.0 : 0.0;
            }

            bool newState = (newValue > 0.0);

            if (newState != currentState)
            {
                currentState = newState;

                if (currentState)
                {
                    if (playbackTrigger != PlaybackMode.OFF)
                    {
                        audioSource.Play();
                    }
                    else
                    {
                        audioSource.Stop();
                    }
                }
                else if (playbackTrigger == PlaybackMode.ON || playbackTrigger == PlaybackMode.LOOP)
                {
                    audioSource.Stop();
                }
                else
                {
                    audioSource.Play();
                }
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
        /// Return if the action is persistent
        /// </summary>
        /// <returns></returns>
        public bool Persistent()
        {
            return true;
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public void ReleaseResources(MASFlightComputer comp)
        {
            GameEvents.OnCameraChange.Remove(OnCameraChange);
            comp.UnregisterNumericVariable(variableName, VariableCallback);
            audioSource.Stop();
            audioSource.clip = null;
            audioSource = null;
        }
    }
}
