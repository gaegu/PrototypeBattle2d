using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace BattleCharacterSystem.Timeline
{
    /// <summary>
    /// 캐릭터별 Timeline 설정
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterTimelineConfig", menuName = "Battle/Timeline/CharacterTimelineConfig")]
    public class CharacterTimelineConfig : ScriptableObject
    {
        [Header("Character Info")]
        public int characterId;
        public string characterName;

        [Header("Basic Timelines")]
        public TimelineDataSO attack1Timeline;
        public TimelineDataSO activeSkill1Timeline;
        public TimelineDataSO passiveSkill1Timeline;

        [Header("Custom Timelines")]
        [SerializeField]
        public List<CustomTimelineEntry> customTimelines = new List<CustomTimelineEntry>();

        [Serializable]
        public class CustomTimelineEntry
        {
            public string stateName;  // "Rage", "Phase2", "Entrance" 등
            public TimelineDataSO timeline;
        }

        /// <summary>
        /// 상태명으로 Timeline 가져오기
        /// </summary>
        public TimelineDataSO GetTimelineByState(string stateName)
        {
            // 기본 타임라인 체크
            switch (stateName.ToLower())
            {
                case "attack1":
                case "attack":
                    return attack1Timeline;
                case "activeskill1":
                case "activeskill":
                    return activeSkill1Timeline;
                case "passiveskill1":
                case "passiveskill":
                    return passiveSkill1Timeline;
            }

            // 커스텀 타임라인 체크
            var custom = customTimelines.Find(x => x.stateName.Equals(stateName, StringComparison.OrdinalIgnoreCase));
            return custom?.timeline;
        }

        /// <summary>
        /// 모든 Timeline 리스트
        /// </summary>
        public List<TimelineDataSO> GetAllTimelines()
        {
            var timelines = new List<TimelineDataSO>();

            if (attack1Timeline != null) timelines.Add(attack1Timeline);
            if (activeSkill1Timeline != null) timelines.Add(activeSkill1Timeline);
            if (passiveSkill1Timeline != null) timelines.Add(passiveSkill1Timeline);

            foreach (var custom in customTimelines)
            {
                if (custom.timeline != null)
                    timelines.Add(custom.timeline);
            }

            return timelines;
        }
    }
}