/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2017-2018 MOARdV
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
    /// The MASCameraMode represents the 
    /// </summary>
    internal class MASCameraMode
    {
        public readonly string name;

        /// <summary>
        /// Post-processing shader to apply.
        /// </summary>
        public Material postProcShader;

        /// <summary>
        /// The resolution of the camera mode in pixels.  Cameras render a square
        /// image.  Valid values are 64 to 2048.  Values outside that range are
        /// clamped.  The value will be adjusted
        /// to a power-of-2 if needed.  Note that large values may cause
        /// problems with lower-end machines.  Defaults to 256.
        /// </summary>
        public readonly int cameraResolution;

        /// <summary>
        /// Names of the shader properties.
        /// </summary>
        public string[] propertyName = new string[0];

        /// <summary>
        /// MAS variables that update shader properties.
        /// </summary>
        public string[] propertyValue = new string[0];

        /// <summary>
        /// Callbacks for updating properties.
        /// </summary>
        public Action<double>[] propertyCallback = new Action<double>[0];

        /// <summary>
        /// The flight computer we registered with.
        /// </summary>
        private MASFlightComputer comp;

        public MASCameraMode(ConfigNode node, string partName)
        {
            if (!node.TryGetValue("name", ref name))
            {
                //Utility.LogErrorMessage(this, "No 'name' defined for MASCamera MODE in {0}", partName);
                name = "(anonymous)";
            }

            string shader = string.Empty;
            if (!node.TryGetValue("shader", ref shader))
            {
                // If I simply blit the output of the camera, I have black sections in the image.  I suspect
                // they're regions where alpha = 0.  So, if the prop config doesn't select a shader, I use a
                // simple pass-through shader that drives alpha to 1.
                shader = "MOARdV/PassThrough";
            }
            else
            {
                string concatProperties = string.Empty;
                if (node.TryGetValue("properties", ref concatProperties))
                {
                    string[] propertiesList = concatProperties.Split(';');
                    int listLength = propertiesList.Length;
                    if (listLength > 0)
                    {
                        propertyName = new string[listLength];
                        propertyValue = new string[listLength];
                        propertyCallback = new Action<double>[listLength];

                        for (int i = 0; i < listLength; ++i)
                        {
                            string[] pair = propertiesList[i].Split(':');
                            if (pair.Length != 2)
                            {
                                throw new ArgumentOutOfRangeException("Incorrect number of parameters for property: requires 2, found " + pair.Length + " in property " + propertiesList[i] + " for camera MODE " + name);
                            }
                            propertyName[i] = pair[0].Trim();
                            propertyValue[i] = pair[1].Trim();
                        }
                    }
                }

            }

            if (!MASLoader.shaders.ContainsKey(shader))
            {
                Utility.LogErrorMessage(this, "Invalid shader \"{0}\" in MASCamera MODE {1} in {2}.", shader, name, partName);
                throw new ArgumentException("MASCameraNode: Invalid post-processing shader name.");
            }

            postProcShader = new Material(MASLoader.shaders[shader]);

            string textureName = string.Empty;
            if (node.TryGetValue("texture", ref textureName))
            {
                Texture auxTexture = GameDatabase.Instance.GetTexture(textureName, false);
                if (auxTexture == null)
                {
                    throw new ArgumentException("Unable to find 'texture' " + textureName + " for CAMERA " + name);
                }
                postProcShader.SetTexture("_AuxTex", auxTexture);
            }

            if (!node.TryGetValue("cameraResolution", ref cameraResolution))
            {
                cameraResolution = 256;
            }
        }

        public void UnregisterShaderProperties()
        {
            if (comp != null)
            {
                for (int i = 0; i < propertyValue.Length; ++i)
                {
                    comp.UnregisterNumericVariable(propertyValue[i], null, propertyCallback[i]);
                }
                comp = null;
            }
        }

        /// <summary>
        /// Callback to update the shader's properties.
        /// </summary>
        /// <param name="propertyId">The property ID to update.</param>
        /// <param name="newValue">The new value for that property.</param>
        private void PropertyCallback(int propertyId, double newValue)
        {
            postProcShader.SetFloat(propertyId, (float)newValue);
        }

        /// <summary>
        /// Callback used to tell the mode to refresh its shader properties.
        /// </summary>
        /// <param name="comp"></param>
        public void UpdateShaderProperties(MASFlightComputer comp)
        {
            if (this.comp == comp)
            {
                return;
            }

            UnregisterShaderProperties();

            this.comp = comp;

            for (int i = 0; i < propertyValue.Length; ++i)
            {
                int propertyId = Shader.PropertyToID(propertyName[i]);
                propertyCallback[i] = delegate(double a) { PropertyCallback(propertyId, a); };
                comp.RegisterNumericVariable(propertyValue[i], null, propertyCallback[i]);
            }
        }
    }

    /// <summary>
    /// The MASCamera represents a physical camera model, and it encapsulates the data specific
    /// to that model (field of view, pan/tilt movement limits, post-processing shaders that
    /// represent physical camera behavior).
    /// </summary>
    class MASCamera : PartModule
    {
        /// <summary>
        /// Defines the minimum and maximum field of view of the camera lens
        /// as measured across the vertical (Y) axis, in degrees.  Automatically
        /// clamps values between 1 and 179.
        /// </summary>
        [KSPField]
        public Vector2 fovRange = new Vector2(50.0f, 50.0f);

        /// <summary>
        /// Used internally to allow desired FoV to persist.  Should only
        /// be changed programmatically through AddFoV() and SetFoV() to
        /// manage the FoV limits.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float goalFov = 50.0f;

        /// <summary>
        /// Current FoV.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float currentFov = 50.0f;

        /// <summary>
        /// Max FoV rate of change in degrees/sec.  0 indicates instant.
        /// </summary>
        [KSPField]
        public float fovRate = 0.0f;

        /// <summary>
        /// Defines the minimum and maximum pan angle (left-right camera rotation)
        /// of the camera lens in degrees.  Automatically clamps between
        /// -180 and +180.  Negative values indicate to the left of the center
        /// position. Positive values indicate to the right.
        /// </summary>
        [KSPField]
        public Vector2 panRange = new Vector2(0.0f, 0.0f);

        /// <summary>
        /// Used internally to allow desired pan angle to persist.  Should only
        /// be changed programmatically through AddPan() and SetPan() to
        /// manage the pan limits.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float goalPan = 0.0f;

        /// <summary>
        /// Current pan position.  May differ from goalPan if panRate != 0
        /// </summary>
        [KSPField(isPersistant = true)]
        public float currentPan = 0.0f;

        /// <summary>
        /// Maximum rate (degrees/sec) for pan.  0 indicates instant.
        /// </summary>
        [KSPField]
        public float panRate = 0.0f;

        /// <summary>
        /// Defines the minimum and maximum tilt angle (up-down camera rotation)
        /// of the camera lens in degrees.  Automatically clamps between
        /// -180 and +180.  Negative values indicate (direction), positive
        /// values indicate (other direction).
        /// </summary>
        [KSPField]
        public Vector2 tiltRange = new Vector2(0.0f, 0.0f);

        /// <summary>
        /// Used internally to allow desired tilt angle to persist.  Should only
        /// be changed programmatically through AddTilt() and SetTilt() to
        /// manage the tilt limits.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float goalTilt = 0.0f;

        /// <summary>
        /// Current tilt position.  May differ from goalTilt if tiltRate != 0
        /// </summary>
        [KSPField(isPersistant = true)]
        public float currentTilt = 0.0f;

        /// <summary>
        /// Maximum rate (degrees/sec) for tilt.  0 indicates instant.
        /// </summary>
        [KSPField]
        public float tiltRate = 0.0f;

        /// <summary>
        /// Name of the transform that the camera lens is attached to.
        /// </summary>
        [KSPField]
        public string cameraTransformName = string.Empty;
        private Transform cameraTransform = null;

        /// <summary>
        /// Name of an optional transform to physically pan the model.
        /// </summary>
        [KSPField]
        public string panTransformName = string.Empty;
        private Transform panTransform = null;
        private Quaternion panRotation = Quaternion.identity;
        private bool updatePan = false;

        /// <summary>
        /// Name of an optional transform to physically tilt the model.
        /// </summary>
        [KSPField]
        public string tiltTransformName = string.Empty;
        private Transform tiltTransform = null;
        private Quaternion tiltRotation = Quaternion.identity;
        private bool updateTilt = false;

        /// <summary>
        /// Offset of the camera lens from its transform's position.
        /// </summary>
        [KSPField]
        public Vector3 translation = Vector3.zero;

        /// <summary>
        /// Euler Rotation of the camera lens from the transform's facing.
        /// </summary>
        [KSPField]
        public Vector3 rotation = Vector3.zero;

        /// <summary>
        /// Allows a camera to refresh less often than every Update().  Values
        /// larger than 1 indicate an update of every Nth Update() (for instance,
        /// 2 means "Render every other frame").
        /// </summary>
        [KSPField]
        public int refreshRate = 1;
        private int frameCount = 0;

        /// <summary>
        /// A unique name for the camera.  Note that cameras missing a name can not
        /// be selected in-flight, and if several cameras have the same name,
        /// only one of them will be selectable.
        /// </summary>
        [KSPField(isPersistant = true)]
        public string cameraName = string.Empty;
        private string newCameraName = string.Empty;

        [UI_Toggle(disabledText = "Off", enabledText = "On")]
        [KSPField(guiActiveEditor = true, guiName = "FOV marker")]
        public bool showFov = false;
        const float rayLength = 10.0f;

        /// <summary>
        /// Used internally to keep track of which camera is active.
        /// </summary>
        [KSPField(isPersistant = true)]
        public int activeMode = 0;
        private MASCameraMode[] mode = new MASCameraMode[0];

        private static readonly string[] knownCameraNames = 
        {
            "GalaxyCamera",
            "Camera ScaledSpace",
            "Camera 01",
            "Camera 00",
            "FXCamera"
        };
        private readonly Camera[] cameras = { null, null, null, null, null };
        private readonly GameObject[] cameraBody = { null, null, null, null, null };
        internal RenderTexture cameraRentex;
        internal event Action<RenderTexture, Material> renderCallback;

        private MASDeployableCamera deploymentController;

        /// <summary>
        /// Is this object ready to use?
        /// </summary>
        /// <returns>true if a this object will function, false otherwise</returns>
        public bool IsValid()
        {
            return cameraTransform != null;
        }

        #region Setup - Teardown
        /// <summary>
        /// Configure everything.
        /// </summary>
        public void Start()
        {
            if (!(HighLogic.LoadedScene == GameScenes.EDITOR || HighLogic.LoadedScene == GameScenes.FLIGHT))
            {
                return;
            }

            if (knownCameraNames.Length != cameras.Length)
            {
                throw new NotImplementedException("MASCamera: Camera Names array has a different size than cameras array!");
            }
            if (cameraBody.Length != cameras.Length)
            {
                throw new NotImplementedException("MASCamera: Camera bodies array has a different size than cameras array!");
            }

            if (!string.IsNullOrEmpty(cameraTransformName))
            {
                Transform cameraParentTransform = part.FindModelTransform(cameraTransformName);
                cameraTransform = new GameObject().transform;
                cameraTransform.gameObject.name = "MASCamera-" + cameraParentTransform.gameObject.name;
                cameraTransform.parent = cameraParentTransform;
                cameraTransform.position = cameraParentTransform.position;
                cameraTransform.rotation = cameraParentTransform.rotation;

                // TODO: Still sort this out - ordering
                if (translation != Vector3.zero)
                {
                    cameraTransform.Translate(translation);
                }
                if (rotation != Vector3.zero)
                {
                    cameraTransform.Rotate(rotation);
                }
                //if (translation != Vector3.zero)
                //{
                //    cameraTransform.Translate(translation);
                //}
            }

            if (cameraTransform != null)
            {
                // Make everything in-order, and clamp the current values to a legal range.
                // TODO: And clamp the ranges to legal ranges.
                if (fovRange.y < fovRange.x)
                {
                    fovRange = new Vector2(fovRange.y, fovRange.x);
                }
                fovRange.x = Mathf.Clamp(fovRange.x, 1.0f, 179.0f);
                fovRange.y = Mathf.Clamp(fovRange.y, 1.0f, 179.0f);
                currentFov = Mathf.Clamp(currentFov, fovRange.x, fovRange.y);
                goalFov = Mathf.Clamp(goalFov, fovRange.x, fovRange.y);
                fovRate = Mathf.Abs(fovRate);

                if (panRange.y < panRange.x)
                {
                    panRange = new Vector2(panRange.y, panRange.x);
                }
                panRange.x = Mathf.Clamp(panRange.x, -180.0f, 180.0f);
                panRange.y = Mathf.Clamp(panRange.y, -180.0f, 180.0f);
                currentPan = Mathf.Clamp(currentPan, panRange.x, panRange.y);
                goalPan = Mathf.Clamp(goalPan, panRange.x, panRange.y);
                panRate = Mathf.Abs(panRate);

                if (tiltRange.y < tiltRange.x)
                {
                    tiltRange = new Vector2(tiltRange.y, tiltRange.x);
                }
                tiltRange.x = Mathf.Clamp(tiltRange.x, -180.0f, 180.0f);
                tiltRange.y = Mathf.Clamp(tiltRange.y, -180.0f, 180.0f);
                currentTilt = Mathf.Clamp(currentTilt, tiltRange.x, tiltRange.y);
                goalTilt = Mathf.Clamp(goalTilt, tiltRange.x, tiltRange.y);
                tiltRate = Mathf.Abs(tiltRate);

                if (HighLogic.LoadedSceneIsEditor)
                {
                    CreateFovRenderer();
                }
            }
            else if (string.IsNullOrEmpty(cameraTransformName))
            {
                Utility.LogErrorMessage(this, "No 'cameraTransformName' provided in part.");
                throw new NotImplementedException("MASCamera: Missing 'cameraTransformName' in module config node.");
            }
            else
            {
                Utility.LogErrorMessage(this, "Unable to find transform \"{0}\" in part", cameraTransformName);
            }

            if (HighLogic.LoadedScene == GameScenes.FLIGHT)
            {
                if (!string.IsNullOrEmpty(panTransformName))
                {
                    panTransform = part.FindModelTransform(panTransformName);
                    if (panTransform == null)
                    {
                        Utility.LogErrorMessage(this, "Unable to find a pan transform named \"{0}\"", panTransformName);
                    }
                    else
                    {
                        panRotation = panTransform.localRotation;
                        panTransform.localRotation = panRotation * Quaternion.Euler(0.0f, currentPan, 0.0f);
                    }
                }

                if (!string.IsNullOrEmpty(tiltTransformName))
                {
                    tiltTransform = part.FindModelTransform(tiltTransformName);
                    if (tiltTransform == null)
                    {
                        Utility.LogErrorMessage(this, "Unable to find a tilt transform named \"{0}\"", tiltTransformName);
                    }
                    else
                    {
                        tiltRotation = tiltTransform.localRotation;
                        tiltTransform.localRotation = tiltRotation * Quaternion.Euler(-currentTilt, 0.0f, 0.0f);
                    }
                }

                deploymentController = part.FindModuleImplementing<MASDeployableCamera>();

                CreateFlightCameras(1.0f);
            }

            refreshRate = Math.Max(refreshRate, 1);
        }

        /// <summary>
        /// Helper function to locate flight cameras.
        /// </summary>
        /// <param name="cameraName">Name of the camera we're looking for.</param>
        /// <returns>The named camera, or null if it was not found.</returns>
        private static Camera GetCameraByName(string cameraName)
        {
            for (int i = 0; i < Camera.allCamerasCount; ++i)
            {
                if (Camera.allCameras[i].name == cameraName)
                {
                    return Camera.allCameras[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Helper method to adjust a resolution to an acceptable power-of-2.
        /// </summary>
        /// <param name="resolution">[inout] Resolution to adjust.</param>
        static internal void AdjustResolution(ref int resolution)
        {
            if (resolution > 2048)
            {
                resolution = 2048;
            }
            else if (resolution < 64)
            {
                resolution = 64;
            }
            resolution &= 0x00000fc0;
            for (int i = 0x800; i != 0; i >>= 1)
            {
                if ((resolution & i) != 0)
                {
                    resolution = resolution & i;
                    break;
                }
            }
        }

        /// <summary>
        /// Update parameters affected by a mode change.
        /// </summary>
        private void ApplyMode()
        {
            if (cameraRentex.width != mode[activeMode].cameraResolution)
            {
                cameraRentex.Release();
                cameraRentex = new RenderTexture(mode[activeMode].cameraResolution, mode[activeMode].cameraResolution, 24);
            }
        }

        /// <summary>
        /// Create the cameras used during flight.
        /// </summary>
        private void CreateFlightCameras(float aspectRatio)
        {
            cameraRentex = new RenderTexture(256, 256, 24);
            ConfigNode partConfigNode = Utility.GetPartModuleConfigNode(part, "MASCamera");
            if (partConfigNode == null)
            {
                Utility.LogErrorMessage(this, "Unable to load part config node for MASCamera {0}.", part.partName);
                throw new NotImplementedException("MASCamera: Unable to load part config node for MASCamera.");
            }

            ConfigNode[] modeNodes = partConfigNode.GetNodes("MODE");
            if (modeNodes == null || modeNodes.Length == 0)
            {
                modeNodes = new ConfigNode[1];
                modeNodes[0] = new ConfigNode();
                modeNodes[0].AddValue("name", "Default");
            }

            mode = new MASCameraMode[modeNodes.Length];
            for (int i = 0; i < modeNodes.Length; ++i)
            {
                mode[i] = new MASCameraMode(modeNodes[i], part.partName);
            }

            activeMode = Mathf.Clamp(activeMode, 0, mode.Length - 1);
            ApplyMode();

            //cameraRentex = new RenderTexture(cameraResolution, cameraResolution, 24);
            for (int i = 0; i < cameras.Length; ++i)
            {
                Camera sourceCamera = GetCameraByName(knownCameraNames[i]);
                if (sourceCamera != null)
                {
                    cameraBody[i] = new GameObject();
                    cameraBody[i].name = "MASCamera-" + i + "-" + cameraBody[i].GetInstanceID();
                    cameras[i] = cameraBody[i].AddComponent<Camera>();

                    // Just in case to support JSITransparentPod.
                    cameras[i].cullingMask &= ~(1 << 16 | 1 << 20);

                    cameras[i].CopyFrom(sourceCamera);
                    cameras[i].enabled = false;
                    cameras[i].aspect = aspectRatio;

                    // These get stomped on at render time:
                    cameras[i].fieldOfView = currentFov;
                    cameras[i].transform.rotation = Quaternion.identity;
                    cameras[i].targetTexture = cameraRentex;

                    // Minor hack to bring the near clip plane for the "up close"
                    // cameras drastically closer to where the cameras notionally
                    // are.  Experimentally, these two cameras have N/F of 0.4 / 300.0,
                    // or 750:1 Far/Near ratio.  Changing this to 8192:1 brings the
                    // near plane to 37cm or so, which hopefully is close enough to
                    // see nearby details without creating z-fighting artifacts.
                    if (i == 3 || i == 4)
                    {
                        cameras[i].nearClipPlane = cameras[i].farClipPlane / 8192.0f;
                    }
                }
            }
        }

        /// <summary>
        /// Set up the FoV renderer when a part is attached.
        /// </summary>
        private void AttachPart()
        {
            Vector3 origin = cameraTransform.TransformPoint(Vector3.zero);
            Vector3 direction = cameraTransform.forward;
            if (minFovRenderer != null)
            {
                minFovRenderer.SetPosition(0, origin);
                minFovRenderer.SetPosition(1, origin + direction * rayLength);
            }
            if (maxFovRenderer != null)
            {
                maxFovRenderer.SetPosition(0, origin);
                maxFovRenderer.SetPosition(1, origin + direction * rayLength);
            }
        }

        private void DetachPart()
        {
            showFov = false;
        }

        /// <summary>
        /// Tear down and release resources.
        /// </summary>
        public void OnDestroy()
        {
            if (minFovRenderer != null)
            {
                Destroy(minFovRenderer);
                minFovRenderer = null;
                Destroy(minFovPosition);
                minFovPosition = null;
                part.OnEditorAttach -= AttachPart;
                part.OnEditorDetach -= DetachPart;
                part.OnEditorDestroy -= DetachPart;
            }
            if (maxFovRenderer != null)
            {
                Destroy(maxFovRenderer);
                maxFovRenderer = null;
                Destroy(maxFovPosition);
                maxFovPosition = null;
            }

            for (int i = 0; i < mode.Length; ++i)
            {
                mode[i].UnregisterShaderProperties();
                Destroy(mode[i].postProcShader);
                mode[i].postProcShader = null;
            }

            if (nameMenu != null)
            {
                InputLockManager.RemoveControlLock("MASCamera-UI");
                nameMenu.Dismiss();
                nameMenu = null;
            }

            if (renderCallback != null)
            {
                renderCallback.Invoke(null, null);
            }

            if (cameraRentex != null)
            {
                cameraRentex.Release();
                cameraRentex = null;
            }
        }
        #endregion

        #region Flight

        /// <summary>
        /// Change the current field of view by `deltaFoV` degrees, remaining within
        /// camera FoV limits.
        /// </summary>
        /// <param name="deltaFoV">The amount to add or subtract to FoV in degrees.</param>
        /// <returns>The adjusted field of view.</returns>
        public float AddFoV(float deltaFoV)
        {
            goalFov = Mathf.Clamp(goalFov + deltaFoV, fovRange.x, fovRange.y);

            return goalFov;
        }

        /// <summary>
        /// Change the current field of view by `deltaFoV` degrees, remaining within
        /// camera FoV limits.
        /// </summary>
        /// <param name="deltaPan">The amount to add or subtract to FoV in degrees.</param>
        /// <returns>The adjusted pan.</returns>
        public float AddPan(float deltaPan)
        {
            goalPan = Mathf.Clamp(goalPan + deltaPan, panRange.x, panRange.y);

            return goalPan;
        }

        /// <summary>
        /// Change the current field of view by `deltaFoV` degrees, remaining within
        /// camera FoV limits.
        /// </summary>
        /// <param name="deltaTilt">The amount to add or subtract to FoV in degrees.</param>
        /// <returns>The adjusted tilt.</returns>
        public float AddTilt(float deltaTilt)
        {
            goalTilt = Mathf.Clamp(goalTilt + deltaTilt, tiltRange.x, tiltRange.y);

            return goalTilt;
        }

        public bool GetDeployable()
        {
            return (deploymentController != null) ? (deploymentController.deployState != ModuleDeployablePart.DeployState.BROKEN) : false;
        }

        public bool IsDamaged()
        {
            return (deploymentController != null) ? (deploymentController.deployState == ModuleDeployablePart.DeployState.BROKEN) : false;
        }

        public bool IsDeployed()
        {
            return (deploymentController != null) ? (deploymentController.deployState == ModuleDeployablePart.DeployState.EXTENDED) : true;
        }

        public bool IsMoving()
        {
            return (deploymentController != null) ? (deploymentController.deployState == ModuleDeployablePart.DeployState.EXTENDING || deploymentController.deployState == ModuleDeployablePart.DeployState.RETRACTING) : false;
        }

        public bool ToggleDeployment()
        {
            if (deploymentController != null)
            {
                if (deploymentController.deployState == ModuleDeployablePart.DeployState.EXTENDED)
                {
                    deploymentController.Retract();
                    return true;
                }
                else if (deploymentController.deployState == ModuleDeployablePart.DeployState.RETRACTED)
                {
                    deploymentController.Extend();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the index of the currently-active camera mode.
        /// </summary>
        /// <returns></returns>
        public int GetMode()
        {
            return activeMode;
        }

        /// <summary>
        /// Return the total count of the camera modes.
        /// </summary>
        /// <returns></returns>
        public int GetModeCount()
        {
            return mode.Length;
        }

        /// <summary>
        /// Return the name of the selected camera mode
        /// </summary>
        /// <returns></returns>
        public string GetModeName(int selectedMode)
        {
            if (selectedMode >= 0 || selectedMode < mode.Length)
            {
                return mode[selectedMode].name;
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Select the camera mode.
        /// </summary>
        /// <param name="newMode"></param>
        /// <returns></returns>
        public int SetMode(int newMode)
        {
            if (newMode >= 0 || newMode < mode.Length)
            {
                activeMode = newMode;
                ApplyMode();
            }

            return activeMode;
        }

        /// <summary>
        /// Set the current field of view, remaining within camera FoV limits.
        /// </summary>
        /// <param name="fieldOfView">The new FoV in degrees.</param>
        /// <returns>The adjusted field of view.</returns>
        public float SetFoV(float fieldOfView)
        {
            goalFov = Mathf.Clamp(fieldOfView, fovRange.x, fovRange.y);

            return goalFov;
        }

        /// <summary>
        /// Set the current pan location within the pan limits.
        /// </summary>
        /// <param name="pan"></param>
        /// <returns>The adjusted pan setting.</returns>
        public float SetPan(float pan)
        {
            goalPan = Mathf.Clamp(pan, panRange.x, panRange.y);

            return goalPan;
        }

        /// <summary>
        /// Set the current pan location within the pan limits.
        /// </summary>
        /// <param name="tilt"></param>
        /// <returns>The current tilt position</returns>
        public float SetTilt(float tilt)
        {
            goalTilt = Mathf.Clamp(tilt, tiltRange.x, tiltRange.y);

            return goalTilt;
        }

        /// <summary>
        /// Enable / disable the FOV cones.  Valid only in the editor.
        /// </summary>
        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                // TODO: Renderers don't show up if the part is added.
                // only when a craft is loaded with camera attached.
                // RPM used callbacks for onattach / ondetach - maybe
                // they need to be used here.
                if (minFovRenderer != null)
                {
                    minFovRenderer.enabled = showFov;
                }
                if (maxFovRenderer != null)
                {
                    maxFovRenderer.enabled = showFov;
                }
            }
            if (showGui)
            {
                if (nameMenu == null)
                {
                    ShowNameMenu();
                }
            }
            else if (nameMenu != null)
            {
                InputLockManager.RemoveControlLock("MASCamera-UI");
                nameMenu.Dismiss();
                nameMenu = null;
            }

            if (HighLogic.LoadedSceneIsFlight)
            {
                if (goalFov != currentFov)
                {
                    if (fovRate > 0.0f)
                    {
                        float fovDelta = Mathf.Min(Mathf.Abs(goalFov - currentFov), panRate * TimeWarp.deltaTime);
                        if (goalFov > currentFov)
                        {
                            currentFov += fovDelta;
                        }
                        else
                        {
                            currentFov -= fovDelta;
                        }
                    }
                    else
                    {
                        currentFov = goalFov;
                    }
                }

                if (goalPan != currentPan)
                {
                    if (panRate > 0.0f)
                    {
                        float panDelta = Mathf.Min(Mathf.Abs(goalPan - currentPan), panRate * TimeWarp.deltaTime);
                        if (goalPan > currentPan)
                        {
                            currentPan += panDelta;
                        }
                        else
                        {
                            currentPan -= panDelta;
                        }
                    }
                    else
                    {
                        currentPan = goalPan;
                    }

                    if (panTransform != null)
                    {
                        updatePan = true;
                    }
                }

                if (goalTilt != currentTilt)
                {
                    if (tiltRate > 0.0f)
                    {
                        float tiltDelta = Mathf.Min(Mathf.Abs(goalTilt - currentTilt), tiltRate * TimeWarp.deltaTime);
                        if (goalTilt > currentTilt)
                        {
                            currentTilt += tiltDelta;
                        }
                        else
                        {
                            currentTilt -= tiltDelta;
                        }
                    }
                    else
                    {
                        currentTilt = goalTilt;
                    }

                    if (tiltTransform != null)
                    {
                        updateTilt = true;
                    }
                }

                if (updatePan)
                {
                    // Update the physical model.
                    panTransform.localRotation = panRotation * Quaternion.Euler(0.0f, currentPan, 0.0f);
                    updatePan = false;
                }

                if (updateTilt)
                {
                    // Update the physical model.
                    tiltTransform.localRotation = tiltRotation * Quaternion.Euler(-currentTilt, 0.0f, 0.0f);
                    updateTilt = false;
                }

                if (renderCallback != null)
                {
                    if (!cameraRentex.IsCreated())
                    {
                        cameraRentex.Create();
                    }

                    if (refreshRate == 1 || (frameCount % refreshRate) == 0)
                    {
                        Quaternion cameraRotation = cameraTransform.rotation * UpdateRotation();
                        Vector3 cameraPosition = cameraTransform.position;

                        cameraRentex.DiscardContents();
                        for (int i = 0; i < cameraBody.Length; ++i)
                        {
                            cameraBody[i].transform.rotation = cameraRotation;
                            cameraBody[i].transform.position = cameraPosition;
                            cameras[i].fieldOfView = currentFov;
                            cameras[i].Render();
                        }

                        renderCallback.Invoke(cameraRentex, mode[activeMode].postProcShader);
                    }

                    ++frameCount;
                }
            }
        }

        /// <summary>
        /// Update the quaternion describing the camera pan/tilt at the lens.
        /// </summary>
        /// <returns></returns>
        private Quaternion UpdateRotation()
        {
            if (panTransform == null)
            {
                if (tiltTransform == null)
                {
                    return Quaternion.Euler(-currentTilt, currentPan, 0.0f);
                }
                else
                {
                    return Quaternion.Euler(0.0f, currentPan, 0.0f);
                }
            }
            else if (tiltTransform == null)
            {
                return Quaternion.Euler(-currentTilt, 0.0f, 0.0f);
            }
            else
            {
                return Quaternion.identity;
            }
        }

        /// <summary>
        /// Make sure the shaders have access to a flight computer, so they can update
        /// variables.
        /// </summary>
        /// <param name="comp"></param>
        public void UpdateFlightComputer(MASFlightComputer comp)
        {
            for (int i = 0; i < mode.Length; ++i)
            {
                mode[i].UpdateShaderProperties(comp);
            }
        }

        #endregion

        #region Editor
        private static readonly Material fovRendererMaterial = new Material(Shader.Find("Particles/Additive"));
        private GameObject minFovPosition;
        private LineRenderer minFovRenderer;
        private GameObject maxFovPosition;
        private LineRenderer maxFovRenderer;
        /// <summary>
        /// Configure the editor field-of-view cones.  There are two - one of the min angle, and one for the
        /// max - but we only configure one if min == max.
        /// </summary>
        private void CreateFovRenderer()
        {
            float minSpan = rayLength * 2.0f * (float)Math.Tan(Mathf.Deg2Rad * fovRange.x * 0.5f);

            part.OnEditorAttach += AttachPart;
            part.OnEditorDetach += DetachPart;
            part.OnEditorDestroy += DetachPart;

            minFovPosition = new GameObject();
            minFovRenderer = minFovPosition.AddComponent<LineRenderer>();
            minFovRenderer.useWorldSpace = true;
            minFovRenderer.material = fovRendererMaterial;
            minFovRenderer.SetWidth(0.054f, minSpan);
            minFovRenderer.SetVertexCount(2);
            minFovRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            minFovRenderer.receiveShadows = false;
            Vector3 origin = cameraTransform.TransformPoint(Vector3.zero);
            Vector3 direction = cameraTransform.forward;
            minFovRenderer.SetPosition(0, origin);
            minFovRenderer.SetPosition(1, origin + direction * rayLength);
            Color startColor = (fovRange.y > fovRange.x) ? new Color(0.0f, 1.0f, 0.0f, 0.75f) : new Color(0.0f, 1.0f, 1.0f, 0.75f);
            Color endColor = startColor;
            endColor.a = 0.0f;
            minFovRenderer.SetColors(startColor, endColor);
            minFovRenderer.enabled = showFov;

            if (fovRange.y > fovRange.x)
            {
                float maxSpan = rayLength * 2.0f * (float)Math.Tan(Mathf.Deg2Rad * fovRange.y * 0.5f);

                maxFovPosition = new GameObject();
                maxFovRenderer = maxFovPosition.AddComponent<LineRenderer>();
                maxFovRenderer.useWorldSpace = true;
                maxFovRenderer.material = fovRendererMaterial;
                maxFovRenderer.SetWidth(0.054f, maxSpan);
                maxFovRenderer.SetVertexCount(2);
                maxFovRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                maxFovRenderer.receiveShadows = false;
                maxFovRenderer.SetPosition(0, origin);
                maxFovRenderer.SetPosition(1, origin + direction * rayLength);
                startColor = new Color(0.0f, 0.0f, 1.0f, 0.65f);
                endColor = startColor;
                endColor.a = 0.0f;
                maxFovRenderer.SetColors(startColor, endColor);
                maxFovRenderer.enabled = showFov;
            }
        }

        private bool showGui = false;
        private PopupDialog nameMenu = null;

        /// <summary>
        /// Create the PopupDialog that allows the user to edit the name of the camera.
        /// </summary>
        private void ShowNameMenu()
        {
            if (nameMenu == null)
            {
                nameMenu = PopupDialog.SpawnPopupDialog(
                   new Vector2(0.5f, 0.5f),
                   new Vector2(0.5f, 0.5f),
                   new MultiOptionDialog(
                       "MASCamera-Name",
                       "Name this camera:",
                       "MASCamera Name",
                       HighLogic.UISkin,
                       new Rect(0.5f, 0.5f, 150.0f, 60.0f),
                       new DialogGUIFlexibleSpace(),
                       new DialogGUIVerticalLayout(
                           new DialogGUIFlexibleSpace(),
                           new DialogGUITextInput(newCameraName, false, 40, SetTextCallback, 140.0f, 30.0f),
                           new DialogGUIButton(KSP.Localization.Localizer.GetStringByTag("#autoLOC_174783"), // Cancel
                               delegate { showGui = false; }, 140.0f, 30.0f, false),
                           new DialogGUIButton(KSP.Localization.Localizer.GetStringByTag("#autoLOC_174814"), // OK
                               delegate { showGui = false; cameraName = newCameraName; }, 140.0f, 30.0f, false)
                           )
                       ),
                       false,
                       HighLogic.UISkin);
                InputLockManager.SetControlLock(ControlTypes.KEYBOARDINPUT, "MASCamera-UI");
            }
        }

        /// <summary>
        /// I assume the return parameter means I can edit this string before returning it.
        /// </summary>
        /// <param name="newString"></param>
        /// <returns></returns>
        private string SetTextCallback(string newString)
        {
            newCameraName = newString;
            return newString;
        }

        /// <summary>
        /// TODO: Meaningful string.
        /// </summary>
        /// <returns></returns>
        public override string GetInfo()
        {
            return "This part is equipped with a camera suitable for display on an MFD.";
        }
        #endregion

        /// <summary>
        /// Open the 'MASCamera Name' GUI to allow changing camera name.
        /// </summary>
        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Set Camera Name")]
        public void SetCameraName()
        {
            showGui = !showGui;
            if (showGui)
            {
                newCameraName = cameraName;
            }
        }
    }

    /// <summary>
    /// A minimal module that allows a MASCamera to be designated as a deployable camera, taking advantage
    /// of the stock mechanism for deployable parts that can be broken by the wind.
    /// </summary>
    public class MASDeployableCamera : ModuleDeployablePart
    {
        public MASDeployableCamera()
        {
            isTracking = false;

            // Fields that might be of interest:
            //partType = "Camera";
            //subPartName = "Camera body";
            //subPartMass = Mathf.Min(0.001f, part.mass * 0.5f);
        }

        public override string GetModuleDisplayName()
        {
            return "Deployable Camera";
        }
    }
}
