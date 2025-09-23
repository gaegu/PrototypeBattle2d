//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using Cysharp.Threading.Tasks;
using IronJade; // UniTask 관련 클래스 모음
using IronJade.Table.Data;
using IronJade.UI.Core;
using UnityEngine.TextCore.Text;
//using UnityEngine.PlayerLoop;
//using UnityEngine.Rendering.UI; // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class CharacterIntroduceController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.CharacterIntroduceView; } }
    public override void SetModel() { SetModel(new CharacterIntroduceViewModel()); }
    private CharacterIntroduceView View { get { return base.BaseView as CharacterIntroduceView; } }
    private CharacterIntroduceViewModel Model { get { return GetModel<CharacterIntroduceViewModel>(); } }
    //=========================================================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value

    private const int TRAINING_DUNGEONID = 899999; // 전투 미리보기용 던전의 ID
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        Model.SetEventShowIntrolView(OnEventShowIntro);
        Model.SetEventShowDetailView(OnEventShowDetailView);
        Model.SetEventShowSkillPreview(OnEventShowSkillPreview);
        Model.SetEventCharacterDetail(OnEventCharacterDetail);
        Model.SetEventCharacterCostume(OnEventCharacterCostume);
        Model.SetEventCharacterReviewMode(OnEventCharacterReviewMode);
        Model.SetEventDefaultMode(OnEventDefaultMode);
        Model.SetEventChangeIdle(OnEventChangeIdle);
        Model.SetEventHelmet(OnEventHelmet);
        Model.SetTownIdle(true);

        Model.SetMode(CharacterIntroduceViewModel.ModeType.Default);
        Model.SetToggleEvent();
    }

    public override async UniTask LoadingProcess()
    {
        await AdditivePrefabManager.Instance.LoadAsync(AdditiveType.CharacterIntroduce);
        await UtilModel.Resources.LoadSceneAsync(Model.BackgroundPath, UnityEngine.SceneManagement.LoadSceneMode.Additive);

        var introduceUnit = AdditivePrefabManager.Instance.IntroduceUnit;
        introduceUnit.Model.SetCharacter(Model.Character);
        introduceUnit.Model.SetCostumeDataList();
        await introduceUnit.ShowAsync();

        CameraManager.Instance.OffVolumeBlur();
        CameraManager.Instance.SetActiveTownCameras(false);
        CameraManager.Instance.ChangeVolumeType(Model.BackgroundPath, VolumeType.None);

        SoundManager.PlayCharacterBGM(Model.CharacterTableData);

        Model.SetHelmetButton(introduceUnit.CheckHelemtButton());
        OnEventHelmet(false);
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override async UniTask BackProcess()
    {
        await Process();
    }

    public override async UniTask PlayHideAsync()
    {
        return;
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        if (Model.Mode == CharacterIntroduceViewModel.ModeType.IntroViewer)
            return true;

        return await base.Exit(async (state) =>
        {
            if (state == UISubState.AfterLoading)
            {
                await UtilModel.Resources.UnLoadSceneAsync(Model.BackgroundPath);
                CameraManager.Instance.OnVolumeBlur();
                CameraManager.Instance.SetActiveTownCameras(true);
                return;
            }

            await OnEventExtra(onEventExtra, state);

        });
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Character/CharacterIntroduceView";
    }

    private async UniTask OnEventShowIntro()
    {
        Model.SetMode(CharacterIntroduceViewModel.ModeType.IntroViewer);
        View.ChangeMode();

        await AdditivePrefabManager.Instance.LoadAsync(AdditiveType.CharacterIntro);

        var introUnit = AdditivePrefabManager.Instance.IntroUnit;
        introUnit.Model.SetCharacterIntroModel(Model.Character, null, OnEventFinishIntro, onEventTouchScreen: OnEventFinishIntro);
        await introUnit.ShowAsync();
    }

    private void OnEventFinishIntro()
    {
        Model.SetMode(CharacterIntroduceViewModel.ModeType.Default);
        View.ChangeMode();

        AdditivePrefabManager.Instance.UnLoadAsync(AdditiveType.CharacterIntro).Forget();

        //CameraManager.Instance.ChangeVolumeType(VolumeType.CollectionFreeLock, false);
    }

    private async UniTask OnEventShowDetailView()
    {
        BaseController characterDetailController = UIManager.Instance.GetController(UIType.CharacterCollectionDetailView);
        CharacterCollectionDetailViewModel viewModel = characterDetailController.GetModel<CharacterCollectionDetailViewModel>();

        viewModel.SetUser(Model.User);
        viewModel.SetCharacterByGoods(Model.Character);
        viewModel.SetCharacters(Model.CharacterList);

        await UIManager.Instance.EnterAsync(characterDetailController);
    }

    private async UniTask OnEventShowSkillPreview()
    {
        //// 진입할 던전의 ID
        //await BattleInfoManager.EnterTrainingBattle(TRAINING_DUNGEONID, Model.Team);
    }

    private async UniTask OnEventCharacterDetail()
    {
        BaseController characterDetailController = UIManager.Instance.GetController(UIType.CharacterDetailView);
        CharacterDetailViewModel model = characterDetailController.GetModel<CharacterDetailViewModel>();
        Character character = PlayerManager.Instance.MyPlayer.User.CharacterModel.GetGoodsByDataId(Model.Character.DataId);

        model.SetCharacter(character);
        model.SetIntroduceButton(false);

        await UIManager.Instance.EnterAsync(characterDetailController, onEventExtra: async (state) =>
        {
            //if (state == UISubState.AfterLoading)
            //    CameraManager.Instance.ReleaseFreeLockVolume();
        });
    }

    private async UniTask OnEventCharacterCostume()
    {
        BaseController controller = UIManager.Instance.GetController(UIType.CharacterCostumeView);
        CharacterCostumeViewModel model = controller.GetModel<CharacterCostumeViewModel>();
        Character character = PlayerManager.Instance.MyPlayer.User.CharacterModel.GetGoodsByDataId(Model.Character.DataId);

        model.SetTargetCharacter(character);

        await UIManager.Instance.EnterAsync(controller, onEventExtra: async (state) =>
        {

        });
    }

    private void OnEventCharacterReviewMode()
    {
        Model.SetMode(CharacterIntroduceViewModel.ModeType.Viewer);
        View.ChangeMode();
    }

    private void OnEventDefaultMode()
    {
        switch (Model.Mode)
        {
            case CharacterIntroduceViewModel.ModeType.Viewer:
                {
                    Model.SetMode(CharacterIntroduceViewModel.ModeType.Default);
                    View.ChangeMode();
                    break;
                }
        }
    }

    private void OnEventChangeIdle()
    {
        Model.SetTownIdle(!Model.IsTownIdle);

        var introduceUnit = AdditivePrefabManager.Instance.IntroduceUnit;
        introduceUnit.Model.OnEventChangeIdle(Model.IsTownIdle);
    }

    private void OnEventHelmet(bool isOn)
    {
        var introduceUnit = AdditivePrefabManager.Instance.IntroduceUnit;
        introduceUnit.Model.OnEventHelmet(isOn);
    }
    #endregion Coding rule : Function
}