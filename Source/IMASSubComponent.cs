/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016 - 2017 MOARdV
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
    internal interface IMASMonitorComponent:IMASSubComponent
    {
        /// <summary>
        /// Enables / disables rendering components for between draw calls.
        /// </summary>
        /// <param name="enable"></param>
        void EnableRender(bool enable);

        /// <summary>
        /// Enables / disables overall page rendering.
        /// </summary>
        /// <param name="enable"></param>
        void EnablePage(bool enable);

        /// <summary>
        /// Handle a softkey event.
        /// </summary>
        /// <param name="keyId">The numeric ID of the key to handle.</param>
        /// <returns>true if the component handled the key, false otherwise.</returns>
        bool HandleSoftkey(int keyId);
    }

    internal interface IMASSubComponent
    {
        /// <summary>
        /// Optional name reported for this subcomponent.
        /// </summary>
        /// <returns>Supplied name or "anonymous"</returns>
        string Name();

        /// <summary>
        /// Release any resources obtained during the lifetime of this object.
        /// </summary>
        void ReleaseResources(MASFlightComputer comp, InternalProp prop);
    }
}
