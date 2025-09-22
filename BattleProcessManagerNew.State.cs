using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public partial class BattleProcessManagerNew : MonoBehaviour
{


    // === 기존 메서드들 (수정 없음) ===
    private async UniTask ProcessState(BattleState state, CancellationToken token)
    {
        switch (state)
        {
            case BattleState.Initialize:
                await InitializeBattle(token);
                break;
            case BattleState.BattleStart:
                await OnBattleStart(token);
                break;
            case BattleState.BattleResult:
                await ShowBattleResult(token);
                break;

            default:
                break;
        }
    }

    private async UniTask InitializeBattle(CancellationToken token)
    {
        Debug.Log("Battle Initialize");


        // 주석 해제하고 배열 초기화 방식 변경
        AllyInfos = new BattleCharInfoNew[5];
        EnemyInfos = new BattleCharInfoNew[5];

        // 아군 캐릭터 로드 (비동기)
        var allyTasks = new List<Task<BattleCharInfoNew>>();
        for (int i = 0; i < characterIds.Length && i < 5; i++)
        {
            if (characterIds[i] > 0)
            {
                allyTasks.Add(BattleDataLoader.LoadCharacterDataCharInfoAsync(
                    characterIds[i],
                    characterKeys[i],
                    true,  // isAlly
                    i      // slotIndex
                ));
            }
            else
            {
                allyTasks.Add(Task.FromResult<BattleCharInfoNew>(null));
            }
        }

        // 비동기 로드 대기
        var allyResults = await Task.WhenAll(allyTasks);
        for (int i = 0; i < allyResults.Length; i++)
        {
            if (allyResults[i] != null)
            {
                AllyInfos[i] = allyResults[i];
                Debug.Log($"[Battle] Loaded ally {i}: {AllyInfos[i].Name}");
            }
        }

        // 적 몬스터 그룹 로드 (비동기)
        var monsterGroupCharInfos = await BattleDataLoader.LoadMonsterGroupCharInfoAsync(monsterGroupId);
        if (monsterGroupCharInfos != null && monsterGroupCharInfos.Count > 0)
        {
            for (int i = 0; i < monsterGroupCharInfos.Count && i < 5; i++)
            {
                EnemyInfos[i] = monsterGroupCharInfos[i];
                Debug.Log($"[Battle] Loaded enemy {i}: {monsterGroupCharInfos[i].Name}");
            }
        }


        // monsterGroup.
        var monsterGroup = await BattleDataLoader.GetMonsterGroupDataAsync(monsterGroupId);


        //battlePosition.SetFormation(monsterGroup.FormationType, false);
        battlePosition.SetFormation(monsterGroup.FormationType);  // 아군/적군 둘 다 설정됨


        Debug.Log($"[Battle] Data loaded - Allies: {AllyInfos.Length}, Enemies: {EnemyInfos.Length}");


        await InitBattleActors();
        // BP 초기화 추가 (이 줄만 추가)
        InitializeAllCharacterBP();


        await UniTask.WaitUntil(() => allyActors.Count > 0 && enemyActors.Count > 0, cancellationToken: token);
    }







    private async UniTask OnBattleStart(CancellationToken token)
    {
        Debug.Log("Battle Start!");
        await UniTask.Delay(500, cancellationToken: token);
    }

    private async UniTask ShowBattleResult(CancellationToken token)
    {
        bool victory = allyActors.Any(a => !a.IsDead);
        Debug.Log(victory ? "Victory!" : "Defeat...");
        if (victory)
        {
            foreach (var ally in allyActors.Where(a => !a.IsDead))
            {
                ally.SetState(BattleActorState.Victory);
            }
        }
        await UniTask.Delay(2000, cancellationToken: token);
    }




   private void ProcessTurnEnd(BattleActor actor)
   {
       
        if (actor?.BattleActorInfo == null) return;

        // BP 처리 (유지)
        ProcessTurnBP(actor);

        // 쿨다운 처리 (추가)
        actor.CooldownManager?.ProcessTurnEnd();

    }

}
