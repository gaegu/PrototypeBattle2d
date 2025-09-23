
using Cysharp.Threading.Tasks;
using IronJade.Server.Web.Core;
using IronJade.LowLevel.Server.Web;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NetworkManager를 서비스로 래핑
/// ResponseEntity 구조 기반 처리
/// </summary>
public class NetworkServiceWrapper : INetworkService
{
    private NetworkManager networkManager => NetworkManager.Web;

    #region Web Process

    public BaseProcess GetWebProcess(WebProcess processType)
    {
        return networkManager?.GetProcess(processType);
    }

    public async UniTask<bool> RequestAsync(BaseProcess process)
    {
        if (process == null)
            return false;

        bool success = await process.OnNetworkAsyncRequest();

        // ResponseEntity 에러 체크
        if (success && process is IResponseProcess responseProcess)
        {
            var response = responseProcess.GetResponseEntity();
            if (response.IsError)
            {
                Debug.LogError($"[NetworkService] Request failed - Code: {response.code}, Message: {response.message}");
                return false;
            }
        }

        return success;
    }

    #endregion

    #region NetMining (Auto Battle)

    public async UniTask RequestNetMining()
    {
        if (!ContentsOpenManager.Instance.CheckContentsOpen(ContentsType.AutoBattle))
            return;

        BaseProcess autoBattleGetProcess = GetWebProcess(WebProcess.AutoBattleGet);

        if (await RequestAsync(autoBattleGetProcess))
        {
            autoBattleGetProcess.OnNetworkResponse();

            // ResponseEntity 처리
            if (autoBattleGetProcess is AutoBattleGetProcess typedProcess)
            {
                var response = typedProcess.GetResponse<AutoBattleGetResponse>();
                if (response != null && !response.entity.IsError)
                {
                    ProcessAutoBattleData(response.data);
                }
            }
        }
    }

    private void ProcessAutoBattleData(AutoBattleDto data)
    {
        if (data.Equals(default(AutoBattleDto)))
            return;

        Debug.Log($"[NetworkService] AutoBattle - Rewards: {data.fastRewardCount}");

        // 자동전투 데이터 처리
        if (data.fastRewardCount > 0)
        {
            // 보상 처리 로직
        }
    }

    #endregion

    #region Red Dot

    public async UniTask RequestReddot()
    {
        var tasks = new List<UniTask>
        {
            MailRedDot(),
            GuildRedDot(),
            DispatchRedDot()
        };

        await UniTask.WhenAll(tasks);
        RedDotManager.Instance.Notify();
    }

    public async UniTask MailRedDot()
    {
        if (!ContentsOpenManager.Instance.CheckContentsOpen(ContentsType.Mail))
            return;

        BaseProcess mailGetProcess = GetWebProcess(WebProcess.MailGet);
        mailGetProcess.SetLoading(false, false);

        if (await RequestAsync(mailGetProcess))
        {
            mailGetProcess.OnNetworkResponse();

            // ResponseEntity 기반 처리
            if (mailGetProcess is MailGetProcess typedProcess)
            {
                var response = typedProcess.GetResponse<MailGetResponse>();
                if (response != null && !response.entity.IsError)
                {
                    ProcessMailData(response.data);
                }
            }
        }
    }

    private void ProcessMailData(MailDto[] mails)
    {
        if (mails == null)
            return;

        int unreadCount = 0;
        foreach (var mail in mails)
        {
            if (!mail.isRead)
                unreadCount++;
        }

        RedDotManager.Instance.SetRedDot(RedDotType.Mail, unreadCount > 0, unreadCount);
    }

    public async UniTask GuildRedDot()
    {
        if (!PlayerManager.Instance.MyPlayer.User.GuildModel.IsJoinedGuild)
            return;

        int guildId = PlayerManager.Instance.MyPlayer.User.GuildModel.GuildId;
        BaseProcess memberGetProcess = GetWebProcess(WebProcess.GuildMemberGet);
        memberGetProcess.SetPacket(new GetGuildMemberInDto(guildId));
        memberGetProcess.SetLoading(false, false);

        if (await RequestAsync(memberGetProcess))
        {
            memberGetProcess.OnNetworkResponse();
        }

        // 권한이 있는 경우 가입 신청 확인
        if (!PlayerManager.Instance.MyPlayer.User.GuildModel.HasAuthority)
            return;

        BaseProcess signupGetProcess = GetWebProcess(WebProcess.GuildReceiveSignupGet);
        signupGetProcess.SetLoading(false, false);

        if (await RequestAsync(signupGetProcess))
        {
            signupGetProcess.OnNetworkResponse();

            if (signupGetProcess is GuildReceiveSignupGetProcess typedProcess)
            {
                var response = typedProcess.GetResponse<GuildReceiveSignupGetResponse>();
                if (response != null && !response.entity.IsError)
                {
                    bool hasNewSignups = response.data?.Length > 0;
                    PlayerManager.Instance.MyPlayer.User.GuildModel.SetNewContents(hasNewSignups);
                }
            }
        }
    }

