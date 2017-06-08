/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2017 MOARdV
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
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// MASActionImage allows the replacement of textures in a prop.  While any texture could be replaced,
    /// the main intent of the module is to allow the display of mission flags on parts using the
    /// %FLAG% keyword.
    /// </summary>
    internal class MASActionImage : IMASSubComponent
    {
        private string name = "(anonymous)";

        internal MASActionImage(ConfigNode config, InternalProp prop, MASFlightComputer comp)
        {
            if (!config.TryGetValue("name", ref name))
            {
                name = "(anonymous)";
            }

            string textureName = string.Empty;
            if (!config.TryGetValue("texture", ref textureName))
            {
                throw new ArgumentException("Unable to find 'texture' in IMAGE " + name);
            }
            if (textureName == "%FLAG%")
            {
                textureName = prop.part.flagURL;
            }
            Texture2D mainTexture = GameDatabase.Instance.GetTexture(textureName, false);
            if (mainTexture == null)
            {
                throw new ArgumentException("Unable to find 'texture' " + textureName + " for IMAGE " + name);
            }

            string transformName = string.Empty;
            if (!config.TryGetValue("transform", ref transformName))
            {
                throw new ArgumentException("Missing 'transform' in IMAGE " + name);
            }

            Transform transform = prop.FindModelTransform(transformName);
            if (transform == null)
            {
                throw new ArgumentException("Unable to find transform \"" + transformName + "\" for IMAGE " + name);
            }

            Renderer renderer = transform.GetComponent<Renderer>();
            if (renderer == null)
            {
                throw new ArgumentException("No renderer attached to transform \"" + transformName + "\" for IMAGE " + name);
            }

            renderer.material.SetTexture("_MainTex", mainTexture);
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
        public void ReleaseResources(MASFlightComputer comp, InternalProp prop)
        {
            //comp.UnregisterNumericVariable(variableName, prop, VariableCallback);
        }
    }
}
