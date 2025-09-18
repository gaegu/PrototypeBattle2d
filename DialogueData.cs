using System;
using UnityEngine;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    public enum DialogueDataType
    {
        Dialogue,
        Action,
        ImageToon,
        DialogueToon,
        Sound,
        Custom
    }

    public enum ActionType
    {
        Move,
        Emotion,
        Animation
    }

    [System.Serializable]
    public class DialogueEntry
    {
        public int id;
        public DialogueDataType dataType;
        public string character;
        public string dialogue;
        public string imagePath;
        public string soundPath;
        public ActionType actionType;
        public Transform targetTransform; // 이동할 대상
        public string animationName; // 애니메이션 이름
        public string emotionType; // 감정 표현 타입
        public string customJson; // Custom 타입에서 사용할 JSON 데이터
        public float duration = 1f; // 클립 재생 시간 (초)
    }

    [System.Serializable]
    public class DialogueClipData
    {
        public int id;
        public string characterName;
        public string dialogueText;
    }

    [System.Serializable]
    public class ImageToonClipData
    {
        public int id;
        public string imagePath;
        public Sprite imageToonSprite; // 프로젝트 뷰에서 드래그 앤 드롭할 스프라이트
    }

    [System.Serializable]
    public class SoundClipData
    {
        public int id;
        public string soundPath;
    }

    [System.Serializable]
    public class ActionClipData
    {
        public int id;
        public ActionType actionType;
        public Transform targetObject; // 이동할 대상 오브젝트 (Move 타입에서 사용)
        public string animationName; // 애니메이션 이름 (Animation 타입에서 사용)
        public string emotionType; // 감정 표현 타입 (Emotion 타입에서 사용)
        
        /// <summary>
        /// GameObject에서 Transform을 가져옵니다.
        /// </summary>
        public Transform GetTargetTransform()
        {
            return targetObject != null ? targetObject.transform : null;
        }
    }

    [System.Serializable]
    public class CustomClipData
    {
        public int id;
        public string customJson; // JSON 형태의 커스텀 데이터
        public string customType; // 커스텀 액션의 타입 (예: "CameraShake", "ParticleEffect" 등)
    }

    [CreateAssetMenu(fileName = "DialogueData", menuName = "IronJade/Dialogue Data")]
    public class DialogueData : ScriptableObject
    {
        public DialogueEntry[] entries;
    }
}
