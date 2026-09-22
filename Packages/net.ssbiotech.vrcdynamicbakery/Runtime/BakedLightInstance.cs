using UdonSharp;
using UnityEditor;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class BakedLightInstance : UdonSharpBehaviour {
    [HideInInspector] public Material skybox;
    [HideInInspector] public Color fogColor;
    [HideInInspector] public float fogDensity;
    [HideInInspector] public Texture2D[] lightmaps;
    [HideInInspector] public BakedLightConfig[] configs;

    public void SetLightmaps(
        Renderer[] meshes,
        Vector4[] lightmapScale,
        int[] lightmapIndices,
        MaterialPropertyBlock block
    ) {
        if (skybox != null) {
            RenderSettings.skybox = skybox;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
        }
        foreach (var config in configs)
            if (config != null)
                config.SetObjectVisibility(true);
        for (var meshIdx = 0; meshIdx < meshes.Length; ++meshIdx) {
            var mesh = meshes[meshIdx];
            if (mesh == null) continue;
            var idx = mesh.lightmapIndex;
            if (idx < 0 || idx >= lightmaps.Length) continue;
            mesh.GetPropertyBlock(block);
            // ReSharper disable once Unity.PreferAddressByIdToGraphicsParams
            block.SetTexture("unity_Lightmap", lightmaps[lightmapIndices[meshIdx]]);
            mesh.SetPropertyBlock(block);
            mesh.lightmapScaleOffset = lightmapScale[meshIdx];
        }
    }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
    private static bool _debugFoldout;
    [CustomEditor(typeof(BakedLightInstance)), CanEditMultipleObjects]
    internal class ManagerEditor : Editor {
        public override void OnInspectorGUI() {
            serializedObject.Update();
            if (GUILayout.Button("Activate")) {
                var instance = target as BakedLightInstance;
                instance.gameObject
                    .GetComponentInParent<BakedLightArea>()
                    .Activate(instance.transform.GetSiblingIndex(), true);
            }
            _debugFoldout = EditorGUILayout.Foldout(_debugFoldout, "Debug");
            if (_debugFoldout) {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("skybox"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fogColor"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fogDensity"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("lightmaps"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("configs"));
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
