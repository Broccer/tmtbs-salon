using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// GitHub Actions (game-ci) bu metodu çağırır.
/// Sahne tamamen koddan kurulduğu için derleyicinin "kullanılmıyor" sanıp attığı
/// shader'ları ve motor sınıflarını burada korumaya alıyoruz (futbol projesinde
/// pembe ekran + eksik SphereCollider hatası bu yüzden çıkmıştı).
/// </summary>
public static class WebGLBuild
{
    const string ScenePath = "Assets/Scenes/Salon.unity";

    public static void Build()
    {
        EnsureScene();
        EnsureShaders();

        PlayerSettings.productName = "Trade of MTBS - Seans Salonu";
        PlayerSettings.companyName = "Bavsla Games";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.stripEngineCode = false;
        PlayerSettings.runInBackground = true;

        string outPath = ArgValue("-customBuildPath") ?? "build/WebGL/WebGL";
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = outPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });
        Debug.Log($"WebGL derleme sonucu: {report.summary.result}, {report.summary.totalSize} bayt → {outPath}");
        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    [MenuItem("Salon/Sahneyi oluştur")]
    public static void EnsureScene()
    {
        if (System.IO.File.Exists(ScenePath)) return;
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        new GameObject("SalonKurucu").AddComponent<SalonBuilder>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.Refresh();
    }

    static void EnsureShaders()
    {
        Shader standard = Shader.Find("Standard");
        Shader text = Shader.Find("GUI/Text Shader");
        Shader unlit = Shader.Find("Unlit/Texture");
        Shader unlitColor = Shader.Find("Unlit/Color");

        System.IO.Directory.CreateDirectory("Assets/Resources");
        MakeMaterial("Assets/Resources/SalonBase.mat", standard);
        MakeMaterial("Assets/Resources/SalonUnlit.mat", unlit);
        MakeMaterial("Assets/Resources/SalonGlow.mat", unlitColor);

        MakeMaterial("Assets/Resources/SalonText.mat", text);
        // Always-included listesine DOKUNMUYORUZ: gömülü shader'lar oraya eklenince
        // derlemeler unity_builtin_extra'yı yazamadı (m_LockCount assertion).
        // Resources'taki malzemeler shader'ları derlemeye zaten sokuyor.
    }

    static void MakeMaterial(string path, Shader shader)
    {
        if (shader == null || AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        AssetDatabase.CreateAsset(new Material(shader), path);
        AssetDatabase.SaveAssets();
    }

    static string ArgValue(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}
