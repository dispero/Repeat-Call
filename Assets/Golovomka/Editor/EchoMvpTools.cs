using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Golovomka;

public static class EchoMvpTools
{
    public const string ScenePath = "Assets/Golovomka/Scenes/Game.unity";
    const string IvanPrefabPath = "Assets/Golovomka/Characters/Prefabs/Ivan.prefab";
    [MenuItem("Golovomka/Prepare Echo MVP")]
    public static void Prepare()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        foreach (string path in new[] { "Assets/Golovomka/Resources/Art/Interiors.png", "Assets/Golovomka/Resources/Art/Room.png" })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 4096;
            importer.alphaIsTransparency = true; importer.SaveAndReimport();
        }
        Directory.CreateDirectory("Assets/Golovomka/Scenes"); AssetDatabase.Refresh();
        string materialPath = "Assets/Golovomka/Resources/Art/MvpSprite.mat";
        if (!AssetDatabase.LoadAssetAtPath<Material>(materialPath))
        {
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (!shader) throw new Exception("Missing URP 2D unlit shader");
            AssetDatabase.CreateAsset(new Material(shader), materialPath);
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject(Path.GetFileNameWithoutExtension(ScenePath), typeof(EchoMvp));
        CreateSceneActors(root.GetComponent<EchoMvp>());
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
        foreach (string roomPath in new[] { PoliceStationLayout.ScenePath, LaboratoryRoom.ScenePath })
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(roomPath))
                scenes.Add(new EditorBuildSettingsScene(roomPath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        PlayerSettings.companyName = "Golovomka"; PlayerSettings.productName = "Golovomka — Echo MVP";
        PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.runInBackground = true; QualitySettings.antiAliasing = 0;
        AssetDatabase.SaveAssets();
        Debug.Log("ECHO_MVP_PREPARED");
    }

    public static void CreateSceneActors(EchoMvp mvp)
    {
        var settings = new SerializedObject(mvp);
        var cameraField = settings.FindProperty("roomCamera");
        var heroField = settings.FindProperty("hero");

        if (!cameraField.objectReferenceValue)
        {
            var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, mvp.gameObject.scene);
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 8.4375f;
            camera.backgroundColor = new Color(.035f, .055f, .065f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.allowHDR = camera.allowMSAA = false;
            var pixel = camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
            pixel.assetsPPU = 16;
            pixel.refResolutionX = 480;
            pixel.refResolutionY = 270;
            pixel.gridSnapping = UnityEngine.Rendering.Universal.PixelPerfectCamera.GridSnapping.PixelSnapping;
            pixel.cropFrame = UnityEngine.Rendering.Universal.PixelPerfectCamera.CropFrame.Windowbox;
            pixel.enabled = false;
            cameraField.objectReferenceValue = camera;
        }

        if (!heroField.objectReferenceValue)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IvanPrefabPath);
            if (!prefab) throw new InvalidOperationException("Префаб Ивана не найден: " + IvanPrefabPath);
            var hero = (GameObject)PrefabUtility.InstantiatePrefab(prefab, mvp.gameObject.scene);
            heroField.objectReferenceValue = hero.GetComponent<SpriteRenderer>();
        }

        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Golovomka/Verify Echo Model")]
    public static string Verify()
    {
        void Check(bool v, string label) { if (!v) throw new Exception(label); }
        EchoTape Tape(float end, int zone, float x)
        {
            return new EchoTape { duration = end, frames = new System.Collections.Generic.List<EchoFrame> {
                new EchoFrame(0, new Vector2(x, 0), zone, 3), new EchoFrame(end, new Vector2(x + 2, 0), zone, 3) } };
        }
        var t = Tape(10, 0, 0);
        Check(t.Sample(5, out var f) && Mathf.Abs(f.x - 1) < .001f, "Interpolation");
        Check(!t.Sample(10, out f), "Disappear at end"); Check(!t.Sample(-1, out f), "No pre-start copy");
        var h = new EchoHistory();
        var first = Tape(10, 1, 0); first.room = 1;
        var second = Tape(20, 2, 2); second.room = 2;
        var replacement = Tape(30, 1, 4); replacement.room = 1;
        h.Push(first); h.Push(second); h.Push(replacement);
        Check(h.cycle == 4 && h.tapes.Count == 2 && h.ForRoom(1) == replacement &&
            h.ForRoom(2) == second, "Only the newest copy survives in each room");
        var plate = new Vector2(1, 2);
        Check(EchoHistory.OnPlate(new EchoFrame(1, plate, 1, 3), plate), "Plate in lab");
        Check(!EchoHistory.OnPlate(new EchoFrame(1, plate, 0, 3), plate), "Room isolation");
        string path = Path.Combine(Path.GetTempPath(), "echo-test-" + Guid.NewGuid() + ".json");
        try
        {
            h.Save(path); h.completed = true; h.Save(path);
            var loaded = EchoHistory.Load(path);
            Check(loaded != null && loaded.completed && loaded.cycle == 4 && loaded.tapes.Count == 2, "Save/load");
            File.WriteAllText(path, "invalid"); loaded = EchoHistory.Load(path);
            Check(loaded != null && !loaded.completed, "Backup recovery");
        }
        finally { foreach (var file in new[] { path, path + ".bak", path + ".tmp" }) if (File.Exists(file)) File.Delete(file); }
        string result = "PASS: interpolation, end, pre-start, one copy per room, room isolation, plate, save/load, backup recovery";
        Debug.Log(result); return result;
    }
}
