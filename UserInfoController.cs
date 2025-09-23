//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.LowLevel.Server.Web;
using IronJade.Server.Web.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using UnityEngine;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class UserInfoController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.UserInfoPopup; } }
    private UserInfoPopup View { get { return base.BaseView as UserInfoPopup; } }
    protected UserInfoPopupModel Model { get; private set; }
    public UserInfoController() { Model = GetModel<UserInfoPopupModel>(); }
    public UserInfoController(BaseModel baseModel) : base(baseModel) { }
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
        Model.SetCategoryType(UserInfoPopupModel.CategoryType.Info);
        Model.SetTeam();
        Model.SetLeaderCharacter();

        Model.SetEventCharacterHold(null);
        Model.SetEventCharacterSelect(null);
        Model.SetEventOpenProfileEdit(OnEventOpenProfileEdit);
        Model.SetEventInputNickName(OnEventInputNickName);
        Model.SetEventCopyUserId(OnEventCopyUserId);
        Model.SetEventCopyUserServer(OnEventCopyServer);
        Model.SetEventCopyUserGuild(OnEventCopyGuild);
        Model.SetEventInputComment(OnEventInputComment);
        Model.SetEventOpenTeamUpdate(OnEventOpenTeamUpdate);
        Model.SetEventCategory(OnEventCategory);
        Model.SetSimpleProfileModel();

        Model.SetTeamThumbnailCharacter();

        Model.SetStoryQuestProgress(OnEventUpdateStoryQuest());
        Model.SetStageDungeonProgress(OnEventUpdateStageDungeon());
        Model.SetTowerProgress(OnEventUpdateTower());
        Model.SetCharacterListbyLicenseType();
        Model.SetExp();

        Model.SetEventCharacterSelect(OnEventCharacterSelect);
        Model.SetSortingModels(OnEventSorting, OnEventSortingOrder);
        Model.SetEventFilter(OnEventFilter);
        Model.SetFilterModel(CharacterFilterType.CharacterManager);
        Model.SetLeaderCharacter(Model.User.CharacterModel.GetLeaderCharacter());

        Model.SetThumbnailCharacterUnitModel();

        Model.SortCharacterThumbnail();
        Model.SetSelectThumbnailCharacter(Model.GetThumbnailCharacterUnitModelByLeaderCharacter());

        Model.SetEventLikeAbilityFrameSelect(OnEvnetLikeAbilityFrameSelect);

        Model.SetEventConfirm(OnEventConfirm);

    }

    public override async UniTask LoadingProcess()
    {
        Model.SetThumbnailLikeAbilityFrameUnitModel();
        Model.SetSelectLikeAbilityFrameModel(Model.GetLikeAbilityFrameModel());
    }

    public override async UniTask Process()
    {
        View.SetActiveInfo(false);
        await RequestGuildGet();

        await View.ShowAsync();

        View.SetActiveInfo(true);
    }

    public override async UniTask BackProcess()
    {
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);
        Model.SetTeam();

        if (Model.SelectedCharacter == null)
            Model.SetSimpleProfileModel();

        View.ShowAsync().Forget();
    }

    public override void Refresh()
    {
        Model.SetSimpleProfileModel();

        View.ShowAsync().Forget();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Common/UserInfoPopup";
    }

    public override void InitializeTestModel(params object[] parameters)
    {
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);
    }

    private void OnEventOpenProfileEdit()
    {
        if (!Model.IsMy)
            return;

        BaseController profileEditController = UIManager.Instance.GetController(UIType.UserProfileEditPopup);
        UserProfileEditPopupModel model = profileEditController.GetModel<UserProfileEditPopupModel>();
        model.SetUserInfo(Model.SimpleProfileUnitModel.User);

        UIManager.Instance.EnterAsync(profileEditController).Forget();
    }

    private void OnEventInputNickName()
    {
        if (!Model.IsMy)
            return;

        BaseController changeNickNameController = UIManager.Instance.GetController(UIType.ChangeNickNamePopup);

        UIManager.Instance.EnterAsync(changeNickNameController).Forget();
    }

    private void OnEventCopyUserId()
    {
        UtilModel.String.Copy(Model.User.UserId.ToString());
        MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_USERINFOPOPUP_COPY_CLIPBOARD);
    }

    private void OnEventCopyServer()
    {
        UtilModel.String.Copy(Config.ServerType.LocalizationText());
        MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_USERINFOPOPUP_COPY_CLIPBOARD);
    }
    private void OnEventCopyGuild()
    {
        UtilModel.String.Copy(Model.User.GuildModel.MyGuild.Name);
        MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_USERINFOPOPUP_COPY_CLIPBOARD);
    }

    private void OnEventInputComment()
    {
        if (!Model.IsMy)
            return;

        LocalizationDefine mainDescription = LocalizationDefine.LOCALIZATION_UI_LABEL_USER_INFORMATION_COMMENT_ENTER_USE;
        LocalizationDefine subDescription = LocalizationDefine.LOCALIZATION_UI_LABEL_USER_INFORMATION_COMMENT_LENGTH;
        LocalizationDefine inputPlaceholder = LocalizationDefine.LOCALIZATION_COMMENTPOPUP_DEFAULT_INPUT;

        MessageBoxManager.ShowInputMessageBox(mainDescription, subDescription, inputPlaceholder: inputPlaceholder, onEventConfirm: (input) =>
        {
            RequestCommentUpdate(input).Forget();
        });
    }

    private void OnEventOpenTeamUpdate()
    {
        if (!Model.IsMy)
            return;

        BaseController teamUpdateController = UIManager.Instance.GetController(UIType.TeamUpdateView);
        TeamUpdateViewModel viewModel = teamUpdateController.GetModel<TeamUpdateViewModel>();
        viewModel.SetUser(Model.User);
        viewModel.SetCurrentDeckType(DeckType.Story);
        viewModel.SetOpenPresetNumber(0);
        UIManager.Instance.EnterAsync(teamUpdateController, onEventExtra: async (state) =>
        {
        }).Forget();
    }

    private string OnEventUpdateStoryQuest()
    {
        var storyQuest = MissionManager.Instance.GetTrackingMission(MissionContentType.StoryQuest);
        if (storyQuest == null)
            return "-";

        StoryQuestTable storyQuestTable = TableManager.Instance.GetTable<StoryQuestTable>();
        StoryQuestTableData storyQuestTableData = storyQuestTable.GetDataByID(storyQuest.DataId);
        EpisodeGroupTable episodeGroupTable = TableManager.Instance.GetTable<EpisodeGroupTable>();
        EpisodeGroupTableData episodeGroupTableData = episodeGroupTable.GetDataByID(storyQuestTableData.GetEPISODE());

        return string.Format(@"{0}-{1}-{2}",
            episodeGroupTableData.GetSEASON_TYPE(),
            episodeGroupTableData.GetEPISODE_NUMBER(),
            episodeGroupTableData.GetNAME_EPISODE_GROUP_NUMBER());
    }

    private string OnEventUpdateStageDungeon()
    {
        StageDungeonGroupModel model = DungeonManager.Instance.GetDungeonGroupModel<StageDungeonGroupModel>();
        if (model == null || model.GetClearDataStageDungeonId(false) == 0)
            return "-";

        StageDungeonTable stageDungeonTable = TableManager.Instance.GetTable<StageDungeonTable>();
        StageDungeonTableData clearStageDungeonData = stageDungeonTable.GetDataByID(model.GetClearDataStageDungeonId(false));
        return string.Format(@"{0}-{1}", clearStageDungeonData.GetSTAGE_DUNGEON_CHAPTER(), clearStageDungeonData.GetSTAGE_DUNGEON_STAGE());
    }
    private string OnEventUpdateTower()
    {
        int myStage = 0;

        InfinityDungeonGroupModel model = DungeonManager.Instance.GetDungeonGroupModel<InfinityDungeonGroupModel>();

        if (model != null)
        {
            LimitedOpenDungeonGroupModel dungeonGroupModel = model.GetDungeonGroupModelByDataId((int)InfinityCircuitDefine.INFINITY_CIRCUIT_GENERAL);

            if (dungeonGroupModel != null)
                myStage = dungeonGroupModel.GetOpenedHighestDungeonIndex();
        }

        if (myStage == 0)
            return "-";

        return string.Format("{0}F", myStage);
    }
    private void OnEventCategory(int index)
    {
        UserInfoPopupModel.CategoryType changeCategory = (UserInfoPopupModel.CategoryType)index;

        if (Model.Category == changeCategory)
            return;

        Model.SetCategoryType(changeCategory);

        switch (changeCategory)
        {
            case UserInfoPopupModel.CategoryType.Info:
                break;
            case UserInfoPopupModel.CategoryType.Growth:
                if (Model.User.IsMy)
                {
                    Model.SetGrowth(Model.User.GetMigrationLevel(),
                                    Model.User.MigrationModel.RegisteredCharacterCount,
                                    Model.User.MembershipModel.CurrentGrade,
                                    0,
                                    Model.User.CharacterModel.Count);
                }
                else
                {
                    RequestOtherUserGrowthGet().Forget();
                }
                break;
            case UserInfoPopupModel.CategoryType.Character:
                break;
        }

        View.ShowCategory().Forget();
    }


    protected virtual ThumbnailSelectType OnEventCharacterSelect(BaseThumbnailUnitModel model)
    {
        Model.SetSelectThumbnailCharacter(model);
        Model.SetSelectLikeAbilityFrameModel(Model.GetLikeAbilityFrameModel());


        View.UpdateThumbnail().Forget();
        View.ShowCharacterScroll(isReset: false);

        return ThumbnailSelectType.None;
    }


    protected void OnEventSorting(System.Enum sortingType)
    {
        Model.SortingModel.SetGoodsSortingValueTypes(GoodsType.Character, sortingType);
        Model.SortCharacterThumbnail();

        View.ShowCharacterScroll(isReset: true);
    }

    protected void OnEventSortingOrder(SortingOrderType orderType)
    {
        Model.SortingModel.SetOrderType(orderType);
        Model.SortCharacterThumbnail();

        View.ShowCharacterScroll(isReset: true);
    }

    protected void OnEventFilter()
    {
        BaseController filterController = UIManager.Instance.GetController(UIType.FilterPopup);
        FilterPopupModel model = filterController.GetModel<FilterPopupModel>();

        model.SetEventConfirm(OnEventChangeFilter);
        model.SetFilterModel(Model.FilterModel);

        UIManager.Instance.EnterAsync(filterController).Forget();
    }

    protected virtual void OnEventChangeFilter()
    {
        if (!Model.FilterModel.CheckChangeFilter())
            return;

        if (Model.CheckEmptyCharacterList())
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_USERINFOPOPUP_FILTER_LIST_EMPTY);
            return;
        }

        Model.FilterModel.SaveFilter();
        Model.SetEventCharacterSelect(OnEventCharacterSelect);
        Model.SetThumbnailCharacterUnitModel();
        Model.SetSelectThumbnailCharacter(Model.GetThumbnailCharacterUnitModelByLeaderCharacter());

        View.ShowAsync().Forget();
    }

    private void OnEvnetLikeAbilityFrameSelect(ThumbnailLikeAbilityFrameUnitModel model)
    {
        Model.SetSelectLikeAbilityFrameModel(model);

        View.UpdateLikeAbilityFrame();
        View.ShowFrameScroll(false);
    }

    private void OnEventConfirm()
    {
        switch (Model.Category)
        {
            case UserInfoPopupModel.CategoryType.Character:
                {
                    if (Model.SelectThumbnailCharacterUnitModel.Id == Model.User.CharacterModel.LeaderCharacterId)
                    {
                        if (Model.User.CharacterModel.MissionDataCharacterId != 0)
                        {
                            ResetMissionCharacter(LocalizationDefine.LOCALIZATION_USERINFOPOPUP_CHANGE_LEADER).Forget();
                        }
                        else
                        {
                            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_USERINFOPOPUP_ALREADY_REGISTERED_LEADER);
                        }

                        return;
                    }

                    RequestUserLeaderCharacterUpdate(LocalizationDefine.LOCALIZATION_USERINFOPOPUP_CHANGE_LEADER).Forget();
                    break;
                }
        }
    }

    private async UniTask RequestCommentUpdate(string input)
    {
        UserCommentUpdateProcess userNickNameCreateProcess = NetworkManager.Web.GetProcess<UserCommentUpdateProcess>();

        userNickNameCreateProcess.Request.SetUserUpdateCommentDto(new UserUpdateCommentDto(input));

        if (await userNickNameCreateProcess.OnNetworkAsyncRequest())
        {
            userNickNameCreateProcess.OnNetworkResponse();
        }
    }

    private async UniTask RequestGuildGet()
    {
        BaseProcess guildGetProcess = NetworkManager.Web.GetProcess(WebProcess.GuildGet);

        if (await guildGetProcess.OnNetworkAsyncRequest())
            guildGetProcess.OnNetworkResponse();
    }

    private async UniTask RequestOtherUserGrowthGet()
    {
        BaseProcess otherUserGrowthGetProcess = NetworkManager.Web.GetProcess(WebProcess.OtherUserGrowthGet);
        otherUserGrowthGetProcess.SetPacket(new GetOtherUserGrowthInDto(Model.User.UserId));

        if (await otherUserGrowthGetProcess.OnNetworkAsyncRequest())
        {
            otherUserGrowthGetProcess.OnNetworkResponse(Model);

            View.ShowCategory().Forget();
        }
    }

    private async UniTask RequestUserLeaderCharacterUpdate(LocalizationDefine localizationDefine)
    {
        await PlayerManager.Instance.RequestChangeLeader(Model.SelectThumbnailCharacterUnitModel.Id, 0, async () =>
        {
            if (PlayerManager.Instance.MyPlayer.IsExistTownPlayer)
                await PlayerManager.Instance.MyPlayer.TownPlayer.VisibleTownObject(true);
        });

        MessageBoxManager.ShowToastMessage(localizationDefine);
    }

    private async UniTask ResetMissionCharacter(LocalizationDefine localizationDefine)
    {
        // 미션 캐릭터 리셋
        MissionBehaviorSaveUserSetting setting = PlayerManager.Instance.UserSetting.GetUserSettingData<MissionBehaviorSaveUserSetting>();

        setting.character = 0;

        await PlayerManager.Instance.UserSetting.SetUserSettingData(UserSettingModel.Save.Server, setting);

        Model.User.CharacterModel.SetMissionCharacterDataId(0);

        // 캐릭터 로드
        PlayerManager.Instance.ResetTempLeaderCharacter();
        await PlayerManager.Instance.LoadMyPlayerCharacterObject();

        if (PlayerManager.Instance.MyPlayer.IsExistTownPlayer)
            await PlayerManager.Instance.MyPlayer.TownPlayer.VisibleTownObject(true);

        MessageBoxManager.ShowToastMessage(localizationDefine);
    }


    public override async UniTask TutorialStepAsync(System.Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            case TutorialExplain.UserInfoCharacterTab:
                {
                    OnEventCategory((int)UserInfoPopupModel.CategoryType.Character);
                    await UniTask.WaitUntil(() => !View.CheckPlayingTween());
                    break;
                }

            case TutorialExplain.UserInfoCharacter:
                {
                    BaseThumbnailUnitModel model = Model.FindCharacterByDataId((int)CharacterDefine.CHARACTER_NOAH);
                    OnEventCharacterSelect(model);
                    break;
                }

            case TutorialExplain.UserInfoConfirm:
                {
                    OnEventConfirm();
                    break;
                }

            case TutorialExplain.UserInfoBack:
                {
                    Exit().Forget();
                    break;
                }
        }
    }

    public override async UniTask<GameObject> GetTutorialFocusObject(string stepKey)
    {
        await UniTask.WaitUntil(() => { return !View.IsPlayingAnimation; });

        TutorialExplain stepType = (TutorialExplain)Enum.Parse(typeof(TutorialExplain), stepKey);

        switch (stepType)
        {
            case TutorialExplain.UserInfoCharacter:
                {
                    int index = Model.FindCharacterIndexByDataId((int)CharacterDefine.CHARACTER_NOAH);
                    return View.GetCharacterrThumbnailObject(index);
                }

            default:
                return await base.GetTutorialFocusObject(stepKey);
        }
    }
    #endregion Coding rule : Function
}