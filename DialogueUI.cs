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
            if (dialoguePanelUI != null) dialoguePanelUI.Hide();
        }

        public void SetDialogue(string characterName, string dialogue)
        {
            if (dialoguePanelUI != null) dialoguePanelUI.SetDialogue(characterName, dialogue);
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
