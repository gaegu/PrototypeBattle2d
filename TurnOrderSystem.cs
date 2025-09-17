using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 라운드별 턴 순서를 관리하는 시스템
/// TurnSpeed가 높을수록 먼저 행동
/// </summary>
public class TurnOrderSystem
{
    // 턴 정보 클래스
    public class TurnOrderInfo
    {
        public BattleActor Actor { get; set; }
        public BattleCharInfoNew CharInfo { get; set; }
        public bool IsAlly { get; set; }
        public int Index { get; set; }
        public float TurnSpeed { get; set; }
        public bool HasActedThisRound { get; set; }  // 이번 라운드에 행동했는지

        public TurnOrderInfo(BattleActor actor, BattleCharInfoNew charInfo, bool isAlly, int index)
        {
            Actor = actor;
            CharInfo = charInfo;
            IsAlly = isAlly;
            Index = index;
            TurnSpeed = charInfo.TurnSpeed > 0 ? charInfo.TurnSpeed : 100f; // 기본값 100
            HasActedThisRound = false;
        }
    }

    private List<TurnOrderInfo> allCharacters = new List<TurnOrderInfo>();
    private Queue<TurnOrderInfo> currentRoundQueue = new Queue<TurnOrderInfo>();

    public int CurrentRound { get; private set; } = 1;
    public int CurrentTurn { get; private set; } = 0;
    public int TurnsInCurrentRound { get; private set; } = 0;

