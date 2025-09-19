using UnityEngine;
using UnityEngine.Playables;
using System.Collections.Generic;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    public class CustomBehaviour : PlayableBehaviour
    {
        public CustomClipData customData;
        private bool isFirstFrame = true;
        private GameObject targetObject;
        private bool isWaitingForClick = false;
        private bool hasReceivedClick = false;
        private PlayableDirector director; // Timeline 제어를 위한 참조
        private double pauseTime; // 클릭 대기 시작 시간

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            isFirstFrame = true;
            isWaitingForClick = false;
            hasReceivedClick = false;
            
            // PlayableDirector 참조 가져오기
            if (director == null)
            {
                director = Object.FindObjectOfType<PlayableDirector>();
            }
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (isFirstFrame)
            {
                targetObject = playerData as GameObject;
                ExecuteCustomAction();
                
                isFirstFrame = false;
            }
            
            // 클립이 거의 끝날 때 클릭 대기 시작
            double currentTime = playable.GetTime();
            double duration = playable.GetDuration();
            double progress = currentTime / duration;
            
            // 클립이 99% 진행되었을 때 클릭 대기 시작
            if (customData != null && customData.waitForClick && !isWaitingForClick && progress >= 0.99)
            {
                isWaitingForClick = true;
                pauseTime = currentTime; // 클릭 대기 시작 시간 저장
                var dialogueUI = Object.FindObjectOfType<DialogueUI>();
                if (dialogueUI != null)
                {
                    dialogueUI.SetWaitingForClick(true);
                }
                
                // Timeline 일시정지
                if (director != null)
                {
                    director.Pause();
                }
                
                // ClickDetector를 통해 클릭 감지 시작
                var clickDetector = Object.FindObjectOfType<ClickDetector>();
                if (clickDetector == null)
                {
                    GameObject detectorObj = new GameObject("ClickDetector");
                    clickDetector = detectorObj.AddComponent<ClickDetector>();
                }
                
                clickDetector.StartWaitingForClick(() => {
                    hasReceivedClick = true;
                    isWaitingForClick = false;
                    var dialogueUI = Object.FindObjectOfType<DialogueUI>();
                    if (dialogueUI != null)
                    {
                        dialogueUI.SetWaitingForClick(false);
                    }
                });
            }
        }

        private void ExecuteCustomAction()
        {
            if (string.IsNullOrEmpty(customData.customJson) || string.IsNullOrEmpty(customData.customType))
                return;

            try
            {
                // JSON 파싱 및 커스텀 액션 실행
                var customAction = JsonUtility.FromJson<CustomActionData>(customData.customJson);
                
                switch (customData.customType.ToLower())
                {
                    case "camerashake":
                        ExecuteCameraShake(customAction);
                        break;
                    case "particleeffect":
                        ExecuteParticleEffect(customAction);
                        break;
                    case "screenflash":
                        ExecuteScreenFlash(customAction);
                        break;
                    default:
                        Debug.LogWarning($"알 수 없는 커스텀 타입: {customData.customType}");
                        break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"커스텀 액션 실행 중 오류: {e.Message}");
            }
        }

        private void ExecuteCameraShake(CustomActionData actionData)
        {
            // 카메라 쉐이크 구현 예시
            Debug.Log($"카메라 쉐이크 실행: 강도={actionData.intensity}, 지속시간={actionData.duration}");
        }

        private void ExecuteParticleEffect(CustomActionData actionData)
        {
            // 파티클 이펙트 구현 예시
            Debug.Log($"파티클 이펙트 실행: 타입={actionData.effectType}, 위치={actionData.position}");
        }

        private void ExecuteScreenFlash(CustomActionData actionData)
        {
            // 화면 플래시 구현 예시
            Debug.Log($"화면 플래시 실행: 색상={actionData.color}, 지속시간={actionData.duration}");
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            // 클릭 대기 중이 아닐 때만 UI 상태를 해제
            if (!isWaitingForClick)
            {
                var dialogueUI = Object.FindObjectOfType<DialogueUI>();
                if (dialogueUI != null)
                {
                    dialogueUI.SetWaitingForClick(false);
                }
            }
        }
    }

    // 커스텀 액션 데이터를 위한 예시 클래스
    [System.Serializable]
    public class CustomActionData
    {
        public float intensity;
        public float duration;
        public string effectType;
        public Vector3 position;
        public Color color;
    }
}
