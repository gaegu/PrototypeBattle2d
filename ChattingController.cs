//=========================================================================================================
#pragma warning disable CS1998
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.Observer.Core;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)

#if YOUME
public class ChattingController : BaseController, IObserver
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.ChattingPopup; } }
    public override void SetModel() { SetModel(new ChattingPopupModel()); }
    private ChattingPopup Popup { get { return base.BaseView as ChattingPopup; } }
    private ChattingPopupModel Model { get { return GetModel<ChattingPopupModel>(); } }
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
        // 기본이 멀티 월드
        Model.SetMyUserId(PlayerManager.Instance.MyPlayer.User.UserId);
        Model.SetCategory(ChattingChannelType.Public);
        Model.SetChattingChannel(ChattingManager.Instance.GetChattingChannel(Model.Category));

        //공지사항 표기
        SetNotice();

        Model.SetEventToggleNotice(OnEventToggleNotice);
        Model.SetEventEndEdit(OnEventEndEdit);
        Model.SetEventChangeCategory(OnEventChangeCategory);
        Model.SetEventChangeChannel(OnEventChangeChannel);
        Model.SetEventEmojiListOpen(OnEventEmojiListOpen);

        ObserverManager.AddObserver(ChattingObserverID.ChattingChannelConnected, this);
        ObserverManager.AddObserver(ChattingObserverID.ChattingGetPublicMessage, this);
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        ObserverManager.RemoveObserver(ChattingObserverID.ChattingChannelConnected, this);
        ObserverManager.RemoveObserver(ChattingObserverID.ChattingGetPublicMessage, this);

        return await base.Exit(onEventExtra);
    }

    public override async UniTask Process()
    {
        await Popup.ShowAsync();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Chatting/ChattingPopup";
    }

    /// <summary> 공지사항 등록 </summary>
    private void SetNotice()
    {
        Model.SetOpenNotice(false);

        Notice notice = NoticeManager.Instance.GetFirstNotice();
        Model.SetNotice(notice != null ? notice.Message : string.Empty);
    }

    private void OnEventToggleNotice()
    {
        Model.SetOpenNotice(!Model.IsOpenNotice);
        Popup.ShowNotice();
    }

    /// <summary>
    /// 채팅 입력
    /// </summary>
    private void OnEventEndEdit(string input)
    {
        if (!Model.IsConnectedChannel)
            return;

        if (string.IsNullOrEmpty(input))
            return;

        if (TableManager.Instance.CheckForbiddenWord(input, out string result))
        {
            // {금칙어} 단어는 사용할 수 없습니다.
            MessageBoxManager.ShowToastMessage(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_INPUT_FORBIDDEN_WORD, result));
            return;
        }

        Chatting chatting = ChattingManager.Instance.GetChatting(ChattingMessageType.Message, input, 0);
        ChattingManager.Instance.SendMessage(Model.Category, chatting);
    }

    /// <summary>
    /// 채널 변경 팝업 오픈
    /// </summary>
    private void OnEventChangeChannel()
    {
        if (!Model.IsPossibleChannelChange)
            return;

        BaseController inputBoxController = UIManager.Instance.GetController(UIType.InputBoxPopup);
        InputBoxPopupModel model = inputBoxController.GetModel<InputBoxPopupModel>();
        model.SetStateType(InputBoxPopupModel.StateType.ChannelChange);
        model.SetEventConfirm(OnEventChangeChannelProcess);

        UIManager.Instance.EnterAsync(inputBoxController);
    }

    /// <summary>
    /// 채널 변경
    /// </summary>
    private void OnEventChangeChannelProcess(string input)
    {
        UIManager.Instance.Exit(UIType.InputBoxPopup).Forget();

        if (!int.TryParse(input, out int channelNumber))
            return;

        if (ChattingManager.Instance.CheckChattingChannel(ChattingChannelType.Public, channelNumber))
        {
            // 이미 채널 채팅에 입장하셨습니다.
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_CHATTINGPOPUP_ALREADY_ENTERED_CHANNEL);
            return;
        }

        ChattingManager.Instance.ConnectChannel(ChattingChannelType.Public, channelNumber);
    }

    /// <summary>
    /// 카테고리 변경
    /// </summary>
    private void OnEventChangeCategory(System.Action onEventSuccess, int category)
    {
        ChattingChannelType channelType = (ChattingChannelType)category;

        if (channelType == ChattingChannelType.Guild)
        {
            if (!ContentsOpenManager.Instance.CheckContentsOpen(ContentsOpenDefine.CONTENTS_GUILD))
            {
                // 길드 콘텐츠가 아직 열리지 않았습니다
                MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_CHATTINGPOPUP_GUILD_HAS_NOT_OPENED_YET);
                return;
            }

            if (PlayerManager.Instance.MyPlayer.User.GuildModel.IsJoinedGuild)
            {
                if ((ChattingChannelType)Model.ChattingChannel.ChannelType == channelType)
                {
                    // 이미 채널 채팅에 입장하셨습니다.
                    MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_CHATTINGPOPUP_ALREADY_ENTERED_CHANNEL);
                    return;
                }
            }
            else
            {
                //ContentsOpenManager.Instance.OpenContents(ContentsType.Guild).Forget();
                return;
            }
        }
        else
        {
            if ((ChattingChannelType)Model.ChattingChannel.ChannelType == channelType)
            {
                // 이미 채널 채팅에 입장하셨습니다.
                MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_CHATTINGPOPUP_ALREADY_ENTERED_CHANNEL);
                return;
            }
        }

        Model.SetCategory(channelType);
        Model.SetChattingChannel(ChattingManager.Instance.GetChattingChannel(Model.Category));

        Popup.ShowChattingScroll(Model.ChattingChannel.ChattingCount, isReset: false).Forget();

        onEventSuccess?.Invoke();
    }

    /// <summary>
    /// 이모지 UI 오픈
    /// </summary>
    private void OnEventEmojiListOpen()
    {
        BaseController emojiController = UIManager.Instance.GetController(UIType.ChattingEmojiPopup);
        ChattingEmojiPopupModel model = emojiController.GetModel<ChattingEmojiPopupModel>();
        model.SetCategory(Model.Category);

        UIManager.Instance.EnterAsync(emojiController).Forget();
    }

    void IObserver.HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        switch (observerMessage)
        {
            case ChattingObserverID.ChattingChannelConnected:
                {
                    Model.SetChattingChannel(ChattingManager.Instance.GetChattingChannel(Model.Category));
                    Popup.ShowChattingScroll(Model.ChattingChannel.ChattingCount, isReset: false).Forget();
                    Popup.ShowChannelName();
                    break;
                }

            case ChattingObserverID.ChattingGetPublicMessage:
                {
                    Popup.RefreshMessage();
                    break;
                }

        }
    }
    #endregion Coding rule : Function
}
#else
public class ChattingController : BaseController, IObserver
{
    public override bool IsPopup => throw new NotImplementedException();

    public override UIType UIType => throw new NotImplementedException();

    public override string GetUIPrefabName()
    {
        throw new NotImplementedException();
    }

    void IObserver.HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        throw new NotImplementedException();
    }
}
#endif