// =====================================================
// SkillEffectPresets.cs
// 효과 프리셋 라이브러리 - Scripts/Battle/Skills/Core 폴더에 저장
// =====================================================

using System.Collections.Generic;
using System.Linq;

namespace SkillSystem
{
    public static class AdvancedEffectPresets
    {
        private static Dictionary<string, List<AdvancedSkillEffect>> presets;

        static AdvancedEffectPresets()
        {
            InitializePresets();
        }

        private static void InitializePresets()
        {
            presets = new Dictionary<string, List<AdvancedSkillEffect>>
            {
                // =====================================================
                // 데미지 프리셋
                // =====================================================
                ["데미지"] = new List<AdvancedSkillEffect>
                {
                    new AdvancedSkillEffect
                    {
                        name = "기본 공격",
                        type = EffectType.Damage,
                        value = 100,
                        tooltip = "기본적인 물리 데미지"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "약공격",
                        type = EffectType.Damage,
                        value = 60,
                        tooltip = "낮은 데미지"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "강공격",
                        type = EffectType.Damage,
                        value = 150,
                        tooltip = "강한 단일 공격"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "필살기",
                        type = EffectType.Damage,
                        value = 187,
                        tooltip = "CSV에서 자주 사용되는 강력한 공격"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "크리티컬 공격",
                        type = EffectType.Damage,
                        value = 250,
                        canCrit = true,
                        tooltip = "치명타 확률이 높은 공격"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "방어 기반 데미지",
                        type = EffectType.Damage,
                        value = 150,
                        damageBase = DamageBase.Defense,
                        tooltip = "자신의 방어력에 비례한 데미지"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "방어 무시",
                        type = EffectType.TrueDamage,
                        value = 100,
                        tooltip = "방어력을 무시하는 고정 데미지"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "연속 공격",
                        type = EffectType.Damage,
                        value = 63,
                        targetType = TargetType.Multiple,
                        targetCount = 3,
                        tooltip = "여러 대상을 공격"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "광역 공격",
                        type = EffectType.Damage,
                        value = 80,
                        targetType = TargetType.EnemyAll,
                        tooltip = "모든 적에게 데미지"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "추가 데미지",
                        type = EffectType.Damage,
                        value = 200,
                        chance = 0.6f,
                        damageBase = DamageBase.Defense,
                        tooltip = "60% 확률로 방어력 비례 추가 데미지"
                    }
                },

                // =====================================================
                // 회복 프리셋
                // =====================================================
                ["회복"] = new List<AdvancedSkillEffect>
                {
                    new AdvancedSkillEffect
                    {
                        name = "소량 회복",
                        type = EffectType.Heal,
                        value = 20,
                        tooltip = "적은 양의 HP 회복"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "기본 회복",
                        type = EffectType.Heal,
                        value = 36,
                        tooltip = "CSV에서 자주 사용되는 회복량"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "대량 회복",
                        type = EffectType.Heal,
                        value = 51,
                        tooltip = "많은 양의 HP 회복"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "지속 회복",
                        type = EffectType.HealOverTime,
                        value = 12,
                        duration = 4,
                        tooltip = "여러 턴에 걸쳐 회복"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "전체 회복",
                        type = EffectType.Heal,
                        value = 30,
                        targetType = TargetType.AllyAll,
                        tooltip = "아군 전체 회복"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "흡혈",
                        type = EffectType.LifeSteal,
                        value = 70,
                        tooltip = "피해량의 70% 회복"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "완전 흡혈",
                        type = EffectType.LifeSteal,
                        value = 100,
                        tooltip = "피해량의 100% 회복"
                    }
                },

                // =====================================================
                // 버프 프리셋
                // =====================================================
                ["버프"] = new List<AdvancedSkillEffect>
                {
                    new AdvancedSkillEffect
                    {
                        name = "공격력 증가",
                        type = EffectType.Buff,
                        statType = StatType.Attack,
                        value = 33,
                        duration = 8,
                        tooltip = "CSV 표준 공격력 버프"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "방어력 증가",
                        type = EffectType.Buff,
                        statType = StatType.Defense,
                        value = 33,
                        duration = 8,
                        tooltip = "CSV 표준 방어력 버프"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "속도 증가",
                        type = EffectType.Buff,
                        statType = StatType.Speed,
                        value = 15,
                        duration = 6,
                        tooltip = "행동 속도 증가"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "치명타율 증가",
                        type = EffectType.Buff,
                        statType = StatType.CritRate,
                        value = 50,
                        duration = 8,
                        tooltip = "치명타 확률 대폭 증가"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "치명타 피해 증가",
                        type = EffectType.Buff,
                        statType = StatType.CritDamage,
                        value = 40,
                        duration = 8,
                        tooltip = "치명타 데미지 증가"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "효과 저항 증가",
                        type = EffectType.Buff,
                        statType = StatType.EffectResist,
                        value = 50,
                        duration = 8,
                        tooltip = "디버프 저항력 증가"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "효과 적중 증가",
                        type = EffectType.Buff,
                        statType = StatType.EffectHit,
                        value = 45,
                        duration = 7,
                        tooltip = "디버프 성공률 증가"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "회피 증가",
                        type = EffectType.Buff,
                        statType = StatType.Evasion,
                        value = 25,
                        duration = 4,
                        tooltip = "회피 확률 증가"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "치명 저항 증가",
                        type = EffectType.Buff,
                        statType = StatType.CritRate,
                        value = -30,
                        duration = 7,
                        tooltip = "받는 치명타 확률 감소"
                    }
                },

                // =====================================================
                // 디버프 프리셋
                // =====================================================
                ["디버프"] = new List<AdvancedSkillEffect>
                {
                    new AdvancedSkillEffect
                    {
                        name = "공격력 감소",
                        type = EffectType.Debuff,
                        statType = StatType.Attack,
                        value = 23,
                        duration = 7,
                        chance = 0.6f,
                        tooltip = "60% 확률로 공격력 감소"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "방어력 감소",
                        type = EffectType.Debuff,
                        statType = StatType.Defense,
                        value = 23,
                        duration = 7,
                        chance = 0.6f,
                        tooltip = "60% 확률로 방어력 감소"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "속도 감소",
                        type = EffectType.Debuff,
                        statType = StatType.Speed,
                        value = 40,
                        duration = 7,
                        chance = 0.6f,
                        tooltip = "60% 확률로 속도 감소"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "효과 저항 감소",
                        type = EffectType.Debuff,
                        statType = StatType.EffectResist,
                        value = 45,
                        duration = 7,
                        chance = 0.6f,
                        tooltip = "디버프에 더 취약하게 만듦"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "적중 감소",
                        type = EffectType.Debuff,
                        statType = StatType.Accuracy,
                        value = 45,
                        duration = 7,
                        chance = 0.6f,
                        tooltip = "공격 명중률 감소"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "회피 감소",
                        type = EffectType.Debuff,
                        statType = StatType.Evasion,
                        value = 35,
                        duration = 7,
                        chance = 0.6f,
                        tooltip = "회피 능력 감소"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "회복 불가",
                        type = EffectType.Debuff,
                        statType = StatType.HealBlock,
                        value = 100,
                        duration = 9,
                        chance = 0.55f,
                        tooltip = "모든 회복 차단"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "회복량 감소",
                        type = EffectType.Debuff,
                        statType = StatType.HealReduction,
                        value = 33,
                        duration = 6,
                        chance = 0.7f,
                        tooltip = "회복 효과 감소"
                    }
                },

                // =====================================================
                // 상태이상 프리셋
                // =====================================================
                ["상태이상"] = new List<AdvancedSkillEffect>
                {
                    new AdvancedSkillEffect
                    {
                        name = "기절",
                        type = EffectType.StatusEffect,
                        statusType = StatusType.Stun,
                        duration = 4,
                        chance = 0.6f,
                        tooltip = "행동 불가"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "짧은 기절",
                        type = EffectType.StatusEffect,
                        statusType = StatusType.Stun,
                        duration = 2,
                        chance = 0.5f,
                        tooltip = "짧은 행동 불가"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "침묵",
                        type = EffectType.StatusEffect,
                        statusType = StatusType.Silence,
                        duration = 4,
                        chance = 0.6f,
                        tooltip = "스킬 사용 불가"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "도발",
                        type = EffectType.StatusEffect,
                        statusType = StatusType.Taunt,
                        duration = 4,
                        chance = 0.6f,
                        tooltip = "강제로 자신을 공격하게 함"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "화상",
                        type = EffectType.StatusEffect,
                        statusType = StatusType.Burn,
                        value = 92,
                        duration = 2,
                        chance = 0.5f,
                        tooltip = "매 턴 92% 데미지"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "중독",
                        type = EffectType.StatusEffect,
                        statusType = StatusType.Poison,
                        value = 92,
                        duration = 2,
                        chance = 0.5f,
                        tooltip = "매 턴 92% 데미지"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "출혈",
                        type = EffectType.StatusEffect,
                        statusType = StatusType.Bleed,
                        value = 82,
                        duration = 3,
                        chance = 0.6f,
                        tooltip = "매 턴 82% 데미지"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "동결",
                        type = EffectType.StatusEffect,
                        statusType = StatusType.Freeze,
                        duration = 2,
                        chance = 0.4f,
                        tooltip = "행동 불가 + 받는 데미지 증가"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "수면",
                        type = EffectType.StatusEffect,
                        statusType = StatusType.Sleep,
                        duration = 3,
                        chance = 0.5f,
                        tooltip = "행동 불가, 공격받으면 해제"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "혼란",
                        type = EffectType.StatusEffect,
                        statusType = StatusType.Confuse,
                        duration = 3,
                        chance = 0.5f,
                        tooltip = "무작위 대상 공격"
                    }
                },

                // =====================================================
                // 보호 프리셋
                // =====================================================
                ["보호"] = new List<AdvancedSkillEffect>
                {
                    new AdvancedSkillEffect
                    {
                        name = "소형 보호막",
                        type = EffectType.Shield,
                        value = 11,
                        duration = 10,
                        shieldBase = ShieldBase.MaxHP,
                        tooltip = "최대 체력의 11% 보호막"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "중형 보호막",
                        type = EffectType.Shield,
                        value = 29,
                        duration = 10,
                        shieldBase = ShieldBase.MaxHP,
                        tooltip = "최대 체력의 29% 보호막"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "대형 보호막",
                        type = EffectType.Shield,
                        value = 36,
                        duration = 11,
                        shieldBase = ShieldBase.MaxHP,
                        tooltip = "최대 체력의 36% 보호막"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "초대형 보호막",
                        type = EffectType.Shield,
                        value = 51,
                        duration = 12,
                        shieldBase = ShieldBase.MaxHP,
                        tooltip = "최대 체력의 51% 보호막"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "스페셜 보호막",
                        type = EffectType.Shield,
                        value = 17,
                        duration = 12,
                        shieldBase = ShieldBase.MaxHP,
                        tooltip = "스페셜 스킬용 보호막"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "피해 반사",
                        type = EffectType.Reflect,
                        value = 30,
                        duration = 8,
                        tooltip = "받는 피해의 30% 반사"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "강화 반사",
                        type = EffectType.Reflect,
                        value = 30,
                        duration = 7,
                        tooltip = "짧지만 강한 반사"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "피해 감소",
                        type = EffectType.DamageReduction,
                        value = 35,
                        duration = 4,
                        tooltip = "받는 피해 35% 감소"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "면역",
                        type = EffectType.Immunity,
                        duration = 6,
                        tooltip = "모든 디버프 면역"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "무적",
                        type = EffectType.Invincible,
                        duration = 2,
                        tooltip = "모든 피해 무시"
                    }
                },

                // =====================================================
                // 해제 프리셋
                // =====================================================
                ["해제"] = new List<AdvancedSkillEffect>
                {
                    new AdvancedSkillEffect
                    {
                        name = "디버프 해제",
                        type = EffectType.Dispel,
                        dispelType = DispelType.Debuff,
                        dispelCount = 2,
                        tooltip = "약화 효과 2개 제거"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "버프 제거",
                        type = EffectType.Dispel,
                        dispelType = DispelType.Buff,
                        dispelCount = 2,
                        tooltip = "적의 강화 효과 제거"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "모든 효과 해제",
                        type = EffectType.Dispel,
                        dispelType = DispelType.All,
                        dispelCount = 99,
                        tooltip = "모든 효과 제거"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "상태이상 해제",
                        type = EffectType.Dispel,
                        dispelType = DispelType.StatusEffect,
                        dispelCount = 3,
                        tooltip = "상태이상 3개 제거"
                    }
                },

                // =====================================================
                // 특수 프리셋
                // =====================================================
                ["특수"] = new List<AdvancedSkillEffect>
                {
                    new AdvancedSkillEffect
                    {
                        name = "수호석 파괴",
                        type = EffectType.Special,
                        specialEffect = "DestroyGuardStone",
                        value = 1,
                        tooltip = "무작위 수호석 1개 파괴"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "즉시 행동",
                        type = EffectType.Special,
                        specialEffect = "ExtraTurn",
                        value = 1,
                        tooltip = "추가 턴 획득"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "부활",
                        type = EffectType.Special,
                        specialEffect = "Revive",
                        value = 50,
                        tooltip = "50% HP로 부활"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "소환",
                        type = EffectType.Summon,
                        summonId = 1001,
                        summonCount = 1,
                        tooltip = "유닛 소환"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "변신",
                        type = EffectType.Transform,
                        transformId = 2001,
                        duration = 5,
                        tooltip = "다른 형태로 변신"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "버프 복사",
                        type = EffectType.Special,
                        specialEffect = "CopyBuffs",
                        value = 1,
                        tooltip = "대상의 버프 복사"
                    },
                    new AdvancedSkillEffect
                    {
                        name = "HP 역전",
                        type = EffectType.Special,
                        specialEffect = "SwapHP",
                        value = 1,
                        tooltip = "대상과 HP 비율 교환"
                    }
                }
            };
        }

