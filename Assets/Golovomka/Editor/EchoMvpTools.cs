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
    const string IvanPreviewPath = "Assets/Golovomka/Resources/Art/IvanIdlePreview.png";
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
            var hero = new GameObject("Ivan", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            SceneManager.MoveGameObjectToScene(hero.gameObject, mvp.gameObject.scene);
            hero.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IvanPreviewPath);
            hero.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Golovomka/Resources/Art/MvpSprite.mat");
            hero.sortingOrder = 100;
            heroField.objectReferenceValue = hero;
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
        var h = new EchoHistory { copySlots = 2 }; var a = Tape(10, 0, 0); var b = Tape(20, 1, 2); var c = Tape(30, 1, 4);
        h.Push(a, 1); h.Push(b, 1); h.Push(c, 2);
        Check(h.cycle == 4 && h.tapes.Count == 2 && h.tapes[0] == b && h.tapes[1] == c, "Last two cycles");
        var firstStage = new EchoHistory(); firstStage.Push(a, 1); firstStage.Push(b, 1);
        Check(firstStage.copySlots == 1 && firstStage.tapes.Count == 1 && firstStage.tapes[0] == b, "One copy initially");
        firstStage.Push(c, 2);
        Check(firstStage.tapes.Count == 2 && firstStage.tapes[0] == b, "Second slot preserves previous recording");
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
        string result = "PASS: interpolation, end, pre-start, last two, room isolation, plate, save/load, backup recovery";
        Debug.Log(result); return result;
    }
}
