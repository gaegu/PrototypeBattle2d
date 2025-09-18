using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    public class SpeechBubble : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image bubbleBackground;
        [SerializeField] private bool faceCamera = true;
        
        private UnityEngine.Camera mainCamera;
        public RectTransform rectTransform { get; private set; }

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform == null)
                rectTransform = gameObject.AddComponent<RectTransform>();
            
            mainCamera = UnityEngine.Camera.main;
        }

        private void Update()
        {
            if (faceCamera && mainCamera != null)
            {
                // 카메라를 향하도록 회전
                transform.LookAt(mainCamera.transform);
                transform.Rotate(0, 180, 0); // UI가 올바른 방향을 향하도록
            }
        }

        public void SetContent(string characterName, string dialogue)
        {
            // 말풍선은 단순히 말하고 있다는 상태만 표시
            // 텍스트는 표시하지 않음
        }

        // 외부에서 호출하여 말풍선 제거
        public void Remove()
        {
            DestroyImmediate(gameObject);
        }
    }
}
