//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using IronJade.LowLevel.Server.Web;
using IronJade.Server.Web.Core; // UniTask 관련 클래스 모음
using IronJade.UI.Core;
using UnityEngine; // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class GroundPassController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.GroundPassPopup; } }
    public override void SetModel() { SetModel(new GroundPassPopupModel()); }
    private GroundPassPopup View { get { return base.BaseView as GroundPassPopup; } }
    private GroundPassPopupModel Model { get { return GetModel<GroundPassPopupModel>(); } }
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
       Debug.unityLogger.logEnabled = true;

        Model.SetEventPassOutDto(EventManager.Instance.EventPass.GroundPassOutDtoList[0]);
        ResetCallBack();
        ResetModelData();
    }

    public override async UniTask LoadingProcess()
    {
    }

    public override async UniTask Process()
    {
        ShowUI(Model.Category);
    }

    public override void Refresh()
    {
        // showcut으로 이동후 미션을 클리어 하고 오면 클리어 정보가 변경되서 여기서 서버랑 데이터를 다시 맞춰줘야함
        EventManager.Instance.PacketEventPassGet((isBool) =>
        {
            Model.SetEventPassOutDto(EventManager.Instance.EventPass.GroundPassOutDtoList[0]);
            ResetModelData();
            ShowUI(Model.Category);
        }).Forget();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Event/GroundPassPopup";
    }

    private async void ShowUI(GroundPassPopupModel.CategoryType categoryType)
    {
        switch (categoryType)
        {
            case GroundPassPopupModel.CategoryType.Attendance:
                {
                    await View.ShowAttendance(true);
                    View.ShowAsync().Forget();
                    break;
                }

            case GroundPassPopupModel.CategoryType.Mission:
                {
                    await View.ShowMission(true);
                    View.ShowAsync().Forget();
                    break;
                }
        }
    }

    private void ResetCallBack()
    {
        Model.SetEventReceiveAll(OnEventReqRewardAllMission);
        Model.SetOnEventClose(OnEventClose);
        Model.SetOnEventMissionDaySelect(OnEventMissionDaySelect);
    }

    private void ResetModelData()
    {
        Model.SetToggleButtonUnitModels(OnEventSelectCategory);
        Model.SetEventPassTableData(Model.EventPassOutDto.eventPassId);

        var itemDto = MissionManager.Instance.GetGameEventDto(Model.EventPassOutDto.gameEventId);
        Model.SetGameEventDto(itemDto);
        Model.SetOpenDay();
        Model.SetRemainTime();

        Model.SetAttendance(OnEventReqRewardMission);
        Model.SetMission(OnEventReqRewardMission);
    }

    private void OnEventMissionDaySelect(int day)
    {
        Model.SetMissionDay(day);
        View.SelectMissionDay();
    }

    // 카테고리 선택
    private void OnEventSelectCategory(int selectCategory)
    {
        // 카테고리 선택
        Model.SetCategory((GroundPassPopupModel.CategoryType)selectCategory);

        // 바뀐 카테고리로 출력
        ShowUI(Model.Category);
    }

    public int[][] ConvertToArray(List<List<int>> listOfLists)
    {
        int[][] array = listOfLists.Select(innerList => innerList.ToArray()).ToArray();

        return array;
    }

    private void OnEventClose()
    {
        Exit().Forget();
    }

    #region Packet

    // 전체 보상받기
    private void OnEventReqRewardAllMission(int index)
    {
        List<List<int>> dataEventPassMissionIds = new List<List<int>>();
        List<int> eventMissionTypes = new List<int>();
        List<int> days = new List<int>();

        if (Model.Category == GroundPassPopupModel.CategoryType.Attendance)
        {
            eventMissionTypes = Model.AttendanceData.eventMissionTypes;
            dataEventPassMissionIds.Add(new List<int>());

            for (int i = 0; i < Model.AttendanceData.attendanceCompletedModels.Count; ++i)
            {
                GroundPassAttendanceUnitModel groundPassAttendanceUnitModel =
                    Model.AttendanceData.attendanceCompletedModels[i];

                days.Add(groundPassAttendanceUnitModel.Day);
                dataEventPassMissionIds[0].Add(groundPassAttendanceUnitModel.EventPassMissionTableData.GetID());
            }
        }
        else if (Model.Category == GroundPassPopupModel.CategoryType.Mission)
        {
            eventMissionTypes = Model.MissionData.eventMissionTypes;

            var Keys = Model.MissionData.missionCompletedModels.Keys.ToList();
            for (int i = 0; i < Keys.Count; ++i)
            {
                days.Add(Keys[i] + 1);
                dataEventPassMissionIds.Add(new List<int>());
                var groundPassMissionList = Model.MissionData.missionCompletedModels[Keys[i]];

                for (int j = 0; j < groundPassMissionList.Count; ++j)
                {
                    dataEventPassMissionIds[i].Add(groundPassMissionList[j].EventPassMissionTableData.GetID());
                }
            }
        }

        OnEventReqRewardMission(days.ToArray(), eventMissionTypes.ToArray(), ConvertToArray(dataEventPassMissionIds)).Forget();
    }

    // 단일 보상받기
    private async UniTask OnEventReqRewardMission(int day, int[] dataEventPassMissionIds)
    {
        int[] days = new[] { day };
        int[] eventMissionTypes = null;
        int[][] dataEventPassMissionIdss = new int[][] { dataEventPassMissionIds };

        if (Model.Category == GroundPassPopupModel.CategoryType.Attendance)
            eventMissionTypes = Model.AttendanceData.eventMissionTypes.ToArray();
        else if (Model.Category == GroundPassPopupModel.CategoryType.Mission)
            eventMissionTypes = new int[] { Model.MissionData.GetMissionType(dataEventPassMissionIds[0]) };

        await OnEventReqRewardMission(days, eventMissionTypes, dataEventPassMissionIdss);
    }

    // 보상 패킷
    private async UniTask OnEventReqRewardMission(int[] days, int[] eventMissionTypes, int[][] dataEventPassMissionIds)
    {
        BaseProcess rewardMissionProcess = NetworkManager.Web.GetProcess(WebProcess.RewardMission);
        rewardMissionProcess.SetPacket(new RewardEventPassInDto(Model.EventPassOutDto.gameEventId, days, eventMissionTypes, dataEventPassMissionIds));

        if (await rewardMissionProcess.OnNetworkAsyncRequest())
        {
            RewardMissionResponse rewardMissionResponse = rewardMissionProcess.GetResponse<RewardMissionResponse>();

            if (rewardMissionResponse.IsError)
            {
               IronJade.Debug.LogError("Packet Error Message : " + rewardMissionResponse.message);
            }
            else
            {
                rewardMissionProcess
                    .OnNetworkResponse(); //이전 User정보를 활용해 획득한 리턴값 goods를 생성해야 해서 goods가 생성된 이후 NetworkResponse 호출

                await EventManager.Instance.PacketEventPassGet((isBool) =>
                {
                    Model.SetEventPassOutDto(EventManager.Instance.EventPass.GroundPassOutDtoList[0]);
                    Model.SetAttendance(OnEventReqRewardMission);
                    Model.SetMission(OnEventReqRewardMission);
                    ShowUI(Model.Category);
                });
            }
        }
    }

    #endregion PacketEnd

    #region Tutorial
    public override async UniTask<GameObject> GetTutorialFocusObject(string stepKey)
    {
        TutorialExplain stepType = (TutorialExplain)Enum.Parse(typeof(TutorialExplain), stepKey);

        switch (stepType)
        {
            case TutorialExplain.GroundPassAttendanceButton:
                return View.GetTutorialAttendanceUnit(0).AttendanceRewardButton.gameObject;
        }

        return await base.GetTutorialFocusObject(stepKey);
    }

    public override async UniTask TutorialStepAsync(Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            case TutorialExplain.GroundPassAttendanceButton:
                {
                    var attendanceUnit = View.GetTutorialAttendanceUnit(0);
                    //(에디터 치트 등으로 테스트하는 경우)이미 보상을 받았다면 그냥 넘어감
                    if (attendanceUnit.Model.IsClaimed() || attendanceUnit.Model.IsNotOpened())
                        break;

                    attendanceUnit.AttendanceRewardButton.OnClcikButton();
                    await TutorialManager.WaitUntilRewardPopup();
                    break;
                }

            case TutorialExplain.GroundPassMissionTab:
                {
                    View.CategoryButton.indexValue = 1;
                    break;
                }
        }
    }
    #endregion Tutorial
    #endregion Coding rule : Function
}