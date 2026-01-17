using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using UnityEngine;

public class RelayBootstrap : MonoBehaviour
{
    private string joinCode = "";
    private string status = "Not Initialized";
    private bool isInit = false;

    async void Start()
    {
        try
        {
            await UnityServices.InitializeAsync();
            if (this == null) return; // 🛑 防止退出播放模式后继续执行 (Zombie Task)

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                if (this == null) return;
            }
            
            status = "Connected. ID: " + AuthenticationService.Instance.PlayerId;
            isInit = true;

            if (NetworkManager.Singleton != null)
            {
                // 监听：有人连进来了吗？ 
                NetworkManager.Singleton.OnClientConnectedCallback += (clientId) => 
                { 
                    if (NetworkManager.Singleton.IsHost) 
                    { 
                        Debug.Log($"<color=green>[Host] 这里的房东：检测到新连接！Client ID: {clientId}</color>"); 
                    } 
                    else 
                    { 
                        Debug.Log($"<color=green>[Client] 这里的房客：我成功连上服务器了！我的 ID: {clientId}</color>"); 
                    } 
                }; 

                // 监听：连接断开了吗？ 
                NetworkManager.Singleton.OnClientDisconnectCallback += (clientId) => 
                { 
                    if (NetworkManager.Singleton.IsHost) 
                    { 
                        Debug.Log($"<color=red>[Host] 这里的房东：有个家伙断开了，ID: {clientId}</color>"); 
                    } 
                    else 
                    { 
                        // 如果我是客户端，收到了 Disconnect，说明我被踢了，或者网络炸了 
                        Debug.LogError($"<color=red>[Client] 这里的房客：我与服务器断开连接了！(原因可能是 404, 超时, 或协议不匹配)</color>"); 
                        
                        // 👇 这里是关键！打印出为什么断开 
                        if (NetworkManager.Singleton.DisconnectReason != string.Empty) 
                        { 
                            Debug.LogError($"[Client] 断开的具体原因: {NetworkManager.Singleton.DisconnectReason}"); 
                        } 
                    } 
                }; 
            }
        }
        catch (System.Exception e)
        {
            status = "Init Error: " + e.Message;
            Debug.LogError(e);
        }
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 400, 400));
        GUILayout.Label("Status: " + status);

        if (!isInit)
        {
            GUILayout.EndArea();
            return;
        }

        // If not connected to any network session
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUILayout.Button("Start Host (Create Relay)", GUILayout.Height(40)))
            {
                StartHost();
            }

            GUILayout.Space(10);
            GUILayout.Label("Join Code:");
            joinCode = GUILayout.TextField(joinCode, GUILayout.Height(30));

            if (GUILayout.Button("Join Client", GUILayout.Height(40)))
            {
                StartClient(joinCode);
            }
        }
        else if (NetworkManager.Singleton != null)
        {
            GUILayout.Label("Network Active");
            if (NetworkManager.Singleton.IsHost) 
            {
                GUILayout.TextField(joinCode); // Selectable text
                GUILayout.Label("(Share this code with client)");
            }
            
            if (GUILayout.Button("Disconnect", GUILayout.Height(40)))
            {
                NetworkManager.Singleton.Shutdown();
            }
        }
        GUILayout.EndArea();
    }

    private async void StartHost()
    {
        try
        {
            status = "Creating Allocation...";
            // Create allocation for 4 players
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(4);
            
            status = "Getting Join Code...";
            joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            
            status = "Starting Host...";
            
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            // 🟢 改为 "wss" (WebSocket Secure):
            var relayServerData = new RelayServerData(allocation, "wss"); 
            transport.SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartHost();
            status = "Host Running. Code: " + joinCode;
        }
        catch (System.Exception e)
        {
            status = "Host Failed: " + e.Message;
            Debug.LogError(e);
        }
    }

    private async void StartClient(string code)
    {
        if (string.IsNullOrEmpty(code)) return;

        try
        {
            status = "Joining Allocation...";
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(code);
            if (this == null) return;

            status = "Starting Client...";
            
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(new RelayServerData(joinAllocation, "wss"));

            NetworkManager.Singleton.StartClient();
            status = "Client Running";
        }
        catch (System.Exception e)
        {
            status = "Join Failed: " + e.Message;
            Debug.LogError(e);
        }
    }
}
