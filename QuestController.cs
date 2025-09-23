//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.LowLevel.Server.Web;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class QuestController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.QuestView; } }
    public override void SetModel() { SetModel(new QuestViewModel()); }
    private QuestView View { get { return base.BaseView as QuestView; } }
    private QuestViewModel Model { get { return GetModel<QuestViewModel>(); } }
    //=========================================================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    private bool isTest = false;
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        Model.SetEventClose(OnEventClose);

        SetQuests();

        var controller = GetOpenDetailPopupController();

        if (controller != null)
            UIManager.Instance.EnterAsync(controller);
    }

    public override async UniTask LoadingProcess()
    {
#if UNITY_EDITOR
        //if (isTest)
        //{
        //    await MissionManager.Instance.RequestGet(MissionContentType.StoryQuest);
        //    await MissionManager.Instance.RequestGet(MissionContentType.DailyQuest);

        //    var progressModel = MissionManager.Instance.GetProgressModel(MissionContentType.DailyQuest) as DailyQuestProgressModel;

        //    MissionGeneratorModel generatorModel = new MissionGeneratorModel();

        //    DailyQuest testQuest1 = generatorModel.GetDailyQuestFromDto(new DailyQuestContentsDto((int)DailyQuestDefine.DAILY_QUEST_01_01));
        //    DailyQuest testQuest2 = generatorModel.GetDailyQuestFromDto(new DailyQuestContentsDto((int)DailyQuestDefine.DAILY_QUEST_02_01));

        //    progressModel.AddQuestByTest(testQuest1);
        //    progressModel.AddQuestByTest(testQuest2);

        //    SetQuests();
        //}
#endif
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();


    }

    public override void Refresh()
    {
        UpdateRedDot();

        View.RefreshAsync().Forget();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Story/QuestView";
    }

    public override void InitializeTestModel(params object[] parameters)
    {
        isTest = true;
    }

    public override UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        Model.ResetOpenQuestDetail();

        return base.Exit(onEventExtra);
    }

    private void OnEventClose()
    {
        Exit().Forget();
    }

    private BaseController GetOpenDetailPopupController()
    {
        if (!Model.IsOpenQuestDetail)
            return null;

        int userId = PlayerManager.Instance.MyPlayer.User.UserId;
        var typeModel = Model.GetQuestTypeModelByOpenPopup();

        if (typeModel == null)
            return null;

        typeModel.SetOpenStepDetail(userId);

        var storyQuestInfoPopup = UIManager.Instance.GetController(UIType.QuestInfoPopup);
        var model = storyQuestInfoPopup.GetModel<QuestInfoPopupModel>();

        model.SetQuestContentType(typeModel.QuestType);
        model.SetGroupModels(typeModel.GroupModels);

        return storyQuestInfoPopup;
    }

    private void SetQuests()
    {
        var storyQuests = MissionManager.Instance.GetMissions(MissionContentType.StoryQuest);
        var subStoryQuests = MissionManager.Instance.GetMissions(MissionContentType.SubStoryQuest);
        var characterQuests = MissionManager.Instance.GetMissions(MissionContentType.CharacterQuest);
        var dailyQuests = MissionManager.Instance.GetMissions(MissionContentType.DailyQuest);

        Model.Initialize(storyQuests.Union(subStoryQuests).Union(characterQuests).Union(dailyQuests).Cast<BaseStoryQuest>());

        UpdateRedDot();
    }

    private void UpdateRedDot()
    {
        for (int index = 0; index < Model.TypeCount; index++)
        {
            var typeModel = Model.GetQuestTypeModel(index);

            QuestContentType type = typeModel.QuestType;
            bool isOn = CheckRedDotByQuest(typeModel.QuestType);
            RedDotManager.Instance.Setting(type, new RedDot(isOn));
        }
    }

    private bool CheckRedDotByQuest(QuestContentType questContentType)
    {
        if (questContentType == QuestContentType.None)
            return false;

        var questTypeModel = Model.GetQuestTypeModel(questContentType);
        if (questTypeModel == null || questTypeModel.GroupCount == 0)
            return false;

        return MissionManager.Instance.CheckNewQuest(questContentType);
    }

    public override async UniTask TutorialStepAsync(Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            case TutorialExplain.StoryQuestDailyJob:
                View.QuestUnits[(int)QuestContentType.DailyJob - 1].OnClickShowGroup();
                await TutorialManager.WaitUntilEnterUI(UIType.QuestInfoPopup);
                break;

            case TutorialExplain.StoryQuestReferenceCheck:
                View.QuestUnits[(int)QuestContentType.CharacterQuest - 1].OnClickShowGroup();
                await TutorialManager.WaitUntilEnterUI(UIType.ReferenceCheckView);
                break;
        }

        await base.TutorialStepAsync(type);
    }
    #endregion Coding rule : Function
}