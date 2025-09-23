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
using IronJade.Table.Data;          // 데이터 테이블
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using UnityEngine;
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class ApplicationController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.ApplicationPopup; } }
    private ApplicationPopup View { get { return base.BaseView as ApplicationPopup; } }
    protected ApplicationPopupModel Model { get; private set; }
    public ApplicationController() { Model = GetModel<ApplicationPopupModel>(); }
    public ApplicationController(BaseModel baseModel) : base(baseModel) { }
    //=========================================================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    private bool isBlockEnterApp = false;
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        isBlockEnterApp = false;
        Model.SetCustomizeMode(false);
        Model.SetHoldApp(ContentsType.None);

        Model.SetApplicationEvents(OnEventEnterApplication, OnEventStartHoldApp, OnEventStopHoldApp,
            OnEventChangeAppPosition, OnEventChangeAppMain, OnEventChangeAppBottom, OnEventSaveAppPosition);

        SetApplications();
    }

    public override void BackEnter()
    {
        isBlockEnterApp = false;

        ContentsOpenManager.Instance.ConfirmAllApp();
    }

    public override async UniTask PlayBackShowAsync()
    {
    }

    public override async UniTask PlayShowAsync()
    {
        SoundManager.PlayUIMenuSFX(3);
        await base.PlayShowAsync();
    }

    public override async UniTask PlayHideAsync()
    {
        await base.PlayHideAsync();
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        if (CheckTutorialBlockEvent())
            return false;

        ContentsOpenManager.Instance.ConfirmAllApp();
        ApplicationManager.Instance.SetLastOpenPageIndex(View.GetCurrentPageIndex());

        if (BackgroundSceneManager.Instance != null)
            BackgroundSceneManager.Instance.ShowTownObjects(true);

        await UIManager.Instance.Exit(this, async (state) => { await OnEventExtra(onEventExtra, state); });

        return true;
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override void Refresh()
    {
        ContentsOpenManager.Instance.ConfirmAllApp();

        isBlockEnterApp = false;

        View.ShowAsync().Forget();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Application/ApplicationPopup";
    }

    /// <summary> (최초 입장 or 저장시) 현재 어플 세팅 </summary>
    private void SetApplications()
    {
        Model.SetApplications(ApplicationManager.Instance.MainMenuApps, ApplicationManager.Instance.BottomMenuApps);
    }

    private void OnEventEnterApplication(ContentsType contentsType)
    {
        if (CheckTutorialBlockEvent())
            return;

        if (Model.IsCustomizeMode)
            return;

        if (isBlockEnterApp)
            return;

        isBlockEnterApp = true;
        ApplicationManager.Instance.SetLastOpenPageIndex(View.GetCurrentPageIndex());
        EnterApplication(contentsType).Forget();
    }

    private async UniTask EnterApplication(ContentsType contentsType)
    {
        if (!await ContentsOpenManager.Instance.OpenContents(contentsType))
            isBlockEnterApp = false;
    }

    private void OnEventSaveAppPosition()
    {
        ApplicationManager.Instance.MainMenuApps.Clear();
        ApplicationManager.Instance.BottomMenuApps.Clear();

        ApplicationPositionDto resultPosition = new ApplicationPositionDto
        {
            mainMenu = new ApplicationSlotDto[Model.currentMainApp.Count],
            bottomMenu = new ApplicationSlotDto[Model.currentBottomApp.Count]
        };

        for (int i = 0; i < Model.currentMainApp.Count; i++)
        {
            var appModel = Model.currentMainApp.ElementAt(i);
            ApplicationSlotDto dto = new ApplicationSlotDto(appModel.Id, appModel.ApplicationModel.AppDataId);
            resultPosition.mainMenu[i] = dto;
            ApplicationManager.Instance.MainMenuApps.Add(new ApplicationInfoModel(dto));
        }

        for (int i = 0; i < Model.currentBottomApp.Count; i++)
        {
            var appModel = Model.currentBottomApp.ElementAt(i);
            ApplicationSlotDto dto = new ApplicationSlotDto(appModel.Id, appModel.ApplicationModel.AppDataId);
            resultPosition.bottomMenu[i] = dto;
            ApplicationManager.Instance.BottomMenuApps.Add(new ApplicationInfoModel(dto));
        }

        ApplicationManager.Instance.SaveApplicationInfos(UtilModel.Json.ToJson(resultPosition));
        Model.SetCustomizeMode(false);

        // 현재 어플 선택
        Model.SetHoldApp(ContentsType.None);

        View.UpdateCustomizeMode();
        View.UpdateHoldApp();
    }

    #region APP EVENT
    /// <summary> 홀드로 어플 선택 </summary>
    private void OnEventStartHoldApp(ContentsType contentsType)
    {
        //FASTTAB 튜토리얼 도중이거나 클리어 전까지는 홀드 금지.
        if (!TutorialManager.Instance.CheckTutorialClear((int)TutorialDefine.TUTORIAL_CONTENTS_FASTTAB_00))
        {
            if (TutorialManager.Instance.CurrentTutorialDataId != (int)TutorialDefine.TUTORIAL_CONTENTS_FASTTAB_00)
                return;
        }

        if (CheckTutorialBlockEvent())
        {
            if (contentsType != ContentsType.Character)
                return;
        }

        if (Model.HoldApp != ContentsType.None)
            return;

        IronJade.Debug.Log($"[Application] OnEventStartHoldApp {contentsType}");

        // 커스터마이징 모드가 아니라면 커스터마이징 모드로 변환
        if (!Model.IsCustomizeMode)
            Model.SetCustomizeMode(true);

        // 현재 어플 선택
        Model.SetHoldApp(contentsType);

        View.UpdateCustomizeMode();
        View.StartHoldApp(contentsType);
        View.UpdateHoldApp();
    }

    /// <summary> 어플 홀드 해제 </summary>
    private void OnEventStopHoldApp()
    {
        if (!Model.IsCustomizeMode)
            return;

        if (Model.HoldApp == ContentsType.None)
            return;

        IronJade.Debug.Log($"[Application] OnEventStopHoldApp ");

        // 선택중인 어플 해제
        Model.SetHoldApp(ContentsType.None);

        View.StopHoldApp();
        View.UpdateHoldApp();
    }

    /// <summary> 어플 위치 변경 이벤트 </summary>
    /// <param name="contentsType">홀드중인 어플이 아닌 변경할 위치에 있는 어플입니다.</param>
    private void OnEventChangeAppPosition(ContentsType contentsType, bool isBefore)
    {
        if (!Model.IsCustomizeMode)
            return;

        if (Model.HoldApp == ContentsType.None)
            return;

        if (Model.HoldApp == contentsType)
            return;

        IronJade.Debug.Log($"[Application] OnEventChangeAppPosition {Model.HoldApp} to {contentsType}-{isBefore}");

        //홀드중인 앱, 옮길 위치 관련 정보 세팅
        ApplicationSlotUnitModel holdModel = null;
        bool isHoldMain = false;
        LinkedListNode<ApplicationSlotUnitModel> targetMainNode = null;
        LinkedListNode<ApplicationSlotUnitModel> targetBottomNode = null;

        for (int i = 0; i < Model.currentMainApp.Count; i++)
        {
            var targetModel = Model.currentMainApp.ElementAt(i);
            if (targetModel.ContentsType == Model.HoldApp)
            {
                holdModel = targetModel;
                isHoldMain = true;
            }
            else if (targetModel.ContentsType == contentsType)
            {
                targetMainNode = Model.currentMainApp.Find(targetModel);
            }
        }

        if (!isHoldMain || targetMainNode == null)
        {
            for (int i = 0; i < Model.currentBottomApp.Count; i++)
            {
                var targetModel = Model.currentBottomApp.ElementAt(i);
                if (targetModel.ContentsType == Model.HoldApp)
                {
                    holdModel = targetModel;
                    isHoldMain = false;
                }
                else if (targetModel.ContentsType == contentsType)
                {
                    targetBottomNode = Model.currentBottomApp.Find(targetModel);
                }
            }
        }

        // 변경 불가 부분 이동 시도시 취소
        if (targetBottomNode != null && isHoldMain && Model.currentBottomApp.Count == IntDefine.MAX_APPLICATION_BOTTOM_APP_COUNT)
            return;

        //어플이 1개인 경우 못 내림
        if (isHoldMain && targetBottomNode != null && Model.currentMainApp.Count == 1)
            return;


        // 홀드중인 앱 기존 위치에서 제거 
        if (isHoldMain)
            Model.currentMainApp.Remove(holdModel);
        else
            Model.currentBottomApp.Remove(holdModel);

        // 홀드중인 앱 신규 위치로 이동
        if (targetMainNode != null)
        {
            if (isBefore)
                Model.currentMainApp.AddBefore(targetMainNode, holdModel);
            else
                Model.currentMainApp.AddAfter(targetMainNode, holdModel);
        }
        else if (targetBottomNode != null)
        {
            if (isBefore)
                Model.currentBottomApp.AddBefore(targetBottomNode, holdModel);
            else
                Model.currentBottomApp.AddAfter(targetBottomNode, holdModel);
        }

        //하단 어플을 상단으로 올리는 경우, 페이지가 증가하는 경우 페이지 추가
        if (!isHoldMain && targetMainNode != null)
        {
            if (Model.currentMainApp.Count % IntDefine.MAX_APPLICATION_PAGE_APP_COUNT == 1)
                Model.ApplicationPages.Add(new ApplicationPageUnitModel());
        }
        //상단 어플을 하단으로 내리는 경우, 페이지가 감소하는 경우 처리
        else if (isHoldMain && targetBottomNode != null)
        {
            if (Model.currentMainApp.Count % IntDefine.MAX_APPLICATION_PAGE_APP_COUNT == 0)
                Model.ApplicationPages.RemoveAt(Model.ApplicationPages.Count - 1);
        }

        //ApplicationPage 업데이트
        Model.UpdateApplicationByCurrentApp();
        View.ChangeAppPosition().Forget();
    }

    /// <summary> 하단 어플을 상단으로 이동 </summary>
    private void OnEventChangeAppMain()
    {
        if (!Model.IsCustomizeMode)
            return;

        if (Model.HoldApp == ContentsType.None)
            return;

        IronJade.Debug.Log($"[Application] OnEventChangeAppMain");

        ApplicationSlotUnitModel holdModel = null;
        for (int i = 0; i < Model.currentBottomApp.Count; i++)
        {
            var targetModel = Model.currentBottomApp.ElementAt(i);
            if (targetModel.ContentsType == Model.HoldApp)
            {
                holdModel = targetModel;
                break;
            }
        }

        if (holdModel == null)
            return;

        Model.currentBottomApp.Remove(holdModel);
        Model.currentMainApp.AddLast(holdModel);

        //메인 어플이 이미 9개가 차있다면 페이지 하나 추가
        if (Model.ApplicationPages[Model.ApplicationPages.Count - 1].Applications.Count == IntDefine.MAX_APPLICATION_PAGE_APP_COUNT)
            Model.ApplicationPages.Add(new ApplicationPageUnitModel());

        Model.UpdateApplicationByCurrentApp();
        View.ChangeAppPosition().Forget();
    }

    /// <summary> 상단 어플을 하단으로 이동 </summary>
    private void OnEventChangeAppBottom()
    {
        if (!Model.IsCustomizeMode)
            return;

        if (Model.HoldApp == ContentsType.None)
            return;

        if (Model.currentBottomApp.Count == IntDefine.MAX_APPLICATION_BOTTOM_APP_COUNT)
            return;

        if (Model.currentMainApp.Count == 1)
            return;

        IronJade.Debug.Log($"[Application] OnEventChangeAppBottom");

        ApplicationSlotUnitModel holdModel = null;
        for (int i = 0; i < Model.currentMainApp.Count; i++)
        {
            var targetModel = Model.currentMainApp.ElementAt(i);
            if (targetModel.ContentsType == Model.HoldApp)
            {
                holdModel = targetModel;
                break;
            }
        }

        if (holdModel == null)
            return;

        Model.currentMainApp.Remove(holdModel);
        Model.currentBottomApp.AddLast(holdModel);

        //페이지 개수가 줄어들었다면 마지막 페이지 제거
        if (Model.ApplicationPages[Model.ApplicationPages.Count - 1].Applications.Count == 1)
            Model.ApplicationPages.RemoveAt(Model.ApplicationPages.Count - 1);

        Model.UpdateApplicationByCurrentApp();
        View.ChangeAppPosition().Forget();
    }
    #endregion APP EVENT

    #region TUTORIAL
    private async UniTask<(int, int)> FindAndSnapApp(ContentsType contentsType)
    {
        int page = -1;
        int slot = -1;
        for (int i = 0; i < Model.ApplicationPages.Count; i++)
        {
            ApplicationPageUnitModel pageModel = Model.ApplicationPages[i];
            if (pageModel.Applications.Any(x => x.ContentsType == contentsType))
            {
                page = i;

                for (int j = 0; j < pageModel.Applications.Count; j++)
                {
                    if (pageModel.Applications.ElementAt(j).ContentsType == contentsType)
                    {
                        slot = j;
                        break;
                    }
                }

                //다른 페이지에 어플이 있으면 페이지 전환
                if (page != 0)
                    await View.SnapPageByIndex(i);

                break;
            }
        }

        return (page, slot);
    }

    public override async UniTask TutorialStepAsync(System.Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            case TutorialExplain.ApplicationBack:
                {
                    await UIManager.Instance.Exit(UIType.ApplicationPopup);
                    break;
                }

            #region Enter Application
            case TutorialExplain.ApplicationMission:
                {
                    await ContentsOpenManager.Instance.OpenContents(ContentsType.Mission);
                    await TutorialManager.WaitUntilEnterUI(UIType.MissionView);
                    break;
                }

            case TutorialExplain.ApplicationChainlink:
                {
                    await ContentsOpenManager.Instance.OpenContents(ContentsType.GachaShop);
                    await TutorialManager.WaitUntilEnterUI(UIType.ChainLinkView);
                    break;
                }

            case TutorialExplain.ApplicationConnect:
                {
                    await ContentsOpenManager.Instance.OpenContents(ContentsType.Character);
                    await TutorialManager.WaitUntilEnterUI(UIType.CharacterManagerView);
                    break;
                }

            case TutorialExplain.ApplicationLevelSync:
                {
                    await ContentsOpenManager.Instance.OpenContents(ContentsType.LevelSync);
                    await TutorialManager.WaitUntilEnterUI(UIType.LevelSyncView);
                    break;
                }

            case TutorialExplain.ApplicationMembership:
                {
                    await ContentsOpenManager.Instance.OpenContents(ContentsType.Membership);
                    await TutorialManager.WaitUntilEnterUI(UIType.MembershipView);
                    break;
                }

            case TutorialExplain.ApplicationStoryQuest:
                {
                    await ContentsOpenManager.Instance.OpenContents(ContentsType.StoryQuest);
                    await TutorialManager.WaitUntilEnterUI(UIType.QuestView);
                    break;
                }

            case TutorialExplain.ApplicationNaviiChat:
                {
                    await ContentsOpenManager.Instance.OpenContents(ContentsType.SocialContents);
                    await TutorialManager.WaitUntilEnterUI(UIType.NaviiChatView);
                    break;
                }

            case TutorialExplain.ApplicationInfinityCircuit:
                {
                    await ContentsOpenManager.Instance.OpenContents(ContentsType.InfinityCircuit);
                    await TutorialManager.WaitUntilEnterUI(UIType.InfinityDungeonLobbyView);
                    break;
                }

            case TutorialExplain.ApplicationCode:
                {
                    await ContentsOpenManager.Instance.OpenContents(ContentsType.Code);
                    await TutorialManager.WaitUntilEnterUI(UIType.CodeHubView);
                    break;
                }


            case TutorialExplain.ApplicationInventory:
                {
                    await ContentsOpenManager.Instance.OpenContents(ContentsType.Inventory);
                    await TutorialManager.WaitUntilEnterUI(UIType.InventoryView);
                    break;
                }


            case TutorialExplain.ApplicationSetting:
                {
                    await ContentsOpenManager.Instance.OpenContents(ContentsType.Setting);
                    await TutorialManager.WaitUntilEnterUI(UIType.SettingWindow);
                    break;
                }


            case TutorialExplain.ApplicationCharacterCollection:
                {
                    await ContentsOpenManager.Instance.OpenContents(ContentsType.CharacterCollection);
                    await TutorialManager.WaitUntilEnterUI(UIType.CharacterCollectionView);
                    break;
                }


            case TutorialExplain.ApplicationDeck:
                {
                    await ContentsOpenManager.Instance.OpenContents(ContentsType.Deck);
                    await TutorialManager.WaitUntilEnterUI(UIType.TeamUpdateView);
                    break;
                }


            case TutorialExplain.ApplicationMail:
                {
                    await ContentsOpenManager.Instance.OpenContents(ContentsType.Mail);
                    await TutorialManager.WaitUntilEnterUI(UIType.MailView);
                    break;
                }


            case TutorialExplain.ApplicationRanking:
                {
                    await ContentsOpenManager.Instance.OpenContents(ContentsType.Ranking);
                    await TutorialManager.WaitUntilEnterUI(UIType.RankingView);
                    break;
                }


            case TutorialExplain.ApplicationCashShop:
                {
                    await ContentsOpenManager.Instance.OpenContents(ContentsType.CashShop);
                    await TutorialManager.WaitUntilEnterUI(UIType.CashShopView);
                    break;
                }
            #endregion Enter Application

            case TutorialExplain.ApplicationHold:
                {
                    OnEventStopHoldApp();
                    TutorialManager.Instance.OnEventTutorialTouchActive(false);
                    break;
                }

            case TutorialExplain.ApplicationBottomMenu:
                {
                    OnEventStopHoldApp();
                    TutorialManager.Instance.OnEventTutorialTouchActive(false);
                    View.TutorialActiveScroll(true);
                    break;
                }

            case TutorialExplain.ApplicationCustomizeComplete:
                {
                    OnEventSaveAppPosition();
                    break;
                }
        }
    }

    public override async UniTask<GameObject> GetTutorialFocusObject(string stepKey)
    {
        await UniTask.WaitUntil(() => { return !View.IsPlayingAnimation; });
        await UniTask.NextFrame();
        await UniTask.NextFrame();

        TutorialExplain stepType = (TutorialExplain)Enum.Parse(typeof(TutorialExplain), stepKey);

        ContentsType contentsType = ContentsType.None;
        switch (stepType)
        {
            case TutorialExplain.ApplicationCharacterCollection:
                {
                    contentsType = ContentsType.CharacterCollection;
                    break;
                }
            case TutorialExplain.ApplicationInventory:
                {
                    contentsType = ContentsType.Inventory;
                    break;
                }
            case TutorialExplain.ApplicationSetting:
                {
                    contentsType = ContentsType.Setting;
                    break;
                }
            case TutorialExplain.ApplicationMission:
                {
                    contentsType = ContentsType.Mission;
                    break;
                }

            case TutorialExplain.ApplicationChainlink:
                {
                    contentsType = ContentsType.GachaShop;
                    break;
                }

            case TutorialExplain.ApplicationConnect:
            case TutorialExplain.ApplicationHold:
            case TutorialExplain.ApplicationBottomMenu:
                {
                    View.TutorialActiveScroll(false);
                    contentsType = ContentsType.Character;
                    break;
                }

            case TutorialExplain.ApplicationLevelSync:
                {
                    contentsType = ContentsType.LevelSync;
                    break;
                }

            case TutorialExplain.ApplicationMembership:
                {
                    contentsType = ContentsType.Membership;
                    break;
                }

            case TutorialExplain.ApplicationStoryQuest:
                {
                    contentsType = ContentsType.StoryQuest;
                    break;
                }

            case TutorialExplain.ApplicationNaviiChat:
                {
                    contentsType = ContentsType.SocialContents;
                    break;
                }

            case TutorialExplain.ApplicationInfinityCircuit:
                {
                    contentsType = ContentsType.InfinityCircuit;
                    break;
                }

            case TutorialExplain.ApplicationCode:
                {
                    contentsType = ContentsType.Code;
                    break;
                }

            case TutorialExplain.ApplicationMail:
                {
                    contentsType = ContentsType.Mail;
                    break;
                }

            case TutorialExplain.ApplicationDeck:
                {
                    contentsType = ContentsType.Deck;
                    break;
                }

            case TutorialExplain.ApplicationCashShop:
                {
                    contentsType = ContentsType.CashShop;
                    break;
                }

            case TutorialExplain.ApplicationRanking:
                {
                    contentsType = ContentsType.Ranking;
                    break;
                }

            default:
                return await base.GetTutorialFocusObject(stepKey);
        }

        (int, int) appPositionInfo = await FindAndSnapApp(contentsType);
        if (appPositionInfo.Item1 != -1)
        {
            return View.GetApplicationObject(appPositionInfo.Item1, appPositionInfo.Item2);
        }
        else
        {
            var applications = Model.ApplicationBottom.Applications;
            if (applications.Any(x => x.ContentsType == contentsType))
            {
                int slot = -1;
                for (int j = 0; j < applications.Count; j++)
                {
                    if (applications.ElementAt(j).ContentsType == contentsType)
                    {
                        slot = j;
                        break;
                    }
                }
                return View.GetApplicationBottomObject(slot);
            }
            return null;
        }
    }

    public override bool GetTutorialCondition(Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            //어플 홀드 : 커스터마이징 모드 돌입시 조건 충족
            case TutorialExplain.ApplicationHold:
                return Model.IsCustomizeMode;

            //어플 하단 추가 : 하단 메뉴 추가된 경우 조건 충족
            case TutorialExplain.ApplicationBottomMenu:
                {
                    if (Model.ApplicationBottom == null)
                        return false;

                    return Model.ApplicationBottom.Applications.Any(x => x.ContentsType == ContentsType.Character);
                }

            default:
                return false;
        }
    }

    private bool CheckTutorialBlockEvent()
    {
        //바로가기 튜토리얼중에는 지정된 어플 제외 미동작
        if (TutorialManager.Instance.CurrentTutorialDataId == (int)TutorialDefine.TUTORIAL_CONTENTS_FASTTAB_00)
            return true;

        return false;
    }
    #endregion TUTORIAL

    #endregion Coding rule : Function
}