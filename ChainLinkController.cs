//=========================================================================================================
#pragma warning disable CS1998
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Febucci.UI.Core;
using IronJade.LowLevel.Server.Web;
using IronJade.Observer.Core;
using IronJade.Server.Web.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;
using UnityEngine.UIElements;
//=========================================================================================================

public class ChainLinkController : BaseController
{
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.ChainLinkView; } }
    private ChainLinkView View { get { return base.BaseView as ChainLinkView; } }
    protected ChainLinkViewModel Model { get; private set; }
    public ChainLinkController() { Model = GetModel<ChainLinkViewModel>(); }
    public ChainLinkController(BaseModel baseModel) : base(baseModel) { }

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    private bool isUpdating = false;

    private Queue<Goods> characterGetAnimQueue = new Queue<Goods>();
    private bool isShowingCharacterGetAnim;
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        Model.SetCurrentState(ChainLinkViewModel.ShowState.Gacha);
        Model.SetOnChangeState(OnChangeState);

        Model.SetOnClickGachaUnitTab(OnClickGachaUnitTab);
        Model.SetOnScrollSelectionChanged(OnScrollSelectionChanged);
        Model.SetOnEventGacha(OnClickGacha);

        Model.SetOnClickProbabilityInfo(OnClickProbabilityInfo);
        Model.SetOnClickMileageShop(OnClickMileageShop);
        Model.SetEventUpdateGameEvent(OnEventUpdateGameEvent);

        Model.SetOnClickCharacterGetNext(OnClickCharacterGetNext);
        Model.SetOnClickCharacterGetSkip(OnClickCharacterGetSkip);
        Model.SetOnClickGachaStop(OnClickGachaStop);
        Model.SetOnClickGachaResume(OnClickGachaResume);

        Model.SetOnClickBack(OnClickBack);
    }

    public override async UniTask LoadingProcess()
    {
        Model.ClearAllModels();

        GetGachaShopList();
        await RequestGachaCountGet();

        Model.SetShowCurrencyUnitModels(PlayerManager.Instance.MyPlayer.User);
    }

    public override async UniTask AfterCreateUIProcess()
    {
        await View.PreloadAsync();
    }

    public override UniTask LoadingBackProcess()
    {
        return UniTask.CompletedTask;
    }

    public override async UniTask Process()
    {
        View.ShowCurrencyUnits().Forget();
        await View.ShowAsync();
    }

    public override void Refresh()
    {
        View.RefreshAsync().Forget();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "ChainLink/ChainLinkView";
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        AdditivePrefabManager.Instance.AppUnit.HideBackground();
        //결과화면에서 뒤로가기 누르면 가챠 메인화면으로 이동.
        if (Model.CurrentState == ChainLinkViewModel.ShowState.Result)
        {
            if (View.IsPlayingAnimation)
                return true;

            Model.OnChangeState(ChainLinkViewModel.ShowState.Gacha);
            CameraManager.Instance.OnVolumeBlur();  //결과화면을 갔다오면서 블러가 꺼지기 때문에 다시 켜줌
            TownSceneManager.Instance.PlayBGM();
            View.RefreshAsync().Forget();

            return false;
        }
        else if (Model.CurrentState == ChainLinkViewModel.ShowState.Cutscene || Model.CurrentState == ChainLinkViewModel.ShowState.CharacterGet)
        {
            return true;
        }
        else
        {
            return await base.Exit(onEventExtra);
        }
    }

    public void OnChangeState(ChainLinkViewModel.ShowState state)
    {
        Model.SetCurrentState(state);

        View.RefreshAsync().Forget();
    }

    private void ShowMessageBoxPopup(string msg)
    {
        //튜토리얼 도중에는 가챠 재화가 부족하면 안됨
        if (TutorialManager.Instance.CheckTutorialPlaying())
        {
            IronJade.Debug.LogError("[Tutorial]Error - 튜토리얼 진행 도중에는 재화가 부족한 상황이 발생하면 안됩니다.\n치트 없이 진행 도중 현재 메세지가 표기되었다면 플로우/보상이 정상적으로 세팅되어있는지 확인 바랍니다.");
            TutorialManager.Instance.ForcedQuitTutorial();
        }

        MessageBoxManager.ShowYesBox(msg).Forget();
    }
    private void ShowCurrencyMessageBoxPopup(string msg, CurrencyUnitModel[] currencyUnitModels, System.Action callback = null)
    {
        MessageBoxManager.ShowCurrencyMessageBox(msg, currencyUnitModels, false, callback);
    }

    private async UniTask TaskGacha(int index, int count)
    {
        //1. (결과화면이라면) 화면 터치 안되게 막기
        View.SetResultTouchMask(true);

        int gachaId = Model.GachaUnitModelList[index].GachaShopData.GetID();
        Model.ClearResultModels();

        //2. 가챠 통신
        bool gachaResultSuccess = await RequestGacha(gachaId, count);
        if (!gachaResultSuccess)
        {
            View.SetResultTouchMask(false);
            return;
        }
        

        //3. 통신받은 결과로 연출 세팅
        characterGetAnimQueue.Clear();
        characterGetAnimQueue = Model.GetGachaResultAnimDataQueue();

        //4. 엘리베이터 연출 재생
        if (characterGetAnimQueue.Count > 0)
        {
            var resultData = characterGetAnimQueue.Peek();
            await TaskGachaCutscene(false, GetGachaResultTier(resultData), GetGachaResultLicense(resultData));
        }

        //5. 캐릭터 획득 연출 or 결과화면
        Model.SetCurrentGachaIndexCountPair(index, count);
        if (Model.IsShowCharacterGetAnim)
        {
            await TaskShowCharacterGetAnimation();
        }
        else
        {
            BackgroundSceneManager.Instance.ShowTownGroup(true);
            await OnEndResultSequence();
            await TransitionManager.Out(TransitionType.WhiteFadeInOut);
        }
    }

    /// <summary> 가챠씬 컷씬 재생 </summary>
    /// <param name="isRepeat">10연차에서 연속으로 나온 경우 true</param>
    /// <param name="gachaTier"></param>
    /// <returns></returns>
    private async UniTask TaskGachaCutscene(bool isRepeat, CharacterGetCutsceneModel.GachaTier gachaTier = CharacterGetCutsceneModel.GachaTier.X, LicenseType licenseType = LicenseType.Black)
    {
        //1. 컷씬에 필요한 데이터 세팅
        CharacterGetCutsceneModel characterGetCutsceneModel = new CharacterGetCutsceneModel();
        characterGetCutsceneModel.SetRepeat(isRepeat);
        characterGetCutsceneModel.SetResultTier(gachaTier);
        characterGetCutsceneModel.SetLicenseType(licenseType);
        characterGetCutsceneModel.SetEventBgmState(View.ChangeBgmState);
        CutsceneManager.Instance.SetCutsceneModels(new ICutsceneModel[] { characterGetCutsceneModel });

        //2. 트랜지션으로 가리기
        if (isRepeat)
            await TransitionManager.In(TransitionType.WhiteFadeInOut);   //캐릭터 연출씬 -> 컷씬
        else
            await TransitionManager.In(TransitionType.Rotation); // UI -> 컷씬

        CameraManager.Instance.OffVolumeBlur();
        UIManager.Instance.EnableApplicationFrame(false);

        //3. 캐릭터 연출이 있다면 제거
        if (isRepeat)
        {
            await AdditivePrefabManager.Instance.UnLoadCloseUIAsync();
            UIManager.Instance.EnableApplicationFrame(false);
        }

        //4. 가챠 컷씬 로드
        await CutsceneManager.Instance.Load(StringDefine.PATH_CHAINLINK_GACHA_CUTSCENE);
        CutsceneManager.Instance.ShowSkipButton(true);

        //5. ChainlinkView 안보이도록 State 변경
        Model.SetCurrentState(ChainLinkViewModel.ShowState.Cutscene);

        // 어플리케이션 유닛 숨기기 [가챠씬의 카메라가 베이스가 되어야함]
        AdditivePrefabManager.Instance.AppUnit.HideBackground();
        AdditivePrefabManager.Instance.AppUnit.SafeSetActive(false);

        View.RefreshAsync().Forget();

        //6. 컷씬 재생 (트랜지션과 동시 진행되어야 해서 Forget)
        CutsceneManager.Instance.PlayCutsceneSequence().Forget();

        //7. 트랜지션 풀기
        if (isRepeat)
            await TransitionManager.Out(TransitionType.WhiteFadeInOut);   //캐릭터 연출씬 -> 컷씬
        else
            await TransitionManager.Out(TransitionType.Rotation);  // UI -> 컷씬

        //8. 컷씬 종료까지 대기
        await CutsceneManager.Instance.WaitPlaying();

        //9. 트랜지션으로 가리기
        await TransitionManager.In(TransitionType.WhiteFadeInOut);

        //10. 컷씬 언로드
        CutsceneManager.Instance.Unload(refreshTownObject: false).Forget();
    }

    private CharacterGetCutsceneModel.GachaTier GetGachaResultTier(Goods resultGoods)
    {
        CharacterGetCutsceneModel.GachaTier gachaTier = (CharacterTier)resultGoods.Tier switch
        {
            CharacterTier.XA => CharacterGetCutsceneModel.GachaTier.XA,
            CharacterTier.X => CharacterGetCutsceneModel.GachaTier.X,
            _ => CharacterGetCutsceneModel.GachaTier.None,
        };

        return gachaTier;
    }

    private LicenseType GetGachaResultLicense(Goods resultGoods)
    {
        switch (resultGoods)
        {
            case Character character:
                {
                    return character.License;
                }

            case Reeltape reeltape:
                {
                    if (reeltape.Tier == (int)CharacterTier.XA)
                    {
                        ReeltapeTable reeltapeTable = TableManager.Instance.GetTable<ReeltapeTable>();
                        CharacterTable characterTable = TableManager.Instance.GetTable<CharacterTable>();

                        var reeltapeData = reeltapeTable.GetDataByID(reeltape.DataId);
                        var linkedCharacterData = characterTable.GetDataByID(reeltapeData.GetLINKED_CHARACTER());

                        return (LicenseType)linkedCharacterData.GetLICENSE();
                    }
                    else
                    {
                        return LicenseType.Blue;
                    }   
                }
        }
        return LicenseType.Blue;    //기본값 Blue(버튼연출 M)
    }

    /// <summary> 캐릭터 연출씬 재생</summary>
    private async UniTask TaskShowCharacterGetAnimation()
    {
        GoodsGeneratorModel goodsGeneratorModel = new GoodsGeneratorModel();

        bool isRepeat = false;
        isShowingCharacterGetAnim = false;

        while (characterGetAnimQueue.Count > 0)
        {
            if (!isShowingCharacterGetAnim)
            {
                Goods resultData = characterGetAnimQueue.Peek();

                // 1. 전조 구간 연출 재생
                if (isRepeat)
                {
                    await TaskGachaCutscene(isRepeat, GetGachaResultTier(resultData), GetGachaResultLicense(resultData));
                    await CutsceneManager.Instance.Unload(false);
                }

                // 2. 캐릭터 인트로 연출 재생 
                if (resultData is Character)
                {
                    Character character = goodsGeneratorModel.CreateCharacterByGoodsValue(resultData.DataId);
                    await AdditivePrefabManager.Instance.LoadAsync(AdditiveType.CharacterIntro);
                    UIManager.Instance.EnableApplicationFrame(false);

                    var introUnit = AdditivePrefabManager.Instance.IntroUnit;
                    introUnit.Model.SetCharacterIntroModel(character, OnClickCharacterGetNext, OnClickCharacterGetSkip, OnClickCharacterGetNext);
                    await introUnit.ShowAsync();

                    Model.OnChangeState(ChainLinkViewModel.ShowState.CharacterGet);

                    await View.RefreshAsync();
                    await TransitionManager.Out(TransitionType.WhiteFadeInOut);

                    isShowingCharacterGetAnim = true;

                    // 엘베 컷씬도 반복재생.
                    isRepeat = true;
                }
                else if (resultData is Reeltape)        // 2. 릴테이프 인트로 연출 재생
                {
                    Reeltape reeltape = goodsGeneratorModel.CreateReeltapeByDataId(resultData.DataId);
                    await AdditivePrefabManager.Instance.LoadAsync(AdditiveType.CharacterIntro);
                    UIManager.Instance.EnableApplicationFrame(false);

                    var introUnit = AdditivePrefabManager.Instance.IntroUnit;
                    introUnit.Model.SetReeltapeIntroModel(reeltape, OnClickCharacterGetNext, OnClickCharacterGetSkip, OnClickCharacterGetNext);
                    await introUnit.ShowAsync();

                    Model.OnChangeState(ChainLinkViewModel.ShowState.CharacterGet);

                    await View.RefreshAsync();
                    await TransitionManager.Out(TransitionType.WhiteFadeInOut);

                    isShowingCharacterGetAnim = true;

                    // 엘베 컷씬도 반복재생.
                    isRepeat = true;
                }
                else
                {
                    characterGetAnimQueue.Dequeue();
                    isShowingCharacterGetAnim = false;
                }
            }

            await UniTask.NextFrame();
        }

        await TransitionManager.In(TransitionType.WhiteFadeInOut);
        await AdditivePrefabManager.Instance.UnLoadCloseUIAsync();

        BackgroundSceneManager.Instance.ShowTownGroup(true);
        await OnEndResultSequence();
        await TransitionManager.Out(TransitionType.WhiteFadeInOut);

        isShowingCharacterGetAnim = false;
    }

    #region Request
    private void GetGachaShopList()
    {
        int index = 0;

        ChainLinkInfoModel chainLinkInfoModel = ShopManager.Instance.GetScheduledShopInfoModel<ChainLinkInfoModel>();
        GachaCombineShopTable gachaShopTable = TableManager.Instance.GetTable<GachaCombineShopTable>();
        if (chainLinkInfoModel == null)
            return;

        IReadOnlyDictionary<int, GameEventDto> gachaShopList = chainLinkInfoModel.GetGachaShopList();
        if (gachaShopList == null)
            return;

        foreach (var dto in gachaShopList.Values)
        {
            //이벤트시간 해당 안되면 걍 안넣기
            if (!UtilModel.Time.CheckOpenTimeUTC(dto.StartAt, dto.EndAt))
                continue;

            GachaCombineShopTableData gachaShopData = gachaShopTable.GetDataByID(dto.contents.dataGachaCombineShopId);
            if (gachaShopData.IsNull())
                continue;

            ChainLinkGachaUnitModel gachaUnitModel = new ChainLinkGachaUnitModel();
            gachaUnitModel.SetGachaUnitModel(dto, index, PlayerManager.Instance.MyPlayer.User, OnEventUpdateGameEvent);
            Model.AddGachaUnitModel(gachaUnitModel);
            index++;
        }
    }

    private async UniTask RequestGachaCountGet()
    {
        BaseProcess process = NetworkManager.Web.GetProcess(WebProcess.GachaCountGet);
        if (await process.OnNetworkAsyncRequest())
        {
            process.OnNetworkResponse();
            GachaCountDto[] countData = process.GetResponse<GachaCountGetResponse>().data;
            for (int i = 0; i < countData.Length; i++)
            {
                var chainLinkGachaUnitModel = Model.GachaUnitModelList.Find(x => x.GachaShopType == (GachaCombineShopType)countData[i].gachaCombineShopType);
                if (chainLinkGachaUnitModel != null)
                    chainLinkGachaUnitModel.SetGachaCount(countData[i]);
            }
        }
    }

    private async UniTask<bool> RequestGacha(int gachaId, int count)
    {
        TransitionManager.LoadingUI(true, true);

        BaseProcess process = NetworkManager.Web.GetProcess(WebProcess.GachaShopCombine);
        BuyCombineGachaInDto inDto = new BuyCombineGachaInDto
        {
            dataGachaCombineShopId = gachaId,
            count = count
        };
        process.SetPacket(inDto);

        if (await process.OnNetworkAsyncRequest())
        {
            process.OnNetworkResponse();
            BuyCombineGachaOutDto response = process.GetResponse<GachaShopCombineResponse>().data;
            await ProcessGachaResult(response.goods, response.gachaResults);
            RefreshShowGoods();
            RefreshGachaUnit(response.gachaCount);
            TransitionManager.LoadingUI(false, false);

            return true;
        }

        TransitionManager.LoadingUI(false, false);
        return false;
    }

    private async UniTask RequestEventGet()
    {
        BaseProcess eventGetProcess = NetworkManager.Web.GetProcess(WebProcess.GameEventGet);

        if (await eventGetProcess.OnNetworkAsyncRequest())
            eventGetProcess.OnNetworkResponse();
    }

    private void RefreshShowGoods()
    {
        Model.SetShowCurrencyUnitModels(PlayerManager.Instance.MyPlayer.User);
        View.ShowCurrencyUnits().Forget();
    }

    private void RefreshGachaUnit(GachaCountDto dto)
    {
        var chainLinkGachaUnitModel = Model.GachaUnitModelList.Find(x => x.GachaShopType == (GachaCombineShopType)dto.gachaCombineShopType);
        if (chainLinkGachaUnitModel != null)
            chainLinkGachaUnitModel.SetGachaCount(dto);

        View.RefreshCurrentGachaUnit().Forget();
    }

    private async UniTask ProcessGachaResult(GoodsOutDto goodsOutDto, GachaResultDto[] resultDto)
    {
        if (goodsOutDto.characters == null && goodsOutDto.items == null)
        {
            IronJade.Debug.LogError("All OutDto is null");
            return;
        }

        //획득한 아이템 (마일리지) 처리
        CheckObtainGoods(goodsOutDto);

        //가챠 실행 전 전 User정보로부터 가챠 결과 모델 세팅
        CharacterGetResultGeneratorModel characterGetResultGeneratorModel = new CharacterGetResultGeneratorModel(PlayerManager.Instance.MyPlayer.User);
        var gachaResultModels = characterGetResultGeneratorModel.GetGachaResultUnitModels(goodsOutDto, resultDto);
        if (gachaResultModels != null)
            Model.AddResultThumbnailUnitModels(gachaResultModels);

        // User 업데이트
        await PlayerManager.Instance.UpdateUserGoodsModelByGoodsDto(goodsOutDto);

        //릴테이프와 연관된 코스튬 업데이트
        PlayerManager.Instance.MyPlayer.User.UpdateCostume();
    }

    private void CheckObtainGoods(GoodsOutDto goodsOutDto)
    {
        GoodsGeneratorModel goodsGeneratorModel = new GoodsGeneratorModel(PlayerManager.Instance.MyPlayer.User);
        IReadOnlyList<Goods> goodsArray = goodsGeneratorModel.GetObtainGoodsByGoodsDto(goodsOutDto);

        Model.SetObtainMileageItem(null);

        foreach (var goods in goodsArray)
        {
            IronJade.Debug.Log($"Check Obtain Goods [{goods.GoodsType}][{goods.DataId}]{goods.Name}");
            if (Model.FocusGachaUnitModel.MileageItemData.GetID() == goods.DataId)
            {
                Model.SetObtainMileageItem((Item)goods);
            }
        }
    }
    #endregion

    #region OnClick
    private void OnClickGacha(bool isMultiple)
    {
        var targetGachaUnit = Model.FocusGachaUnitModel;
        int count = isMultiple ? targetGachaUnit.MultipleGachaCount : targetGachaUnit.SingleGachaCount;

        OnClickGacha(targetGachaUnit.Index, count);
    }

    private void OnClickGacha(int index, int count)
    {
        User user = PlayerManager.Instance.MyPlayer.User;
        int currencyIndex = Model.SingleGachaCount == count ? 0 : 1;
        var currencyModels = Model.GetCostCurrencyModel(user, currencyIndex);
        bool isEnoughGoods = Model.CheckEnoughGoods(user, currencyModels);

        if (!Model.IsUseCostTypeIncludeTicket && !isEnoughGoods)
        {
            ShowMessageBoxPopup(Model.GetGachaCurrencyLackMessage(index, count));
        }
        else
        {
            ShowCurrencyMessageBoxPopup(Model.GetGachaTryMessage(index, count), currencyModels, () =>
            {
                if (isEnoughGoods)
                {
                    TaskGacha(index, count).Forget();
                }
                else
                {
                    ShowMessageBoxPopup(Model.GetGachaCurrencyLackMessage(index, count));
                }
            });
        }
    }

    private void OnClickGachaStop()
    {
        Model.OnChangeState(ChainLinkViewModel.ShowState.Gacha);
        View.RefreshAsync().Forget();
    }

    private void OnClickGachaResume()
    {
        OnClickGacha(Model.CurrentGachaIndexCountPair.Key, Model.CurrentGachaIndexCountPair.Value);
    }

    private async UniTask OnEndResultSequence()
    {
        UIManager.Instance.EnableApplicationFrame(true);

        Model.SetCurrentState(ChainLinkViewModel.ShowState.Result);

        await View.RefreshAsync(); // 여기서 RefreshAsync호출 하고 그 안에서 UpdateUI호출됨
        View.SetResultTouchMask(false);

        await AdditivePrefabManager.Instance.UnLoadAsync(AdditiveType.CharacterIntro);

        //숨겼던 유닛 다시 커줌
        AdditivePrefabManager.Instance.AppUnit.SafeSetActive(true);
        await AdditivePrefabManager.Instance.AppUnit.ShowBackground(StringDefine.PATH_CHAINLINK_BACKGROND);

        // 한번 안키면 결과창에서 이전 State에 블러가 남아있음.
        CameraManager.Instance.SetActiveCamera(GameCameraType.Blur, true);
    }

    private void OnClickProbabilityInfo()
    {
        GachaCombineShopTable gachaShopTable = TableManager.Instance.GetTable<GachaCombineShopTable>();

        int gachaShopId = Model.FocusGachaUnitModel.GachaShopData.GetID();
        GachaCombineShopTableData gachaShopTableData = gachaShopTable.GetDataByID(gachaShopId);
        GachaShopProbInfoGroupGenerator generator = new GachaShopProbInfoGroupGenerator();
        var probabilityInfos = generator.Generate(gachaShopTableData);

        BaseController controller = UIManager.Instance.GetController(UIType.ProbabilityInfoPopup);
        ProbabilityInfoPopupModel model = controller.GetModel<ProbabilityInfoPopupModel>();
        model.SetProbabilityInfoGroups(probabilityInfos);

        UIManager.Instance.EnterAsync(controller).Forget();

    }

    private void OnClickGachaUnitTab(bool isRight)
    {
        int resultIndex = Model.FocusGachaIndex;
        resultIndex = isRight ? resultIndex++ : resultIndex--;
        if (resultIndex > Model.GachaUnitModelList.Count - 1)
            resultIndex = 0;
        else if (resultIndex < 0)
            resultIndex = Model.GachaUnitModelList.Count - 1;

        Model.SetFocusGachaIndex(resultIndex);
        View.SnapGachaScroll(isRight);
    }
    
    private async UniTask CashShop()
    {
        CashShopGetProcess cashShopGetProcess = NetworkManager.Web.GetProcess<CashShopGetProcess>();
        
        if (await cashShopGetProcess.OnNetworkAsyncRequest())
        {
            cashShopGetProcess.OnNetworkResponse();

            var data = cashShopGetProcess.Response.data;
            
            BaseController controller = UIManager.Instance.GetController(UIType.CashShopView);
            controller.GetModel<CashShopViewModel>().SetCashShopOutDto(data);
            UIManager.Instance.EnterAsync(controller).Forget();
        }
    }
    
    private void OnClickOpenCashShop()
    {
        CashShop();
    }

    private void OnScrollSelectionChanged(int index)
    {
        if (Model.GachaUnitModelList.Count == 0)
            return;

        Model.SetFocusGachaIndex(index);
        RefreshShowGoods();
    }

    private void OnClickBack()
    {
        Back().Forget();
    }

    private void OnClickMileageShop()
    {
        MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_COMMON_TO_BE_DEVELOPED);

        //BaseController controller = UIManager.Instance.GetController(UIType.ContentsShopView);
        //ContentsShopViewModel contentsShopModel = controller.GetModel<ContentsShopViewModel>();
        //contentsShopModel.SetCurrentShop(ContentsShopCategory.Mileage);
        //UIManager.Instance.EnterAsync(controller).Forget();
    }

    private async UniTask OnEventUpdateGameEvent()
    {
        if (isUpdating)
            return;

        isUpdating = true;

        await RequestEventGet();

        GetGachaShopList();

        await View.ShowAsync();

        RefreshShowGoods();

        isUpdating = false;
    }

    public override async UniTask TutorialStepAsync(System.Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            case TutorialExplain.ChainLink10Gacha:
                {
                    View.OnClickTenGacha();
                    await TutorialManager.WaitUntilEnterUI(UIType.MessageBoxCurrencyPopup);
                    break;
                }

            case TutorialExplain.ChainLinkConfirm:
                {
                    OnClickGachaStop();
                    break;
                }

            case TutorialExplain.ChainLinkBack:
                {
                    Exit().Forget();
                    break;
                }
        }
    }

    public void OnClickCharacterGetNext()
    {
        characterGetAnimQueue.Dequeue();
        isShowingCharacterGetAnim = false;
    }

    public void OnClickCharacterGetSkip()
    {
        characterGetAnimQueue.Clear();
    }
    #endregion


    #region CHEAT
