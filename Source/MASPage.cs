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
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    internal class MASPage
    {
        private List<IMASMonitorComponent> component = new List<IMASMonitorComponent>();
        private Dictionary<int, Action> softkeyAction = new Dictionary<int, Action>();
        private string name = string.Empty;
        private GameObject pageRoot;

        private static IMASMonitorComponent CreatePageComponent(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
        {
            switch(config.name)
            {
                case "CAMERA":
                    return new MASPageCamera(config, prop, comp, monitor, pageRoot, depth);
                case "ELLIPSE":
                    return new MASPageEllipse(config, prop, comp, monitor, pageRoot, depth);
                case "HORIZON":
                    return new MASPageHorizon(config, prop, comp, monitor, pageRoot, depth);
                case "HORIZONTAL_BAR":
                    return new MASPageHorizontalBar(config, prop, comp, monitor, pageRoot, depth);
                case "HORIZONTAL_STRIP":
                    return new MASPageHorizontalStrip(config, prop, comp, monitor, pageRoot, depth);
                case "IMAGE":
                    return new MASPageImage(config, prop, comp, monitor, pageRoot, depth);
                case "LINE_GRAPH":
                    return new MASPageLineGraph(config, prop, comp, monitor, pageRoot, depth);
                case "LINE_STRING":
                    return new MASPageLineString(config, prop, comp, monitor, pageRoot, depth);
                case "NAVBALL":
                    return new MASPageNavBall(config, prop, comp, monitor, pageRoot, depth);
                case "RPM_MODULE":
                    return new MASPageRpmModule(config, prop, comp, monitor, pageRoot, depth);
                case "TEXT":
                    return new MASPageText(config, prop, comp, monitor, pageRoot, depth);
                case "VERTICAL_BAR":
                    return new MASPageVerticalBar(config, prop, comp, monitor, pageRoot, depth);
                case "VERTICAL_STRIP":
                    return new MASPageVerticalStrip(config, prop, comp, monitor, pageRoot, depth);
                case "VIEWPORT":
                    return new MASPageViewport(config, prop, comp, monitor, pageRoot, depth);
                default:
                    throw new ArgumentException("Unrecognized MASPage child node " + config.name);
            }
        }

        internal MASPage(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform rootTransform)
        {
            if (!config.TryGetValue("name", ref name))
            {
                throw new ArgumentException("Invalid or missing 'name' in MASPage");
            }

            string[] softkeys = config.GetValues("softkey");
            int numSoftkeys = softkeys.Length;
            for (int i = 0; i < numSoftkeys; ++i)
            {
                string[] pair = Utility.SplitVariableList(softkeys[i]);
                if (pair.Length == 2)
                {
                    int id;
                    if(int.TryParse(pair[0], out id))
                    {
                        Action action = comp.GetAction(pair[1], prop);
                        if(action != null)
                        {
                            softkeyAction[id] = action;
                        }
                    }
                }
            }

            pageRoot = new GameObject();
            pageRoot.name = Utility.ComposeObjectName(this.GetType().Name, name, prop.propID);
            pageRoot.layer = rootTransform.gameObject.layer;
            pageRoot.transform.parent = rootTransform;
            pageRoot.transform.Translate(0.0f, 0.0f, 1.0f);

            float depth = 0.0f;
            ConfigNode[] components = config.GetNodes();
            int numComponents = components.Length;
            for (int i = 0; i < numComponents; ++i)
            {
                component.Add(CreatePageComponent(components[i], prop, comp, monitor, pageRoot.transform, depth));
                depth -= MASMonitor.depthDelta;
            }
        }

        /// <summary>
        /// Enable / disable the page from rendering.
        /// </summary>
        /// <param name="enable"></param>
        internal void EnablePage(bool enable)
        {
            pageRoot.SetActive(enable);
            int numComponents = component.Count;
            for (int i = 0; i < numComponents; ++i)
            {
                component[i].EnablePage(enable);
            }
        }

        /// <summary>
        /// Enable / disable component renderers (without disabling the game objects).
        /// </summary>
        /// <param name="enable"></param>
        internal void EnableRender(bool enable)
        {
            int numComponents = component.Count;
            for (int i = 0; i < numComponents; ++i)
            {
                component[i].EnableRender(enable);
            }
        }

        /// <summary>
        /// Handle a softkey event, either directly or by forwarding it to components of this page.
        /// </summary>
        /// <param name="keyId">The softkey id.</param>
        /// <returns>True if the key was handled, false otherwise.</returns>
        internal bool HandleSoftkey(int keyId)
        {
            Action keyHandler;
            if (softkeyAction.TryGetValue(keyId, out keyHandler))
            {
                keyHandler();
                return true;
            }
            else
            {
                int componentCount = component.Count;
                bool handled = false;
                for(int i=0; i<componentCount; ++i)
                {
                    // Submit to each component.
                    if (component[i].HandleSoftkey(keyId))
                    {
                        handled = true;
                    }
                }

                return handled;
            }
        }

        /// <summary>
        /// Notify children to release resources prior to being freed.
        /// </summary>
        /// <param name="comp"></param>
        internal void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            int numComponents = component.Count;
            for (int i = 0; i < numComponents; ++i)
            {
                component[i].ReleaseResources(comp, internalProp);
            }
            component.Clear();
        }
    }
}
