using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Mağaza sürümü (Trade of MTBS 3D): game-ci Android ve iOS derlemeleri bu metotları çağırır.
/// İmza bilgileri game-ci'nin geçtiği komut satırı argümanlarından okunur.
/// </summary>
public static class AndroidBuild
{
    public const string BundleId = "com.bavsla.tmtbs3d";
    public const string ProductName = "Trade of MTBS 3D";

    static void Common()
    {
        WebGLBuild.EnsureScene();
        WebGLBuild.EnsureShaders();
        CharImport.Prepare();
        PlayerSettings.productName = ProductName;
        PlayerSettings.companyName = "Bavsla Games";
        PlayerSettings.stripEngineCode = false;
        PlayerSettings.runInBackground = false;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        string v = WebGLBuild.ArgValue("-buildVersion");
        if (!string.IsNullOrEmpty(v)) PlayerSettings.bundleVersion = v;
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icon/app_icon.png");
        if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        PlayerSettings.SplashScreen.showUnityLogo = false;
    }

    public static void Build()
    {
        Common();
        var nb = NamedBuildTarget.Android;
        PlayerSettings.SetApplicationIdentifier(nb, BundleId);
        PlayerSettings.SetScriptingBackend(nb, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
        PlayerSettings.Android.forceInternetPermission = true;
        // GameActivity'de TouchScreenKeyboard (e-posta/şifre kutuları) güvenilir açılmıyor: klasik Activity.
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
        PlayerSettings.Android.renderOutsideSafeArea = false;
        string code = WebGLBuild.ArgValue("-androidVersionCode");
        if (int.TryParse(code, out int vc)) PlayerSettings.Android.bundleVersionCode = vc;

        string ks = WebGLBuild.ArgValue("-androidKeystoreName");
        if (!string.IsNullOrEmpty(ks) && System.IO.File.Exists(ks))
        {
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = System.IO.Path.GetFullPath(ks);
            PlayerSettings.Android.keystorePass = WebGLBuild.ArgValue("-androidKeystorePass");
            PlayerSettings.Android.keyaliasName = WebGLBuild.ArgValue("-androidKeyaliasName");
            PlayerSettings.Android.keyaliasPass = WebGLBuild.ArgValue("-androidKeyaliasPass");
        }
        else Debug.LogWarning("Keystore yok: hata ayıklama anahtarıyla imzalanacak (" + ks + ")");

        bool aab = WebGLBuild.ArgValue("-androidExportType") == "androidAppBundle";
        EditorUserBuildSettings.buildAppBundle = aab;
        string outPath = WebGLBuild.ArgValue("-customBuildPath") ?? (aab ? "build/Android/tmtbs3d.aab" : "build/Android/tmtbs3d.apk");
        Run(outPath, BuildTarget.Android);
    }

    public static void BuildIOS()
    {
        Common();
        var nb = NamedBuildTarget.iOS;
        PlayerSettings.SetApplicationIdentifier(nb, BundleId);
        PlayerSettings.iOS.targetOSVersionString = "13.0";
        PlayerSettings.iOS.appleEnableAutomaticSigning = false;
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly; // iPad ekran görüntüsü gerekmesin
        PlayerSettings.iOS.appleDeveloperTeamID = "PWNYB7XZ3P";
        string code = WebGLBuild.ArgValue("-buildNumber");
        PlayerSettings.iOS.buildNumber = string.IsNullOrEmpty(code) ? "1" : code;
        string outPath = WebGLBuild.ArgValue("-customBuildPath") ?? "build/iOS/iOS";
        Run(outPath, BuildTarget.iOS);
    }

    static void Run(string outPath, BuildTarget target)
    {
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Salon.unity" },
            locationPathName = outPath,
            target = target,
            options = BuildOptions.None
        });
        Debug.Log($"{target} derleme sonucu: {report.summary.result}, {report.summary.totalSize} bayt → {outPath}");
        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
