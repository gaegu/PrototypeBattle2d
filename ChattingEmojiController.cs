
//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)

#if YOUME
public class ChattingEmojiController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.ChattingEmojiPopup; } }
    public override void SetModel() { SetModel(new ChattingEmojiPopupModel()); }
    private ChattingEmojiPopup View { get { return base.BaseView as ChattingEmojiPopup; } }
    private ChattingEmojiPopupModel Model { get { return GetModel<ChattingEmojiPopupModel>(); } }
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
        Model.SetEmoji(OnEventSelectEmoji);
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Chatting/ChattingEmojiPopup";
    }

    /// <summary>
    /// 이모지 선택 (메시지 보내기)
    /// </summary>
    private void OnEventSelectEmoji(int index)
    {
        if (!ChattingManager.Instance.CheckConnected(Model.Category))
        {
            Exit().Forget();
            return;
        }

        Chatting chatting = ChattingManager.Instance.GetChatting(ChattingMessageType.Emoji, string.Empty, Model.GetDataEmojiId(index));
        ChattingManager.Instance.SendMessage(Model.Category, chatting);

        Exit().Forget();
    }
    #endregion Coding rule : Function
}
#else
public class ChattingEmojiController : BaseController
{
    public override bool IsPopup => throw new System.NotImplementedException();

    public override UIType UIType => throw new System.NotImplementedException();

    public override string GetUIPrefabName()
    {
        throw new System.NotImplementedException();
    }
}
#endif