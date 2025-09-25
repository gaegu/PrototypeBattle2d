//=========================================================================================================
//using System;
//using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Cysharp.Threading.Tasks;
using IronJade.Table.Data;
using UnityEngine;
//using IronJade.Table.Data;          // 데이터 테이블
//=========================================================================================================

public class TownObjectManager
{
    private static TownObjectManager instance;
    public static TownObjectManager Instance
    {
        get
        {
            if (instance == null)
                instance = new TownObjectManager();

            return instance;
        }
    }

    private TownObjectManager() { }

    //=================================================================
    // 불필요한 부분은 지우고 사용하시면 됩니다.
    //=================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    public List<ITownSupport> AllTownObjects { get { return allTownObjects; } }
    public System.Action OnEventClearTownTag { get; private set; }
    public System.Func<TownTagGroup, UniTask> OnEventShowTag { get; private set; }
    public System.Func<TownSmallTalk, UniTask> OnEventShowSmallTalk { get; private set; }
    public System.Func<TownTalk, UniTask> OnEventShowTalk { get; private set; }
    public int FloorCount { get { return floors.Count; } }

    public bool IsTestCameraByNpcTalk { get; set; }
    #endregion Coding rule : Property

    #region Coding rule : Value
    private List<ITownSupport> allTownObjects = new List<ITownSupport>();
    private Dictionary<TownObjectType, Dictionary<string, ITownSupport>> typeToTownObjects = new Dictionary<TownObjectType, Dictionary<string, ITownSupport>>();
    private Dictionary<FieldMapDefine, TownNpcInfoGroup> townNpcInfoGroupDic = new Dictionary<FieldMapDefine, TownNpcInfoGroup>();
    private TownNpcWarpInfo townNpcWarpInfo = null;
    private List<Transform> floors;
    private System.Action onEventFactoryOperate;
    private System.Action onEventFactoryCancel;
    private System.Func<string, Transform> onEventWarpPointGet;

    #region Decorator Type NPC
    //세일즈용 NPC들은 PC버전에만 포함해야 하므로
    //모바일에서는 컨디션을 무조건 false로 만듬
    //시나리오에서 데코형 NPC로 분류한 타입
    private HashSet<int> decoTypeNPC = new HashSet<int>()
            {
                (int)NpcDefine.CHAR_HEIZE,
                (int)NpcDefine.OBJ_HEIZE_SPEAKER,
                (int)NpcDefine.CHAR_RAVI,
                (int)NpcDefine.CHAR_OSCAR,
                //(int)NpcDefine.NPC_SANTA,
                (int)NpcDefine.NPC_BRODY,
                (int)NpcDefine.NPC_HIRUMA,
                //(int)NpcDefine.NPC_NANA,
                (int)NpcDefine.NPC_NANA_02,
                (int)NpcDefine.NPC_KID,
                (int)NpcDefine.NPC_VLACKY,
                (int)NpcDefine.OBJ_MAILROBOT,
                (int)NpcDefine.NPC_DT_100J,
                (int)NpcDefine.NPC_DT_100J_02,
                (int)NpcDefine.NPC_POLICE,
                (int)NpcDefine.NPC_PROTESTER_01,
                (int)NpcDefine.NPC_PROTESTER_02,
                (int)NpcDefine.NPC_PROTESTER_03,
                (int)NpcDefine.NPC_PROTESTER_04,
                (int)NpcDefine.NPC_PROTESTER_05,
                (int)NpcDefine.NPC_PROTESTER_06,
                (int)NpcDefine.NPC_PROTESTER_07,
                (int)NpcDefine.NPC_PROTESTER_08,
                (int)NpcDefine.NPC_PROTESTER_09,
                (int)NpcDefine.NPC_SLAMMER_01,
                (int)NpcDefine.NPC_SLAMMER_02,
                (int)NpcDefine.NPC_AUDIENCE_MANEKIN,
                (int)NpcDefine.NPC_AUDIENCE_YELLOWMON,
                (int)NpcDefine.NPC_CHARGING_MANEKIN_01,
                (int)NpcDefine.NPC_CHARGING_MANEKIN_02,
                (int)NpcDefine.NPC_CHARGING_YELLOWMON_01,
                (int)NpcDefine.NPC_CHARGING_YELLOWMON_02,
                (int)NpcDefine.NPC_BROTHER_MANEKIN,
                (int)NpcDefine.NPC_BROTHER_YELLOWMON,
                (int)NpcDefine.NPC_BROTHER_SLAMMER,
                (int)NpcDefine.NPC_JAGEAR_HIRUMA,
                (int)NpcDefine.NPC_JAGEAR_NANA,
                (int)NpcDefine.NPC_BUSSTOP1F_NANA,
                (int)NpcDefine.NPC_BUSSTOP1F_HIRUMA,
                //(int)NpcDefine.NPC_ROAD1F_MANEKIN,
                (int)NpcDefine.NPC_BUSSTOP2F_HIRUMA,
                (int)NpcDefine.NPC_ARGUE_NANA_01,
                (int)NpcDefine.NPC_ARGUE_NANA_02,
                (int)NpcDefine.NPC_ARGUE_MANEKIN,
                (int)NpcDefine.NPC_SIT_MANEKIN_01,
                (int)NpcDefine.NPC_SIT_MANEKIN_02,
                (int)NpcDefine.NPC_SIT_KID_01,
                (int)NpcDefine.NPC_SIT_KID_02,
                (int)NpcDefine.NPC_SIT_KID_03,
                (int)NpcDefine.NPC_SIT_HIRUMA_01,
                (int)NpcDefine.NPC_SIT_HIRUMA_02,
                (int)NpcDefine.NPC_SIT_HIRUMA_03,
                (int)NpcDefine.NPC_CASEY,
                (int)NpcDefine.NPC_DIZZY_MANEKIN,
                (int)NpcDefine.NPC_WORRY_HIRUMA,
                (int)NpcDefine.NPC_WATCHING_MANEKIN,
                (int)NpcDefine.NPC_STRETCH_HIRUMA,
                (int)NpcDefine.NPC_ANGRY_NANA,
                (int)NpcDefine.NPC_OBSERVE_HIRUMA,
                (int)NpcDefine.NPC_OBSERVE_NANA,
                (int)NpcDefine.NPC_EMERGENCY_HIRUMA,
                (int)NpcDefine.NPC_EMERGENCY_YELLOWMON,
                (int)NpcDefine.NPC_BOXING_MANEKIN,
                (int)NpcDefine.NPC_BOXING_YELLOWMON,
                (int)NpcDefine.NPC_CROWD_MANEKIN_01,
                (int)NpcDefine.NPC_CROWD_MANEKIN_02,
                (int)NpcDefine.NPC_CROWD_MANEKIN_03,
                (int)NpcDefine.NPC_CROWD_MANEKIN_04,
                (int)NpcDefine.NPC_CROWD_YELLOWMON_01,
                (int)NpcDefine.NPC_CROWD_YELLOWMON_02,
                (int)NpcDefine.NPC_CROWD_YELLOWMON_03,
                (int)NpcDefine.NPC_CROWD_YELLOWMON_04,
            };
    #endregion
    #endregion Coding rule : Value

