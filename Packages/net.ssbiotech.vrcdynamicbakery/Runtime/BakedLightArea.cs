using UdonSharp;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.UdonNetworkCalling;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class BakedLightArea : UdonSharpBehaviour {
    [HideInInspector] public Renderer[] renderers;
    [HideInInspector] public Vector4[] lightmapScale;
    [HideInInspector] public int[] lightmapIndices;
    [HideInInspector] public int lightmapSize;

    [HideInInspector] [UdonSynced] public int selection;

    public MaterialPropertyBlock Block;
    private BakedLightInstance _activeInstance;
    private bool _initialized;

    private void Start() {
        Block = new MaterialPropertyBlock();
        Activate(0, false);
    }

    public override void OnDeserialization() {
        if (!_initialized) Activate(selection, false);
        _initialized = true;
    }

    public void Toggle(BakedLightConfig newConfig, bool synced = true) {
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var group in GetComponentsInChildren<BakedLightGroup>(false))
            if (group.selection == newConfig)
                return;
        BakedLightInstance matchingInstance = null;
        foreach (var instance in GetComponentsInChildren<BakedLightInstance>(false)) {
            var instanceMatch = true;
            foreach (var config in instance.configs) {
                var configMatch = config == newConfig;
                // ReSharper disable once LoopCanBeConvertedToQuery
                foreach (var group in GetComponentsInChildren<BakedLightGroup>(false)) {
                    if (group.selection != config) continue;
                    configMatch = true;
                    break;
                }
                if (configMatch && config != newConfig.GetComponentInParent<BakedLightGroup>(false).selection) continue;
                instanceMatch = false;
                break;
            }
            if (!instanceMatch) continue;
            matchingInstance = instance;
            break;
        }
        if (matchingInstance == null) return;
        if (synced)
            SendCustomNetworkEvent(
                NetworkEventTarget.All,
                nameof(Activate),
                matchingInstance.transform.GetSiblingIndex(),
                false
            );
        else Activate(matchingInstance.transform.GetSiblingIndex(), false);
    }

    [NetworkCallable]
    public void Activate(int index, bool force) {
        var instance = GetComponentsInChildren<BakedLightInstance>(false)[index];
        if (!force && _activeInstance == instance) return;
        selection = index;
        RequestSerialization();
        _activeInstance = instance;
        foreach (var config in instance.configs)
            config.GetComponentInParent<BakedLightGroup>(false).selection = config;
        foreach (var config in GetComponentsInChildren<BakedLightConfig>(false)) config.SetObjectVisibility(false);
        instance.SetLightmaps(renderers, lightmapScale, lightmapIndices, Block);
    }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
    private static bool _debugFoldout;
    [CustomEditor(typeof(BakedLightArea)), CanEditMultipleObjects]
    internal class ManagerEditor : Editor {
        public override void OnInspectorGUI() {
            serializedObject.Update();
            _debugFoldout = EditorGUILayout.Foldout(_debugFoldout, "Debug");
            if (_debugFoldout) {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("renderers"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("lightmapScale"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("lightmapIndices"));
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
