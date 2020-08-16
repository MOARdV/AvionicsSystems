/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2020 MOARdV
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
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    class MASComponentInternalText : IMASSubComponent
    {
        private InternalText textObj;
        private bool coroutineEnabled = false;
        MdVTextMesh.TextRow textRow;

        internal MASComponentInternalText(ConfigNode config, InternalProp prop, MASFlightComputer comp)
            : base(config, prop, comp)
        {
            string transform = string.Empty;
            if (!config.TryGetValue("transform", ref transform))
            {
                throw new ArgumentException("Missing 'transform' in INTERNAL_TEXT " + name);
            }

            string text = string.Empty;
            if (!config.TryGetValue("text", ref text))
            {
                throw new ArgumentException("Invalid or missing 'text' in INTERNAL_TEXT " + name);
            }

            Color passiveColor = Color.white;
            string passiveColorStr = string.Empty;
            if (!config.TryGetValue("passiveColor", ref passiveColorStr))
            {
                throw new ArgumentException("Invalid or missing 'passiveColor' in INTERNAL_TEXT " + name);
            }
            else
            {
                Color32 namedColor;
                if (comp.TryGetNamedColor(passiveColorStr, out namedColor))
                {
                    passiveColor = namedColor;
                }
                else
                {
                    string[] startColors = Utility.SplitVariableList(passiveColorStr);
                    if (startColors.Length < 3 || startColors.Length > 4)
                    {
                        throw new ArgumentException("passiveColor does not contain 3 or 4 values in INTERNAL_TEXT " + name);
                    }

                    float x;
                    if (!float.TryParse(startColors[0], out x))
                    {
                        throw new ArgumentException("Unable to parse passiveColor red value in INTERNAL_TEXT " + name);
                    }
                    passiveColor.r = Mathf.Clamp01(x * (1.0f / 255.0f));

                    if (!float.TryParse(startColors[1], out x))
                    {
                        throw new ArgumentException("Unable to parse passiveColor green value in INTERNAL_TEXT " + name);
                    }
                    passiveColor.g = Mathf.Clamp01(x * (1.0f / 255.0f));

                    if (!float.TryParse(startColors[2], out x))
                    {
                        throw new ArgumentException("Unable to parse passiveColor blue value in INTERNAL_TEXT " + name);
                    }
                    passiveColor.b = Mathf.Clamp01(x * (1.0f / 255.0f));

                    if (startColors.Length == 4)
                    {
                        if (!float.TryParse(startColors[3], out x))
                        {
                            throw new ArgumentException("Unable to parse passiveColor alpha value in INTERNAL_TEXT " + name);
                        }
                        passiveColor.a = Mathf.Clamp01(x * (1.0f / 255.0f));
                    }
                }
            }

            text = MdVTextMesh.UnmangleText(text);

            // See if this is a single-line text, or multi-line.  The latter is not supported here.
            string[] textRows = text.Split(Utility.LineSeparator, StringSplitOptions.RemoveEmptyEntries);
            if (textRows.Length > 1)
            {
                throw new ArgumentException("Multi-line text is not supported in INTERNAL_TEXT " + name);
            }
            text = textRows[0];

            bool mutable = false;
            if (text.Contains(MdVTextMesh.VariableListSeparator[0]) || text.Contains(MdVTextMesh.VariableListSeparator[1]))
            {
                mutable = true;
            }

            Transform textObjTransform = prop.FindModelTransform(transform);
            // TODO: Allow variable fontSize?
            float fontSize = 0.15f;
            textObj = InternalComponents.Instance.CreateText("Arial", fontSize, textObjTransform, (mutable) ? "-" : text, passiveColor, false, "TopLeft");
            try
            {
                Transform q = textObj.text.transform;
                q.Translate(0.0f, 0.0048f, 0.0f);
            }
            catch (Exception)
            {
            }

            if (mutable)
            {
                string[] sections = text.Split(MdVTextMesh.VariableListSeparator, StringSplitOptions.RemoveEmptyEntries);
                if (sections.Length != 2)
                {
                    throw new ArgumentException("Error parsing text in INTERNAL_TEXT " + name);
                }

                MdVTextMesh.TextRow tr = new MdVTextMesh.TextRow();
                tr.formatString = sections[0];

                // See if this text contains formatted rich text nudges
                if (tr.formatString.Contains("["))
                {
                    throw new ArgumentException("Formatted rich text is not supported in INTERNAL_TEXT " + name);
                }

                string[] variables = sections[1].Split(';');
                tr.variable = new Variable[variables.Length];
                tr.evals = new object[variables.Length];
                tr.callback = (double dontCare) => { tr.rowInvalidated = true; };
                for (int var = 0; var < tr.variable.Length; ++var)
                {
                    try
                    {
                        tr.variable[var] = variableRegistrar.RegisterVariableChangeCallback(variables[var], tr.callback);
                    }
                    catch (Exception e)
                    {
                        Utility.LogError(this, "Variable {0} threw an exception", variables[var]);
                        throw e;
                    }
                }
                tr.rowInvalidated = true;
                tr.EvaluateVariables();
                textObj.text.text = tr.formattedData;

                textRow = tr;

                coroutineEnabled = true;
                comp.StartCoroutine(StringUpdateCoroutine());
            }
        }

        /// <summary>
        /// Use a coroutine once per fixed update to regenerate the string, if needed.
        /// </summary>
        /// <returns></returns>
        private IEnumerator StringUpdateCoroutine()
        {
            while (coroutineEnabled)
            {
                yield return MASConfig.waitForFixedUpdate;

                if (textRow.rowInvalidated)
                {
                    textRow.EvaluateVariables();
                    textObj.text.text = textRow.formattedData;
                }
            }
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            coroutineEnabled = false;
            if (textRow.variable != null)
            {
                for (int var = 0; var < textRow.variable.Length; ++var)
                {
                    textRow.variable[var] = null;
                }
                textRow.variable = null;
                textRow.callback = null;
            }
            variableRegistrar.ReleaseResources();
        }
    }
}
