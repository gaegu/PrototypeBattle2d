using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using IronJade.Observer.Core;

// BattleActor의 부분 클래스로 이펙트 관련 기능 추가
public partial class BattleActor : MonoBehaviour
{
    [Header("이펙트 설정")]
    [SerializeField] private string attackEffectId = "BasicAttack";
    [SerializeField] private string skillEffectId = "BasicSkill";
    [SerializeField] private string hitEffectId = "BasicHit";
    [SerializeField] private bool useCharacterSpecificEffects = true;

    // 이펙트 ID 캐시
    private Dictionary<BattleActorEffectType, string> effectIdCache = new Dictionary<BattleActorEffectType, string>();
    private bool effectsInitialized = false;

    // BP 이펙트 관련 필드
    private EffectInstance currentBPEffectInstancce = null;


    /// <summary>
    /// 현재 사용 예정인 BP 가져오기
    /// </summary>
    private int GetPendingBP()
    {
        // BattleProcessManagerNew에서 pendingBPUse 값 가져오기
        if (BattleProcessManagerNew.Instance != null)
        {
            // pendingBPUse를 public property로 노출하거나
            // 또는 BattleActorInfo의 UsedBPThisTurn 사용
            return BattleActorInfo.UsedBPThisTurn;
        }
        return 0;
    }


    /// <summary>
    /// BattleActor 초기화 시 호출
    /// </summary>
    private void InitializeBattleActor()
    {
        // 이펙트 ID 초기화 자동 호출
        InitializeEffectIds();

        InitializeSkillSystem();
    }

    /// <summary>
    /// 이펙트 ID 초기화
    /// </summary>
    private void InitializeEffectIds()
    {
        if (effectsInitialized) return;

        effectIdCache.Clear();


        //CommonEffect
        var commonEffects = BattleEffectManager.Instance.GetCommonEffectIds();
        if (commonEffects != null && commonEffects.Count > 0)
        {
            // 가져온 이펙트 ID로 캐시 업데이트
            foreach (var kvp in commonEffects)
            {
                effectIdCache[kvp.Key] = kvp.Value;
                Debug.Log($"Common [BattleActor] {name} - {kvp.Key}: {kvp.Value}");
            }
        }


        // BattleEffectManager에서 캐릭터별 이펙트 가져오기
        if (BattleEffectManager.Instance != null && useCharacterSpecificEffects)
        {
            string characterName = GetCharacterName();

            if (!string.IsNullOrEmpty(characterName))
            {
                // BattleEffectManager에서 캐릭터별 이펙트 ID 가져오기
                var characterEffects = BattleEffectManager.Instance.GetCharacterEffectIds(characterName);

                if (characterEffects != null && characterEffects.Count > 0)
                {
                    // 가져온 이펙트 ID로 캐시 업데이트
                    foreach (var kvp in characterEffects)
                    {
                        effectIdCache[kvp.Key] = kvp.Value;
                        Debug.Log($"[BattleActor] {name} - {kvp.Key}: {kvp.Value}");
                    }

                    // 직렬화된 필드도 업데이트 (Inspector에서 확인용)
                    if (effectIdCache.TryGetValue(BattleActorEffectType.Attack1, out string attack))
                        attackEffectId = attack;
                    if (effectIdCache.TryGetValue(BattleActorEffectType.Skill1, out string skill))
                        skillEffectId = skill;
                    if (effectIdCache.TryGetValue(BattleActorEffectType.Hit, out string hit))
                        hitEffectId = hit;
                }
            }
        }



        // 캐시에 없는 이펙트 타입은 기본값 사용
        if (!effectIdCache.ContainsKey(BattleActorEffectType.Attack1))
            effectIdCache[BattleActorEffectType.Attack1] = attackEffectId;
        if (!effectIdCache.ContainsKey(BattleActorEffectType.Skill1))
            effectIdCache[BattleActorEffectType.Skill1] = skillEffectId;
        if (!effectIdCache.ContainsKey(BattleActorEffectType.Hit))
            effectIdCache[BattleActorEffectType.Hit] = hitEffectId;
        if (!effectIdCache.ContainsKey(BattleActorEffectType.Heal))
            effectIdCache[BattleActorEffectType.Heal] = "BasicHeal";




        effectsInitialized = true;
        Debug.Log($"[BattleActor] {name} effect IDs initialized");
    }

