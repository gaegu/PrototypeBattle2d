using Cysharp.Threading.Tasks;
using IronJade.Table.Data;
using System;
using System.Threading;
using UnityEngine;

public class PlayerService : IPlayerService
{
    private PlayerManager manager => PlayerManager.Instance;

    public Player MyPlayer => manager?.MyPlayer;

    //public UserSetting UserSetting => throw new System.NotImplementedException();

    public async UniTask CreateMyPlayerUnit()
    {
        if (manager != null)
            await manager.CreateMyPlayerUnit();
    }


  

    public async UniTask LoadMyPlayerCharacterObject()
    {
        try
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await PlayerManager.Instance.LoadMyPlayerCharacterObject()
                .AttachExternalCancellation(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Debug.LogError("LoadMyPlayerCharacterObject timeout!");
            return;
        }
    }

    public void SetMyPlayerSpawn(TownObjectType objectType, string targetId, bool isLeaderPosition, bool isUpdateTargetPosition = false)
    {
        manager?.SetMyPlayerSpawn(objectType, targetId, isLeaderPosition, isUpdateTargetPosition);
    }

    public void ResetTempLeaderCharacter()
    {
        manager?.ResetTempLeaderCharacter();
    }

    public void SetTempLeaderCharacter(int characterId)
    {
        manager?.SetTempLeaderCharacter(characterId);
    }

    public UniTask DestroyTownMyPlayerCharacter()
    {
        throw new System.NotImplementedException();
    }

    public UniTask DestroyTownOtherPlayer()
    {
        throw new System.NotImplementedException();
    }

    public void ShowMyPlayerSpawnEffect(bool isShow)
    {
        throw new System.NotImplementedException();
    }

    public bool CheckFixedAllyTeam(DungeonTableData dungeonData)
    {
        throw new System.NotImplementedException();
    }

    public bool CheckFixedAllyHaveTeam(DungeonTableData dungeonData)
    {
        throw new System.NotImplementedException();
    }

    public void UpdateLeaderCharacterPosition()
    {
        throw new System.NotImplementedException();
    }

    public void CancelLeaderCharacterPosition()
    {
        throw new System.NotImplementedException();
    }

    public bool CheckMleofficeIndoorFiled()
    {
        throw new System.NotImplementedException();
    }

    public UniTask LoadDummyUserSetting()
    {
        throw new System.NotImplementedException();
    }

    public UniTask<bool> SaveUserSetting(UserSettingModel.Save saveType, object data)
    {
        throw new System.NotImplementedException();
    }
}
