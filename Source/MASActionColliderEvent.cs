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
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// The COLLIDER_EVENT manages mouse clicks on colliders.  It can support
    /// auto-repeating the onClick event (once per FixedUpdate), and it can
    /// support transient actions using onClick and onRelease.
    /// </summary>
    class MASActionColliderEvent : IMASSubComponent
    {
        private string name = "(anonymous)";
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
            private bool buttonState = false;
            internal bool autoRepeat = false;

            /// <summary>
            /// Mouse press handler.  Trigger the autorepeat event, if appropriate.
            /// </summary>
            public void OnMouseDown()
            {
                if (onClick != null)
                {
                    onClick();
                    buttonState = true;

                    if (autoRepeat)
                    {
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
                yield return new WaitForFixedUpdate();

                while (buttonState)
                {
                    onClick();
                    yield return new WaitForFixedUpdate();
                }
            }

            /// <summary>
            /// Mouse release event, cancel the autorepeat event.
            /// </summary>
            public void OnMouseUp()
            {
                if (onRelease != null)
                {
                    onRelease();
                    buttonState = false;
                }
            }
        }

        internal MASActionColliderEvent(ConfigNode config, InternalProp prop, MASFlightComputer comp)
        {
            if (!config.TryGetValue("name", ref name))
            {
                name = "(anonymous)";
            }

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

            Transform tr = prop.FindModelTransform(collider.Trim());
            if (tr == null)
            {
                throw new ArgumentException("Unable to find transform '" + collider + "' in prop for COLLIDER_EVENT " + name);
            }

            bool autoRepeat;
            if (!bool.TryParse("autoRepeat", out autoRepeat))
            {
                autoRepeat = false;
            }

            buttonObject = tr.gameObject.AddComponent<ButtonObject>();
            buttonObject.parent = this;
            buttonObject.autoRepeat = autoRepeat;

            if (!string.IsNullOrEmpty(clickEvent))
            {
                buttonObject.onClick = comp.GetAction(clickEvent, prop);
            }
            if (!string.IsNullOrEmpty(releaseEvent))
            {
                buttonObject.onRelease = comp.GetAction(releaseEvent, prop);
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
            if (buttonObject != null)
            {
                buttonObject.onClick = null;
                buttonObject.onRelease = null;
                buttonObject.parent = null;
            }
        }
    }
}