    /// <summary>
    /// 캐릭터 이름 가져오기
    /// </summary>
    private string GetCharacterName()
    {
        // BattleActorInfo에서 이름 가져오기
        if (BattleActorInfo != null)
        {
            return BattleActorInfo.Name;
        }

        // GameObject 이름에서 추출 (ALLY> 또는 ENEMY> 제거)
        string objName = gameObject.name;
        if (objName.Contains(">"))
        {
            string[] parts = objName.Split('>');
            if (parts.Length > 1)
            {
                // "ALLY>CharacterName / 0" 형식에서 캐릭터 이름 추출
                string nameWithSlot = parts[1].Trim();
                if (nameWithSlot.Contains("/"))
                {
                    return nameWithSlot.Split('/')[0].Trim();
                }
                return nameWithSlot;
            }
        }

        return name;
    }


    /// <summary>
    /// 공격 수행 (이펙트 시스템 통합)
    /// </summary>
    public async UniTask PerformAttack()
    {
        SetAnimation(BattleActorAnimation.Attack);
        string effectId = GetEffectId(BattleActorEffectType.Attack1);

        // 타겟이 설정되어 있으면 타겟 정보도 전달
        BattleActor target = GetCurrentTarget();

        await BattleEffectManager.Instance.PlayEffect(
            effectId,
            this,           // 시전자
            target,         // 타겟
            OnAttackHit     // 피격 콜백
        );

        SetAnimation(BattleActorAnimation.Idle);
    }

    /// <summary>
    /// BP 연속 공격
    /// </summary>
    public void ExecuteBPCombo(BattleActor target, int bpAmount)
    {
        if (bpManager == null) return;

        // BP 수만큼 연속 공격
        for (int i = 0; i < bpAmount; i++)
        {
            if (bpManager.UseBP(1))
            {
                // 기본 공격 또는 액티브 스킬 사용
                var activeSkill = GetAvailableActiveSkill();
                if (activeSkill != null)
                {
                    // BP 사용하여 업그레이드하고 실행
                    UseSkillWithBP(activeSkill.SkillData, target, true);
                }
                else
                {
                    // 기본 공격
                    PerformAttack().Forget();
                }
            }
        }
    }


    /// <summary>
    /// 스킬 수행 (이펙트 시스템 통합)
    /// </summary>
    public async UniTask PerformSkill()
    {
        SetAnimation(BattleActorAnimation.Skill);

        // 스킬 이펙트 재생

        string effectId = GetEffectId(BattleActorEffectType.Skill1);
        BattleActor target = GetCurrentTarget();

        await BattleEffectManager.Instance.PlayEffect(
            effectId,
            this,
            target,
            OnSkillHit
        );


        SetAnimation(BattleActorAnimation.Idle);
    }


    public void PerformDeath()
    {

        // 사망 애니메이션 재생 (기존 코드에 있다면)
        SetAnimation(BattleActorAnimation.Dead);

        // 사망 이벤트 발생
        var deathEvent = new StatusEventData(BattleEventType.CharacterDeath)
        {
            Actor = this
        };
        BattleEventManager.Instance?.TriggerEvent(BattleEventType.CharacterDeath, deathEvent);
    }



