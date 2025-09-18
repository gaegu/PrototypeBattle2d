using UnityEngine;
using TMPro;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
	public class DialoguePanelUI : MonoBehaviour
	{
		[Header("UI Elements")]
		[SerializeField] private GameObject panelRoot;
		[SerializeField] private TextMeshProUGUI dialogueText;
		[SerializeField] private TextMeshProUGUI nameText;
		
		[Header("Character Speech Bubble")]
		[SerializeField] private GameObject speechBubblePrefab; // 말풍선 프리팹
		[SerializeField] private Vector3 bubbleOffset = new Vector3(0, 2f, 0); // 캐릭터 위 오프셋
		[SerializeField] private float bubbleDuration = 3f; // 말풍선 지속 시간
		[SerializeField] private ImageToonUI imageToonUI; // 이미지툰 UI 참조
		[SerializeField] private DialogueUI dialogueUI; // DialogueUI 참조 (speechBubbleParent 접근용)
		
		private GameObject currentBubble;
		private UnityEngine.Camera mainCamera;
		private Canvas worldCanvas; // 월드 스페이스 캔버스

		private void Awake()
		{
			mainCamera = UnityEngine.Camera.main;
			worldCanvas = FindObjectOfType<Canvas>();
			if (worldCanvas == null)
			{
				Debug.LogWarning("월드 스페이스 캔버스를 찾을 수 없습니다. 말풍선 기능이 제한됩니다.");
			}
		}

		public void Show()
		{
			if (panelRoot != null) panelRoot.SetActive(true);
		}

		public void Hide()
		{
			if (panelRoot != null) panelRoot.SetActive(false);
			
			// 말풍선도 함께 제거
			if (currentBubble != null)
			{
				SpeechBubble bubbleComponent = currentBubble.GetComponent<SpeechBubble>();
				if (bubbleComponent != null)
				{
					bubbleComponent.Remove();
				}
				else
				{
					Destroy(currentBubble);
				}
				currentBubble = null;
			}
		}
		
		/// <summary>
		/// 특정 캐릭터의 말하기 상태를 중지
		/// </summary>
		public void StopCharacterSpeaking(string characterName)
		{
			GameObject character = FindCharacterByName(characterName);
			if (character != null)
			{
				Character characterComponent = character.GetComponent<Character>();
				if (characterComponent != null)
				{
					characterComponent.StopSpeaking();
				}
			}
		}
		
		/// <summary>
		/// 말풍선만 숨기기 (이미지툰 표시 시 사용)
		/// </summary>
		public void HideSpeechBubble()
		{
			if (currentBubble != null)
			{
				SpeechBubble bubbleComponent = currentBubble.GetComponent<SpeechBubble>();
				if (bubbleComponent != null)
				{
					bubbleComponent.Remove();
				}
				else
				{
					Destroy(currentBubble);
				}
				currentBubble = null;
			}
		}

		public void SetDialogue(string characterName, string dialogue)
		{
			if (nameText != null) nameText.text = characterName;
			if (dialogueText != null) dialogueText.text = dialogue;
			
			// 이미지툰이 표시 중이면 말풍선만 표시하지 않음 (대사창은 표시)
			if (imageToonUI != null && imageToonUI.gameObject.activeInHierarchy)
			{
				Debug.Log("이미지툰이 표시 중이므로 말풍선만 표시하지 않습니다.");
				return;
			}
			
			// 캐릭터 오브젝트를 찾아서 말풍선 표시
			ShowSpeechBubble(characterName, dialogue);
		}
		
		private void ShowSpeechBubble(string characterName, string dialogue)
		{
			// 이미지툰이 표시되고 있거나 곧 표시될 예정이면 말풍선을 표시하지 않음
			if (imageToonUI != null && (imageToonUI.gameObject.activeInHierarchy || IsImageToonScheduled()))
			{
				Debug.Log("이미지툰이 표시 중이거나 예정되어 있으므로 말풍선을 표시하지 않습니다.");
				return;
			}
			
			// 기존 말풍선 제거
			if (currentBubble != null)
			{
				DestroyImmediate(currentBubble);
			}
			
			// 프리팹 체크
			if (speechBubblePrefab == null)
			{
				Debug.LogError("SpeechBubble 프리팹이 설정되지 않았습니다!");
				return;
			}
			
			// 캐릭터 오브젝트 찾기
			GameObject character = FindCharacterByName(characterName);
			if (character == null)
			{
				Debug.LogError($"캐릭터 '{characterName}'를 찾을 수 없습니다!");
				return;
			}
			
			// Character 컴포넌트에서 개별 오프셋 가져오기
			Vector3 bubbleOffset = this.bubbleOffset; // 기본값
			Character characterComponent = character.GetComponent<Character>();
			if (characterComponent != null && characterComponent.ShowSpeechBubble)
			{
				bubbleOffset = characterComponent.BubbleOffset;
				characterComponent.StartSpeaking(); // 말하기 시작 상태로 설정
			}
			
			// SpeechBubble 부모 오브젝트 찾기
			Transform bubbleParent = null;
			if (dialogueUI != null && dialogueUI.speechBubbleParent != null)
			{
				bubbleParent = dialogueUI.speechBubbleParent;
			}
			else
			{
				// DialogueUI의 speechBubbleParent가 없으면 기존 방식 사용
				Canvas targetCanvas = worldCanvas;
				if (targetCanvas == null)
				{
					targetCanvas = FindObjectOfType<Canvas>();
				}
				
				if (targetCanvas == null)
				{
					Debug.LogError("캔버스를 찾을 수 없습니다. 말풍선을 생성할 수 없습니다.");
					return;
				}
				bubbleParent = targetCanvas.transform;
			}
			
			// 말풍선을 지정된 부모의 자식으로 생성
			currentBubble = Instantiate(speechBubblePrefab, bubbleParent);
			
			// 말풍선 설정 (단순히 말하고 있다는 상태만 표시)
			SpeechBubble bubbleComponent = currentBubble.GetComponent<SpeechBubble>();
			if (bubbleComponent == null)
			{
				Debug.LogError("SpeechBubble 컴포넌트를 찾을 수 없습니다. 프리팹에 SpeechBubble 스크립트가 추가되어 있는지 확인하세요!");
				return;
			}
			
			// 말풍선은 단순히 말하고 있다는 상태만 표시
			bubbleComponent.SetContent(characterName, dialogue);
			
			// 말풍선 위치를 캐릭터 위로 설정 (원래 방식)
			if (mainCamera == null)
			{
				return;
			}
			
			// 캐릭터의 월드 위치를 스크린 좌표로 변환
			Vector3 worldPosition = character.transform.position + bubbleOffset;
			Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);
			
			// Canvas 렌더 모드에 따라 위치 설정
			Canvas canvasForPosition = worldCanvas;
			if (canvasForPosition == null)
			{
				canvasForPosition = FindObjectOfType<Canvas>();
			}
			
			if (canvasForPosition == null)
			{
				Debug.LogError("캔버스를 찾을 수 없습니다. 말풍선을 생성할 수 없습니다.");
				return;
			}
			
			// Canvas 렌더 모드에 따라 위치 설정
			if (bubbleComponent.rectTransform != null)
			{
				switch (canvasForPosition.renderMode)
				{
					case RenderMode.ScreenSpaceOverlay:
						// Screen Space - Overlay: 스크린 좌표 직접 사용
						bubbleComponent.rectTransform.position = screenPosition;
						break;
						
					case RenderMode.ScreenSpaceCamera:
						// Screen Space - Camera: 스크린 좌표를 캔버스 좌표로 변환
						Vector2 canvasPosition;
						if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
							canvasForPosition.transform as RectTransform,
							screenPosition,
							canvasForPosition.worldCamera,
							out canvasPosition))
						{
							bubbleComponent.rectTransform.anchoredPosition = canvasPosition;
						}
						else
						{
							Debug.LogWarning("ScreenSpaceCamera: 좌표 변환 실패");
						}
						break;
						
					case RenderMode.WorldSpace:
						// World Space: 월드 좌표 직접 사용
						bubbleComponent.transform.position = worldPosition;
						break;
				}
			}
			else
			{
				Debug.LogWarning("SpeechBubble의 RectTransform이 null입니다.");
			}
			
			// 일정 시간 후 자동 제거 - 오브젝트가 활성화되어 있을 때만 코루틴 시작
			if (gameObject.activeInHierarchy)
			{
				StartCoroutine(RemoveBubbleAfterDelay(bubbleDuration));
			}
			else
			{
				// 오브젝트가 비활성화되어 있으면 Invoke 사용
				Invoke(nameof(RemoveBubble), bubbleDuration);
			}
		}
		
		private GameObject FindCharacterByName(string characterName)
		{
			// 씬에서 캐릭터 이름으로 오브젝트 찾기
			// 여러 방법으로 시도
			
			// 1. 정확한 이름으로 찾기
			GameObject character = GameObject.Find(characterName);
			if (character != null)
			{
				return character;
			}
			
			// 2. 태그로 찾기 (Character 태그가 있다면)
			GameObject[] characters = GameObject.FindGameObjectsWithTag("Character");
			foreach (GameObject charObj in characters)
			{
				if (charObj.name.Contains(characterName) || charObj.name.ToLower().Contains(characterName.ToLower()))
				{
					return charObj;
				}
			}
			
			// 3. Character 컴포넌트가 있는 오브젝트에서 찾기
			Character[] characterComponents = FindObjectsOfType<Character>();
			foreach (Character charComp in characterComponents)
			{
				if (charComp.CharacterName == characterName || charComp.name.Contains(characterName))
				{
					return charComp.gameObject;
				}
			}
			
			return null;
		}
		
		private System.Collections.IEnumerator RemoveBubbleAfterDelay(float delay)
		{
			yield return new WaitForSeconds(delay);
			RemoveBubble();
		}
		
		private void RemoveBubble()
		{
			if (currentBubble != null)
			{
				Destroy(currentBubble);
				currentBubble = null;
			}
		}
		
		/// <summary>
		/// 이미지툰이 곧 표시될 예정인지 확인 (Timeline에서 동시에 실행되는 경우 대비)
		/// </summary>
		private bool IsImageToonScheduled()
		{
			// Timeline에서 ImageToonBehaviour가 실행 중인지 확인
			// 이는 Timeline의 동시 실행으로 인한 타이밍 문제를 해결하기 위함
			return false; // 현재는 단순히 false 반환, 필요시 Timeline 상태 확인 로직 추가 가능
		}
	}
}


