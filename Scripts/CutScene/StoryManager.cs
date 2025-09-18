using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using IronJade.ResourcesAddressable._2DRenewal.PortraitNew;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class StoryManager : MonoBehaviour
{
    private static StoryManager instance;
    public static StoryManager Instance
    {
        get
        {
            if (instance == null)
                instance = FindObjectOfType<StoryManager>();
            
            if (instance == null)
            {
                GameObject go = new GameObject("StoryManager");
                instance = go.AddComponent<StoryManager>();
            }

            return instance;
        }
    }

    [Header("씬별 대화 데이터")]
    [SerializeField] public List<DialogueData> sceneDialogueData = new List<DialogueData>();
    
    [Header("타임라인 설정")]
    [SerializeField] private PlayableDirector playableDirector;
    [SerializeField] private TimelineAsset templateTimeline; // 미리 설정된 타임라인 템플릿
    
    [Header("UI 참조")]
    [SerializeField] private DialogueUI dialogueUI;
    
    private TimelineAsset currentTimeline;
    private int currentSceneIndex = 0;

    const string UIPath = "2DRenewal/PortraitNew/Prefabs/DialogueUI.prefab";

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // PlayableDirector 완료 이벤트 등록
        if (playableDirector != null)
        {
            playableDirector.stopped += OnTimelineStopped;
        }
    }

    private void OnDestroy()
    {
        // 이벤트 해제
        if (playableDirector != null)
        {
            playableDirector.stopped -= OnTimelineStopped;
        }
    }

    private void OnTimelineStopped(PlayableDirector director)
    {
        Debug.Log("타임라인 재생 완료");
        // 필요한 후처리 작업을 여기에 추가
    }

    public void StartStory()
    {
        InitializeStory();
    }

    public async void InitializeStory()
    {
        Debug.Log("StoryManager 초기화 시작...");
        
        // UI 로드 (Addressable 로딩 실패 시 기본 UI 생성)
        if (dialogueUI == null)
        {
            try
            {
                GameObject uiPrefab = await UtilModel.Resources.LoadAsync<GameObject>(UIPath);
                if (uiPrefab != null)
                {
                    GameObject uiObject = Instantiate(uiPrefab);
                    dialogueUI = uiObject.GetComponent<DialogueUI>();
                    Debug.Log("✓ DialogueUI 로드 완료");
                }
                else
                {
                    Debug.LogWarning("DialogueUI 프리팹을 찾을 수 없습니다. 기본 UI를 생성합니다.");
                    CreateDefaultDialogueUI();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"DialogueUI 로드 실패: {e.Message}. 기본 UI를 생성합니다.");
                CreateDefaultDialogueUI();
            }
        }

        // PlayableDirector 설정
        if (playableDirector == null)
        {
            playableDirector = GetComponent<PlayableDirector>();
            if (playableDirector == null)
            {
                playableDirector = gameObject.AddComponent<PlayableDirector>();
                Debug.Log("✓ PlayableDirector 생성 완료");
            }
        }

        // PlayableDirector 설정
        playableDirector.extrapolationMode = DirectorWrapMode.None;
        playableDirector.timeUpdateMode = DirectorUpdateMode.GameTime;
        
        // 이벤트 등록
        playableDirector.stopped += OnTimelineStopped;
        
        Debug.Log("✓ StoryManager 초기화 완료");
    }

    private void CreateDefaultDialogueUI()
    {
        // 기본 DialogueUI 생성
        GameObject uiObject = new GameObject("DefaultDialogueUI");
        dialogueUI = uiObject.AddComponent<DialogueUI>();
        
        // Canvas에 추가
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            uiObject.transform.SetParent(canvas.transform, false);
        }
        
        Debug.Log("✓ 기본 DialogueUI 생성 완료");
    }

    /// <summary>
    /// 특정 씬의 대화 데이터를 타임라인으로 변환하여 재생
    /// </summary>
    public void PlayScene(int sceneIndex)
    {
        Debug.Log($"PlayScene 호출됨: sceneIndex = {sceneIndex}");
        
        if (sceneIndex < 0 || sceneIndex >= sceneDialogueData.Count)
        {
            Debug.LogError($"Scene index {sceneIndex} is out of range! (총 {sceneDialogueData.Count}개 씬)");
            return;
        }

        if (templateTimeline == null)
        {
            Debug.LogError("템플릿 타임라인이 설정되지 않았습니다!");
            return;
        }

        // 현재 재생 중인 타임라인 정지
        StopCurrentScene();

        currentSceneIndex = sceneIndex;
        DialogueData sceneData = sceneDialogueData[sceneIndex];
        
        Debug.Log($"씬 데이터 로드: {sceneData.entries.Length}개 엔트리");
        
        // 템플릿 타임라인을 복사하고 클립 생성
        TimelineAsset timeline = CreateTimelineFromTemplate(sceneData);
        
        if (timeline == null)
        {
            Debug.LogError("타임라인 생성 실패!");
            return;
        }
        
        Debug.Log($"타임라인 생성 완료: {timeline.outputTrackCount}개 트랙");
        
        // 타임라인 재생
        playableDirector.playableAsset = timeline;
        playableDirector.time = 0; // 시간을 0으로 리셋
        playableDirector.Play();
        
        Debug.Log($"씬 {sceneIndex} 재생 시작 완료");
    }

    /// <summary>
    /// 템플릿 타임라인에 대화 데이터로 클립 생성
    /// </summary>
    private TimelineAsset CreateTimelineFromTemplate(DialogueData dialogueData)
    {
        Debug.Log("템플릿 타임라인을 복제하여 새 타임라인 생성 시작...");
        
        // 1단계: 템플릿을 복제하여 새로운 타임라인 생성
        string newTimelinePath = $"Assets/IronJade/ResourcesAddressable/2DRenewal/PortraitNew/TimelineAssets/Timeline_Scene_{currentSceneIndex}.playable";
        
        // 디렉토리가 없으면 생성
        string directory = System.IO.Path.GetDirectoryName(newTimelinePath);
        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }
        
        // 템플릿을 새 경로로 복사
        UnityEditor.AssetDatabase.CopyAsset(UnityEditor.AssetDatabase.GetAssetPath(templateTimeline), newTimelinePath);
        TimelineAsset timeline = UnityEditor.AssetDatabase.LoadAssetAtPath<TimelineAsset>(newTimelinePath);
        timeline.name = $"Timeline_Scene_{currentSceneIndex}";
        
        // 2단계: 복제된 타임라인의 모든 클립 제거
        ClearAllClips(timeline);
        
        // 트랙 찾기
        DialogueTrack dialogueTrack = FindTrack<DialogueTrack>(timeline);
        SoundTrack soundTrack = FindTrack<SoundTrack>(timeline);
        ActionTrack actionTrack = FindTrack<ActionTrack>(timeline);
        ImageToonTrack imageToonTrack = FindTrack<ImageToonTrack>(timeline);
        CustomTrack customTrack = FindTrack<CustomTrack>(timeline);
        
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

    /// <summary>
    /// 타임라인에서 특정 타입의 트랙 찾기
    /// </summary>
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


    /// <summary>
    /// 타임라인의 모든 클립 제거
    /// </summary>
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

    /// <summary>
    /// 특정 씬의 대화 데이터를 템플릿 타임라인에 클립을 채워서 파일로 저장
    /// </summary>
    public void SaveSceneAsTimeline(int sceneIndex, string fileName = "")
    {
        if (sceneIndex < 0 || sceneIndex >= sceneDialogueData.Count)
        {
            Debug.LogError($"Scene index {sceneIndex} is out of range!");
            return;
        }

        if (templateTimeline == null)
        {
            Debug.LogError("템플릿 타임라인이 설정되지 않았습니다!");
            return;
        }

        DialogueData sceneData = sceneDialogueData[sceneIndex];
        
        // 템플릿을 복사하여 새 타임라인에 클립 채우기
        TimelineAsset timeline = CreateTimelineFromTemplate(sceneData);
        
        if (timeline == null)
        {
            Debug.LogError("템플릿에 클립 채우기 실패!");
            return;
        }
        
        // 파일명 설정
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = $"Timeline_Scene_{sceneIndex}";
        }
        
        // 타임라인을 파일로 저장
        SaveTimelineAsset(timeline, fileName);
    }

    /// <summary>
    /// TimelineAsset을 파일로 저장
    /// </summary>
    private void SaveTimelineAsset(TimelineAsset timeline, string fileName)
    {
        // 타임라인을 더티로 표시하여 변경사항 저장
        UnityEditor.EditorUtility.SetDirty(timeline);
        
        // 모든 클립들을 더티로 표시
        foreach (var track in timeline.GetOutputTracks())
        {
            foreach (var clip in track.GetClips())
            {
                if (clip.asset != null)
                {
                    UnityEditor.EditorUtility.SetDirty(clip.asset);
                }
            }
        }
        
        // 트랙들의 바인딩 정리 (다른 씬에서 사용할 수 있도록)
        CleanupTimelineBindings(timeline);
        
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
        
        string path = $"Assets/IronJade/ResourcesAddressable/2DRenewal/PortraitNew/TimelineAssets/{fileName}.playable";
        Debug.Log($"타임라인이 저장되었습니다: {path}");
    }
    
    /// <summary>
    /// 타임라인의 바인딩을 정리하여 다른 씬에서 사용할 수 있도록 함
    /// </summary>
    private void CleanupTimelineBindings(TimelineAsset timeline)
    {
        // 모든 트랙의 바인딩 해제
        foreach (var track in timeline.GetOutputTracks())
        {
            // 바인딩 해제
            playableDirector.SetGenericBinding(track, null);
        }
        
        Debug.Log("타임라인 바인딩 정리 완료");
    }

    private void CreateDialogueClip(DialogueTrack track, DialogueEntry entry, float startTime)
    {
        // 기본 클립 생성 후 직접 설정
        TimelineClip timelineClip = track.CreateDefaultClip();
        DialogueClip clip = timelineClip.asset as DialogueClip;
        
        if (clip != null)
        {
            timelineClip.start = startTime;
            timelineClip.duration = entry.duration;
            
            // 데이터 설정
            clip.dialogueData.id = entry.id;
            clip.dialogueData.characterName = entry.character;
            clip.dialogueData.dialogueText = entry.dialogue;
            
            // Unity에 변경사항 알림
            UnityEditor.EditorUtility.SetDirty(clip);
            UnityEditor.EditorUtility.SetDirty(track);
        }
    }

    private void CreateSoundClip(SoundTrack track, DialogueEntry entry, float startTime)
    {
        // 기본 클립 생성 후 직접 설정
        TimelineClip timelineClip = track.CreateDefaultClip();
        SoundClip clip = timelineClip.asset as SoundClip;
        
        if (clip != null)
        {
            timelineClip.start = startTime;
            timelineClip.duration = entry.duration;
            
            // 데이터 설정
            clip.soundData.id = entry.id;
            clip.soundData.soundPath = entry.soundPath;
            
            // Unity에 변경사항 알림
            UnityEditor.EditorUtility.SetDirty(clip);
            UnityEditor.EditorUtility.SetDirty(track);
        }
    }

    private void CreateActionClip(ActionTrack track, DialogueEntry entry, float startTime)
    {
        // 기본 클립 생성 후 직접 설정
        TimelineClip timelineClip = track.CreateDefaultClip();
        ActionClip clip = timelineClip.asset as ActionClip;
        
        if (clip != null)
        {
            timelineClip.start = startTime;
            timelineClip.duration = entry.duration;
            
            // 데이터 설정
            clip.actionData.id = entry.id;
            clip.actionData.actionType = entry.actionType;
            clip.actionData.targetObject = entry.targetTransform != null ? entry.targetTransform : null;
            clip.actionData.animationName = entry.animationName;
            clip.actionData.emotionType = entry.emotionType;
            
            // 커브 설정 (Move 타입일 때)
            if (entry.actionType == IronJade.ResourcesAddressable._2DRenewal.PortraitNew.ActionType.Move)
            {
                // 기본적으로 부드러운 이동을 위한 Ease 설정
                clip.useCustomEase = true;
                clip.easeType = DG.Tweening.Ease.InOutQuad;
                clip.moveSpeed = 2f; // 기본 이동 속도
                clip.useSpeedInsteadOfDuration = false; // Timeline Duration 사용
                clip.useCustomCurve = false; // DOTween Ease 사용
                
                // 기본 오프셋 설정 (대화를 위한 적당한 거리)
                clip.useOffset = true;
                clip.offsetDistance = 1.5f; // 기본 오프셋 거리
            }
            
            // Unity에 변경사항 알림
            UnityEditor.EditorUtility.SetDirty(clip);
            UnityEditor.EditorUtility.SetDirty(track);
        }
    }

    private void CreateImageToonClip(ImageToonTrack track, DialogueEntry entry, float startTime)
    {
        // 기본 클립 생성 후 직접 설정
        TimelineClip timelineClip = track.CreateDefaultClip();
        ImageToonClip clip = timelineClip.asset as ImageToonClip;
        
        if (clip != null)
        {
            timelineClip.start = startTime;
            timelineClip.duration = entry.duration;
            
            // 데이터 설정
            clip.imageToonData.id = entry.id;
            clip.imageToonData.imagePath = entry.imagePath;
            
            // imagePath에서 스프라이트 로드 시도
            if (!string.IsNullOrEmpty(entry.imagePath))
            {
                Sprite sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(entry.imagePath);
                if (sprite != null)
                {
                    clip.imageToonData.imageToonSprite = sprite;
                }
                else
                {
                    Debug.LogWarning($"ImageToonClip: 스프라이트를 로드할 수 없습니다: {entry.imagePath}");
                }
            }
            
            // Unity에 변경사항 알림
            UnityEditor.EditorUtility.SetDirty(clip);
            UnityEditor.EditorUtility.SetDirty(track);
        }
    }

    private void CreateDialogueToonClip(ImageToonTrack imageTrack, DialogueTrack dialogueTrack, DialogueEntry entry, float startTime)
    {
        // 이미지툰과 대화를 동시에 재생
        CreateImageToonClip(imageTrack, entry, startTime);
        CreateDialogueClip(dialogueTrack, entry, startTime);
    }

    private void CreateCustomClip(CustomTrack track, DialogueEntry entry, float startTime)
    {
        // 기본 클립 생성 후 직접 설정
        TimelineClip timelineClip = track.CreateDefaultClip();
        CustomClip clip = timelineClip.asset as CustomClip;
        
        if (clip != null)
        {
            timelineClip.start = startTime;
            timelineClip.duration = entry.duration;
            
            // 데이터 설정
            clip.customData.id = entry.id;
            clip.customData.customJson = entry.customJson;
            clip.customData.customType = entry.character; // Character 컬럼에 커스텀 타입 저장
            
            // Unity에 변경사항 알림
            UnityEditor.EditorUtility.SetDirty(clip);
            UnityEditor.EditorUtility.SetDirty(track);
        }
    }

    /// <summary>
    /// 씬별 대화 데이터 추가
    /// </summary>
    public void AddSceneDialogueData(DialogueData dialogueData)
    {
        if (!sceneDialogueData.Contains(dialogueData))
        {
            sceneDialogueData.Add(dialogueData);
        }
    }

    /// <summary>
    /// 현재 재생 중인 타임라인 정지
    /// </summary>
    public void StopCurrentScene()
    {
        if (playableDirector != null)
        {
            if (playableDirector.state == PlayState.Playing)
            {
                playableDirector.Stop();
            }
            
            // PlayableGraph 정리
            if (playableDirector.playableGraph.IsValid())
            {
                playableDirector.playableGraph.Destroy();
            }
            
            // 타임라인 에셋 해제
            playableDirector.playableAsset = null;
            
            Debug.Log("타임라인 정지 완료");
        }
    }

    /// <summary>
    /// 타임라인이 재생 중인지 확인
    /// </summary>
    public bool IsPlaying()
    {
        return playableDirector != null && playableDirector.state == PlayState.Playing;
    }

    /// <summary>
    /// 현재 재생 중인 타임라인 일시정지
    /// </summary>
    public void PauseCurrentScene()
    {
        if (playableDirector != null && playableDirector.state == PlayState.Playing)
        {
            playableDirector.Pause();
            Debug.Log("타임라인 일시정지");
        }
    }

    /// <summary>
    /// 일시정지된 타임라인 재개
    /// </summary>
    public void ResumeCurrentScene()
    {
        if (playableDirector != null && playableDirector.state == PlayState.Paused)
        {
            playableDirector.Resume();
            Debug.Log("타임라인 재개");
        }
    }

    /// <summary>
    /// 저장된 타임라인을 로드하고 재생
    /// </summary>
    public void LoadAndPlayTimeline(string timelinePath)
    {
        Debug.Log($"타임라인 로드 시도: {timelinePath}");
        
        // 현재 재생 중인 타임라인 정지
        StopCurrentScene();
        
        // 타임라인 에셋 로드
        TimelineAsset timeline = UnityEditor.AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
        if (timeline == null)
        {
            Debug.LogError($"타임라인을 찾을 수 없습니다: {timelinePath}");
            return;
        }
        
        // 타임라인 설정 및 바인딩
        SetupTimelineBindings(timeline);
        
        // 타임라인 재생
        playableDirector.playableAsset = timeline;
        playableDirector.time = 0;
        playableDirector.Play();
        
        Debug.Log($"타임라인 재생 시작: {timelinePath}");
    }
    
    /// <summary>
    /// 타임라인의 바인딩을 현재 씬의 오브젝트에 설정
    /// </summary>
    private void SetupTimelineBindings(TimelineAsset timeline)
    {
        foreach (var track in timeline.GetOutputTracks())
        {
            if (track is DialogueTrack)
            {
                // DialogueTrack을 DialogueUI에 바인딩
                if (dialogueUI != null)
                {
                    playableDirector.SetGenericBinding(track, dialogueUI);
                    Debug.Log($"DialogueTrack 바인딩 완료: {track.name}");
                }
                else
                {
                    Debug.LogWarning("DialogueUI가 없어서 DialogueTrack을 바인딩할 수 없습니다.");
                }
            }
            else if (track is SoundTrack)
            {
                // SoundTrack을 AudioSource에 바인딩
                AudioSource audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
                playableDirector.SetGenericBinding(track, audioSource);
                Debug.Log($"SoundTrack 바인딩 완료: {track.name}");
            }
            else if (track is ActionTrack)
            {
                // ActionTrack을 Transform에 바인딩 (기본적으로 자기 자신)
                playableDirector.SetGenericBinding(track, transform);
                Debug.Log($"ActionTrack 바인딩 완료: {track.name}");
            }
            else if (track is ImageToonTrack)
            {
                // ImageToonTrack을 DialogueUI에 바인딩
                if (dialogueUI != null)
                {
                    playableDirector.SetGenericBinding(track, dialogueUI);
                    Debug.Log($"ImageToonTrack 바인딩 완료: {track.name}");
                }
                else
                {
                    Debug.LogWarning("DialogueUI가 없어서 ImageToonTrack을 바인딩할 수 없습니다.");
                }
            }
        }
    }
}
