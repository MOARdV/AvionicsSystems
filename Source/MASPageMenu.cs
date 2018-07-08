/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2018 MOARdV
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
    internal class MASPageMenu : IMASMonitorComponent
    {
        private GameObject rootObject;
        private MenuItem[] menuItems;

        private Vector3 textOrigin = Vector3.zero;
        private Vector2 position = Vector2.zero;
        private readonly float lineAdvance;
        private readonly float charAdvance;
        private int topLine;
        private readonly int maxLines;
        private readonly int upSoftkey, downSoftkey, enterSoftkey, homeSoftkey, endSoftkey;

        private Action softkeyUpAction, softkeyDownAction, softkeyHomeAction, softkeyEndAction;

        private int cursorPosition;
        private GameObject cursorObject;
        private MdVTextMesh cursorText;

        private bool updateMenu = true;

        internal MASPageMenu(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
            : base(config, prop, comp)
        {
            if (!config.TryGetValue("maxLines", ref maxLines))
            {
                maxLines = int.MaxValue;
            }
            if (maxLines < 1)
            {
                throw new ArgumentException("'maxLines' must be greater than zero in MENU " + name);
            }

            int itemPositionShift = 0;
            if (!config.TryGetValue("itemPositionShift", ref itemPositionShift))
            {
                itemPositionShift = 0;
            }

            string cursorPersistentName = string.Empty;
            if (!config.TryGetValue("cursorPersistentName", ref cursorPersistentName))
            {
                throw new ArgumentException("Missing 'cursorPersistentName' in MENU " + name);
            }

            config.TryGetValue("upSoftkey", ref upSoftkey);
            config.TryGetValue("downSoftkey", ref downSoftkey);
            config.TryGetValue("enterSoftkey", ref enterSoftkey);
            config.TryGetValue("homeSoftkey", ref homeSoftkey);
            config.TryGetValue("endSoftkey", ref endSoftkey);

            string localFonts = string.Empty;
            if (!config.TryGetValue("font", ref localFonts))
            {
                localFonts = string.Empty;
            }

            string styleStr = string.Empty;
            FontStyle style = FontStyle.Normal;
            if (config.TryGetValue("style", ref styleStr))
            {
                style = MdVTextMesh.FontStyle(styleStr);
            }
            else
            {
                style = monitor.defaultStyle;
            }

            Vector2 fontSize = Vector2.zero;
            if (!config.TryGetValue("fontSize", ref fontSize) || fontSize.x < 0.0f || fontSize.y < 0.0f)
            {
                fontSize = monitor.fontSize;
            }

            charAdvance = fontSize.x * itemPositionShift;
            lineAdvance = fontSize.y;

            Color32 cursorColor;
            string cursorColorStr = string.Empty;
            if (!config.TryGetValue("cursorColor", ref cursorColorStr) || string.IsNullOrEmpty(cursorColorStr))
            {
                cursorColor = monitor.textColor_;
            }
            else
            {
                cursorColor = Utility.ParseColor32(cursorColorStr, comp);
            }

            // Set up our text.
            textOrigin = pageRoot.position + new Vector3(monitor.screenSize.x * -0.5f, monitor.screenSize.y * 0.5f, depth);

            rootObject = new GameObject();
            rootObject.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name, (int)(-depth / MASMonitor.depthDelta));
            rootObject.layer = pageRoot.gameObject.layer;
            rootObject.transform.parent = pageRoot;
            rootObject.transform.position = textOrigin;

            string positionString = string.Empty;
            if (config.TryGetValue("position", ref positionString))
            {
                string[] positions = Utility.SplitVariableList(positionString);
                if (positions.Length != 2)
                {
                    throw new ArgumentException("position does not contain 2 values in MENU " + name);
                }

                variableRegistrar.RegisterVariableChangeCallback(positions[0], (double newValue) =>
                {
                    position.x = (float)newValue * monitor.fontSize.x;
                    rootObject.transform.position = textOrigin + new Vector3(position.x, -position.y, 0.0f);
                    updateMenu = true;
                });

                variableRegistrar.RegisterVariableChangeCallback(positions[1], (double newValue) =>
                {
                    position.y = (float)newValue * monitor.fontSize.y;
                    rootObject.transform.position = textOrigin + new Vector3(position.x, -position.y, 0.0f);
                    updateMenu = true;
                });
            }

            Font font;
            if (string.IsNullOrEmpty(localFonts))
            {
                font = monitor.defaultFont;
            }
            else
            {
                font = MASLoader.GetFont(localFonts.Trim());
            }

            cursorObject = new GameObject();
            cursorObject.name = rootObject.name + "_cursor";
            cursorObject.layer = rootObject.layer;
            cursorObject.transform.parent = rootObject.transform;
            cursorObject.transform.position = textOrigin;

            cursorText = cursorObject.AddComponent<MdVTextMesh>();
            cursorText.material = new Material(MASLoader.shaders["MOARdV/TextMonitor"]);
            cursorText.SetFont(font, fontSize);
            cursorText.SetColor(cursorColor);
            cursorText.material.SetFloat(Shader.PropertyToID("_EmissiveFactor"), 1.0f);
            cursorText.fontStyle = style;

            string cursorPrompt = string.Empty;
            config.TryGetValue("cursor", ref cursorPrompt);
            // text, immutable, preserveWhitespace, comp, prop
            cursorText.SetText(cursorPrompt, false, true, comp, prop);

            List<MenuItem> itemNodes = new List<MenuItem>();
            ConfigNode[] menuItemConfigNodes = config.GetNodes("ITEM");
            foreach (ConfigNode itemNode in menuItemConfigNodes)
            {
                try
                {
                    MenuItem cpt = new MenuItem(itemNode, rootObject, font, fontSize, style, monitor.textColor_, comp, prop, variableRegistrar, itemNodes.Count);
                    itemNodes.Add(cpt);
                }
                catch (Exception e)
                {
                    Utility.LogError(this, "Exception creating ITEM in MENU " + name);
                    Utility.LogError(this, e.ToString());
                }
            }
            if (itemNodes.Count == 0)
            {
                throw new ArgumentException("No valid ITEM nodes in MENU " + name);
            }
            menuItems = itemNodes.ToArray();
            maxLines = Math.Min(maxLines, menuItems.Length);

            variableRegistrar.RegisterVariableChangeCallback(string.Format("fc.GetPersistentAsNumber(\"{0}\")", cursorPersistentName), CursorMovedCallback);
            softkeyUpAction = comp.GetAction(string.Format("fc.AddPersistentWrapped(\"{0}\", -1, 0, {1})", cursorPersistentName, menuItems.Length), prop);
            softkeyDownAction = comp.GetAction(string.Format("fc.AddPersistentWrapped(\"{0}\", 1, 0, {1})", cursorPersistentName, menuItems.Length), prop);
            softkeyHomeAction = comp.GetAction(string.Format("fc.SetPersistent(\"{0}\", 0)", cursorPersistentName), prop);
            softkeyEndAction = comp.GetAction(string.Format("fc.SetPersistent(\"{0}\", {1})", cursorPersistentName, menuItems.Length - 1), prop);

            string masterVariableName = string.Empty;
            if (config.TryGetValue("variable", ref masterVariableName))
            {
                rootObject.SetActive(false);

                variableRegistrar.RegisterVariableChangeCallback(masterVariableName, VariableCallback);
            }
            else
            {
                rootObject.SetActive(true);
            }

            RenderPage(false);
        }

        /// <summary>
        /// Handle a changed value
        /// </summary>
        /// <param name="newValue"></param>
        private void VariableCallback(double newValue)
        {
            if (EvaluateVariable(newValue))
            {
                rootObject.SetActive(currentState);
            }
        }

        /// <summary>
        /// Note when the cursor is moved up/down.
        /// </summary>
        /// <param name="newValue"></param>
        private void CursorMovedCallback(double newValue)
        {
            int newPosition = Mathf.Clamp((int)newValue, 0, menuItems.Length - 1);
            if (newPosition != cursorPosition)
            {
                updateMenu = true;
                cursorPosition = newPosition;
            }
        }

        /// <summary>
        /// Called with `true` prior to the page rendering.  Called with
        /// `false` after the page completes rendering.
        /// </summary>
        /// <param name="enable">true indicates that the page is about to
        /// be rendered.  false indicates that the page has completed rendering.</param>
        public override void RenderPage(bool enable)
        {
            cursorText.SetRenderEnabled(enable);

            if (enable)
            {
                if (updateMenu)
                {
                    if (cursorPosition < topLine)
                    {
                        topLine = cursorPosition;
                    }
                    else if(cursorPosition >= topLine + maxLines)
                    {
                        topLine = 1 + cursorPosition - maxLines;
                    }
                    updateMenu = false;
                    // Update positions.
                    cursorObject.transform.position = (textOrigin + new Vector3(position.x, -(position.y + lineAdvance * (cursorPosition - topLine)), 0.0f));
                    
                    Vector3 rowPosition = (textOrigin + new Vector3(position.x + charAdvance, -position.y, 0.0f));
                    for (int i = 0; i < maxLines; ++i)
                    {
                        menuItems[i + topLine].UpdatePosition(rowPosition);
                        rowPosition.y -= lineAdvance;
                    }
                }
                for (int i = 0; i < maxLines; ++i)
                {
                    menuItems[i + topLine].RenderPage(enable);
                }
            }
            else
            {
                for (int i = menuItems.Length - 1; i >= 0; --i)
                {
                    menuItems[i].RenderPage(enable);
                }
            }
        }

        /// <summary>
        /// Handle a softkey event.
        /// </summary>
        /// <param name="keyId">The numeric ID of the key to handle.</param>
        /// <returns>true if the component handled the key, false otherwise.</returns>
        public override bool HandleSoftkey(int keyId)
        {
            if (keyId == upSoftkey)
            {
                softkeyUpAction();
                updateMenu = true;
                return true;
            }
            else if (keyId == downSoftkey)
            {
                softkeyDownAction();
                updateMenu = true;
                return true;
            }
            else if (keyId == homeSoftkey)
            {
                softkeyHomeAction();
                updateMenu = true;
                return true;
            }
            else if (keyId == endSoftkey)
            {
                softkeyEndAction();
                updateMenu = true;
                return true;
            }
            else if (keyId == enterSoftkey)
            {
                menuItems[cursorPosition].TriggerAction();
                updateMenu = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            UnityEngine.GameObject.Destroy(rootObject);
            rootObject = null;

            variableRegistrar.ReleaseResources();
            menuItems = null;
        }

        /// <summary>
        /// Object encapsulating a single entry on the menu.
        /// </summary>
        internal class MenuItem
        {
            internal readonly string name;

            private Action selectEvent;

            private GameObject activeObject;
            private MdVTextMesh activeText;
            private Color32 activeColor;

            private GameObject passiveObject;
            private MdVTextMesh passiveText;
            private Color32 passiveColor;

            internal GameObject disabledObject;
            internal MdVTextMesh disabledText;

            private readonly bool usesDisabled;
            private bool enabled;
            private bool active;

            internal MenuItem(ConfigNode itemNode, GameObject rootObject, Font font, Vector2 fontSize, FontStyle style, Color32 defaultColor, MASFlightComputer comp, InternalProp prop, VariableRegistrar variableRegistrar, int itemId)
            {
                string itemIdStr = itemId.ToString();

                if (!itemNode.TryGetValue("name", ref name))
                {
                    name = "anonymous";
                }

                string selectEventStr = string.Empty;
                if (itemNode.TryGetValue("selectEvent", ref selectEventStr))
                {
                    selectEventStr = selectEventStr.Replace("%ITEMID%", itemIdStr);
                    selectEvent = comp.GetAction(selectEventStr, prop);
                }

                enabled = true;

                string activeColorStr = string.Empty;
                if (!itemNode.TryGetValue("activeColor", ref activeColorStr) || string.IsNullOrEmpty(activeColorStr))
                {
                    activeColor = defaultColor;
                }
                else
                {
                    activeColor = Utility.ParseColor32(activeColorStr, comp);
                }

                string activeTextStr = string.Empty;
                if (!itemNode.TryGetValue("activeText", ref activeTextStr))
                {
                    throw new ArgumentException("Missing 'activeText' in ITEM " + name);
                }
                activeTextStr = activeTextStr.Replace("%ITEMID%", itemIdStr);

                activeObject = new GameObject();
                activeObject.name = rootObject.name + "_activeitem_" + name;
                activeObject.layer = rootObject.layer;
                activeObject.transform.parent = rootObject.transform;
                activeObject.transform.position = rootObject.transform.position;

                activeText = activeObject.AddComponent<MdVTextMesh>();
                activeText.material = new Material(MASLoader.shaders["MOARdV/TextMonitor"]);
                activeText.SetFont(font, fontSize);
                activeText.SetColor(activeColor);
                activeText.material.SetFloat(Shader.PropertyToID("_EmissiveFactor"), 1.0f);
                activeText.fontStyle = style;
                activeText.SetText(activeTextStr, false, true, comp, prop);

                string passiveColorStr = string.Empty;
                if (!itemNode.TryGetValue("passiveColor", ref passiveColorStr) || string.IsNullOrEmpty(passiveColorStr))
                {
                    passiveColor = activeColor;
                }
                else
                {
                    passiveColor = Utility.ParseColor32(passiveColorStr, comp);
                }

                string passiveTextStr = string.Empty;
                if (!itemNode.TryGetValue("passiveText", ref passiveTextStr))
                {
                    passiveObject = activeObject;
                    passiveText = activeText;
                }
                else
                {
                    passiveTextStr = passiveTextStr.Replace("%ITEMID%", itemIdStr);

                    passiveObject = new GameObject();
                    passiveObject.name = rootObject.name + "_passiveitem_" + name;
                    passiveObject.layer = rootObject.layer;
                    passiveObject.transform.parent = rootObject.transform;
                    passiveObject.transform.position = rootObject.transform.position;

                    passiveText = passiveObject.AddComponent<MdVTextMesh>();
                    passiveText.material = new Material(MASLoader.shaders["MOARdV/TextMonitor"]);
                    passiveText.SetFont(font, fontSize);
                    passiveText.SetColor(passiveColor);
                    passiveText.material.SetFloat(Shader.PropertyToID("_EmissiveFactor"), 1.0f);
                    passiveText.fontStyle = style;
                    passiveText.SetText(passiveTextStr, false, true, comp, prop);
                }

                string activeVariableStr = string.Empty;
                if (itemNode.TryGetValue("activeVariable", ref activeVariableStr))
                {
                    active = false;
                    activeVariableStr = activeVariableStr.Replace("%ITEMID%", itemIdStr);
                    variableRegistrar.RegisterVariableChangeCallback(activeVariableStr, (double newValue) => active = (newValue > 0.0));
                }
                else
                {
                    active = true;
                }

                string enabledVariableStr = string.Empty;
                if (itemNode.TryGetValue("enabledVariable", ref enabledVariableStr))
                {
                    usesDisabled = true;
                    enabled = false;
                    enabledVariableStr = enabledVariableStr.Replace("%ITEMID%", itemIdStr);
                    variableRegistrar.RegisterVariableChangeCallback(enabledVariableStr, (double newValue) => enabled = (newValue > 0.0));

                    string disabledTextStr = string.Empty;
                    if (!itemNode.TryGetValue("disabledText", ref disabledTextStr))
                    {
                        throw new ArgumentException("Missing 'disabledText' in ITEM " + name);
                    }
                    disabledTextStr = disabledTextStr.Replace("%ITEMID%", itemIdStr);

                    Color32 disabledColor;
                    string disabledColorStr = string.Empty;
                    if (!itemNode.TryGetValue("disabledColor", ref disabledColorStr) || string.IsNullOrEmpty(disabledColorStr))
                    {
                        disabledColor = defaultColor;
                    }
                    else
                    {
                        disabledColor = Utility.ParseColor32(disabledColorStr, comp);
                    }

                    disabledObject = new GameObject();
                    disabledObject.name = rootObject.name + "_disableditem_" + name;
                    disabledObject.layer = rootObject.layer;
                    disabledObject.transform.parent = rootObject.transform;
                    disabledObject.transform.position = rootObject.transform.position;

                    disabledText = disabledObject.AddComponent<MdVTextMesh>();
                    disabledText.material = new Material(MASLoader.shaders["MOARdV/TextMonitor"]);
                    disabledText.SetFont(font, fontSize);
                    disabledText.SetColor(disabledColor);
                    disabledText.material.SetFloat(Shader.PropertyToID("_EmissiveFactor"), 1.0f);
                    disabledText.fontStyle = style;
                    disabledText.SetText(disabledTextStr, false, true, comp, prop);
                }
                else 
                {
                    enabled = true;
                    usesDisabled = false;
                }
            }

            /// <summary>
            /// Conditionally trigger the action assigned to this menu item.
            /// </summary>
            internal void TriggerAction()
            {
                if (enabled && selectEvent != null)
                {
                    selectEvent();
                }
            }

            /// <summary>
            /// Update this object's position.
            /// </summary>
            /// <param name="position">New position for this item.</param>
            internal void UpdatePosition(Vector3 position)
            {
                activeObject.transform.position = position;
                passiveObject.transform.position = position;
                if (usesDisabled)
                {
                    disabledObject.transform.position = position;
                }
            }

            /// <summary>
            /// Enable and position the appropriate text object for rendering.
            /// </summary>
            /// <param name="showPage">Are we showing the page (true) or hiding it (false)?</param>
            internal void RenderPage(bool showPage)
            {
                if (showPage)
                {
                    if (enabled)
                    {
                        if (active)
                        {
                            activeText.SetRenderEnabled(true);
                            // Since I alias activeText and passiveText, I have to update
                            // the colors here.  Maybe I should simple create active and
                            // passive instances from the activeText, but that can
                            // double the number of texts that need updated.
                            activeText.SetColor(activeColor);
                        }
                        else
                        {
                            passiveText.SetRenderEnabled(true);
                            passiveText.SetColor(passiveColor);
                        }
                    }
                    else
                    {
                        if (usesDisabled)
                        {
                            disabledText.SetRenderEnabled(true);
                        }
                        else
                        {
                            if (active)
                            {
                                activeText.SetRenderEnabled(true);
                                activeText.SetColor(activeColor);
                            }
                            else
                            {
                                passiveText.SetRenderEnabled(true);
                                passiveText.SetColor(passiveColor);
                            }
                        }
                    }
                }
                else
                {
                    activeText.SetRenderEnabled(false);
                    passiveText.SetRenderEnabled(false);
                    if (usesDisabled)
                    {
                        disabledText.SetRenderEnabled(false);
                    }
                }
            }
        }
    }
}