    #region Coding rule : Function
    /// <summary>
    /// 상호작용 이벤트 등록
    /// </summary>
    public void SetEventTownInteraction(System.Action onEventClearTownTag,
                                        System.Func<TownTagGroup, UniTask> onEventShowTag,
                                        System.Func<TownSmallTalk, UniTask> onEventShowSmallTalk,
                                        System.Func<TownTalk, UniTask> onEventShowTalk)
    {
        // 태그, 스몰토크 등록
        OnEventClearTownTag = onEventClearTownTag;
        OnEventShowTag = onEventShowTag;
        OnEventShowSmallTalk = onEventShowSmallTalk;
        OnEventShowTalk = onEventShowTalk;

        Debug.LogError("#### SetEventTownInteractionSetEventTownInteraction");
    }

    /// <summary>
    /// NPC 정보를 담는다. (Gimmick, Trigger 등도 다 포함)
    /// </summary>
    public void SetTownNpcInfo(ITownSupport townSupport, FieldMapDefine fieldMapEnumId)
    {
        if (townSupport == null)
            return;

        TownNpcInfoGroup townNpcInfoGroup = GetTownNpcInfoGroup(fieldMapEnumId);

        if (townNpcInfoGroup == null)
        {
            IronJade.Debug.LogError($"{fieldMapEnumId} NpcInfoGroup is null.");
            return;
        }

        BaseTownNpcInfo townNpcInfo = townNpcInfoGroup.GetTownObjectInfo(townSupport.TownObjectType, townSupport.DataId);

        // Condition 값이 없으면 비활성화 됩니다.
        if (townNpcInfo == null)
        {
            IronJade.Debug.LogError($"{townSupport.EnumId}({townSupport.DataId}) NpcInfo is null.");
            return;
        }

        townSupport.SetTownNpcInfo(townNpcInfo);
    }

    public void SetEventWarpPointGet(System.Func<string, Transform> onEventWarpPointGet)
    {
        this.onEventWarpPointGet = onEventWarpPointGet;
    }

