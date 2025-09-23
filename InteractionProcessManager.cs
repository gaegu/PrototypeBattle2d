#pragma warning disable CS1998
using Cysharp.Threading.Tasks;
using IronJade.UI.Core;

/// <summary>
/// 마을 상호작용 관리
/// </summary>
public class InteractionProcessManager
{
    public async UniTask TalkProcess(bool isTalk, TownObjectType townObjectType, int targetDataId, string talkTargetKey, BaseTownInteraction townInteraction)
    {
        // 상호작용 시작
        //await TownSceneManager.Instance.TalkInteracting(isTalk);

        //// 플레이어를 상호작용 상태로 만듬
        //PlayerManager.Instance.MyPlayer.SetInteracting(isTalk);

        //// 대화 대상
        //ITownSupport townSupport = TownObjectManager.Instance.GetTownObjectByTargetKey<ITownSupport>(townObjectType, talkTargetKey);

        //if (isTalk)
        //{
        //    //// 해당 NPC의 현재 대화를 시작 상태로 전환한다. (Dialog 시작이 아닌, 대사의 시작)
        //    //townSupport.StartTalk();

        //    // 대화 카메라로 전환한다.
        //    CameraFollowData npcTalkCameraTarget = TownObjectManager.Instance.GetCameraFollowData(talkTargetKey);
        //    CameraManager.Instance.OnEventStartTalkCamera(npcTalkCameraTarget, false);

        //    // 대화창 오픈
        //    BaseController baseController = UIManager.Instance.GetController(UIType.DialogPopup);
        //    DialogPopupModel model = baseController.GetModel<DialogPopupModel>();
        //    model.SetData(townObjectType, targetDataId, talkTargetKey);
        //    model.SetInteraction(townInteraction);
        //    await UIManager.Instance.EnterAsync(baseController);
        //}
        //else
        //{
        //    //// 해당 NPC의 현재 대화를 종료 상태로 전환한다. (Dialog 종료가 아닌, 대사의 종료)
        //    //townSupport.EndTalk();

        //    // 카메라 시점 전환 연출을 보여준다.
        //    CameraManager.Instance.OnEventEndTalkCamera();
        //}
    }

    public async UniTask SmallTalkProcess(bool isTalk, TownObjectType townObjectType, int targetDataId, string talkTargetKey, string smallTalkEnumId, string smallTalkSound, BaseTownInteraction townInteraction)
    {
        // 상호작용 시작
        //await TownSceneManager.Instance.TalkInteracting(isTalk);

        //// 플레이어를 상호작용 상태로 만듬
        //PlayerManager.Instance.MyPlayer.SetInteracting(isTalk);

        //// 대화 대상
        //ITownSupport townSupport = TownObjectManager.Instance.GetTownObjectByTargetKey<ITownSupport>(townObjectType, talkTargetKey);
        ////townSupport.SetTalkLayer(isTalk);  // 레이어를 바꿔준다. (대화 전용)

        //if (isTalk)
        //{
        //    // 대화 카메라로 전환한다.
        //    CameraFollowData npcTalkCameraTarget = TownObjectManager.Instance.GetCameraFollowData(talkTargetKey);
        //    CameraManager.Instance.OnEventStartTalkCamera(npcTalkCameraTarget, false);

        //    // 대화창 오픈
        //    BaseController baseController = UIManager.Instance.GetController(UIType.DialogPopup);
        //    DialogPopupModel model = baseController.GetModel<DialogPopupModel>();
        //    model.SetData(townObjectType, targetDataId, talkTargetKey);
        //    model.SetInteraction(townInteraction);
        //    model.SetCameraTarget(targetDataId.ToString());

        //    await UIManager.Instance.EnterAsync(baseController);

        //    // 스몰토크를 활성
        //    string targetKey = talkTargetKey;
        //    string talk = TableManager.Instance.GetLocalization(smallTalkEnumId);
        //    string talkSound = smallTalkSound;
        //    TownSmallTalk townSmallTalk = new TownSmallTalk(townSupport.TownObjectType, targetKey, talk, talkSound, townSupport.TalkTransform, true);
        //    await TownObjectManager.Instance.ShowSmallTalk(townSmallTalk);
        //}
        //else
        //{
        //    // 카메라 시점 전환 연출을 보여준다.
        //    CameraManager.Instance.OnEventEndTalkCamera();
        //}
    }

    public async UniTask ContentsOpenProcess(bool isTalk, TownObjectType townObjectType, int targetDataId, string talkTargetKey, BaseTownInteraction townInteraction)
    {
        await ContentsOpenManager.Instance.ShowContents(townInteraction.ContentsOpenDataId);
    }

    public async UniTask BattleProcess()
    {
        BaseController stageInfoController = UIManager.Instance.GetController(UIType.StageInfoWindow);
        StageInfoPopupModel stageInfoPopupModel = stageInfoController.GetModel<StageInfoPopupModel>();
        stageInfoPopupModel.SetDungeonData();
        stageInfoPopupModel.SetHideAfterStart(true);
        UIManager.Instance.EnterAsync(stageInfoController);
    }

    public async UniTask DynamicActionProcess(int targetDataId, string interactionId)
    {
        ITownSupport townSupport = TownObjectManager.Instance.GetTownObjectByDataId(TownObjectType.Gimmick, targetDataId);
        townSupport.PlayTimeline(interactionId);
    }
}