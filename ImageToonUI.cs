using UnityEngine;
using UnityEngine.UI;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
	public class ImageToonUI : MonoBehaviour
	{
			[Header("Image Settings")]
	[SerializeField] private Image image;
	[SerializeField] private bool maintainAspectRatio = true; // 스프라이트 비율 유지 여부
		
		[Header("References")]
		[SerializeField] private DialoguePanelUI dialoguePanelUI; // 말풍선 제거를 위한 참조

		private void Awake()
		{
			Hide();
		}

		public void SetSprite(Sprite sprite)
		{
			if (image != null)
			{
				image.sprite = sprite;
				
				// 스프라이트의 원본 비율을 유지하도록 이미지 크기 조정
				if (sprite != null)
				{
					AdjustImageSizeToSpriteAspectRatio(sprite);
				}
			}
		}
		
		/// <summary>
		/// 스프라이트의 원본 비율에 맞게 이미지 크기를 조정합니다.
		/// </summary>
		private void AdjustImageSizeToSpriteAspectRatio(Sprite sprite)
		{
			if (image == null || sprite == null || !maintainAspectRatio) return;
			
			RectTransform imageRect = image.rectTransform;
			if (imageRect == null) return;
			
			// 스프라이트의 원본 크기
			Vector2 spriteSize = sprite.rect.size;
			float spriteAspectRatio = spriteSize.x / spriteSize.y;
			
			// 현재 이미지의 크기
			Vector2 currentSize = imageRect.sizeDelta;
			float currentAspectRatio = currentSize.x / currentSize.y;
			
			// 비율이 다르면 조정
			if (Mathf.Abs(spriteAspectRatio - currentAspectRatio) > 0.01f)
			{
				Vector2 newSize = currentSize;
				
				if (spriteAspectRatio > currentAspectRatio)
				{
					// 스프라이트가 더 넓음 - 높이를 줄임
					newSize.y = currentSize.x / spriteAspectRatio;
				}
				else
				{
					// 스프라이트가 더 높음 - 너비를 줄임
					newSize.x = currentSize.y * spriteAspectRatio;
				}
				
				imageRect.sizeDelta = newSize;
				Debug.Log($"이미지툰 크기 조정: {currentSize} → {newSize} (비율: {spriteAspectRatio:F2})");
			}
		}

		public void LoadFromPath(string imagePath)
		{
			// Addressable 연동 리소스 위치 확정되면
			Debug.Log($"ImageToonUI LoadFromPath: {imagePath}");
		}

		public void Show()
		{
			// 이미지툰이 표시될 때 먼저 말풍선 제거 (활성화 전에)
			if (dialoguePanelUI != null)
			{
				dialoguePanelUI.HideSpeechBubble();
			}
			
			// speechBubbleParent의 모든 자식 말풍선도 제거
			RemoveAllSpeechBubbles();
			
			gameObject.SetActive(true);
			
			// 활성화 후에도 한 번 더 확인하여 말풍선 제거
			if (dialoguePanelUI != null)
			{
				dialoguePanelUI.HideSpeechBubble();
			}
			
			Debug.Log("이미지툰이 표시되었습니다. 기존 말풍선을 제거했습니다.");
		}
		
		private void RemoveAllSpeechBubbles()
		{
			// DialogueUI의 speechBubbleParent에서 모든 SpeechBubble 제거
			if (dialoguePanelUI != null)
			{
				// DialogueUI 참조를 통해 speechBubbleParent 접근
				var dialogueUI = dialoguePanelUI.GetComponent<DialogueUI>();
				if (dialogueUI != null && dialogueUI.speechBubbleParent != null)
				{
					// speechBubbleParent의 모든 자식 중 SpeechBubble 컴포넌트가 있는 것들 제거
					for (int i = dialogueUI.speechBubbleParent.childCount - 1; i >= 0; i--)
					{
						Transform child = dialogueUI.speechBubbleParent.GetChild(i);
						if (child.GetComponent<SpeechBubble>() != null)
						{
							DestroyImmediate(child.gameObject);
						}
					}
				}
			}
		}

		public void Hide()
		{
			gameObject.SetActive(false);
		}
	}
}