    /// <summary>
    /// BattleFormularHelper의 결과를 사용하는 새로운 데미지 처리
    /// </summary>
    public async UniTask TakeDamageWithResult(
        BattleFormularHelper.DamageResult damageResult,
        BattleActor attacker,
        string attackEffectId = null)
    {
        if (BattleActorInfo == null) return;


        // === 2. GuardianStone 처리 ===
        if (guardianStoneSystem != null && attacker != null)
        {
            // Hit 판정일 때만 GuardianStone 체크
            if ((damageResult.Judgement & BattleFormularHelper.JudgementType.Hit) != 0 ||
                (damageResult.Judgement & BattleFormularHelper.JudgementType.Critical) != 0 ||
                (damageResult.Judgement & BattleFormularHelper.JudgementType.Strike) != 0)
            {
                bool stoneBroken = guardianStoneSystem.TryBreakStone(attacker.BattleActorInfo.Element);

                if (stoneBroken)
                {
                    // Break 이펙트 재생
                    string breakEffectId = GetEffectId(BattleActorEffectType.Break);
                    if (!string.IsNullOrEmpty(breakEffectId))
                    {
                        await BattleEffectManager.Instance.PlayEffect(breakEffectId, this);
                    }

                    // Break UI
                    if (guardianStoneSystem.IsBreakState && BattleUIManager.Instance != null)
                    {
                        /*BattleUIManager.Instance.ShowBattleText(
                            transform.position + Vector3.up * 2f,
                            "BREAK!",
                            Color.magenta
                        );*/
                    }
                }
            }
        }

        // === 3. Break 상태 데미지 보정 ===
        float finalDamage = damageResult.FinalDamage;
        if (guardianStoneSystem != null && guardianStoneSystem.IsBreakState)
        {
            // Break 상태에서 데미지 1.5배
            finalDamage = damageResult.FinalDamage * 1.5f;
            Debug.Log($"[GuardianStone] Break state damage: {damageResult.FinalDamage} -> {finalDamage}");
        }

        // === 4. Block 처리 ===
        if (damageResult.IsBlocked)
        {
            Debug.Log($"[Battle] {attacker.name} -> {name}: BLOCKED!");

            // Block 이펙트
            if (BattleEffectManager.Instance != null)
            {
                await BattleEffectManager.Instance.PlayEffect("BlockEffect", this);
            }

            // Block UI
            if (BattleUIManager.Instance != null)
            {
                /*BattleUIManager.Instance.ShowBattleText(
                    transform.position + Vector3.up * 0.5f,
                    "BLOCK",
                    Color.blue
                );*/

            }

            // Block은 데미지는 받음 (이미 BattleFormularHelper에서 감소 적용됨)
        }

        // === 5. Critical 처리 ===
        if (damageResult.IsCritical)
        {
            Debug.Log($"[Battle] CRITICAL HIT! Bonus: {damageResult.CriticalBonus}");

            // Critical 이펙트
            if (BattleEffectManager.Instance != null)
            {
                await BattleEffectManager.Instance.PlayEffect("CriticalEffect", this);
            }
        }


        // 데미지 감소 적용 (방어막 보유시)
        if (BattleActorInfo.HasShield && BattleActorInfo.DamageReductionPercent > 0)
        {
            damageResult.FinalDamage = BattleActorInfo.ApplyDamageReduction(damageResult.FinalDamage);
        }



        // ============= 여기에 Shield 처리 추가 =============
        // === 5-1. Shield 처리 UI (새로 추가) ===
        if (damageResult.ShieldAbsorbed > 0)
        {
            Debug.Log($"[Shield] Absorbed {damageResult.ShieldAbsorbed} damage");

            // Shield 흡수 이펙트
            if (BattleEffectManager.Instance != null)
            {
                await BattleEffectManager.Instance.PlayEffect("ShieldAbsorb", this);
            }

            // Shield 수치 표시
            if (BattleUIManager.Instance != null)
            {
                /*BattleUIManager.Instance.ShowBattleText(
                    transform.position + Vector3.up * 1.5f,
                    $"Shield -{damageResult.ShieldAbsorbed}",
                    Color.cyan
                );*/
            }

            // Shield가 완전히 파괴된 경우
            if (BattleActorInfo.Shield <= 0 && damageResult.FinalDamage > 0)
            {
                Debug.Log($"[Shield] Shield broken! Remaining damage: {damageResult.FinalDamage}");

                // Shield 파괴 이펙트
                if (BattleEffectManager.Instance != null)
                {
                    await BattleEffectManager.Instance.PlayEffect("ShieldBreak", this);
                }
            }
        }



        // === 7. Reflect処理（新規追加） ===
        if (damageResult.FinalDamage > 0 && attacker != null && attacker != this)
        {
            int reflectedDamage = CheckAndApplyReflect(damageResult.FinalDamage, attacker);

            if (reflectedDamage > 0)
            {
                Debug.Log($"[Reflect] {name} reflects {reflectedDamage} damage back to {attacker.name}");

                // 反射ダメージは追加エフェクトなしのピュアダメージ
                var reflectResult = new BattleFormularHelper.DamageResult
                {
                    FinalDamage = reflectedDamage,
                    OriginalDamage = reflectedDamage,
                    Judgement = BattleFormularHelper.JudgementType.Hit,
                    Options = BattleFormularHelper.DamageOption.IgnoreDefense | BattleFormularHelper.DamageOption.IgnoreShield
                };

                // 反射ダメージ適用（再帰を防ぐため直接HP減少）
                attacker.TakeDamageDirectly(reflectedDamage, this);

                // 反射エフェクト
                if (BattleEffectManager.Instance != null)
                {
                    await BattleEffectManager.Instance.PlayEffect("ReflectDamage", this);
                }

                // UI表示
                if (BattleUIManager.Instance != null)
                {
                    /*BattleUIManager.Instance.ShowBattleText(
                        attacker.transform.position + Vector3.up,
                        $"Reflected: {reflectedDamage}",
                        Color.magenta
                    );*/
                }
            }
        }



        // === 6. Resist 처리 (스킬 효과만) ===
        if (damageResult.IsResisted)
        {
            Debug.Log($"[Battle] Skill effect RESISTED!");

            if (BattleUIManager.Instance != null)
            {
                BattleUIManager.Instance.ShowBattleText( this, BattleTextType.Resist );
            }
        }

        // === 7. HP 감소 ===
        int actualDamage = Mathf.RoundToInt(finalDamage);
        BattleActorInfo.Hp = Mathf.Max(0, BattleActorInfo.Hp - actualDamage);

        // === 8. 데미지 표시 ===
        if (BattleUIManager.Instance != null && actualDamage > 0)
        {

            BattleUIManager.Instance.ShowDamageNumber(
                this,
                actualDamage,
                damageResult.IsCritical
            );
        }




        // === 데미지 처리 완료 후 공격자의 LifeSteal 처리 추가 ===
        if (attacker != null && damageResult.HasLifeSteal && damageResult.LifeStealAmount > 0)
        {
            // 공격자 HP 회복
            int previousHP = attacker.BattleActorInfo.Hp;
            attacker.BattleActorInfo.Hp = Mathf.Min(
                attacker.BattleActorInfo.MaxHp,
                previousHP + damageResult.LifeStealAmount
            );

            int actualHealed = attacker.BattleActorInfo.Hp - previousHP;

            if (actualHealed > 0)
            {
                Debug.Log($"[LifeSteal] {attacker.name} healed {actualHealed} HP from life steal");

                // 흡혈 이펙트 표시
                if (BattleEffectManager.Instance != null)
                {
                    await BattleEffectManager.Instance.PlayEffect("LifeStealEffect", attacker);
                }

                // UI 업데이트
                attacker.UpdateTag();
            }
        }




        // === 9. 카메라 쉐이크 ===
        if (actualDamage > 0)
        {
            CamShake.shake(damageResult.IsCritical ? 2 : 1);
        }

        // === 10. 피격 애니메이션 및 이펙트 ===
        if (!damageResult.IsBlocked && actualDamage > 0)  // Block이 아닐 때만
        {
            SetAnimation(BattleActorAnimation.Hit);
            PlayHitColorEffect();

            // 피격 이펙트
            if (!string.IsNullOrEmpty(attackEffectId))
            {
                await BattleEffectManager.Instance.PlayHitEffect(attackEffectId, this, attacker);
            }

            await UniTask.Delay(500);
            SetAnimation(BattleActorAnimation.Idle);
        }

        // === 11. 태그 UI 업데이트 ===
        UpdateTag();

        // === 12. 사망 체크 ===
        if (BattleActorInfo.IsDead)
        {
            await HandleDeath();

            Die(attacker);
        }

        Debug.Log($"[Damage] {name} took {finalDamage} damage! (Critical: {damageResult.IsCritical})");
    }


