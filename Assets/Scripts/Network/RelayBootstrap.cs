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
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            status = "Connected. ID: " + AuthenticationService.Instance.PlayerId;
            isInit = true;
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
