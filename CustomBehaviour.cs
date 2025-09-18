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

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            isFirstFrame = true;
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (isFirstFrame)
            {
                targetObject = playerData as GameObject;
                ExecuteCustomAction();
                isFirstFrame = false;
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
            // 필요한 정리 작업
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