    public void Die(BattleActor attacker )
    {
 
        // Kill 이벤트 발생
        var param = new BattleObserverParam(attacker, this);
        ObserverManager.NotifyObserver(BattleNewObserverID.Kill, param);

        // BattleEventManager를 통한 이벤트도 발생 (선택사항)
        if (BattleEventManager.Instance != null)
        {
            var eventData = new DamageEventData(BattleEventType.CharacterDeath)
            {
                Source = attacker,
                Target = this
            };
            BattleEventManager.Instance.TriggerEvent(BattleEventType.CharacterDeath, eventData);
        }
    }




    // 기존 TakeDamage는 하위 호환성을 위해 유지
    public async UniTask TakeDamage(int damage, BattleActor attacker, string attackEffectId = null)
    {
        // 간단한 DamageResult 생성
        var simpleResult = new BattleFormularHelper.DamageResult
        {
            FinalDamage = damage,
            OriginalDamage = damage,
            Judgement = BattleFormularHelper.JudgementType.Hit,
            IsCritical = false,
            IsMissed = false,
            IsDodged = false,
            IsBlocked = false,
            IsResisted = false,
            IsImmune = false
        };

        await TakeDamageWithResult(simpleResult, attacker, attackEffectId);
    }



    // Reflect計算メソッド（新規追加）
    private int CheckAndApplyReflect(int incomingDamage, BattleActor attacker)
    {
        if (SkillManager == null) return 0;

        // 現在のReflectバフを確認
        var reflectBuffs = SkillManager.GetReflectBuffs();
        if (reflectBuffs.Count == 0) return 0;

        int totalReflected = 0;

        foreach (var reflectBuff in reflectBuffs)
        {
            if (reflectBuff.CurrentEffect != null &&
                reflectBuff.CurrentEffect.type == SkillSystem.EffectType.Reflect)
            {
                float reflectPercent = reflectBuff.CurrentEffect.value;
                int reflectAmount = Mathf.RoundToInt(incomingDamage * (reflectPercent / 100f));

                // 最大反射ダメージ制限
                if (reflectBuff.CurrentEffect.maxReflectDamage > 0)
                {
                    reflectAmount = Mathf.Min(reflectAmount, reflectBuff.CurrentEffect.maxReflectDamage);
                }

                totalReflected += reflectAmount;
            }
        }

        return totalReflected;
    }

