#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class TimeLineCreateEditor : MonoBehaviour
{
    [MenuItem("Window/Sequencing/Battle_Intro_Timeline")]
    private static void CreateTimelineWithTrackGroup()
    {
        // 1. 타임라인 오브젝트 생성
        GameObject timelineObject = new GameObject("_Intro_TimeLine");
        PlayableDirector director = timelineObject.AddComponent<PlayableDirector>();

        // 2. 타임라인 애셋 생성
        TimelineAsset timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();

        // 3. TrackGroup 추가 ("Cam", "Char", "Sound")
        timelineAsset.CreateTrack<GroupTrack>(null, "Cam");
        timelineAsset.CreateTrack<GroupTrack>(null, "Char");
        timelineAsset.CreateTrack<GroupTrack>(null, "Sound");

        // 4. 타임라인 애셋을 프로젝트에 저장할 경로 설정
        string assetPath = "Assets/IronJade/ResourcesAddressable/Character/Battle_Intro_Timeline_Playable.asset";

        // 5. 애셋을 프로젝트에 저장
        AssetDatabase.CreateAsset(timelineAsset, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 6. PlayableDirector에 타임라인 애셋 연결
        director.playableAsset = timelineAsset;

        // 7. 타임라인 생성 완료 메시지 출력
       IronJade.Debug.Log($"Timeline과 PlayableAsset이 {assetPath} 경로에 생성되었습니다.");
    }
}
#endif
