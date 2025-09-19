using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IronJade.Observer.Core;
using IronJade.Table.Data;
using System;
using BattleAI;
using System.Xml.Linq;
using BattleCharacterSystem;
using Cysharp.Threading.Tasks;


[Flags]
public enum StatusFlag
{
    None = 0,
    CannotAct = 1 << 0,        // 행동 불가
    CannotUseSkill = 1 << 1,   // 스킬 사용 불가
    CannotMove = 1 << 2,        // 이동 불가
    Sleeping = 1 << 3,          // 수면 상태
    Confused = 1 << 4,          // 혼란 상태
    Feared = 1 << 5,            // 공포 상태
    Taunted = 1 << 6,           // 도발 상태
}



[System.Serializable]
public partial class BattleCharInfoNew
{
    // ===== 테스트 모드 설정 추가 =====
    [Header("테스트 설정")]
    [SerializeField]
    public bool isTestMode = true;  // Inspector에서 설정 가능


    [SerializeField]
    public bool customStat = false;  // 커스텀 스탯 사용 여부


    [Header("테스트 모드 스탯 (isTestMode가 true일 때 사용)")]

    [SerializeField] private EAttackType testAttackType = EAttackType.Physical;


    [SerializeField] private int testHp = 1000;

    [SerializeField] private int testAttack = 100;

    [SerializeField] private int testDefence = 50;

    [SerializeField] private float testCriRate = 10f;

    [SerializeField] private float testCriDmg = 150f;

    [SerializeField] private float testTurnSpeed = 100f;

    [SerializeField] private int testAggo = 1; // 캐릭터 테이블에 나중에 어그로 추가. 


    // Monster/Boss 배수 설정
    [Header("=== Enemy Multipliers ===")]
    [SerializeField] private float monsterStatMultiplier = 1.2f;  // 몬스터 기본 배수
    [SerializeField] private float bossStatMultiplier = 2.0f;      // 보스 기본 배수

    private BattleCharacterDataSO _cachedCharacterData = null;
    private bool _isCharacterDataLoaded = false;

    private BattleMonsterDataSO _cachedMonsterData = null;
    private bool _isMonsterDataLoaded = false;

    public async UniTask LoadCharacterDataAsync()
    {
        if (_isCharacterDataLoaded && _cachedCharacterData != null)
        {
            return;
        }

        try
        {
            //SO_Ru_1001_asset
            _cachedCharacterData = await BattleDataLoader.GetCharacterDataAsync(resourceId, BattleDataLoader.GetDataAddressableKey(this));
            _isCharacterDataLoaded = true;

            if (_cachedCharacterData != null)
            {
                Debug.Log($"[BattleCharInfoNew] Loaded CharacterDataSO for {Name} (ID: {resourceId})");
            }
            else
            {
                Debug.LogWarning($"[BattleCharInfoNew] Failed to load CharacterDataSO for {Name} (ID: {resourceId})");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BattleCharInfoNew] Error loading CharacterDataSO: {e.Message}");
            _isCharacterDataLoaded = true; // 에러 시에도 재시도 방지
        }
    }

    public async UniTask LoadMonsterDataAsync()
    {
        if (_isMonsterDataLoaded && _cachedMonsterData != null)
        {
            return;
        }

        try
        {
            //SO_Ru_1001_asset
            _cachedMonsterData = await BattleDataLoader.GetMonsterDataAsync(resourceId, BattleDataLoader.GetDataAddressableKey(this));
            _isMonsterDataLoaded = true;

            if (_cachedMonsterData != null)
            {
                Debug.Log($"[BattleCharInfoNew] Loaded MonsterDataSO for {Name} (ID: {resourceId})");
            }
            else
            {
                Debug.LogWarning($"[BattleCharInfoNew] Failed to load MonsterDataSO for {Name} (ID: {resourceId})");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BattleCharInfoNew] Error loading MonsterDataSO: {e.Message}");
            _isMonsterDataLoaded = true; // 에러 시에도 재시도 방지
        }
    }




    public BattleCharacterDataSO CharacterDataSO
    {
        get
        {
            return _cachedCharacterData;
        }

    }
    public BattleMonsterDataSO MonsterDataSO
    {
        get
        {
            return _cachedMonsterData;
        }

    }

    // ==================================
    // === BP 스킬 테스트 필드 추가 ===
    [Header("BP 스킬 테스트 설정 (TestMode일 때)")]
    [SerializeField] private bool enableBPSkillTest = false;
    [SerializeField] private List<int> testBaseSkillIds = new List<int>();
    [SerializeField][Range(0, 5)] private int testBPLevel = 0;
    [SerializeField] private BPUpgradeDatabase testBPDatabase;

    // BP 테스트 관련 프로퍼티
    public bool EnableBPSkillTest => isTestMode && enableBPSkillTest;
    public List<int> TestBaseSkillIds => testBaseSkillIds;
    public int TestBPLevel => testBPLevel;
    public BPUpgradeDatabase TestBPDatabase => testBPDatabase;



    // 새 데이터 시스템용 필드 추가
    private string name = ""; //실제 SO파일에 적용된 이름 
    private string prefabName = "";
    private string rootName = ""; //ResourcesName
    private int resourceId = 0; // 기본값
    private string prefabPath = "";
    public string addressableKey = ""; //

    // 새 데이터 시스템용 setter
    public void SetPrefabInfo(string name, string prefab, string root, string addressable, int resId = 1)
    {
        this.name = name;
        prefabName = prefab;
        rootName = root;
        resourceId = resId;

        addressableKey = addressable;
    }

    // CreateBattleActor에서 사용할 getter
    public string GetName() => name;
    public string GetPrefabName() => prefabName;
    public string GetRootName() => rootName;
    public int GetResourceId() => resourceId;

    public string GetAddressableKey() => addressableKey;

    public string GetPrefabPath() => prefabPath;

    // 에디터 전용 테스트 메서드
#if UNITY_EDITOR

    /// <summary>
    /// BP 레벨 즉시 변경 (런타임 테스트용)
    /// </summary>
    [ContextMenu("Apply Test BP Level")]
    public void ApplyTestBPLevel()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("Play Mode에서만 실행 가능합니다.");
            return;
        }

       
        if (Owner?.BPManager != null)
        {
            Owner.BPManager.SetBP(testBPLevel);
           // Debug.Log($"[BP Test] {name} BP set to {testBPLevel}");
        }
    }

    /// <summary>
    /// 스킬 업그레이드 정보 출력
    /// </summary>
    [ContextMenu("Show BP Upgrade Info")]
    public void ShowBPUpgradeInfo()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("Play Mode에서만 실행 가능합니다.");
            return;
        }

        if (Owner?.BPManager != null)
        {
            Owner.BPManager.PrintBPStatus();

            foreach (int skillId in testBaseSkillIds)
            {
                var info = Owner.BPManager.GetNextUpgradeInfo(skillId);
                Debug.Log($"[BP Test] Skill {skillId}: {info}");
            }
        }
    }

    /// <summary>
    /// 첫 번째 스킬 테스트
    /// </summary>
    [ContextMenu("Test First Skill with BP")]
    public void TestFirstSkillWithBP()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("Play Mode에서만 실행 가능합니다.");
            return;
        }



        var skills = Owner.SkillManager.GetAllActiveSkills();
        if (skills.Count > 0)
        {
            var skill = skills[0];
            Debug.Log($"[BP Test] Testing skill {skill.SkillID} with BP Level {testBPLevel}");

            // 더미 타겟 (자기 자신)
            Owner.UseSkillWithBP(skill.SkillData, Owner, true);
        }
    }