    /// <summary>
    /// 데코레이터 공장 셋팅
    /// </summary>
    public void SetDecoratorFactory(System.Action onEventFactoryOperate, System.Action onEventFactoryCancel)
    {
        this.onEventFactoryOperate = onEventFactoryOperate;
        this.onEventFactoryCancel = onEventFactoryCancel;
    }

    /// <summary>
    /// 데코레이터 공장을 가동한다.
    /// </summary>
    public void OperateDecoratorFactory()
    {
        // 도입부 특정 시퀀스에서는 데코레이터 가동 X (ex) 코드 경보)
        if (PrologueManager.Instance.IsProgressing && !PrologueManager.Instance.IsCreateDecorator())
            return;

        onEventFactoryOperate?.Invoke();
    }

    /// <summary>
    /// 데코레이터 공장을 멈춘다 (3D모델 제거)
    /// </summary>
    public void CancelDecoratorFactory()
    {
        onEventFactoryCancel?.Invoke();
    }

    /// <summary>
    /// 사운드 음소거
    /// </summary>
    public void SoundMute(bool isMute)
    {
        for (int i = 0; i < AllTownObjects.Count; ++i)
        {
            if (AllTownObjects[i].TownObject == null)
                continue;

            AllTownObjects[i].TownObject.SoundMute(isMute);
        }
    }

    /// <summary>
    /// 마을 오브젝트를 등록한다.
    /// </summary>
    public void AddTownObject(TownObjectType townObjectType, ITownSupport characterObject)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            return;
#endif

        if (!typeToTownObjects.ContainsKey(townObjectType))
            typeToTownObjects.Add(townObjectType, new Dictionary<string, ITownSupport>());

        if (typeToTownObjects[townObjectType].ContainsKey(characterObject.EnumId))
            return;

        Debug.LogError(typeToTownObjects.Count + "AddTownObject() 타이밍 확인차 ");

