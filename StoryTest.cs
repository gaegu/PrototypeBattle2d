using UnityEngine;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    public class StoryTest : MonoBehaviour
    {
        [Header("테스트 설정")]
        [SerializeField] private bool autoStart = true;
        [SerializeField] private float startDelay = 2f;

        void Start()
        {
            if (autoStart)
            {
                Invoke("TestStoryManager", startDelay);
            }
        }

        void TestStoryManager()
        {
            Debug.Log("=== StoryManager 테스트 시작 ===");
            
            // 1. StoryManager 인스턴스 확인
            if (StoryManager.Instance == null)
            {
                Debug.LogError("StoryManager.Instance가 null입니다!");
                return;
            }
            
            Debug.Log("✓ StoryManager 인스턴스 확인됨");

            // 2. StoryManager 초기화
            try
            {
                StoryManager.Instance.StartStory();
                Debug.Log("✓ StoryManager 초기화 완료");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"StoryManager 초기화 실패: {e.Message}");
                return;
            }

            // 3. 씬 데이터 확인
            if (StoryManager.Instance.sceneDialogueData.Count == 0)
            {
                Debug.LogWarning("씬 데이터가 없습니다. 테스트용 데이터를 생성합니다.");
                CreateTestData();
            }

            // 4. 첫 번째 씬 재생 시도
            if (StoryManager.Instance.sceneDialogueData.Count > 0)
            {
                Debug.Log("첫 번째 씬 재생 시도...");
                StoryManager.Instance.PlayScene(0);
            }
        }

        void CreateTestData()
        {
            // 테스트용 DialogueData 생성
            DialogueData testData = ScriptableObject.CreateInstance<DialogueData>();
            testData.entries = new DialogueEntry[]
            {
                new DialogueEntry
                {
                    id = 1,
                    dataType = DialogueDataType.Dialogue,
                    character = "테스트",
                    dialogue = "안녕하세요! 테스트입니다."
                },
                new DialogueEntry
                {
                    id = 2,
                    dataType = DialogueDataType.Dialogue,
                    character = "테스트2",
                    dialogue = "두 번째 대화입니다."
                },
                new DialogueEntry
                {
                    id = 3,
                    dataType = DialogueDataType.Action,
                    actionType = ActionType.Move,
                    dialogue = "이동 액션"
                },
                new DialogueEntry
                {
                    id = 4,
                    dataType = DialogueDataType.Sound,
                    soundPath = "Sounds/test_sound"
                },
                new DialogueEntry
                {
                    id = 5,
                    dataType = DialogueDataType.ImageToon,
                    imagePath = "Images/test_image"
                }
            };

            StoryManager.Instance.AddSceneDialogueData(testData);
            Debug.Log("✓ 테스트 데이터 생성 완료");
        }

        void Update()
        {
            // 키보드 입력으로 테스트
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TestStoryManager();
            }
            
            if (Input.GetKeyDown(KeyCode.P))
            {
                if (StoryManager.Instance != null)
                {
                    StoryManager.Instance.PlayScene(0);
                }
            }
            
            if (Input.GetKeyDown(KeyCode.S))
            {
                if (StoryManager.Instance != null)
                {
                    StoryManager.Instance.StopCurrentScene();
                }
            }
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("StoryManager 테스트");
            
            if (GUILayout.Button("테스트 시작"))
            {
                TestStoryManager();
            }
            
            if (GUILayout.Button("씬 재생"))
            {
                if (StoryManager.Instance != null)
                {
                    StoryManager.Instance.PlayScene(0);
                }
            }
            
            if (GUILayout.Button("정지"))
            {
                if (StoryManager.Instance != null)
                {
                    StoryManager.Instance.StopCurrentScene();
                }
            }
            
            if (StoryManager.Instance != null)
            {
                GUILayout.Label($"재생 상태: {StoryManager.Instance.IsPlaying()}");
                GUILayout.Label($"씬 데이터 수: {StoryManager.Instance.sceneDialogueData.Count}");
            }
            
            GUILayout.EndArea();
        }
    }
}
