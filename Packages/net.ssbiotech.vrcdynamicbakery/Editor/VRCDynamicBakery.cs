#if UNITY_EDITOR
using UnityEditor;
using static UnityEditor.EditorUtility;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.GUILayout;

namespace Editor {
#if UNITY_EDITOR
    public class VrcDynamicBakery : EditorWindow {
        [MenuItem("VRC Dynamic Bakery/Open Bakery")]
        public static void ShowWindow() {
            var window = GetWindow<VrcDynamicBakery>();
            window.titleContent = new GUIContent("VRC Dynamic Bakery");
            window.Show();
        }

        private void OnEnable() {
            EditorApplication.hierarchyChanged += VerifyState;
            EditorApplication.playModeStateChanged += ModeChanged;
        }

        private void OnDisable() {
            EditorApplication.hierarchyChanged -= VerifyState;
            EditorApplication.playModeStateChanged -= ModeChanged;
        }

        private static void VerifyState() {
            foreach (var area in FindObjectsOfType<BakedLightArea>(false)) {
                if (area.GetComponentsInChildren<BakedLightGroup>(false).Length < 1) {
                    new GameObject {
                        transform = { position = area.transform.position, parent = area.transform },
                        name = $"LightGroup;{area.name}"
                    }.AddComponent<BakedLightGroup>();
                }
                foreach (var group in area.GetComponentsInChildren<BakedLightGroup>(false)) {
                    while (group.GetComponentsInChildren<BakedLightConfig>(false).Length < 2) {
                        new GameObject {
                            transform = { position = group.transform.position, parent = group.transform },
                            name = $"{group.name};{group.GetComponentsInChildren<BakedLightConfig>(false).Length}"
                        }.AddComponent<BakedLightConfig>();
                    }
                }
            }
        }

        private void ModeChanged(PlayModeStateChange state) {
            if (state == PlayModeStateChange.EnteredEditMode)
                SetDefaults(FindObjectsOfType<BakedLightArea>(false));
        }

        private void OnGUI() {
            var areas = FindObjectsOfType<BakedLightArea>(false);
            EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
            BeginHorizontal();
            if (Button("Bake All", ExpandWidth(false))) {
                PopulateFields(areas);
                PreBakeLightmaps(areas);
                BakeLightmaps(areas);
                PostBakeLightmaps(areas);
                FetchLightmaps(areas);
                SetDefaults(FindObjectsOfType<BakedLightArea>(false));
                ClearProgressBar();
                AssetDatabase.Refresh();
            }
            if (Button("Refresh All", ExpandWidth(false))) {
                SetDefaults(FindObjectsOfType<BakedLightArea>(false));
                ClearProgressBar();
                AssetDatabase.Refresh();
            }
            EndHorizontal();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
            Label($"Areas: {FindObjectsOfType<BakedLightArea>(false).Length}");
            Label($"Groups: {FindObjectsOfType<BakedLightGroup>(false).Length}");
            Label($"Configs: {FindObjectsOfType<BakedLightConfig>(false).Length}");
            Label($"Instances: {FindObjectsOfType<BakedLightInstance>(false).Length}");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            if (Button("Populate Fields", ExpandWidth(false))) {
                PopulateFields(areas);
                ClearProgressBar();
                AssetDatabase.Refresh();
            }
            if (Button("Quick Bake", ExpandWidth(false))) {
                PreBakeLightmaps(areas);
                PostBakeLightmaps(areas);
                ClearProgressBar();
                AssetDatabase.Refresh();
            }
        }

        [MenuItem("VRC Dynamic Bakery/Bake Selected &b")]
        private static void BakeSelected() {
            var areas = new List<BakedLightArea>();
            areas.AddRange(Selection.transforms.SelectMany(it => it.GetComponentsInChildren<BakedLightArea>()));
            PopulateFields(areas.ToArray());
            PreBakeLightmaps(areas.ToArray());
            BakeLightmaps(areas.ToArray());
            PostBakeLightmaps(areas.ToArray());
            FetchLightmaps(areas.ToArray());
            SetDefaults(FindObjectsOfType<BakedLightArea>(false));
            ClearProgressBar();
            AssetDatabase.Refresh();
        }

