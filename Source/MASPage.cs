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
        private Action onEntry, onExit;
        private bool enabled;

        private static IMASMonitorComponent CreatePageComponent(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
        {
            switch (config.name)
            {
                case "CAMERA":
                    return new MASPageCamera(config, prop, comp, monitor, pageRoot, depth);
                case "COMPOUND_TEXT":
                    return new MASPageCompoundText(config, prop, comp, monitor, pageRoot, depth);
                case "ELLIPSE":
                    return new MASPageEllipse(config, prop, comp, monitor, pageRoot, depth);
                case "GROUND_TRACK":
                    return new MASPageGroundTrack(config, prop, comp, monitor, pageRoot, depth);
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
                case "ORBIT_DISPLAY":
                    return new MASPageOrbitDisplay(config, prop, comp, monitor, pageRoot, depth);
                case "POLYGON":
                    return new MASPagePolygon(config, prop, comp, monitor, pageRoot, depth);
                case "ROLLING_DIGIT":
                    return new MASPageRollingDigit(config, prop, comp, monitor, pageRoot, depth);
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
                    Utility.LogError(config, "Unrecognized MASPage child node {0} found", config.name);
                    return null;
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
                    if (int.TryParse(pair[0], out id))
                    {
                        Action action = comp.GetAction(pair[1], prop);
                        if (action != null)
                        {
                            softkeyAction[id] = action;
                        }
                    }
                }
            }

            string entryMethod = string.Empty;
            if (config.TryGetValue("onEntry", ref entryMethod))
            {
                onEntry = comp.GetAction(entryMethod, prop);
            }
            string exitMethod = string.Empty;
            if (config.TryGetValue("onExit", ref exitMethod))
            {
                onExit = comp.GetAction(exitMethod, prop);
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
                try
                {
                    var pageComponent = CreatePageComponent(components[i], prop, comp, monitor, pageRoot.transform, depth);
                    if (pageComponent != null)
                    {
                        component.Add(pageComponent);
                        depth -= MASMonitor.depthDelta;
                    }
                }
                catch(Exception e)
                {
                    string componentName = string.Empty;
                    if (!components[i].TryGetValue("name", ref componentName))
                    {
                        componentName = "anonymous";
                    }

                    string error = string.Format("Error configuring MASPage " + name + " " + config.name + " " + componentName + ":");
                    Utility.LogError(this, error);
                    Utility.LogError(this, "{0}", e.ToString());
                    Utility.ComplainLoudly(error);
                }
            }
        }

        /// <summary>
        /// Enable / disable the page from rendering.  Called with `true` when
        /// the page is selected, called with `false` when the page is no longer
        /// selected.
        /// </summary>
        /// <param name="enable">true when the page becomes the active page on the monitor,
        /// false when it is no longer the active page.</param>
        internal void SetPageActive(bool enable)
        {
            if (enabled != enable)
            {
                if (enable)
                {
                    if (onEntry != null)
                    {
                        onEntry();
                    }
                }
                else if (!enable)
                {
                    if (onExit != null)
                    {
                        onExit();
                    }
                }
                enabled = enable;
            }
            pageRoot.SetActive(enable);
            int numComponents = component.Count;
            for (int i = 0; i < numComponents; ++i)
            {
                component[i].SetPageActive(enable);
            }
        }

        /// <summary>
        /// Indicate the page is ready to render (that is, the monitor's camera
        /// is preparing to capture the scene), or that the page has finished
        /// rendering.
        /// </summary>
        /// <param name="enable">true indicates the camera is ready to render, so
        /// displayable objects need to be prepared and values updated.  false indicates
        /// that the camera has finished rendering, so objects should be switched
        /// back off.</param>
        internal void RenderPage(bool enable)
        {
            int numComponents = component.Count;
            for (int i = 0; i < numComponents; ++i)
            {
                component[i].RenderPage(enable);
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
                for (int i = 0; i < componentCount; ++i)
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
