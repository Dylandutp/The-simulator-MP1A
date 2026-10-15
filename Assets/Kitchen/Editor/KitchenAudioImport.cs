using System;
using UnityEditor;
using UnityEngine;

namespace HouseSim.Kitchen.Editor
{
    // Keep the supplied recordings intact; only their Unity import settings change.
    public sealed class KitchenAudioImport : AssetPostprocessor
    {
        const string AudioFolder = "Assets/Kitchen/Resources/KitchenAudio/";

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(AudioFolder, StringComparison.Ordinal)) return;

            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            var serialized = new SerializedObject(importer);
            var normalize = serialized.FindProperty("m_Normalize");
            if (normalize != null)
            {
                normalize.boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            importer.loadInBackground = false;
            importer.ambisonic = false;

            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            settings.preloadAudioData = true;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
        }

        public override uint GetVersion() => 1;
    }
}
