using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using IronJade.ResourcesAddressable._2DRenewal.PortraitNew;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    public class CSVToTimelineConverter : EditorWindow
    {
        private string csvFilePath = "";
        private string outputPath = "Assets/Cosmos/PortraitNew/TimelineAssets/";
        private string outputFileName = "Timeline_Converted";
        private TimelineAsset templateTimeline;
        
        // 트랙 참조들
        private DialogueTrack dialogueTrack;
        private SoundTrack soundTrack;
        private ActionTrack actionTrack;
        private ImageToonTrack imageToonTrack;
        private CustomTrack customTrack;

        [MenuItem("IronJade/CSV to Timeline Converter")]
        public static void ShowWindow()
        {
            GetWindow<CSVToTimelineConverter>("CSV to Timeline Converter");
        }

        private void OnGUI()
        {
            GUILayout.Label("CSV to Timeline Converter", EditorStyles.boldLabel);
            
            GUILayout.Space(10);
            
            // CSV 파일 선택
            GUILayout.Label("CSV 파일 선택:");
            EditorGUILayout.BeginHorizontal();
            csvFilePath = EditorGUILayout.TextField(csvFilePath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFilePanel("CSV 파일 선택", "", "csv");
                if (!string.IsNullOrEmpty(path))
                {
                    csvFilePath = path;
                }
            }
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // 템플릿 타임라인 선택
            GUILayout.Label("템플릿 타임라인:");
            templateTimeline = (TimelineAsset)EditorGUILayout.ObjectField(templateTimeline, typeof(TimelineAsset), false);
            
            GUILayout.Space(10);
            
            // 출력 설정
            GUILayout.Label("출력 설정:");
            outputPath = EditorGUILayout.TextField("출력 경로:", outputPath);
            outputFileName = EditorGUILayout.TextField("파일명:", outputFileName);
            
            GUILayout.Space(20);
            
            // 변환 버튼
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(csvFilePath) || templateTimeline == null);
            if (GUILayout.Button("CSV를 Timeline으로 변환"))
            {
                ConvertCSVToTimeline();
            }
            EditorGUI.EndDisabledGroup();
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("폴더 열기"))
            {
                if (Directory.Exists(outputPath))
                {
                    EditorUtility.RevealInFinder(outputPath);
                }
            }
            
            // 도움말
            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "1. CSV 파일과 템플릿 타임라인을 선택하세요.\n" +
                "2. 템플릿 타임라인에는 DialogueTrack, SoundTrack, ActionTrack, ImageToonTrack, CustomTrack이 있어야 합니다.\n" +
                "3. 변환 버튼을 클릭하면 CSV 데이터가 타임라인 클립으로 변환됩니다.",
                MessageType.Info
            );
        }

        private void ConvertCSVToTimeline()
        {
            if (string.IsNullOrEmpty(csvFilePath) || !File.Exists(csvFilePath))
            {
                EditorUtility.DisplayDialog("오류", "유효한 CSV 파일을 선택해주세요.", "확인");
                return;
            }

            if (templateTimeline == null)
            {
                EditorUtility.DisplayDialog("오류", "템플릿 타임라인을 선택해주세요.", "확인");
                return;
            }

            try
            {
                // CSV 파일 읽기 및 파싱
                DialogueData dialogueData = ParseCSVFile(csvFilePath);
                if (dialogueData == null)
                {
                    EditorUtility.DisplayDialog("오류", "CSV 파일 파싱에 실패했습니다.", "확인");
                    return;
                }

                // 타임라인 생성
                TimelineAsset timeline = CreateTimelineFromData(dialogueData);
                if (timeline == null)
                {
                    EditorUtility.DisplayDialog("오류", "타임라인 생성에 실패했습니다.", "확인");
                    return;
                }

                // 타임라인 저장
                SaveTimelineAsset(timeline, outputFileName);

                EditorUtility.DisplayDialog("성공", $"변환이 완료되었습니다!\n저장 위치: {outputPath}{outputFileName}.playable", "확인");
                
                // 생성된 파일 선택
                string fullPath = $"{outputPath}{outputFileName}.playable";
                var timelineAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(fullPath);
                if (timelineAsset != null)
                {
                    Selection.activeObject = timelineAsset;
                    EditorGUIUtility.PingObject(timelineAsset);
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("오류", $"변환 중 오류가 발생했습니다:\n{e.Message}", "확인");
                Debug.LogError($"CSV to Timeline 변환 오류: {e}");
            }
        }

        private DialogueData ParseCSVFile(string filePath)
        {
            string[] lines = File.ReadAllLines(filePath);
            if (lines.Length < 2)
            {
                Debug.LogError("CSV 파일이 비어있거나 헤더만 있습니다.");
                return null;
            }

            List<DialogueEntry> entries = new List<DialogueEntry>();
            
            for (int i = 1; i < lines.Length; i++) // 첫 번째 줄(헤더) 제외
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;

                DialogueEntry entry = ParseCSVLine(line);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }

            DialogueData dialogueData = ScriptableObject.CreateInstance<DialogueData>();
            dialogueData.entries = entries.ToArray();
            
            return dialogueData;
        }

        private DialogueEntry ParseCSVLine(string line)
        {
            string[] fields = ParseCSVFields(line);
            
            // 후처리: 모든 필드 정규화
            for (int fi = 0; fi < fields.Length; fi++)
            {
                fields[fi] = CleanCsvField(fields[fi]);
            }
            
            if (fields.Length < 3)
            {
                Debug.LogError($"CSV 라인 필드 수가 부족합니다: {line}");
                return null;
            }

            DialogueEntry entry = new DialogueEntry();
            
            // ID 파싱
            if (int.TryParse(fields[0], out int id))
            {
                entry.id = id;
            }
            else
            {
                Debug.LogError($"ID 파싱 실패: {fields[0]}");
                return null;
            }

            // DataType 파싱
            if (System.Enum.TryParse<DialogueDataType>(fields[1], true, out DialogueDataType dataType))
            {
                entry.dataType = dataType;
            }
            else
            {
                Debug.LogError($"DataType 파싱 실패: {fields[1]}");
                return null;
            }

            // CSV 컬럼 구조: ID,DataType,Character,Dialogue,ImageToonPath,SoundPath,Duration
            // Duration 파싱 (마지막 컬럼)
            if (fields.Length >= 7 && !string.IsNullOrEmpty(fields[6]))
            {
                if (float.TryParse(fields[6], out float duration))
                {
                    entry.duration = duration;
                }
                else
                {
                    Debug.LogWarning($"Duration 파싱 실패, 기본값 사용: {fields[6]}");
                    entry.duration = GetDefaultDuration(entry.dataType);
                }
            }
            else
            {
                // Duration이 없으면 기본값 사용
                entry.duration = GetDefaultDuration(entry.dataType);
            }
            
            // DataType에 따른 필드 파싱
            switch (entry.dataType)
            {
                case DialogueDataType.Dialogue:
                    if (fields.Length >= 4)
                    {
                        entry.character = fields[2];
                        entry.dialogue = fields[3];
                    }
                    break;
                    
                case DialogueDataType.ImageToon:
                    if (fields.Length >= 3)
                    {
                        entry.imagePath = fields[2];
                    }
                    break;
                    
                case DialogueDataType.Sound:
                    if (fields.Length >= 3)
                    {
                        entry.soundPath = fields[2];
                    }
                    break;
                    
                case DialogueDataType.Action:
                    if (fields.Length >= 3)
                    {
                        if (System.Enum.TryParse<ActionType>(fields[2], true, out ActionType actionType))
                        {
                            entry.actionType = actionType;
                        }
                        
                        // 추가 액션 데이터 파싱
                        if (fields.Length >= 4)
                        {
                            switch (entry.actionType)
                            {
                                case ActionType.Move:
                                    // Transform 참조는 나중에 설정
                                    break;
                                case ActionType.Animation:
                                    entry.animationName = fields[3];
                                    break;
                                case ActionType.Emotion:
                                    entry.emotionType = fields[3];
                                    break;
                            }
                        }
                    }
                    break;
                    
                case DialogueDataType.DialogueToon:
                    if (fields.Length >= 4)
                    {
                        entry.character = fields[2];
                        entry.dialogue = fields[3];
                        if (fields.Length >= 5)
                        {
                            entry.imagePath = fields[4];
                        }
                    }
                    break;
                    
                case DialogueDataType.Custom:
                    if (fields.Length >= 4)
                    {
                        entry.customJson = fields[2]; // JSON 데이터
                        if (fields.Length >= 5)
                        {
                            // Character 컬럼에 커스텀 타입을 저장 (예: "CameraShake", "ParticleEffect" 등)
                            entry.character = fields[3];
                        }
                    }
                    break;
            }

            return entry;
        }

        private float GetDefaultDuration(DialogueDataType dataType)
        {
            switch (dataType)
            {
                case DialogueDataType.Dialogue:
                case DialogueDataType.DialogueToon:
                    return 3.0f; // 대사는 3초
                case DialogueDataType.ImageToon:
                case DialogueDataType.Sound:
                    return 2.0f; // 이미지/사운드는 2초
                case DialogueDataType.Action:
                case DialogueDataType.Custom:
                default:
                    return 1.0f; // 기타는 1초
            }
        }

        private TimelineAsset CreateTimelineFromData(DialogueData dialogueData)
        {
            Debug.Log("템플릿 타임라인을 복제하여 새 타임라인 생성 시작...");
            
            // 1단계: 템플릿을 복제하여 새로운 타임라인 생성
            string newTimelinePath = $"{outputPath}{outputFileName}.playable";
            
            // 디렉토리가 없으면 생성
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }
            
            // 템플릿을 새 경로로 복사
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(templateTimeline), newTimelinePath);
            TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(newTimelinePath);
            timeline.name = outputFileName;
            
            // 2단계: 복제된 타임라인의 모든 클립 제거
            ClearAllClips(timeline);
            
            // 트랙 찾기
            dialogueTrack = FindTrack<DialogueTrack>(timeline);
            soundTrack = FindTrack<SoundTrack>(timeline);
            actionTrack = FindTrack<ActionTrack>(timeline);
            imageToonTrack = FindTrack<ImageToonTrack>(timeline);
            customTrack = FindTrack<CustomTrack>(timeline);
            
            if (dialogueTrack == null || soundTrack == null || actionTrack == null || imageToonTrack == null)
            {
                Debug.LogError("필요한 트랙을 찾을 수 없습니다! 템플릿 타임라인을 확인해주세요.");
                return null;
            }
            
            Debug.Log("✓ 트랙 찾기 완료");

            float currentTime = 0f;
            int clipCount = 0;

            foreach (DialogueEntry entry in dialogueData.entries)
            {
                Debug.Log($"엔트리 처리: ID={entry.id}, Type={entry.dataType}, Character={entry.character}");
                
                switch (entry.dataType)
                {
                    case DialogueDataType.Dialogue:
                        CreateDialogueClip(dialogueTrack, entry, currentTime);
                        clipCount++;
                        break;
                        
                    case DialogueDataType.Sound:
                        CreateSoundClip(soundTrack, entry, currentTime);
                        clipCount++;
                        break;
                        
                    case DialogueDataType.Action:
                        CreateActionClip(actionTrack, entry, currentTime);
                        clipCount++;
                        break;
                        
                    case DialogueDataType.ImageToon:
                        CreateImageToonClip(imageToonTrack, entry, currentTime);
                        clipCount++;
                        break;
                        
                    case DialogueDataType.DialogueToon:
                        CreateDialogueToonClip(imageToonTrack, dialogueTrack, entry, currentTime);
                        clipCount += 2; // 대화 + 이미지툰
                        break;
                        
                    case DialogueDataType.Custom:
                        CreateCustomClip(customTrack, entry, currentTime);
                        clipCount++;
                        break;
                }
                
                currentTime += entry.duration;
            }
            
            Debug.Log($"✓ 템플릿에 클립 채우기 완료: {clipCount}개 클립 생성");
            return timeline;
        }

        private T FindTrack<T>(TimelineAsset timeline) where T : TrackAsset
        {
            foreach (var track in timeline.GetOutputTracks())
            {
                if (track is T targetTrack)
                {
                    return targetTrack;
                }
            }
            return null;
        }


        private void ClearAllClips(TimelineAsset timeline)
        {
            foreach (var track in timeline.GetOutputTracks())
            {
                var clips = new List<TimelineClip>(track.GetClips());
                foreach (var clip in clips)
                {
                    track.DeleteClip(clip);
                }
            }
            Debug.Log("기존 클립들 제거 완료");
        }

        private void CreateDialogueClip(DialogueTrack track, DialogueEntry entry, float startTime)
        {
            TimelineClip timelineClip = track.CreateDefaultClip();
            DialogueClip clip = timelineClip.asset as DialogueClip;
            
            if (clip != null)
            {
                timelineClip.start = startTime;
                timelineClip.duration = entry.duration;
                
                clip.dialogueData.id = entry.id;
                clip.dialogueData.characterName = entry.character;
                clip.dialogueData.dialogueText = entry.dialogue;
                
                EditorUtility.SetDirty(clip);
                EditorUtility.SetDirty(track);
            }
        }

        private void CreateSoundClip(SoundTrack track, DialogueEntry entry, float startTime)
        {
            TimelineClip timelineClip = track.CreateDefaultClip();
            SoundClip clip = timelineClip.asset as SoundClip;
            
            if (clip != null)
            {
                timelineClip.start = startTime;
                timelineClip.duration = entry.duration;
                
                clip.soundData.id = entry.id;
                clip.soundData.soundPath = entry.soundPath;
                
                EditorUtility.SetDirty(clip);
                EditorUtility.SetDirty(track);
            }
        }

        private void CreateActionClip(ActionTrack track, DialogueEntry entry, float startTime)
        {
            TimelineClip timelineClip = track.CreateDefaultClip();
            ActionClip clip = timelineClip.asset as ActionClip;
            
            if (clip != null)
            {
                timelineClip.start = startTime;
                timelineClip.duration = entry.duration;
                
                clip.actionData.id = entry.id;
                clip.actionData.actionType = entry.actionType;
                clip.actionData.targetObject = entry.targetTransform != null ? entry.targetTransform : null;
                clip.actionData.animationName = entry.animationName;
                clip.actionData.emotionType = entry.emotionType;
                
                EditorUtility.SetDirty(clip);
                EditorUtility.SetDirty(track);
            }
        }

        private void CreateImageToonClip(ImageToonTrack track, DialogueEntry entry, float startTime)
        {
            TimelineClip timelineClip = track.CreateDefaultClip();
            ImageToonClip clip = timelineClip.asset as ImageToonClip;
            
            if (clip != null)
            {
                timelineClip.start = startTime;
                timelineClip.duration = entry.duration;
                
                clip.imageToonData.id = entry.id;
                clip.imageToonData.imagePath = entry.imagePath;
                
                // imagePath에서 스프라이트 로드 시도
                if (!string.IsNullOrEmpty(entry.imagePath))
                {
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.imagePath);
                    if (sprite != null)
                    {
                        clip.imageToonData.imageToonSprite = sprite;
                    }
                    else
                    {
                        Debug.LogWarning($"ImageToonClip: 스프라이트를 로드할 수 없습니다: {entry.imagePath}");
                    }
                }
                
                EditorUtility.SetDirty(clip);
                EditorUtility.SetDirty(track);
            }
        }

        private void CreateDialogueToonClip(ImageToonTrack imageTrack, DialogueTrack dialogueTrack, DialogueEntry entry, float startTime)
        {
            CreateImageToonClip(imageTrack, entry, startTime);
            CreateDialogueClip(dialogueTrack, entry, startTime);
        }

        private void CreateCustomClip(CustomTrack track, DialogueEntry entry, float startTime)
        {
            TimelineClip timelineClip = track.CreateDefaultClip();
            CustomClip clip = timelineClip.asset as CustomClip;
            
            if (clip != null)
            {
                timelineClip.start = startTime;
                timelineClip.duration = entry.duration;
                
                clip.customData.id = entry.id;
                clip.customData.customJson = entry.customJson;
                clip.customData.customType = entry.character;
                
                EditorUtility.SetDirty(clip);
                EditorUtility.SetDirty(track);
            }
        }

        private void SaveTimelineAsset(TimelineAsset timeline, string fileName)
        {
            // 타임라인을 더티로 표시하여 변경사항 저장
            EditorUtility.SetDirty(timeline);
            
            // 모든 클립들을 더티로 표시
            foreach (var track in timeline.GetOutputTracks())
            {
                foreach (var clip in track.GetClips())
                {
                    if (clip.asset != null)
                    {
                        EditorUtility.SetDirty(clip.asset);
                    }
                }
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            string fullPath = $"{outputPath}{fileName}.playable";
            Debug.Log($"타임라인이 저장되었습니다: {fullPath}");
        }

        private string[] ParseCSVFields(string line)
        {
            List<string> fields = new List<string>();
            bool inQuotes = false;
            string currentField = "";
            
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // 이스케이프된 따옴표
                        currentField += '"';
                        i++; // 다음 따옴표 건너뛰기
                    }
                    else
                    {
                        // 따옴표 시작/끝
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    // 필드 구분자
                    fields.Add(currentField.Trim());
                    currentField = "";
                }
                else
                {
                    currentField += c;
                }
            }
            
            // 마지막 필드 추가
            fields.Add(currentField.Trim());
            
            return fields.ToArray();
        }

        private string CleanCsvField(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            string v = value.Trim();
            // 연속 이스케이프 따옴표 축약
            v = v.Replace("\"\"", "\"");
            // 제거 대상 따옴표 집합
            char[] quoteChars = new char[] { '"', '\u201C', '\u201D', '\u201E', '\u00AB', '\u00BB', '\uFF02' };
            // 앞뒤 따옴표를 반복적으로 제거 (혼합된 경우도 처리)
            bool stripped = true;
            while (v.Length >= 2 && stripped)
            {
                stripped = false;
                char first = v[0];
                char last = v[v.Length - 1];
                bool firstIsQuote = System.Array.IndexOf(quoteChars, first) >= 0;
                bool lastIsQuote = System.Array.IndexOf(quoteChars, last) >= 0;
                if (firstIsQuote && lastIsQuote)
                {
                    v = v.Substring(1, v.Length - 2).Trim();
                    stripped = true;
                }
            }
            return v;
        }
    }
}
