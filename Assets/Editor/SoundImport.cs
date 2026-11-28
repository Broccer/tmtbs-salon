using UnityEditor;
using UnityEngine;

/// <summary>
/// Resources/Sounds altındaki gerçek kayıtların (BigSoundBank, CC0) içe aktarma ayarları.
/// Uzun salon uğultusu bellekte sıkıştırılmış tutulur (açılınca ~40 MB'a açılmasın); kısa efektler açılarak yüklenir.
/// Meta dosyaları depoda olmadığından ayarlar her derlemede buradan verilir.
/// </summary>
public class SoundImport : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Resources/Sounds/")) return;
        var imp = (AudioImporter)assetImporter;
        bool longLoop = assetPath.Contains("ugultu");
        var s = imp.defaultSampleSettings;
        s.loadType = longLoop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = longLoop ? 0.45f : 0.6f;
        s.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
        s.sampleRateOverride = longLoop ? 32000u : 44100u;
        imp.defaultSampleSettings = s;
        imp.forceToMono = !longLoop;
        imp.loadInBackground = longLoop;
    }
}
