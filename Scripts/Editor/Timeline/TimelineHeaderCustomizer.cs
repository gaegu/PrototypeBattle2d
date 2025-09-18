using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[InitializeOnLoad]
public static class TimelineHeaderCustomizer
{
    static TimelineHeaderCustomizer()
    {
        // Timeline이 그려질 때 이벤트 후킹
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
    }

    private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
    {
        // Timeline 창에서만 작동하도록
        var timelineWindow = EditorWindow.focusedWindow;
        if (timelineWindow == null ||
            timelineWindow.GetType().Name != "TimelineWindow") return;

        // Track 바인딩 텍스트 수정
        ModifyTrackBindingDisplay();
    }

    private static void ModifyTrackBindingDisplay()
    {
        // Timeline의 모든 Track 검사
        var director = UnityEngine.Object.FindAnyObjectByType<PlayableDirector>();
        if (director == null || director.playableAsset == null) return;

        var timeline = director.playableAsset as TimelineAsset;
        if (timeline == null) return;

        foreach (var track in timeline.GetOutputTracks())
        {
            if (track is CosmosAnimationTrack animTrack)
            {
                // Track 이름을 바인딩된 오브젝트 이름으로
                if (animTrack.BoundAnimator != null)
                {
                    track.name = animTrack.BoundAnimator.gameObject.name;
                }
            }
        }
    }
}

