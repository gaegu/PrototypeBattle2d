using UnityEngine;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    public class Character : MonoBehaviour
    {
        [Header("Character Info")]
        [SerializeField] private string characterName = "";
        [SerializeField] private string displayName = ""; // 표시용 이름 (다른 언어 지원)
        [SerializeField] private CharacterType characterType = CharacterType.Normal;
        
        [Header("Speech Bubble Settings")]
        [SerializeField] private Vector3 bubbleOffset = new Vector3(0, 2f, 0); // 말풍선 오프셋
        [SerializeField] private bool showSpeechBubble = true; // 말풍선 표시 여부
        
        [Header("Character State")]
        [SerializeField] private bool isSpeaking = false;
        [SerializeField] private bool isVisible = true;
        
        // 프로퍼티
        public string CharacterName => characterName;
        public string DisplayName => !string.IsNullOrEmpty(displayName) ? displayName : characterName;
        public CharacterType CharacterType => characterType;
        public Vector3 BubbleOffset => bubbleOffset;
        public bool ShowSpeechBubble => showSpeechBubble;
        public bool IsSpeaking => isSpeaking;
        public bool IsVisible => isVisible;
        
        private void Awake()
        {
            // characterName이 비어있으면 GameObject 이름 사용
            if (string.IsNullOrEmpty(characterName))
            {
                characterName = gameObject.name;
            }
        }
        
        /// <summary>
        /// 캐릭터가 말하기 시작할 때 호출
        /// </summary>
        public void StartSpeaking()
        {
            isSpeaking = true;
            // 필요한 경우 애니메이션 등 추가
        }
        
        /// <summary>
        /// 캐릭터가 말하기를 멈출 때 호출
        /// </summary>
        public void StopSpeaking()
        {
            isSpeaking = false;
            // 필요한 경우 애니메이션 등 추가
        }
        
        /// <summary>
        /// 캐릭터 표시/숨김 설정
        /// </summary>
        public void SetVisible(bool visible)
        {
            isVisible = visible;
            // 렌더러 컴포넌트들 설정
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = visible;
            }
        }
        
        /// <summary>
        /// 말풍선 오프셋 설정
        /// </summary>
        public void SetBubbleOffset(Vector3 offset)
        {
            bubbleOffset = offset;
        }
        
        /// <summary>
        /// 말풍선 표시 여부 설정
        /// </summary>
        public void SetShowSpeechBubble(bool show)
        {
            showSpeechBubble = show;
        }
        
        // Gizmos로 말풍선 위치 표시 (에디터에서만)
        private void OnDrawGizmosSelected()
        {
            if (showSpeechBubble)
            {
                Gizmos.color = Color.yellow;
                Vector3 bubblePosition = transform.position + bubbleOffset;
                Gizmos.DrawWireSphere(bubblePosition, 0.3f);
                Gizmos.DrawLine(transform.position, bubblePosition);
            }
        }
    }
    
    public enum CharacterType
    {
        Normal,     // 일반 캐릭터
        Player,     // 플레이어 캐릭터
        NPC,        // NPC
        Boss,       // 보스 캐릭터
        Background  // 배경 캐릭터
    }
}
