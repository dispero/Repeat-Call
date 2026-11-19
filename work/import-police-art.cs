using UnityEditor;
using UnityEngine;
AssetDatabase.Refresh();
foreach (var name in new[] { "PoliceFurniture", "PoliceSurfaces", "PoliceStationLayout" })
{
    string path = "Assets/Golovomka/Resources/Art/" + name + ".png";
    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
    importer.textureType = TextureImporterType.Default;
    importer.filterMode = FilterMode.Point;
    importer.mipmapEnabled = false;
    importer.textureCompression = TextureImporterCompression.Uncompressed;
    importer.npotScale = TextureImporterNPOTScale.None;
    importer.maxTextureSize = 2048;
    importer.alphaIsTransparency = true;
    importer.SaveAndReimport();
}
AssetDatabase.SaveAssets();
return EchoMvpTools.Verify();

