using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    public static class CutSceneEditorMenu
    {
        private const string CUTSCENE_MAKER_SCENE_PATH = "Assets/Cosmos/PortraitNew/Scene/CutSceneMaker.unity";

        [MenuItem("*COSMOS*/CutScene/Open CutSceneMaker", false, 1)]
        public static void OpenCutSceneMaker()
        {
            OpenScene(CUTSCENE_MAKER_SCENE_PATH, "CutSceneMaker");
        }

        [MenuItem("*COSMOS*/CutScene/CSV to Timeline Converter", false, 10)]
        public static void OpenCSVToTimelineConverter()
        {
            CSVToTimelineConverter.ShowWindow();
        }

        private static void OpenScene(string scenePath, string sceneName)
        {
            // 씬 파일이 존재하는지 확인
            if (!System.IO.File.Exists(scenePath))
            {
                EditorUtility.DisplayDialog("오류", $"씬 파일을 찾을 수 없습니다:\n{scenePath}", "확인");
                return;
            }

            // 현재 씬에 변경사항이 있는지 확인
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                if (EditorUtility.DisplayDialog("씬 변경사항", 
                    "현재 씬에 저장되지 않은 변경사항이 있습니다. 저장하시겠습니까?", 
                    "저장", "저장하지 않음"))
                {
                    EditorSceneManager.SaveOpenScenes();
                }
            }

            // 씬 열기
            try
            {
                EditorSceneManager.OpenScene(scenePath);
                Debug.Log($"씬이 열렸습니다: {sceneName}");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("오류", $"씬을 열 수 없습니다:\n{e.Message}", "확인");
                Debug.LogError($"씬 열기 실패: {e}");
            }
        }

        // 메뉴 유효성 검사
        [MenuItem("Cosmos/CutScene/Open CutSceneMaker", true)]
        public static bool ValidateOpenCutSceneMaker()
        {
            return System.IO.File.Exists(CUTSCENE_MAKER_SCENE_PATH);
        }
    }
}
