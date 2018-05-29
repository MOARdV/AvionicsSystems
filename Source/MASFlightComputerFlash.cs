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
//using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    public partial class MASFlightComputer : PartModule
    {
        private Dictionary<float, FlashModule> flashModule = new Dictionary<float,FlashModule>();

        private class FlashModule
        {
            internal float period;
            internal bool state;
            internal event Action<bool> flashCallbacks;

            internal void Toggle()
            {
                try
                {
                    state = !state;
                    flashCallbacks.Invoke(state);
                }
                catch
                {
                    Utility.LogError(this, "Exception caught in {0:0.00} - callback no longer valid?", period);
                }
            }
        };

        /// <summary>
        /// Quantize the flash period so multiple flashes that are near to each
        /// other in period use the same result.
        /// </summary>
        /// <param name="period"></param>
        /// <returns></returns>
        static private float QuantizePeriod(float period)
        {
            return Mathf.Floor(period * 30.0f) / 30.0f;
        }

        /// <summary>
        /// Register a callback that is notified each time the flash toggles on
        /// or off.
        /// </summary>
        /// <param name="period"></param>
        /// <param name="callback"></param>
        internal void RegisterFlashCallback(float period, Action<bool> callback)
        {
            period = QuantizePeriod(period);

            if(flashModule.ContainsKey(period))
            {
                flashModule[period].flashCallbacks += callback;
                callback(flashModule[period].state);
            }
            else
            {
                FlashModule fm = new FlashModule();
                fm.period = period;
                fm.state = false;
                fm.flashCallbacks += callback;
                flashModule.Add(period,fm);
                callback(false);

                StartCoroutine(FlashCoroutine(fm));
            }
        }

        /// <summary>
        /// Unregister the flash callback
        /// </summary>
        /// <param name="period"></param>
        /// <param name="callback"></param>
        internal void UnregisterFlashCallback(float period, Action<bool> callback)
        {
            period = QuantizePeriod(period);
            if (flashModule.ContainsKey(period))
            {
                flashModule[period].flashCallbacks -= callback;
            }
        }

        /// <summary>
        /// Coroutine for flashing
        /// </summary>
        /// <param name="fm"></param>
        /// <returns></returns>
        private IEnumerator FlashCoroutine(FlashModule fm)
        {
            while(fm.period > 0.0f)
            {
                yield return new WaitForSeconds(fm.period / TimeWarp.CurrentRate);
                fm.Toggle();
            }
        }
    }
}