    public async UniTask DispatchRedDot()
    {
        if (!ContentsOpenManager.Instance.CheckContentsOpen(ContentsType.Dispatch))
            return;

        BaseProcess dispatchGetProcess = GetWebProcess(WebProcess.DispatchGet);
        dispatchGetProcess.SetLoading(false, false);

        if (await RequestAsync(dispatchGetProcess))
        {
            dispatchGetProcess.OnNetworkResponse();
        }
    }

    #endregion

    #region Season

    public async UniTask RequestUpdateSeason(SeasonType seasonType)
    {
        BaseProcess seasonUpdateProcess = GetWebProcess(WebProcess.UserSeasonUpdate);
        seasonUpdateProcess.SetPacket(new UpdateUserSeasonInDto(seasonType));

        if (await RequestAsync(seasonUpdateProcess))
        {
            seasonUpdateProcess.OnNetworkResponse();

            if (seasonUpdateProcess is UserSeasonUpdateProcess typedProcess)
            {
                var response = typedProcess.GetResponse<UserSeasonUpdateResponse>();
                if (response != null && !response.entity.IsError)
                {
                    Debug.Log($"[NetworkService] Season updated to: {seasonType}");
                }
            }
        }
    }

    public async UniTask CheckAndUpdateSeason()
    {
        if (PlayerManager.Instance.MyPlayer.User.SeasonType != SeasonType.First)
        {
            await RequestUpdateSeason(SeasonType.First);
        }
    }

    #endregion

    #region Events

    public async UniTask CheckAndUpdateEvents()
    {
        // 10분 단위 체크
        if (!TimeManager.Instance.CheckTenMinuteDelayAPI())
            return;

        await EventManager.Instance.PacketEventPassGet();
        await UpdateActiveEvents();
    }

    private async UniTask UpdateActiveEvents()
    {
        BaseProcess eventListProcess = GetWebProcess(WebProcess.EventPassGet);

        if (await RequestAsync(eventListProcess))
        {
            eventListProcess.OnNetworkResponse();

            if (eventListProcess is EventPassGetProcess typedProcess)
            {
                var response = typedProcess.GetResponse<EventPassGetResponse>();
                if (response != null && !response.entity.IsError)
                {
                    ProcessEventData(response.data);
                }
            }
        }
    }

    private void ProcessEventData(GetEventPassOutDto[] events)
    {
        if (events == null)
            return;

        foreach (var eventData in events)
        {
            Debug.Log($"[NetworkService] Processing event: {eventData.id}");

            // 오늘 시작한 이벤트 체크
            if (IsEventStartedToday(eventData))
            {
                ShowNewEventNotification(eventData);
            }
        }
    }

    private bool IsEventStartedToday(GetEventPassOutDto eventData)
    {
        DateTime today = TimeManager.Instance.GetServerTime().Date;
        return eventData.startTime.Date == today;
    }

    private void ShowNewEventNotification(GetEventPassOutDto eventData)
    {
        string message = $"New Event: {eventData.title}";
        MessageBoxManager.ShowToastMessage(message, 3000);
    }

    #endregion

    #region Generic Request Methods

    /// <summary>
    /// ResponseEntity 기반 제네릭 요청 처리
    /// </summary>
    public async UniTask<TResponse> RequestWithResponse<TResponse>(WebProcess processType, IDto packet = null)
        where TResponse : class, IResponseData
    {
        BaseProcess process = GetWebProcess(processType);

        if (packet != null)
            process.SetPacket(packet);

        if (await RequestAsync(process))
        {
            process.OnNetworkResponse();

            // 타입별 응답 처리
            var response = process.GetResponse<ResponseWrapper<TResponse>>();
            if (response != null && !response.entity.IsError)
            {
                return response.data;
            }
        }

        return null;
    }

    /// <summary>
    /// 단순 요청 (응답 데이터 없음)
    /// </summary>
    public async UniTask<bool> RequestSimple(WebProcess processType, IDto packet = null)
    {
        BaseProcess process = GetWebProcess(processType);

        if (packet != null)
            process.SetPacket(packet);

        if (await RequestAsync(process))
        {
            process.OnNetworkResponse();
            return true;
        }

        return false;
    }

    /// <summary>
    /// PayLoad를 포함한 요청 처리
    /// </summary>
    public async UniTask<(bool success, PayLoad payLoad)> RequestWithPayLoad(WebProcess processType, IDto packet = null)
    {
        BaseProcess process = GetWebProcess(processType);

        if (packet != null)
            process.SetPacket(packet);

        if (await RequestAsync(process))
        {
            process.OnNetworkResponse();

            if (process is IPayLoadProcess payLoadProcess)
            {
                var entity = payLoadProcess.GetResponseEntity();
                if (!entity.IsError)
                {
                    return (true, entity.payLoad);
                }
            }
        }

        return (false, default);
    }

    #endregion
}


/// <summary>
/// 네트워크 응답 인터페이스
/// </summary>
public interface IResponseData
{
    // 마커 인터페이스
}

public interface IResponseProcess
{
    ResponseEntity GetResponseEntity();
}

public interface IPayLoadProcess : IResponseProcess
{
    // PayLoad를 포함한 프로세스
}

/// <summary>
/// 응답 래퍼 클래스
/// </summary>
public class ResponseWrapper<T> where T : class, IResponseData
{
    public ResponseEntity entity { get; set; }
    public T data { get; set; }
}