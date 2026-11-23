using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Mixamo karakterleri ve animasyonları:
///  - Resources/Characters/*.fbx ve Resources/Anims/*.fbx Humanoid olarak içe alınır.
///  - Animasyonlar döngülü, kök hareketi gövdeye gömülü (yerinde) olur.
///  - Karakter dokuları CharTex/ altına çıkarılır ve 512 px'e küçültülür (WebGL boyutu için).
///  - Resources/Borsaci.controller: Durum (0 bekle, 1 yürü, 2 otur, 3 konuş) parametreli animatör.
/// </summary>
public class CharImport : AssetPostprocessor
{
    const string CharDir = "Assets/Resources/Characters";
    const string AnimDir = "Assets/Resources/Anims";
    const string TexDir = "Assets/CharTex";
    public const string ControllerPath = "Assets/Resources/Borsaci.controller";

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(CharDir) && !assetPath.StartsWith(AnimDir)) return;
        var mi = (ModelImporter)assetImporter;
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importBlendShapes = false;
        mi.meshCompression = ModelImporterMeshCompression.Medium;
        mi.optimizeGameObjects = false;
        bool anim = assetPath.StartsWith(AnimDir);
        mi.importAnimation = anim;
        mi.materialImportMode = anim ? ModelImporterMaterialImportMode.None : ModelImporterMaterialImportMode.ImportViaMaterialDescription;
    }

    void OnPreprocessAnimation()
    {
        if (!assetPath.StartsWith(AnimDir)) return;
        var mi = (ModelImporter)assetImporter;
        var clips = mi.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return;
        string name = Path.GetFileNameWithoutExtension(assetPath);
        foreach (var c in clips)
        {
            c.name = name;
            c.loopTime = true;
            c.loopPose = true;
            c.lockRootRotation = true;
            c.keepOriginalOrientation = true;
            c.lockRootHeightY = true;
            c.keepOriginalPositionY = true;
            c.heightFromFeet = false;
            c.lockRootPositionXZ = true;
            c.keepOriginalPositionXZ = true;
        }
        mi.clipAnimations = clips.Take(1).ToArray();
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(TexDir)) return;
        var ti = (TextureImporter)assetImporter;
        ti.maxTextureSize = 512;
        ti.textureCompression = TextureImporterCompression.Compressed;
        ti.mipmapEnabled = true;
        if (assetPath.ToLowerInvariant().Contains("normal")) ti.textureType = TextureImporterType.NormalMap;
    }

    /// <summary>Derlemeden önce: dokuları dışarı çıkar, animatörü kur.</summary>
    public static void Prepare()
    {
        if (!Directory.Exists(CharDir)) return;
        foreach (var f in Directory.GetFiles(CharDir, "*.fbx"))
        {
            string p = f.Replace('\\', '/');
            string dir = TexDir + "/" + Path.GetFileNameWithoutExtension(p);
            if (Directory.Exists(dir) && Directory.GetFiles(dir).Length > 0) continue;
            Directory.CreateDirectory(dir);
            var mi = AssetImporter.GetAtPath(p) as ModelImporter;
            if (mi == null) continue;
            try
            {
                mi.ExtractTextures(dir);
                AssetDatabase.Refresh();
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                Debug.Log("Karakter dokuları çıkarıldı: " + p);
            }
            catch (System.Exception e) { Debug.LogWarning("Doku çıkarılamadı " + p + ": " + e.Message); }
        }
        BuildController();
    }

    static AnimationClip Clip(string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath(AnimDir + "/" + name + ".fbx")
            .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }

    static void BuildController()
    {
        var idle = Clip("Idle");
        var walk = Clip("Walking");
        var sit = Clip("Sitting");
        var talk = Clip("Talking");
        if (idle == null) { Debug.LogWarning("Idle animasyonu yok; animatör kurulmadı."); return; }
        if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        ac.AddParameter("Durum", AnimatorControllerParameterType.Int);
        ac.AddParameter("Hiz", AnimatorControllerParameterType.Float);
        var sm = ac.layers[0].stateMachine;
        AnimationClip[] clips = { idle, walk ?? idle, sit ?? idle, talk ?? idle };
        string[] names = { "Bekle", "Yuru", "Otur", "Konus" };
        for (int i = 0; i < clips.Length; i++)
        {
            var st = sm.AddState(names[i], new Vector3(300, 60 * i, 0));
            st.motion = clips[i];
            if (i == 1) { st.speedParameter = "Hiz"; st.speedParameterActive = true; }
            if (i == 0) sm.defaultState = st;
            var tr = sm.AddAnyStateTransition(st);
            tr.AddCondition(AnimatorConditionMode.Equals, i, "Durum");
            tr.duration = 0.18f;
            tr.hasExitTime = false;
            tr.canTransitionToSelf = false;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Animatör kuruldu: " + ControllerPath);
    }
}
