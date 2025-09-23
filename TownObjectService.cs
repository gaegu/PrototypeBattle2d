using Cysharp.Threading.Tasks;
using UnityEngine;

public class TownObjectService : ITownObjectService
{
    private TownObjectManager manager => TownObjectManager.Instance;

    public ITownSupport GetTownObjectByEnumId(string enumId)
    {
        return manager?.GetTownObjectByEnumId(enumId);
    }

    public ITownSupport GetTownObjectByDataId(int dataId)
    {
        return manager?.GetTownObjectByDataId(dataId);
    }

    public async UniTask StartProcessAsync()
    {
        if (manager != null)
            await manager.StartProcessAsync();
    }

    public async UniTask RefreshProcess(bool isMissionUpdate = false)
    {
        if (manager != null)
            await manager.RefreshProcess(isMissionUpdate);
    }

    public void ClearTownObjects()
    {
        manager?.ClearTownObjects();
    }

    public void SetConditionRoad()
    {
        manager?.SetConditionRoad();
    }

    public void LoadAllTownNpcInfoGroup()
    {
        manager?.LoadAllTownNpcInfoGroup();
    }

    public ITownSupport GetTownObjectByType(TownObjectType type, string targetId)
    {
        throw new System.NotImplementedException();
    }

    public void DestroyTownObject()
    {
        throw new System.NotImplementedException();
    }

    public void UnloadAllTownNpcInfoGroup()
    {
        throw new System.NotImplementedException();
    }

    public bool IsMissionRelatedNpc(FieldMapDefine fieldMap, int npcId)
    {
        throw new System.NotImplementedException();
    }

    public void OperateDecoratorFactory()
    {
        throw new System.NotImplementedException();
    }

    public void OnEventClearTownTag()
    {
        throw new System.NotImplementedException();
    }
}