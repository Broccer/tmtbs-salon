#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

/// <summary>
/// iOS Xcode projesine Apple ile giriş yetkisini (entitlement) ve AuthenticationServices çerçevesini ekler.
/// App ID'de (com.bavsla.tmtbs3d) "Sign In with Apple" özelliği de Apple Developer'da açık olmalı.
/// </summary>
public static class IOSPostBuild
{
    [PostProcessBuild(100)]
    public static void OnPostprocessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        string projPath = PBXProject.GetPBXProjectPath(path);
        var proj = new PBXProject();
        proj.ReadFromFile(projPath);
        string main = proj.GetUnityMainTargetGuid();
        string fw = proj.GetUnityFrameworkTargetGuid();
        proj.AddFrameworkToProject(fw, "AuthenticationServices.framework", false);
        proj.AddFrameworkToProject(main, "AuthenticationServices.framework", false);
        proj.WriteToFile(projPath);

        var caps = new ProjectCapabilityManager(projPath, "Unity-iPhone/tmtbs3d.entitlements", null, main);
        caps.AddSignInWithApple();
        caps.WriteToFile();
    }
}
#endif
