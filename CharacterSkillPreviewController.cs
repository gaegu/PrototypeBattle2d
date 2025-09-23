//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class CharacterSkillPreviewController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.CharacterSkillPreviewView; } }
    public override void SetModel() { SetModel(new CharacterSkillPreviewViewModel()); }
    private CharacterSkillPreviewView View { get { return base.BaseView as CharacterSkillPreviewView; } }
    private CharacterSkillPreviewViewModel Model { get { return GetModel<CharacterSkillPreviewViewModel>(); } }
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
        Model.SetEvent(OnEventSelectSkill, OnEventPlayingExecution);
        Model.SetSkill(0);
        Model.SetHideUI(false);
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override async UniTask<bool> Back(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        FlowManager.Instance.ChangeFlow(FlowType.TownFlow, null, isStack: false).Forget();

        return true;
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Character/CharacterSkillPreviewView";
    }

    private void OnEventPlayingExecution()
    {
        if (!BattleProcessManager.Instance.IsPlayingExecution)
        {
            Model.SetHideUI(false);
        }
    }
    
    private void OnEventSelectSkill(int index)
    {
        if (index == (int)CharacterSkillPreviewView.SkillType.Excution)
        {
            Model.SetExcutionSkill();
            Model.SetHideUI(true);
        }
        else
        {
            Model.SetSkill(index);
            Model.SetHideUI(false);
        }
        
        View.UiRefresh();

        BattleProcessManager.Instance.ViewerOnEventContinueAttackOff();
        BattleProcessManager.Instance.ViewerEventTownIdle(false);

        switch (index)
        {
            // 1 패시브 스킬
            case (int)CharacterSkillPreviewView.SkillType.Attack: // 일반 공격
                BattleProcessManager.Instance.ViewerOnEventContinueAttack(); // playe move
                break;
            case (int)CharacterSkillPreviewView.SkillType.Active: // 엑티브 스킬
                BattleProcessManager.Instance.ViewerEventSkill(false); // dash
                break;
            case (int)CharacterSkillPreviewView.SkillType.Excution: // 인연 스킬
                BattleProcessManager.Instance.ViewerEventTag((isBool) =>
                {
                    Model.SetHideUI(false);
                    View.ShowUI(!Model.IsHideUI);
                }); // 인연
                break;
        }

        BattleProcessManager.Instance.BattleInfo.SetBattleDungeonType(DungeonType.Training);

        UFE.Instance.GetControlsScriptTeamMember(UFE_TeamSide.Ally, 0).BattleCharInfo.SetIsTownIdle(false);
    }

    #endregion Coding rule : Function
}