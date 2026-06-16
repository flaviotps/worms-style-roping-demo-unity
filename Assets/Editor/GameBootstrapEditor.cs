using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameBootstrap))]
public class GameBootstrapEditor : Editor {
    public override void OnInspectorGUI() {
        DrawDefaultInspector();

        var bootstrap = (GameBootstrap)target;

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Map Preview", EditorStyles.boldLabel);

        if (GUILayout.Button("Generate Preview")) {
            bootstrap.GeneratePreview();
        }
        if (GUILayout.Button("Clear Preview")) {
            bootstrap.ClearPreview();
        }
    }
}