    /// <summary>
    /// 시스템 초기화
    /// </summary>
    public void Initialize(List<BattleActor> allies, List<BattleActor> enemies)
    {
        allCharacters.Clear();
        currentRoundQueue.Clear();
        CurrentRound = 1;
        CurrentTurn = 0;
        TurnsInCurrentRound = 0;

        // 아군 추가
        for (int i = 0; i < allies.Count; i++)
        {
            if (allies[i] != null && !allies[i].IsDead)
            {
                var turnInfo = new TurnOrderInfo(allies[i], allies[i].BattleActorInfo, true, i);
                allCharacters.Add(turnInfo);
            }
        }

        // 적군 추가
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null && !enemies[i].IsDead)
            {
                var turnInfo = new TurnOrderInfo(enemies[i], enemies[i].BattleActorInfo, false, i);
                allCharacters.Add(turnInfo);
            }
        }

        // 첫 라운드 순서 생성
        BuildRoundOrder();

        Debug.Log($"[TurnOrder] Round {CurrentRound} - Order determined by TurnSpeed");
        PrintRoundOrder();
    }

    /// <summary>
    /// 라운드 시작 시 턴 순서 결정
    /// </summary>
    private void BuildRoundOrder()
    {
        currentRoundQueue.Clear();
        TurnsInCurrentRound = 0;

        // 모든 캐릭터의 행동 플래그 초기화
        foreach (var character in allCharacters)
        {
            character.HasActedThisRound = false;
        }

        // TurnSpeed 기준으로 정렬 (높은 순)
        var sortedCharacters = allCharacters
            .Where(c => c.Actor != null && !c.Actor.IsDead)
            .OrderByDescending(c => c.TurnSpeed)  // 속도 높은 순
            .ThenBy(c => UnityEngine.Random.Range(0f, 1f))  // 같은 속도면 랜덤
            .ToList();

        // 큐에 추가
        foreach (var character in sortedCharacters)
        {
            currentRoundQueue.Enqueue(character);
            TurnsInCurrentRound++;
        }

        Debug.Log($"[Round {CurrentRound}] {TurnsInCurrentRound} characters will act this round");
    }

    /// <summary>
    /// 다음 행동할 캐릭터 가져오기
    /// </summary>
    public TurnInfo GetNextTurn()
    {
        // 현재 라운드의 큐가 비었으면 새 라운드 시작
        if (currentRoundQueue.Count == 0)
        {
            // 살아있는 캐릭터가 있는지 확인
            if (!HasAliveCharacters())
            {
                return new TurnInfo(false, -1, null);
            }

            // 새 라운드 시작
            StartNewRound();
        }

        // 큐에서 다음 캐릭터 가져오기
        TurnOrderInfo nextCharacter = null;

        while (currentRoundQueue.Count > 0)
        {
            nextCharacter = currentRoundQueue.Dequeue();

            // 캐릭터가 죽었으면 스킵
            if (nextCharacter.Actor.IsDead)
            {
                Debug.Log($"[Turn Skip] {(nextCharacter.IsAlly ? "Ally" : "Enemy")} {nextCharacter.Index} is dead");
                continue;
            }

            break;
        }

        if (nextCharacter == null || nextCharacter.Actor.IsDead)
        {
            return new TurnInfo(false, -1, null);
        }

        // 행동 플래그 설정
        nextCharacter.HasActedThisRound = true;
        CurrentTurn++;

        // 턴 정보 생성
        var turnInfo = new TurnInfo(nextCharacter.IsAlly, nextCharacter.Index, nextCharacter.Actor);

        Debug.Log($"[Turn {CurrentTurn}] Round {CurrentRound} - {(nextCharacter.IsAlly ? "Ally" : "Enemy")} {nextCharacter.Index} " +
                  $"(Speed: {nextCharacter.TurnSpeed})");

        return turnInfo;
    }

    /// <summary>
    /// 새 라운드 시작
    /// </summary>
    private void StartNewRound()
    {
        CurrentRound++;
        Debug.Log($"[Round {CurrentRound}] Starting new round!");

        // 새 라운드의 턴 순서 결정
        BuildRoundOrder();
        PrintRoundOrder();


    }

    // AI가 사용할 메서드 추가
    public List<BattleActor> GetNextActors(int count)
    {
        var aliveCharacters = allCharacters
            .Where(c => !c.Actor.IsDead)
            .OrderByDescending(c => c.TurnSpeed)
            .Take(count)
            .Select(c => c.Actor)
            .ToList();

        return aliveCharacters;
    }


    /// <summary>
    /// 현재 라운드가 끝났는지 확인
    /// </summary>
    public bool IsRoundComplete()
    {
        // 큐가 비었고, 모든 살아있는 캐릭터가 행동했으면 라운드 완료
        return currentRoundQueue.Count == 0 &&
               allCharacters.Where(c => !c.Actor.IsDead).All(c => c.HasActedThisRound);
    }

    /// <summary>
    /// 살아있는 캐릭터가 있는지 확인
    /// </summary>
    public bool HasAliveCharacters()
    {
        return allCharacters.Any(c => c.Actor != null && !c.Actor.IsDead);
    }

    /// <summary>
    /// 캐릭터의 턴 속도 변경 (버프/디버프)
    /// </summary>
    public void ModifyTurnSpeed(BattleActor actor, float speedMultiplier)
    {
        var character = allCharacters.FirstOrDefault(c => c.Actor == actor);
        if (character != null)
        {
            float oldSpeed = character.TurnSpeed;
            character.TurnSpeed *= speedMultiplier;

            Debug.Log($"[TurnSpeed] {actor.name} speed changed: {oldSpeed} → {character.TurnSpeed}");

            // 다음 라운드부터 적용됨
            Debug.Log($"[TurnSpeed] Speed change will be applied from Round {CurrentRound + 1}");
        }
    }

    /// <summary>
    /// 현재 라운드의 남은 턴 순서 가져오기 (UI용)
    /// </summary>
    public List<TurnOrderInfo> GetRemainingTurnsInRound()
    {
        var remaining = new List<TurnOrderInfo>();
        remaining.AddRange(currentRoundQueue.Where(c => !c.Actor.IsDead));
        return remaining;
    }

    /// <summary>
    /// 다음 라운드의 예상 순서 미리보기 (UI용)
    /// </summary>
    public List<TurnOrderInfo> GetNextRoundPreview()
    {

        // 살아있는 캐릭터가 없으면 빈 리스트 반환
        var aliveCharacters = allCharacters
            .Where(c => c.Actor != null && !c.Actor.IsDead)
            .ToList();

        if (aliveCharacters.Count == 0)
        {
            Debug.Log("[TurnOrder] No alive characters for next round preview");
            return new List<TurnOrderInfo>();
        }

        // 현재 살아있는 캐릭터들을 TurnSpeed로 정렬
        return aliveCharacters
            .OrderByDescending(c => c.TurnSpeed)
            .ToList();
    }

    /// <summary>
    /// 라운드 순서 출력 (디버그용)
    /// </summary>
    public void PrintRoundOrder()
    {
        Debug.Log($"=== Round {CurrentRound} Turn Order ===");
        var order = GetRemainingTurnsInRound();

        int index = 1;
        foreach (var c in order)
        {
            string name = c.CharInfo?.CharacterData.GetNAME_ENG() +
                         $"{(c.IsAlly ? "Ally" : "Enemy")} {c.Index}";
            Debug.Log($"{index}. {name} (Speed: {c.TurnSpeed})");
            index++;
        }
    }

    /// <summary>
    /// 캐릭터 제거 (사망 시)
    /// </summary>
    public void RemoveCharacter(BattleActor actor)
    {
        var character = allCharacters.FirstOrDefault(c => c.Actor == actor);
        if (character != null)
        {
            Debug.Log($"[TurnOrder] {actor.name} died and removed from turn order");
            // 실제로 리스트에서 제거하지 않고 IsDead 체크로 처리
        }
    }

    /// <summary>
    /// 현재 라운드 진행 상황 가져오기
    /// </summary>
    public (int current, int total) GetRoundProgress()
    {
        int acted = allCharacters.Count(c => !c.Actor.IsDead && c.HasActedThisRound);
        int total = allCharacters.Count(c => !c.Actor.IsDead);
        return (acted, total);
    }
}