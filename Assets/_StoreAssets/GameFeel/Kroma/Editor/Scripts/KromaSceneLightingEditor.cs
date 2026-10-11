using System;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEditor;
using UnityEngine.Rendering;
using FayLabs.Kroma.Editor;
using System.Linq;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace FayLabs.Kroma
{
    /// <summary>
    /// Custom inspector for KromaSceneLighting component
    /// </summary>


    [CustomEditor(typeof(KromaSceneLighting))]
    public class KromaSceneLightingEditor : UnityEditor.Editor
    {
        private KromaSceneLighting _target;
        private KromaProfileLibrary _profileLibrary;
        private VisualElement _root;

        private void OnEnable()
        {
            _target = (KromaSceneLighting)target;
            
            // Auto-collapse the Transform component for a cleaner UI
            Transform transformComponent = _target.GetComponent<Transform>();
            if (transformComponent != null)
            {
                UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(transformComponent, false);
            }
            EditorApplication.hierarchyChanged += HandleHierarchyOrUndo;
            Undo.undoRedoPerformed += HandleHierarchyOrUndo;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= HandleHierarchyOrUndo;
            Undo.undoRedoPerformed -= HandleHierarchyOrUndo;

            if (_target != null)
            {
                Transform transformComponent = _target.GetComponent<Transform>();
                if (transformComponent != null)
                {
                    UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(transformComponent, true);
                }
            }
        }

        private void HandleHierarchyOrUndo()
        {
            if (_target == null || _root == null) return;

            var listContainer = _root.Q<VisualElement>("atmosphere-list-container");
            if (listContainer == null) return;

            if (_target.appliedSceneObjects == null || _target.appliedSceneObjects.Length == 0)
            {
                if (listContainer.childCount > 0)
                {
                    SetupAtmospherePrefabs();
                }
                return;
            }

            bool needsRefresh = false;
            int validCount = 0;
            for (int i = 0; i < _target.appliedSceneObjects.Length; i++)
            {
                var obj = _target.appliedSceneObjects[i];
                if (obj == null)
                {
                    needsRefresh = true;
                    break;
                }
                validCount++;
            }

            if (!needsRefresh && validCount != listContainer.childCount)
            {
                needsRefresh = true;
            }

            if (!needsRefresh)
            {
                var rows = listContainer.Children().ToList();
                for (int i = 0; i < rows.Count && i < _target.appliedSceneObjects.Length; i++)
                {
                    var objField = rows[i].Q<ObjectField>("atmosphere-object-field");
                    if (objField == null || (GameObject)objField.value != _target.appliedSceneObjects[i])
                    {
                        needsRefresh = true;
                        break;
                    }
                }
            }

            if (needsRefresh)
            {
                SetupAtmospherePrefabs();
            }
        }

        public override VisualElement CreateInspectorGUI()
        {
            _root = new VisualElement();

            // Prevent horizontal scrollbar from appearing due to full-bleed banner negative margins
            _root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                var scrollView = _root.GetFirstAncestorOfType<ScrollView>();
                if (scrollView != null)
                {
                    scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                }
            });
            BuildUI();

            var profileProp = serializedObject.FindProperty(nameof(KromaSceneLighting.kromaProfile));
            if (profileProp != null)
            {
                _root.TrackPropertyValue(profileProp, prop =>
                {
                    _root.Clear();
                    BuildUI();
                });
            }

            return _root;
        }

        private void BuildUI()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/FayLabs/Kroma/Editor/UI/Views/KromaSceneLighting.uxml");
            if (visualTree == null)
            {
                _root.Add(new Label("Failed to load KromaSceneLighting.uxml"));
                return;
            }

            visualTree.CloneTree(_root);

            SetupTopCard();
            SetupProfileTuning();
            SetupAtmospherePrefabs();
            SetupProfileDetails();
            SetupCloneAndReset();
        }

        private static void SetElementVisible(VisualElement element, bool visible)
        {
            if (element == null) return;
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
            {
                element.RemoveFromClassList("kroma-hidden");
            }
            else
            {
                element.AddToClassList("kroma-hidden");
            }
        }

        private void SetupTopCard()
        {
            var topCardImg = _root.Q<Image>("top-card-image");
            if (topCardImg != null)
            {
                Texture2D topTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FayLabs/Kroma/Editor/Resources/UI/header-Kroma-profile.png");
                if (topTex != null)
                {
                    topCardImg.image = topTex;
                }
            }
        }

        private void SetupProfileTuning()
        {
            var tuningSection = _root.Q<VisualElement>("section-profile-tuning");
            var boostRow = _root.Q<VisualElement>("row-lighting-boost");
            var weightRow = _root.Q<VisualElement>("row-volume-weight");
            var fogRow = _root.Q<VisualElement>("row-fog-intensity");
            var boostSlider = _root.Q<Slider>("slider-lighting-boost");
            var weightSlider = _root.Q<Slider>("slider-volume-weight");
            var fogSlider = _root.Q<Slider>("slider-fog-intensity");
            var boostResetBtn = _root.Q<Button>("btn-reset-lighting-boost");
            var weightResetBtn = _root.Q<Button>("btn-reset-volume-weight");
            var fogResetBtn = _root.Q<Button>("btn-reset-fog-intensity");

            if (_target.kromaProfile == null)
            {
                SetElementVisible(tuningSection, false);
                return;
            }

            KromaProfile profile = (KromaProfile)_target.kromaProfile;
            bool showBoost = true;
            bool showWeight = profile.volumeProfile != null;
            bool showFog = profile.fogEnabled;

            if (!showBoost && !showWeight && !showFog)
            {
                SetElementVisible(tuningSection, false);
                return;
            }

            SetElementVisible(tuningSection, true);
            serializedObject.Update();

            if (showBoost)
            {
                SetElementVisible(boostRow, true);
                boostSlider.showInputField = true;
                
                if (profile.environmentSource == AmbientMode.Skybox)
                {
                    boostSlider.lowValue = 0f;
                    boostSlider.highValue = 5f;
                }
                else
                {
                    boostSlider.lowValue = -2f;
                    boostSlider.highValue = 2f;
                }

                float defaultBoost = (profile.environmentSource == AmbientMode.Skybox) ? profile.environmentMultiplier : 0f;
                void UpdateBoostResetState(float val)
                {
                    if (boostResetBtn != null)
                    {
                        boostResetBtn.SetEnabled(!Mathf.Approximately(val, defaultBoost));
                    }
                }

                var boostProp = serializedObject.FindProperty(nameof(KromaSceneLighting.environmentLightingBoost));
                if (boostProp != null)
                {
                    boostSlider.BindProperty(boostProp);
                    UpdateBoostResetState(boostProp.floatValue);
                    boostSlider.TrackPropertyValue(boostProp, prop =>
                    {
                        ApplyLightingBoost(prop.floatValue, profile);
                        UpdateBoostResetState(prop.floatValue);
                    });
                }

                SetupSliderFill(boostSlider);

                if (boostResetBtn != null)
                {
                    SetupResetButtonIcon(boostResetBtn);
                    boostResetBtn.clicked += () =>
                    {
                        Undo.RecordObject(_target, "Reset Lighting Boost");
                        serializedObject.Update();
                        var prop = serializedObject.FindProperty(nameof(KromaSceneLighting.environmentLightingBoost));
                        if (prop != null)
                        {
                            prop.floatValue = defaultBoost;
                            serializedObject.ApplyModifiedProperties();
                        }
                        ApplyLightingBoost(defaultBoost, profile);
                        UpdateBoostResetState(defaultBoost);
                    };
                }
            }
            else
            {
                SetElementVisible(boostRow, false);
            }

            if (showWeight)
            {
                SetElementVisible(weightRow, true);
                weightSlider.showInputField = true;
                weightSlider.lowValue = 0.001f;
                weightSlider.highValue = 1f;

                void UpdateWeightResetState(float val)
                {
                    if (weightResetBtn != null)
                    {
                        weightResetBtn.SetEnabled(!Mathf.Approximately(val, 1f));
                    }
                }

                var weightProp = serializedObject.FindProperty(nameof(KromaSceneLighting.volumeWeight));
                if (weightProp != null)
                {
                    weightSlider.BindProperty(weightProp);
                    UpdateWeightResetState(weightProp.floatValue);
                    weightSlider.TrackPropertyValue(weightProp, prop =>
                    {
                        ApplyVolumeWeight(prop.floatValue, profile);
                        UpdateWeightResetState(prop.floatValue);
                    });
                }

                SetupSliderFill(weightSlider);

                if (weightResetBtn != null)
                {
                    SetupResetButtonIcon(weightResetBtn);
                    weightResetBtn.clicked += () =>
                    {
                        Undo.RecordObject(_target, "Reset Volume Weight");
                        serializedObject.Update();
                        var prop = serializedObject.FindProperty(nameof(KromaSceneLighting.volumeWeight));
                        if (prop != null)
                        {
                            prop.floatValue = 1f;
                            serializedObject.ApplyModifiedProperties();
                        }
                        ApplyVolumeWeight(1f, profile);
                        UpdateWeightResetState(1f);
                    };
                }
            }
            else
            {
                SetElementVisible(weightRow, false);
            }

            if (showFog)
            {
                SetElementVisible(fogRow, true);
                fogSlider.showInputField = true;
                fogSlider.lowValue = 0f;
                fogSlider.highValue = 10f;

                void UpdateFogResetState(float val)
                {
                    if (fogResetBtn != null)
                    {
                        fogResetBtn.SetEnabled(!Mathf.Approximately(val, 1f));
                    }
                }

                var fogProp = serializedObject.FindProperty(nameof(KromaSceneLighting.fogIntensity));
                if (fogProp != null)
                {
                    fogSlider.BindProperty(fogProp);
                    UpdateFogResetState(fogProp.floatValue);
                    fogSlider.TrackPropertyValue(fogProp, prop =>
                    {
                        ApplyFogIntensity(prop.floatValue, profile);
                        UpdateFogResetState(prop.floatValue);
                    });
                }

                SetupSliderFill(fogSlider);

                if (fogResetBtn != null)
                {
                    SetupResetButtonIcon(fogResetBtn);
                    fogResetBtn.clicked += () =>
                    {
                        Undo.RecordObject(_target, "Reset Fog Control");
                        serializedObject.Update();
                        var prop = serializedObject.FindProperty(nameof(KromaSceneLighting.fogIntensity));
                        if (prop != null)
                        {
                            prop.floatValue = 1f;
                            serializedObject.ApplyModifiedProperties();
                        }
                        ApplyFogIntensity(1f, profile);
                        UpdateFogResetState(1f);
                    };
                }
            }
            else
            {
                SetElementVisible(fogRow, false);
            }
        }

        private static void SetupResetButtonIcon(Button btn)
        {
            if (btn == null) return;
            var img = btn.Q<Image>();
            if (img != null)
            {
                Texture2D customIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FayLabs/Kroma/Editor/UI/icons/kroma-Ico-Reset.png");
                if (customIcon != null)
                {
                    img.image = customIcon;
                }
                else if (img.image == null)
                {
                    var iconContent = EditorGUIUtility.IconContent("d_Refresh");
                    if (iconContent != null && iconContent.image != null)
                    {
                        img.image = iconContent.image;
                    }
                }
            }
        }

        private static void SetupSliderFill(Slider slider)
        {
            if (slider == null) return;

            var tracker = slider.Q<VisualElement>("unity-tracker") ?? slider.Q<VisualElement>(className: "unity-base-slider__tracker");
            var dragContainer = slider.Q<VisualElement>("unity-drag-container") ?? slider.Q<VisualElement>(className: "unity-base-slider__drag-container");
            if (tracker == null || dragContainer == null) return;

            var fillOverlay = new VisualElement { name = "kroma-slider-fill-overlay" };
            fillOverlay.pickingMode = PickingMode.Ignore;
            fillOverlay.style.position = Position.Absolute;
            fillOverlay.style.left = 0;
            fillOverlay.style.top = 0;
            fillOverlay.style.right = 0;
            fillOverlay.style.bottom = 0;
            
            // Insert overlay right after the tracker so it renders over the tracker but under the dragger
            int trackerIndex = dragContainer.IndexOf(tracker);
            if (trackerIndex >= 0) {
                dragContainer.Insert(trackerIndex + 1, fillOverlay);
            } else {
                dragContainer.Add(fillOverlay);
            }

            var fillColor = new Color32(255, 168, 58, 255); // #ffa83a - Brand Orange from theme
            
            fillOverlay.generateVisualContent += mgc =>
            {
                if (float.IsNaN(tracker.layout.width) || tracker.layout.width <= 0.1f) return;
                
                float range = slider.highValue - slider.lowValue;
                if (range <= 0.0001f) return;

                // Find where 0 is on the slider, clamped to the slider bounds
                float originValue = Mathf.Clamp(0f, slider.lowValue, slider.highValue);
                float originPct = (originValue - slider.lowValue) / range;
                float currentPct = Mathf.Clamp01((slider.value - slider.lowValue) / range);
                
                float startPct = Mathf.Min(originPct, currentPct);
                float endPct = Mathf.Max(originPct, currentPct);
                
                if (endPct - startPct <= 0.0001f) return;

                float fillWidth = tracker.layout.width * (endPct - startPct);
                float fillHeight = tracker.layout.height;
                if (fillWidth <= 0.001f || fillHeight <= 0.001f || float.IsNaN(fillWidth) || float.IsNaN(fillHeight)) return;

                var painter = mgc.painter2D;
                painter.fillColor = fillColor;
                painter.BeginPath();
                
                // Draw rounded rect manually or just a rect
                float x = tracker.layout.x + (tracker.layout.width * startPct);
                float y = tracker.layout.y;
                float r = 2f; // border-radius
                
                // Rounded rect path
                painter.MoveTo(new Vector2(x + r, y));
                painter.LineTo(new Vector2(x + fillWidth - r, y));
                painter.ArcTo(new Vector2(x + fillWidth, y), new Vector2(x + fillWidth, y + r), r);
                painter.LineTo(new Vector2(x + fillWidth, y + fillHeight - r));
                painter.ArcTo(new Vector2(x + fillWidth, y + fillHeight), new Vector2(x + fillWidth - r, y + fillHeight), r);
                painter.LineTo(new Vector2(x + r, y + fillHeight));
                painter.ArcTo(new Vector2(x, y + fillHeight), new Vector2(x, y + fillHeight - r), r);
                painter.LineTo(new Vector2(x, y + r));
                painter.ArcTo(new Vector2(x, y), new Vector2(x + r, y), r);
                
                painter.ClosePath();
                painter.Fill();
            };

            slider.RegisterValueChangedCallback(_ => fillOverlay.MarkDirtyRepaint());
            tracker.RegisterCallback<GeometryChangedEvent>(_ => fillOverlay.MarkDirtyRepaint());
            slider.schedule.Execute(() => fillOverlay.MarkDirtyRepaint()).StartingIn(100);
        }

        private void ApplyFogIntensity(float intensity, KromaProfile profile)
        {
            if (profile == null) return;

            // Never toggle RenderSettings.fog to false to prevent global shader keyword swapping and recompilation hitching
            RenderSettings.fog = true;

            if (profile.fogMode == FogMode.Linear)
            {
                if (intensity <= 0.001f)
                {
                    // Push fog distances beyond visible range when set to 0
                    RenderSettings.fogStartDistance = 100000f;
                    RenderSettings.fogEndDistance = 100001f;
                }
                else
                {
                    // Linear fog needs a stronger curve to feel impactful at higher slider values
                    float linearMultiplier = Mathf.Pow(intensity, 2f);
                    RenderSettings.fogStartDistance = profile.fogStartDistance / Mathf.Max(linearMultiplier, 0.05f);
                    RenderSettings.fogEndDistance = profile.fogEndDistance / Mathf.Max(linearMultiplier, 0.05f);
                }
            }
            else
            {
                // Exponential fog is already highly sensitive; use raw intensity to prevent blowout.
                // At 0, density becomes 0 (completely clear visually) without turning off the shader keyword.
                RenderSettings.fogDensity = profile.fogDensity * Mathf.Max(0f, intensity);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        private void ApplyLightingBoost(float boost, KromaProfile profile)
        {
            if (profile == null) return;

            if (profile.environmentSource == AmbientMode.Skybox)
            {
                RenderSettings.ambientIntensity = boost;
            }
            else if (profile.environmentSource == AmbientMode.Flat)
            {
                float multiplier = boost < 0f ? (1f + (boost / 2f)) : (1f + boost);
                RenderSettings.ambientLight = profile.ambientColor * multiplier;
            }
            else // AmbientMode.Trilight
            {
                float multiplier = boost < 0f ? (1f + (boost / 2f)) : (1f + boost);
                RenderSettings.ambientSkyColor = profile.ambientSkyColor * multiplier;
                RenderSettings.ambientEquatorColor = profile.ambientEquatorColor * multiplier;
                RenderSettings.ambientGroundColor = profile.ambientGroundColor;
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        private void ApplyVolumeWeight(float weight, KromaProfile profile)
        {
            if (profile == null) return;

            Volume targetVol = null;
            Volume[] volumes = KromaLightingUtils.FindObjects<Volume>();
            foreach (var v in volumes)
            {
                if (v.sharedProfile == profile.volumeProfile)
                {
                    targetVol = v;
                    break;
                }
            }
            if (targetVol == null)
            {
                targetVol = Object.FindAnyObjectByType<Volume>();
            }

            if (targetVol != null)
            {
                targetVol.weight = weight;
                EditorUtility.SetDirty(targetVol);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        private void SetupAtmospherePrefabs()
        {
            var section = _root.Q<VisualElement>("section-atmosphere-prefabs");
            var listContainer = _root.Q<VisualElement>("atmosphere-list-container");
            var emptyLabel = _root.Q<Label>("atmosphere-empty-label");

            if (listContainer != null)
            {
                listContainer.Clear();
            }

            if (_target.appliedSceneObjects == null || _target.appliedSceneObjects.Length == 0)
            {
                SetElementVisible(section, false);
                return;
            }

            VisualTreeAsset rowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/FayLabs/Kroma/Editor/UI/Views/KromaAtmosphereRow.uxml");
            Texture2D playTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FayLabs/Kroma/Editor/UI/icons/kroma-Ico-Play.png");
            Texture2D pauseTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FayLabs/Kroma/Editor/UI/icons/kroma-Ico-Pause.png");
            Texture2D followOnTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FayLabs/Kroma/Editor/UI/icons/kroma-Ico-follow-Camera.png");
            Texture2D followOffTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FayLabs/Kroma/Editor/UI/icons/kroma-Ico-Unfollow-Camera.png");
            Texture2D showTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FayLabs/Kroma/Editor/UI/icons/kroma-Ico-Show.png");
            Texture2D hideTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FayLabs/Kroma/Editor/UI/icons/kroma-Ico-Hide.png");
            Texture2D deleteTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FayLabs/Kroma/Editor/UI/icons/kroma-Ico-Delete.png");

            bool arrayNeedsUpdate = false;
            System.Collections.Generic.List<GameObject> validObjects = new System.Collections.Generic.List<GameObject>();
            int validCount = 0;

            for (int i = 0; i < _target.appliedSceneObjects.Length; i++)
            {
                GameObject obj = _target.appliedSceneObjects[i];
                if (obj == null)
                {
                    arrayNeedsUpdate = true;
                    continue;
                }
                
                validObjects.Add(obj);
                validCount++;

                if (rowTemplate != null)
                {
                    VisualElement row = rowTemplate.Instantiate();
                    listContainer.Add(row);

                    var objField = row.Q<ObjectField>("atmosphere-object-field");
                    objField.objectType = typeof(GameObject);
                    objField.value = obj;

                    // 0. Particle Play/Pause
                    var btnParticle = row.Q<Button>("btn-particle-toggle");
                    var iconParticle = row.Q<Image>("icon-particle");
                    var particleController = obj.GetComponentInChildren<KromaParticleController>(true);
                    bool hasParticles = particleController != null || obj.GetComponentInChildren<ParticleSystem>(true) != null;
                    
                    if (!hasParticles)
                    {
                        btnParticle.SetEnabled(false);
                        btnParticle.tooltip = "Only available for particle systems";
                        iconParticle.style.backgroundImage = StyleKeyword.Null;
                        if (playTex != null)
                        {
                            iconParticle.image = playTex;
                        }
                        else
                        {
                            iconParticle.image = EditorGUIUtility.IconContent("d_PlayButton").image;
                        }
                    }
                    else
                    {
                        void UpdateParticleState(bool isPlaying)
                        {
                            Texture2D pTex = isPlaying ? pauseTex : playTex;
                            iconParticle.style.backgroundImage = StyleKeyword.Null;
                            if (pTex != null)
                            {
                                iconParticle.image = pTex;
                            }
                            else
                            {
                                string fallback = isPlaying ? "d_PauseButton" : "d_PlayButton";
                                iconParticle.image = EditorGUIUtility.IconContent(fallback).image;
                            }
                            btnParticle.tooltip = isPlaying ? "Pause particle simulation" : "Play particle simulation";
                        }

                        bool isPlaying = particleController != null && particleController.enabled;
                        UpdateParticleState(isPlaying);
                        
                        btnParticle.clicked += () =>
                        {
                            if (particleController != null)
                            {
                                Undo.RecordObject(particleController, $"{(particleController.enabled ? "Pause" : "Play")} Particles on {obj.name}");
                                particleController.enabled = !particleController.enabled;
                                EditorUtility.SetDirty(particleController);
                                
                                UpdateParticleState(particleController.enabled);
                            }
                        };
                    }

                    // 1. Camera Follow
                    var btnFollow = row.Q<Button>("btn-follow-toggle");
                    var iconFollow = row.Q<Image>("icon-follow");
                    var followScript = obj.GetComponentInChildren<KromaCameraFollow>(true);
                    
                    if (!hasParticles && followScript == null)
                    {
                        btnFollow.style.display = DisplayStyle.None;
                    }
                    else
                    {
                        void UpdateFollowState(bool isFollowing)
                        {
                            Texture2D fTex = isFollowing ? followOnTex : followOffTex;
                            iconFollow.style.backgroundImage = StyleKeyword.Null;
                            if (fTex != null)
                            {
                                iconFollow.image = fTex;
                            }
                            else
                            {
                                iconFollow.image = EditorGUIUtility.IconContent("d_Camera Gizmo").image;
                            }
                            btnFollow.tooltip = isFollowing ? "Stop following camera" : "Follow camera";
                        }

                        UpdateFollowState(followScript != null && followScript.enabled);

                        btnFollow.clicked += () =>
                        {
                            var script = obj.GetComponentInChildren<KromaCameraFollow>(true);
                            if (script == null)
                            {
                                Undo.AddComponent<KromaCameraFollow>(obj);
                                EditorUtility.SetDirty(obj);
                                script = obj.GetComponentInChildren<KromaCameraFollow>(true);
                            }
                            else
                            {
                                Undo.RecordObject(script, $"{(script.enabled ? "Disable" : "Enable")} Follow on {obj.name}");
                                script.enabled = !script.enabled;
                                EditorUtility.SetDirty(script);
                            }
                            
                            UpdateFollowState(script != null && script.enabled);
                        };
                    }

                    // 2. Eye Toggle
                    var btnVisibility = row.Q<Button>("btn-visibility-toggle");
                    var iconVisibility = row.Q<Image>("icon-visibility");
                    
                    void UpdateEyeState(bool active)
                    {
                        Texture2D eyeTex = active ? showTex : hideTex;
                        iconVisibility.style.backgroundImage = StyleKeyword.Null;
                        if (eyeTex != null)
                        {
                            iconVisibility.image = eyeTex;
                        }
                        else
                        {
                            string fallback = active ? "d_scenevis_visible_hover" : "d_scenevis_hidden_hover";
                            iconVisibility.image = EditorGUIUtility.IconContent(fallback).image;
                        }
                        btnVisibility.tooltip = active ? "Hide from scene" : "Show in scene";
                    }

                    UpdateEyeState(obj.activeSelf);

                    btnVisibility.clicked += () =>
                    {
                        bool newActive = !obj.activeSelf;
                        Undo.RecordObject(obj, $"{(obj.activeSelf ? "Hide" : "Show")} {obj.name}");
                        obj.SetActive(newActive);
                        EditorUtility.SetDirty(obj);
                        
                        UpdateEyeState(newActive);
                    };

                    // 3. Delete
                    var btnDelete = row.Q<Button>("btn-delete");
                    var iconDelete = row.Q<Image>("icon-delete");
                    iconDelete.style.backgroundImage = StyleKeyword.Null;
                    if (deleteTex != null)
                    {
                        iconDelete.image = deleteTex;
                    }
                    else
                    {
                        iconDelete.image = EditorGUIUtility.IconContent("d_clear").image;
                    }
                    
                    btnDelete.clicked += () =>
                    {
                        if (obj != null)
                        {
                            bool confirmed = EditorUtility.DisplayDialog(
                                "Delete Atmosphere Prefab",
                                $"Are you sure you want to permanently delete '{obj.name}' from the scene?",
                                "Delete",
                                "Cancel");

                            if (!confirmed)
                            {
                                return;
                            }
                        }

                        Undo.DestroyObjectImmediate(obj);
                        
                        Undo.RecordObject(_target, "Delete Atmosphere Prefab");
                        System.Collections.Generic.List<GameObject> newValid = new System.Collections.Generic.List<GameObject>(_target.appliedSceneObjects);
                        newValid.Remove(obj);
                        newValid.RemoveAll(x => x == null);
                        _target.appliedSceneObjects = newValid.ToArray();
                        EditorUtility.SetDirty(_target);
                        
                        SetupAtmospherePrefabs();
                    };
                }
            }

            if (validCount == 0)
            {
                SetElementVisible(section, false);
            }
            else
            {
                SetElementVisible(section, true);
                SetElementVisible(emptyLabel, false);
            }

            if (arrayNeedsUpdate)
            {
                Undo.RecordObject(_target, "Update Atmosphere Prefabs List");
                _target.appliedSceneObjects = validObjects.ToArray();
                EditorUtility.SetDirty(_target);
            }
        }

        private void SetupProfileDetails()
        {
            var section = _root.Q<VisualElement>("section-profile-details");
            var activeContainer = _root.Q<VisualElement>("profile-active-container");
            var noProfileBox = _root.Q<HelpBox>("profile-no-profile-box");

            if (_target.kromaProfile != null)
            {
                SetElementVisible(activeContainer, true);
                SetElementVisible(noProfileBox, false);

                KromaProfile profile = (KromaProfile)_target.kromaProfile;

                var nameLabel = _root.Q<Label>("profile-details-name");
                if (nameLabel != null)
                {
                    string pName = !string.IsNullOrEmpty(profile.profileName) ? profile.profileName : KromaLightingUtils.GetCleanProfileName(profile.name);
                    nameLabel.text = pName.ToUpper();
                }

                var thumbImg = _root.Q<Image>("profile-details-thumb");
                if (thumbImg != null)
                {
                    Texture2D thumbTex = profile.thumbnail;
                    if (thumbTex == null)
                    {
                        string[] defThumbGuids = AssetDatabase.FindAssets("Kroma_Default_Thumbnail t:Texture2D");
                        if (defThumbGuids.Length > 0)
                        {
                            thumbTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(defThumbGuids[0]));
                        }
                    }

                    if (thumbTex != null)
                    {
                        thumbImg.image = thumbTex;
                    }
                }

                var profileField = _root.Q<ObjectField>("field-kroma-profile");
                profileField.objectType = typeof(KromaProfile);
                profileField.bindingPath = "kromaProfile";
                profileField.Bind(serializedObject);

                var skyboxField = _root.Q<ObjectField>("field-skybox");
                skyboxField.objectType = typeof(Material);
                skyboxField.value = profile.skyboxMaterial;
                skyboxField.SetEnabled(false);

                var ppVolumeField = _root.Q<ObjectField>("field-pp-volume");
                ppVolumeField.objectType = typeof(VolumeProfile);
                ppVolumeField.value = profile.volumeProfile;
                ppVolumeField.SetEnabled(false);
            }
            else
            {
                SetElementVisible(activeContainer, false);
                SetElementVisible(noProfileBox, true);
            }
        }

        private void SetupCloneAndReset()
        {
            var toolsSection = _root.Q<VisualElement>("section-profile-tools");

            if (_target.kromaProfile == null)
            {
                SetElementVisible(toolsSection, false);
                return;
            }

            SetElementVisible(toolsSection, true);

            var btnClone = _root.Q<Button>("btn-clone-profile");
            if (btnClone != null)
            {
                btnClone.clicked += () =>
                {
                    KromaCloneWindow.ShowPrompt((KromaProfile)_target.kromaProfile);
                };
            }

            var btnReset = _root.Q<Button>("btn-reset-profile");
            if (btnReset != null)
            {
                btnReset.clicked += ShowResetModal;
            }

            var btnCaptureSun = _root.Q<Button>("btn-capture-direct-light");
            if (btnCaptureSun != null)
            {
                btnCaptureSun.clicked += () =>
                {
                    if (_target.kromaProfile != null)
                    {
                        KromaUpdateProfileModal.PromptConfirmation((KromaProfile)_target.kromaProfile, () =>
                        {
                            KromaLightingUtils.CaptureSunPosition((KromaProfile)_target.kromaProfile);
                        });
                    }
                };
            }

            var btnCaptureFog = _root.Q<Button>("btn-capture-fog-ambient");
            if (btnCaptureFog != null)
            {
                btnCaptureFog.clicked += () =>
                {
                    if (_target.kromaProfile != null)
                    {
                        KromaUpdateProfileModal.PromptConfirmation((KromaProfile)_target.kromaProfile, () =>
                        {
                            KromaLightingUtils.CaptureFogDetails((KromaProfile)_target.kromaProfile);
                        });
                    }
                };
            }
        }

        private void ShowResetModal()
        {
            if (_target.kromaProfile == null)
            {
                return;
            }

            ResetSettingsWindow.ShowWindow(_target, (KromaProfile)_target.kromaProfile);
        }

        private void LoadProfileLibrary()
        {
            string[] guids = AssetDatabase.FindAssets("t:KromaProfileLibrary");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _profileLibrary = AssetDatabase.LoadAssetAtPath<KromaProfileLibrary>(path);
            }
        }
    }

    /// <summary>
    /// Modal window for selective reset
    /// </summary>
    public class ResetSettingsWindow : EditorWindow
    {
        private const string UXML_PATH = "Assets/FayLabs/Kroma/Editor/UI/Views/KromaResetProfileModal.uxml";
        private const string THEME_USS_PATH = "Assets/FayLabs/Kroma/Editor/UI/Theme/KromaTheme.uss";
        private const string COMMON_USS_PATH = "Assets/FayLabs/Kroma/Editor/UI/Components/KromaCommon.uss";
        private const string MODAL_USS_PATH = "Assets/FayLabs/Kroma/Editor/UI/Components/KromaModal.uss";

        private KromaSceneLighting _targetComponent;
        private KromaProfile _targetProfile;
        
        private bool _resetPP = true;
        private bool _resetSkybox = true;
        private bool _resetSun = true;
        private bool _resetFog = true;

        private bool _isPPApplied = false;
        private bool _isSkyboxApplied = false;

        private Button _btnConfirm;

        public static void ShowWindow(KromaSceneLighting target, KromaProfile profile)
        {
            ResetSettingsWindow window = CreateInstance<ResetSettingsWindow>();
            window.titleContent = new GUIContent("Reset Profile Settings");
            window._targetComponent = target;
            window._targetProfile = profile;
            window.CheckCurrentSceneState();

            Rect main = EditorGUIUtility.GetMainWindowPosition();
            float initialWidth = 460;
            float initialHeight = 320;
            float x = main.x + (main.width - initialWidth) * 0.5f;
            float y = main.y + (main.height - initialHeight) * 0.5f;

            window.position = new Rect(x, y, initialWidth, initialHeight);
            window.minSize = new Vector2(initialWidth, 200);
            window.maxSize = new Vector2(initialWidth, 600);
            window.ShowUtility();
        }

        private void CheckCurrentSceneState()
        {
            if (_targetProfile == null) return;

            // Check Skybox
            if (RenderSettings.skybox != null && RenderSettings.skybox == _targetProfile.skyboxMaterial)
            {
                _isSkyboxApplied = true;
                _resetSkybox = false; // Uncheck by default if already applied
            }

            // Check Post Processing
            Volume vol = Object.FindAnyObjectByType<Volume>();
            if (vol != null && vol.sharedProfile == _targetProfile.volumeProfile)
            {
                _isPPApplied = true;
                _resetPP = false; // Uncheck by default if already applied
            }
        }

        public void CreateGUI()
        {
            LoadStyleSheet(THEME_USS_PATH);
            LoadStyleSheet(COMMON_USS_PATH);
            LoadStyleSheet(MODAL_USS_PATH);

            VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UXML_PATH);
            if (uxml == null)
            {
                string[] guids = AssetDatabase.FindAssets("KromaResetProfileModal t:VisualTreeAsset");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
                }
            }

            if (uxml != null)
            {
                uxml.CloneTree(rootVisualElement);
            }

            // Body label
            var bodyLabel = rootVisualElement.Q<Label>("modal-body-label");
            if (bodyLabel != null)
            {
                bodyLabel.enableRichText = true;
                string profileName = _targetProfile != null ? _targetProfile.name : "Active Profile";
                bodyLabel.text = $"Resetting \"<color=#ffa83a>{profileName}</color>\" will restore its original lighting settings and reconnect default assets.";
            }

            _btnConfirm = rootVisualElement.Q<Button>("btn-confirm");

            // Setup Toggles
            SetupToggle("toggle-pp", "badge-pp-applied", _isPPApplied, _resetPP, val => _resetPP = val);
            SetupToggle("toggle-skybox", "badge-skybox-applied", _isSkyboxApplied, _resetSkybox, val => _resetSkybox = val);
            SetupToggle("toggle-sun", null, false, _resetSun, val => _resetSun = val);
            SetupToggle("toggle-fog", null, false, _resetFog, val => _resetFog = val);

            UpdateConfirmState();

            // Actions
            var btnCancel = rootVisualElement.Q<Button>("btn-cancel");
            if (btnCancel != null)
            {
                btnCancel.clicked += Close;
            }

            if (_btnConfirm != null)
            {
                _btnConfirm.clicked += () =>
                {
                    PerformReset();
                    Close();
                };
            }

            // Auto-fit window height dynamically to rendered content
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void SetupToggle(string toggleName, string badgeName, bool isAlreadyApplied, bool initialValue, Action<bool> onValueChanged)
        {
            var toggle = rootVisualElement.Q<Toggle>(toggleName);
            if (toggle == null) return;

            var badge = !string.IsNullOrEmpty(badgeName) ? rootVisualElement.Q<Label>(badgeName) : null;

            if (isAlreadyApplied)
            {
                toggle.value = false;
                toggle.SetEnabled(false);
                if (badge != null)
                {
                    badge.style.display = DisplayStyle.Flex;
                }
            }
            else
            {
                toggle.value = initialValue;
                toggle.EnableInClassList("kroma-toggle--checked", initialValue);
                if (badge != null)
                {
                    badge.style.display = DisplayStyle.None;
                }

                toggle.RegisterValueChangedCallback(evt =>
                {
                    onValueChanged?.Invoke(evt.newValue);
                    toggle.EnableInClassList("kroma-toggle--checked", evt.newValue);
                    UpdateConfirmState();
                });
            }
        }

        private void UpdateConfirmState()
        {
            if (_btnConfirm != null)
            {
                bool canReset = _resetPP || _resetSkybox || _resetSun || _resetFog;
                _btnConfirm.SetEnabled(canReset);
            }
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            var modalRoot = rootVisualElement.Q(className: "kroma-modal-root");
            if (modalRoot != null && modalRoot.layout.height > 50)
            {
                rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                float naturalHeight = Mathf.Ceil(modalRoot.layout.height);
                minSize = new Vector2(460, naturalHeight);
                maxSize = new Vector2(460, naturalHeight);

                Rect main = EditorGUIUtility.GetMainWindowPosition();
                float x = main.x + (main.width - 460) * 0.5f;
                float y = main.y + (main.height - naturalHeight) * 0.5f;
                position = new Rect(x, y, 460, naturalHeight);
            }
        }

        private void LoadStyleSheet(string path)
        {
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (sheet != null && !rootVisualElement.styleSheets.Contains(sheet))
            {
                rootVisualElement.styleSheets.Add(sheet);
            }
        }

        private void PerformReset()
        {
            if (_targetComponent == null || _targetProfile == null)
                return;

            // Apply selected resets via KromaLightingApplier using the generic ApplyLightingSettings
            KromaLightingApplier.ApplyLightingSettings(_targetProfile, _resetSkybox, _resetSun, _resetFog);
            
            // Reset post-processing if checked
            if (_resetPP)
            {
                Volume vol = Object.FindAnyObjectByType<Volume>();
                if (vol != null && _targetComponent.kromaProfile != null)
                {
                    Undo.RecordObject(vol, "Reset Post Processing");
                    vol.sharedProfile = ((KromaProfile)_targetComponent.kromaProfile).volumeProfile;
                    EditorUtility.SetDirty(vol);
                }
            }
            
            // Debug.Log($"[Kroma] Reset settings for profile '{_targetComponent.kromaProfile.name}'");
        }
    }
}