#endif


    private StatusFlag statusFlags = StatusFlag.None;

    public void AddStatusFlag(StatusFlag flag)
    {
        statusFlags |= flag;
    }

    public void RemoveStatusFlag(StatusFlag flag)
    {
        statusFlags &= ~flag;
    }

    public bool HasStatusFlag(StatusFlag flag)
    {
        return (statusFlags & flag) != 0;
    }

    public bool CanAct()
    {
        return !HasStatusFlag(StatusFlag.CannotAct);
    }

    public bool CanUseSkill()
    {
        return !HasStatusFlag(StatusFlag.CannotUseSkill);
    }

    public BattleActor Owner{ get; private set; }
    public void SetOwner(BattleActor owner)
    {
        Owner = owner;
    }

    public string Name { get; private set; }
    public void SetName( string name )
    {
        Name = name; 
    }


    public int CharacterDataID { get; private set; }
    public int SlotIndex { get; private set; }
    public void SetSlotIndex( int slotIndex )
    {
        SlotIndex = slotIndex;
    }

    public int ID { get; private set; }


    public CharacterTier CharacterTier { get; private set; }

    public void SetCharacterTier(CharacterTier slotIndex)
    {
        CharacterTier = slotIndex;
    }

    public EAttackType AttackType { get; private set; }
    public void SetAttackType(EAttackType attackType )
    {
        AttackType = attackType;
    }

    public EAttackRange AttackRange { get; private set; }
    public void SetAttackRange(EAttackRange attackRange )
    {
        AttackRange = attackRange;
    }

    public ClassType ClassType { get; private set; }
    public void SetClassType(ClassType classType )
    {
        ClassType = classType;
    }

    public LicenseType LicenseType { get; private set; }
    public void SetLicenseType(LicenseType licenseType)
    {
        LicenseType = licenseType;
    }


    public int Hp { get; set; }
    public int MaxHp { get; private set; }
    public void SetMaxHp( int maxHp )
    {
        MaxHp = maxHp;
    }

    public void SetHp(int hp)
    {
        Hp = hp;
    }

    public float HpRatio => _ = Hp / MaxHp;


    public int Bp { get; private set; }
    public void SetCurrentBp(int currentBp)
    {
        Bp = currentBp;
    }

    public int MaxBp { get; private set; }
    
    public void SetMaxBp( int maxBp )
    {
        MaxBp = maxBp;
    }


    // BP 관련 추가 필드
    private int usedBPThisTurn = 0;
    private bool skipBPGainNextTurn = false;

    // BP 프로퍼티 추가
    public int CurrentBP => Bp;


    #region 추가해야 할 스탯들 (BattleFormularHelper에서 필요)


    /// <summary>
    /// 공격력 - Attack과 동일 (Atk로 별칭 제공)
    /// </summary>
    public int Atk => Attack;  // 기존 Attack을 Atk로도 접근 가능

    /// <summary>
    /// 방어력 - Defence와 동일 (Def로 별칭 제공)
    /// </summary>
    public int Def => Defence;  // 기존 Defence를 Def로도 접근 가능

    /// <summary>
    /// 명중률 (%) - 기본 공격 명중률
    /// </summary>
    public float HitRate { get; private set; } = 95f;  // 기본 95%

    /// <summary>
    /// 회피율 (%) - 기본 회피 확률
    /// </summary>
    public float DodgeRate { get; private set; } = 5f;  // 기본 5%

    /// <summary>
    /// 블록율 (%) - 데미지 경감 확률
    /// </summary>
    public float BlockRate { get; private set; } = 10f;  // 기본 10%

    /// <summary>
    /// 어그로 - 타겟 선택 가중치
    /// </summary>
    public int Aggro { get; private set; } = 100;  // 기본 100


    // 면역 타입 (CharacterTableData의 IMM_TYPE에서 가져옴)
    public string ImmunityType { get; set; } = "";

    // 스킬 적중/저항 (CharacterTableData에서 가져옴)
    public float SkillHitRate { get; set; } = 0.95f;    // 기본 95%
    public float SkillResistRate { get; set; } = 0.05f;  // 기본 5%


    // 필드 추가
    private float damageReductionPercent = 0f;

    // 프로퍼티
    public float DamageReductionPercent
    {
        get => damageReductionPercent;
        set => damageReductionPercent = Mathf.Clamp(value, 0f, 100f);
    }

    public int Shield { get; set; } = 0;  //실드 
    public bool HasShield => Shield > 0;

    // 필드 추가
    private float critNullifyChance = 0f;

    // 프로퍼티
    public float CritNullifyChance
    {
        get => critNullifyChance;
        set => critNullifyChance = Mathf.Clamp01(value > 1 ? value / 100f : value);
    }




    public void SetAggro( int aggro )
    {
        Aggro = aggro;
    }


    // 기존에 없는 필드만 추가
    public float CriticalResist { get; set; } = 0.0f;    // 크리티컬 저항
    public float VariantDamage { get; set; } = 0.1f;     // 데미지 변동률 (±10%)
    public float Cooperation { get; set; } = 0.0f;       // 협공 확률
    public float Counter { get; set; } = 0.0f;           // 반격 확률

    public float Lifesteal { get; set; } = 0.0f;           // 흡혈
    public float DefaultAggro { get; set; } = 100f;      // 초기 어그로 값
    public bool IsFrontFormation { get; set; } = false;  // 앞열 여부
    public bool IsMonster { get; set; } = false;         // 몬스터 여부
    public bool IsBoss { get; set; } = false;            // 보스 여부

    public void SetIsMonster( bool monster )
    {
        IsMonster = monster;
    }

    public void SetIsBoss(bool boss)
    {
        IsBoss = boss;
    }



    // Min/Max 데미지 (랜덤 범위용)
    public int MinDamage { get; set; } = 0;
    public int MaxDamage { get; set; } = 0;

    // 기본 데미지 값 (MinDamage와 MaxDamage가 0일 때 사용)
    public int Damage => Attack;  // Attack을 기본 데미지로 사용


    [Header("추가 전투 스탯")]
    private float penetration = 0f;        // 방어 관통 (0~100%)
    private float elementalAtk = 0f;       // 속성 공격력 증가 (0~100%)
    private float elementalRes = 0f;       // 속성 저항 (0~100%)
    private float damageReduce = 0f;       // 받는 피해 감소 (0~90%)
    private float healPower = 0f;          // 힐/실드 효과 증가 (-100~100%)
    private float blockPower = 50f;        // 블록시 피해 감소율 (20~80%)
    private float ccEnhance = 0f;          // CC 효과 증폭 (0~100%)
    private float tenacity = 0f;           // 강인함/디버프 시간 감소 (0~80%)







    #endregion

    #region 테스트 모드 스탯 추가 (Inspector에서 설정 가능)

    [Header("추가 테스트 스탯")]
    [SerializeField] private float testHitRate = 95f;
    [SerializeField] private float testDodgeRate = 5f;
    [SerializeField] private float testBlockRate = 10f;
    [SerializeField] private int testAggro = 100;

    #endregion
    #region 메서드 수정/추가

    [Header("진형 시스템")]
    public CharacterJobType JobType = CharacterJobType.None;

    // 진형 버프 관련 필드
    public float FormationAtkBuff = 1f;
    public float FormationDefBuff = 1f;
    public float FormationSpeedBuff = 1f;

    /// <summary>
    /// 진형 버프가 적용된 실제 공격력 계산
    /// </summary>
    public int GetBuffedAtk()
    {
        // 기본 공격력에 진형 버프 적용
        return Mathf.RoundToInt(Atk * FormationAtkBuff);
    }

    /// <summary>
    /// 진형 버프가 적용된 실제 방어력 계산
    /// </summary>
    public int GetBuffedDef()
    {
        // 기본 방어력에 진형 버프 적용
        return Mathf.RoundToInt(Def * FormationDefBuff);
    }

    /// <summary>
    /// 진형 버프가 적용된 실제 속도 계산
    /// </summary>
    public float GetBuffedTurnSpeed()
    {
        // 기본 속도에 진형 버프 적용
        return TurnSpeed * FormationSpeedBuff;
    }

    /// <summary>
    /// 진형 버프 초기화
    /// </summary>
    public void ResetFormationBuffs()
    {
        FormationAtkBuff = 1f;
        FormationDefBuff = 1f;
        FormationSpeedBuff = 1f;
    }

    /// <summary>
    /// 진형 버프 적용
    /// </summary>
    public void ApplyFormationBuff(float atkBuff, float defBuff, float speedBuff)
    {
        FormationAtkBuff = atkBuff;
        FormationDefBuff = defBuff;
        FormationSpeedBuff = speedBuff;
    }

    /// <summary>
    /// 기존 ApplyTestStats 메서드에 추가할 내용
    /// </summary>
    /// 
    /// <summary>
    /// 커스텀 테스트 스탯 적용 (customStat = true)
    /// </summary>
    private void ApplyCustomTestStats()
    {
        AttackType = testAttackType;

        // Inspector에서 설정한 테스트 값 적용
        Attack = testAttack;
        Defence = testDefence;
        MaxHp = testHp;
        Hp = MaxHp;

        CriRate = testCriRate;
        CriDmg = testCriDmg;
        TurnSpeed = testTurnSpeed + UnityEngine.Random.Range(-10f, 10f); // 약간의 랜덤성 추가

        // 캐릭터 리소스 경로 설정
        SetDefalutCharacterPrefab();

        // 데미지 트래커 초기화
        if (DamageTracker == null)
        {
            DamageTracker = new BattleDamageTracker();
        }
        DamageTracker.Init();

        Debug.Log($"[CustomStat Applied] HP:{MaxHp}, ATK:{Attack}, DEF:{Defence}, SPD:{TurnSpeed:F0}");
    }

    /// <summary>
    /// 계산된 테스트 스탯 적용 (customStat = false)
    /// CharacterTier, ClassType, Level에 따른 스탯 계산
    /// </summary>
    private void ApplyCalculatedTestStats()
    {
        // 기본 스탯 계산 (티어, 클래스, 레벨 기반)
        int level = Character?.Level ?? 1;

        // 티어별 기본 스탯 배수
        float tierMultiplier = CharacterTier switch
        {
            CharacterTier.XA => 1.5f,
            CharacterTier.X => 1.3f,
            CharacterTier.S => 1.1f,
            CharacterTier.A => 1.0f,
            _ => 1.0f
        };

        // 클래스별 스탯 분배
        float atkRatio = 1.0f;
        float defRatio = 1.0f;
        float hpRatio = 1.0f;
        float speedRatio = 1.0f;

        switch (ClassType)
        {
            case ClassType.Vanguard:  // 방어형
                atkRatio = 0.8f;
                defRatio = 1.3f;
                hpRatio = 1.2f;
                speedRatio = 0.9f;
                break;
            case ClassType.Slaughter: // 공격형
                atkRatio = 1.3f;
                defRatio = 0.9f;
                hpRatio = 1.0f;
                speedRatio = 1.1f;
                break;
            case ClassType.Jacker:    // 특수형
                atkRatio = 1.1f;
                defRatio = 0.8f;
                hpRatio = 0.9f;
                speedRatio = 1.3f;
                break;
            case ClassType.Rewinder:  // 지원형
                atkRatio = 0.9f;
                defRatio = 1.0f;
                hpRatio = 1.1f;
                speedRatio = 1.0f;
                break;
        }

        // 레벨 기반 기본 스탯 계산
        int baseHp = 500 + (level * 100);
        int baseAtk = 50 + (level * 10);
        int baseDef = 25 + (level * 5);
        float baseSpeed = 100 + (level * 2);

        // 최종 스탯 계산 (티어, 클래스 배수 적용)
        MaxHp = (int)(baseHp * hpRatio * tierMultiplier);
        Hp = MaxHp;
        Attack = (int)(baseAtk * atkRatio * tierMultiplier);
        Defence = (int)(baseDef * defRatio * tierMultiplier);
        TurnSpeed = baseSpeed * speedRatio * tierMultiplier + UnityEngine.Random.Range(-10f, 10f);

        // 크리티컬 스탯 (티어 기반)
        CriRate = 5.0f + (tierMultiplier * 2);
        CriDmg = 1.5f + (tierMultiplier * 0.1f);

        // 공격 타입 설정
        AttackType = (ClassType == ClassType.Rewinder || ClassType == ClassType.Jacker)
            ? EAttackType.Magical : EAttackType.Physical;

        // 캐릭터 리소스 경로 설정
        SetDefalutCharacterPrefab();

        // 데미지 트래커 초기화
        if (DamageTracker == null)
        {
            DamageTracker = new BattleDamageTracker();
        }
        DamageTracker.Init();

        Debug.Log($"[Calculated Stats] Tier:{CharacterTier}, Class:{ClassType}, Level:{level}");
        Debug.Log($"[Calculated Stats] HP:{MaxHp}, ATK:{Attack}, DEF:{Defence}, SPD:{TurnSpeed:F0}");
    }

    /// <summary>
    /// Enemy일 경우 Monster/Boss 배수 적용
    /// </summary>
    private void ApplyEnemyMultipliers()
    {
        float multiplier = 1.0f;

        if (IsBoss)
        {
            // 보스 배수 적용
            multiplier = bossStatMultiplier;
            Debug.Log($"[Boss Multiplier Applied] x{multiplier}");
        }
        else if (IsMonster)
        {
            // 일반 몬스터 배수 적용
            multiplier = monsterStatMultiplier;
            Debug.Log($"[Monster Multiplier Applied] x{multiplier}");
        }

        // 스탯에 배수 적용
        MaxHp = (int)(MaxHp * multiplier);
        Hp = MaxHp;
        Attack = (int)(Attack * multiplier);
        Defence = (int)(Defence * multiplier);
        // 속도는 배수 적용하지 않거나 적게 적용
        TurnSpeed = TurnSpeed * (1 + (multiplier - 1) * 0.3f); // 30%만 적용

        Debug.Log($"[Enemy Stats After Multiplier] HP:{MaxHp}, ATK:{Attack}, DEF:{Defence}, SPD:{TurnSpeed:F0}");
    }




    private void ApplyTestStats_Extended()  // 기존 ApplyTestStats()에 추가
    {
        HitRate = testHitRate;
        DodgeRate = testDodgeRate;
        BlockRate = testBlockRate;
        Aggro = testAggro;
    
    }

    /// <summary>
    /// 실제 전투 데이터 설정 시 추가 스탯 초기화
    /// SetBattleCharInfo 메서드에 추가할 내용
    /// </summary>
    public void Initialize( BattleActor owner )
    {
        // Character 데이터에서 추가 스탯 가져오기 (있다면)
        // 없으면 기본값 사용
        this.Owner = owner;

        //So방식이 아닐경우. 
        if (resourceId == 0)
        {
            LoadAdditionalStats(this.Character.CharacterTableData);

            InitializeClassBonusStats();
        }

    }


    //임시 설정 
    public void InitializeClassBonusStats()
    {
        // ClassType은 이미 LoadAdditionalStats에서 설정됨
        switch (ClassType)
        {
            case ClassType.Vanguard:  // 방어형
                // 기존 스탯 보너스
                BlockRate += 15f;
                SetAggro(150);

                // 새로운 스탯 보너스
                blockPower = 60f;      // 블록시 60% 피해 감소
                tenacity = 20f;        // 디버프 20% 감소
                damageReduce = 5f;     // 기본 피해 5% 감소
                break;

            case ClassType.Slaughter: // 공격형
                // 기존 스탯 보너스
                CriRate += 5f;
                CriDmg += 20f;

                // 새로운 스탯 보너스
                penetration = 10f;     // 방어 관통 10%
                elementalAtk = 5f;     // 속성 공격 5% 증가
                break;

            case ClassType.Jacker:    // 특수형
                // 기존 스탯 보너스
                DodgeRate += 10f;
                HitRate = 100f;
                Counter += 10f;

                // 새로운 스탯 보너스
                penetration = 5f;      // 약간의 관통
                ccEnhance = 10f;       // CC 효과 10% 증가
                break;

            case ClassType.Rewinder:  // 지원형
                // 기존 스탯 보너스
                SkillHitRate += 10f;

                // 새로운 스탯 보너스
                healPower = 25f;       // 힐 효과 25% 증가
                elementalAtk = 10f;    // 속성 공격 10% 증가
                ccEnhance = 15f;       // CC 효과 15% 증가
                elementalRes = 10f;    // 속성 저항 10%
                break;
        }
    }




    /// <summary>
    /// 데미지 받기 메서드 (BattleFormularHelper와 연동)
    /// </summary>
    public void TakeDamage(int damage)
    {
        int actualDamage = Mathf.Max(0, damage);
        Hp = Mathf.Max(0, Hp - actualDamage);

        // 데미지 트래커가 있다면 기록
        if (DamageTracker != null)
        {
            DamageTracker.AddDamage(actualDamage);
        }

        Debug.Log($"[Damage] {Name} took {actualDamage} damage. HP: {Hp}/{MaxHp}");
    }

    /// <summary>
    /// 힐 받기 메서드
    /// </summary>
    public void Heal(int amount)
    {
        int actualHeal = Mathf.Min(amount, MaxHp - Hp);
        Hp = Mathf.Min(MaxHp, Hp + actualHeal);

        Debug.Log($"[Heal] {Name} healed {actualHeal}. HP: {Hp}/{MaxHp}");
    }



    #endregion



    // 속성 상성 체크 메서드
    public bool IsAdvantage(EBattleElementType attacker, EBattleElementType defender)
    {
        // 속성 상성표: Power > Bio > Chemical > Plasma > Electrical > Network > Power
        return attacker switch
        {
            EBattleElementType.Power => defender == EBattleElementType.Bio,
            EBattleElementType.Bio => defender == EBattleElementType.Chemical,
            EBattleElementType.Chemical => defender == EBattleElementType.Plasma,
            EBattleElementType.Plasma => defender == EBattleElementType.Electrical,
            EBattleElementType.Electrical => defender == EBattleElementType.Network,
            EBattleElementType.Network => defender == EBattleElementType.Power,
            _ => false
        };
    }

    public bool IsDisadvantage(EBattleElementType attacker, EBattleElementType defender)
    {
        return IsAdvantage(defender, attacker);
    }

    // 테이블 데이터에서 추가 스탯 로드
    public void LoadAdditionalStats(CharacterTableData characterData)
    {
        // CharacterTableData에서 값 가져오기
        if (characterData.IsNull() == false)
        {
            // Hit과 Dodge는 이미 HitRate, DodgeRate로 존재
            // HitRate = characterData.GetHIT_VALUE();  // 이미 있음
            // DodgeRate = characterData.GetDODGE_VALUE(); // 이미 있음

            CharacterTier = (CharacterTier)characterData.GetTIER();

            AttackType = (EAttackType)characterData.GetATTACK_TYPE();
            ClassType = (ClassType)characterData.GetCLASS();
            AttackRange = (EAttackRange)characterData.GetATTACK_RANGE();
            LicenseType = (LicenseType)characterData.GetLICENSE();

            // 크리티컬 관련
            CriRate = characterData.GetC_HIT_CHANCE();
            CriDmg = characterData.GetC_HIT_DAMAGE();

            // Min/Max 데미지
            MinDamage = characterData.GetMIN_DAMAGE();
            MaxDamage = characterData.GetMAX_DAMAGE();


            // 속도는 TurnSpeed의 일부를 사용하거나 별도 계산

            TurnSpeed = characterData.GetATTACK_SPEED();


            // 추가 필드 로드
            ImmunityType = characterData.GetIMM_TYPE();
            SkillHitRate = characterData.GetSKILL_HIT_RATE();
            SkillResistRate = characterData.GetSKILL_RESIST_RATE();




            // 추가 스탯들은 테이블에 없으면 기본값 사용
            // Cooperation, Counter, VariantDamage 등
        }
    }



    /// <summary>
    /// 타겟 공격 가능 여부 체크
    /// </summary>
    public bool CanAttackTarget(BattleActor target, List<BattleActor> allEnemies)
    {
        if (target == null || target.IsDead)
            return false;

        // Special과 Range는 모든 타겟 공격 가능
        if (AttackRange == EAttackRange.Special || AttackRange == EAttackRange.Range)
            return true;

        // Melee인 경우 전열 체크
        if (AttackRange == EAttackRange.Melee)
        {
            // 타겟이 전열인지 확인
            bool isTargetFrontRow = IsInFrontRow(target);
            if (isTargetFrontRow)
                return true;  // 전열은 항상 공격 가능

            // 타겟이 후열인 경우, 전열에 살아있는 적이 있는지 확인
            bool hasFrontRowEnemies = HasAliveFrontRowEnemies(allEnemies);
            if (hasFrontRowEnemies)
            {
                Debug.Log($"[Attack] {Name} (Melee) cannot attack back row - front row enemies exist!");
                return false;  // 전열에 적이 있으면 후열 공격 불가
            }

            return true;  // 전열에 적이 없으면 후열 공격 가능
        }

        return true;
    }

    /// <summary>
    /// 전열 위치 확인
    /// </summary>
    private bool IsInFrontRow(BattleActor actor)
    {
        if (actor == null || actor.BattleActorInfo == null)
            return false;

        int slotIndex = actor.BattleActorInfo.SlotIndex;

        // 진형에 따라 전열 슬롯이 다름
        // BattlePositionNew에서 진형 정보를 가져와야 함
        if (BattleProcessManagerNew.Instance?.battlePosition != null)
        {
            var frontSlots = BattleProcessManagerNew.Instance.battlePosition
                .GetFrontRowSlots(actor.BattleActorInfo.IsAlly);
            return frontSlots.Contains(slotIndex);
        }

        // 기본값: 슬롯 0,1이 전열
        return slotIndex <= 1;
    }

    /// <summary>
    /// 살아있는 전열 적 존재 여부 확인
    /// </summary>
    private bool HasAliveFrontRowEnemies(List<BattleActor> enemies)
    {
        foreach (var enemy in enemies)
        {
            if (enemy != null && !enemy.IsDead && IsInFrontRow(enemy))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 공격 범위에 따른 데미지 계수 계산
    /// </summary>
    public float GetRangeDamageModifier(BattleActor target)
    {
        // Range 타입이 전열 공격 시 -20% 페널티
        if (AttackRange == EAttackRange.Range && IsInFrontRow(target))
        {
            Debug.Log($"[Attack] {Name} (Range) attacking front row - 20% damage penalty applied");
            return 0.8f;  // 80% 데미지 (20% 감소)
        }

        return 1.0f;  // 100% 데미지
    }


    // 치명타 무효화 체크
    public bool CheckAndNullifyCrit()
    {
        if (critNullifyChance <= 0) return false;

        bool nullified = UnityEngine.Random.value < critNullifyChance;

        if (nullified)
        {
            Debug.Log($"[CritNullify] 치명타 무효화 발동! ({critNullifyChance * 100:F0}% 확률)");
        }

        return nullified;
    }


    public void IncreaseBP()
    {
        Bp++;
        Bp = Mathf.Clamp(Bp, 0, MaxBp);

       // Debug.LogError("#### IncreaseBP() " + Bp);
    }

    public void ReduceBP()
    {
        Bp -= 1;

        Bp = Mathf.Clamp(Bp, 0, MaxBp);

     //   Debug.LogError("#### ReduceBP() " + Bp );

    }


    public int UsedBPThisTurn => usedBPThisTurn;

    public void ResetBPThisTurn()
    {
        usedBPThisTurn = 0;
    }

    public void SetBPThisTurn( int bp)
    {
        usedBPThisTurn = bp;
    }

    public bool SkipBPGainNextTurn => skipBPGainNextTurn;




    public bool IsDead { get { return Hp <= 0; } }
    public CharacterTableData CharacterData { get; private set; }

    public ICharacterBaseData CharacterBaseData { get { return characterBaseData; } }
    public Character Character { get; private set; }

    public MonsterTableData MonsterData { get; private set; }


    // public BattleElementType AttackElement { get; private set; } //공격 속성.

    //   public BattleElementType[] WeakElement { get; private set; } //수호석 정보.  

    public EBattleElementType Element;

    public EBattleElementType[] GuardianStoneElement;


    [CustomEnumPopup]
    public CharacterDefine CharacterDataEnum = CharacterDefine.None;

    [CustomEnumPopup]
    public MonsterDefine MonsterDataEnum = MonsterDefine.None;

 

    public bool IsAlly { get; private set; }
    public void SetIsAlly( bool isAlly )
    {
        IsAlly = isAlly;
    }

    public int Attack { get; private set; }
    public void SetAttack( int attack )
    {
        Attack = attack;
    }
    public int Defence { get; private set; }
    public void SetDefence( int defence )
    {
        Defence = defence;
    }


    public float CriRate { get; private set; }
    public float CriDmg { get; private set; }
    public float PenaltyValue { get; private set; } = 1f;

    private float baseHp = 0;
    private float baseAttack = 0;
    private float baseDefence = 0;

    public float TurnSpeed { get; private set; }
    public void SetTurnSpeed( float turnSpeed )
    {
        TurnSpeed = turnSpeed;
    }


    public void SetCriDmg( float value )
    {
        CriDmg = value;
    }

    public void SetCriRate( float value )
    {
        CriRate = value;
    }

    public void SetHitRate(float value)
    {
        HitRate = value;
    }
    public void SetDodgeRate(float value)
    {
        DodgeRate = value;

    }
    public void SetBlockRate(float value)
    {
        BlockRate = value;

    }
    public void SetSkillHitRate(float value)
    {
        SkillHitRate = value;

    }

    public void SetSkillResistRate(float value)
    {
        SkillResistRate = value;

    }

    public void SetElementType(EBattleElementType value)
    {

        Element = value;

    }





    private ICharacterBaseData characterBaseData;

    public string CharacterPrefabFullPath { get; private set; }


    public string[] CharacterDeadVoicePaths { get; private set; }
    public string[] CharacterBattleResultWinVoice { get; private set; }
    public string[] CharacterBattleResultLoseVoice { get; private set; }
    public string[] CharacterBattleStackEffectPath { get; private set; }


    public BattleDamageTracker DamageTracker { get; private set; }






    /// <summary>
    /// 테스트 모드에 따라 적절한 초기화 메서드를 호출합니다.
    /// </summary>
    public void InitializeCharacterInfo(bool isAlly, int id, int slotIndex, Character character, BattleInfoNew battleInfo, int waveIndex = 0)
    {
        if (isTestMode)
        {
            // 테스트 모드: 더미 데이터 사용
            SetTestDummyData(isAlly, id, slotIndex, character, battleInfo);

            if (customStat)
            {
                // customStat이 true일 때만 Inspector 값 사용
                ApplyCustomTestStats();
            }
            else
            {
                // customStat이 false일 때는 티어/클래스/레벨 기반 스탯 계산
                ApplyCalculatedTestStats();
            }

            // Enemy일 경우 배수 적용
            if (!isAlly)
            {
                ApplyEnemyMultipliers();
            }


            ApplyTestStats_Extended();
        }
        else
        {
            // 실제 전투 모드: 전체 데이터 설정
            SetBattleCharInfo(isAlly, slotIndex, character, battleInfo, waveIndex);
        }

        //이름 세팅 / 영어일경우만
        Name = Capitalize(TableManager.Instance.GetLocalization(CharacterData.GetNAME()));
    }


  


    public string Capitalize(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToUpper(input[0]) + input.Substring(1).ToLower();
    }



    /// <summary>
    /// 캐릭터의 속성을 설정합니다.
    /// </summary>
    public void SetElement(EBattleElementType element)
    {
        Element = element;
      //  Debug.Log($"[Element] {CharacterData?.GetNAME()} set to {element}");
    }

    /// <summary>
    /// 테스트용 더미 데이터 설정시 속성도 설정
    /// </summary>
    public void SetTestDummyDataWithElement(bool isAlly, int id, int slotIndex, Character character, BattleInfoNew battleInfo, EBattleElementType element)
    {
        SetTestDummyData(isAlly, id, slotIndex, character, battleInfo);
        SetElement(element);
    }

    /// <summary>
    /// 대상과의 속성 상성을 계산합니다.
    /// </summary>
    public float GetElementAdvantageAgainst(BattleCharInfoNew target)
    {
        if (target == null) return 1.0f;
        return BattleElementHelper.GetElementMultiplier(this.Element, target.Element);
    }

    /// <summary>
    /// 대상과의 속성 관계를 텍스트로 반환합니다.
    /// </summary>
    public string GetElementRelationAgainst(BattleCharInfoNew target)
    {
        if (target == null) return "Normal";
        return BattleElementHelper.GetElementRelation(this.Element, target.Element);
    }




    /// <summary>
    /// BP 초기화 (전투 시작 시)
    /// </summary>
    public void InitializeBP()
    {
        Bp = 0;  // 모든 캐릭터는 BP 0로 시작 어짜피 첫턴에는 1이 된다. 
        MaxBp = 5;  // 최대 BP는 5
        usedBPThisTurn = 0;
        skipBPGainNextTurn = false;
    }

    /// <summary>
    /// 턴 시작 시 BP 증가
    /// </summary>
    public void OnTurnStartBP()
    {
        if (!skipBPGainNextTurn && Bp < MaxBp)
        {
            Bp++;
            Debug.Log($"[BP] {Name} BP increased to {Bp}/{MaxBp}");
        }
        else if (skipBPGainNextTurn)
        {
            skipBPGainNextTurn = false;  // 스킵 플래그 리셋
            Debug.Log($"[BP] {Name} BP gain skipped this turn");
        }

        // 턴 시작 시 사용한 BP 리셋
        usedBPThisTurn = 0;
    }

    /// <summary>
    /// BP 사용
    /// </summary>
    public bool UseBP(int amount = 1)
    {
        // 최대 3개까지만 한 번에 사용 가능
        if (amount > 3)
        {
            Debug.LogWarning($"[BP] Cannot use more than 3 BP at once");
            return false;
        }

        if (Bp >= amount)
        {
            Bp -= amount;
            usedBPThisTurn += amount;
            skipBPGainNextTurn = true;  // 다음 턴 BP 증가 스킵

            Debug.Log($"[BP] {Name} used {amount} BP. Remaining: {Bp}/{MaxBp}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// BP 추가 (아이템이나 스킬 효과용)
    /// </summary>
    public void AddBP(int amount)
    {
        int oldBP = Bp;
        Bp = Mathf.Min(Bp + amount, MaxBp);

        if (Bp != oldBP)
        {
            Debug.Log($"[BP] {Name} gained {Bp - oldBP} BP. Current: {Bp}/{MaxBp}");
        }
    }


    public void AddHp(int value)
    {
        int previousHp = Hp;
        //IronJade.Debug.Log($"[Heal AddHp 1] hp = {Hp} / heal = {value}");

        int clacHP = Hp + value > MaxHp ? MaxHp : Hp + value;
        Hp = clacHP;

        //IronJade.Debug.Log($"[Heal AddHp 2] hp = {Hp} / heal = {value}");

        DamageTracker.RecieveHealing(Hp - previousHp);

      //  NotifyObserverUpdateHp();
    }


    public void AddStatBuffValue(CharacterStatType affectStat, float buffValue)
    {
        float currentValue = GetStatFloat(affectStat);
        float sign = 1.0f;

        // 고정값 스탯과 퍼센트 스탯 구분
        if (IsPercentageStat(affectStat))
        {
            // 퍼센트 스탯: 곱연산
            float multiplier = 1f + buffValue;
            ChangeStatBuffValue(affectStat, currentValue * multiplier);
        }
        else
        {
            // 고정값 스탯: 덧셈
            float addValue = IsBuffValueByInteger(affectStat) ?
                (int)buffValue : buffValue;
            ChangeStatBuffValue(affectStat, currentValue + addValue);
        }

        Debug.Log($"[Buff] {affectStat} changed: {currentValue} -> {GetStatFloat(affectStat)} (buff: {buffValue})");
    }

    public void RemoveStatBuffValue(CharacterStatType affectStat, float buffValue)
    {
        float currentValue = GetStatFloat(affectStat);

        if (IsPercentageStat(affectStat))
        {
            // 퍼센트 스탯: 역곱연산
            float multiplier = 1f + buffValue;
            if (multiplier != 0)
            {
                ChangeStatBuffValue(affectStat, currentValue / multiplier);
            }
        }
        else
        {
            // 고정값 스탯: 뺄셈
            float removeValue = IsBuffValueByInteger(affectStat) ?
                (int)buffValue : buffValue;
            ChangeStatBuffValue(affectStat, currentValue - removeValue);
        }

        Debug.Log($"[Buff] {affectStat} restored: {currentValue} -> {GetStatFloat(affectStat)} (removed: {buffValue})");
    }

    /// <summary>
    /// 퍼센트로 적용되는 스탯인지 확인
    /// </summary>
    private bool IsPercentageStat(CharacterStatType statType)
    {
        return statType switch
        {
            CharacterStatType.Attack => true,
            CharacterStatType.Defence => true,
            CharacterStatType.MaxHp => true,
            CharacterStatType.TurnSpeed => true,
            CharacterStatType.CriDmg => true,
            CharacterStatType.HealPower => true,
            _ => false
        };
    }

    private bool IsBuffValueByInteger(CharacterStatType characterStatType)
    {
        switch (characterStatType)
        {
            case CharacterStatType.Attack:
            case CharacterStatType.Defence:
            case CharacterStatType.Hp:
            case CharacterStatType.MaxHp:
            case CharacterStatType.CriRate:
            case CharacterStatType.CriDmg:
            case CharacterStatType.Hit:
                return true;

            default:
                return false;
        }
    }

    private void ChangeStatBuffValue(CharacterStatType affectStat, float value)
    {
        switch (affectStat)
        {
            case CharacterStatType.Attack:
                Attack = (int)value;
                break;
            case CharacterStatType.Defence:
                Defence = (int)value;
                break;
            case CharacterStatType.Hp:
                Hp = (int)value;
                break;
            case CharacterStatType.MaxHp:
                MaxHp = (int)value;
                if (Hp > MaxHp) Hp = MaxHp;  // HP가 MaxHP를 초과하지 않도록
                break;
            case CharacterStatType.CriRate:
                CriRate = value;
                break;
            case CharacterStatType.CriDmg:
                CriDmg = value;
                break;

            // 새로운 스탯들 처리
            case CharacterStatType.Penetration:
                penetration = Mathf.Clamp(value, 0, 100f);  // 최대 100%
                break;
            case CharacterStatType.ElementalAtk:
                elementalAtk = Mathf.Clamp(value, -100f, 200f);  // -100% ~ 200%
                break;
            case CharacterStatType.ElementalRes:
                elementalRes = Mathf.Clamp(value, -100f, 100f);  // -100% ~ 100%
                break;
            case CharacterStatType.DamageReduce:
                damageReduce = Mathf.Clamp(value, -100f, 90f);  // -100% ~ 90%
                break;
            case CharacterStatType.HealPower:
                healPower = Mathf.Clamp(value, -100f, 200f);  // -100% ~ 200%
                break;
            case CharacterStatType.BlockPower:
                blockPower = Mathf.Clamp(value, 20f, 80f);  // 20% ~ 80%
                break;
            case CharacterStatType.CCEnhance:
                ccEnhance = Mathf.Clamp(value, -100f, 100f);  // -100% ~ 100%
                break;
            case CharacterStatType.Tenacity:
                tenacity = Mathf.Clamp(value, 0, 80f);  // 최대 80%
                break;
        }
    }


    public int GetCurrentStat(CharacterStatType statType)
    {
        switch (statType)
        {
            case CharacterStatType.Attack:
                return Attack;

            case CharacterStatType.Defence:
                return Defence;

            case CharacterStatType.Hp:
                return Hp;

            case CharacterStatType.MaxHp:
                return MaxHp;

            case CharacterStatType.HpRate:
                float fHp = Hp;
                float fMaxHp = MaxHp;

                return (int)(fHp / fMaxHp * 100f);

            case CharacterStatType.CriDmg:
                return (int)CriDmg;

            case CharacterStatType.CriRate:
                return (int)CriRate;

            case CharacterStatType.Hit:
                return (int)HitRate;

            case CharacterStatType.TurnSpeed:
                return (int)TurnSpeed;

            case CharacterStatType.Penetration:
                return (int)penetration;
            case CharacterStatType.ElementalAtk:
                return (int)elementalAtk;
            case CharacterStatType.ElementalRes:
                return (int)elementalRes;
            case CharacterStatType.DamageReduce:
                return (int)damageReduce;
            case CharacterStatType.HealPower:
                return (int)healPower;
            case CharacterStatType.BlockPower:
                return (int)blockPower;
            case CharacterStatType.CCEnhance:
                return (int)ccEnhance;
            case CharacterStatType.Tenacity:
                return (int)tenacity;


            default:
                return 0;
        }
    }


    // float 값이 필요한 경우를 위한 메서드
    public float GetStatFloat(CharacterStatType statType)
    {
        switch (statType)
        {
            // 기존 float 스탯들
            case CharacterStatType.CriRate:
                return CriRate;
            case CharacterStatType.CriDmg:
                return CriDmg;
            case CharacterStatType.Hit:
                return HitRate;
            case CharacterStatType.Dodge:
                return DodgeRate;
            case CharacterStatType.Block:
                return BlockRate;
            case CharacterStatType.SkillHit:
                return SkillHitRate;
            case CharacterStatType.SkillResist:
                return SkillResistRate;
            case CharacterStatType.Counter:
                return Counter;
            case CharacterStatType.LifeSteal:
                return Lifesteal;

            // 새로운 float 스탯들
            case CharacterStatType.Penetration:
                return penetration;
            case CharacterStatType.ElementalAtk:
                return elementalAtk;
            case CharacterStatType.ElementalRes:
                return elementalRes;
            case CharacterStatType.DamageReduce:
                return damageReduce;
            case CharacterStatType.HealPower:
                return healPower;
            case CharacterStatType.BlockPower:
                return blockPower;
            case CharacterStatType.CCEnhance:
                return ccEnhance;
            case CharacterStatType.Tenacity:
                return tenacity;

            // int 스탯들은 float로 변환
            case CharacterStatType.Attack:
                return Attack;
            case CharacterStatType.Defence:
                return Defence;
            case CharacterStatType.Hp:
                return Hp;
            case CharacterStatType.MaxHp:
                return MaxHp;

            default:
                return GetCurrentStat(statType);
        }
    }






    public float GetStat(AppliableType appliableType, CharacterStatType characterStatType)
    {
        float statValue = 0f;

        if (appliableType == AppliableType.DefaultStat)
        {
            if (characterStatType == CharacterStatType.Attack || characterStatType == CharacterStatType.Defence || characterStatType == CharacterStatType.Hp)
            {
                float fValue = Character.GetFinalStat(characterStatType).FloatStatValue * PenaltyValue;

                statValue = (int)Math.Round(fValue);
            }
            else
            {
                statValue = Character.GetFinalStat(characterStatType).FloatStatValue;
            }
        }
        else if (appliableType == AppliableType.CurrentStat)
        {
            statValue = GetCurrentStat(characterStatType);
        }

        return statValue;
    }





    public float ElementyTypeAdvantageDamageValue { get; private set; } = 1f;

    private void AddElementyTypeAdvantageDamageValue(float addValue)
    {
        ElementyTypeAdvantageDamageValue += addValue;
    }

    private void SetElementyTypeAdvantageDamageValue(bool isAcceptAdvantage)
    {
        ElementyTypeAdvantageDamageValue = 1f;

        if (!isAcceptAdvantage)
            return;

        BalanceTable balanceTable = TableManager.Instance.GetTable<BalanceTable>();
        BalanceTableData balanceTableData = balanceTable.GetDataByID((int)BalanceDefine.BALANCE_ELEMENT_COMPATIBILITY_DAMAGE_VALUE);

        if (!balanceTableData.IsNull() && balanceTableData.GetINDEXCount() > 0)
        {
            ElementyTypeAdvantageDamageValue += (float)balanceTableData.GetINDEX(0);
        }
    }


    public void SetPenaltyValue(BattleInfo battleInfo)
    {
        PenaltyValue = 1f;

        if (IsAlly)
        {
            if (battleInfo.BattleDungeonType == DungeonType.Arena)
            {
                if (battleInfo.IsArena5VS5)
                {
                    PenaltyValue = 1f - battleInfo.GetPowerBalanceValue(battleInfo.CurrentWave);
                }
                else
                {
                    PenaltyValue = 1f - battleInfo.GetPowerBalanceValue(0);
                }
            }
            else
            {
                //PenaltyValue = 1f - battleInfo.GetPowerBalanceValue(0);
                PenaltyValue = battleInfo.GetPowerBalanceValue(0);
            }
        }
    }


    public void SetTestDummyData(bool isAlly, int id, int slotIndex, Character character, BattleInfoNew battleInfo)
    {
        ID = id;
        SlotIndex = slotIndex;
        IsAlly = isAlly;
        Character = character;


        //현재는 캐릭터만 본다. 
      //  if (targetEntity.Type == ViewerEntityType.Character)
        {
            CharacterTable characterTable = TableManager.Instance.GetTable<CharacterTable>();
            CharacterData = characterTable.GetDataByID(character.DataId);
            characterBaseData = new CharacterDataWrapper(CharacterData);
        }
      /*  else
        {
            MonsterTable monsterTable = TableManager.Instance.GetTable<MonsterTable>();
            MonsterData = monsterTable.GetDataByID(character.DataId);
            characterBaseData = new CharacterDataWrapper(MonsterData);
        }*/




        /*if (WeakInfo == null)
        {
            WeakInfo = new BattleWeakInfo();
        }

        if (battleInfo.BattleDungeonType == DungeonType.Arena)
        {
            WeakInfo.SetWeakElement(Character.Element);
            WeakInfo.SetWeakPointType((BreakType)Character.WeakPoint);
            WeakInfo.SetAttackWeakPointType(Character.AttackWeakPointType);
        }
        else
        {
            if (!IsAlly)
            {
                WeakInfo.SetWeakElementByDungeonWeakElement(battleInfo.WeakElement);
                WeakInfo.SetAttackWeakPointType(Character.AttackWeakPointType);
            }
            else
            {
                WeakInfo.SetWeakElement(Character.Element);
                WeakInfo.SetWeakPointType((BreakType)Character.WeakPoint);
                WeakInfo.SetAttackWeakPointType(Character.AttackWeakPointType);
            }
        }*/

    }


    public void SetBattleCharInfo(bool isAlly, int slotIndex, Character character, BattleInfoNew battleInfo, int waveIndex)
    {
        ID = character.Id;
        SlotIndex = slotIndex;
        IsAlly = isAlly;
        Character = character;


        CharacterTable characterTable = TableManager.Instance.GetTable<CharacterTable>();
        CharacterData = characterTable.GetDataByID(character.DataId);
        CharacterDataID = CharacterData.GetID();
        characterBaseData = new CharacterDataWrapper(CharacterData);

 
        SetDefalutCharacterPrefab();

        if (isAlly || character.IsBoss)
        {
            if (battleInfo.DungeonCorrectionLevel > 0)
            {
                BattleHelper.SetCharacterLevel(character, battleInfo.DungeonCorrectionLevel);
            }
        }

        SetElementyTypeAdvantageDamageValue(battleInfo.IsElementAdvantage);

        baseAttack = character.GetFinalStat(CharacterStatType.Attack).FloatStatValue;
        baseDefence = character.GetFinalStat(CharacterStatType.Defence).FloatStatValue;
        baseHp = character.GetFinalStat(CharacterStatType.Hp).FloatStatValue;

        float fAttack = baseAttack * PenaltyValue;
        float fDefence = baseDefence * PenaltyValue;
        float fHp = baseHp * PenaltyValue;

        Attack = (int)Math.Round(fAttack);
        Defence = (int)Math.Round(fDefence);
        Hp = (int)Math.Round(fHp);
        MaxHp = Hp;
        CriRate = character.GetFinalStat(CharacterStatType.CriRate).FloatStatValue;
        CriDmg = character.GetFinalStat(CharacterStatType.CriDmg).FloatStatValue;


        if (DamageTracker == null)
        {
            DamageTracker = new BattleDamageTracker();
        }

        DamageTracker.Init();
    }


    public void SetDefalutCharacterPrefab()
    {
        CharacterResourceTable characterResourceTable = TableManager.Instance.GetTable<CharacterResourceTable>();
        CharacterResourceTableData characterResourceTableData = characterResourceTable.GetDataByID(CharacterBaseData.GetCHARACTER_RESOURCE());
        CharacterResourceTableData costumeCharacterResourceTableData = default;

        //코스튬 데이터가 있다면 대체
        if (Character.IsEquippedCostume)
        {
            CostumeTable costumeTable = TableManager.Instance.GetTable<CostumeTable>();
            CostumeTableData costumeData = costumeTable.GetDataByID(Character.WearCostumeDataId);
            costumeCharacterResourceTableData = characterResourceTable.GetDataByID(costumeData.GetRESOURCE_CHARACTER());
        }

        string prefabName = costumeCharacterResourceTableData.IsNull() ? characterResourceTableData.GetBattlePrefabPath() : costumeCharacterResourceTableData.GetBattlePrefabPath();
        CharacterPrefabFullPath = prefabName;

        int pathCount = characterResourceTableData.GetBATTLE_CHARACTER_DEATH_PATHCount();
        CharacterDeadVoicePaths = new string[pathCount];
        if (pathCount > 0)
        {
            for (int i = 0; i < pathCount; i++)
            {
                CharacterDeadVoicePaths[i] = characterResourceTableData.GetBATTLE_CHARACTER_DEATH_PATH(i);
            }
        }

        pathCount = characterResourceTableData.GetBATTLE_END_WIN_PATHCount();
        CharacterBattleResultWinVoice = new string[pathCount];
        if (pathCount > 0)
        {
            for (int i = 0; i < pathCount; i++)
            {
                CharacterBattleResultWinVoice[i] = characterResourceTableData.GetBATTLE_END_WIN_PATH(i);
            }
        }

        pathCount = characterResourceTableData.GetBATTLE_END_LOSE_PATHCount();
        CharacterBattleResultLoseVoice = new string[pathCount];
        if (pathCount > 0)
        {
            for (int i = 0; i < pathCount; i++)
            {
                CharacterBattleResultLoseVoice[i] = characterResourceTableData.GetBATTLE_END_LOSE_PATH(i);
            }
        }

        pathCount = characterResourceTableData.GetBATTLE_STATE_EFFECTCount();
        CharacterBattleStackEffectPath = new string[pathCount];
        if (pathCount > 0)
        {
            for (int i = 0; i < pathCount; i++)
            {
                CharacterBattleStackEffectPath[i] = characterResourceTableData.GetBATTLE_STATE_EFFECT(i);
            }
        }

    }


    // 데미지 감소 체크 (방어막 보유시만)
    public int ApplyDamageReduction(int originalDamage)
    {
        if (!HasShield || damageReductionPercent <= 0)
            return originalDamage;

        float reduction = originalDamage * (damageReductionPercent / 100f);
        int finalDamage = Mathf.Max(1, originalDamage - (int)reduction);

        Debug.Log($"[DamageReduction] {originalDamage} → {finalDamage} (방어막 보유, {damageReductionPercent}% 감소)");
        return finalDamage;
    }



}
