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
    class MASPageOrbitDisplay : IMASMonitorComponent
    {
        private string name = "anonymous";

        private GameObject imageObject;
        private GameObject cameraObject;
        private MeshRenderer rentexRenderer;
        private Material imageMaterial;
        private RenderTexture displayRenTex;
        private Camera orbitCamera;
        private static readonly int orbitLayer = 29;

        // 1/2 width and 1/2 height.
        private Vector2 size;
        private VariableRegistrar variableRegistrar;
        private MASFlightComputer comp;
        
        /// <summary>
        /// This angle provides the reference direction to the vessel's argument of periapsis.
        /// We need this to make sure other orbits are correctly rotated to the same frame
        /// of reference as the vessel's orbit.
        /// </summary>
        private double normalizingAngle;

        private readonly int vertexCount;
        private Vector3[] vesselVertices;
        private GameObject vesselOrigin;
        private LineRenderer vesselRenderer;
        private KeplerianElements vesselOrbit = new KeplerianElements();
        private Color vesselStartColor = XKCDColors.White;
        private Color vesselEndColor = XKCDColors.White;

        private Vector3[] targetVertices;
        private GameObject targetOrigin;
        private LineRenderer targetRenderer;
        private KeplerianElements targetOrbit = new KeplerianElements();
        private Color targetStartColor = XKCDColors.White;
        private Color targetEndColor = XKCDColors.White;
        private bool targetValid;

        private Vector3[] maneuverVertices;
        private GameObject maneuverOrigin;
        private LineRenderer maneuverRenderer;
        private KeplerianElements maneuverOrbit = new KeplerianElements();
        private Color maneuverStartColor = XKCDColors.White;
        private Color maneuverEndColor = XKCDColors.White;
        private bool maneuverValid;

        private Vector3[] bodyVertices;
        private GameObject bodyOrigin;
        private LineRenderer bodyRenderer;
        private Color bodyColor = XKCDColors.White;
        private bool useBodyColor = false;
        private CelestialBody lastBody = null;

        private Vector3[] atmoVertices;
        private GameObject atmoOrigin;
        private LineRenderer atmoRenderer;
        private Color atmoColor = XKCDColors.White;
        private bool useAtmoColor = false;

        private MASFlightComputer.Variable range1, range2;
        private readonly bool rangeMode;
        private bool currentState;

        internal static readonly float maxDepth = 1.0f - depthDelta;
        internal static readonly float minDepth = 0.5f;
        internal static readonly float depthDelta = 1.0f / 256.0f;

        /// <summary>
        /// Initialize everything
        /// </summary>
        /// <param name="config"></param>
        /// <param name="prop"></param>
        /// <param name="comp"></param>
        /// <param name="monitor"></param>
        /// <param name="pageRoot"></param>
        /// <param name="depth"></param>
        internal MASPageOrbitDisplay(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
        {
            variableRegistrar = new VariableRegistrar(comp, prop);
            this.comp = comp;

            if (!config.TryGetValue("name", ref name))
            {
                name = "anonymous";
            }

            Vector2 position = Vector2.zero;
            if (!config.TryGetValue("position", ref position))
            {
                throw new ArgumentException("Unable to find 'position' in ORBIT_DISPLAY " + name);
            }

            size = Vector2.zero;
            if (!config.TryGetValue("size", ref size))
            {
                throw new ArgumentException("Unable to find 'size' in ORBIT_DISPLAY " + name);
            }
            if (Mathf.Approximately(size.x, 0.0f) || Mathf.Approximately(size.y, 0.0f))
            {
                throw new ArgumentException("Invalid 'size' in ORBIT_DISPLAY " + name);
            }

            float orbitWidth = 1.0f;
            if (!config.TryGetValue("orbitWidth", ref orbitWidth))
            {
                orbitWidth = 1.0f;
            }
            float bodyWidth = 1.0f;
            if (!config.TryGetValue("bodyWidth", ref bodyWidth))
            {
                bodyWidth = 1.0f;
            }

            if (!config.TryGetValue("vertexCount", ref vertexCount))
            {
                throw new ArgumentException("Unable to find 'vertexCount' in ORBIT_DISPLAY " + name);
            }
            if (vertexCount < 3)
            {
                throw new ArgumentException("'vertexCount' needs to be at least 3 in ORBIT_DISPLAY " + name);
            }
            vesselVertices = new Vector3[vertexCount];
            targetVertices = new Vector3[vertexCount];
            maneuverVertices = new Vector3[vertexCount];
            bodyVertices = new Vector3[vertexCount];
            atmoVertices = new Vector3[vertexCount];

            string variableName = string.Empty;
            if (config.TryGetValue("variable", ref variableName))
            {
                variableName = variableName.Trim();
            }

            string range = string.Empty;
            if (config.TryGetValue("range", ref range))
            {
                string[] ranges = Utility.SplitVariableList(range);
                if (ranges.Length != 2)
                {
                    throw new ArgumentException("Incorrect number of values in 'range' in ORBIT_DISPLAY " + name);
                }
                range1 = comp.GetVariable(ranges[0], prop);
                range2 = comp.GetVariable(ranges[1], prop);

                rangeMode = true;
            }
            else
            {
                rangeMode = false;
            }

            // Set up our display surface.
            int renTexX = (int)size.x;
            Utility.LastPowerOf2(ref renTexX, 64, 1024);
            int renTexY = (int)size.y;
            Utility.LastPowerOf2(ref renTexY, 64, 1024);

            // Need 1/2 sizes for the rest of this:
            size = size * 0.5f;

            displayRenTex = new RenderTexture(renTexX, renTexY, 24, RenderTextureFormat.ARGB32);
            displayRenTex.Create();
            displayRenTex.DiscardContents();

            // MASMonitor display object
            imageObject = new GameObject();
            imageObject.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name, (int)(-depth / MASMonitor.depthDelta));
            imageObject.layer = pageRoot.gameObject.layer;
            imageObject.transform.parent = pageRoot;
            imageObject.transform.position = pageRoot.position;
            imageObject.transform.Translate(monitor.screenSize.x * -0.5f + position.x + size.x, monitor.screenSize.y * 0.5f - position.y - size.y, depth);
            // add renderer stuff
            MeshFilter meshFilter = imageObject.AddComponent<MeshFilter>();
            rentexRenderer = imageObject.AddComponent<MeshRenderer>();
            Mesh mesh = new Mesh();
            mesh.vertices = new[]
                {
                    new Vector3(-size.x, size.y, 0.0f),
                    new Vector3(size.x, size.y, 0.0f),
                    new Vector3(-size.x, -size.y, 0.0f),
                    new Vector3(size.x, -size.y, 0.0f),
                };
            mesh.uv = new[]
                {
                    new Vector2(0.0f, 1.0f),
                    Vector2.one,
                    Vector2.zero,
                    new Vector2(1.0f, 0.0f),
                };
            mesh.triangles = new[] 
                {
                    0, 1, 2,
                    1, 3, 2
                };
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            meshFilter.mesh = mesh;
            imageMaterial = new Material(Shader.Find("KSP/Alpha/Unlit Transparent"));
            imageMaterial.mainTexture = displayRenTex;
            rentexRenderer.material = imageMaterial;

            // cameraObject
            cameraObject = new GameObject();
            cameraObject.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name, (int)(-depth / MASMonitor.depthDelta));
            cameraObject.layer = pageRoot.gameObject.layer;
            cameraObject.transform.parent = pageRoot;
            cameraObject.transform.position = pageRoot.position;
            orbitCamera = cameraObject.AddComponent<Camera>();
            orbitCamera.enabled = true;
            orbitCamera.orthographic = true;
            orbitCamera.aspect = size.x / size.y;
            orbitCamera.eventMask = 0;
            orbitCamera.farClipPlane = 1.0f + depthDelta;
            orbitCamera.nearClipPlane = depthDelta;
            orbitCamera.orthographicSize = size.y;
            orbitCamera.cullingMask = 1 << orbitLayer;
            //orbitCamera.backgroundColor = new Color(0.0f, 0.0f, 0.4f, 1.0f);
            orbitCamera.backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
            orbitCamera.clearFlags = CameraClearFlags.SolidColor;
            orbitCamera.transparencySortMode = TransparencySortMode.Orthographic;
            orbitCamera.transform.position = Vector3.zero;
            orbitCamera.transform.LookAt(new Vector3(0.0f, 0.0f, maxDepth), Vector3.up);
            orbitCamera.targetTexture = displayRenTex;

            // lineRenderers
            float lineDepth = 1.0f;
            Shader lineShader = MASLoader.shaders["MOARdV/Monitor"];

            // vessel orbit
            vesselOrigin = new GameObject();
            vesselOrigin.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name + "vessel", (int)(-lineDepth / MASMonitor.depthDelta));
            vesselOrigin.layer = orbitLayer;// pageRoot.gameObject.layer;
            vesselOrigin.transform.parent = cameraObject.transform;
            vesselOrigin.transform.position = cameraObject.transform.position;
            vesselOrigin.transform.Translate(0.0f, 0.0f, lineDepth);

            vesselRenderer = vesselOrigin.AddComponent<LineRenderer>();
            vesselRenderer.useWorldSpace = false;
            vesselRenderer.material = new Material(lineShader);
            vesselRenderer.startColor = vesselStartColor;
            vesselRenderer.endColor = vesselEndColor;
            vesselRenderer.startWidth = orbitWidth;
            vesselRenderer.endWidth = orbitWidth;
            vesselRenderer.positionCount = vertexCount;
            vesselRenderer.loop = true;
            lineDepth -= 0.0625f;

            // target
            targetOrigin = new GameObject();
            targetOrigin.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name + "target", (int)(-lineDepth / MASMonitor.depthDelta));
            targetOrigin.layer = orbitLayer;// pageRoot.gameObject.layer;
            targetOrigin.transform.parent = cameraObject.transform;
            targetOrigin.transform.position = cameraObject.transform.position;
            targetOrigin.transform.Translate(0.0f, 0.0f, lineDepth);

            targetRenderer = targetOrigin.AddComponent<LineRenderer>();
            targetRenderer.useWorldSpace = false;
            targetRenderer.material = new Material(lineShader);
            targetRenderer.startColor = targetStartColor;
            targetRenderer.endColor = targetEndColor;
            targetRenderer.startWidth = orbitWidth;
            targetRenderer.endWidth = orbitWidth;
            targetRenderer.positionCount = vertexCount;
            targetRenderer.loop = true;
            lineDepth -= 0.0625f;

            // Maneuver
            maneuverOrigin = new GameObject();
            maneuverOrigin.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name + "maneuver", (int)(-lineDepth / MASMonitor.depthDelta));
            maneuverOrigin.layer = orbitLayer;// pageRoot.gameObject.layer;
            maneuverOrigin.transform.parent = cameraObject.transform;
            maneuverOrigin.transform.position = cameraObject.transform.position;
            maneuverOrigin.transform.Translate(0.0f, 0.0f, lineDepth);

            maneuverRenderer = maneuverOrigin.AddComponent<LineRenderer>();
            maneuverRenderer.useWorldSpace = false;
            maneuverRenderer.material = new Material(lineShader);
            maneuverRenderer.startColor = targetStartColor;
            maneuverRenderer.endColor = targetEndColor;
            maneuverRenderer.startWidth = orbitWidth;
            maneuverRenderer.endWidth = orbitWidth;
            maneuverRenderer.positionCount = vertexCount;
            maneuverRenderer.loop = true;
            lineDepth -= 0.0625f;

            // vessel.mainBody
            bodyOrigin = new GameObject();
            bodyOrigin.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name + "body", (int)(-lineDepth / MASMonitor.depthDelta));
            bodyOrigin.layer = orbitLayer;
            bodyOrigin.transform.parent = cameraObject.transform;
            bodyOrigin.transform.position = cameraObject.transform.position;
            bodyOrigin.transform.Translate(0.0f, 0.0f, lineDepth);

            bodyRenderer = bodyOrigin.AddComponent<LineRenderer>();
            bodyRenderer.useWorldSpace = false;
            bodyRenderer.material = new Material(lineShader);
            bodyRenderer.startColor = bodyColor;
            bodyRenderer.endColor = bodyColor;
            bodyRenderer.startWidth = bodyWidth;
            bodyRenderer.endWidth = bodyWidth;
            bodyRenderer.positionCount = vertexCount;
            bodyRenderer.loop = true;
            lineDepth -= 0.0625f;

            // vessel.mainBody atmosphere
            atmoOrigin = new GameObject();
            atmoOrigin.name = Utility.ComposeObjectName(pageRoot.gameObject.name, this.GetType().Name, name + "atmo", (int)(-lineDepth / MASMonitor.depthDelta));
            atmoOrigin.layer = orbitLayer;
            atmoOrigin.transform.parent = cameraObject.transform;
            atmoOrigin.transform.position = cameraObject.transform.position;
            atmoOrigin.transform.Translate(0.0f, 0.0f, lineDepth);

            atmoRenderer = atmoOrigin.AddComponent<LineRenderer>();
            atmoRenderer.useWorldSpace = false;
            atmoRenderer.material = new Material(lineShader);
            atmoRenderer.startColor = atmoColor;
            atmoRenderer.endColor = atmoColor;
            atmoRenderer.startWidth = bodyWidth;
            atmoRenderer.endWidth = bodyWidth;
            atmoRenderer.positionCount = vertexCount;
            atmoRenderer.loop = true;
            lineDepth -= 0.0625f;

            // Load the colors.
            InitVesselColor(config);
            InitTargetColor(config);
            InitManeuverColor(config);
            InitBodyColor(config);
            InitAtmoColor(config);

            if (!string.IsNullOrEmpty(variableName))
            {
                // Disable the lines if we're in variable mode
                imageObject.SetActive(false);
                currentState = false;
                //vesselOrigin.SetActive(false);
                variableRegistrar.RegisterNumericVariable(variableName, VariableCallback);
            }
            else
            {
                imageObject.SetActive(true);
                currentState = true;
            }

            Camera.onPreCull += CameraPrerender;
            Camera.onPostRender += CameraPostrender;
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
                imageObject.SetActive(currentState);
            }
        }

        #region Color Initializers
        /// <summary>
        /// Process `vesselStartColor` and `vesselEndColor` as applicable.
        /// </summary>
        /// <param name="config">Config node.</param>
        private void InitVesselColor(ConfigNode config)
        {
            string vesselStartColorString = string.Empty;
            string vesselEndColorString = string.Empty;
            config.TryGetValue("vesselEndColor", ref vesselEndColorString);

            if (config.TryGetValue("vesselStartColor", ref vesselStartColorString))
            {
                if (string.IsNullOrEmpty(vesselEndColorString))
                {
                    Color32 color;
                    if (comp.TryGetNamedColor(vesselStartColorString, out color))
                    {
                        vesselStartColor = color;
                        vesselRenderer.startColor = vesselStartColor;
                        vesselRenderer.endColor = vesselStartColor;
                    }
                    else
                    {
                        string[] vesselColors = Utility.SplitVariableList(vesselStartColorString);
                        if (vesselColors.Length < 3 || vesselColors.Length > 4)
                        {
                            throw new ArgumentException("vesselStartColor does not contain 3 or 4 values in ORBIT_DISPLAY " + name);
                        }

                        variableRegistrar.RegisterNumericVariable(vesselColors[0], (double newValue) =>
                        {
                            vesselStartColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            vesselRenderer.startColor = vesselStartColor;
                            vesselRenderer.endColor = vesselStartColor;
                        });
                        variableRegistrar.RegisterNumericVariable(vesselColors[1], (double newValue) =>
                        {
                            vesselStartColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            vesselRenderer.startColor = vesselStartColor;
                            vesselRenderer.endColor = vesselStartColor;
                        });
                        variableRegistrar.RegisterNumericVariable(vesselColors[2], (double newValue) =>
                        {
                            vesselStartColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            vesselRenderer.startColor = vesselStartColor;
                            vesselRenderer.endColor = vesselStartColor;
                        });

                        if (vesselColors.Length == 4)
                        {
                            variableRegistrar.RegisterNumericVariable(vesselColors[3], (double newValue) =>
                            {
                                vesselStartColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                                vesselRenderer.startColor = vesselStartColor;
                                vesselRenderer.endColor = vesselStartColor;
                            });
                        }
                    }
                }
                else
                {
                    Color32 color;
                    if (comp.TryGetNamedColor(vesselStartColorString, out color))
                    {
                        vesselStartColor = color;
                        vesselRenderer.startColor = vesselStartColor;
                    }
                    else
                    {
                        string[] vesselColors = Utility.SplitVariableList(vesselStartColorString);
                        if (vesselColors.Length < 3 || vesselColors.Length > 4)
                        {
                            throw new ArgumentException("vesselStartColor does not contain 3 or 4 values in ORBIT_DISPLAY " + name);
                        }

                        variableRegistrar.RegisterNumericVariable(vesselColors[0], (double newValue) =>
                        {
                            vesselStartColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            vesselRenderer.startColor = vesselStartColor;
                        });
                        variableRegistrar.RegisterNumericVariable(vesselColors[1], (double newValue) =>
                        {
                            vesselStartColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            vesselRenderer.startColor = vesselStartColor;
                        });
                        variableRegistrar.RegisterNumericVariable(vesselColors[2], (double newValue) =>
                        {
                            vesselStartColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            vesselRenderer.startColor = vesselStartColor;
                        });

                        if (vesselColors.Length == 4)
                        {
                            variableRegistrar.RegisterNumericVariable(vesselColors[3], (double newValue) =>
                            {
                                vesselStartColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                                vesselRenderer.startColor = vesselStartColor;
                            });
                        }
                    }

                    if (comp.TryGetNamedColor(vesselEndColorString, out color))
                    {
                        vesselEndColor = color;
                        vesselRenderer.endColor = vesselEndColor;
                    }
                    else
                    {
                        string[] vesselColors = Utility.SplitVariableList(vesselEndColorString);
                        if (vesselColors.Length < 3 || vesselColors.Length > 4)
                        {
                            throw new ArgumentException("vesselEndColor does not contain 3 or 4 values in ORBIT_DISPLAY " + name);
                        }

                        variableRegistrar.RegisterNumericVariable(vesselColors[0], (double newValue) =>
                        {
                            vesselEndColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            vesselRenderer.endColor = vesselEndColor;
                        });
                        variableRegistrar.RegisterNumericVariable(vesselColors[1], (double newValue) =>
                        {
                            vesselEndColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            vesselRenderer.endColor = vesselEndColor;
                        });
                        variableRegistrar.RegisterNumericVariable(vesselColors[2], (double newValue) =>
                        {
                            vesselEndColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            vesselRenderer.endColor = vesselEndColor;
                        });

                        if (vesselColors.Length == 4)
                        {
                            variableRegistrar.RegisterNumericVariable(vesselColors[3], (double newValue) =>
                            {
                                vesselEndColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                                vesselRenderer.endColor = vesselEndColor;
                            });
                        }
                    }
                }
            }
            else if (!string.IsNullOrEmpty(vesselEndColorString))
            {
                throw new ArgumentException("vesselEndColor found, but no vesselStartColor in ORBIT_DISPLAY " + name);
            }
        }

        /// <summary>
        /// Process `targetStartColor` and `targetEndColor` as applicable.
        /// </summary>
        /// <param name="config">Config node.</param>
        private void InitTargetColor(ConfigNode config)
        {
            string targetStartColorString = string.Empty;
            string targetEndColorString = string.Empty;
            config.TryGetValue("targetEndColor", ref targetEndColorString);

            if (config.TryGetValue("targetStartColor", ref targetStartColorString))
            {
                if (string.IsNullOrEmpty(targetEndColorString))
                {
                    Color32 color;
                    if (comp.TryGetNamedColor(targetStartColorString, out color))
                    {
                        targetStartColor = color;
                        targetRenderer.startColor = targetStartColor;
                        targetRenderer.endColor = targetStartColor;
                    }
                    else
                    {
                        string[] targetColors = Utility.SplitVariableList(targetStartColorString);
                        if (targetColors.Length < 3 || targetColors.Length > 4)
                        {
                            throw new ArgumentException("targetStartColor does not contain 3 or 4 values in ORBIT_DISPLAY " + name);
                        }

                        variableRegistrar.RegisterNumericVariable(targetColors[0], (double newValue) =>
                        {
                            targetStartColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            targetRenderer.startColor = targetStartColor;
                            targetRenderer.endColor = targetStartColor;
                        });
                        variableRegistrar.RegisterNumericVariable(targetColors[1], (double newValue) =>
                        {
                            targetStartColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            targetRenderer.startColor = targetStartColor;
                            targetRenderer.endColor = targetStartColor;
                        });
                        variableRegistrar.RegisterNumericVariable(targetColors[2], (double newValue) =>
                        {
                            targetStartColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            targetRenderer.startColor = targetStartColor;
                            targetRenderer.endColor = targetStartColor;
                        });

                        if (targetColors.Length == 4)
                        {
                            variableRegistrar.RegisterNumericVariable(targetColors[3], (double newValue) =>
                            {
                                targetStartColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                                targetRenderer.startColor = targetStartColor;
                                targetRenderer.endColor = targetStartColor;
                            });
                        }
                    }
                }
                else
                {
                    Color32 color;
                    if (comp.TryGetNamedColor(targetStartColorString, out color))
                    {
                        targetStartColor = color;
                        targetRenderer.startColor = targetStartColor;
                    }
                    else
                    {
                        string[] targetColors = Utility.SplitVariableList(targetStartColorString);
                        if (targetColors.Length < 3 || targetColors.Length > 4)
                        {
                            throw new ArgumentException("targetStartColor does not contain 3 or 4 values in ORBIT_DISPLAY " + name);
                        }

                        variableRegistrar.RegisterNumericVariable(targetColors[0], (double newValue) =>
                        {
                            targetStartColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            targetRenderer.startColor = targetStartColor;
                        });
                        variableRegistrar.RegisterNumericVariable(targetColors[1], (double newValue) =>
                        {
                            targetStartColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            targetRenderer.startColor = targetStartColor;
                        });
                        variableRegistrar.RegisterNumericVariable(targetColors[2], (double newValue) =>
                        {
                            targetStartColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            targetRenderer.startColor = targetStartColor;
                        });

                        if (targetColors.Length == 4)
                        {
                            variableRegistrar.RegisterNumericVariable(targetColors[3], (double newValue) =>
                            {
                                targetStartColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                                targetRenderer.startColor = targetStartColor;
                            });
                        }
                    }

                    if (comp.TryGetNamedColor(targetEndColorString, out color))
                    {
                        targetEndColor = color;
                        targetRenderer.endColor = targetEndColor;
                    }
                    else
                    {
                        string[] vesselColors = Utility.SplitVariableList(targetEndColorString);
                        if (vesselColors.Length < 3 || vesselColors.Length > 4)
                        {
                            throw new ArgumentException("targetEndColor does not contain 3 or 4 values in ORBIT_DISPLAY " + name);
                        }

                        variableRegistrar.RegisterNumericVariable(vesselColors[0], (double newValue) =>
                        {
                            targetEndColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            targetRenderer.endColor = targetEndColor;
                        });
                        variableRegistrar.RegisterNumericVariable(vesselColors[1], (double newValue) =>
                        {
                            targetEndColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            targetRenderer.endColor = targetEndColor;
                        });
                        variableRegistrar.RegisterNumericVariable(vesselColors[2], (double newValue) =>
                        {
                            targetEndColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            targetRenderer.endColor = targetEndColor;
                        });

                        if (vesselColors.Length == 4)
                        {
                            variableRegistrar.RegisterNumericVariable(vesselColors[3], (double newValue) =>
                            {
                                targetEndColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                                targetRenderer.endColor = targetEndColor;
                            });
                        }
                    }
                }
            }
            else if (!string.IsNullOrEmpty(targetEndColorString))
            {
                throw new ArgumentException("targetEndColor found, but no targetStartColor in ORBIT_DISPLAY " + name);
            }
        }

        /// <summary>
        /// Process `maneuverStartColor` and `maneuverEndColor` as applicable.
        /// </summary>
        /// <param name="config">Config node.</param>
        private void InitManeuverColor(ConfigNode config)
        {
            string maneuverStartColorString = string.Empty;
            string maneuverEndColorString = string.Empty;
            config.TryGetValue("maneuverEndColor", ref maneuverEndColorString);

            if (config.TryGetValue("maneuverStartColor", ref maneuverStartColorString))
            {
                if (string.IsNullOrEmpty(maneuverEndColorString))
                {
                    Color32 color;
                    if (comp.TryGetNamedColor(maneuverStartColorString, out color))
                    {
                        maneuverStartColor = color;
                        maneuverRenderer.startColor = maneuverStartColor;
                        maneuverRenderer.endColor = maneuverStartColor;
                    }
                    else
                    {
                        string[] maneuverColors = Utility.SplitVariableList(maneuverStartColorString);
                        if (maneuverColors.Length < 3 || maneuverColors.Length > 4)
                        {
                            throw new ArgumentException("maneuverStartColor does not contain 3 or 4 values in ORBIT_DISPLAY " + name);
                        }

                        variableRegistrar.RegisterNumericVariable(maneuverColors[0], (double newValue) =>
                        {
                            maneuverStartColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            maneuverRenderer.startColor = maneuverStartColor;
                            maneuverRenderer.endColor = maneuverStartColor;
                        });
                        variableRegistrar.RegisterNumericVariable(maneuverColors[1], (double newValue) =>
                        {
                            maneuverStartColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            maneuverRenderer.startColor = maneuverStartColor;
                            maneuverRenderer.endColor = maneuverStartColor;
                        });
                        variableRegistrar.RegisterNumericVariable(maneuverColors[2], (double newValue) =>
                        {
                            maneuverStartColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            maneuverRenderer.startColor = maneuverStartColor;
                            maneuverRenderer.endColor = maneuverStartColor;
                        });

                        if (maneuverColors.Length == 4)
                        {
                            variableRegistrar.RegisterNumericVariable(maneuverColors[3], (double newValue) =>
                            {
                                maneuverStartColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                                maneuverRenderer.startColor = maneuverStartColor;
                                maneuverRenderer.endColor = maneuverStartColor;
                            });
                        }
                    }
                }
                else
                {
                    Color32 color;
                    if (comp.TryGetNamedColor(maneuverStartColorString, out color))
                    {
                        maneuverStartColor = color;
                        maneuverRenderer.startColor = maneuverStartColor;
                    }
                    else
                    {
                        string[] targetColors = Utility.SplitVariableList(maneuverStartColorString);
                        if (targetColors.Length < 3 || targetColors.Length > 4)
                        {
                            throw new ArgumentException("maneuverStartColor does not contain 3 or 4 values in ORBIT_DISPLAY " + name);
                        }

                        variableRegistrar.RegisterNumericVariable(targetColors[0], (double newValue) =>
                        {
                            maneuverStartColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            maneuverRenderer.startColor = maneuverStartColor;
                        });
                        variableRegistrar.RegisterNumericVariable(targetColors[1], (double newValue) =>
                        {
                            maneuverStartColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            maneuverRenderer.startColor = maneuverStartColor;
                        });
                        variableRegistrar.RegisterNumericVariable(targetColors[2], (double newValue) =>
                        {
                            maneuverStartColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            maneuverRenderer.startColor = maneuverStartColor;
                        });

                        if (targetColors.Length == 4)
                        {
                            variableRegistrar.RegisterNumericVariable(targetColors[3], (double newValue) =>
                            {
                                maneuverStartColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                                maneuverRenderer.startColor = maneuverStartColor;
                            });
                        }
                    }

                    if (comp.TryGetNamedColor(maneuverEndColorString, out color))
                    {
                        maneuverEndColor = color;
                        maneuverRenderer.endColor = maneuverEndColor;
                    }
                    else
                    {
                        string[] vesselColors = Utility.SplitVariableList(maneuverEndColorString);
                        if (vesselColors.Length < 3 || vesselColors.Length > 4)
                        {
                            throw new ArgumentException("maneuverEndColor does not contain 3 or 4 values in ORBIT_DISPLAY " + name);
                        }

                        variableRegistrar.RegisterNumericVariable(vesselColors[0], (double newValue) =>
                        {
                            maneuverEndColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            maneuverRenderer.endColor = maneuverEndColor;
                        });
                        variableRegistrar.RegisterNumericVariable(vesselColors[1], (double newValue) =>
                        {
                            maneuverEndColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            maneuverRenderer.endColor = maneuverEndColor;
                        });
                        variableRegistrar.RegisterNumericVariable(vesselColors[2], (double newValue) =>
                        {
                            maneuverEndColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            maneuverRenderer.endColor = maneuverEndColor;
                        });

                        if (vesselColors.Length == 4)
                        {
                            variableRegistrar.RegisterNumericVariable(vesselColors[3], (double newValue) =>
                            {
                                maneuverEndColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                                maneuverRenderer.endColor = maneuverEndColor;
                            });
                        }
                    }
                }
            }
            else if (!string.IsNullOrEmpty(maneuverEndColorString))
            {
                throw new ArgumentException("maneuverEndColor found, but no maneuverStartColor in ORBIT_DISPLAY " + name);
            }
        }

        /// <summary>
        /// Process optional `bodyColor`.
        /// </summary>
        /// <param name="config">Config node.</param>
        private void InitBodyColor(ConfigNode config)
        {
            string bodyColorString = string.Empty;

            if (config.TryGetValue("bodyColor", ref bodyColorString))
            {
                Color32 color;
                if (comp.TryGetNamedColor(bodyColorString, out color))
                {
                    bodyColor = color;
                    bodyRenderer.startColor = bodyColor;
                    bodyRenderer.endColor = bodyColor;
                }
                else
                {
                    string[] bodyColors = Utility.SplitVariableList(bodyColorString);
                    if (bodyColors.Length < 3 || bodyColors.Length > 4)
                    {
                        throw new ArgumentException("bodyColor does not contain 3 or 4 values in ORBIT_DISPLAY " + name);
                    }

                    variableRegistrar.RegisterNumericVariable(bodyColors[0], (double newValue) =>
                    {
                        bodyColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        bodyRenderer.startColor = bodyColor;
                        bodyRenderer.endColor = bodyColor;
                    });
                    variableRegistrar.RegisterNumericVariable(bodyColors[1], (double newValue) =>
                    {
                        bodyColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        bodyRenderer.startColor = bodyColor;
                        bodyRenderer.endColor = bodyColor;
                    });
                    variableRegistrar.RegisterNumericVariable(bodyColors[2], (double newValue) =>
                    {
                        bodyColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        bodyRenderer.startColor = bodyColor;
                        bodyRenderer.endColor = bodyColor;
                    });

                    if (bodyColors.Length == 4)
                    {
                        variableRegistrar.RegisterNumericVariable(bodyColors[3], (double newValue) =>
                        {
                            bodyColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            bodyRenderer.startColor = bodyColor;
                            bodyRenderer.endColor = bodyColor;
                        });
                    }
                }
                useBodyColor = true;
            }
        }

        /// <summary>
        /// Process optional `atmoColor`.
        /// </summary>
        /// <param name="config">Config node.</param>
        private void InitAtmoColor(ConfigNode config)
        {
            string atmoColorString = string.Empty;

            if (config.TryGetValue("atmoColor", ref atmoColorString))
            {
                Color32 color;
                if (comp.TryGetNamedColor(atmoColorString, out color))
                {
                    atmoColor = color;
                    atmoRenderer.startColor = atmoColor;
                    atmoRenderer.endColor = atmoColor;
                }
                else
                {
                    string[] atmoColors = Utility.SplitVariableList(atmoColorString);
                    if (atmoColors.Length < 3 || atmoColors.Length > 4)
                    {
                        throw new ArgumentException("atmoColor  does not contain 3 or 4 values in ORBIT_DISPLAY " + name);
                    }

                    variableRegistrar.RegisterNumericVariable(atmoColors[0], (double newValue) =>
                    {
                        atmoColor.r = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        atmoRenderer.startColor = atmoColor;
                        atmoRenderer.endColor = atmoColor;
                    });
                    variableRegistrar.RegisterNumericVariable(atmoColors[1], (double newValue) =>
                    {
                        atmoColor.g = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        atmoRenderer.startColor = atmoColor;
                        atmoRenderer.endColor = atmoColor;
                    });
                    variableRegistrar.RegisterNumericVariable(atmoColors[2], (double newValue) =>
                    {
                        atmoColor.b = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                        atmoRenderer.startColor = atmoColor;
                        atmoRenderer.endColor = atmoColor;
                    });

                    if (atmoColors.Length == 4)
                    {
                        variableRegistrar.RegisterNumericVariable(atmoColors[3], (double newValue) =>
                        {
                            atmoColor.a = Mathf.Clamp01((float)newValue * (1.0f / 255.0f));
                            atmoRenderer.startColor = atmoColor;
                            atmoRenderer.endColor = atmoColor;
                        });
                    }
                }
                useAtmoColor = true;
            }
        }
        #endregion

        /// <summary>
        /// Simple circle generator.  Used for the reference body and atmosphere (where applicable).
        /// </summary>
        /// <param name="verts">Vertex array</param>
        /// <param name="radius">Radius of the circle in pixels.</param>
        private void GenerateCircle(ref Vector3[] verts, float radius)
        {
            // This is wrong: It assumes startTheta and endTheta are measured from the center, but the
            // true anomaly is measured from the focus.
            float theta = 0.0f;
            float radiansPerVertex = Mathf.PI * 2.0f / (float)vertexCount;
            for (int i = 0; i < vertexCount; ++i)
            {
                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);
                theta += radiansPerVertex;

                verts[i].x = radius * cosTheta;
                verts[i].y = radius * sinTheta;
                verts[i].z = 0.0f;
            }
        }

        /// <summary>
        /// Generate all or part of an ellipse.  This method renders from the orbital focus, which is treated as the origin.
        /// It uses True Anomaly as the angle of measurement.
        /// </summary>
        /// <param name="verts">Vertex array</param>
        /// <param name="semiMajorAxis">Semi-major axis of the ellipse, in pixels.</param>
        /// <param name="eccentricity">Eccentricity of the ellipse.</param>
        /// <param name="startTA">Starting True Anomaly to render.</param>
        /// <param name="endTA">Ending True Anomaly to render.</param>
        private void GenerateEllipse(ref Vector3[] verts, float semiMajorAxis, float eccentricity, float startTA, float endTA)
        {
            float numerator = semiMajorAxis * (1.0f - eccentricity * eccentricity);

            // True Anomaly of 0 is where the vessel crosses the periapsis.  Since we're putting the
            // periapsis on the left, but we're leaving the winding of the ellipse computation alone,
            // we subtract pi from the TA.
            float theta = startTA - Mathf.PI;
            float radiansPerVertex = (endTA - startTA) / (float)vertexCount;
            for (int i = 0; i < vertexCount; ++i)
            {
                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);
                float distance = numerator / (1.0f - eccentricity * cosTheta);
                theta += radiansPerVertex;

                verts[i].x = distance * cosTheta;
                verts[i].y = distance * sinTheta;
                verts[i].z = 0.0f;
            }
        }

        /// <summary>
        /// Evaluate whether the Keplerian orbital elements in our local cache are close enough
        /// to the latest parameters in the associated Orbit.  If not, update cached values and
        /// signal that vertices need re-evaluated.
        /// </summary>
        /// <param name="lastValue">Last cached value, updated by this method as needed.</param>
        /// <param name="orbit">Reference orbit.</param>
        /// <returns>true if the values are close enough to matching, false if they differ significantly.</returns>
        private bool OrbitsMatch(ref KeplerianElements lastValue, Orbit orbit, bool spewDebug = false)
        {
            bool match = true;
            if (Math.Abs(lastValue.eccentricity - orbit.eccentricity) > 0.001)
            {
                match = false;
                lastValue.eccentricity = orbit.eccentricity;
            }
            if (Math.Abs(lastValue.semiMajorAxis - orbit.semiMajorAxis) > 10.0) // 10 m tolerance  Probably safe to use a larger value
            {
                match = false;
                lastValue.semiMajorAxis = orbit.semiMajorAxis;
                lastValue.semiMinorAxis = orbit.semiMinorAxis;
            }
            if (Math.Abs(lastValue.inclination - orbit.inclination) > 1.0) // degrees
            {
                match = false;
                lastValue.inclination = orbit.inclination;
            }
            if (Math.Abs(lastValue.LAN - orbit.LAN) > 1.0) // degrees
            {
                match = false;
                lastValue.LAN = orbit.LAN;
            }
            if (Math.Abs(lastValue.argumentOfPeriapsis - orbit.argumentOfPeriapsis) > 1.0) // degrees
            {
                match = false;
                lastValue.argumentOfPeriapsis = orbit.argumentOfPeriapsis;
            }

            double startTA;
            if (orbit.StartUT > Planetarium.GetUniversalTime())
            {
                startTA = orbit.TrueAnomalyAtUT(orbit.StartUT);
            }
            else
            {
                startTA = orbit.trueAnomaly;
            }
            if (spewDebug) Utility.LogMessage(this, "startTA is {0:0.00}", startTA);
            if (Math.Abs(lastValue.startTrueAnomaly - startTA) > (Math.PI / 256.0)) // units of radians
            {
                match = false;
                lastValue.startTrueAnomaly = startTA;

                // For *whatever* reason, planetary orbits will use INITIAL patch transition for their end...
                // Since they don't actually initialize the start / end UT like normal orbits *either*,
                // I can't even use the secondary path below to fill everything in.
                if (orbit.patchEndTransition == Orbit.PatchTransitionType.FINAL || orbit.patchEndTransition == Orbit.PatchTransitionType.INITIAL)
                {
                    lastValue.endTrueAnomaly = lastValue.startTrueAnomaly + 2.0 * Math.PI;
                    if (spewDebug) Utility.LogMessage(this, "endTA is {0:0.00} because patchEnd is closed", lastValue.endTrueAnomaly);
                    lastValue.closedEllipse = true;
                }
                else
                {
                    lastValue.endTrueAnomaly = orbit.TrueAnomalyAtUT(orbit.EndUT);
                    if (spewDebug) Utility.LogMessage(this, "endTA is {0:0.00} because endUT is {1:0} and patchEnd is {2} (start is {3})", lastValue.endTrueAnomaly, orbit.EndUT, orbit.patchEndTransition, orbit.patchStartTransition);
                    lastValue.closedEllipse = false;
                    if(lastValue.endTrueAnomaly < lastValue.startTrueAnomaly)
                    {
                        lastValue.endTrueAnomaly += 2.0 * Math.PI;
                    }
                }
            }

            return match;
        }

        /// <summary>
        /// Compute the scaling factor that will fit the desired orbits onto the screen.
        /// </summary>
        /// <returns></returns>
        private double ComputeScaling(double focusOffset)
        {
            double xLimit = size.x / 1.01;
            double yLimit = size.y / 1.01;

            if (double.IsNaN(vesselOrbit.semiMajorAxis))
            {
                return 0.0;
            }

            // Being computed: The scalar that fits the orbits we're tracking onto the screen.
            // Need to account for the center of the screen being the center of the vessel
            // orbit, but the focus being offset.
            double metersToPixels = double.MaxValue;
            // Fit the vessel's orbit
            if (vesselOrbit.eccentricity < 1.0)
            {
                // Elliptical orbit
                metersToPixels = Math.Min(metersToPixels, xLimit / vesselOrbit.semiMajorAxis);
                metersToPixels = Math.Min(metersToPixels, yLimit / vesselOrbit.semiMinorAxis);
            }
            else
            {
                // Hyperbolic orbit - TODO
            }

            if (targetValid)
            {
                // !!! Need to transform the orbit according to the difference in the
                // argument of periapsis!  Bounds check against all four sides.
                if (targetOrbit.eccentricity < 1.0)
                {
                    // Elliptical orbit
                    metersToPixels = Math.Min(metersToPixels, xLimit / targetOrbit.semiMajorAxis);
                    metersToPixels = Math.Min(metersToPixels, yLimit / targetOrbit.semiMinorAxis);
                }
                else
                {
                    // Hyperbolic orbit - TODO
                }
            }

            if (maneuverValid)
            {
                // !!! Need to transform the orbit according to the difference in the
                // argument of periapsis!  Bounds check against all four sides.
                if (maneuverOrbit.eccentricity < 1.0)
                {
                    // Elliptical orbit
                    metersToPixels = Math.Min(metersToPixels, xLimit / maneuverOrbit.semiMajorAxis);
                    metersToPixels = Math.Min(metersToPixels, yLimit / maneuverOrbit.semiMinorAxis);
                }
                else
                {
                    // Hyperbolic orbit - TODO
                }
            }

            // Fit the body we're orbiting, accounting for its offset due to being at a focus of the ellipse
            if (lastBody.atmosphere)
            {
                metersToPixels = Math.Min(metersToPixels, yLimit / (lastBody.Radius + lastBody.atmosphereDepth));
                metersToPixels = Math.Min(metersToPixels, xLimit / (lastBody.Radius + lastBody.atmosphereDepth + focusOffset));
            }
            else
            {
                metersToPixels = Math.Min(metersToPixels, yLimit / lastBody.Radius);
                metersToPixels = Math.Min(metersToPixels, xLimit / (lastBody.Radius + focusOffset));
            }

            return metersToPixels;
        }

        /// <summary>
        /// Adjust RGB channels so one of them is 1.0.  Adjust alpha to 1.0.  Used because
        /// the OrbitDriver colors are fairly dim and have an alpha of about 0.5.
        /// </summary>
        /// <param name="colorIn"></param>
        /// <returns></returns>
        static private Color GainColor(Color colorIn)
        {
            float gain = Mathf.Max(colorIn.r, colorIn.g);
            gain = Mathf.Max(gain, colorIn.b);
            gain = 1.0f / gain;

            colorIn.r *= gain;
            colorIn.g *= gain;
            colorIn.b *= gain;
            colorIn.a = 1.0f;

            return colorIn;
        }

        /// <summary>
        /// Helper function to shift the origin of the orbital line game objects to
        /// the focus.
        /// </summary>
        /// <param name="o"></param>
        /// <param name="displacementX"></param>
        static private void ShiftOrigin(GameObject o, float displacementX, float rotation)
        {
            //if (Mathf.Approximately(rotation, 0.0f))
            {
                // Is there a more efficient way to do this?
                // Move to origin
                Vector3 pos = o.transform.position;
                pos.x = 0.0f;
                o.transform.position = pos;

                // rotate
                o.transform.rotation = Quaternion.Euler(0.0f, 0.0f, rotation);

                // move from origin
                pos.x = displacementX;
                o.transform.position = pos;
            }
            //else
            //{
            //    Vector3 pos = o.transform.position;
            //    pos.x = displacementX;
            //    o.transform.position = pos;
            //}
        }

        /// <summary>
        /// Update all of the vertices.
        /// </summary>
        private void UpdateVertices()
        {
            bool invalidateVertices = false;

            // Do this step first to make sure lastBody is current
            if (lastBody != comp.vc.mainBody)
            {
                invalidateVertices = true; 
                lastBody = comp.vc.mainBody;

                if (!useBodyColor)
                {
                    OrbitRendererData ord;
                    if (PSystemManager.OrbitRendererDataCache.TryGetValue(lastBody, out ord))
                    {
                        bodyColor = GainColor(ord.orbitColor);
                    }
                    else
                    {
                        bodyColor = XKCDColors.White;
                    }
                }
                if (!useAtmoColor)
                {
                    if (lastBody.atmosphere)
                    {
                        atmoColor = GainColor(lastBody.atmosphericAmbientColor);
                    }
                }
                bodyRenderer.startColor = bodyColor;
                bodyRenderer.endColor = bodyColor;
                atmoRenderer.startColor = atmoColor;
                atmoRenderer.endColor = atmoColor;
            }

            // Check components for required updates
            if (!OrbitsMatch(ref vesselOrbit, comp.vessel.GetOrbit()))
            {
                invalidateVertices = true;
                normalizingAngle = Utility.NormalizeAngle(vesselOrbit.LAN + vesselOrbit.argumentOfPeriapsis);
            }

            // Only show targets that orbit the same body.
            if (comp.vc.targetType != MASVesselComputer.TargetType.None && comp.vc.targetOrbit.referenceBody == lastBody)
            {
                if (!targetValid)
                {
                    // Target valid - recalculate render scaling.
                    invalidateVertices = true;
                }

                targetValid = true;
                if (!OrbitsMatch(ref targetOrbit, comp.vc.targetOrbit))
                {
                    invalidateVertices = true;
                }
            }
            else
            {
                if (targetValid)
                {
                    // Target no longer valid - recalculate render scaling.
                    invalidateVertices = true;
                }
                targetValid = false;
            }

            // Only show maneuver nodes in the same sphere of influence.
            if (comp.vc.nodeOrbit != null && comp.vc.nodeOrbit.referenceBody == lastBody)
            {
                if (!maneuverValid)
                {
                    // Maneuver valid - recalculate render scaling.
                    invalidateVertices = true;
                }
                maneuverValid = true;
                if (!OrbitsMatch(ref maneuverOrbit, comp.vc.nodeOrbit))
                {
                    invalidateVertices = true;
                }
            }
            else
            {
                if (maneuverValid)
                {
                    // Maneuver no longer valid - recalculate render scaling.
                    invalidateVertices = true;
                }
                maneuverValid = false;
            }

            if (invalidateVertices)
            {
                double focusOffset;
                // Distance from the center of the vessel orbit to the focus
                if (vesselOrbit.eccentricity < 1.0)
                {
                    focusOffset = Math.Sqrt(vesselOrbit.semiMajorAxis * vesselOrbit.semiMajorAxis - vesselOrbit.semiMinorAxis * vesselOrbit.semiMinorAxis);
                }
                else
                {
                    focusOffset = Math.Sqrt(vesselOrbit.semiMajorAxis * vesselOrbit.semiMajorAxis + vesselOrbit.semiMinorAxis * vesselOrbit.semiMinorAxis);
                }

                double metersToPixels = ComputeScaling(focusOffset);

                // Distance from the center of the window to the focus of the orbital ellipse.
                float focusDisplacement = -(float)(focusOffset * metersToPixels);

                // TODO: Hyperbolic orbits.
                if (vesselOrbit.eccentricity < 1.0)
                {
                    GenerateEllipse(ref vesselVertices, (float)(vesselOrbit.semiMajorAxis * metersToPixels), (float)vesselOrbit.eccentricity, (float)vesselOrbit.startTrueAnomaly, (float)vesselOrbit.endTrueAnomaly);
                }
                else
                {
                    Utility.LogWarning(this, "vesselOrbit e >= 1.0 - not updated");
                }
                // else generate hyperbolic section...
                vesselRenderer.SetPositions(vesselVertices);
                vesselRenderer.loop = vesselOrbit.closedEllipse;
                ShiftOrigin(vesselOrigin, focusDisplacement, 0.0f);

                if (targetValid)
                {
                    float relativeArgPe = (float)(targetOrbit.LAN + targetOrbit.argumentOfPeriapsis - normalizingAngle);
                    Utility.LogMessage(this, "target: SMA scaled to {0:0}px, TA {1:0.00} to {2:0.00}, relative ArgPe = {3:0.00}",
                        (float)(targetOrbit.semiMajorAxis * metersToPixels),
                        targetOrbit.startTrueAnomaly,
                        targetOrbit.endTrueAnomaly,
                        relativeArgPe);
                    // TODO: Hyperbolic orbits.
                    //if (targetOrbit.eccentricity < 1.0f)
                    {
                        GenerateEllipse(ref targetVertices, (float)(targetOrbit.semiMajorAxis * metersToPixels), (float)targetOrbit.eccentricity, (float)targetOrbit.startTrueAnomaly, (float)targetOrbit.endTrueAnomaly);
                    }
                    //else
                    //{
                    //    Utility.LogWarning(this, "targetOrbit e >= 1.0 - not updated");
                    //}
                    // else generate hyperbolic section
                    targetRenderer.SetPositions(targetVertices);
                    targetRenderer.loop = targetOrbit.closedEllipse;

                    //float relativeArgPe = (float)(targetOrbit.LAN + targetOrbit.argumentOfPeriapsis - normalizingAngle);
                    ShiftOrigin(targetOrigin, focusDisplacement, relativeArgPe);
                }

                if (maneuverValid)
                {
                    // TODO: Hyperbolic orbits.
                    if (maneuverOrbit.eccentricity < 1.0f)
                    {
                        GenerateEllipse(ref maneuverVertices, (float)(maneuverOrbit.semiMajorAxis * metersToPixels), (float)maneuverOrbit.eccentricity, (float)maneuverOrbit.startTrueAnomaly, (float)maneuverOrbit.endTrueAnomaly);
                    }
                    else
                    {
                        Utility.LogWarning(this, "maneuverOrbit e >= 1.0 - not updated");
                    }
                    // else generate hyperbolic section
                    maneuverRenderer.SetPositions(maneuverVertices);
                    maneuverRenderer.loop = maneuverOrbit.closedEllipse;

                    float relativeArgPe = (float)(maneuverOrbit.LAN + maneuverOrbit.argumentOfPeriapsis - normalizingAngle);
                    ShiftOrigin(maneuverOrigin, focusDisplacement, relativeArgPe);
                }

                // Body
                GenerateCircle(ref bodyVertices, (float)(lastBody.Radius * metersToPixels));
                bodyRenderer.SetPositions(bodyVertices);
                ShiftOrigin(bodyOrigin, focusDisplacement, 0.0f);

                if (lastBody.atmosphere)
                {
                    GenerateCircle(ref atmoVertices, (float)((lastBody.Radius + lastBody.atmosphereDepth) * metersToPixels));

                    atmoRenderer.SetPositions(atmoVertices);
                    ShiftOrigin(atmoOrigin, focusDisplacement, 0.0f);
                }
            }
        }

        /// <summary>
        /// Callback called before culling - we use this to update marker positions
        /// and determine which markers (and the navball) should be rendered.
        /// </summary>
        /// <param name="whichCamera"></param>
        private void CameraPrerender(Camera whichCamera)
        {
            if (currentState == true && whichCamera == orbitCamera)
            {
                UpdateVertices();

                rentexRenderer.enabled = true;
                vesselRenderer.enabled = true;
                if (targetValid)
                {
                    targetRenderer.enabled = true;
                }
                if (maneuverValid)
                {
                    maneuverRenderer.enabled = true;
                }
                bodyRenderer.enabled = true;
                if (lastBody.atmosphere)
                {
                    atmoRenderer.enabled = true;
                }

                if (!displayRenTex.IsCreated())
                {
                    displayRenTex.Create();
                    orbitCamera.targetTexture = displayRenTex;
                    imageMaterial.mainTexture = displayRenTex;
                }
            }
        }

        /// <summary>
        /// Switch off our renderers once the navball camera is done.
        /// </summary>
        /// <param name="whichCamera"></param>
        private void CameraPostrender(Camera whichCamera)
        {
            if (whichCamera == orbitCamera)
            {
                rentexRenderer.enabled = false;
                vesselRenderer.enabled = false;
                targetRenderer.enabled = false;
                maneuverRenderer.enabled = false;
                bodyRenderer.enabled = false;
                atmoRenderer.enabled = false;
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
            rentexRenderer.enabled = enable;
        }

        /// <summary>
        /// Called with `true` when the page is active on the monitor, called with
        /// `false` when the page is no longer active.
        /// </summary>
        /// <param name="enable">true when the page is actively displayed, false when the page
        /// is no longer displayed.</param>
        public void SetPageActive(bool enable)
        {
            orbitCamera.enabled = enable;
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
            Camera.onPreCull -= CameraPrerender;
            Camera.onPostRender -= CameraPostrender;

            this.comp = null;

            UnityEngine.GameObject.Destroy(imageObject);
            imageObject = null;
            UnityEngine.Object.Destroy(imageMaterial);
            imageMaterial = null;

            UnityEngine.GameObject.Destroy(cameraObject);
            cameraObject = null;

            variableRegistrar.ReleaseResources(comp, internalProp);
        }

        /// <summary>
        /// Describes the parameters of an orbit.  Cache values so we can see if an orbit has changed,
        /// which would trigger the need to update our orbit display.
        /// </summary>
        class KeplerianElements
        {
            public double eccentricity = double.MaxValue;
            public double semiMajorAxis = double.MaxValue;
            public double inclination = double.MaxValue;
            public double LAN = double.MaxValue;
            public double argumentOfPeriapsis = double.MaxValue;
            public double startTrueAnomaly = double.MaxValue; // This is the position of the object, but not important to drawing the orbital ellipse.
            public double endTrueAnomaly;
            public double semiMinorAxis = double.MaxValue; // Not important to drawing, but handy to avoid computing it later.
            public bool closedEllipse;
        }
    }
}
