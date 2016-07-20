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
    /// <summary>
    /// The navball component renders a NavBall for a monitor.
    /// </summary>
    internal class MASPageNavBall : IMASSubComponent
    {
        private string name = "(anonymous)";
        private string text = string.Empty;

        private GameObject imageObject;
        private GameObject navballModel;
        private GameObject[] markers = new GameObject[12];
        private Material[] markerMaterial = new Material[12];
        private RenderTexture navballRenTex;
        private Camera navballCamera;
        private Material imageMaterial;
        private Material navballMaterial;
        private string variableName;
        private MASFlightComputer.Variable range1, range2;
        private readonly bool rangeMode;
        private bool currentState;
        private MASFlightComputer comp;
        private readonly float navballExtents;
        private readonly float iconDepth;
        private readonly float iconAlphaScalar;
        private static readonly int navballLayer = 29;
        private static int colorIdx = Shader.PropertyToID("_Color");
        private readonly Color[] activeMarkerColor = new Color[12];

        enum MarkerId
        {
            Prograde,
            Retrograde,
            RadialOut,
            RadialIn,
            NormalPlus,
            NormalMinus,
            ManeuverPlus,
            ManeuverMinus,
            TargetPlus,
            TargetMinus,
            DockingAlignment,
            Waypoint
        }

        internal MASPageNavBall(ConfigNode config, InternalProp prop, MASFlightComputer comp, MASMonitor monitor, Transform pageRoot, float depth)
        {
            this.comp = comp;
            if (!config.TryGetValue("name", ref name))
            {
                name = "(anonymous)";
            }

            string modelName = string.Empty;
            if (!config.TryGetValue("model", ref modelName))
            {
                throw new ArgumentException("Unable to find 'model' in NAVBALL " + name);
            }
            navballModel = GameDatabase.Instance.GetModel(modelName);
            if (navballModel == null)
            {
                throw new ArgumentException("Unable to find 'model' " + modelName + " for NAVBALL " + name);
            }
            try
            {
                Vector3 extents = navballModel.GetComponent<MeshFilter>().mesh.bounds.extents;
                navballExtents = Mathf.Max(extents.x, extents.y) * 1.01f;
            }
            catch
            {
                navballExtents = 1.0f;
            }
            iconDepth = 1.4f - navballExtents - 0.01f;
            iconAlphaScalar = 0.6f / navballExtents;
            //Utility.LogMessage(this, "navballExtents -> {0}", navballExtents);

            string textureName = string.Empty;
            if (!config.TryGetValue("texture", ref textureName))
            {
                throw new ArgumentException("Unable to find 'texture' in NAVBALL " + name);
            }
            Texture2D navballTexture = GameDatabase.Instance.GetTexture(textureName, false);
            if (navballTexture == null)
            {
                throw new ArgumentException("Unable to find 'texture' " + textureName + " for NAVBALL " + name);
            }

            Vector2 position = Vector2.zero;
            if (!config.TryGetValue("position", ref position))
            {
                position = monitor.screenSize * 0.5f;
            }

            Vector2 size = Vector2.zero;
            if (!config.TryGetValue("size", ref size))
            {
                throw new ArgumentException("Unable to find 'size' in NAVBALL " + name);
            }
            size = size * 0.5f;

            float opacity = 1.0f;
            if (!config.TryGetValue("opacity", ref opacity))
            {
                opacity = 1.0f;
            }
            else
            {
                opacity = Mathf.Clamp01(opacity);
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

            // Set up our navball renderer
            Shader displayShader = Shader.Find("KSP/Alpha/Unlit Transparent");
            navballRenTex = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            navballRenTex.Create();
            navballRenTex.DiscardContents();

            // Set up our display surface.
            imageObject = new GameObject();
            imageObject.name = pageRoot.gameObject.name + "-MASPageNavBall-" + name + "-" + depth.ToString();
            imageObject.layer = pageRoot.gameObject.layer;
            imageObject.transform.parent = pageRoot;
            imageObject.transform.position = pageRoot.position;
            imageObject.transform.Translate(monitor.screenSize.x * -0.5f + position.x, monitor.screenSize.y * 0.5f - position.y, depth);
            // add renderer stuff
            MeshFilter meshFilter = imageObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = imageObject.AddComponent<MeshRenderer>();
            Mesh mesh = new Mesh();
            mesh.vertices = new[]
                {
                    new Vector3(-size.x, size.y, depth),
                    new Vector3(size.x, size.y, depth),
                    new Vector3(-size.x, -size.y, depth),
                    new Vector3(size.x, -size.y, depth),
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
            mesh.Optimize();
            mesh.UploadMeshData(true);
            meshFilter.mesh = mesh;
            imageMaterial = new Material(displayShader);
            imageMaterial.mainTexture = navballRenTex;
            meshRenderer.material = imageMaterial;

            navballCamera = imageObject.AddComponent<Camera>();
            navballCamera.enabled = true;
            navballCamera.orthographic = true;
            navballCamera.aspect = 1.0f;
            navballCamera.eventMask = 0;
            navballCamera.farClipPlane = 13.0f;
            navballCamera.orthographicSize = navballExtents;
            navballCamera.cullingMask = 1 << navballLayer;
            // TODO: Different shader... clearing to a=0 hides the navball
            navballCamera.backgroundColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);
            //navballCamera.backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
            navballCamera.clearFlags = CameraClearFlags.SolidColor;
            navballCamera.transparencySortMode = TransparencySortMode.Orthographic;
            navballCamera.transform.LookAt(navballCamera.transform.position + new Vector3(0.0f, 0.0f, 1.0f), Vector3.up);
            navballCamera.targetTexture = navballRenTex;
            Camera.onPreRender += CameraPrerender;

            navballModel.layer = navballLayer;
            navballModel.transform.parent = imageObject.transform;
            // TODO: this isn't working when the camera is shifted.
            //navballModel.transform.Translate(new Vector3(position.x, -position.y, 2.4f));
            navballModel.transform.Translate(new Vector3(0.0f, 0.0f, 2.4f));
            Renderer navballRenderer = null;
            navballMaterial = navballModel.GetComponentCached<Renderer>(ref navballRenderer).material;
            navballMaterial.shader = displayShader;
            navballMaterial.mainTexture = navballTexture;
            navballMaterial.SetFloat("_Opacity", opacity);
            navballRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            navballModel.SetActive(true);

            InitMarkers(imageObject.transform);
            //Utility.LogMessage(this, "navball @ {0}, marker[0] @ {1}", navballModel.transform.position, markers[0].transform.position);
            //Utility.LogMessage(this, "camera @ {0}, extent = {1}, shift ~ {2}", navballCamera.transform.position, navballExtents, -navballExtents - 0.01f);

            if (!string.IsNullOrEmpty(variableName))
            {
                // Disable the mesh if we're in variable mode
                imageObject.SetActive(false);
                comp.RegisterNumericVariable(variableName, prop, VariableCallback);
            }
            else
            {
                imageObject.SetActive(true);
            }
        }

        /// <summary>
        /// Convert the Z-value of a direction vector into an alpha value for fading icons
        /// </summary>
        /// <param name="zValue"></param>
        /// <returns></returns>
        private float GetIconAlpha(float zValue)
        {
            // Current iconAlphaScalar = 0.6f / navballExtents
            return Mathf.Clamp01(zValue * iconAlphaScalar + 0.4f);
        }

        /// <summary>
        /// Callback called before rendering - we use this to update markers, etc.
        /// </summary>
        /// <param name="whichCamera"></param>
        private void CameraPrerender(Camera whichCamera)
        {
            if (whichCamera == navballCamera)
            {
                if (!navballRenTex.IsCreated())
                {
                    navballRenTex.Create();
                    navballCamera.targetTexture = navballRenTex;
                    imageMaterial.mainTexture = navballRenTex;
                }
                //navballRenTex.DiscardContents();
                // Apply navball gimbal
                navballModel.transform.rotation = comp.vc.navBallRelativeGimbal;

                Quaternion attitudeGimbal = comp.vc.navBallAttitudeGimbal;
                var speedMode = FlightGlobals.speedDisplayMode;
                if (speedMode == FlightGlobals.SpeedDisplayModes.Orbit)
                {
                    Vector3 prograde = (attitudeGimbal * comp.vc.prograde) * navballExtents;
                    markers[0].transform.localPosition = new Vector3(prograde.x, prograde.y, iconDepth);
                    activeMarkerColor[0].a = GetIconAlpha(prograde.z);
                    markerMaterial[0].SetColor(colorIdx, activeMarkerColor[0]);
                    markers[1].transform.localPosition = new Vector3(-prograde.x, -prograde.y, iconDepth);
                    activeMarkerColor[1].a = GetIconAlpha(-prograde.z);
                    markerMaterial[1].SetColor(colorIdx, activeMarkerColor[1]);

                    Vector3 radialOut = (attitudeGimbal * comp.vc.radialOut) * navballExtents;
                    markers[2].SetActive(true);
                    markers[2].transform.localPosition = new Vector3(radialOut.x, radialOut.y, iconDepth);
                    activeMarkerColor[2].a = GetIconAlpha(radialOut.z);
                    markerMaterial[2].SetColor(colorIdx, activeMarkerColor[2]);
                    markers[3].SetActive(true);
                    markers[3].transform.localPosition = new Vector3(-radialOut.x, -radialOut.y, iconDepth);
                    activeMarkerColor[3].a = GetIconAlpha(-radialOut.z );
                    markerMaterial[3].SetColor(colorIdx, activeMarkerColor[3]);

                    Vector3 normal = (attitudeGimbal * comp.vc.normal) * navballExtents;
                    markers[4].SetActive(true);
                    markers[4].transform.localPosition = new Vector3(normal.x, normal.y, iconDepth);
                    activeMarkerColor[4].a = GetIconAlpha(normal.z );
                    markerMaterial[4].SetColor(colorIdx, activeMarkerColor[4]);
                    markers[5].SetActive(true);
                    markers[5].transform.localPosition = new Vector3(-normal.x, -normal.y, iconDepth);
                    activeMarkerColor[5].a = GetIconAlpha(-normal.z );
                    markerMaterial[5].SetColor(colorIdx, activeMarkerColor[5]);
                }
                else if(speedMode == FlightGlobals.SpeedDisplayModes.Surface)
                {
                    Vector3 prograde = (attitudeGimbal * comp.vessel.srf_velocity.normalized) * navballExtents;
                    markers[0].transform.localPosition = new Vector3(prograde.x, prograde.y, iconDepth);
                    activeMarkerColor[0].a = GetIconAlpha( prograde.z );
                    markerMaterial[0].SetColor(colorIdx, activeMarkerColor[0]);
                    markers[1].transform.localPosition = new Vector3(-prograde.x, -prograde.y, iconDepth);
                    activeMarkerColor[1].a = GetIconAlpha(-prograde.z );
                    markerMaterial[1].SetColor(colorIdx, activeMarkerColor[1]);
                    for (int i = 2; i < 6; ++i)
                    {
                        markers[i].SetActive(false);
                    }
                }
                else
                {
                    // TODO:
                    Vector3 prograde = (attitudeGimbal * comp.vc.prograde) * navballExtents;
                    markers[0].transform.localPosition = new Vector3(prograde.x, prograde.y, iconDepth);
                    activeMarkerColor[0].a = GetIconAlpha(prograde.z );
                    markerMaterial[0].SetColor(colorIdx, activeMarkerColor[0]);
                    markers[1].transform.localPosition = new Vector3(-prograde.x, -prograde.y, iconDepth);
                    activeMarkerColor[1].a = GetIconAlpha(-prograde.z);
                    markerMaterial[1].SetColor(colorIdx, activeMarkerColor[1]);
                    for (int i = 2; i < 6; ++i)
                    {
                        markers[i].SetActive(false);
                    }
                }

                // Maneuver +/-
                // Target +/-
                // Docking Port
                // Waypoint

                //Utility.LogMessage(this, "[0] -> {0}", markers[0].transform.position);

                //Utility.LogMessage(this, "[1] -> {0}", markers[1].transform.position);
            }
        }

        private static readonly Vector2[] markerUV =
        {
            new Vector2(0.0f / 3.0f, 2.0f / 3.0f), // Prograde
            new Vector2(1.0f / 3.0f, 2.0f / 3.0f), // Retrograde
            new Vector2(0.0f / 3.0f, 2.0f / 3.0f), // RadialOut
            new Vector2(1.0f / 3.0f, 1.0f / 3.0f), // RadialIn
            new Vector2(0.0f / 3.0f, 1.0f / 3.0f), // NormalPlus
            new Vector2(1.0f / 3.0f, 0.0f / 3.0f), // NormalMinus
            new Vector2(2.0f / 3.0f, 0.0f / 3.0f), // ManeuverPlus
            new Vector2(1.0f / 3.0f, 2.0f / 3.0f), // ManeuverMinus
            new Vector2(2.0f / 3.0f, 1.0f / 3.0f), // TargetPlus
            new Vector2(2.0f / 3.0f, 2.0f / 3.0f), // TargetMinus
            new Vector2(0.0f / 3.0f, 2.0f / 3.0f), // DockingAlignment
            new Vector2(0.0f / 3.0f, 2.0f / 3.0f), // Waypoint
        };
        private static readonly Color[] markerColor =
        {
            XKCDColors.LimeGreen,
            XKCDColors.LimeGreen,
            XKCDColors.Cyan,
            XKCDColors.Cyan,
            XKCDColors.HotMagenta,
            XKCDColors.HotMagenta,
            XKCDColors.Magenta,
            XKCDColors.Magenta,
            XKCDColors.Magenta,
            XKCDColors.Magenta,
            XKCDColors.Red,
            XKCDColors.Red,
        };
        private GameObject MakeMarker(Transform rootTransform, Shader displayShader, Texture maneuverTexture, int markerIdx)
        {
            GameObject newMarker = new GameObject();
            newMarker.layer = navballLayer;
            newMarker.transform.parent = rootTransform;
            newMarker.transform.localPosition = new Vector3(0.0f, 0.0f, iconDepth);

            Material markerMaterial = new Material(displayShader);
            markerMaterial.mainTexture = maneuverTexture;
            markerMaterial.SetColor(colorIdx, markerColor[markerIdx]);

            MeshFilter meshFilter = newMarker.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = newMarker.AddComponent<MeshRenderer>();
            meshRenderer.material = markerMaterial;
            this.markerMaterial[markerIdx] = markerMaterial;

            Vector2 uv0 = markerUV[markerIdx];
            Vector2 uv1 = uv0 + new Vector2(1.0f / 3.0f, 1.0f/3.0f);
            // Half-extents based on centered position.  * 0.15 to scale
            // relative to the navball
            float markerExtents = navballExtents * 0.18f;
            //float markerExtents = navballExtents * 0.5f * 0.15f;
            Mesh mesh = new Mesh();
            mesh.vertices = new[]
                {
                    new Vector3(-markerExtents, markerExtents, 0.0f),
                    new Vector3(markerExtents, markerExtents, 0.0f),
                    new Vector3(-markerExtents, -markerExtents, 0.0f),
                    new Vector3(markerExtents, -markerExtents, 0.0f),
                };
            mesh.colors32 = new Color32[]
                {
                    XKCDColors.White,
                    XKCDColors.White,
                    XKCDColors.White,
                    XKCDColors.White,
                };
            mesh.uv = new[]
                {
                    new Vector2(uv0.x, uv1.y),
                    uv1,
                    uv0,
                    new Vector2(uv1.x, uv0.y),
                };
            mesh.triangles = new[] 
                {
                    0, 3, 2,
                    0, 1, 3
                };
            mesh.RecalculateBounds();
            mesh.Optimize();
            mesh.UploadMeshData(true);
            meshFilter.mesh = mesh;

            newMarker.SetActive(true);

            return newMarker;
        }

        private void InitMarkers(Transform rootTransform)
        {
            // marker is ~15% width of ball
            Shader displayShader = MASLoader.shaders["MOARdV/TextMonitor"];//Shader.Find("KSP/Alpha/Unlit Transparent");
            Texture2D maneuverTexture = GameDatabase.Instance.GetTexture("Squad/Props/IVANavBall/ManeuverNode_vectors", false);
            //float iconDepth = 2.4f - navballExtents - 0.01f;
            //float iconDepth = 1.4f - navballExtents - 0.01f;
            //Utility.LogMessage(this, "InitMarkers - rootTransform = {0}, iconDepth = {1}", rootTransform.position.z, iconDepth);
            //float iconDepth = -navballExtents - 0.01f;

            int markerCount = 6;//markers.Length
            for (int markerId = 0; markerId < markerCount; ++markerId)
            {
                markers[markerId] = MakeMarker(rootTransform, displayShader, maneuverTexture, markerId);
                markers[markerId].transform.localPosition = new Vector3(markerId * 0.5f, 0.0f, iconDepth);
                activeMarkerColor[markerId] = markerColor[markerId];
                //Utility.LogMessage(this, "- markers{1} = {0}", markers[markerId].transform.position.z, markerId);
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
                imageObject.SetActive(currentState);
            }
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
            Camera.onPreRender -= CameraPrerender;
            UnityEngine.GameObject.Destroy(imageObject);
            imageObject = null;
            UnityEngine.GameObject.Destroy(imageMaterial);
            imageMaterial = null;
            UnityEngine.GameObject.Destroy(navballModel);
            navballModel = null;
            UnityEngine.GameObject.Destroy(navballMaterial);
            navballMaterial = null;
            if (!string.IsNullOrEmpty(variableName))
            {
                comp.UnregisterNumericVariable(variableName, internalProp, VariableCallback);
            }
            this.comp = null;
        }
    }
}
