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
using UnityEngine;
using System.IO;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

// This script adds a menu item "Build AssetBundles" to the Assets menu in the
// Unity editor.  It collects all of the named asset bundles and dispatches them
// to either a fonts list (if the asset bundle's name ends in "-font"), or into
// a shaders list (for all other asset bundle names).
//
// For each asset bundle in the fonts list, a single asset bundle is exported using
// the StandaloneWindows target.  The assumption for these is that there is nothing OS
// specific that requires multiple asset bundles.  Each of these asset bundles is named
// by adding ".assetbundle" to the bundle's name (ie, "mas-font" -> "mas-font.assetbundle").
//
// All asset bundles in the other list are exported once per supported OS (Windows,
// OSX, and Linux) under the assumption that there are OS-specific differences in
// the asset bundle.  Each asset bundle name is suffixed with "-windows", "-osx",
// or "-linux" as appropriate, and the extension ".assetbundle" is added to the end.
//
// NOTE: It's trivial to add other asset types to the fonts list by keying off of
// the asset bundle naming convention.  The names are used as presented, so, for
// instance, a models in a bundle ending in "-model" could be processed in the same
// way.
public class AssetBundleCompiler
{
    [MenuItem("Assets/Build AssetBundles")]
    static void BuildBundles()
    {
        string[] assetNames = AssetDatabase.GetAllAssetBundleNames();
        List<string> fontBundles = new List<string>();
        List<string> shaderBundles = new List<string>();
        foreach (string name in assetNames)
        {
            if (name.EndsWith("-font"))
            {
                // Any bundle suffixed with "-font" is assumed to be a fonts-only asset,
                // and it is built once using StandaloneWindows.
                fontBundles.Add(name);
            }
            else
            {
                // All other bundles are assumed to be shader bundles.  They will be
                // built once per OS type.
                shaderBundles.Add(name);
            }
        }

        string assetBundleDirectory = "Assets/AssetBundles";
        if (!Directory.Exists(assetBundleDirectory))
        {
            Directory.CreateDirectory(assetBundleDirectory);
        }

        AssetBundleBuild[] build = new AssetBundleBuild[1];
        build[0] = new AssetBundleBuild();
        foreach (string name in fontBundles)
        {
            string[] assets = AssetDatabase.GetAssetPathsFromAssetBundle(name);
            build[0].assetNames = assets;
            build[0].assetBundleName = name + ".assetbundle";
            BuildPipeline.BuildAssetBundles(assetBundleDirectory, build, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);
        }

        foreach (string name in shaderBundles)
        {
            string[] assets = AssetDatabase.GetAssetPathsFromAssetBundle(name);
            build[0].assetNames = assets;
            build[0].assetBundleName = name + "-windows.assetbundle";
            BuildPipeline.BuildAssetBundles(assetBundleDirectory, build, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);

            build[0].assetBundleName = name + "-osx.assetbundle";
            BuildPipeline.BuildAssetBundles(assetBundleDirectory, build, BuildAssetBundleOptions.None, BuildTarget.StandaloneOSXUniversal);

            build[0].assetBundleName = name + "-linux.assetbundle";
            BuildPipeline.BuildAssetBundles(assetBundleDirectory, build, BuildAssetBundleOptions.None, BuildTarget.StandaloneLinux);
        }
    }
}
