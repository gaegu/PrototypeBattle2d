//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using System.Threading;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.LowLevel.Server.Web;
using IronJade.Observer.Core;
using IronJade.Server.Web.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using UnityEngine;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class NetMiningController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.NetMiningView; } }
    private NetMiningView View { get { return base.BaseView as NetMiningView; } }
    protected NetMiningViewModel Model { get; private set; }
    public NetMiningController() { Model = GetModel<NetMiningViewModel>(); }
    public NetMiningController(BaseModel baseModel) : base(baseModel) { }
    //=========================================================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================


    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    private DateTime lastRequestRewardGetTime;

    private bool isPlayingChargeAnimation = false;
    private int forcedChargeCount = 0;
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);

        Model.SetEventShowRewardList(OnEventShowRewardList);
        Model.SetEventFastReward(OnEventFastReward);
        Model.SetEventReceiveReward(OnEventReceiveReward);

        Model.SetShowRewardList(false);

        lastRequestRewardGetTime = DateTime.MinValue;

        if (TutorialManager.Instance.CheckTutorialPlaying())
            SetTutorialInfo();
        else
            SetInfo();
    }

    public override void BackEnter()
    {
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        TokenPool.Cancel(GetHashCode());

        onEventExtra = async (state) =>
        {
            //꺼줬던 타운카메라 다시 켜줌
            if (state == UISubState.AfterLoading)
                CameraManager.Instance.SetActiveCamera(GameCameraType.TownCharacter, true);
        };

        return await base.Exit(onEventExtra);
    }

    public override async UniTask PlayHideAsync()
    {
        base.PlayHideAsync().Forget();
    }

    public override async UniTask LoadingProcess()
    {
        //넷마이닝 ProgressUnit에 달린 카메라가 Base가 되어야 하기 때문에 타운 카메라를 꺼줌
        CameraManager.Instance.SetActiveCamera(GameCameraType.TownCharacter, false);
        //넷마이닝 UI가 접근 루트가 많아서 어플리케이션 3D 닫기 애니메이션 재생(어플 넷마이닝, G-Task 넷마이닝 바로가기, 로비 넷마이닝버튼)
        await AdditivePrefabManager.Instance.UnLoadApplicationUnit();

        if (!TutorialManager.Instance.CheckTutorialPlaying())
        {
            //보상창 누르지 않고 수령 버튼 누르는 경우가 존재해서 UI 입장시 보상 1회 조회
            BaseProcess autoBattleRewardProcess = NetworkManager.Web.GetProcess(WebProcess.AutoBattleRewardGet);
            if (await autoBattleRewardProcess.OnNetworkAsyncRequest())
                autoBattleRewardProcess.OnNetworkResponse();
        }
        else
        {
            //튜토리얼중에는 임의로 세팅된 진행도를 데이터를 사용한다.
            NetMiningManager.Instance.SetStartAt(DateTime.UtcNow);
        }

        NetMiningManager manager = NetMiningManager.Instance;

        await AdditivePrefabManager.Instance.LoadAsync(AdditiveType.NetMiningProgress);
        var netMiningUnit = AdditivePrefabManager.Instance.NetMiningUnit;
        netMiningUnit.Model.SetTotalSeconds(manager.GetNormalChargeElapsedTimeTotalSeconds());
        netMiningUnit.Model.SetProgressPercent(manager.GetNormalChargePercentage());
        await netMiningUnit.ShowAsync();
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();

        View.ShowForcedChargeButton(TutorialManager.Instance.CheckTutorialPlaying());

        TaskUpdateElapsedSeconds().Forget();
    }

    public override async UniTask PlayShowAsync()
    {
        if (CheckPlayAnimationToday())
        {
            View.SkipPlayShowAsync(2.5).Forget();
            var netMiningUnit = AdditivePrefabManager.Instance.NetMiningUnit;
            netMiningUnit.SkipAnimation();
        }
        else
        {
            PlayerPrefsWrapper.SetString(StringDefine.KEY_PLAYER_PREFS_PLAY_NETMINING_ANIMATION_DATE, NetworkManager.Web.ServerTimeUTC.ToString());
            base.PlayShowAsync().Forget();
        }
    }

    public override void Refresh()
    {
        lastRequestRewardGetTime = DateTime.MinValue;
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "NetMining/NetMiningView";
    }

    private void OnEventReceiveReward()
    {
        RequestReceiveReward().Forget();
    }

    private void OnEventForcedCharge()
    {
        TaskForcedChargeAnimation(forcedChargeCount).Forget();
    }

    private void OnEventShowRewardList(bool isShow)
    {
        if (Model.IsShowRewardList == isShow)
            return;

        Model.SetShowRewardList(isShow);
        if (isShow)
        {
            RequestGetRewardList().Forget();
        }
        else
        {
            View.ShowAsync().Forget();
        }
    }

    private void OnEventFastReward()
    {
        GetFastRewardPopup().Forget();
    }

    /// <summary> 보상 탭 오픈시 메인 보상(재화) 및 확률 보상 세팅 </summary>
    private async UniTask RequestGetRewardList()
    {
        //진행시간이 쿨타임 미만인 경우 보상 갱신 안함(빈 칸으로 표기)
        if (NetMiningManager.Instance.ElapsedSeconds < IntDefine.TIME_NETMINING_RECEIVE_COOLTIME)
        {
            Model.ClearStackedRewardGoods();
            View.ShowRewards().Forget();
            return;
        }

        //연속 클릭 방지 API 호출 쿨타임 적용
        TimeSpan elapsedLastRequestTime = NetworkManager.Web.ServerTimeUTC - lastRequestRewardGetTime;
        if (elapsedLastRequestTime.TotalSeconds >= IntDefine.TIME_NETMINING_REWARD_GET_COOLTIME)
        {
            BaseProcess autoBattleRewardProcess = NetworkManager.Web.GetProcess(WebProcess.AutoBattleRewardGet);
            if (await autoBattleRewardProcess.OnNetworkAsyncRequest())
            {
                autoBattleRewardProcess.OnNetworkResponse();

                AutoBattleRewardGetResponse response = autoBattleRewardProcess.GetResponse<AutoBattleRewardGetResponse>();
                Model.SetStackedRewardGoods(response.data);
                lastRequestRewardGetTime = NetworkManager.Web.ServerTimeUTC;
            }
        }

        View.ShowRewards().Forget();
    }

    private async UniTask ShowTutorialRewardList()
    {
        Model.SetShowRewardList(true);
        Model.ClearStackedRewardGoods();
        await View.ShowRewards();
    }

    /// <summary> 보상 수령 </summary>
    private async UniTask RequestReceiveReward()
    {
        //연속 클릭 방지 API 호출 쿨타임 적용
        if (NetMiningManager.Instance.ElapsedSeconds >= IntDefine.TIME_NETMINING_RECEIVE_COOLTIME)
        {
            //보상창이 빈 상태에서 열어놓은 채로 20초가 지난 경우 막기.
            if (Model.IsShowRewardList && Model.StackedRewardList.Count == 0)
                return;

            //닫혀있는 상태라면 reward/get 먼저 호출(가장 최신 보상으로 수령)
            //열려있는 상태면 현재 표기중인 보상만 보여줘야 해서 호출하지 않는다.
            if (!Model.IsShowRewardList)
            {
                BaseProcess autoBattleRewardGetProcess = NetworkManager.Web.GetProcess(WebProcess.AutoBattleRewardGet);
                if (await autoBattleRewardGetProcess.OnNetworkAsyncRequest())
                    autoBattleRewardGetProcess.OnNetworkResponse();
            }

            BaseProcess autoBattleRewardProcess = NetworkManager.Web.GetProcess(WebProcess.AutoBattleReward);
            if (await autoBattleRewardProcess.OnNetworkAsyncRequest())
            {
                autoBattleRewardProcess.OnNetworkResponse();
                SetInfo();
                Model.ClearStackedRewardGoods();
                View.ShowAsync().Forget();
            }
        }
    }

    private async UniTask GetFastRewardPopup()
    {
        int fastRewardCount = NetMiningManager.Instance.FastRewardCount;
        int maxFastRewardCount = NetMiningManager.Instance.MaxFastRewardCount;
        int rewardTime = NetMiningManager.Instance.FastBattleRewardTime;

        BaseController fastRewardPopupController = UIManager.Instance.GetController(UIType.FastRewardPopup);
        FastRewardPopupModel model = fastRewardPopupController.GetModel<FastRewardPopupModel>();
        model.SetFastRewardPopup(fastRewardCount, maxFastRewardCount, rewardTime, Model.GetMainRewardGoods(), Model.AutoBattleData);
        await UIManager.Instance.EnterAsync(fastRewardPopupController);
    }

    /// <summary> 튜토리얼 중에는 임의로 세팅된 값을 표기한다.</summary>
    private void SetTutorialInfo()
    {
        //Set Time
        NetMiningManager netMiningManager = NetMiningManager.Instance;
        Model.SetElapsedTimeInfo(0);

        //Set StageDungeon & NetMiningLevel
        StageDungeonTable tempTable = TableManager.Instance.GetTable<StageDungeonTable>();
        StageDungeonGroupModel dungeonClearModel = DungeonManager.Instance.GetDungeonGroupModel<StageDungeonGroupModel>();
        StageDungeonTableData stageDungeonData = tempTable.GetDataByID(dungeonClearModel.GetClearDataStageDungeonId(false));
        int totalClearCount = dungeonClearModel.GetTotalClearCount(false);

        AutoBattleTableData autoBattleData = netMiningManager.GetCurrentAutoBattleDataByClearCount(totalClearCount);
        int nextClearCount = netMiningManager.GetNextLevelClearCount(autoBattleData);
        Model.SetStageClearInfo(stageDungeonData, autoBattleData, totalClearCount, nextClearCount);
        Model.SetTutorialRewardInfo();
    }

    private void SetInfo()
    {
        //Set Time
        NetMiningManager netMiningManager = NetMiningManager.Instance;
        Model.SetElapsedTimeInfo(netMiningManager.GetNormalChargePercentage());
        //Model.SetOverCharge(netMiningManager.IsOverCharge);
        //Model.SetOverElapsedTimeInfo(netMiningManager.GetOverChargePercentage());

        //Set StageDungeon & NetMiningLevel
        StageDungeonTable tempTable = TableManager.Instance.GetTable<StageDungeonTable>();
        StageDungeonGroupModel dungeonClearModel = DungeonManager.Instance.GetDungeonGroupModel<StageDungeonGroupModel>();
        StageDungeonTableData tempTableData = tempTable.GetDataByID(dungeonClearModel.GetClearDataStageDungeonId(false));
        int totalClearCount = dungeonClearModel.GetTotalClearCount(false);

        AutoBattleTableData currentData = netMiningManager.GetCurrentAutoBattleDataByClearCount(totalClearCount);
        int nextClearCount = netMiningManager.GetNextLevelClearCount(currentData);
        Model.SetStageClearInfo(tempTableData, currentData, totalClearCount, nextClearCount);
    }

    private async UniTask TaskUpdateElapsedSeconds()
    {
        int prevElapsedSeconds = int.MinValue;
        while (View != null)
        {
            if (!isPlayingChargeAnimation)
            {
                int elapsedSeconds = NetMiningManager.Instance.ElapsedSeconds;
                if (prevElapsedSeconds != elapsedSeconds)
                {
                    prevElapsedSeconds = elapsedSeconds;
                    UpdateElapsedInfo();
                    View.ShowProgress();
                }
            }
            await UniTask.NextFrame(cancellationToken: TokenPool.Get(GetHashCode()));
        }
    }

    private async UniTask TaskForcedChargeAnimation(int forcedChargeCount)
    {
        isPlayingChargeAnimation = true;

        // 애니메이션 시작 시점, 엔딩 시점 세팅
        float startSeconds = NetMiningManager.Instance.GetNormalChargeElapsedTimeTotalSeconds();
        float startChargePercent = NetMiningManager.Instance.GetNormalChargePercentage();

        GoodsGeneratorModel generator = new GoodsGeneratorModel();
        GoodsModel<Goods> endRewardGoods = generator.GetGoodsModelByRewardDataId((int)RewardDefine.REWARD_AUTOBATTLE_TUTORIAL);
        GoodsModel<Goods> currentRewardGoods = generator.GetGoodsModelByRewardDataId((int)RewardDefine.REWARD_AUTOBATTLE_TUTORIAL);
        int[] startCount = new int[currentRewardGoods.Count];

        AutoBattleDto dto = new AutoBattleDto();
        if (forcedChargeCount == 0)
        {
            dto.startedAt = DateTime.UtcNow.AddHours(-12).ToString();
            for (int i = 0; i < endRewardGoods.Count; i++)
            {
                startCount[i] = 1;
                Goods goods = endRewardGoods.GetGoodsByIndex(i);
                goods.SetCount(goods.Count / 2);
            }
        }
        else
        {
            dto.startedAt = DateTime.UtcNow.AddHours(-24).ToString();
            for (int i = 0; i < endRewardGoods.Count; i++)
            {
                startCount[i] = (int)(endRewardGoods.GetGoodsByIndex(i).Count / 2);
            }
            View.ShowForcedChargeButton(false);
        }
        NetMiningManager.Instance.SetNetMiningInfo(dto);

        float endSeconds = NetMiningManager.Instance.GetNormalChargeElapsedTimeTotalSeconds();
        float endChargePercent = NetMiningManager.Instance.GetNormalChargePercentage();

        // 연출 시작
        float duration = 2.0f;
        float elapsedTime = 0f;

        SoundManager.SfxFmod.Play(StringDefine.FMOD_EVENT_UI_MENU_SFX, StringDefine.FMOD_DEFAULT_PARAMETER, 27);

        Time.timeScale = 10f;
        duration *= Time.timeScale;
        View.ShowForcedChargeObject(true);

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;

            float currentSeconds = Mathf.Lerp(startSeconds, endSeconds, t);
            int currentChargePercent = (int)Mathf.Lerp(startChargePercent, endChargePercent, t);

            for (int i = 0; i < currentRewardGoods.Count; i++)
            {
                Goods endGoods = endRewardGoods.GetGoodsByIndex(i);
                Goods currentGoods = currentRewardGoods.GetGoodsByIndex(i);

                int count = (int)Mathf.Lerp(startCount[i], endGoods.Count, t);
                currentGoods.SetCount(count);
            }

            Model.SetElapsedTimeInfo(currentChargePercent);
            Model.SetStackedRewardGoods(currentRewardGoods);

            View.ShowProgress();
            View.ShowForcedChargeThumbnail();

            var netMiningUnit = AdditivePrefabManager.Instance.NetMiningUnit;
            netMiningUnit.Model.SetTotalSeconds(currentSeconds);
            netMiningUnit.Model.SetProgressPercent(currentChargePercent);
            netMiningUnit.RefreshAsync().Forget();

            await UniTask.NextFrame();
        }

        // 연출 종료
        Time.timeScale = 1f;
        View.ShowForcedChargeObject(false);
        if (this.forcedChargeCount == 1)
            SoundManager.SfxFmod.Play(StringDefine.FMOD_EVENT_UI_MENU_SFX, StringDefine.FMOD_DEFAULT_PARAMETER, 28);

        this.forcedChargeCount++;
        isPlayingChargeAnimation = false;
    }

    private void UpdateElapsedInfo()
    {
        try
        {
            float totalSeconds = NetMiningManager.Instance.GetNormalChargeElapsedTimeTotalSeconds();
            float normalChargePercent = NetMiningManager.Instance.GetNormalChargePercentage();
            //float overChargePercent = NetMiningManager.Instance.GetOverChargePercentage();

            Model.SetElapsedTimeInfo(normalChargePercent);
            //Model.SetOverElapsedTimeInfo(overChargePercent);

            var netMiningUnit = AdditivePrefabManager.Instance.NetMiningUnit;
            netMiningUnit.Model.SetTotalSeconds(totalSeconds);
            netMiningUnit.Model.SetProgressPercent(normalChargePercent);
            netMiningUnit.RefreshAsync().Forget();
        }
        catch
        {
        }
    }

    private bool CheckPlayAnimationToday()
    {
        string playAt = PlayerPrefsWrapper.GetString(StringDefine.KEY_PLAYER_PREFS_PLAY_NETMINING_ANIMATION_DATE);

        if (playAt == string.Empty)
            return false;

        DateTime playTime = DateTime.Parse(playAt);
        DateTime refreshTime = TimeManager.Instance.GetDailyResetTimeByDate(playTime);

        return (refreshTime - NetworkManager.Web.ServerTimeUTC).TotalSeconds > 0;
    }

    public override async UniTask TutorialStepAsync(System.Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        //넷마이닝 튜토리얼은 기존 넷마이닝 동작과 달리 튜토리얼 전용 플로우대로 동작합니다.
        switch (stepType)
        {
            case TutorialExplain.NetMiningGetReward:
                {
                    //튜토리얼 마지막 스텝 고정. 튜토리얼 완료 보상이 넷마이닝 보상을 대체합니다. 튜토리얼중에는 단순 오브젝트 없애는 연출만 진행합니다.
                    NetMiningManager.Instance.SetStartAt(DateTime.UtcNow);
                    SetInfo();
                    Model.ClearStackedRewardGoods();
                    View.ShowAsync().Forget();
                    break;
                }

            case TutorialExplain.NetMiningShowRewards:
                {
                    await ShowTutorialRewardList();
                    break;
                }

            case TutorialExplain.NetMiningForcedCharge:
                {
                    OnEventForcedCharge();
                    await UniTask.WaitUntil(() => isPlayingChargeAnimation);
                    await UniTask.WaitUntil(() => !isPlayingChargeAnimation);
                    break;
                }
        }
    }
    #endregion Coding rule : Function
}