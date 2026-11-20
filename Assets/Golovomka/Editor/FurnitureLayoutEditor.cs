using Golovomka;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(FurnitureLayout))]
public sealed class FurnitureLayoutEditor : Editor
{
    readonly BoxBoundsHandle box = new BoxBoundsHandle();
    int room = 1;
    readonly string[] rooms = { "Участок", "Комната 1", "Комната 2", "Комната 3", "Комната 4" };

    void OnEnable() => SceneView.duringSceneGui += DrawScene;
    void OnDisable() => SceneView.duringSceneGui -= DrawScene;

    [MenuItem("Golovomka/Edit Furniture Hitboxes")]
    public static void Open()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Остановите Play Mode перед редактированием хитбоксов."); return; }
        var game = Object.FindAnyObjectByType<EchoMvp>();
        if (!game) { Debug.LogWarning("Откройте сцену Game."); return; }
        var layout = game.GetComponent<FurnitureLayout>();
        if (!layout)
        {
            layout = Undo.AddComponent<FurnitureLayout>(game.gameObject);
            layout.InitializeDefaults();
            EditorUtility.SetDirty(layout);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        }
        UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(layout, true);
        Selection.activeGameObject = layout.gameObject;
        ActiveEditorTracker.sharedTracker.ForceRebuild();
        SceneView.lastActiveSceneView?.Focus();
    }

    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        root.Add(new HelpBox("Вне Play Mode выберите комнату и нажмите «Показать в Scene». Центр прямоугольника перемещает область, крайние ручки меняют размер. Ctrl+Z — отмена, Ctrl+S — сохранить сцену. Координаты Areas — мировые. Двери и стены здесь не редактируются.", HelpBoxMessageType.Info));
        var availableRooms = new System.Collections.Generic.List<string>(rooms);
        void AddRoomLink(int index, string path)
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (!scene) return;
            availableRooms.Remove(rooms[index]);
            root.Add(new Button(() => Selection.activeObject = scene) { text = "Найти сцену " + scene.name + " в Project" });
        }
        AddRoomLink(0, PoliceStationLayout.ScenePath);
        AddRoomLink(1, LaboratoryRoom.ScenePath);
        AddRoomLink(2, Room02Layout.ScenePath);
        if (!availableRooms.Contains(rooms[room])) room = System.Array.IndexOf(rooms, availableRooms[0]);
        var choice = new PopupField<string>("Комната", availableRooms, rooms[room]);
        choice.RegisterValueChangedCallback(_ => { room = System.Array.IndexOf(rooms, choice.value); FrameRoom(); });
        root.Add(choice);
        root.Add(new Button(FrameRoom) { text = "Показать в Scene" });
        root.Add(new PropertyField(serializedObject.FindProperty("areas"), "Области мебели"));
        return root;
    }

    void FrameRoom()
    {
        var view = SceneView.lastActiveSceneView;
        if (!view) return;
        view.in2DMode = true;
        view.LookAt(Vector3.zero, Quaternion.identity, 12, true, true);
        view.Repaint();
    }

    void DrawScene(SceneView view)
    {
        if (EditorApplication.isPlaying) return;
        var layout = (FurnitureLayout)target;
        // Плоская подложка только в 2D Scene; не создаёт объектов и не попадает в игру.
        if (room <= 1 && SceneView.currentDrawingSceneView.in2DMode)
        {
            var texture = Resources.Load<Texture2D>(room == 0 ? "Art/PoliceStationLayout" : "Art/LaboratoryIntroduction");
            if (texture)
            {
                var rect = FurnitureLayout.FromPixels(room, 0, 0, room == 0 ? 1586 : 1599, room == 0 ? 992 : 984);
                var topLeft = HandleUtility.WorldToGUIPoint(new Vector3(rect.xMin, rect.yMax));
                var bottomRight = HandleUtility.WorldToGUIPoint(new Vector3(rect.xMax, rect.yMin));
                Handles.BeginGUI();
                GUI.DrawTexture(new Rect(topLeft, bottomRight - topLeft), texture, ScaleMode.StretchToFill);
                Handles.EndGUI();
            }
        }
        foreach (var area in layout.areas)
        {
            if (area.room != room) continue;
            var rect = area.bounds;
            Handles.color = Color.green;
            Handles.DrawWireCube(rect.center, rect.size);
            Handles.Label(new Vector3(rect.xMin, rect.yMax), area.label);
            EditorGUI.BeginChangeCheck();
            var center = Handles.Slider2D(rect.center, Vector3.forward, Vector3.right, Vector3.up,
                HandleUtility.GetHandleSize(rect.center) * .055f, Handles.RectangleHandleCap, Vector2.zero);
            box.axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y;
            box.center = center;
            box.size = new Vector3(rect.width, rect.height, 0);
            box.SetColor(Color.green);
            box.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(layout, "Изменить хитбокс мебели");
                area.bounds = new Rect((Vector2)box.center - (Vector2)box.size * .5f,
                    new Vector2(Mathf.Max(.01f, box.size.x), Mathf.Max(.01f, box.size.y)));
                EditorUtility.SetDirty(layout);
                PrefabUtility.RecordPrefabInstancePropertyModifications(layout);
            }
        }
    }
}
