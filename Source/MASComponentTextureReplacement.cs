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
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// MASComponentTextureReplacement can be used to replace textures on portions of a model.
    /// </summary>
    internal class MASComponentTextureReplacement : IMASSubComponent
    {
        private Material localMaterial = null;

        internal MASComponentTextureReplacement(ConfigNode config, InternalProp prop, MASFlightComputer comp)
            : base(config, prop, comp)
        {
            string transform = string.Empty;
            if (!config.TryGetValue("transform", ref transform))
            {
                throw new ArgumentException("Missing 'transform' in TEXTURE_REPLACEMENT " + name);
            }

            string layers = "_MainTex";
            config.TryGetValue("layers", ref layers);

            Transform t = prop.FindModelTransform(transform);
            Renderer r = t.GetComponent<Renderer>();
            localMaterial = r.material;

            string[] layer = layers.Split();
            int layerLength = layer.Length;
            for (int i = 0; i < layerLength; ++i)
            {
                layer[i] = layer[i].Trim();
            }

            string textureName = string.Empty;
            if (!config.TryGetValue("texture", ref textureName))
            {
                throw new ArgumentException("Unable to find 'texture' in TEXTURE_REPLACEMENT " + name);
            }
            Texture2D mainTexture = null;
            if (textureName == "%FLAG%")
            {
                textureName = prop.part.flagURL;
            }
            mainTexture = GameDatabase.Instance.GetTexture(textureName, false);

            if (mainTexture == null)
            {
                throw new ArgumentException("Unable to find 'texture' " + textureName + " for TEXTURE_REPLACEMENT " + name);
            }

            for (int i = layer.Length - 1; i >= 0; --i)
            {
                localMaterial.SetTexture(layer[i], mainTexture);
            }
        }
        /// <summary>
        /// Release resources
        /// </summary>
        public override void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            UnityEngine.Object.Destroy(localMaterial);
        }
    }
}
