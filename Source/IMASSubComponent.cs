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

namespace AvionicsSystems
{
    internal abstract class IMASMonitorComponent : IMASSubComponent
    {
        internal IMASMonitorComponent(ConfigNode config, InternalProp prop, MASFlightComputer comp)
            : base(config, prop, comp)
        {

        }

        /// <summary>
        /// Called with `true` prior to the page rendering.  Called with
        /// `false` after the page completes rendering.
        /// </summary>
        /// <param name="enable">true indicates that the page is about to
        /// be rendered.  false indicates that the page has completed rendering.</param>
        public abstract void RenderPage(bool enable);

        /// <summary>
        /// Called with `true` when the page is active on the monitor, called with
        /// `false` when the page is no longer active.
        /// 
        /// Note that it is not needed to switch of the game objects in a given monitor
        /// page as long as they're attached to the MASPage's pageRoot game object.  This
        /// callback is intended to handle other required processing (such as starting / stopping
        /// a coroutine).
        /// </summary>
        /// <param name="enable">true when the page is actively displayed, false when the page
        /// is no longer displayed.</param>
        virtual public void SetPageActive(bool enable)
        {

        }

        /// <summary>
        /// Handle a softkey event.
        /// </summary>
        /// <param name="keyId">The numeric ID of the key to handle.</param>
        /// <returns>true if the component handled the key, false otherwise.</returns>
        virtual public bool HandleSoftkey(int keyId)
        {
            return false;
        }
    }

    internal abstract class IMASSubComponent
    {
        internal readonly string name;
        internal VariableRegistrar variableRegistrar;

        /// <summary>
        /// Configure the common fields.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="prop"></param>
        /// <param name="comp"></param>
        internal IMASSubComponent(ConfigNode config, InternalProp prop, MASFlightComputer comp)
        {
            variableRegistrar = new VariableRegistrar(comp, prop);
            if (!config.TryGetValue("name", ref name))
            {
                name = "anonymous";
            }
        }

        /// <summary>
        /// Optional name reported for this subcomponent.
        /// </summary>
        /// <returns>Supplied name or "anonymous"</returns>
        public string Name()
        {
            return name;
        }

        /// <summary>
        /// Release any resources obtained during the lifetime of this object.
        /// </summary>
        public abstract void ReleaseResources(MASFlightComputer comp, InternalProp prop);
    }
}
