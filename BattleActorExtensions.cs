using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using SkillSystem;

/// <summary>
/// BattleEventHandler에서 필요한 확장 메서드들
/// </summary>
public static class BattleEventHandlerExtensions
{
    /// <summary>
    /// BattleActor의 스탯 가져오기
    /// </summary>
    public static BattleCharInfoNew GetStats(this BattleActor actor)
    {
        if (actor == null) return null;
        return actor.BattleActorInfo;
    }

    /// <summary>
    /// 데미지 받기
    /// </summary>
    public static void TakeDamage(this BattleActor actor, float damage, bool isCritical)
    {
        if (actor == null || actor.BattleActorInfo == null) return;

        int damageInt = Mathf.RoundToInt(damage);

        // HP 감소
        actor.BattleActorInfo.TakeDamage(damageInt);

        // 데미지 숫자 표시
        ShowDamageNumber(actor, damageInt, isCritical, false);

        // 사망 체크
        if (actor.BattleActorInfo.IsDead)
        {
            //actor.SetState(BattleActor.State.Dead);
        }
    }

    /// <summary>
    /// 데미지 숫자 표시
    /// </summary>
    public static void ShowDamageNumber(this BattleActor actor, int damage, bool isCritical, bool isEvaded)
    {
        if (actor == null) return;

        // BattleDamageNumberUI를 통한 데미지 표시
        var damageUI = GameObject.FindObjectOfType<BattleDamageNumberUI>();
        if (damageUI != null)
        {
            if (isEvaded)
            {
                // MISS 표시
                damageUI.ShowDamage(actor.transform.position, 0, false, true);
            }
            else
            {
                damageUI.ShowDamage(actor.transform.position, damage, isCritical, false);
            }
        }
    }

    /// <summary>
    /// 현재 실행 중인 스킬 가져오기
    /// </summary>
    public static AdvancedSkillData GetCurrentSkill(this BattleProcessManagerNew manager)
    {
        if (manager == null) return null;

        return manager.GetSelectedSkill();

    }

    /// <summary>
    /// 모든 적 가져오기
    /// </summary>
    public static List<BattleActor> GetAllEnemies(this BattleProcessManagerNew manager)
    {
        if (manager == null) return new List<BattleActor>();

        var enemies = new List<BattleActor>();


        foreach (var enemyActor in manager.GetEnemyActors())
        {
            var enemyInfo = enemyActor.BattleActorInfo;

            if (enemyInfo != null && !enemyInfo.IsDead)
            {
                enemies.Add(enemyActor);
            }
        }


        return enemies;
    }

    /// <summary>
    /// 모든 아군 가져오기
    /// </summary>
    public static List<BattleActor> GetAllAllies(this BattleProcessManagerNew manager)
    {
        if (manager == null) return new List<BattleActor>();

        var allies = new List<BattleActor>();

        foreach (var allyActor in manager.GetAllAllies())
        {
            var allyInfo = allyActor.BattleActorInfo;

            if (allyInfo != null && !allyInfo.IsDead )
            {
                allies.Add(allyActor);
            }
        }

        return allies;
    }

    /// <summary>
    /// 액터 사망 처리
    /// </summary>
    public static void OnActorDeath(this BattleProcessManagerNew manager, BattleActor actor)
    {
        if (manager == null || actor == null) return;

        // TurnOrderSystem에서 제거
        if (manager.turnOrderSystem != null)
        {
            manager.turnOrderSystem.RemoveCharacter(actor);
        }

        // 전투 종료 체크는 BattleProcessManagerNew의 CheckBattleEnd() 호출
      
        
        //manager.CheckBattleResult();




    }
}

/// <summary>
/// BattleDamageNumberUI 확장 메서드
/// </summary>
public static class BattleDamageNumberUIExtensions
{
    public static void ShowDamage(this BattleDamageNumberUI ui, Vector3 position, int damage, bool isCritical, bool isMiss)
    {
        if (ui == null) return;

        // 데미지 텍스트 생성
        var damageText = isMiss ? "MISS" : damage.ToString();

        // 크리티컬이면 색상이나 크기 변경
        if (isCritical)
        {
            ui.ShowCriticalDamage(position, damage);
        }
        else if (isMiss)
        {
            ui.ShowMiss(position);
        }
        else
        {
            ui.ShowNormalDamage(position, damage);
        }
    }

    public static void ShowNormalDamage(this BattleDamageNumberUI ui, Vector3 position, int damage)
    {
        if (ui == null) return;
       // ui.CreateDamageText(position, damage.ToString(), Color.white, 1f);
    }

    public static void ShowCriticalDamage(this BattleDamageNumberUI ui, Vector3 position, int damage)
    {
        if (ui == null) return;
     //   ui.CreateDamageText(position, damage.ToString() + "!", Color.yellow, 1.5f);
    }

    public static void ShowMiss(this BattleDamageNumberUI ui, Vector3 position)
    {
        if (ui == null) return;
       // ui.CreateDamageText(position, "MISS", Color.gray, 0.8f);
    }
}