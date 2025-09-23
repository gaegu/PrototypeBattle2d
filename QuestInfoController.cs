//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.Observer.Core;
using IronJade.Table.Data;          // 데이터 테이블
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using UnityEngine;
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class QuestInfoController : BaseController, IObserver
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.QuestInfoPopup; } }
    public override void SetModel() { SetModel(new QuestInfoPopupModel()); }
    private QuestInfoPopup View { get { return base.BaseView as QuestInfoPopup; } }
    private QuestInfoPopupModel Model { get { return GetModel<QuestInfoPopupModel>(); } }
    //=========================================================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        ObserverManager.AddObserver(MissionObserverID.Update, this);

        Model.SetEventClose(OnEventClose);
        Model.SetEventShowFilterPopup(OnEventShowFilterPopup);
        Model.SetCallbackEvent(OnEventAutoMoving, OnEventRefresh);

        Model.SetFilterModel(GetOpenFieldMaps());
        Model.SetFilterQuestGroups();
        Model.SortFilteredGroupModels();

        Model.SetFocusIndex(0);

        SetCompletedQuestCount();
    }

    public override async UniTask LoadingProcess()
    {
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override void Refresh()
    {
        SetCompletedQuestCount();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Story/QuestInfoPopup";
    }

    public override UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        ObserverManager.RemoveObserver(MissionObserverID.Update, this);

        return base.Exit(onEventExtra);
    }

    public override void InitializeTestModel(params object[] parameters)
    {
        TableManager.Instance.LoadTable<StoryQuestTable>();

        StoryQuestTable storyQuestTable = TableManager.Instance.GetTable<StoryQuestTable>();
        MissionGeneratorModel generatorModel = new MissionGeneratorModel();
        int count = Mathf.Min(storyQuestTable.GetDataTotalCount(), 10);

        // 데이터에서 가져오기
        List<BaseStoryQuest> quests = new List<BaseStoryQuest>();
        for (int i = 0; i < count; i++)
        {
            StoryQuestTableData data = storyQuestTable.GetDataByIndex(i);

            quests.Add(generatorModel.GetStoryQuestFromData(data));
        }

        // 임시로 지정한 그룹별로 분리
        Dictionary<int, List<BaseStoryQuest>> groupDic = new Dictionary<int, List<BaseStoryQuest>>();

        for (int i = 0; i < quests.Count; i++)
        {
            int index = i % 3;

            if (!groupDic.TryGetValue(index, out var groupQuests))
                groupDic[index] = new List<BaseStoryQuest>() { quests[i] };
            else
                groupQuests.Add(quests[i]);
        }

        // 모델 세팅
        QuestGroupUnitModel[] groupModels = new QuestGroupUnitModel[3];

        for (int i = 0; i < 3; i++)
        {
            groupModels[i] = new QuestGroupUnitModel(i, groupDic[i]);
        }

        Model.SetQuestContentType(QuestContentType.StoryQuest);
        Model.SetGroupModels(groupModels);
    }

    private void OnEventClose()
    {
        Exit().Forget();
    }

    private async UniTask OnEventAutoMoving(BaseStoryQuest storyQuest)
    {
        if (CheckPlayingAnimation())
            return;

        await MissionManager.Instance.SetTrackingDataId(storyQuest.DataId);
        MissionManager.Instance.OnEventAutoMove(true);

        //var storyQuestView = UIManager.Instance.GetController(UIType.QuestView);

        //if (storyQuestView == null || storyQuestView.CheckPlayingAnimation())
        //    return;

        //// 로비로..
        //Exit(async (state) =>
        //{
        //    if (state == UISubState.Finished)
        //    {
        //        storyQuestView.Exit(async (state) =>
        //        {
        //            if (state == UISubState.Finished)
        //            {
        //                if (!UIManager.Instance.CheckOpenUI(UIType.ApplicationPopup))
        //                {
        //                    StartAutoMove(storyQuest);
        //                }
        //                else
        //                {
        //                    UIManager.Instance.SetSpecificExitEvent(UIType.ApplicationPopup, async () => { StartAutoMove(storyQuest); });

        //                    var applicationPopup = UIManager.Instance.GetController(UIType.ApplicationPopup);

        //                    applicationPopup.Exit().Forget();
        //                }
        //            }
        //        }).Forget();
        //    }
        //}).Forget();
    }

    private void OnEventShowFilterPopup()
    {
        BaseController filterController = UIManager.Instance.GetController(UIType.FilterPopup);
        FilterPopupModel model = filterController.GetModel<FilterPopupModel>();

        model.SetEventConfirm(OnEventChangeFilter);
        model.SetFilterModel(Model.FilterModel);

        UIManager.Instance.EnterAsync(filterController).Forget();
    }

    private void OnEventChangeFilter()
    {
        if (!Model.FilterModel.CheckChangeFilter())
            return;

        Model.FilterModel.SaveFilter();
        Model.SetFilterQuestGroups();
        Model.SortFilteredGroupModels();

        View.ShowQuestGroup().Forget();
    }

    private async UniTask OnEventRefresh(int focusIndex)
    {
        var groupModel = Model.GetFilteredQuestGroupModel(focusIndex);

        Model.SortFilteredGroupModels();

        Model.SetFocusIndex(groupModel.Index);

        View.RefreshAsync().Forget();
    }

    private IEnumerable<int> GetOpenFieldMaps()
    {
        HashSet<int> fieldMaps = new HashSet<int>();

        DailyQuestTable dailyQuestTable = TableManager.Instance.GetTable<DailyQuestTable>();

        for (int i = 0; i < dailyQuestTable.GetDataTotalCount(); i++)
        {
            DailyQuestTableData dailyQuestData = dailyQuestTable.GetDataByIndex(i);
            int dataStoryQuestId = dailyQuestData.GetSTORY_QUEST_OPEN_CONDITION();
            int dataFieldMapId = dailyQuestData.GetFIELD_MAP();

            if (dataStoryQuestId > 0 && MissionManager.Instance.GetMissionProgress(MissionContentType.StoryQuest, dataStoryQuestId) != MissionProgressState.Completed)
                continue;

            if (!fieldMaps.Contains(dataFieldMapId))
                fieldMaps.Add(dataFieldMapId);
        }

        return fieldMaps;
    }

    private void StartAutoMove(BaseStoryQuest storyQuest)
    {
        CharacterParam characterParam = new CharacterParam();

        var target = storyQuest.GetCurrentTarget();
        int dataId = target.DataId;
        int targetFieldMap = target.DataFieldMapId;
        TownObjectType townObjectType = target.TargetType;

        characterParam.SetAutoMoveTarget(townObjectType, dataId, targetFieldMap);
        characterParam.SetAutoMoveState(CharacterAutoMoveState.Move);
        characterParam.SetActiveAutoMoveStopButton(false);
        ObserverManager.NotifyObserver(CharacterObserverID.AutoMoveCharacter, characterParam);
    }

    private void SetCompletedQuestCount()
    {
        Model.SetCompletedQuestCount(GetQuestCompletedCount());
    }

    private string GetQuestCompletedCount()
    {
        switch (Model.QuestType)
        {
            case QuestContentType.StoryQuest:
                {
                    StoryQuestProgressModel progressModel = MissionManager.Instance.GetProgressModel<StoryQuestProgressModel>();
                    int count = progressModel.CompletedQuestGroupCount;
                    int maxCount = 0;

                    return string.Format(StringDefine.STRING_FORMAT_DIVISION_COUNT, count, maxCount);
                }

            case QuestContentType.DailyJob:
                {
                    DailyQuestProgressModel progressModel = MissionManager.Instance.GetProgressModel<DailyQuestProgressModel>();
                    int count = progressModel.CompletedQuestGroupCount;
                    int maxCount = (int)TableManager.Instance.GetBalanceValueByIndex(BalanceDefine.BALANCE_MAX_DAILY_QUEST);
                    maxCount += PlayerManager.Instance.MyPlayer.User.MembershipModel.GetEffectValue(SpecialEffectType.DailyQuestMaxCount);

                    return string.Format(StringDefine.STRING_FORMAT_DIVISION_COUNT, count, maxCount);
                }

            default:
                return string.Empty;
        }
    }

    public override async UniTask<GameObject> GetTutorialFocusObject(string stepKey)
    {
        TutorialExplain stepType = (TutorialExplain)Enum.Parse(typeof(TutorialExplain), stepKey);
        switch (stepType)
        {
            case TutorialExplain.StoryQuestInfoQuestGroup:
                return View.GetStoryQuestGroupUnit(0).gameObject;

            case TutorialExplain.StoryQuestInfoAccept:
                return View.GetStoryQuestGroupUnit(0).GetDailyQuestStepUnit().AcceptButton;

            case TutorialExplain.StoryQuestInfoAutoMove:
                return View.GetStoryQuestGroupUnit(0).GetDailyQuestStepUnit().AutoMoveButton;

            default:
                return await base.GetTutorialFocusObject(stepKey);
        }
    }

    public override async UniTask TutorialStepAsync(Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            case TutorialExplain.StoryQuestInfoAccept:
                {
                    var stepUnit = View.GetStoryQuestGroupUnit(0).GetDailyQuestStepUnit();
                    stepUnit.OnClickAcceptQuest();
                    await UniTask.WaitUntil(() => stepUnit.AutoMoveButton.activeSelf);
                    break;
                }


            case TutorialExplain.StoryQuestInfoAutoMove:
                {
                    View.GetStoryQuestGroupUnit(0).GetDailyQuestStepUnit().OnClickAutoMoving(true);
                    break;
                }

        }

        await base.TutorialStepAsync(type);
    }

    void IObserver.HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        if (View == null)
            return;

        switch (observerMessage)
        {
            case MissionObserverID.Update:
                {
                    SetCompletedQuestCount();
                }
                break;
        }
    }
    #endregion Coding rule : Function
}