        typeToTownObjects[townObjectType].Add(characterObject.EnumId, characterObject);
        allTownObjects.Add(characterObject);
        characterObject.Add();
    }

    /// <summary>
    /// 마을 오브젝트를 제거한다.
    /// </summary>
    public void RemoveTownObject(TownObjectType townObjectType, ITownSupport characterObject)
    {
        if (!typeToTownObjects.ContainsKey(townObjectType))
            return;

        typeToTownObjects[townObjectType].Remove(characterObject.EnumId);
        allTownObjects.Remove(characterObject);
        characterObject.Remove();
    }

    /// <summary>
    /// 목록을 초기화한다.
    /// </summary>
    public void ClearTownObjects()
    {
        typeToTownObjects.Clear();
        allTownObjects.Clear();
        floors.Clear();
        
        // 수집된 LODGroup을 Clear한다. 
        if (GameSettingManager.Instance != null)
        {
            GameSettingManager.Instance.GraphicSettingModel.LodGroupUnitModel.ClearLodGroup();
        }
    }

    /// <summary>
    /// 오브젝트들을 모두 제거한다.
    /// </summary>
    public void DestroyTownObject()
    {
       IronJade.Debug.Log("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@ DestroyTownObject");

        for (int i = 0; i < allTownObjects.Count; ++i)
            allTownObjects[i].DestroyObject();
    }

    public void ActiveAllTownObjects(bool value)
    {
        for (int i = 0; i < allTownObjects.Count; ++i)
            allTownObjects[i].VisibleTownObject(value);
    }

    public void SetFloors(List<Transform> floorList)
    {
        floors = floorList;
    }

    /// <summary>
    /// 플레이어를 제외한 마을 오브젝트들 시작
    /// </summary>
    public async UniTask StartProcessAsync()
    {
        Debug.LogError("##### StartProcessAsync()");

        await UniTask.WhenAll(StartProcess(TownObjectType.Npc),
                              StartProcess(TownObjectType.Gimmick),
                              StartProcess(TownObjectType.Trigger));
    }

    /// <summary>
    /// 타입에 맞는 마을 오브젝트들 생성 및 시작
    /// </summary>
    private async UniTask StartProcess(TownObjectType townObjectType)
    {
        Debug.LogError("##### StartProcess()" + townObjectType );

        if( typeToTownObjects.Count() <= 0  )
        {
            Debug.LogError(townObjectType + "의 갯수가 0개이다.. 로딩 안댄거 같은데..");
        }


        if (!typeToTownObjects.ContainsKey(townObjectType))
            return;


        System.Func<ITownSupport, UniTask> startLogic = async (townSupport) =>
        {
            if (townSupport == null)
                return;

            await townSupport.StartProcess();

            if (townSupport.CheckCondition())
            {
                if (townSupport.TownObject.Town3DModel.CheckSafeNull())
                    await townSupport.LoadTownObject();
            }
            else
            {
                await townSupport.DestroyObject();
            }
        };

        // IOS 외에는 병렬로 처리함
#if UNITY_IOS
        foreach (var townSupport in typeToTownObjects[townObjectType].Values)
        {
            await startLogic(townSupport);
        }
#else
        UniTask[] uniTasks = new UniTask[typeToTownObjects[townObjectType].Values.Count];
        int index = 0;

        foreach (var townSupport in typeToTownObjects[townObjectType].Values)
        {
            uniTasks[index++] = startLogic(townSupport);


            Debug.LogError(townObjectType + "로딩 잘댐 .." + townSupport.EnumId + " / " + townSupport.Transform.gameObject.name );

        }

        await UniTask.WhenAll(uniTasks);
#endif
    }

    /// <summary>
    /// 플레이어를 제외한 마을 오브젝트들 생성 및 갱신
    /// </summary>
    public async UniTask RefreshProcess(bool whenall = true)
    {
        IronJade.Debug.Log($"Start Town Refresh (whenAll : {whenall})");

        if (whenall)
        {
            await UniTask.WhenAll(RefreshProcess(TownObjectType.Npc),
                        RefreshProcess(TownObjectType.Gimmick),
                        RefreshProcess(TownObjectType.Trigger));
        }
        else
        {

            await RefreshProcess(TownObjectType.Npc);
            await RefreshProcess(TownObjectType.Gimmick);
            await RefreshProcess(TownObjectType.Trigger);
        }

        // 퀘스트 아이콘 위치 갱신
        MissionManager.Instance.NotifyStoryQuestTracking();

        IronJade.Debug.Log($"End Town Refresh (whenAll : {whenall})");
    }

    /// <summary>
    /// 타입에 맞는 마을 오브젝트들 생성 및 갱신
    /// </summary>
    private async UniTask RefreshProcess(TownObjectType townObjectType)
    {
        if (typeToTownObjects.Count() <= 0)
        {
            Debug.LogError(townObjectType + "의 갯수가 0개이다.. 로딩 안댄거 같은데..RefreshProcess");
        }



        if (!typeToTownObjects.ContainsKey(townObjectType))
            return;

        System.Func<ITownSupport, UniTask> refreshLogic = async (townSupport) =>
        {
            if (townSupport == null)
                return;

            await townSupport.RefreshProcess();

            if (townSupport.CheckCondition())
            {
                if (townSupport.TownObject.Town3DModel.CheckSafeNull())
                    await townSupport.LoadTownObject();
            }
            else
            {
                await townSupport.DestroyObject();
            }
        };

        // IOS 외에는 병렬로 처리함
#if UNITY_IOS
        foreach (var townSupport in typeToTownObjects[townObjectType].Values)
        {
            await refreshLogic(townSupport);
        }
#else
        UniTask[] uniTasks = new UniTask[typeToTownObjects[townObjectType].Values.Count];
        int index = 0;

        foreach (var townSupport in typeToTownObjects[townObjectType].Values)
        {
            uniTasks[index++] = refreshLogic(townSupport);
        }

        await UniTask.WhenAll(uniTasks);
#endif
    }

    /// <summary>
    /// 마을 오브젝트를 생성한다.
    /// (프레임 드랍을 막기 위해 Queue에 담아놓고 순차적으로 생성한다.)
    /// </summary>
    public async UniTask CreateTownObject(ITownSupport townSupport)
    {
        await townSupport.LoadTownObject();
    }

    public async UniTask DestroyTownObject(ITownSupport townSupport)
    {
       IronJade.Debug.Log("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@ DestroyTownObject");

        await townSupport.DestroyObject();
    }
    /// <summary>
    /// 필드맵에 해당하는 NPC 그룹을 로드한다.
    /// </summary>
    public void LoadAllTownNpcInfoGroup()
    {
        var fieldMapTable = TableManager.Instance.GetTable<FieldMapTable>();
        FieldMapDefine[] fieldMapTableDefines = fieldMapTable.FindAll(x => (VisibleState)x.GetSTATE() == VisibleState.Live && Enum.IsDefined(typeof(FieldMapDefine), x.GetID()))
                                                .Select(x => (FieldMapDefine)x.GetID())
                                                .ToArray();

        townNpcInfoGroupDic.Clear();

        NpcTable npcTable = TableManager.Instance.GetTable<NpcTable>();
        GimmickTable gimmickTable = TableManager.Instance.GetTable<GimmickTable>();
        TriggerTable triggerTable = TableManager.Instance.GetTable<TriggerTable>();
        StoryQuestTable storyQuestTable = TableManager.Instance.GetTable<StoryQuestTable>();

        // Condition 데이터를 오브젝트 타입에 따라 분류합니다.
        Dictionary<TownConditionKey, List<int>> conditions = GetConditionDataIdsByTownObject();

        if (conditions != null && conditions.Count > 0)
        {
            // 테이블에서 속해있는 필드맵을 확인 및 세팅합니다.
            foreach (var fieldMapDefine in fieldMapTableDefines)
            {
                TownNpcInfoGroup infoGroup = new TownNpcInfoGroup();

                infoGroup.SetFieldMap(fieldMapDefine);

                for (int i = 0; i < npcTable.GetDataTotalCount(); i++)
                {
                    NpcTableData npcData = npcTable.GetDataByIndex(i);

                    SetInfoGroup(conditions, ref infoGroup, in npcData);
                }

                for (int i = 0; i < gimmickTable.GetDataTotalCount(); i++)
                {
                    GimmickTableData gimmickData = gimmickTable.GetDataByIndex(i);

                    SetInfoGroup(conditions, ref infoGroup, in gimmickData);
                }

                for (int i = 0; i < triggerTable.GetDataTotalCount(); i++)
                {
                    TriggerTableData triggerData = triggerTable.GetDataByIndex(i);

                    SetInfoGroup(conditions, ref infoGroup, in triggerData);
                }

                // 퀘스트로 세팅하는 경우가 제일 마지막이 되어야 합니다.
                for (int i = 0; i < storyQuestTable.GetDataTotalCount(); i++)
                {
                    StoryQuestTableData storyQuestData = storyQuestTable.GetDataByIndex(i);

                    SetInfoGroup(conditions, ref infoGroup, in storyQuestData);
                }

                townNpcInfoGroupDic[fieldMapDefine] = infoGroup;
            }
        }

        //townNpcWarpInfo = await UtilModel.Resources.LoadAsync<TownNpcWarpInfo>(StringDefine.PATH_TOWN_NPC_WARP_INFO_GROUP_PATH);
        townNpcWarpInfo = new TownNpcWarpInfo();
        townNpcWarpInfo.FindAllRoadsProcess(conditions);
        townNpcWarpInfo.SetEventCheckCondition(MissionManager.Instance.CheckMissionProgressState);
        townNpcWarpInfo.SetEventCheckActive(OnEventCheckActive);
    }

    private Dictionary<TownConditionKey, List<int>> GetConditionDataIdsByTownObject()
    {
        Dictionary<TownConditionKey, List<int>> conditions = new Dictionary<TownConditionKey, List<int>>();
        NpcConditionTable npcConditionTable = TableManager.Instance.GetTable<NpcConditionTable>();

        if (npcConditionTable == null)
        {
            IronJade.Debug.LogError("NpcConditionTable is null!!");
            return null;
        }

        for (int i = 0; i < npcConditionTable.GetDataTotalCount(); i++)
        {
            NpcConditionTableData npcConditionData = npcConditionTable.GetDataByIndex(i);
            CommonTownObjectType townObjectType = (CommonTownObjectType)npcConditionData.GetNPC_TYPE();
            int townObjectDataId = npcConditionData.GetNPC_ID(0);
            FieldMapDefine fieldMap = (FieldMapDefine)npcConditionData.GetFIELD_MAP_LOCATION();

            if (townObjectType == CommonTownObjectType.None ||
                townObjectDataId == 0 ||
                fieldMap == FieldMapDefine.None)
                continue;

            TownConditionKey key = new TownConditionKey(townObjectType, townObjectDataId, fieldMap);

            if (conditions.TryGetValue(key, out var dataIds))
                dataIds.Add(npcConditionData.GetID());
            else
                conditions.Add(key, new List<int>() { npcConditionData.GetID() });
        }

        return conditions;
    }

    private void SetInfoGroup(Dictionary<TownConditionKey, List<int>> conditions, ref TownNpcInfoGroup infoGroup, in NpcTableData npcData)
    {
        if (infoGroup == null)
            return;

        if (conditions == null || conditions.Count == 0)
            return;

        FieldMapDefine fieldMap = infoGroup.FieldMap;
        CommonTownObjectType commonType = CommonTownObjectType.Npc;
        int dataObjectId = npcData.GetID();

        TownConditionKey key = new TownConditionKey(commonType, dataObjectId, fieldMap);

        if (!conditions.TryGetValue(key, out var conditionDataIds))
            return;

        TownNpcInfo npcInfo = new TownNpcInfo();

        npcInfo.SetData(npcData);
        npcInfo.SetConditions(conditionDataIds);
        npcInfo.SetDataFieldMapId((int)fieldMap);

#if UNITY_EDITOR
        if (IsTestCameraByNpcTalk)
        {
            NpcInteractionTable npcInteractionTable = TableManager.Instance.GetTable<NpcInteractionTable>();
            NpcInteractionTableData testNpcInteractionData = npcInteractionTable.FindAll(x => Enum.IsDefined(typeof(NpcInteractionDefine), x.GetID()))
                                                .Where(x => (TownObjectInteractionType)x.GetINTERACTION_TYPE() == TownObjectInteractionType.Talk)
                                                .FirstOrDefault();

            // 대화 선택지 추가 및 강제 활성화
            if (!testNpcInteractionData.IsNull())
            {
                for (int i = 0; i < npcInfo.ConditionCount; i++)
                {
                    var condition = npcInfo.GetConditionInfo(i) as TownNpcConditionInfo;

                    var interactionInfos = condition.interactionInfos;
                    var newInteractionInfos = new TownNpcInteractionInfo[interactionInfos.Length + 1];
                    var newTestInteractionInfo = new TownNpcInteractionInfo();

                    newTestInteractionInfo.SetData(testNpcInteractionData);
                    newTestInteractionInfo.interactionLocalizationId = 0;

                    Array.Copy(interactionInfos, newInteractionInfos, interactionInfos.Length);
                    newInteractionInfos[newInteractionInfos.Length - 1] = newTestInteractionInfo;

                    condition.interactionInfos = newInteractionInfos;
                    condition.visibleState = TownObjectVisibleState.Visible;

                    if (i == npcInfo.ConditionCount - 1)
                    {
                        condition.missionCondition.openDataType = OpenConditionType.None;
                        condition.missionCondition.openDataId = 0;
                        condition.missionCondition.closeDataId = 0;
                    }
                }
            }
        }
#endif

        infoGroup.SetInfo(TownObjectType.Npc, npcData.GetID(), npcInfo);
    }

    private void SetInfoGroup(Dictionary<TownConditionKey, List<int>> conditions, ref TownNpcInfoGroup infoGroup, in GimmickTableData gimmickData)
    {
        if (infoGroup == null)
            return;

        if (conditions == null || conditions.Count == 0)
            return;

        FieldMapDefine fieldMap = infoGroup.FieldMap;
        CommonTownObjectType commonType = CommonTownObjectType.Gimmick;
        int dataObjectId = gimmickData.GetID();

        TownConditionKey key = new TownConditionKey(commonType, dataObjectId, fieldMap);

        if (!conditions.TryGetValue(key, out var conditionDataIds))
            return;

        TownGimmickInfo gimmickInfo = new TownGimmickInfo();

        gimmickInfo.SetData(gimmickData);
        gimmickInfo.SetConditions(conditionDataIds);
        gimmickInfo.SetDataFieldMapId((int)fieldMap);

        infoGroup.SetInfo(TownObjectType.Gimmick, gimmickData.GetID(), gimmickInfo);
    }

    private void SetInfoGroup(Dictionary<TownConditionKey, List<int>> conditions, ref TownNpcInfoGroup infoGroup, in TriggerTableData triggerData)
    {
        if (infoGroup == null)
            return;

        if (conditions == null || conditions.Count == 0)
            return;

        FieldMapDefine fieldMap = infoGroup.FieldMap;
        CommonTownObjectType commonType = CommonTownObjectType.Trigger;
        int dataObjectId = triggerData.GetID();

        TownConditionKey key = new TownConditionKey(commonType, dataObjectId, fieldMap);

        if (!conditions.TryGetValue(key, out var conditionDataIds))
            return;

        TownTriggerInfo triggerInfo = new TownTriggerInfo();

        triggerInfo.SetData(triggerData);
        triggerInfo.SetConditions(conditionDataIds);
        triggerInfo.SetDataFieldMapId((int)fieldMap);

        infoGroup.SetInfo(TownObjectType.Trigger, triggerData.GetID(), triggerInfo);
    }

    private void SetInfoGroup(Dictionary<TownConditionKey, List<int>> conditions, ref TownNpcInfoGroup infoGroup, in StoryQuestTableData storyQuestData)
    {
        if (infoGroup == null)
            return;

        if (conditions == null || conditions.Count == 0)
            return;

        FieldMapDefine fieldMap = infoGroup.FieldMap;
        CommonTownObjectType commonType = (CommonTownObjectType)storyQuestData.GetNPC_TYPE();
        int dataObjectId = storyQuestData.GetNPC_LOCATIONCount() > 0 ? storyQuestData.GetNPC_LOCATION(0) : 0;

        // 없는 경우에만 추가합니다.
        switch (commonType)
        {
            case CommonTownObjectType.Npc:
                SetInfoGroup(conditions, ref infoGroup, TableManager.Instance.GetTable<NpcTable>().GetDataByID(dataObjectId));
                break;

            case CommonTownObjectType.Gimmick:
                SetInfoGroup(conditions, ref infoGroup, TableManager.Instance.GetTable<GimmickTable>().GetDataByID(dataObjectId));
                break;

            case CommonTownObjectType.Trigger:
                SetInfoGroup(conditions, ref infoGroup, TableManager.Instance.GetTable<TriggerTable>().GetDataByID(dataObjectId));
                break;
        }
    }

    public void UnloadAllTownNpcInfoGroup()
    {
        townNpcInfoGroupDic.Clear();
        townNpcWarpInfo = null;
    }

    private bool OnEventCheckActive(int dataId)
    {
        var warpTarget = GetTownObjectByDataId(dataId);

        return warpTarget != null;
    }

    /// <summary>
    /// 포탈로 이동 가능한 길 찾기 정보 갱신
    /// </summary>
    public void SetConditionRoad()
    {
        townNpcWarpInfo?.SetConditionRoad();
    }

    /// <summary>
    /// 마을 캐릭터들 다 제거 (메모리)
    /// </summary>
    public void UnLoadTown3DModels()
    {
       IronJade.Debug.Log("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@ UnLoadTown3DModels");

        foreach (var townObject in typeToTownObjects[TownObjectType.Npc].Values)
        {
            if (townObject == null)
                continue;

            townObject.DestroyObject();
        }
    }

    /// <summary>
    /// NPC 길찾기
    /// </summary>
    public int DFS(FieldMapDefine startFieldMapDefine, FieldMapDefine targetFieldMapDefine, int targetDataId)
    {
        return townNpcWarpInfo.FindWarpDataNpcId(startFieldMapDefine, targetFieldMapDefine);
    }

    public IReadOnlyList<Road> GetRoads(FieldMapDefine startFieldMapDefine, bool setConditionRoad = false)
    {
        if (setConditionRoad)
            townNpcWarpInfo.SetConditionRoad();

        return townNpcWarpInfo.GetStartindexes(startFieldMapDefine);
    }

    /// <summary>
    /// 필드맵에 해당하는 NPC 그룹 정보를 얻는다.
    /// (NPC, GIMMICK 등 데이터가 필요한 마을 오브젝트들의 정보가 담긴 그룹)
    /// </summary>
    public TownNpcInfoGroup GetTownNpcInfoGroup(FieldMapDefine fieldMapDefine)
    {
        return townNpcInfoGroupDic.TryGetValue(fieldMapDefine, out TownNpcInfoGroup townNpcInfoGroup) ? townNpcInfoGroup : null;
    }

    /// <summary>
    /// 데이터 ENUM_ID와 매칭되는 TownSupport를 찾아준다.
    /// </summary>
    public ITownSupport GetTownObjectByEnumId(string enumId)
    {
        ITownSupport npc = GetTownObjectByEnumId(TownObjectType.Npc, enumId);
        if (npc != null)
            return npc;

        ITownSupport gimmick = GetTownObjectByEnumId(TownObjectType.Gimmick, enumId);
        if (gimmick != null)
            return gimmick;

        ITownSupport trigger = GetTownObjectByEnumId(TownObjectType.Trigger, enumId);
        if (trigger != null)
            return trigger;

        ITownSupport player = GetTownObjectByEnumId(TownObjectType.MyPlayer, enumId);
        return player;
    }

    /// <summary>
    /// 데이터 ID와 매칭되는 TownSupport를 찾아준다.
    /// </summary>
    public ITownSupport GetTownObjectByDataId(int dataId)
    {
        ITownSupport npc = GetTownObjectByDataId(TownObjectType.Npc, dataId);
        if (npc != null)
            return npc;

        ITownSupport gimmick = GetTownObjectByDataId(TownObjectType.Gimmick, dataId);
        if (gimmick != null)
            return gimmick;

        ITownSupport trigger = GetTownObjectByDataId(TownObjectType.Trigger, dataId);
        if (trigger != null)
            return trigger;

        ITownSupport player = GetTownObjectByDataId(TownObjectType.MyPlayer, dataId);
        return player;
    }

    /// <summary>
    /// TownObjectType에 해당하는 목록을 얻는다.
    /// </summary>
    public ITownSupport[] GetTownObjectsByType(TownObjectType townObjectType)
    {
        if (!typeToTownObjects.ContainsKey(townObjectType))
            return null;

        ITownSupport[] townObjectArray = new ITownSupport[typeToTownObjects[townObjectType].Values.Count];

        int index = 0;

        foreach (ITownSupport townObject in typeToTownObjects[townObjectType].Values)
        {
            townObjectArray[index] = townObject;
            index++;
        }

        return townObjectArray;
    }


    /// <summary>
    /// EnumId로 TownObject를 찾는다.
    /// </summary>
    public ITownSupport GetTownObjectByEnumId(TownObjectType townObjectType, string targetKey)
    {
        if (!typeToTownObjects.ContainsKey(townObjectType))
            return null;

        if (typeToTownObjects[townObjectType].ContainsKey(targetKey))
            return typeToTownObjects[townObjectType][targetKey];

        return null;
    }

    /// <summary>
    /// DataId로 TownObject를 찾는다.
    /// </summary>
    public ITownSupport GetTownObjectByDataId(TownObjectType townObjectType, int dataId = 0)
    {
        if (!typeToTownObjects.ContainsKey(townObjectType))
            return null;

        foreach (ITownSupport townObject in typeToTownObjects[townObjectType].Values)
        {
            if (townObject.DataId == dataId)
                return townObject;
        }

        return null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public int GetTownFloor(Vector3 pos)
    {
        if (floors == null)
            return 0;

        for (int i = 0; i < floors.Count; i++)
        {
            if (pos.y < floors[i].transform.position.y)
                return i;
        }

        return 0;
    }

    public int GetPlayerFloor()
    {
        if (PlayerManager.Instance.MyPlayer == null)
            return 0;

        if (!PlayerManager.Instance.MyPlayer.IsExistTownPlayer)
            return 0;

        if (floors == null)
            return 0;

        for (int i = 0; i < floors.Count; i++)
        {
            if (PlayerManager.Instance.MyPlayer.TownPlayer.Transform.position.y < floors[i].transform.position.y)
                return i;
        }

        return 0;
    }

    public Transform GetWarpPoint(string key)
    {
        if (onEventWarpPointGet == null)
            return null;

        return onEventWarpPointGet.Invoke(key);
    }

    /// <summary>
    /// 최종 타겟에 가기 위한 경로 중 지나야 할 타겟이 있으면 중간 타겟을 가져오거나, 최종 타겟을 가져옴.
    /// </summary>
    public ITownSupport GetMidPointTarget(FieldMapDefine targetFieldMap, int targetDataId)
    {
        FieldMapDefine currentMap = PlayerManager.Instance.MyPlayer.CurrentFieldMap;

        if (currentMap != targetFieldMap)
        {
            // 각 필드별 워프 가능 NPC List를 만든다.
            // NPC 중에 현재 내 필드에 갈 수 있는지를 DFS로 찾는다.
            int warpTargetDataId = DFS(PlayerManager.Instance.MyPlayer.CurrentFieldMap, targetFieldMap, targetDataId);

            // 타겟으로 가는 길이 없음
            if (warpTargetDataId == 0)
                return null;

            return GetTownObjectByDataId(warpTargetDataId);
        }
        else
        {
            return GetTownObjectByDataId(targetDataId);
        }
    }

    public TownObjectType ConvertToObjectType(CommonTownObjectType type)
    {
        return type switch
        {
            CommonTownObjectType.Npc => TownObjectType.Npc,
            CommonTownObjectType.Gimmick => TownObjectType.Gimmick,
            CommonTownObjectType.Trigger => TownObjectType.Trigger,
            _ => TownObjectType.None
        };
    }

    public bool IsMissionRelatedNpc(FieldMapDefine fiedlMapDefine, int npcDataID)
    {
        var infoGroup = GetTownNpcInfoGroup(fiedlMapDefine);

        if (infoGroup == null)
            return false;

        var info = infoGroup.GetTownObjectInfo(TownObjectType.Npc, npcDataID);

        if (info == null)
        {
            info = infoGroup.GetTownObjectInfo(TownObjectType.Gimmick, npcDataID);

            if (info == null)
                return false;
        }

        for (int i = 0; i < info.ConditionCount; i++)
        {
            if (MissionManager.Instance.CheckMissionProgressState(info.GetConditionInfo(i).GetMissionCondition()))
                return true;
        }

        return false;
    }

    public bool IsDecoratorTypeNPC(int dataId)
    {
        if (decoTypeNPC == null)
            return false;

        return decoTypeNPC.Contains(dataId);
    }
    #endregion Coding rule : Function
}
