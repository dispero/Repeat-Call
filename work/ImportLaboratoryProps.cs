using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ImportLaboratoryProps
{
    const string ParentFolder = "Assets/Golovomka/Rooms";
    const string FolderName = "LaboratoryProps";
    const string OutputFolder = ParentFolder + "/" + FolderName;
    const float PixelsPerUnit = 36;

    static readonly (string name, int order)[] Props =
    {
        ("TeaThermos", 120),
        ("EnamelMug", 120),
        ("TeaBox", 120),
        ("WallCalendar1996", -92),
        ("ExperimentLogbook", 120),
        ("CyrillicEquipmentLabel", 121),
        ("ConduitJunctionBox", -92),
        ("RingRepairTray", 100),
        ("ChartPaperRoll", 120),
        ("ChartTraces", 120),
        ("RelayWiringDiagram", -92),
        ("WallFireExtinguisher", -92),
        ("RoomNumberPlate", -92),
    };

    public static string[] Run()
    {
        var project = Path.GetDirectoryName(Application.dataPath);
        var sourceFolder = Path.Combine(project, "work", "laboratory-props-export");
        var lit = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
        if (!lit) throw new InvalidOperationException("Материал лаборатории не найден.");

        foreach (var prop in Props)
            if (!File.Exists(Path.Combine(sourceFolder, prop.name + ".png")))
                throw new FileNotFoundException(prop.name);

        var openScenes = Enumerable.Range(0, SceneManager.sceneCount)
            .Select(i => SceneManager.GetSceneAt(i))
            .Select(scene => (scene, scene.isDirty, scene.rootCount)).ToArray();
        var activeScene = SceneManager.GetActiveScene();
        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder(ParentFolder, FolderName);

        var preview = EditorSceneManager.NewPreviewScene();
        var results = new List<string>();
        try
        {
            foreach (var prop in Props)
            {
                var texturePath = OutputFolder + "/" + prop.name + ".png";
                var prefabPath = OutputFolder + "/" + prop.name + ".prefab";
                var destination = Path.Combine(project, texturePath);
                if (!File.Exists(destination))
                    File.Copy(Path.Combine(sourceFolder, prop.name + ".png"), destination);
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                float resolution = prop.name is "TeaBox" or "WallCalendar1996" or
                    "CyrillicEquipmentLabel" or "RoomNumberPlate" ? PixelsPerUnit * 2 : PixelsPerUnit;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = resolution;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
                if (!sprite || sprite.texture.filterMode != FilterMode.Point ||
                    sprite.texture.mipmapCount != 1 || sprite.pixelsPerUnit != resolution)
                    throw new InvalidOperationException("Не удалось настроить спрайт " + prop.name);

                if (!AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath))
                {
                    var temporary = new GameObject(prop.name);
                    try
                    {
                        SceneManager.MoveGameObjectToScene(temporary, preview);
                        var renderer = temporary.AddComponent<SpriteRenderer>();
                        renderer.sprite = sprite;
                        renderer.sharedMaterial = lit;
                        renderer.sortingOrder = prop.order;
                        PrefabUtility.SaveAsPrefabAsset(temporary, prefabPath, out bool saved);
                        if (!saved) throw new IOException(prefabPath);
                    }
                    finally
                    {
                        Object.DestroyImmediate(temporary);
                    }
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var verified = prefab ? prefab.GetComponent<SpriteRenderer>() : null;
                if (!verified || verified.sprite != sprite || verified.sharedMaterial != lit ||
                    verified.sortingOrder != prop.order || prefab.GetComponent<Collider2D>())
                    throw new InvalidOperationException("Проверка префаба не пройдена: " + prop.name);
                results.Add(prop.name + " " + sprite.rect.width + "×" + sprite.rect.height);
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }

        if (SceneManager.GetActiveScene() != activeScene || openScenes.Any(item =>
            item.scene.isDirty != item.isDirty || item.scene.rootCount != item.rootCount))
            throw new InvalidOperationException("Состояние одной из открытых сцен изменилось.");
        return results.ToArray();
    }
}