        public static List<string> GetCategories()
        {
            return presets.Keys.ToList();
        }

        public static List<AdvancedSkillEffect> GetPresetsForCategory(string category)
        {
            return presets.ContainsKey(category) ? presets[category] : new List<AdvancedSkillEffect>();
        }

        public static AdvancedSkillEffect GetPresetById(string id)
        {
            // 간단한 ID 기반 검색
            var parts = id.Split('_');
            if (parts.Length < 2) return null;

            switch (parts[0])
            {
                case "damage":
                    return new AdvancedSkillEffect
                    {
                        type = EffectType.Damage,
                        value = 187,
                        tooltip = "기본 데미지"
                    };
                case "heal":
                    return new AdvancedSkillEffect
                    {
                        type = EffectType.Heal,
                        value = 36,
                        tooltip = "기본 회복"
                    };
                case "buff":
                    return new AdvancedSkillEffect
                    {
                        type = EffectType.Buff,
                        statType = StatType.Attack,
                        value = 33,
                        duration = 8,
                        tooltip = "공격력 버프"
                    };
                case "debuff":
                    return new AdvancedSkillEffect
                    {
                        type = EffectType.Debuff,
                        statType = StatType.Defense,
                        value = 23,
                        duration = 7,
                        chance = 0.6f,
                        tooltip = "방어력 디버프"
                    };
                case "status":
                    return new AdvancedSkillEffect
                    {
                        type = EffectType.StatusEffect,
                        statusType = StatusType.Stun,
                        duration = 4,
                        chance = 0.6f,
                        tooltip = "기절"
                    };
                case "shield":
                    return new AdvancedSkillEffect
                    {
                        type = EffectType.Shield,
                        value = 36,
                        duration = 11,
                        tooltip = "보호막"
                    };
                case "reflect":
                    return new AdvancedSkillEffect
                    {
                        type = EffectType.Reflect,
                        value = 30,
                        duration = 8,
                        tooltip = "피해 반사"
                    };
                case "dispel":
                    return new AdvancedSkillEffect
                    {
                        type = EffectType.Dispel,
                        dispelType = DispelType.Debuff,
                        dispelCount = 2,
                        tooltip = "디버프 해제"
                    };
                default:
                    return new AdvancedSkillEffect();
            }
        }

        public static List<AdvancedSkillEffect> GetAllPresets()
        {
            var allPresets = new List<AdvancedSkillEffect>();
            foreach (var category in presets.Values)
            {
                allPresets.AddRange(category);
            }
            return allPresets;
        }
    }
}