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
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    internal class MASActionAudioPlayer : IMASSubComponent
    {
        private string name = "(anonymous)";
        private string variableName = string.Empty;
        private string pitchVariableName = string.Empty;
        private string volumeVariableName = string.Empty;
        private string soundVariableName = string.Empty;
        private float pitch = 1.0f;
        private float volume = 1.0f;
        private MASFlightComputer.Variable range1, range2;
        private MASFlightComputer.Variable soundVariable;
        private AudioSource audioSource;
        private readonly bool rangeMode = false;
        private readonly bool mustPlayOnce = false;
        private bool hasAudioClip = false;
        private bool currentState = false;
        private readonly PlaybackMode playbackTrigger = PlaybackMode.ON;

        private enum PlaybackMode
        {
            ON,
            OFF,
            BOTH,
            LOOP
        };

        internal MASActionAudioPlayer(ConfigNode config, InternalProp internalProp, MASFlightComputer comp)
        {
            if (!config.TryGetValue("name", ref name))
            {
                name = "(anonymous)";
            }

            if (!config.TryGetValue("volume", ref volumeVariableName))
            {
                volumeVariableName = "1";
            }

            if (!config.TryGetValue("pitch", ref pitchVariableName))
            {
                pitchVariableName = "1";
            }

            string sound = string.Empty;
            if (!config.TryGetValue("sound", ref sound) || string.IsNullOrEmpty(sound))
            {
                if (!config.TryGetValue("variableSound", ref soundVariableName) || string.IsNullOrEmpty(soundVariableName))
                {
                    throw new ArgumentException("Missing or invalid parameters 'sound' and/or 'soundVariable' in AUDIO_PLAYER " + name);
                }
            }

            if (!config.TryGetValue("mustPlayOnce", ref mustPlayOnce))
            {
                mustPlayOnce = false;
            }

            //Try Load audio
            AudioClip clip = null;
            if (!string.IsNullOrEmpty(sound))
            {
                clip = GameDatabase.Instance.GetAudioClip(sound);
                if (clip == null)
                {
                    throw new ArgumentException("Unable to load 'sound' " + sound + " in AUDIO_PLAYER " + name);
                }
                else
                {
                    hasAudioClip = true;
                }
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
                    if (mustPlayOnce)
                    {
                        throw new ArgumentException("Cannot use 'mustPlayOnce' with looping audio in AUDIO_PLAYER" + name);
                    }
                    this.playbackTrigger = PlaybackMode.LOOP;
                }
                else
                {
                    throw new ArgumentException("Unrecognized parameter 'trigger = " + playbackTrigger + "' in AUDIO_PLAYER " + name);
                }
            }

            Transform audioTransform = new GameObject().transform;
            audioTransform.gameObject.name = "MASActionAudioPlayer-" + internalProp.propID + "-" + name;
            audioTransform.gameObject.layer = internalProp.transform.gameObject.layer;
            audioTransform.SetParent(internalProp.transform, false);
            audioSource = audioTransform.gameObject.AddComponent<AudioSource>();

            audioSource.clip = clip;
            audioSource.Stop();
            audioSource.volume = GameSettings.SHIP_VOLUME;
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
                range1 = comp.GetVariable(ranges[0], internalProp);
                range2 = comp.GetVariable(ranges[1], internalProp);
                rangeMode = true;
            }
            else
            {
                rangeMode = false;
            }

            audioSource.mute = (CameraManager.Instance.currentCameraMode != CameraManager.CameraMode.IVA);

            GameEvents.OnCameraChange.Add(OnCameraChange);

            comp.RegisterNumericVariable(pitchVariableName, internalProp, PitchCallback);
            comp.RegisterNumericVariable(volumeVariableName, internalProp, VolumeCallback);
            comp.RegisterNumericVariable(variableName, internalProp, VariableCallback);
            if (!string.IsNullOrEmpty(soundVariableName))
            {
                soundVariable = comp.RegisterOnVariableChange(soundVariableName, internalProp, SoundClipCallback);
                // Initialize the audio.
                SoundClipCallback();
            }
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
        /// Callback that allows changing the audio clip attached to this player.
        /// </summary>
        private void SoundClipCallback()
        {
            audioSource.Stop();

            AudioClip clip = GameDatabase.Instance.GetAudioClip(soundVariable.String());
            if (clip == null)
            {
                Utility.LogErrorMessage(this, "Unable to load audio clip '{0}'.", soundVariable.String());
                hasAudioClip = false;
            }
            else
            {
                audioSource.clip = clip;
                hasAudioClip = true;
                PlayAudio();
            }
        }

        /// <summary>
        /// Callback to change audio playback volume.
        /// </summary>
        /// <param name="newVolume"></param>
        private void VolumeCallback(double newVolume)
        {
            volume = Mathf.Clamp01((float)newVolume);
            audioSource.volume = GameSettings.SHIP_VOLUME * volume;
        }

        /// <summary>
        /// Callback to change audio playback pitch.
        /// </summary>
        /// <param name="newPitch"></param>
        private void PitchCallback(double newPitch)
        {
            pitch = (float)newPitch;
            audioSource.pitch = pitch;
        }

        /// <summary>
        /// Update the audio play state.
        /// </summary>
        private void PlayAudio()
        {
            if (currentState)
            {
                if (playbackTrigger != PlaybackMode.OFF)
                {
                    if (hasAudioClip)
                    {
                        audioSource.Play();
                    }
                }
                else
                {
                    if (!mustPlayOnce)
                    {
                        audioSource.Stop();
                    }
                }
            }
            else if (playbackTrigger == PlaybackMode.ON || playbackTrigger == PlaybackMode.LOOP)
            {
                if (!mustPlayOnce)
                {
                    audioSource.Stop();
                }
            }
            else if (hasAudioClip)
            {
                audioSource.Play();
            }
        }

        /// <summary>
        /// Variable callback used to update the audio source when it is playing.
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
                if (hasAudioClip == true)
                {
                    // No audio clip: return early.
                    PlayAudio();
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
        /// Release resources
        /// </summary>
        public void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            GameEvents.OnCameraChange.Remove(OnCameraChange);
            comp.UnregisterNumericVariable(pitchVariableName, internalProp, PitchCallback);
            comp.UnregisterNumericVariable(variableName, internalProp, VariableCallback);
            comp.UnregisterNumericVariable(volumeVariableName, internalProp, VolumeCallback);
            if (!string.IsNullOrEmpty(soundVariableName))
            {
                comp.UnregisterOnVariableChange(soundVariableName, internalProp, SoundClipCallback);
            }
            audioSource.Stop();
            audioSource.clip = null;
            audioSource = null;
        }
    }
}
