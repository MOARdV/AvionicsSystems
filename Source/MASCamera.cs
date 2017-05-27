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
using UnityEngine;

namespace AvionicsSystems
{
    class MASCamera : PartModule
    {
        /// <summary>
        /// Defines the minimum and maximum field of view of the camera lens
        /// as measured across the vertical (Y) axis, in degrees.  Automatically
        /// clamps values between 1 and 90.
        /// </summary>
        [KSPField]
        public Vector2 fovRange = new Vector2(50.0f, 50.0f);

        /// <summary>
        /// Used internally to allow current FoV to persist.
        /// </summary>
        [KSPField(isPersistant=true)]
        public float currentFov = 50.0f;

        /// <summary>
        /// Defines the minimum and maximum pan angle (left-right camera rotation)
        /// of the camera lens in degrees.  Automatically clamps between
        /// -180 and +180.  Negative values indicate to the left of the center
        /// position. Positive values indicate to the right.
        /// </summary>
        [KSPField]
        public Vector2 panRange = new Vector2(0.0f, 0.0f);

        /// <summary>
        /// Used internally to allow current pan angle to persist.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float currentPan = 0.0f;

        /// <summary>
        /// Defines the minimum and maximum tilt angle (up-down camera rotation)
        /// of the camera lens in degrees.  Automatically clamps between
        /// -180 and +180.  Negative values indicate (direction), positive
        /// values indicate (other direction).
        /// </summary>
        [KSPField]
        public Vector2 tiltRange = new Vector2(0.0f, 0.0f);

        /// <summary>
        /// Used internally to allow current tilt angle to persist.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float currentTilt= 0.0f;

        /// <summary>
        /// Name of the transform that the camera is attached to.
        /// </summary>
        [KSPField]
        public string cameraTransformName = string.Empty;
        internal Transform cameraTransform = null;

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
        /// A unique name for the camera.  Note that cameras missing a name can not
        /// be selected in-flight, and if several cameras have the same name,
        /// only one of them will be selectable.
        /// </summary>
        [KSPField (isPersistant=true)]
        public string cameraName = string.Empty;
        public string newCameraName = string.Empty;

        [UI_Toggle(disabledText = "Off", enabledText = "On")]
        [KSPField(guiActiveEditor = true, guiName = "FOV marker")]
        public bool showFov = false;

        public void Start()
        {
        }

        private bool showGui = false;
        private Rect windowPos = new Rect(Screen.width / 4, Screen.height / 4, 10f, 10f);
        private void mainGUI(int windowID)
        {
            GUIStyle styleWindow = new GUIStyle(GUI.skin.window);
            styleWindow.padding.left = 4;
            styleWindow.padding.top = 4;
            styleWindow.padding.bottom = 4;
            styleWindow.padding.right = 4;

            GUILayout.Label("Camera Name", styleWindow);
            newCameraName = GUILayout.TextArea(newCameraName, styleWindow);
            if (GUILayout.Button("Cancel", styleWindow, GUILayout.Height(30)))
            {
                showGui = false;
            }
            if (GUILayout.Button("OK", styleWindow, GUILayout.Height(30)))
            {
                cameraName = newCameraName;
                showGui = false;
            }
        }

        internal void OnGUI()
        {
            if ((HighLogic.LoadedScene == GameScenes.EDITOR || HighLogic.LoadedScene == GameScenes.FLIGHT) && showGui)
            {
                windowPos = GUILayout.Window(-524628, windowPos, mainGUI, "MASCamera Name", GUILayout.Width(300), GUILayout.Height(100));
            }
        }

        public override string GetInfo()
        {
            return "Cameras rule!";
        }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Set Camera Name")]
        public void SetCameraName()
        {
            showGui = !showGui;
            if(showGui)
            {
                newCameraName = cameraName;
            }
        }
    }
}
