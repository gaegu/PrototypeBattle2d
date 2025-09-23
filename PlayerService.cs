using Cysharp.Threading.Tasks;
using UnityEngine;

public class PlayerService : IPlayerService
{
    private PlayerManager manager => PlayerManager.Instance;

    public Player MyPlayer => manager?.MyPlayer;

    public async UniTask CreateMyPlayerUnit()
    {
        if (manager != null)
            await manager.CreateMyPlayerUnit();
    }

    public async UniTask LoadMyPlayerCharacterObject()
    {
        if (manager != null)
            await manager.LoadMyPlayerCharacterObject();
    }

    public void SetMyPlayerSpawn(TownObjectType objectType, string targetId, bool isLeaderPosition)
    {
        manager?.SetMyPlayerSpawn(objectType, targetId, isLeaderPosition);
    }

    public void ResetTempLeaderCharacter()
    {
        manager?.ResetTempLeaderCharacter();
    }

    public void SetTempLeaderCharacter(int characterId)
    {
        manager?.SetTempLeaderCharacter(characterId);
    }
}
