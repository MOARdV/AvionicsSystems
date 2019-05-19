/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016-2019 MOARdV
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
        internal class HitBox
        {
            private bool enabled = true;
            internal Rect bounds;
            internal Action<Vector2> onClick;
            internal Action<Vector2> onRelease;
            internal Action<Vector2> onDrag;

            internal bool IsHit(Vector2 hitCoordinate)
            {
                if (enabled && bounds.Contains(hitCoordinate))
                {
                    Vector2 scaledCoordinate = hitCoordinate - bounds.min;
                    scaledCoordinate.x = Mathf.Clamp(scaledCoordinate.x, 0.0f, bounds.max.x);
                    scaledCoordinate.y = Mathf.Clamp(scaledCoordinate.y, 0.0f, bounds.max.y);
                    if (onClick != null)
                    {
                        onClick(scaledCoordinate);
                    }
                    return true;
                }
                return false;
            }

            internal Vector2 TransformHit(Vector2 hitCoordinate)
            {
                if (bounds.Contains(hitCoordinate))
                {
                    Vector2 scaledCoordinate = hitCoordinate - bounds.min;
                    scaledCoordinate.x = Mathf.Clamp(scaledCoordinate.x, 0.0f, bounds.max.x);
                    scaledCoordinate.y = Mathf.Clamp(scaledCoordinate.y, 0.0f, bounds.max.y);
                    return scaledCoordinate;
                }
                else
                {
                    return new Vector2(-1.0f, -1.0f);
                }
            }

            internal void VariableCallback(double newValue)
            {
                bool newState = (newValue > 0.0);

                if (newState != enabled)
                {
                    enabled = newState;
                }
            }
        };

        private VariableRegistrar variableRegistrar;
        private List<IMASMonitorComponent> component = new List<IMASMonitorComponent>();
        private Dictionary<int, Action> softkeyAction = new Dictionary<int, Action>();
        private List<HitBox> hitboxActions = new List<HitBox>();
        private HitBox activeHitBox = null;
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
                case "MENU":
                    return new MASPageMenu(config, prop, comp, monitor, pageRoot, depth);
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
                case "hitbox":
                    return null; // Not an error - these nodes are handled separately.
                default:
                    Utility.LogError(config, "Unrecognized MASPage child node {0} found", config.name);
                    return null;
            }
        }

        private static bool IsTextNode(ConfigNode config)
        {
            return (config.name == "TEXT") || (config.name == "ROLLING_DIGIT") || (config.name == "COMPOUND_TEXT");
        }

        internal HitBox InitHitBox(int idx, ConfigNode hitBoxConfig, InternalProp prop, MASFlightComputer comp)
        {
            HitBox hb = new HitBox();

            string positionString = string.Empty;
            if (!hitBoxConfig.TryGetValue("position", ref positionString))
            {
                Utility.LogError(this, "Missing 'position' in hitbox for MASPage " + name);
                return null;
            }
            string[] positions = Utility.SplitVariableList(positionString);
            if (positions.Length != 2)
            {
                Utility.LogError(this, "Incorrect number of values in 'position' in hitbox for MASPage " + name);
                return null;
            }

            float x1, y1;
            if (!(float.TryParse(positions[0], out x1) && float.TryParse(positions[1], out y1)))
            {
                Utility.LogError(this, "Unable to parse 'position' in hitbox for MASPage " + name);
                return null;
            }

            string sizeString = string.Empty;
            if (!hitBoxConfig.TryGetValue("size", ref sizeString))
            {
                Utility.LogError(this, "Missing 'size' in hitbox for MASPage " + name);
                return null;
            }
            string[] sizes = Utility.SplitVariableList(sizeString);
            if (sizes.Length != 2)
            {
                Utility.LogError(this, "Incorrect number of values in 'size' in hitbox for MASPage " + name);
                return null;
            }

            float w, h;
            if (!(float.TryParse(sizes[0], out w) && float.TryParse(sizes[1], out h)))
            {
                Utility.LogError(this, "Unable to parse 'size' in hitbox for MASPage " + name);
                return null;
            }
            hb.bounds = new Rect(x1, y1, w, h);

            string onClickString = string.Empty;
            if (hitBoxConfig.TryGetValue("onClick", ref onClickString))
            {
                Action<Vector2> onClick = comp.GetColliderAction(onClickString, idx, "click", prop);
                if (onClick == null)
                {
                    Utility.LogError(this, "Unable to configure 'onClick' in hitbox for MASPage " + name);
                    return null;
                }
                hb.onClick = onClick;
            }

            string onDragString = string.Empty;
            if (hitBoxConfig.TryGetValue("onDrag", ref onDragString))
            {
                Action<Vector2> onDrag = comp.GetColliderAction(onDragString, idx, "drag", prop);
                if (onDrag == null)
                {
                    Utility.LogError(this, "Unable to configure 'onDrag' in hitbox for MASPage " + name);
                    return null;
                }
                hb.onDrag = onDrag;
            }

            string onReleaseString = string.Empty;
            if (hitBoxConfig.TryGetValue("onRelease", ref onReleaseString))
            {
                Action<Vector2> onRelease = comp.GetColliderAction(onReleaseString, idx, "release", prop);
                if (onRelease == null)
                {
                    Utility.LogError(this, "Unable to configure 'onRelease' in hitbox for MASPage " + name);
                    return null;
                }
                hb.onRelease = onRelease;
            }

            if (hb.onClick == null && hb.onDrag == null && hb.onRelease == null)
            {
                Utility.LogError(this, "No 'onClick', 'onDrag', or 'onRelease' entries in hitbox for MASPage " + name);
                return null;
            }

            string variableString = string.Empty;
            if (hitBoxConfig.TryGetValue("variable", ref variableString))
            {
                // Force the state to false to begin with.
                hb.VariableCallback(0.0);
                variableRegistrar.RegisterVariableChangeCallback(variableString, hb.VariableCallback);
            }
            return hb;
        }

        internal MASPage(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform rootTransform)
        {
            if (!config.TryGetValue("name", ref name))
            {
                throw new ArgumentException("Invalid or missing 'name' in MASPage");
            }
            variableRegistrar = new VariableRegistrar(comp, prop);

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

            ConfigNode[] hitboxes = config.GetNodes("hitbox");
            int numHitboxes = hitboxes.Length;
            for (int i = 0; i < numHitboxes; ++i)
            {
                HitBox hb = InitHitBox(i, hitboxes[i], prop, comp);
                if (hb != null)
                {
                    hitboxActions.Add(hb);
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

            if (Array.Exists(components, x => x.name == "SUB_PAGE"))
            {
                // Need to collate
                List<ConfigNode> newComponents = new List<ConfigNode>();

                for (int i = 0; i < numComponents; ++i)
                {
                    ConfigNode node = components[i];
                    if (node.name == "SUB_PAGE")
                    {
                        newComponents.AddRange(ResolveSubPage(node, monitor.fontSize));
                    }
                    else
                    {
                        newComponents.Add(node);
                    }
                }

                components = newComponents.ToArray();
                numComponents = components.Length;
            }

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
                catch (Exception e)
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

            if (numComponents > 256)
            {
                Utility.LogWarning(this, "{0} elements were used in MASPage {1}. This may exceed the number of supported elements.", numComponents, name);
            }
        }

        private List<ConfigNode> ResolveSubPage(ConfigNode subPageNode, Vector2 fontSize)
        {
            string subPageName = string.Empty;
            // Test for 'name'
            if (!subPageNode.TryGetValue("name", ref subPageName))
            {
                throw new ArgumentException("No 'name' field in SUB_PAGE found in MASPage " + name);
            }

            // Test for 'variable'
            string variableString = string.Empty;

            bool editVariable = (subPageNode.TryGetValue("variable", ref variableString));

            // Test for 'position'
            bool editPosition = false;
            string[] position = new string[2] { string.Empty, string.Empty };
            string positionString = string.Empty;
            if (subPageNode.TryGetValue("position", ref positionString))
            {
                position = Utility.SplitVariableList(positionString);
                if (position.Length != 2)
                {
                    throw new ArgumentException("Invalid number of entries in 'position' for SUB_PAGE '" + subPageName + "' in MASPage " + name);
                }
                editPosition = true;
            }

            // Find the sub page.
            List<ConfigNode> subPageNodes;
            if (!MASLoader.subPages.TryGetValue(subPageName, out subPageNodes))
            {
                throw new ArgumentException("Unable to find MAS_SUB_PAGE '" + subPageName + "' for SUB_PAGE found in MASPage " + name);
            }
            List<ConfigNode> newNodes = new List<ConfigNode>();

            for (int subPageNodeIdx = 0; subPageNodeIdx < subPageNodes.Count; ++subPageNodeIdx)
            {
                ConfigNode subNode = subPageNodes[subPageNodeIdx].CreateCopy();
                string subNodeName = string.Empty;
                if (!subNode.TryGetValue("name", ref subNodeName))
                {
                    subNodeName = "anonymous";
                }

                if (editVariable)
                {
                    string currentVariable = string.Empty;
                    if (subNode.TryGetValue("variable", ref currentVariable))
                    {
                        subNode.SetValue("variable", string.Format("({0}) and ({1})", variableString, currentVariable));
                    }
                    else
                    {
                        subNode.SetValue("variable", variableString, true);
                    }
                }

                if (editPosition)
                {
                    string currentPositionString = string.Empty;
                    if (subNode.TryGetValue("position", ref currentPositionString))
                    {
                        string[] currentPosition = Utility.SplitVariableList(currentPositionString);
                        if (currentPosition.Length != 2)
                        {
                            throw new ArgumentException("Invalid number of values in 'position' for node '" + subNodeName + "' in MAS_SUB_PAGE " + subPageName);
                        }

                        if (IsTextNode(subNode))
                        {
                            subNode.SetValue("position", string.Format("(1/{4:R}) * ({0}) + ({1}), (1/{5:R}) * ({2}) + ({3})",
                                position[0], currentPosition[0], position[1], currentPosition[1],
                                fontSize.x, fontSize.y));
                        }
                        else
                        {
                            subNode.SetValue("position", string.Format("({0}) + ({1}), ({2}) + ({3})",
                                position[0], currentPosition[0], position[1], currentPosition[1]));
                        }
                    }
                    else
                    {
                        if (IsTextNode(subNode))
                        {
                            subNode.SetValue("position", string.Format("(1/{2:R}) * {0}, (1/{3:R}) * {1}",
                                position[0], position[1],
                                fontSize.x, fontSize.y), true);
                        }
                        else
                        {
                            subNode.SetValue("position", string.Format("{0}, {1}", position[0], position[1]), true);
                        }
                    }
                }

                if (subNode.name == "SUB_PAGE")
                {
                    newNodes.AddRange(ResolveSubPage(subNode, fontSize));
                }
                else
                {
                    newNodes.Add(subNode);
                }
            }

            return newNodes;
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
        /// Handle a click/drag/release location within a collider.  This function is intended for emulating
        /// touchscreen displays.
        /// </summary>
        /// <param name="hitCoordinate">x and y coordinate of the click, as processed by the COLLIDER_ADVANCED</param>
        /// <returns>True if the click was handled, false otherwise.</returns>
        internal bool HandleTouchLocation(Vector2 hitCoordinate, EventType eventType)
        {
            if (eventType == EventType.MouseDown)
            {
                int numHitboxes = hitboxActions.Count;
                for (int i = 0; i < numHitboxes; ++i)
                {
                    if (hitboxActions[i].IsHit(hitCoordinate))
                    {
                        activeHitBox = hitboxActions[i];
                        return true;
                    }
                }
            }
            else if (activeHitBox != null)
            {
                if (eventType == EventType.MouseUp)
                {
                    if (activeHitBox.onRelease != null)
                    {
                        hitCoordinate = activeHitBox.TransformHit(hitCoordinate);
                        activeHitBox.onRelease(hitCoordinate);
                    }
                    activeHitBox = null;
                }
                else if (eventType == EventType.MouseDrag)
                {
                    if (activeHitBox.onDrag != null)
                    {
                        hitCoordinate = activeHitBox.TransformHit(hitCoordinate);
                        activeHitBox.onDrag(hitCoordinate);
                    }
                }
            }

            return false;
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
            variableRegistrar.ReleaseResources();

            int numComponents = component.Count;
            for (int i = 0; i < numComponents; ++i)
            {
                component[i].ReleaseResources(comp, internalProp);
            }
            component.Clear();
        }
    }
}