        [MenuItem("VRC Dynamic Bakery/Refresh Lightmaps &r")]
        private static void RefreshAll() {
            SetDefaults(FindObjectsOfType<BakedLightArea>(false));
            ClearProgressBar();
            AssetDatabase.Refresh();
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

        private static void PopulateFields(BakedLightArea[] areas) {
            foreach (var area in areas) {
                area.renderers = new[] { area.gameObject }.SelectMany(it => it.GetComponentsInChildren<Renderer>(true))
                    .Where(IsStatic)
                    .ToArray();
                var groupedConfigs = area.GetComponentsInChildren<BakedLightConfig>(false)
                    .GroupBy(it => it.GetComponentInParent<BakedLightGroup>(false))
                    .ToList();
                foreach (var grouping in groupedConfigs) {
                    var group = grouping.Key;
                    var configs = grouping.ToList();
                    if (grouping.Count() == 1) {
                        var noneObject = new GameObject {
                            transform = { position = group.transform.position, parent = group.transform },
                            name = $"{group.name}Off"
                        };
                        noneObject.transform.SetSiblingIndex(group.transform.GetSiblingIndex());
                        configs.Insert(0, noneObject.AddComponent<BakedLightConfig>());
                    }
                    foreach (var config in configs) {
                        config.toggledObjects
                            = (from Transform child in config.transform select child.gameObject).ToArray();
                        config.nonStaticObjects = config.toggledObjects
                            .SelectMany(it => it.GetComponentsInChildren<Transform>(true))
                            .Where(it => !IsStatic(it))
                            .Select(it => it.gameObject)
                            .ToArray();
                        config.staticRenderers = config.toggledObjects
                            .SelectMany(it => it.GetComponentsInChildren<Renderer>(true))
                            .Where(IsStatic)
                            .ToArray();
                        config.staticColliders = config.toggledObjects
                            .SelectMany(it => it.GetComponentsInChildren<Collider>(true))
                            .Where(IsStatic)
                            .ToArray();
                    }
                }
                var instances = area.transform.Find("_vdbInstances");
                if (instances != null) DestroyImmediate(instances.gameObject);
                CreateInstances(
                    new GameObject {
                        transform = { position = area.transform.position, parent = area.transform },
                        name = "_vdbInstances"
                    },
                    new List<BakedLightConfig>(),
                    area.GetComponentsInChildren<BakedLightConfig>(false)
                        .GroupBy(it => it.GetComponentInParent<BakedLightGroup>(false))
                        .Select(it => it.ToList())
                        .ToList()
                );
            }
        }

        private static List<BakedLightInstance> CreateInstances(
            GameObject instancesParent,
            List<BakedLightConfig> selection,
            List<List<BakedLightConfig>> options
        ) {
            if (options.Count == 0) {
                var instanceObject = new GameObject {
                    transform = { position = instancesParent.transform.position, parent = instancesParent.transform }
                };
                var instance = instanceObject.AddComponent<BakedLightInstance>();

                foreach (var config in selection.ToArray()) {
                    instance.skybox = config.bakedSkybox;
                    instance.fogColor = config.fogColor;
                    instance.fogDensity = config.fogDensity;
                }
                instance.lightmaps ??= Array.Empty<Texture2D>();
                instance.configs = selection.ToArray();
                instance.name = GetInstanceName(instance);
                return new[] { instance }.ToList();
            }
            var createdInstances = new List<BakedLightInstance>();
            var group = options.First();
            var remainingOptions = new List<List<BakedLightConfig>>();
            remainingOptions.AddRange(options);
            remainingOptions.Remove(group);
            foreach (var config in group) {
                var newSelection = new List<BakedLightConfig>();
                newSelection.AddRange(selection);
                newSelection.Add(config);
                createdInstances.AddRange(CreateInstances(instancesParent, newSelection, remainingOptions));
            }
            return createdInstances;
        }

        private static Renderer[] _disabledRenderers;
        private static LightProbeGroup[] _disabledProbes;

        private static void PreBakeLightmaps(BakedLightArea[] areas) {
            _disabledRenderers = FindObjectsOfType<Renderer>(true).Where(IsStatic).ToArray();
            _disabledProbes = FindObjectsOfType<LightProbeGroup>(false).ToArray();
            foreach (var renderer in _disabledRenderers)
                renderer.gameObject.SetActive(false);
            foreach (var probe in _disabledProbes)
                probe.gameObject.SetActive(false);
            foreach (var area in areas.Where(it => it != null)) {
                foreach (var config in area.GetComponentsInChildren<BakedLightConfig>(false)) {
                    config.PipelineSetObjectVisibility(false);
                    config.PipelineSetLightVisibility(false);
                }
            }
        }

        private static void BakeLightmaps(BakedLightArea[] areas) {
            var totalInstances = areas.SelectMany(area => area.GetComponentsInChildren<BakedLightInstance>(false))
                .Count();
            var current = 1;
            foreach (var area in areas) {
                foreach (var renderer in area.GetComponentsInChildren<Renderer>(true).Where(IsStatic))
                    renderer.gameObject.SetActive(true);
                var instances = area.GetComponentsInChildren<BakedLightInstance>(false).ToList();
                for (var idx = instances.Count - 1; idx >= 0; --idx) {
                    DisplayProgressBar(
                        "[VRCDynamicBakery]",
                        $"Baking Lightmaps ({current++} of {totalInstances})",
                        0.5F
                    );
                    var instance = instances[idx];
                    foreach (var config in instance.configs.Where(it => it != null)) {
                        config.PipelineSetObjectVisibility(true);
                        config.PipelineSetLightVisibility(true);
                    }
                    Lightmapping.Bake();
                    area.lightmapSize = LightmapSettings.lightmaps.Length;
                    if (!AssetDatabase.IsValidFolder("Assets/VRCDynamicBakery"))
                        AssetDatabase.CreateFolder("Assets", "VRCDynamicBakery");
                    for (var lightmapIndex = 0; lightmapIndex < LightmapSettings.lightmaps.Length; ++lightmapIndex) {
                        var color = $"Assets/VRCDynamicBakery/{GetLightmapName(instance, lightmapIndex)}.exr";
                        AssetDatabase.DeleteAsset(color);
                        AssetDatabase.CopyAsset(
                            AssetDatabase.GetAssetPath(LightmapSettings.lightmaps[lightmapIndex].lightmapColor),
                            color
                        );
                    }
                    foreach (var config in instance.configs.Where(it => it != null)) {
                        config.PipelineSetObjectVisibility(false);
                        config.PipelineSetLightVisibility(false);
                    }
                }
                area.lightmapScale = area.renderers.Select(it => it.lightmapScaleOffset).ToArray();
                area.lightmapIndices = area.renderers.Select(it => it.lightmapIndex).ToArray();
                foreach (var renderer in area.GetComponentsInChildren<Renderer>(true).Where(IsStatic)) {
                    renderer.gameObject.SetActive(false);
                }
            }
        }

        private static void PostBakeLightmaps(BakedLightArea[] areas) {
            foreach (var probe in _disabledProbes)
                probe.gameObject.SetActive(true);
            foreach (var renderer in _disabledRenderers)
                renderer.gameObject.SetActive(true);
            foreach (var area in areas) {
                foreach (var config in area.GetComponentsInChildren<BakedLightConfig>(false))
                    config.PipelineSetObjectVisibility(true);
            }
            foreach (var area in areas) {
                var instances = area.GetComponentsInChildren<BakedLightInstance>(false);
                if (instances.Length == 0) continue;
                foreach (var config in instances[0].configs.Where(it => it != null))
                    config.PipelineSetLightVisibility(true);
            }
            var resolution = Lightmapping.lightingSettings.lightmapResolution;
            Lightmapping.lightingSettings.lightmapResolution = 0.000000001F;
            Lightmapping.Bake();
            Lightmapping.lightingSettings.lightmapResolution = resolution;
            foreach (var area in areas.Where(it => it != null))
            foreach (var config in area.GetComponentsInChildren<BakedLightConfig>(false)) {
                config.PipelineSetLightVisibility(false);
                config.SetObjectVisibility(false);
            }
        }

        private static void FetchLightmaps(BakedLightArea[] areas) {
            foreach (var area in areas) {
                foreach (var instance in area.GetComponentsInChildren<BakedLightInstance>(false)) {
                    var colors = new List<Texture2D>();
                    if (!AssetDatabase.IsValidFolder("Assets/VRCDynamicBakery"))
                        AssetDatabase.CreateFolder("Assets", "VRCDynamicBakery");
                    for (var lightmapIndex = 0; lightmapIndex < area.lightmapSize; ++lightmapIndex) {
                        colors.Add(
                            AssetDatabase.LoadAssetAtPath<Texture2D>(
                                $"Assets/VRCDynamicBakery/{GetLightmapName(instance, lightmapIndex)}.exr"
                            )
                        );
                    }
                    instance.lightmaps = colors.ToArray();
                }
            }
        }

        private static void SetDefaults(BakedLightArea[] areas) {
            foreach (var area in areas) {
                var instances = area.GetComponentsInChildren<BakedLightInstance>(false);
                if (instances.Length == 0) continue;
                area.Block ??= new MaterialPropertyBlock();
                area.Activate(instances.First().transform.GetSiblingIndex(), true);
                SceneView.RepaintAll();
            }
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

        private static string GetInstanceName(BakedLightInstance instance) {
            var obj = instance.transform.parent.parent.gameObject;
            var path = $"{obj.name}";
            while (obj.transform.parent != null) {
                obj = obj.transform.parent.gameObject;
                path = $"{obj.name};{path}";
            }

            return $"{path};{instance.transform.GetSiblingIndex()}";
        }

        private static string GetLightmapName(BakedLightInstance instance, int lightmapIndex) {
            return $"{GetInstanceName(instance)};{lightmapIndex}";
        }

        private static bool IsStatic(Component val) {
            return GameObjectUtility.AreStaticEditorFlagsSet(val.gameObject, StaticEditorFlags.ContributeGI);
        }
    }
#endif
}
