using UnityEngine;
using TMPro;
using UnityEngine.UI;

/* 📋 LOGIC MEMO: PlayerUIManager
--------------------------------------------------
1. Core: Manage local player UI (Death Screen).
2. Singleton: Local instance for the player to find.
--------------------------------------------------
*/
public class PlayerUIManager : MonoBehaviour
{
    public static PlayerUIManager Instance;

    [SerializeField] private GameObject deathPanel;
    [SerializeField] private Button respawnButton;

    private void Awake()
    {
        Instance = this;
        deathPanel.SetActive(false);
        respawnButton.onClick.AddListener(OnRespawnClicked);
    }

    public void ShowDeathScreen(bool show)
    {
        deathPanel.SetActive(show);
    }

    private void OnRespawnClicked()
    {
        // Find local player and call Respawn
        Debug.Log("[PlayerUIManager] Respawn button clicked.");
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogError("[PlayerUIManager] NetworkManager.Singleton 为 null！");
            return;
        }
        if (nm.LocalClient == null)
        {
            Debug.LogError("[PlayerUIManager] LocalClient 为 null！");
            return;
        }
        if (nm.LocalClient.PlayerObject == null)
        {
            Debug.LogError("[PlayerUIManager] PlayerObject 为 null！");
            return;
        }
        var player = nm.LocalClient.PlayerObject.GetComponent<SimpleNetworkPlayer>();
        if (player != null)
        {
            Debug.Log("[PlayerUIManager] 成功获取 SimpleNetworkPlayer，调用 RequestRespawn。");
            player.RequestRespawn();
        }
        else
        {
            Debug.LogError("[PlayerUIManager] 未在 PlayerObject 上找到 SimpleNetworkPlayer！");
        }
    }
}
