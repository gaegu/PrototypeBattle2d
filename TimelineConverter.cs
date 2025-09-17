#if UNITY_EDITOR
using BattleCharacterSystem.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BattleCharacterSystem.Editor
{
    /// <summary>
    /// Timeline Asset을 TimelineDataSO로 변환하는 유틸리티
    /// CosmosAnimationTrack과 CosmosEffectTrack만 파싱
    /// </summary>
    public static class TimelineConverter
    {

        private static string ExtractCharacterNameFromTimeline(TimelineAsset timeline)
        {
            if (timeline == null) return "";

            string timelineName = timeline.name;

            // "캐릭터명_Timeline" 패턴에서 캐릭터명 추출
            if (timelineName.EndsWith("_Timeline"))
            {
                return timelineName.Replace("_Timeline", "");
            }

            return timelineName;
        }

        /// <summary>
        /// Timeline Asset을 파싱해서 TimelineDataSO로 변환
        /// </summary>
        public static TimelineDataSO ConvertTimelineToSO(TimelineAsset timelineAsset, string savePath = null)
        {
            if (timelineAsset == null)
            {
                Debug.LogError("[TimelineConverter] Timeline Asset is null!");
                return null;
            }

            // 기존 SO 찾기 또는 새로 생성
            TimelineDataSO timelineData = null;

            string characterName = ExtractCharacterNameFromTimeline(timelineAsset);

            // 저장 경로 자동 생성 (savePath가 없을 경우)
            if (string.IsNullOrEmpty(savePath) && !string.IsNullOrEmpty(characterName))
            {
                savePath = $"Assets/Cosmos/ResourcesAddressable/Characters/{characterName}/Animations/{timelineAsset.name}_Data.asset";
            }

            if (!string.IsNullOrEmpty(savePath))
            {
                timelineData = AssetDatabase.LoadAssetAtPath<TimelineDataSO>(savePath);
            }

            if (timelineData == null)
            {
                timelineData = ScriptableObject.CreateInstance<TimelineDataSO>();
            }

            // 기본 정보 설정
            timelineData.timelineName = timelineAsset.name;
            timelineData.duration = (float)timelineAsset.duration;
            timelineData.sourceTimelineAssetPath = AssetDatabase.GetAssetPath(timelineAsset);

            // 이벤트 초기화
            timelineData.animationEvents.Clear();
            timelineData.effectEvents.Clear();
            timelineData.soundEvents.Clear();
            timelineData.cameraEvents.Clear();
            timelineData.customEvents.Clear();

            Debug.Log($"[TimelineConverter] Converting Timeline: {timelineAsset.name}");

            // Timeline 트랙 파싱
            ParseTimelineTracks(timelineAsset, timelineData);

            // 리소스 수집
            timelineData.CollectRequiredResources();

            // AnimationClip 저장 (SO 저장 전에 실행)
            if (!string.IsNullOrEmpty(savePath))
            {
                SaveExtractedAnimationClips(timelineData, savePath, characterName );
            }

            // SO 저장
            if (!string.IsNullOrEmpty(savePath))
            {
                if (!AssetDatabase.Contains(timelineData))
                {
                    AssetDatabase.CreateAsset(timelineData, savePath);
                }

                EditorUtility.SetDirty(timelineData);
                AssetDatabase.SaveAssets();

                Debug.Log($"[TimelineConverter] Timeline converted and saved to: {savePath}");
            }

            return timelineData;
        }


        // ConvertTimelineToSO 메서드 끝부분에 추가
        private static void SaveExtractedAnimationClips(TimelineDataSO timelineData, string outputPath, string characterName)
        {
#if UNITY_EDITOR

            // Animations 폴더 경로
            string animationsFolder = $"Assets/Cosmos/ResourcesAddressable/Characters/{characterName}/Animations";
           
            // 폴더 생성
            if (!AssetDatabase.IsValidFolder(animationsFolder))
            {
                string[] folders = animationsFolder.Split('/');
                string currentPath = folders[0];
                for (int i = 1; i < folders.Length; i++)
                {
                    string nextPath = $"{currentPath}/{folders[i]}";
                    if (!AssetDatabase.IsValidFolder(nextPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, folders[i]);
                    }
                    currentPath = nextPath;
                }
            }

            // Clip 애니메이션 저장 (기존 코드)
            foreach (var animEvent in timelineData.animationEvents)
            {
                if (animEvent.extractedClip != null && !AssetDatabase.Contains(animEvent.extractedClip))
                {
                    string clipPath = $"{animationsFolder}/{animEvent.animationStateName}_clip.anim";
                    AssetDatabase.CreateAsset(animEvent.extractedClip, clipPath);
                }
            }

            // Track 애니메이션 저장 (추가)
            foreach (var trackAnim in timelineData.trackAnimations)
            {
                if (trackAnim.animationClip != null && !AssetDatabase.Contains(trackAnim.animationClip))
                {
                    // 이미 복사본이므로 바로 저장
                    string trackClipPath = $"{animationsFolder}/{trackAnim.trackName}_track.anim";
                    AssetDatabase.CreateAsset(trackAnim.animationClip, trackClipPath);
                    EditorUtility.SetDirty(trackAnim.animationClip);
                }
            }
#endif
        }

        /// <summary>
        /// Timeline의 모든 트랙 파싱 (Cosmos 커스텀 트랙만)
        /// </summary>
        private static void ParseTimelineTracks(TimelineAsset timelineAsset, TimelineDataSO timelineData)
        {
            var tracks = timelineAsset.GetOutputTracks();
            int animationTrackCount = 0;
            int effectTrackCount = 0;

            foreach (var track in tracks)
            {
                if (track.muted) continue; // Muted 트랙은 건너뛰기

                // CosmosAnimationTrack 처리
                if (track is CosmosAnimationTrack animTrack)
                {
                    ParseCosmosAnimationTrack(animTrack, timelineData, timelineAsset);
                    animationTrackCount++;
                    Debug.Log($"[TimelineConverter] Found CosmosAnimationTrack: {track.name}");
                }
                // CosmosEffectTrack 처리
                else if (track is CosmosEffectTrack effectTrack)
                {
                    ParseCosmosEffectTrack(effectTrack, timelineData, timelineAsset);
                    effectTrackCount++;
                    Debug.Log($"[TimelineConverter] Found CosmosEffectTrack: {track.name}");
                }
                // 그룹 트랙인 경우 재귀적으로 자식 트랙도 파싱
                else if (track is GroupTrack groupTrack)
                {
                    ParseGroupTrack(groupTrack, timelineData, timelineAsset);
                }
                else
                {
                    // 다른 트랙 타입은 무시
                    Debug.Log($"[TimelineConverter] Skipping track type: {track.GetType().Name} - {track.name}");
                }
            }

            Debug.Log($"[TimelineConverter] Parsed {animationTrackCount} Animation Tracks, {effectTrackCount} Effect Tracks");
        }


        private static AnimationClip ExtractAnimationClipFromTrack(CosmosAnimationTrack track, TimelineClip timelineClip)
        {
            // 1. InfiniteClip 확인
            if (track.infiniteClip != null)
            {
                return track.infiniteClip;
            }

            // 2. AnimationPlayableAsset의 clip 확인
            var playableAsset = timelineClip.asset as AnimationPlayableAsset;
            if (playableAsset != null && playableAsset.clip != null)
            {
                return playableAsset.clip;
            }

            // 3. Track의 recorded clip 확인 (Timeline Recording 기능 사용시)
            // track.infiniteClip이 null이 아닌 경우 이미 처리됨

            return null;
        }

        // Timeline의 Animated Values를 감지
        private static bool DetectAnimatedValues(CosmosAnimationTrack track)
        {
            // infiniteClip이 있으면 Animated Values 존재
            return track.infiniteClip != null;
        }

        // 바인딩 경로 추출
        private static string GetBindingPath(CosmosAnimationTrack track, PlayableDirector director)
        {
            if (director == null) return "";

            var binding = director.GetGenericBinding(track);
            if (binding != null)
            {
                var animator = binding as Animator;
                if (animator != null)
                {
                    return AnimationUtility.CalculateTransformPath(
                        animator.transform,
                        animator.transform.root
                    );
                }
            }

            return "";
        }
        private static PlayableDirector FindDirectorForTimeline(TimelineAsset timeline)
        {
#if UNITY_EDITOR
            // Editor에서 현재 열린 Timeline의 Director 찾기
            var directors = UnityEngine.Object.FindObjectsByType<PlayableDirector>(FindObjectsSortMode.InstanceID);
            foreach (var director in directors)
            {
                if (director.playableAsset == timeline)
                {
                    return director;
                }
            }
#endif
            return null;
        }

        /// <summary>
        /// CosmosAnimationTrack 파싱
        /// </summary>
        private static void ParseCosmosAnimationTrack(CosmosAnimationTrack track, TimelineDataSO timelineData, TimelineAsset timelineAsset)
        {
            // Track의 infiniteClip 처리 (Animated Values)
            if (track.infiniteClip != null)
            {
                // AnimationClip 복사
                AnimationClip copiedClip = UnityEngine.Object.Instantiate(track.infiniteClip);
                copiedClip.name = $"{track.name}_track";


#if UNITY_EDITOR
                // 빈 Path를 실제 구조에 맞게 수정
                var bindings = AnimationUtility.GetCurveBindings(copiedClip);
                foreach (var binding in bindings)
                {
                    if (string.IsNullOrEmpty(binding.path))
                    {
                        // Root 애니메이션인 경우
                        Debug.Log($"[TimelineConverter] Root animation detected for property: {binding.propertyName}");
                    }
                }
#endif


                var trackAnimation = new TimelineDataSO.TrackAnimation
                {
                    trackName = track.name,
                    animationClip = copiedClip,
                    startTime = 0f,
                    duration = (float)timelineAsset.duration,
                    targetPath = GetBindingPath(track, FindDirectorForTimeline(timelineAsset)),
                    isLooping = false,  // Track 애니메이션은 보통 Timeline 전체 길이
                                        // Addressable 키 추가
                    animationClipAddressableKey = $"Char_{track.name}_track_anim"  // 복사본의 키
                };

                timelineData.trackAnimations.Add(trackAnimation);
                Debug.Log($"[TimelineConverter] Found track animation: {track.name} with infiniteClip");
            }

            var clips = track.GetClips();

            foreach (var clip in clips)
            {
                // AnimationPlayableAsset으로 캐스팅
                var animClip = clip.asset as AnimationPlayableAsset;
                if (animClip == null)
                {
                    Debug.LogWarning($"[TimelineConverter] Clip is not AnimationPlayableAsset: {clip.displayName}");
                    continue;
                }

                var animEvent = new TimelineDataSO.AnimationEvent
                {
                    triggerTime = (float)clip.start,
                    animationStateName = clip.displayName,
                    crossFadeDuration = (float)clip.blendInDuration,
                    clipSpeed = (float)clip.timeScale,
                    loop = clip.postExtrapolationMode == TimelineClip.ClipExtrapolation.Loop ||
                           clip.preExtrapolationMode == TimelineClip.ClipExtrapolation.Loop,

                    // 추가 부분
                    extractedClip = ExtractAnimationClipFromTrack(track, clip),
                    hasAnimatedValues = DetectAnimatedValues(track),
                    targetBindingPath = GetBindingPath(track, FindDirectorForTimeline(timelineAsset))
                };

                // Animation Clip이 있으면 Addressable 키 추출
                if (animClip.clip != null)
                {
                    string path = AssetDatabase.GetAssetPath(animClip.clip);
                    animEvent.animationClipAddressableKey = ExtractAddressableKey(path);

                    // Clip 이름이 없으면 AnimationClip 이름 사용
                    if (string.IsNullOrEmpty(animEvent.animationStateName))
                    {
                        animEvent.animationStateName = animClip.clip.name;
                    }
                }

                // Play Mode 결정 (blendInDuration 기반)
                if (clip.blendInDuration > 0)
                {
                    animEvent.playMode = TimelineDataSO.AnimationPlayMode.CrossFade;
                }
                else
                {
                    animEvent.playMode = TimelineDataSO.AnimationPlayMode.Play;
                }

                // CosmosAnimationTrack의 추가 정보가 있다면 여기서 처리
                // 예: track.CachedCharacterName, track.SelectedCharacterKey 등

                timelineData.animationEvents.Add(animEvent);
                Debug.Log($"[TimelineConverter] Added animation event: {animEvent.animationStateName} at {animEvent.triggerTime}s");
            }
        }

        /// <summary>
        /// CosmosEffectTrack 파싱
        /// </summary>
        private static void ParseCosmosEffectTrack(CosmosEffectTrack track, TimelineDataSO timelineData, TimelineAsset timelineAsset)
        {

            var clips = track.GetClips();

            foreach (var clip in clips)
            {
                var effectClip = clip.asset as CosmosEffectClip;
                if (effectClip == null)
                {
                    Debug.LogWarning($"[TimelineConverter] Clip is not CosmosEffectClip: {clip.displayName}");
                    continue;
                }

                var effectEvent = new TimelineDataSO.EffectEvent
                {
                    triggerTime = (float)clip.start,
                    duration = (float)clip.duration,
                    effectAddressableKey = effectClip.effectAddressableKey,
                    positionOffset = effectClip.positionOffset,
                    rotationOffset = effectClip.rotationOffset,
                    scale = effectClip.scale,
                    attachToActor = effectClip.attachToActor,
                    attachBoneName = effectClip.attachBoneName
                };

                // Addressable 키가 없으면 Prefab에서 추출 시도
                if (string.IsNullOrEmpty(effectEvent.effectAddressableKey) && effectClip.effectPrefab != null)
                {
                    string path = AssetDatabase.GetAssetPath(effectClip.effectPrefab);
                    effectEvent.effectAddressableKey = ExtractAddressableKey(path);
                }

                // Duration 기반으로 PlayMode 결정
                if (clip.duration > 0 && clip.duration < 100) // 100초 이상이면 무한으로 간주
                {
                    effectEvent.playMode = TimelineDataSO.EffectPlayMode.Duration;
                }
                else
                {
                    effectEvent.playMode = TimelineDataSO.EffectPlayMode.OneShot;
                }

                // Track의 추가 정보 활용
                if (string.IsNullOrEmpty(effectEvent.effectAddressableKey))
                {
                    // Track의 템플릿 키 사용
                    effectEvent.effectAddressableKey = track.GetTemplateAddressableKey();
                }

                timelineData.effectEvents.Add(effectEvent);
                Debug.Log($"[TimelineConverter] Added effect event: {effectEvent.effectAddressableKey} at {effectEvent.triggerTime}s");
            }
        }

        /// <summary>
        /// Group Track 파싱 (재귀적으로 자식 트랙 처리)
        /// </summary>
        private static void ParseGroupTrack(GroupTrack groupTrack, TimelineDataSO timelineData, TimelineAsset timelineAsset)
        {
            Debug.Log($"[TimelineConverter] Parsing GroupTrack: {groupTrack.name}");

            var childTracks = groupTrack.GetChildTracks();
            foreach (var childTrack in childTracks)
            {
                if (childTrack.muted) continue;

                if (childTrack is CosmosAnimationTrack animTrack)
                {
                    ParseCosmosAnimationTrack(animTrack, timelineData, timelineAsset);
                }
                else if (childTrack is CosmosEffectTrack effectTrack)
                {
                    ParseCosmosEffectTrack(effectTrack, timelineData, timelineAsset);
                }
                else if (childTrack is GroupTrack nestedGroup)
                {
                    ParseGroupTrack(nestedGroup, timelineData, timelineAsset);
                }
                else
                {
                    Debug.Log($"[TimelineConverter] Skipping child track type: {childTrack.GetType().Name}");
                }
            }
        }

        /// <summary>
        /// Asset 경로에서 Addressable 키 추출
        /// </summary>
        private static string ExtractAddressableKey(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return "";

            // ResourcesAddressable 폴더 이후의 경로를 키로 사용
            const string addressableRoot = "Assets/Cosmos/ResourcesAddressable/";
            if (assetPath.StartsWith(addressableRoot))
            {
                string key = assetPath.Substring(addressableRoot.Length);
                // 확장자 제거
                int extensionIndex = key.LastIndexOf('.');
                if (extensionIndex > 0)
                {
                    key = key.Substring(0, extensionIndex);
                }
                return key;
            }

            // Dev 폴더의 경우 (개발 중인 리소스)
            const string devRoot = "Assets/Dev/Cosmos/";
            if (assetPath.StartsWith(devRoot))
            {
                // 파일명만 사용
                return System.IO.Path.GetFileNameWithoutExtension(assetPath);
            }

            // 기본: 파일명만 사용
            return System.IO.Path.GetFileNameWithoutExtension(assetPath);
        }

        /// <summary>
        /// 캐릭터 폴더의 모든 Timeline 일괄 변환
        /// </summary>
        [MenuItem("*COSMOS*/Timeline/Convert All Character Timelines")]
        public static void ConvertAllCharacterTimelines()
        {
            string timelinePath = "Assets/Dev/Cosmos/Timeline/Characters";
            string outputPath = "Assets/Cosmos/ResourcesAddressable/ScriptableObjects/Timelines";

            // 출력 폴더 생성
            if (!AssetDatabase.IsValidFolder(outputPath))
            {
                System.IO.Directory.CreateDirectory(outputPath);
            }

            // Timeline Asset 찾기
            string[] guids = AssetDatabase.FindAssets("t:TimelineAsset", new[] { timelinePath });
            int converted = 0;
            int skipped = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);

                if (timeline != null)
                {
                    // 캐릭터명 추출 (경로에서)
                    string[] pathParts = path.Split('/');
                    string characterName = "";
                    for (int i = 0; i < pathParts.Length - 1; i++)
                    {
                        if (pathParts[i] == "Characters" && i + 1 < pathParts.Length)
                        {
                            characterName = pathParts[i + 1];
                            break;
                        }
                    }

                    // 출력 경로 생성
                    /*string charOutputPath = $"{outputPath}/{characterName}";
                    if (!AssetDatabase.IsValidFolder(charOutputPath))
                    {
                        System.IO.Directory.CreateDirectory(charOutputPath);
                    }*/

                    //string soPath = $"{charOutputPath}/{timeline.name}_Data.asset";
                    // 출력 경로 생성 (수정된 경로)
                    string soPath = $"Assets/Cosmos/ResourcesAddressable/Characters/{characterName}/Animations/{timeline.name}_Data.asset";

                    try
                    {
                        // 변환
                        var result = ConvertTimelineToSO(timeline, soPath);
                        if (result != null)
                        {
                            converted++;
                            Debug.Log($"[TimelineConverter] ✅ Converted: {timeline.name}");
                        }
                        else
                        {
                            skipped++;
                            Debug.LogWarning($"[TimelineConverter] ⚠️ Skipped: {timeline.name} (conversion failed)");
                        }
                    }
                    catch (Exception e)
                    {
                        skipped++;
                        Debug.LogError($"[TimelineConverter] ❌ Error converting {timeline.name}: {e.Message}");
                    }
                }
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Timeline Conversion Complete",
                $"Converted {converted} Timeline Assets to ScriptableObjects\nSkipped: {skipped}", "OK");
            Debug.Log($"[TimelineConverter] Conversion complete - Success: {converted}, Skipped: {skipped}");
        }

        /// <summary>
        /// 선택된 Timeline Asset 변환
        /// </summary>
        [MenuItem("Assets/*COSMOS*/Convert to Timeline SO", false, 100)]
        public static void ConvertSelectedTimeline()
        {
            var selection = Selection.activeObject as TimelineAsset;
            if (selection == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a Timeline Asset", "OK");
                return;
            }

            // 캐릭터명 추출
            string characterName = ExtractCharacterNameFromTimeline(selection);
            if (string.IsNullOrEmpty(characterName))
            {
                // Timeline 이름에서 캐릭터명을 못 찾으면 사용자에게 물어봄
                characterName = EditorUtility.DisplayDialog("Character Name",
                    "Enter character name:", "Character", "Cancel") ? "Character" : "";

                if (string.IsNullOrEmpty(characterName))
                    return;
            }


            string outputPath = $"Assets/Cosmos/ResourcesAddressable/Characters/{characterName}/Animations/{selection.name}_Data.asset";
           
            
            // 폴더 존재 확인 및 생성
            string animationsFolder = $"Assets/Cosmos/ResourcesAddressable/Characters/{characterName}/Animations";
            if (!AssetDatabase.IsValidFolder(animationsFolder))
            {
                string[] folders = animationsFolder.Split('/');
                string currentPath = folders[0];
                for (int i = 1; i < folders.Length; i++)
                {
                    string nextPath = $"{currentPath}/{folders[i]}";
                    if (!AssetDatabase.IsValidFolder(nextPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, folders[i]);
                    }
                    currentPath = nextPath;
                }
            }


            // 확인 다이얼로그
            if (EditorUtility.DisplayDialog("Convert Timeline",
                $"Convert '{selection.name}' to TimelineDataSO?\n\nOutput: {outputPath}",
                "Convert", "Cancel"))
            {
                try
                {
                    var result = ConvertTimelineToSO(selection, outputPath);
                    if (result != null)
                    {
                        Selection.activeObject = result;
                        EditorGUIUtility.PingObject(result);
                        EditorUtility.DisplayDialog("Success",
                            $"Timeline converted successfully!\n{outputPath}", "OK");
                    }
                }
                catch (Exception e)
                {
                    EditorUtility.DisplayDialog("Error",
                        $"Failed to convert Timeline:\n{e.Message}", "OK");
                }
            }
        }

        /// <summary>
        /// 메뉴 아이템 검증
        /// </summary>
        [MenuItem("Assets/*COSMOS*/Convert to Timeline SO", true)]
        public static bool ValidateConvertSelectedTimeline()
        {
            return Selection.activeObject is TimelineAsset;
        }

        /// <summary>
        /// 특정 캐릭터의 Timeline 변환 (캐릭터명 지정)
        /// </summary>
        public static void ConvertCharacterTimelines(string characterName)
        {
            string timelinePath = $"Assets/Dev/Cosmos/Timeline/Characters/{characterName}";
            string outputPath = $"Assets/Cosmos/ResourcesAddressable/ScriptableObjects/Timelines/{characterName}";

            if (!AssetDatabase.IsValidFolder(timelinePath))
            {
                Debug.LogError($"[TimelineConverter] Character timeline folder not found: {timelinePath}");
                return;
            }

            // 출력 폴더 생성
            if (!AssetDatabase.IsValidFolder(outputPath))
            {
                System.IO.Directory.CreateDirectory(outputPath);
            }

            // Timeline Asset 찾기
            string[] guids = AssetDatabase.FindAssets("t:TimelineAsset", new[] { timelinePath });

            Debug.Log($"[TimelineConverter] Converting {characterName} timelines. Found {guids.Length} timelines.");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);

                if (timeline != null)
                {
                    string soPath = $"{outputPath}/{timeline.name}_Data.asset";
                    ConvertTimelineToSO(timeline, soPath);
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[TimelineConverter] {characterName} timeline conversion complete.");
        }
    }
}
#endif