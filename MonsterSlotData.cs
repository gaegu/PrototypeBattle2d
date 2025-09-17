using System;
using System.Collections.Generic;
using UnityEngine;
using BattleCharacterSystem;

namespace BattleCharacterSystem
{
    /// <summary>
    /// 몬스터 그룹 슬롯 데이터
    /// </summary>
    [Serializable]
    public class MonsterSlotData
    {
        [Header("슬롯 정보")]
        public int slotIndex; // 0-4 (5개 슬롯)
        public bool isEmpty = true;

        [Header("몬스터 정보")]
        public BattleMonsterDataSO monsterData;
        public int level = 1;

        [Header("위치 오버라이드")]
        public bool useCustomPosition = false;
        public Vector3 customPosition;

        public MonsterSlotData(int index)
        {
            this.slotIndex = index;
            this.isEmpty = true;
        }

        public void SetMonster(BattleMonsterDataSO monster, int level = 1)
        {
            this.monsterData = monster;
            this.level = level;
            this.isEmpty = (monster == null);
        }

        public void Clear()
        {
            this.monsterData = null;
            this.isEmpty = true;
            this.level = 1;
        }
    }
}