using System;
using System.Collections.Generic;
using UnityEngine;
using BattleCharacterSystem;

namespace BattleCharacterSystem
{
    /// <summary>
    /// 몬스터 그룹 데이터 ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "NewMonsterGroup", menuName = "Battle Character System/Monster Group", order = 3)]
    public class MonsterGroupDataSO : ScriptableObject
    {
        [Header("===== 기본 정보 =====")]
        [SerializeField] private int groupId = 5000;
        [SerializeField] private string groupName = "New Monster Group";
        [SerializeField] private string description = "";

        [Header("===== 그룹 분류 =====")]
        [SerializeField] private string groupPurpose = "Normal"; // Tutorial, Normal, Boss, Special, Event
        [SerializeField] private int recommendedLevel = 1;
        [SerializeField] private int difficulty = 1; // 1-10 난이도

        [Header("===== 몬스터 슬롯 (최대 5개) =====")]
        [SerializeField] private MonsterSlotData[] monsterSlots = new MonsterSlotData[5];

        [Header("===== 진형 설정 =====")]
        [SerializeField] private FormationType formationType = FormationType.DefensiveBalance;
        [SerializeField] private bool useCustomFormation = false;

        [Header("===== 전투 설정 =====")]
        [SerializeField] private int totalPower = 0; // 전투력
        [SerializeField] private bool autoCalculatePower = true;

        [Header("===== 보상 설정 (선택) =====")]
        [SerializeField] private int goldReward = 100;
        [SerializeField] private int expReward = 50;
        [SerializeField] private List<int> itemRewardIds = new List<int>();

        [Header("===== 특수 조건 =====")]
        [SerializeField] private bool hasTimeLimit = false;
        [SerializeField] private float timeLimit = 180f; // 3분
        [SerializeField] private bool hasSpecialCondition = false;
        [SerializeField] private string specialConditionDescription = "";

        // Properties
        public int GroupId => groupId;
        public string GroupName => groupName;
        public string Description => description;
        public string GroupPurpose => groupPurpose;
        public int RecommendedLevel => recommendedLevel;
        public int Difficulty => difficulty;
        public MonsterSlotData[] MonsterSlots => monsterSlots;
        public FormationType FormationType => formationType;
        public int TotalPower => totalPower;

        // 초기화
        private void OnEnable()
        {
            InitializeSlots();
        }

        /// <summary>
        /// 슬롯 초기화
        /// </summary>
        private void InitializeSlots()
        {
            if (monsterSlots == null || monsterSlots.Length != 5)
            {
                monsterSlots = new MonsterSlotData[5];
                for (int i = 0; i < 5; i++)
                {
                    monsterSlots[i] = new MonsterSlotData(i);
                }
            }
        }

        /// <summary>
        /// 몬스터 추가
        /// </summary>
        public bool AddMonster(int slotIndex, BattleMonsterDataSO monster, int level = 1)
        {
            if (slotIndex < 0 || slotIndex >= 5) return false;
            if (monster == null) return false;

            monsterSlots[slotIndex].SetMonster(monster, level);

            if (autoCalculatePower)
            {
                CalculateTotalPower();
            }

            return true;
        }

        /// <summary>
        /// 몬스터 제거
        /// </summary>
        public void RemoveMonster(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 5) return;

            monsterSlots[slotIndex].Clear();

            if (autoCalculatePower)
            {
                CalculateTotalPower();
            }
        }

        /// <summary>
        /// 전체 몬스터 클리어
        /// </summary>
        public void ClearAllMonsters()
        {
            for (int i = 0; i < 5; i++)
            {
                monsterSlots[i].Clear();
            }
            totalPower = 0;
        }

