using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DamageNumbersPro;

public class BattleUIManager : MonoBehaviour
{
    private static BattleUIManager instance;
    public static BattleUIManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<BattleUIManager>();
            }
            return instance;
        }
    }


    [Header("전투 시작/종료 UI")]
    public GameObject startBattlePanel;      // START BATTLE 패널
    public GameObject battleEndPanel;        // Victory/Defeat 패널
    public CustomTextMeshProUGUI battleEndText;    // Victory/Defeat 텍스트




    [Header("전투 UI 패널")]
    public GameObject battleCommandPanel;

    [Header("현재 캐릭터 UI 패널")]
    public GameObject currentAllyPanel;



    [Header("커맨드 버튼들")]
    public Button attackButton;
    public Button skillButton;
    public CustomTextMeshProUGUI skillButtonText; 

    public Button useBPButton;


    [SerializeField] private Button autoBattleButton;
    [SerializeField] private CustomTextMeshProUGUI autoBattleText;
    private bool isAutoEnabled = false;



    [Header("타이머 UI")]
    public CustomTextMeshProUGUI timerText;
    public Slider timerSlider;

    [Header("전투 정보 UI")]
    public CustomTextMeshProUGUI characterNameText;  // 캐릭터 정보 표시
    public CustomTextMeshProUGUI characterHPText;  // 캐릭터 정보 표시
    public CustomTextMeshProUGUI characterMPText;  // 캐릭터 정보 표시
    public CustomTextMeshProUGUI characterBPText;  // 캐릭터 정보 표시

    [Header("전투 진행 정보")]
    public CustomTextMeshProUGUI roundText;           // "Round: 1"
    public CustomTextMeshProUGUI turnText;            // "Turn: 3"



    [Header("컴포넌트들")]
    public Transform rootComponents;
    public GameObject BattleActorTagUI;



    [Header("Damage Display")]
    [SerializeField] private GameObject damageNumberPrefab; // Inspector에서 할당

    [Header("Battle Text Display")]
    [SerializeField] private GameObject battleTextUIPrefab; // Inspector에서 할당


    [Header("===============================")]

    private BattleActor currentActor = null;


    private List<GameObject> turnOrderItems = new List<GameObject>();
    private Action<string> onCommandSelected;
    private GameObject characterHighlight;


    // BP 관련 필드 추가
    private int pendingBPUse = 0;  // 사용 예정인 BP
    private const int MAX_BP_USE = 3;  // 한 번에 사용 가능한 최대 BP

    // BP 이펙트 관련 (선택사항)
    //[Header("BP Effects")]
    // [SerializeField] private GameObject[] bpEffectPrefabs;  // 1, 2, 3 BP 이펙트
    //  private GameObject currentBPEffect;

    [Header("Timeline")]
    public BattleTimelineSystem timelineSystem;  // 새 타임라인



    private void Awake()
    {
        instance = this;

        // 버튼 이벤트 연결
        if (attackButton != null)
            attackButton.onClick.AddListener(() => OnCommandButtonClick("attack"));

        if (skillButton != null)
            skillButton.onClick.AddListener(OnSkillButtonClick);
    
        if (useBPButton != null)
            useBPButton.onClick.AddListener(OnUseBPButtonClick);

        // Auto Battle 버튼 이벤트 연결
        if (autoBattleButton != null)
        {
            autoBattleButton.onClick.AddListener(OnAutoBattleButtonClick);
        }


        // 초기에는 UI 숨김
        HideBattleUI();

        // 시작/종료 UI도 숨김
        if (startBattlePanel != null)
            startBattlePanel.SetActive(false);
        if (battleEndPanel != null)
            battleEndPanel.SetActive(false);
        

    }

    private void Start()
    {

        // 새 타임라인 시스템 초기화 추가
        if (timelineSystem != null)
        {
            var turnSystem = BattleProcessManagerNew.Instance.turnOrderSystem;
            timelineSystem.Initialize(turnSystem);
        }

    }


    // ========== 새로 추가된 메서드 1: START BATTLE 표시 ==========
    public async UniTask ShowStartBattle()
    {
        if ( startBattlePanel == null )
        {
            Debug.LogWarning("[BattleUIManager] StartBattle UI not configured, skipping...");
            await UniTask.Delay(1500); // UI가 없어도 1.5초 대기
            return;
        }

        startBattlePanel.SetActive(true);

        // 1.5초 대기
        await UniTask.Delay(1500);

        startBattlePanel.SetActive(false);
    }

    // ========== 새로 추가된 메서드 2: Victory/Defeat 표시 ==========
    public async UniTask ShowBattleEnd(bool isVictory)
    {
        // 전투 UI 모두 숨기기
        HideBattleUI();

        if (battleEndPanel == null || battleEndText == null)
        {
            Debug.LogWarning("[BattleUIManager] BattleEnd UI not configured");
            return;
        }

        battleEndPanel.SetActive(true);
        battleEndText.text = isVictory ? "VICTORY!" : "DEFEAT...";
        battleEndText.color = isVictory ? Color.yellow : Color.red;

        // 2초 대기
        await UniTask.Delay(2000);

        // 결과 화면 표시
        battleEndPanel.SetActive(false);
    }


    public void UpdateTimer(float remainingTime, float maxTime)
    {
        // 타이머 슬라이더 업데이트
        if (timerSlider != null)
        {
            timerSlider.maxValue = maxTime;
            timerSlider.value = remainingTime;
        }

        // 타이머 텍스트 업데이트
        if (timerText != null)
        {
            timerText.text = $"{remainingTime:F1}s";

            // 시간이 얼마 안 남으면 색상 변경
            if (remainingTime <= 2.0f)
            {
                timerText.color = Color.red;
                // 긴급 효과 (점멸 등)
                float flash = Mathf.Sin(Time.time * 10f) * 0.5f + 0.5f;
                timerText.color = Color.Lerp(Color.red, Color.white, flash);
            }
            else if (remainingTime <= 5.0f)
            {
                timerText.color = Color.yellow;
            }
            else
            {
                timerText.color = Color.white;
            }
        }
    }

    public void ShowBattleUI(Action<string> commandCallback, BattleActor currentActor = null)
    {
        onCommandSelected = commandCallback;
        this.currentActor = currentActor;  // 현재 액터 저장
                                           // 캐릭터 정보 업데이트
        if (currentActor != null)
        {
            UpdateCharacterInfo(currentActor);
            UpdateButtonStates(currentActor);
            UpdateBPButton(currentActor);  // BP 버튼 업데이트 추가
        }


        if (battleCommandPanel != null)
        {
            battleCommandPanel.SetActive(true);
        }


        if (currentAllyPanel != null)
        {
            currentAllyPanel.SetActive(true);
        }



        // 타이머 초기화
        if (timerSlider != null)
        {
            timerSlider.maxValue = 10f;
            timerSlider.value = 10f;
        }

        if (timerText != null)
        {
            timerText.text = "10.0s";
            timerText.color = Color.white;
        }

    }

    public void HideBattleUI()
    {
        if (battleCommandPanel != null)
        {
            battleCommandPanel.SetActive(false);
        }


        if (currentAllyPanel != null)
        {
            currentAllyPanel.SetActive(false);
        }


        // 콜백 초기화
        onCommandSelected = null;
    }


    /// <summary>
    /// 데미지 숫자 표시 메서드 추가
    /// </summary>
    public void ShowDamageNumber(BattleActor owner, int damage, bool isCritical = false)
    {
        if (damageNumberPrefab == null)
        {
            Debug.LogWarning("[BattleUIManager] DamageNumber prefab is not set!");
            return;
        }

        GameObject damageObj = Instantiate(damageNumberPrefab);
        BattleDamageNumberUI damageUI = damageObj.GetComponent<BattleDamageNumberUI>();

        // rootComponents를 부모로 설정 (Canvas 하위여야 함)
        damageObj.transform.SetParent(rootComponents, false); // false가 중요! - 로컬 스케일 유지

        damageUI.Set(owner, damage);

        if (isCritical)
        {
            damageUI.SetDamageType("critical");
        }

    //    Debug.Log($"[DamageUI] World: {worldPosition}, Screen: {screenPosition}, UI Local: {localPoint}");
    }



    public void ShowBattleText(BattleActor owner, BattleTextType textType )
    {
        if (battleTextUIPrefab == null)
        {
            Debug.LogWarning("[BattleUIManager] battleTextUIPrefab prefab is not set!");
            return;
        }


        GameObject textUIObj = Instantiate(battleTextUIPrefab);
        BattleTextUI textUI = textUIObj.GetComponent<BattleTextUI>();

        // rootComponents를 부모로 설정 (Canvas 하위여야 함)
        textUIObj.transform.SetParent(rootComponents, false); // false가 중요! - 로컬 스케일 유지

        textUI.Set(owner, textType);

    }



    private void OnCommandButtonClick(string command)
    {
        Debug.Log($"Command Button Clicked: {command}");

        // BP 사용 처리
        if (pendingBPUse > 0 && (command == "attack" || command == "skill"))
        {
            if (currentActor.BattleActorInfo.UseBP(pendingBPUse))
            {
                // BP를 사용한 커맨드로 변경
                command = $"{command}_bp_{pendingBPUse}";
            }
        }

        // 애니메이션 효과
        PlayButtonClickAnimation();

        onCommandSelected?.Invoke(command);
        onCommandSelected = null;

        // BP 초기화
        pendingBPUse = 0;
       // ClearBPEffect();


        HideBattleUI();
    }


    private void OnSkillButtonClick()
    {

        // 스킬 패널이 없으면 기본 스킬 실행
        OnCommandButtonClick("skill");
     
    }


    private async void OnUseBPButtonClick()
    {

        if (currentActor?.BattleActorInfo == null) return;

        // BP 사용 가능 확인
        if (currentActor.BattleActorInfo.Bp > pendingBPUse && pendingBPUse < MAX_BP_USE)
        {
            pendingBPUse++;

            await currentActor.ShowBPChargeEffect(pendingBPUse);

            UpdateBPButton(currentActor);
            Debug.Log($"[BP] Pending BP use: {pendingBPUse}");
        }
    }

    private void OnAutoBattleButtonClick()
    {
        isAutoEnabled = !isAutoEnabled;

        // BattleProcessManagerNew에 전달
        BattleProcessManagerNew.Instance.EnableAutoBattle(isAutoEnabled);


        // UI 업데이트
        if (autoBattleText != null)
        {
            autoBattleText.text = isAutoEnabled ? "Auto ON" : "Auto OFF";
            autoBattleText.color = isAutoEnabled ? Color.green : Color.white;
        }

        Debug.Log($"Auto Battle: {(isAutoEnabled ? "Enabled" : "Disabled")}");
    }


    /// <summary>
    /// 라운드와 턴 정보 업데이트
    /// </summary>
    public void UpdateBattleInfo(int currentRound, int currentTurn, (int current, int total) roundProgress)
    {
        // 라운드 표시
        if (roundText != null)
        {
            roundText.text = $"Round: {currentRound}";
        }

        // 턴 표시
        if (turnText != null)
        {
            turnText.text = $"Turn: {currentTurn}";
        }
    }


    /// <summary>
    /// 현재 라운드의 턴 순서 표시 (타임라인 형태)
    /// </summary>
    public void UpdateTurnTimeline(
        TurnOrderSystem.TurnOrderInfo currentTurn,
        List<TurnOrderSystem.TurnOrderInfo> remainingTurns,
        List<TurnOrderSystem.TurnOrderInfo> nextRoundPreview)
    {

        if (timelineSystem != null)
        {
            timelineSystem.UpdateTimeline(currentTurn, remainingTurns, nextRoundPreview);
        }

    }





    /// <summary>
    /// 현재 행동 중인 캐릭터 강조
    /// </summary>
    public void HighlightCurrentTurn(BattleActor actor)
    {
        if (actor == null) return;

        // 캐릭터 위에 화살표 또는 하이라이트 효과 표시
        ShowCharacterHighlight(actor.transform.position);

        // 첫 번째 턴 순서 아이템 강조
        if (turnOrderItems.Count > 0)
        {
            var firstItem = turnOrderItems[0];

            // 펄스 애니메이션
            StartCoroutine(PulseAnimation(firstItem));
        }
    }

    /// <summary>
    /// 캐릭터 하이라이트 표시
    /// </summary>
    private void ShowCharacterHighlight(Vector3 worldPosition)
    {
        // 월드 좌표를 스크린 좌표로 변환
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition + Vector3.up * 2f);

        // 하이라이트 UI 생성 또는 이동
        if (characterHighlight == null)
        {
            characterHighlight = new GameObject("CharacterHighlight");
            characterHighlight.transform.SetParent(transform, false);

            Image arrow = characterHighlight.AddComponent<Image>();
            arrow.sprite = null; // 화살표 스프라이트 설정
            arrow.color = Color.yellow;

            RectTransform rect = characterHighlight.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(50, 50);
        }

        characterHighlight.transform.position = screenPos;
        characterHighlight.SetActive(true);

        // 위아래 움직임 애니메이션
        StartCoroutine(BounceAnimation(characterHighlight));
    }

    /// <summary>
    /// 턴 스피드 변화 표시
    /// </summary>
    public void ShowSpeedChange(BattleActor actor, float multiplier)
    {
        Vector3 worldPos = actor.transform.position + Vector3.up;
        string text = multiplier > 1f ? $"Speed UP! x{multiplier:F1}" : $"Speed DOWN! x{multiplier:F1}";
        Color color = multiplier > 1f ? Color.green : Color.red;

        ShowFloatingText(worldPos, text, color);
    }

    /// <summary>
    /// 속성 상성 표시
    /// </summary>
    public void ShowElementAdvantage(BattleActor attacker, BattleActor target)
    {
        if (attacker == null || target == null) return;

        float multiplier = BattleElementHelper.GetElementMultiplier(
            attacker.BattleActorInfo.Element,
            target.BattleActorInfo.Element
        );

        Color relationColor = BattleElementHelper.GetElementRelationColor(
            attacker.BattleActorInfo.Element,
            target.BattleActorInfo.Element
        );

        // UI 텍스트 업데이트
        /*if (characterInfoText != null)
        {
            string elementInfo = $"\nElement: {attacker.BattleActorInfo.Element}";
            if (multiplier != 1.0f)
            {
                elementInfo += $"\n<color=#{ColorUtility.ToHtmlStringRGB(relationColor)}>vs {target.BattleActorInfo.Element}: x{multiplier:F2}</color>";
            }

            characterInfoText.text += elementInfo;
        }*/

    }

    private void UpdateCharacterInfo(BattleActor actor)
    {
        if ( actor == null) return;

        var info = actor.BattleActorInfo;
        if (info == null) return;


        characterNameText.text = info.Name;
        characterHPText.text = $"HP: {info.Hp}/{info.MaxHp}";
        characterMPText.text = "";
       // characterMPText.text = $"MP: {info.Mp}/{info.MaxMp}";
    }



    private void UpdateButtonStates(BattleActor actor)
    {
        if (actor == null) return;

        var info = actor.BattleActorInfo;
        if (info == null) return;

        // MP가 부족하면 스킬 버튼 비활성화
        if ( actor.SkillManager.GetActiveSkillCount() > 0 )
        {
            skillButton.gameObject.SetActive(true);

            AdvancedSkillRuntime activeSkill = actor.GetAvailableActiveSkill();

            if (activeSkill == null) return;


            bool canUseSkill = actor.CooldownManager.CanUseSkill(activeSkill.SkillID) && !actor.IsSilenced;
            skillButton.interactable = canUseSkill;

            skillButtonText.text = activeSkill.SkillName;

            if ( canUseSkill == false )
            {
                skillButtonText.text += "- Remain Turn : " + actor.CooldownManager.GetRemainingCooldown(activeSkill.SkillID).ToString();
            }

            // 비활성화된 버튼 시각적 표시
            var buttonImage = skillButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = skillButton.interactable ? Color.white : Color.gray;
            }
        }
        else
        {
            skillButton.gameObject.SetActive(false);
        }

    }


    // BP 버튼 업데이트 메서드 추가
    public void UpdateBPButton(BattleActor actor)
    {
        if (useBPButton == null || actor?.BattleActorInfo == null) return;

        // BP가 1 이상이면 버튼 활성화
        bool hasAvailableBP = actor.BattleActorInfo.Bp > pendingBPUse;
        bool canUseBP = hasAvailableBP && pendingBPUse < MAX_BP_USE;

        useBPButton.gameObject.SetActive(hasAvailableBP);
        useBPButton.GetComponent<Button>().interactable = canUseBP;

        characterBPText.text = $"BP: {actor.BattleActorInfo.Bp- pendingBPUse}/{actor.BattleActorInfo.MaxBp}";

    }



    private void PlayButtonClickAnimation()
    {
        // 버튼 클릭 애니메이션 또는 사운드 재생
        // TODO: 구현
    }

    // 특수 효과 메서드들
    public void ShowDamageText(Vector3 worldPosition, int damage, bool isCritical = false)
    {
        string text = isCritical ? $"CRITICAL!\n{damage}" : damage.ToString();
        Color color = isCritical ? Color.yellow : Color.white;
        ShowFloatingText(worldPosition, text, color);
    }

    public void ShowEffectText(Vector3 worldPosition, string effect)
    {
        ShowFloatingText(worldPosition, effect, Color.cyan);
    }

    /// <summary>
    /// 플로팅 텍스트 표시
    /// </summary>
    private void ShowFloatingText(Vector3 worldPosition, string text, Color color)
    {
        GameObject floatingText = new GameObject("FloatingText");
        floatingText.transform.SetParent(transform, false);

        Text textComponent = floatingText.AddComponent<Text>();
        textComponent.text = text;
        textComponent.fontSize = 24;
        textComponent.color = color;
        textComponent.alignment = TextAnchor.MiddleCenter;
        textComponent.font = Font.CreateDynamicFontFromOSFont("Arial", 24);

        RectTransform rect = floatingText.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200, 50);

        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);
        floatingText.transform.position = screenPos;

        // 위로 올라가면서 페이드 아웃
        StartCoroutine(FloatAndFade(floatingText));
    }

    /// <summary>
    /// 펄스 애니메이션
    /// </summary>
    private IEnumerator PulseAnimation(GameObject target)
    {
        if (target == null) yield break;

        float time = 0;
        Vector3 originalScale = target.transform.localScale;

        while (target != null && target.activeInHierarchy)
        {
            time += Time.deltaTime;
            float scale = 1f + Mathf.Sin(time * 4f) * 0.1f;
            target.transform.localScale = originalScale * scale;
            yield return null;
        }
    }

    /// <summary>
    /// 바운스 애니메이션
    /// </summary>
    private IEnumerator BounceAnimation(GameObject target)
    {
        if (target == null) yield break;

        float time = 0;
        Vector3 originalPos = target.transform.position;

        while (target != null && target.activeInHierarchy)
        {
            time += Time.deltaTime;
            float offset = Mathf.Sin(time * 3f) * 10f;
            target.transform.position = originalPos + Vector3.up * offset;
            yield return null;
        }
    }

    /// <summary>
    /// 플로팅 애니메이션
    /// </summary>
    private IEnumerator FloatAndFade(GameObject floatingText)
    {
        if (floatingText == null) yield break;

        Text text = floatingText.GetComponent<Text>();
        Vector3 startPos = floatingText.transform.position;
        float timer = 0;

        while (timer < 1.5f)
        {
            timer += Time.deltaTime;

            // 위로 이동
            floatingText.transform.position = startPos + Vector3.up * (timer * 50f);

            // 페이드 아웃
            if (text != null)
            {
                Color c = text.color;
                c.a = 1f - (timer / 1.5f);
                text.color = c;
            }

            yield return null;
        }

        Destroy(floatingText);
    }

    // 전투 종료 UI
    public void ShowBattleResult(bool isVictory)
    {
        // 전투 결과 표시
        // TODO: 구현
        Debug.Log($"Battle Result: {(isVictory ? "Victory!" : "Defeat...")}");
    }
}