//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine.UI;               // UnityEngine의 UI 기본
//using TMPro;                        // TextMeshPro 관련 클래스 모음 ( UnityEngine.UI.Text 대신 이걸 사용 )
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using UnityEngine;                  // UnityEngine 기본
using UnityEngine.AI;
//=========================================================================================================

public class TownPlayerUnit : BaseTownObjectSupport, ITownPlayer
{

    #region Coding rule : Property
    #region ITownSupport
    public override int DataId { get { return Character == null ? 0 : Character.DataId; } }
    public override ITownObject TownObject { get { return townCharacter; } }
    public override Transform LookAtTransform
    {
        get
        {
            return townCharacter == null ? transform : townCharacter.HeadTarget;
        }
    }
    #endregion
    public bool IsWaitCollision { get; protected set; }
    public CameraFollowData TalkCameraFollowData { get; protected set; }
    protected Character Character { get; private set; }
    protected Character PrevCharacter { get; private set; }
    protected Vector3 SpawnPosition { get; private set; }
    protected Vector3 SpawnRotation { get; private set; }
    protected string TownCharacterPath
    {
        get
        {
            if (Character.IsEquippedCostume)
            {
                CostumeTable costumeTable = TableManager.Instance.GetTable<CostumeTable>();
                CostumeTableData costumeData = costumeTable.GetDataByID(Character.WearCostumeDataId);
                return ThumbnailGeneratorModel.GetCharacterPrefabByResourceDataId(costumeData.GetRESOURCE_CHARACTER(), CharacterPrefabType.LOD0_2_Town);
            }
            else
            {
                CharacterTable characterTable = TableManager.Instance.GetTable<CharacterTable>();
                CharacterTableData characterTableData = characterTable.GetDataByID(Character.DataId);
                return ThumbnailGeneratorModel.GetCharacterPrefabByData(characterTableData, CharacterPrefabType.LOD0_2_Town);
            }

        }
    }
    protected string TownCharacterAnimatorPath
    {
        get
        {
            CharacterTable characterTable = TableManager.Instance.GetTable<CharacterTable>();
            CharacterTableData characterTableData = characterTable.GetDataByID(Character.DataId);
            return ThumbnailGeneratorModel.GetCharacterAnimatorByData(characterTableData, CharacterAnimatorType.Town);
        }
    }
    #endregion Coding rule : Property

    #region Coding rule : Value
    [SerializeField]
    protected TownCharacter townCharacter = null;

    protected TownTagGroup townTagGroup = null;
    protected int pathFindTargetDataId = 0;
    #endregion Coding rule : Value

    #region Coding rule : Function
    /// <summary>
    /// 캐릭터 정보 셋팅
    /// </summary>
    public void SetCharacter(Character character)
    {
        PrevCharacter = Character;
        Character = character;
    }

    /// <summary>
    /// 등장 위치 값만 변경
    /// </summary>
    public void SetSpawn(Vector3 spawnPosition, Vector3 spawnRotation)
    {
        SpawnPosition = spawnPosition;
        SpawnRotation = spawnRotation;
    }

    /// <summary>
    /// 위치 이동
    /// </summary>
    public void ChangeSpawn()
    {
        if (townCharacter == null)
            return;

        townCharacter.SetSpawnTransform(SpawnPosition, SpawnRotation);

        if (townCharacter.NavMeshAgent.isOnNavMesh)
        {
            IronJade.Debug.LogError($"################ 테스트 위치 이동 로그 isOnNavMesh Success");
        }
        else
        {
            if (NavMesh.SamplePosition(SpawnPosition, out NavMeshHit hit, 1.0f, NavMesh.AllAreas))
            {
                IronJade.Debug.LogError($"################ 테스트 위치 이동 로그 SamplePos Success");
            }
            else
            {
                IronJade.Debug.LogError($"################ 테스트 위치 이동 로그 SamplePos Failed");
            }
        }
    }

    public void HideSpawnEffect()
    {
    }

    /// <summary>
    /// 등장 연출
    /// </summary>
    public void ShowSpawnEffect(bool isFouce)
    {
        if (townCharacter == null)
            return;

        if (!isFouce)
        {
            if (!CheckChangeCharacter())
                return;
        }

        townCharacter.ShowSpawnEffect();
    }

    /// <summary>
    /// 길찾기
    /// </summary>
    public virtual CharacterAutoMoveState SetPathfind(CharacterAutoMoveState state, TownObjectType townObjectType, int townObjectId)
    {
        return state;
    }

    /// <summary>
    /// 길찾기 진행 여부
    /// </summary>
    public virtual bool CheckPathfind()
    {
        if (townCharacter == null)
            return false;

        if (townCharacter.PathfindData == null)
            return false;

        return townCharacter.PathfindData.IsPathfind;
    }

    /// <summary>
    /// 진행중인 길찾기 타겟과 동일한지 비교
    /// </summary>
    public bool CheckPathfindTarget(int dataId)
    {
        return pathFindTargetDataId == dataId;
    }

    protected bool CheckChangeCharacter()
    {
        if (Character == null)
            return true;

        if (PrevCharacter == null)
            return true;

        return Character.DataId != PrevCharacter.DataId;
    }

    public virtual void ShowNotifyEffect(bool isActive) { }

    public virtual void UpdateState() { }

#if YOUME
    public virtual async UniTask ShowChattingTag(Chatting chatting)
    {
        townTagGroup.SetUITarget(Transform, TalkTransform);
        townTagGroup.TownChattingTag.SetActive(true);
        townTagGroup.TownChattingTag.SetChatting(chatting);

        await TownObjectManager.Instance.OnEventShowTag(townTagGroup);
    }
#endif

    public virtual async UniTask CheckCollision() { }

    public virtual async UniTask CheckGimmickCollision() { }
    #endregion Coding rule : Function
}