        /// <summary>
        /// 전투력 계산
        /// </summary>
        public void CalculateTotalPower()
        {
            totalPower = 0;

            foreach (var slot in monsterSlots)
            {
                if (!slot.isEmpty && slot.monsterData != null)
                {
                    // 몬스터의 레벨별 스탯 계산
                    var stats = slot.monsterData.GetFinalStats(slot.level);

                    // 간단한 전투력 공식: (HP/10) + Attack + (Defense/2)
                    int monsterPower = (stats.hp / 10) + stats.attack + (stats.defense / 2);

                    // 보스는 추가 배율
                    if (slot.monsterData.IsBoss)
                    {
                        monsterPower = Mathf.RoundToInt(monsterPower * 1.5f);
                    }

                    totalPower += monsterPower;
                }
            }
        }

        /// <summary>
        /// 활성 몬스터 수
        /// </summary>
        public int GetActiveMonsterCount()
        {
            int count = 0;
            foreach (var slot in monsterSlots)
            {
                if (!slot.isEmpty && slot.monsterData != null)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 템플릿에서 초기화
        /// </summary>
        public void InitializeFromTemplate(string templateType)
        {
            ClearAllMonsters();

            switch (templateType)
            {
                case "Tutorial":
                    groupPurpose = "Tutorial";
                    recommendedLevel = 1;
                    difficulty = 1;
                    formationType = FormationType.Offensive;
                    // 슬롯 2에만 약한 몬스터 1개
                    break;

                case "Balanced":
                    groupPurpose = "Normal";
                    recommendedLevel = 10;
                    difficulty = 3;
                    formationType = FormationType.DefensiveBalance;
                    // 탱커 1, 딜러 2, 서포터 1
                    break;

                case "Aggressive":
                    groupPurpose = "Normal";
                    recommendedLevel = 15;
                    difficulty = 5;
                    formationType = FormationType.Offensive;
                    // 올 딜러 구성
                    break;

                case "Boss":
                    groupPurpose = "Boss";
                    recommendedLevel = 20;
                    difficulty = 8;
                    formationType = FormationType.Defensive;
                    // 중앙에 보스, 양옆에 서포터
                    break;

                case "Special":
                    groupPurpose = "Special";
                    recommendedLevel = 25;
                    difficulty = 10;
                    formationType = FormationType.DefensiveBalance;
                    hasSpecialCondition = true;
                    break;
            }
        }

        /// <summary>
        /// 그룹 검증
        /// </summary>
        public bool ValidateGroup(out string errorMessage)
        {
            errorMessage = "";

            // 최소 1개 몬스터 필요
            if (GetActiveMonsterCount() == 0)
            {
                errorMessage = "At least one monster is required.";
                return false;
            }

            // 보스 그룹은 보스가 있어야 함
            if (groupPurpose == "Boss")
            {
                bool hasBoss = false;
                foreach (var slot in monsterSlots)
                {
                    if (!slot.isEmpty && slot.monsterData != null && slot.monsterData.IsBoss)
                    {
                        hasBoss = true;
                        break;
                    }
                }

                if (!hasBoss)
                {
                    errorMessage = "Boss group must contain at least one boss monster.";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// CSV 내보내기
        /// </summary>
        public string ToCSV()
        {
            string monsters = "";
            for (int i = 0; i < 5; i++)
            {
                if (!monsterSlots[i].isEmpty && monsterSlots[i].monsterData != null)
                {
                    monsters += $"{monsterSlots[i].monsterData.MonsterId}:{monsterSlots[i].level};";
                }
                else
                {
                    monsters += "0:0;";
                }
            }

            return $"{groupId},{groupName},{groupPurpose},{recommendedLevel},{difficulty}," +
                   $"{formationType},{totalPower},{monsters}";
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // ID 범위 체크
            if (groupId < 5000 || groupId >= 6000)
            {
                groupId = Mathf.Clamp(groupId, 5000, 5999);
            }

            // 슬롯 초기화
            InitializeSlots();

            // 자동 전투력 계산
            if (autoCalculatePower)
            {
                CalculateTotalPower();
            }
        }
#endif
    }
}