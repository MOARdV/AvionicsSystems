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
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// Polygon renderer.
    /// </summary>
    class MASPagePolygon : IMASMonitorComponent
    {
        private Color color = Color.white;

        private Vector3[] vertices;
        private GameObject polygonOrigin;
        private Material polygonMaterial;
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private bool retriangulate;

        // Only need one.  It's used one-at-a-time.
        static TriPoly triPoly = new TriPoly();

        internal MASPagePolygon(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
            : base(config, prop, comp)
        {
            string colorString = string.Empty;
            if (!config.TryGetValue("color", ref colorString))
            {
                throw new ArgumentException("Unable to find 'color' in POLYGON " + name);
            }

            string[] vertexStrings = config.GetValues("vertex");
            if (vertexStrings.Length < 3)
            {
                throw new ArgumentException("Insufficient number of 'vertex' entries in POLYGON " + name + " (must have at least 3)");
            }

            Vector2 position = Vector2.zero;
            if (!config.TryGetValue("position", ref position))
            {
                throw new ArgumentException("Unable to find 'position' in POLYGON " + name);
            }

            string variableName = string.Empty;
            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            polygonOrigin = new GameObject();
            polygonOrigin.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name, (int)(-depth / MASMonitor.depthDelta));
            polygonOrigin.layer = pageRoot.gameObject.layer;
            polygonOrigin.transform.parent = pageRoot;
            polygonOrigin.transform.position = pageRoot.position;
            polygonOrigin.transform.Translate(monitor.screenSize.x * -0.5f + position.x, monitor.screenSize.y * 0.5f - position.y, depth);

            // add renderer stuff
            MeshFilter meshFilter = polygonOrigin.AddComponent<MeshFilter>();
            meshRenderer = polygonOrigin.AddComponent<MeshRenderer>();
            mesh = new Mesh();
            meshFilter.mesh = mesh;

            int numVertices = vertexStrings.Length;
            vertices = new Vector3[numVertices];

            polygonMaterial = new Material(MASLoader.shaders["MOARdV/Monitor"]);
            polygonMaterial.color = color;
            meshRenderer.material = polygonMaterial;

            for (int i = 0; i < numVertices; ++i)
            {
                // Need to make a copy of the value for the lambda capture,
                // otherwise we'll try using i = numVertices in the callbacks.
                int index = i;
                vertices[i] = Vector3.zero;

                string[] vtx = Utility.SplitVariableList(vertexStrings[i]);
                if (vtx.Length != 2)
                {
                    throw new ArgumentException("vertex " + (i + 1).ToString() + " does not contain two values in POLYGON " + name);
                }

                variableRegistrar.RegisterNumericVariable(vtx[0], (double newValue) =>
                {
                    vertices[index].x = (float)newValue;
                    retriangulate = true;
                });

                variableRegistrar.RegisterNumericVariable(vtx[1], (double newValue) =>
                {
                    // Invert the value, since we stipulate +y is down on the monitor.
                    vertices[index].y = -(float)newValue;
                    retriangulate = true;
                });
            }
            mesh.vertices = vertices;

            // For 3 or 4 vertices, the index array is invariant.  Load it now and be done with it.
            if (numVertices == 3)
            {
                mesh.triangles = new[] 
                    {
                        0, 2, 1
                    };
            }
            else if (numVertices == 4)
            {
                mesh.triangles = new[] 
                    {
                        0, 2, 1,
                        0, 3, 2
                    };
            }

            RenderPage(false);

            string rotationVariableName = string.Empty;
            if (config.TryGetValue("rotation", ref rotationVariableName))
            {
                variableRegistrar.RegisterNumericVariable(rotationVariableName, RotationCallback);
            }

            if (!string.IsNullOrEmpty(variableName))
            {
                // Disable the mesh if we're in variable mode
                polygonOrigin.SetActive(false);
                variableRegistrar.RegisterNumericVariable(variableName, VariableCallback);
            }
            else
            {
                currentState = true;
                polygonOrigin.SetActive(true);
            }

            Color32 col;
            if (comp.TryGetNamedColor(colorString, out col))
            {
                color = col;
                polygonMaterial.color = color;
            }
            else
            {
                string[] colors = Utility.SplitVariableList(colorString);
                if (colors.Length < 3 || colors.Length > 4)
                {
                    throw new ArgumentException("color does not contain 3 or 4 values in POLYGON " + name);
                }

                variableRegistrar.RegisterNumericVariable(colors[0], (double newValue) =>
                {
                    color.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                    polygonMaterial.color = color;
                });

                variableRegistrar.RegisterNumericVariable(colors[1], (double newValue) =>
                {
                    color.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                    polygonMaterial.color = color;
                });

                variableRegistrar.RegisterNumericVariable(colors[2], (double newValue) =>
                {
                    color.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                    polygonMaterial.color = color;
                });

                if (colors.Length == 4)
                {
                    variableRegistrar.RegisterNumericVariable(colors[3], (double newValue) =>
                    {
                        color.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        polygonMaterial.color = color;
                    });
                }
            }
        }

        /// <summary>
        /// Handle a changed value
        /// </summary>
        /// <param name="newValue"></param>
        private void VariableCallback(double newValue)
        {
            if (EvaluateVariable(newValue))
            {
                polygonOrigin.SetActive(currentState);
            }
        }

        /// <summary>
        /// Apply a rotation to the line string.
        /// </summary>
        /// <param name="newValue"></param>
        private void RotationCallback(double newValue)
        {
            newValue = newValue % 360.0;
            polygonOrigin.transform.localRotation = Quaternion.Euler(new Vector3(0.0f, 0.0f, (float)newValue));
        }

        /// <summary>
        /// Called with `true` prior to the page rendering.  Called with
        /// `false` after the page completes rendering.
        /// </summary>
        /// <param name="enable">true indicates that the page is about to
        /// be rendered.  false indicates that the page has completed rendering.</param>
        public override void RenderPage(bool enable)
        {
            meshRenderer.enabled = enable;

            if (enable && retriangulate)
            {
                mesh.vertices = vertices;
                if (vertices.Length > 4)
                {
                    // Debug spew:
                    //int[] tri = triPoly.Patch(vertices);
                    //Utility.LogMessage(this, "{0} verts:", vertices.Length);
                    //foreach (Vector3 v in vertices)
                    //{
                    //    Utility.LogMessage(this, "{0}", v);
                    //}
                    //Utility.LogMessage(this, "{0} patch:", tri.Length);
                    //foreach (int i in tri)
                    //{
                    //    Utility.LogMessage(this, "{0}", i);
                    //}
                    //mesh.triangles = tri;
                    mesh.triangles = triPoly.Patch(vertices);
                }

                mesh.UploadMeshData(false);
                retriangulate = false;
            }
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public override void ReleaseResources(MASFlightComputer comp, InternalProp internalProp)
        {
            UnityEngine.Object.Destroy(polygonMaterial);
            polygonMaterial = null;
            UnityEngine.Object.Destroy(polygonOrigin);
            polygonOrigin = null;

            variableRegistrar.ReleaseResources();
        }
    }
}
