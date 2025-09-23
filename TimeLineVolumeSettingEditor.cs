#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TimeLineVolumeSetting))]
[CanEditMultipleObjects]
public class TimeLineVolumeSettingEditor : Editor
{
    private TimeLineVolumeSetting timeLineVolumeSetting;

    public override void OnInspectorGUI()
    {
        timeLineVolumeSetting = (TimeLineVolumeSetting)target;
        
        if (GUILayout.Button("Volume 셋팅 애니메이션 생성기", GUILayout.Width(250), GUILayout.Height(30)))
        {
            timeLineVolumeSetting.Create();
        }
        
        if (GUILayout.Button("Volume 지우기 저장하기전에 무조건 눌러주세요.", GUILayout.Width(450), GUILayout.Height(30)))
        {
            timeLineVolumeSetting.Destroy();
        }
    }
}
#endif