#if CHEAT
    public void OnEventCheatGachaTest(List<int> gachaDataIds)
    {
        CharacterTable characterTable = TableManager.Instance.GetTable<CharacterTable>();
        ReeltapeTable reeltapeTable = TableManager.Instance.GetTable<ReeltapeTable>();

        List<GachaResultUnitModel> models = new List<GachaResultUnitModel>();
        GoodsGeneratorModel generator = new GoodsGeneratorModel();

        for (int i = 0; i < gachaDataIds.Count; i++)
        {
            int dataId = gachaDataIds[i];

            if (!characterTable.GetDataByID(dataId).IsNull())
            {
                Character character = generator.CreateCharacterByDataId(dataId);
                GachaResultUnitModel model = new GachaResultUnitModel();
                model.SetGachaResultCharacter(character, false);
                models.Add(model);
            }
            else
            {
                Reeltape reeltape = generator.CreateReeltapeByDataId(dataId);
                GachaResultUnitModel model = new GachaResultUnitModel();
                model.SetGachaResultReeltape(reeltape, false);
                models.Add(model);
            }
        }

        if (models.Count < 10)
        {
            int remainCount = 10 - models.Count;
            CharacterTableData fillInData = characterTable.Find(x => x.GetCHARACTER_TYPE() == (int)CharacterType.PlayerCharacter && x.GetTIER() == (int)CharacterTier.A);
            Character character = generator.CreateCharacterByDataId(fillInData.GetID());
            GachaResultUnitModel model = new GachaResultUnitModel();
            model.SetGachaResultCharacter(character, false);
            for (int i = 0; i < remainCount; i++)
                models.Add(model);
        }

        Model.AddResultThumbnailUnitModels(models);
        TaskCheatGachaTest().Forget();
    }

    private async UniTask TaskCheatGachaTest()
    {
        characterGetAnimQueue.Clear();
        characterGetAnimQueue = Model.GetGachaResultAnimDataQueue();

        if (characterGetAnimQueue.Count > 0)
        {
            var resultData = characterGetAnimQueue.Peek();
            await TaskGachaCutscene(false, GetGachaResultTier(resultData), GetGachaResultLicense(resultData));
        }

        Model.SetCurrentGachaIndexCountPair(0, 10);
        if (Model.IsShowCharacterGetAnim)
        {
            await TaskShowCharacterGetAnimation();
        }
        else
        {
            BackgroundSceneManager.Instance.ShowTownGroup(true);
            await OnEndResultSequence();
            await TransitionManager.Out(TransitionType.WhiteFadeInOut);
        }
    }
#endif
    #endregion CHEAT
    #endregion Coding rule : Function
}