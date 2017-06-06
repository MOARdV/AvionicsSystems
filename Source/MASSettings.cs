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
using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    /// <summary>
    /// MASSettings implements the AppLauncher button and user interface to change MAS
    /// settings in game.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    class MASSettings : MonoBehaviour
    {
        private static ApplicationLauncherButton appLauncherButton = null;

        private Texture2D iconDisableTexture;
        private Texture2D iconEnableTexture;
        private PopupDialog settingsMenu = null;
        private bool guiVisible = false;

        public void Awake()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(AddAppLauncherButton);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(RemoveAppLauncherButton);
        }

        public void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(AddAppLauncherButton);
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(RemoveAppLauncherButton);
        }

        private void AddAppLauncherButton()
        {
            if (appLauncherButton == null)
            {
                appLauncherButton = InitAppLauncherButton();
            }
        }

        private void RemoveAppLauncherButton()
        {
            if (appLauncherButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(appLauncherButton);
                appLauncherButton = null;
            }
        }

        private ApplicationLauncherButton InitAppLauncherButton()
        {
            iconDisableTexture = GameDatabase.Instance.GetTexture("MOARdV/AvionicsSystems/Icons/toolbar_disabled", false);
            iconEnableTexture = GameDatabase.Instance.GetTexture("MOARdV/AvionicsSystems/Icons/toolbar_enabled", false);

            return ApplicationLauncher.Instance.AddModApplication(onAppLauncherShow, onAppLauncherHide,
                null, null, null, null,
                ApplicationLauncher.AppScenes.SPACECENTER,
                iconDisableTexture);
        }

        private void ShowGui()
        {
            if (settingsMenu == null)
            {
                InitValues();
                settingsMenu = PopupDialog.SpawnPopupDialog(
                   new Vector2(0.5f, 0.5f),
                   new Vector2(0.5f, 0.5f),
                   new MultiOptionDialog(
                       "MASSettings-Config",
                       "",
                       "MAS Settings",
                       HighLogic.UISkin,
                       new Rect(0.5f, 0.5f, 150.0f, 60.0f),
                       new DialogGUIFlexibleSpace(),
                       new DialogGUIVerticalLayout(
                           new DialogGUIFlexibleSpace(),
                           new DialogGUIToggle(verboseLogging, "Verbose Logging",
                               ToggleLogging),
                           new DialogGUIButton(KSP.Localization.Localizer.GetStringByTag("#autoLOC_174783"), // Cancel
                               delegate 
                               { 
                                   onAppLauncherHide();
                                   appLauncherButton.SetFalse(false); 
                               }, 140.0f, 30.0f, false),
                           new DialogGUIButton(KSP.Localization.Localizer.GetStringByTag("#autoLOC_174814"), // OK
                               delegate 
                               { 
                                   onAppLauncherHide();
                                   appLauncherButton.SetFalse(false);
                                   ApplyChanges();
                               }, 140.0f, 30.0f, false)
                           )
                       ),
                       false,
                       HighLogic.UISkin);
            }
        }

        private bool verboseLogging;
        private void InitValues()
        {
            verboseLogging = MASConfig.VerboseLogging;
        }

        private void ApplyChanges()
        {
            MASConfig.VerboseLogging = verboseLogging;
        }

        private void ToggleLogging(bool newValue)
        {
            verboseLogging = newValue;
        }

        private void onAppLauncherShow()
        {
            guiVisible = true;
            ToggleIcon();
            ShowGui();
        }

        private void onAppLauncherHide()
        {
            guiVisible = false;
            ToggleIcon();
            if (settingsMenu != null)
            {
                settingsMenu.Dismiss();
                settingsMenu = null;
            }
        }

        private void ToggleIcon()
        {
            if (guiVisible)
            {
                appLauncherButton.SetTexture(iconEnableTexture);
            }
            else
            {
                appLauncherButton.SetTexture(iconDisableTexture);
            }
        }
    }
}
