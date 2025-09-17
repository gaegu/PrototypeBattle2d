using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace SkillSystem
{  
    
    // =====================================================
    // ScriptableObject 래퍼
    // =====================================================

    [CreateAssetMenu(fileName = "SkillDatabase", menuName = "Skills/Database")]
    public class SkillDatabaseAsset : ScriptableObject
    {
        public AdvancedSkillDatabase database = new AdvancedSkillDatabase();
    }


}