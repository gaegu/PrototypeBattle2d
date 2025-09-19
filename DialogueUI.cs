using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    public class DialogueUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] public DialoguePanelUI dialoguePanelUI;
        [SerializeField] public ImageToonUI imageToonUI;
        [SerializeField] public Transform speechBubbleParent; // SpeechBubble의 부모 오브젝트
        
        [Header("클릭 대기 UI")]
        [SerializeField] private GameObject clickIndicator; // 클릭 대기 표시 UI (예: 화살표, 깜빡이는 텍스트 등)
        
        private bool isWaitingForClick = false;
        private bool forceShowUI = false; // 클릭 대기 중일 때 UI 강제 표시

        private void Awake()
        {
            // 초기 숨김
            HideDialogue();
            HideImageToon();
        }

        #region 대화 UI
        public void ShowDialogue()
        {
            if (dialoguePanelUI != null) dialoguePanelUI.Show();
        }

        public void HideDialogue()
        {
            // 클릭 대기 중이면 UI를 숨기지 않음
            if (isWaitingForClick && forceShowUI)
            {
                return;
            }
            
            if (dialoguePanelUI != null) dialoguePanelUI.Hide();
        }

        public void SetDialogue(string characterName, string dialogue)
        {
            if (dialoguePanelUI != null) dialoguePanelUI.SetDialogue(characterName, dialogue);
        }
        
        /// <summary>
        /// 클릭 대기 상태 설정
        /// </summary>
        public void SetWaitingForClick(bool waiting)
        {
            isWaitingForClick = waiting;
            forceShowUI = waiting; // 클릭 대기 중일 때 UI 강제 표시
            
            if (clickIndicator != null)
            {
                clickIndicator.SetActive(waiting);
            }
            
            // DialoguePanelUI에도 클릭 대기 상태 전달
            if (dialoguePanelUI != null)
            {
                dialoguePanelUI.SetWaitingForClick(waiting);
            }
        }
        
        /// <summary>
        /// 클릭 대기 중인지 확인
        /// </summary>
        public bool IsWaitingForClick()
        {
            return isWaitingForClick;
        }
        #endregion

        #region 이미지툰 UI
        public void SetImageToon(string imagePath)
        {
            if (!string.IsNullOrEmpty(imagePath))
            {
                LoadImageToon(imagePath);
            }
        }

        public void SetImageToon(Sprite sprite)
        {
            if (imageToonUI != null && sprite != null)
            {
                imageToonUI.SetSprite(sprite);
                Debug.Log($"이미지툰 스프라이트 설정: {sprite.name}");
            }
        }

        public void SetImageToon()
        {
            // 이미지툰 설정 (추후 확장)
        }

        public void ShowImageToon()
        {
            if (imageToonUI != null) imageToonUI.Show();
        }

        public void HideImageToon()
        {
            if (imageToonUI != null) imageToonUI.Hide();
        }
        #endregion

        #region 유틸리티
        private async void LoadImageToon(string imagePath)
        {
            // Sprite sprite = await UtilModel.Resources.LoadAsync<Sprite>(imagePath);
            // if (sprite != null && imageToonUI != null)
            // {
            //     imageToonUI.SetSprite(sprite);
            // }
            
            Debug.Log($"이미지툰 로드: {imagePath}");
            if (imageToonUI != null)
            {
                imageToonUI.LoadFromPath(imagePath);
            }
        }       
        #endregion
    }
}