    // 直接ダメージメソッド（反射ダメージ用、再帰防止）
    public void TakeDamageDirectly(int damage, BattleActor source)
    {
        if (damage <= 0) return;

        BattleActorInfo.Hp = Mathf.Max(0, BattleActorInfo.Hp - damage);

        Debug.Log($"[DirectDamage] {name} took {damage} direct damage from {source?.name}. HP: {BattleActorInfo.Hp}/{BattleActorInfo.MaxHp}");

        // HP UIアップデート
        UpdateTag();

        // 死亡チェック
        if (BattleActorInfo.IsDead)
        {
            HandleDeath().Forget();
        }
    }

    // 행동 가능 여부 체크
    public bool CanAct()
    {
        // StatusFlag 체크 추가
        if (BattleActorInfo != null && !BattleActorInfo.CanAct())
        {
            Debug.Log($"[BattleActor] {name} cannot act - StatusFlag.CannotAct!");
            return false;
        }

        // 브레이크 상태면 행동 불가
        if (guardianStoneSystem != null && guardianStoneSystem.IsBreakState)
        {
            Debug.Log($"[GuardianStone] {name} cannot act - BREAK state!");
            return false;
        }


        return !IsDead;
    }

    // 턴 종료 시 호출
    public void OnTurnEnd()
    {
        guardianStoneSystem?.ProcessTurn();

        skillManager?.OnTurnUpdate();
    }


    /// <summary>
    /// 사망 처리
    /// </summary>
    private async UniTask HandleDeath()
    {
        Debug.Log($"[Death] {name} has died!");
        SetState(BattleActorState.Dead);
        SetAnimation(BattleActorAnimation.Dead);

        // 태그 어둡게 처리
        if (actorTag != null)
        {
            CanvasGroup canvasGroup = actorTag.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = actorTag.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0.3f;
            actorTag.SetSelectable(false);
        }

        // 사망 이벤트 발생
        if (BattleEventManager.Instance != null)
        {
            var deathData = new StatusEventData(BattleEventType.CharacterDeath)
            {
                Actor = this
            };
            BattleEventManager.Instance.TriggerEvent(BattleEventType.CharacterDeath, deathData);
        }

        await UniTask.Delay(1000);
    }

