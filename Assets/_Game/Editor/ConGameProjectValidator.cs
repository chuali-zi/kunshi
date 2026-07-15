using System;
using System.Collections.Generic;
using ConGame.Domain.Interaction;
using ConGame.Domain.Counterplay;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConGame.Editor
{

    public static class ConGameProjectValidator
    {
        private static readonly string[] ExpectedScenes = { "Assets/_Game/Scenes/Bootstrap.unity",
                                                            "Assets/_Game/Scenes/MainMenu.unity",
                                                            "Assets/_Game/Scenes/Room_M1.unity" };

        [MenuItem("ConGame/Validate Project", priority = 100)]
        public static void ValidateFromMenu()
        {
            int errors = ValidateProject(true);
            EditorUtility.DisplayDialog(
                "ConGame Validation",
                errors == 0 ? "Validation passed." : $"Validation found {errors} error(s). See Console.", "OK");
        }

        public static int ValidateProject(bool log)
        {
            int errors = 0;
            errors += ValidateItems(log);
            errors += ValidateCounterplays(log);
            errors += ValidateBuildScenes(log);
            errors += ValidateProjectSettings(log);

            if (log && errors == 0)
            {
                Debug.Log("[ConGame] Project validation passed.");
            }

            return errors;
        }

        private static int ValidateItems(bool log)
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemDefinitionAsset", new[] { "Assets/_Game/Content/Items" });
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            int errors = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                ItemDefinitionAsset item =
                    AssetDatabase.LoadAssetAtPath<ItemDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (item == null || string.IsNullOrWhiteSpace(item.ItemId) || !ids.Add(item.ItemId))
                {
                    errors++;
                    if (log)
                    {
                        Debug.LogError($"Invalid or duplicate item ID at GUID {guids[i]}.", item);
                    }
                }
            }

            if (guids.Length != ItemSeedLibrary.All.Count)
            {
                errors++;
                if (log)
                {
                    Debug.LogError($"Expected {ItemSeedLibrary.All.Count} item definitions, found {guids.Length}.");
                }
            }

            return errors;
        }

        private static int ValidateCounterplays(bool log)
        {
            string[] guids =
                AssetDatabase.FindAssets("t:CounterplayDefinitionAsset", new[] { "Assets/_Game/Content/Counterplays" });
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<int> orders = new HashSet<int>();
            bool foundFallback = false;
            int errors = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                CounterplayDefinitionAsset definition =
                    AssetDatabase.LoadAssetAtPath<CounterplayDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (definition == null)
                {
                    errors++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.OutcomeId) || !ids.Add(definition.OutcomeId))
                {
                    errors++;
                    if (log)
                    {
                        Debug.LogError("Counterplay has an empty or duplicate outcome ID.", definition);
                    }
                }

                if (!orders.Add(definition.SortOrder))
                {
                    errors++;
                    if (log)
                    {
                        Debug.LogError($"Counterplay sort order {definition.SortOrder} is duplicated.", definition);
                    }
                }
                if (definition.ContentStatus == ContentStatus.Implemented && definition.Performance == null)
                {
                    errors++;
                    if (log)
                    {
                        Debug.LogError(
                            $"Implemented counterplay {definition.OutcomeId} must reference an M1 performance asset.",
                            definition);
                    }
                }

                if (definition.OutcomeId == "D01" && (definition.TriggerMode & CounterplayTriggerMode.Fallback) != 0 &&
                    (definition.Conditions == null || definition.Conditions.Length == 0))
                {
                    foundFallback = true;
                }
            }

            if (!foundFallback)
            {
                errors++;
                if (log)
                {
                    Debug.LogError("Counterplay catalog must contain an unconditional D01 fallback.");
                }
            }

            if (guids.Length != CounterplaySeedLibrary.All.Count)
            {
                errors++;
                if (log)
                {
                    Debug.LogError(
                        $"Expected {CounterplaySeedLibrary.All.Count} counterplay definitions, found {guids.Length}.");
                }
            }

            return errors;
        }

        private static int ValidateBuildScenes(bool log)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Length < ExpectedScenes.Length)
            {
                if (log)
                {
                    Debug.LogError("Build Settings does not contain all ConGame scenes.");
                }

                return 1;
            }

            for (int i = 0; i < ExpectedScenes.Length; i++)
            {
                if (!scenes[i].enabled || !string.Equals(scenes[i].path, ExpectedScenes[i], StringComparison.Ordinal))
                {
                    if (log)
                    {
                        Debug.LogError($"Build scene {i} must be enabled and equal to {ExpectedScenes[i]}.");
                    }

                    return 1;
                }
            }

            return 0;
        }

        private static int ValidateProjectSettings(bool log)
        {
            int errors = 0;
            if (GraphicsSettings.defaultRenderPipeline == null || QualitySettings.renderPipeline == null)
            {
                errors++;
                if (log)
                {
                    Debug.LogError("URP is not assigned in both Graphics and Quality settings.");
                }
            }

            UnityEngine.Object[] settingsAssets =
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settingsAssets == null || settingsAssets.Length == 0)
            {
                return errors;
            }

            SerializedObject settings = new SerializedObject(settingsAssets[0]);
            SerializedProperty activeInputHandler = settings.FindProperty("activeInputHandler");
            if (activeInputHandler != null && activeInputHandler.intValue == 0)
            {
                errors++;
                if (log)
                {
                    Debug.LogError("Active Input Handling must enable the new Input System backend.");
                }
            }

            return errors;
        }
    }
}
