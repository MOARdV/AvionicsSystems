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
using System.IO;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    internal class MASPageText : IMASMonitorComponent
    {
        private string name = "(anonymous)";
        private string text = string.Empty;

        private GameObject meshObject;
        private MdVTextMesh textObj;
        private string variableName;
        private MASFlightComputer.Variable range1, range2;
        private readonly bool rangeMode;
        private bool currentState;

        internal MASPageText(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
        {
            if (!config.TryGetValue("name", ref name))
            {
                name = "(anonymous)";
            }

            if (!config.TryGetValue("text", ref text))
            {
                string textfile = string.Empty;
                if (!config.TryGetValue("textfile", ref textfile))
                {
                    throw new ArgumentException("Unable to find 'text' or 'textfile' in TEXT " + name);
                }

                // Load text
                text = string.Join(Environment.NewLine, File.ReadAllLines(KSPUtil.ApplicationRootPath + "GameData/" + textfile.Trim(), Encoding.UTF8));
            }

            string localFonts = string.Empty;
            if (!config.TryGetValue("font", ref localFonts))
            {
                localFonts = string.Empty;
            }

            Vector2 fontSize = Vector2.zero;
            if (!config.TryGetValue("fontSize", ref fontSize) || fontSize.x < 0.0f || fontSize.y < 0.0f)
            {
                fontSize = monitor.fontSize;
            }

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

            Vector2 position = Vector2.zero;
            if (!config.TryGetValue("position", ref position))
            {
                position = Vector2.zero;
            }
            else
            {
                position = Vector2.Scale(position, fontSize);
            }

            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            string range = string.Empty;
            if (config.TryGetValue("range", ref range))
            {
                string[] ranges = range.Split(',');
                if (ranges.Length != 2)
                {
                    throw new ArgumentException("Incorrect number of values in 'range' in TEXT " + name);
                }
                range1 = comp.GetVariable(ranges[0], prop);
                range2 = comp.GetVariable(ranges[1], prop);

                rangeMode = true;
            }
            else
            {
                rangeMode = false;
            }

            // Set up our text.
            meshObject = new GameObject();
            meshObject.name = pageRoot.gameObject.name + "-MASPageText-" + name + "-" + depth.ToString();
            meshObject.layer = pageRoot.gameObject.layer;
            meshObject.transform.parent = pageRoot;
            meshObject.transform.position = pageRoot.position;
            meshObject.transform.Translate(monitor.screenSize.x * -0.5f + position.x, monitor.screenSize.y * 0.5f - position.y, depth);

            textObj = meshObject.gameObject.AddComponent<MdVTextMesh>();

            Font font;
            if (string.IsNullOrEmpty(localFonts))
            {
                font = monitor.defaultFont;
            }
            else
            {
                string[] selectedFonts = localFonts.Split(',');
                // TODO: Multiple fonts
                font = MASLoader.GetFont(selectedFonts[0].Trim());
            }

            // We want to use a different shader for monitor displays.
            textObj.material = new Material(MASLoader.shaders["MOARdV/TextMonitor"]);
            textObj.SetFont(font, fontSize);
            textObj.SetColor(textColor);
            textObj.material.SetFloat(Shader.PropertyToID("_EmissiveFactor"), 1.0f);

            // text, immutable, preserveWhitespace, comp
            textObj.SetText(text, false, true, comp, prop);

            if (!string.IsNullOrEmpty(variableName))
            {
                // Disable the mesh if we're in variable mode
                meshObject.SetActive(false);
                comp.RegisterNumericVariable(variableName, prop, VariableCallback);
            }
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
                meshObject.SetActive(currentState);
            }
        }

        /// <summary>
        /// Enable / disable renderer components without disabling game objects.
        /// </summary>
        /// <param name="enable"></param>
        public void EnableRender(bool enable)
        {
            textObj.SetRenderEnabled(enable);
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
            UnityEngine.GameObject.Destroy(meshObject);
            meshObject = null;
            if (!string.IsNullOrEmpty(variableName))
            {
                comp.UnregisterNumericVariable(variableName, internalProp, VariableCallback);
            }
        }
    }
}