    /// <summary>
    /// 이펙트 ID 가져오기
    /// </summary>
    private string GetEffectId(BattleActorEffectType effectType)
    {
        if (effectIdCache.TryGetValue(effectType, out string effectId))
        {
            return effectId;
        }

        // 기본값 반환
        switch (effectType)
        {
            case BattleActorEffectType.Attack1:
                return "BasicAttack";
            case BattleActorEffectType.Skill1:
                return "BasicSkill";
            case BattleActorEffectType.Hit:
                return "BasicHit";
            case BattleActorEffectType.Heal:
                return "BasicHeal";

            case BattleActorEffectType.ChargeBP1:
                return "Common_UseBP1";

            case BattleActorEffectType.ChargeBP2:
                return "Common_UseBP2";

            case BattleActorEffectType.ChargeBP3:
                return "Common_UseBP3";

            default:
                return "BasicEffect";
        }
    }





    /// <summary>
    /// 현재 타겟 가져오기
    /// </summary>
    private BattleActor GetCurrentTarget()
    {
        // BattleProcessManagerNew의 상태 머신에서 현재 타겟 정보 가져오기
        if (BattleProcessManagerNew.Instance != null)
        {
            var stateMachine = BattleProcessManagerNew.Instance.GetStateMachine();
            if (stateMachine != null)
            {
                return stateMachine.CurrentTurn.Target;
            }
        }
        return null;
    }

    /// <summary>
    /// 공격 피격 콜백
    /// </summary>
    private void OnAttackHit(BattleActor target)
    {
        if (target == null || target.IsDead) return;


        // BattleFormularHelper 사용으로 변경
        var context = new BattleFormularHelper.BattleContext
        {
            Attacker = this,
            Defender = target,
            SkillPower = 100,  // 기본 공격
            UsedBP = GetPendingBP(),  // BP 사용량 가져오기
            DamageOptions = BattleFormularHelper.DamageOption.None
        };

        var damageResult = BattleFormularHelper.CalculateDamage(context);

        // 피격 처리 (기존 메서드 활용)
        // target.TakeDamageWithResult(damageResult, this, attackEffectId).Forget();

        // 판정별 처리
        ProcessDamageResult(target, damageResult, attackEffectId).Forget();

    }





    /// <summary>
    /// 스킬 피격 콜백
    /// </summary>
    private void OnSkillHit(BattleActor target)
    {
        if (target == null || target.IsDead) return;

        // 스킬 데미지 계산
        var context = new BattleFormularHelper.BattleContext
        {
            Attacker = this,
            Defender = target,
            SkillPower = 150,  // 스킬 계수 150%
            UsedBP = GetPendingBP(),
            DamageOptions = BattleFormularHelper.DamageOption.None,
            AttackType = target.BattleActorInfo.AttackType,
            IsSkillAttack = true
        };

        

        var damageResult = BattleFormularHelper.CalculateDamage(context);

        // 판정별 처리
        ProcessDamageResult(target, damageResult, skillEffectId).Forget();


    }


