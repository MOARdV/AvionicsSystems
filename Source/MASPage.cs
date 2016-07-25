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
    internal class MASPage
    {
        private List<IMASSubComponent> component = new List<IMASSubComponent>();
        private string name = string.Empty;
        private GameObject pageRoot;

        private static IMASSubComponent CreatePageComponent(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
        {
            if (config.name == "HORIZON")
            {
                return new MASPageHorizon(config, prop, comp, monitor, pageRoot, depth);
            }
            else if (config.name == "HORIZONTAL_BAR")
            {
                return new MASPageHorizontalBar(config, prop, comp, monitor, pageRoot, depth);
            }
            else if (config.name == "HORIZONTAL_STRIP")
            {
                return new MASPageHorizontalStrip(config, prop, comp, monitor, pageRoot, depth);
            }
            else if (config.name == "IMAGE")
            {
                return new MASPageImage(config, prop, comp, monitor, pageRoot, depth);
            }
            else if (config.name == "LINE_GRAPH")
            {
                return new MASPageLineGraph(config, prop, comp, monitor, pageRoot, depth);
            }
            else if (config.name == "NAVBALL")
            {
                return new MASPageNavBall(config, prop, comp, monitor, pageRoot, depth);
            }
            else if (config.name == "TEXT")
            {
                return new MASPageText(config, prop, comp, monitor, pageRoot, depth);
            }
            else if (config.name == "VERTICAL_STRIP")
            {
                return new MASPageVerticalStrip(config, prop, comp, monitor, pageRoot, depth);
            }
            else
            {
                throw new ArgumentException("Unrecognized MAS_PAGE component " + config.name);
            }
        }

        internal MASPage(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform rootTransform)
        {
            if (!config.TryGetValue("name", ref name))
            {
                throw new ArgumentException("Invalid or missing 'name' in MASPage");
            }

            pageRoot = new GameObject();
            pageRoot.name = "MASPage-" + prop.propID + "-" + name;
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
        /// Enable / disable the page from rendering
        /// </summary>
        /// <param name="enable"></param>
        internal void EnablePage(bool enable)
        {
            pageRoot.SetActive(enable);
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
