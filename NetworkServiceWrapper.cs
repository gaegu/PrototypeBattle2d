using Cysharp.Threading.Tasks;
using IronJade.LowLevel.Server.Web;
using IronJade.Server.Web.Core;
using IronJade.Server.Web.Management;
using UnityEngine;

public class NetworkServiceWrapper : INetworkService
{
    private WebServer webServer => NetworkManager.Web;

    public BaseProcess GetWebProcess(WebProcess processType)
    {
        return webServer?.GetProcess(processType);
    }

    public async UniTask<bool> RequestAsync(BaseProcess process)
    {
        if (process == null) return false;

        try
        {
            return await process.OnNetworkAsyncRequest();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NetworkService] Request failed: {e}");
            return false;
        }
    }

    public async UniTask RequestNetMining()
    {
        if (!ContentsOpenManager.Instance.CheckContentsOpen(ContentsType.AutoBattle))
            return;

        var process = GetWebProcess(WebProcess.AutoBattleGet);
        if (process != null)
        {
            process.SetLoading(false, false);
            if (await RequestAsync(process))
            {
                process.OnNetworkResponse();
            }
        }
    }

    public async UniTask RequestReddot()
    {
        var tasks = new UniTask[]
        {
            MailRedDot(),
            GuildRedDot(),
            DispatchRedDot()
        };

        await UniTask.WhenAll(tasks);
        RedDotManager.Instance?.Notify();
    }

    public async UniTask MailRedDot()
    {
        if (!ContentsOpenManager.Instance.CheckContentsOpen(ContentsType.Mail))
            return;

        var process = GetWebProcess(WebProcess.MailGet);
        if (process != null)
        {
            process.SetLoading(false, false);
            if (await RequestAsync(process))
            {
                process.OnNetworkResponse();
            }
        }
    }

    public async UniTask GuildRedDot()
    {
        if (!PlayerManager.Instance.MyPlayer.User.GuildModel.IsJoinedGuild)
            return;

        var process = GetWebProcess(WebProcess.GuildMemberGet);
        if (process != null)
        {
            process.SetLoading(false, false);
            if (await RequestAsync(process))
            {
                process.OnNetworkResponse();
            }
        }
    }

    public async UniTask DispatchRedDot()
    {
        if (!ContentsOpenManager.Instance.CheckContentsOpen(ContentsType.Dispatch))
            return;

        var process = GetWebProcess(WebProcess.DispatchGet);
        if (process != null)
        {
            process.SetLoading(false, false);
            if (await RequestAsync(process))
            {
                process.OnNetworkResponse();
            }
        }
    }

    public async UniTask RequestUpdateSeason(SeasonType seasonType)
    {
        var process = GetWebProcess(WebProcess.UserSeasonUpdate);
        if (process != null)
        {
            process.SetPacket(new UpdateUserSeasonInDto(seasonType));
            if (await RequestAsync(process))
            {
                process.OnNetworkResponse();
            }
        }
    }

    public async UniTask CheckAndUpdateEvents()
    {
        if (!TimeManager.Instance.CheckTenMinuteDelayAPI())
            return;

        await EventManager.Instance.PacketEventPassGet();
    }
}