    /// <summary>
    /// 데미지 결과 처리 (판정별 이펙트 및 데미지 적용)
    /// </summary>
    private async UniTask ProcessDamageResult(BattleActor target, BattleFormularHelper.DamageResult result, string baseEffectId)
    {

        // 1. Miss 처리
        if (result.IsMissed)
        {
            Debug.Log($"[Battle] {name} -> {target.name}: MISS!");

            // Miss 이펙트 재생
            await PlayMissEffect(target);

            BattleUIManager.Instance.ShowBattleText(target, BattleTextType.Miss);

            return;  // Miss는 데미지 없음
        }

        // 2. Dodge 처리
        if (result.IsDodged)
        {
            Debug.Log($"[Battle] {name} -> {target.name}: DODGED!");

            // Dodge 이펙트 재생
            await PlayDodgeEffect(target);

            BattleUIManager.Instance.ShowBattleText(target, BattleTextType.Dodge);

            return;  // Dodge는 데미지 없음
        }

        // 3. Immune 처리
        if (result.IsImmune)
        {
            Debug.Log($"[Battle] {name} -> {target.name}: IMMUNE!");

            // Immune 이펙트 재생
            await PlayImmuneEffect(target);

            BattleUIManager.Instance.ShowBattleText(target, BattleTextType.Immune);

            return;  // Immune는 데미지 없음
        }

        // 4. Block 처리 (데미지는 있지만 감소)
        if (result.IsBlocked)
        {
            Debug.Log($"[Battle] {name} -> {target.name}: BLOCKED! Damage reduced by {result.BlockedDamage}");

            // Block 이펙트 재생
            await PlayBlockEffect(target);

            BattleUIManager.Instance.ShowBattleText(target, BattleTextType.Block);

        }


        // 6. Resist 처리 (스킬 효과만 저항, 데미지는 입음)
        if (result.IsResisted)
        {
            Debug.Log($"[Battle] {name} -> {target.name}: Skill effect RESISTED!");

            BattleUIManager.Instance.ShowBattleText(target, BattleTextType.Resist);

        }

        // 7. 데미지 적용 (Miss, Dodge, Immune가 아닌 경우)
        if (result.FinalDamage > 0)
        {
            await target.TakeDamageWithResult(result, this, baseEffectId);

        }


    }




    /// <summary>
    /// 각종 판정 이펙트 재생 메서드들
    /// </summary>
    private async UniTask PlayMissEffect(BattleActor target)
    {
        // Miss 이펙트 재생
        if (BattleEffectManager.Instance != null)
        {
            await BattleEffectManager.Instance.PlayEffect("MissEffect", this, target);
        }
    }

    private async UniTask PlayDodgeEffect(BattleActor target)
    {
        // Dodge 이펙트 재생
        if (BattleEffectManager.Instance != null)
        {
            await BattleEffectManager.Instance.PlayEffect("DodgeEffect", target, null);
        }
    }

    private async UniTask PlayBlockEffect(BattleActor target)
    {
        // Block 이펙트 재생
        if (BattleEffectManager.Instance != null)
        {
            await BattleEffectManager.Instance.PlayEffect("BlockEffect", target, null);
        }
    }

    private async UniTask PlayCriticalEffect(BattleActor target)
    {
        // Critical 이펙트 재생
        if (BattleEffectManager.Instance != null)
        {
            await BattleEffectManager.Instance.PlayEffect("CriticalHitEffect", target, null);
        }
    }

    private async UniTask PlayImmuneEffect(BattleActor target)
    {
        // Immune 이펙트 재생
        if (BattleEffectManager.Instance != null)
        {
            await BattleEffectManager.Instance.PlayEffect("ImmuneEffect", target, null);
        }
    }


    /// <summary>
    /// BP 차지 이펙트 표시 (기존 ShowEffect 메서드 활용)
    /// </summary>
    public async UniTask ShowBPChargeEffect(int bpLevel)
    {
        // 기존 이펙트 제거
        ClearBPEffect();

        // BP 레벨에 따른 이펙트 타입 결정
        string commonEffectName = "";
  
        switch (bpLevel)
        {
            case 1:
                commonEffectName = GetEffectId(BattleActorEffectType.ChargeBP1);
                break;
            case 2:
                commonEffectName = GetEffectId(BattleActorEffectType.ChargeBP2);
                break;
            case 3:
                commonEffectName = GetEffectId(BattleActorEffectType.ChargeBP3);
                break;
        }

        // 피격 이펙트 재생
        if (!string.IsNullOrEmpty(commonEffectName))
        {
            // 공격 이펙트에 연결된 피격 이펙트 재생
            currentBPEffectInstancce = await BattleEffectManager.Instance.PlayEffect(commonEffectName, this );
        }

        Debug.Log($"[BP] {name} charging {bpLevel} BP");
    }


    /// <summary>
    /// BP 이펙트 제거
    /// </summary>
    public void ClearBPEffect()
    {
        if (currentBPEffectInstancce != null)
        {
            currentBPEffectInstancce.effectObject.SetActive(false);
            currentBPEffectInstancce = null;
        }
    }

   
}