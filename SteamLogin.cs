#if UNITY_STANDALONE
using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

public class SteamLogin : IPlatformLogin
{
    private enum State
    {
        None,
        Succeeded,
        Failed,
    }


    public async UniTask<string> GetPlatformToken()
    {
        return await RequestSteamAuthTicket();
    }


    private State state = State.None;

    private Steamworks.HAuthTicket hAuthTicket = Steamworks.HAuthTicket.Invalid;
    private Steamworks.Callback<Steamworks.GetAuthSessionTicketResponse_t> ticketResponse;
    private Steamworks.SteamNetworkingIdentity identity;
    private byte[] ticketBuffer = new byte[1024];
    private uint ticketSize = 0;
    private ulong steamId64;
    private string hexTicket;

    private async UniTask<string> RequestSteamAuthTicket()
    {
        if (!SteamManager.Initialized)
        {
            IronJade.Debug.LogError("[Steam] SteamManagager Initialize Failed");
            return string.Empty;
        }

        identity = new Steamworks.SteamNetworkingIdentity();
        identity.SetGenericString(StringDefine.STRING_FORMAT_STEAM_IDENTITY);

        hAuthTicket = Steamworks.SteamUser.GetAuthSessionTicket(
                       ticketBuffer, ticketBuffer.Length,
                       out ticketSize,
                       ref identity);

        ticketResponse = Steamworks.Callback<Steamworks.GetAuthSessionTicketResponse_t>
                                .Create(OnAuthSessionTicket);

        await UniTask.WaitUntil(() => { return state != State.None; });

        if (state == State.Succeeded)
            return hexTicket;

        return string.Empty;
    }

    private void OnAuthSessionTicket(Steamworks.GetAuthSessionTicketResponse_t cb)
    {
        try
        {
            if (cb.m_hAuthTicket != hAuthTicket || cb.m_eResult != Steamworks.EResult.k_EResultOK)
            {
               IronJade.Debug.LogError($"[Steam] OnAuthSessionTicket Failed : {cb.m_eResult}");
                state = State.Failed;
                return;
            }

            var trimmed = new byte[ticketSize];
            Buffer.BlockCopy(ticketBuffer, 0, trimmed, 0, (int)ticketSize);
            hexTicket = BitConverter.ToString(trimmed).Replace("-", "");
            steamId64 = Steamworks.SteamUser.GetSteamID().m_SteamID;

            identity.ToString(out string identityStr);

            IronJade.Debug.Log($"[Steam] SteamID64={steamId64}, hexTicket={hexTicket}, ticketLen={ticketSize}, identity={identityStr}");

            state = State.Succeeded;
        }
        catch (Exception e)
        {
           IronJade.Debug.LogError($"[Steam] OnAuthSessionTicket Failed : {e}");
            state = State.Failed;
        }
    }
}
#endif
