﻿/*****************************************************************************
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AvionicsSystems
{
    internal class MASPageCompoundText : IMASMonitorComponent
    {
        private string name = "anonymous";

        private GameObject rootObject;
        private CompoundPageText[] textElements;

        private VariableRegistrar registeredVariables;
        private Vector3 textOrigin = Vector3.zero;
        private Vector2 position = Vector2.zero;
        private float lineAdvance;
        private int maxLines;

        private MASFlightComputer.Variable range1, range2;
        private readonly bool rangeMode;
        private bool currentState;

        private bool coroutineActive = false;
        private MASFlightComputer comp;

        internal MASPageCompoundText(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
        {
            registeredVariables = new VariableRegistrar(comp, prop);
            this.comp = comp;

            if (!config.TryGetValue("name", ref name))
            {
                name = "anonymous";
            }

            if (!config.TryGetValue("maxLines", ref maxLines))
            {
                throw new ArgumentException("Missing 'maxLines' in COMPOUND_TEXT " + name);
            }

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

            lineAdvance = fontSize.y;

            Color32 textColor;
            string textColorStr = string.Empty;
            if (!config.TryGetValue("textColor", ref textColorStr) || string.IsNullOrEmpty(textColorStr))
            {
                textColor = monitor.textColor_;
            }
            else
            {
                textColor = Utility.ParseColor32(textColorStr, comp);
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
                    throw new ArgumentException("position does not contain 2 values in COMPOUND_TEXT " + name);
                }

                registeredVariables.RegisterNumericVariable(positions[0], (double newValue) =>
                {
                    position.x = (float)newValue * monitor.fontSize.x;
                    rootObject.transform.position = textOrigin + new Vector3(position.x, -position.y, 0.0f);
                });

                registeredVariables.RegisterNumericVariable(positions[1], (double newValue) =>
                {
                    position.y = (float)newValue * monitor.fontSize.y;
                    rootObject.transform.position = textOrigin + new Vector3(position.x, -position.y, 0.0f);
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

            List<CompoundPageText> textNodes = new List<CompoundPageText>();
            ConfigNode[] textConfigNodes = config.GetNodes("TEXT");
            foreach (ConfigNode textNode in textConfigNodes)
            {
                CompoundPageText cpt = new CompoundPageText();

                if (!textNode.TryGetValue("name", ref cpt.name))
                {
                    cpt.name = "anonymous";
                }

                string variableName = string.Empty;
                if (!textNode.TryGetValue("variable", ref variableName))
                {
                    variableName.Trim();
                }

                string range = string.Empty;
                if (textNode.TryGetValue("range", ref range))
                {
                    string[] ranges = Utility.SplitVariableList(range);
                    if (ranges.Length != 2)
                    {
                        throw new ArgumentException("Incorrect number of values in 'range' in COMPOUND_TEXT " + name + " node " + cpt.name);
                    }
                    cpt.range1 = comp.GetVariable(ranges[0], prop);
                    cpt.range2 = comp.GetVariable(ranges[1], prop);

                    cpt.rangeMode = true;
                }
                else
                {
                    cpt.rangeMode = false;
                }

                string text = string.Empty;
                if (textNode.TryGetValue("text", ref text))
                {
                    cpt.textObject = new GameObject();
                    cpt.textObject.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name + textNodes.Count, cpt.name, (int)(-depth / MASMonitor.depthDelta));
                    cpt.textObject.layer = rootObject.layer;
                    cpt.textObject.transform.parent = rootObject.transform;
                    cpt.textObject.transform.position = rootObject.transform.position;

                    cpt.textMesh = cpt.textObject.AddComponent<MdVTextMesh>();
                    cpt.textMesh.material = new Material(MASLoader.shaders["MOARdV/TextMonitor"]);
                    cpt.textMesh.SetFont(font, fontSize);
                    cpt.textMesh.SetColor(textColor);
                    cpt.textMesh.material.SetFloat(Shader.PropertyToID("_EmissiveFactor"), 1.0f);
                    cpt.textMesh.fontStyle = style;

                    // text, immutable, preserveWhitespace, comp, prop
                    cpt.textMesh.SetText(text, false, true, comp, prop);
                }

                // Process callbacks
                if (!string.IsNullOrEmpty(variableName))
                {
                    if (cpt.textObject != null)
                    {
                        cpt.textObject.SetActive(false);
                    }
                    registeredVariables.RegisterNumericVariable(variableName, (double newValue) => VariableCallback(newValue, cpt));
                }
                else
                {
                    cpt.currentState = true;
                    if (!coroutineActive)
                    {
                        coroutineActive = true;
                        comp.StartCoroutine(TextMethodUpdate());
                    }
                }

                textNodes.Add(cpt);
            }
            textElements = textNodes.ToArray();

            string masterVariableName = string.Empty;
            if (config.TryGetValue("variable", ref masterVariableName))
            {
                rootObject.SetActive(false);

                string range = string.Empty;
                if (config.TryGetValue("range", ref range))
                {
                    string[] ranges = Utility.SplitVariableList(range);
                    if (ranges.Length != 2)
                    {
                        throw new ArgumentException("Incorrect number of values in 'range' in COMPOUND_TEXT " + name);
                    }
                    range1 = comp.GetVariable(ranges[0], prop);
                    range2 = comp.GetVariable(ranges[1], prop);

                    rangeMode = true;
                }
                else
                {
                    rangeMode = false;
                }

                registeredVariables.RegisterNumericVariable(masterVariableName, VariableCallback);
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
            if (rangeMode)
            {
                newValue = (newValue.Between(range1.SafeValue(), range2.SafeValue())) ? 1.0 : 0.0;
            }

            bool newState = (newValue > 0.0);

            if (newState != currentState)
            {
                currentState = newState;
                rootObject.SetActive(currentState);
            }
        }

        /// <summary>
        /// Check to see if the RPM module has updated its text.
        /// </summary>
        /// <returns></returns>
        private IEnumerator TextMethodUpdate()
        {
            yield return MASConfig.waitForFixedUpdate;

            int numActiveLines = 0;
            Vector3 newPosition = Vector3.zero;
            for (int i = 0; i < textElements.Length; ++i)
            {
                if (numActiveLines < maxLines && textElements[i].currentState == true)
                {
                    if (textElements[i].textObject != null)
                    {
                        textElements[i].textObject.SetActive(true);
                        textElements[i].textObject.transform.position = rootObject.transform.position + newPosition;
                    }

                    newPosition.y -= lineAdvance;
                    ++numActiveLines;
                }
                else if (textElements[i].textObject != null)
                {
                    textElements[i].textObject.SetActive(false);
                }
            }
            coroutineActive = false;
        }

        /// <summary>
        /// Handle a changed value
        /// </summary>
        /// <param name="newValue"></param>
        private void VariableCallback(double newValue, CompoundPageText cpt)
        {
            if (cpt.rangeMode)
            {
                newValue = (newValue.Between(cpt.range1.SafeValue(), cpt.range2.SafeValue())) ? 1.0 : 0.0;
            }

            bool newState = (newValue > 0.0);

            if (newState != cpt.currentState)
            {
                cpt.currentState = newState;
                if (cpt.textObject != null)
                {
                    cpt.textObject.SetActive(cpt.currentState);
                }
                if (!coroutineActive)
                {
                    coroutineActive = true;
                    comp.StartCoroutine(TextMethodUpdate());
                }
            }
        }

        /// <summary>
        /// Called with `true` prior to the page rendering.  Called with
        /// `false` after the page completes rendering.
        /// </summary>
        /// <param name="enable">true indicates that the page is about to
        /// be rendered.  false indicates that the page has completed rendering.</param>
        public void RenderPage(bool enable)
        {
            for (int i = textElements.Length - 1; i >= 0; --i)
            {
                if (textElements[i].textMesh != null)
                {
                    textElements[i].textMesh.SetRenderEnabled(enable);
                }
            }
        }

        /// <summary>
        /// Called with `true` when the page is active on the monitor, called with
        /// `false` when the page is no longer active.
        /// </summary>
        /// <param name="enable">true when the page is actively displayed, false when the page
        /// is no longer displayed.</param>
        public void SetPageActive(bool enable)
        {
        }

        /// <summary>
        /// Handle a softkey event.
        /// </summary>
        /// <param name="keyId">The numeric ID of the key to handle.</param>
        /// <returns>true if the component handled the key, false otherwise.</returns>
        public bool HandleSoftkey(int keyId)
        {
            return false;
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
            this.comp = null;

            UnityEngine.GameObject.Destroy(rootObject);
            rootObject = null;

            registeredVariables.ReleaseResources(comp, internalProp);
            textElements = null;
        }

        internal class CompoundPageText
        {
            internal string name;
            internal GameObject textObject;
            internal MdVTextMesh textMesh;
            internal MASFlightComputer.Variable range1, range2;
            internal bool rangeMode;
            internal bool currentState;
        };
    }
}
