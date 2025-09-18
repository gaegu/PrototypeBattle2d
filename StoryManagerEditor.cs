using UnityEngine;
using UnityEditor;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    [CustomEditor(typeof(StoryManager))]
    public class StoryManagerEditor : UnityEditor.Editor
    {
        private int selectedSceneIndex = 0;
        private string timelineFileName = "";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            StoryManager storyManager = (StoryManager)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("타임라인 관리", EditorStyles.boldLabel);

            // 씬 선택
            if (storyManager.sceneDialogueData.Count > 0)
            {
                string[] sceneNames = new string[storyManager.sceneDialogueData.Count];
                for (int i = 0; i < sceneNames.Length; i++)
                {
                    sceneNames[i] = $"Scene {i}: {storyManager.sceneDialogueData[i].name}";
                }

                selectedSceneIndex = EditorGUILayout.Popup("선택된 씬", selectedSceneIndex, sceneNames);
                timelineFileName = EditorGUILayout.TextField("타임라인 파일명", timelineFileName);

                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("씬 재생"))
                {
                    storyManager.PlayScene(selectedSceneIndex);
                }

                if (GUILayout.Button("타임라인으로 저장"))
                {
                    string fileName = string.IsNullOrEmpty(timelineFileName) ? 
                        $"Timeline_Scene_{selectedSceneIndex}" : timelineFileName;
                    storyManager.SaveSceneAsTimeline(selectedSceneIndex, fileName);
                }

                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("현재 재생 중지"))
                {
                    storyManager.StopCurrentScene();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("씬 데이터가 없습니다. Scene Dialogue Data에 DialogueData를 추가해주세요.", MessageType.Info);
            }
        }
    }
}
