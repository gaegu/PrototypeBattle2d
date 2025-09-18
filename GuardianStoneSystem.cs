// ===== 1. GuardianStoneSystem.cs - 새 파일 =====
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GuardianStoneSystem
{
    // GuardianStone 상태
    public enum StoneState
    {
        Closed,     // 닫힌 상태 (속성 숨김)
        Open,       // 열린 상태 (속성 보임)
        Broken      // 깨진 상태
    }

    // 개별 GuardianStone 정보
    public class GuardianStone
    {
        public EBattleElementType Element { get; set; }
        public StoneState State { get; set; }
        public int Index { get; set; }

        public GuardianStone(EBattleElementType element, int index)
        {
            Element = element;
            State = StoneState.Closed;
            Index = index;
        }
    }

    private BattleActor owner;
    private List<GuardianStone> stones = new List<GuardianStone>();
    private bool isBreakState = false;
    private int breakTurnsRemaining = 0;
    private BattleActorTagUI tagUI;

    public bool IsBreakState => isBreakState;
    public int BreakTurnsRemaining => breakTurnsRemaining;
    public List<GuardianStone> Stones => stones;

    public void Initialize(BattleActor actor, BattleCharInfoNew charInfo, BattleActorTagUI ui)
    {
        owner = actor;
        tagUI = ui;

        if (charInfo.GuardianStoneElement != null && charInfo.GuardianStoneElement.Length > 0)
        {
            for (int i = 0; i < charInfo.GuardianStoneElement.Length && i < 10; i++)
            {
                if (charInfo.GuardianStoneElement[i] != EBattleElementType.None)
                {
                    stones.Add(new GuardianStone(charInfo.GuardianStoneElement[i], i));
                }
            }
        }

        UpdateUI();
    }

    public bool TryBreakStone(EBattleElementType attackerElement)
    {
        if (isBreakState || stones.Count == 0)
            return false;

        var targetStone = stones.FirstOrDefault(s =>
            s.Element == attackerElement && s.State != StoneState.Broken);

        if (targetStone != null)
        {
            targetStone.State = StoneState.Broken;
            Debug.Log($"[GuardianStone] Stone broken! Element: {attackerElement}");

            if (stones.All(s => s.State == StoneState.Broken))
            {
                EnterBreakState();
            }

            UpdateUI();
            return true;
        }

        return false;
    }

    private void EnterBreakState()
    {
        isBreakState = true;
        breakTurnsRemaining = 7;
        Debug.Log($"[GuardianStone] {owner.name} entered BREAK state! (7 turns)");
        UpdateUI();
    }

    public void ProcessTurn()
    {
        if (isBreakState && breakTurnsRemaining > 0)
        {
            breakTurnsRemaining--;

            if (breakTurnsRemaining <= 0)
            {
                ExitBreakState();
            }

            UpdateUI();
        }
    }

    private void ExitBreakState()
    {
        isBreakState = false;
        breakTurnsRemaining = 0;

        foreach (var stone in stones)
        {
            stone.State = StoneState.Open;
        }

        Debug.Log($"[GuardianStone] {owner.name} recovered from BREAK state!");
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (tagUI == null) return;

        if (stones.Count == 0)
        {
            if (tagUI.panelGuardianStone != null)
                tagUI.panelGuardianStone.SetActive(false);
            if (tagUI.textBreak != null)
                tagUI.textBreak.gameObject.SetActive(false);
            return;
        }

        if (isBreakState)
        {
            if (tagUI.panelGuardianStone != null)
                tagUI.panelGuardianStone.SetActive(false);

            if (tagUI.textBreak != null)
            {
                tagUI.textBreak.gameObject.SetActive(true);
                tagUI.textBreak.text = $"BREAK ({breakTurnsRemaining})";
            }
        }
        else
        {
            if (tagUI.panelGuardianStone != null)
                tagUI.panelGuardianStone.SetActive(true);
            if (tagUI.textBreak != null)
                tagUI.textBreak.gameObject.SetActive(false);

            UpdateStoneIcons();
        }
    }

    private void UpdateStoneIcons()
    {
        if (tagUI.guardianStoneElementIcon == null) return;

        for (int i = 0; i < tagUI.guardianStoneElementIcon.Length; i++)
        {
            if (i < stones.Count)
            {
                tagUI.guardianStoneElementIcon[i].gameObject.SetActive(true);

                var stone = stones[i];

                if (stone.State == StoneState.Closed)
                {
                    tagUI.guardianStoneElementIcon[i].sprite = tagUI.closedGuardianStoneIcon;
                }
                else if (stone.State == StoneState.Open || stone.State == StoneState.Broken)
                {
                    tagUI.guardianStoneElementIcon[i].sprite = GetElementIcon(stone.Element);

                    var color = tagUI.guardianStoneElementIcon[i].color;
                    color.a = stone.State == StoneState.Broken ? 0.3f : 1f;
                    tagUI.guardianStoneElementIcon[i].color = color;
                }
            }
            else
            {
                tagUI.guardianStoneElementIcon[i].gameObject.SetActive(false);
            }
        }
    }

    private UnityEngine.Sprite GetElementIcon(EBattleElementType element)
    {
        switch (element)
        {
            case EBattleElementType.Power: return tagUI.powerIcon;
            case EBattleElementType.Plasma: return tagUI.plasmaIcon;
            case EBattleElementType.Bio: return tagUI.bioIcon;
            case EBattleElementType.Chemical: return tagUI.chemicalIcon;
            case EBattleElementType.Electrical: return tagUI.electricalIcon;
            case EBattleElementType.Network: return tagUI.networkIcon;
            default: return tagUI.noneIcon;
        }
    }
}