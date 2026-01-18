using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEngine.UI;
using System.Collections.Generic;

public class MvpSetup : EditorWindow
{
    [MenuItem("MVP/Setup Full Game Loop")]
    public static void Setup()
    {
        SetupResourcesFolders();

        // 1. Core Network Setup
        var netManager = SetupNetworkManager();

        // 2. Prefabs Creation
        var playerPrefab = CreatePlayerPrefab();
        var debrisPrefab = CreateDebrisPrefab();
        var enemyPrefab = CreateEnemyPrefab();
        
        // 3. Scene Setup
        SetupEnemySpawner(enemyPrefab);
        SetupFractureManager(debrisPrefab);
        SetupUI();

        // 4. Register Prefabs
        RegisterNetworkPrefabs(netManager, playerPrefab, enemyPrefab);

        Debug.Log("✅ MVP Game Loop Setup Complete!");
    }

    private static void SetupResourcesFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
    }

    private static NetworkManager SetupNetworkManager()
    {
        GameObject nmGo = GameObject.Find("NetworkManager");
        if (nmGo == null)
        {
            nmGo = new GameObject("NetworkManager");
            Undo.RegisterCreatedObjectUndo(nmGo, "Create NetworkManager");
        }
        
        var netManager = nmGo.GetComponent<NetworkManager>();
        if (netManager == null) netManager = Undo.AddComponent<NetworkManager>(nmGo);
        
        var transport = nmGo.GetComponent<UnityTransport>();
        if (transport == null) transport = Undo.AddComponent<UnityTransport>(nmGo);

        netManager.NetworkConfig.NetworkTransport = transport;
        
        // Relay Bootstrap
        GameObject bootGo = GameObject.Find("RelayBootstrap");
        if (bootGo == null)
        {
            bootGo = new GameObject("RelayBootstrap");
            Undo.AddComponent<RelayBootstrap>(bootGo);
        }

        return netManager;
    }

    private static GameObject CreatePlayerPrefab()
    {
        string path = "Assets/Resources/SimpleNetPlayer.prefab";
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "SimpleNetPlayer";
        go.GetComponent<Renderer>().material.color = Color.green;

        if (go.GetComponent<NetworkObject>() == null) go.AddComponent<NetworkObject>();
        if (go.GetComponent<SimpleNetworkPlayer>() == null) go.AddComponent<SimpleNetworkPlayer>();
        if (go.GetComponent<NetworkTransform>() == null) go.AddComponent<NetworkTransform>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        GameObject.DestroyImmediate(go);
        return prefab;
    }

    private static GameObject CreateDebrisPrefab()
    {
        string path = "Assets/Resources/Debris.prefab";
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Debris";
        go.transform.localScale = Vector3.one * 0.3f;
        
        if (go.GetComponent<Rigidbody>() == null) go.AddComponent<Rigidbody>();
        
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        GameObject.DestroyImmediate(go);
        return prefab;
    }

    private static GameObject CreateEnemyPrefab()
    {
        string path = "Assets/Resources/Enemy.prefab";
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Enemy";
        
        if (go.GetComponent<NetworkObject>() == null) go.AddComponent<NetworkObject>();
        if (go.GetComponent<NetworkTransform>() == null) go.AddComponent<NetworkTransform>();
        if (go.GetComponent<Rigidbody>() == null) 
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true; // Controlled by transform in script, kinematic avoids physics jitter
            rb.useGravity = false;
        }
        if (go.GetComponent<Enemy>() == null) go.AddComponent<Enemy>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        GameObject.DestroyImmediate(go);
        return prefab;
    }

    private static void SetupEnemySpawner(GameObject enemyPrefab)
    {
        GameObject go = GameObject.Find("EnemySpawner");
        if (go == null) go = new GameObject("EnemySpawner");
        
        if (go.GetComponent<NetworkObject>() == null) go.AddComponent<NetworkObject>();
        
        var spawner = go.GetComponent<EnemySpawner>();
        if (spawner == null) spawner = go.AddComponent<EnemySpawner>();

        // Assign Prefab via SerializedObject
        SerializedObject so = new SerializedObject(spawner);
        SerializedProperty prop = so.FindProperty("enemyPrefab");
        if (prop != null)
        {
            prop.objectReferenceValue = enemyPrefab;
            so.ApplyModifiedProperties();
        }
    }

    private static void SetupFractureManager(GameObject debrisPrefab)
    {
        GameObject go = GameObject.Find("FractureManager");
        if (go == null) go = new GameObject("FractureManager");

        var fx = go.GetComponent<FractureEffect>();
        if (fx == null) fx = go.AddComponent<FractureEffect>();

        // Assign Debris Prefab
        SerializedObject so = new SerializedObject(fx);
        SerializedProperty prop = so.FindProperty("piecePrefab");
        if (prop != null)
        {
            prop.objectReferenceValue = debrisPrefab;
            so.ApplyModifiedProperties();
        }
        
        // Also assign static Prefab in script if possible, but instance is easier
        // The script uses singleton pattern in Awake
    }

    private static void SetupUI()
    {
        GameObject canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null)
        {
            canvasGo = new GameObject("Canvas");
            canvasGo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        if (canvasGo.GetComponent<PlayerUIManager>() == null) canvasGo.AddComponent<PlayerUIManager>();
        var uiManager = canvasGo.GetComponent<PlayerUIManager>();

        // Create Panel
        GameObject panel = GameObject.Find("DeathPanel");
        if (panel == null)
        {
            panel = new GameObject("DeathPanel");
            panel.transform.SetParent(canvasGo.transform, false);
            var img = panel.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.8f);
            
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            
            // Text
            GameObject text = new GameObject("Text");
            text.transform.SetParent(panel.transform, false);
            var t = text.AddComponent<Text>(); // Using legacy Text for simplicity, or TMP if user insisted
            t.text = "YOU DIED";
            t.color = Color.red;
            t.fontSize = 50;
            t.alignment = TextAnchor.MiddleCenter;
            t.rectTransform.sizeDelta = new Vector2(400, 100);
            t.rectTransform.anchoredPosition = new Vector2(0, 50);

            // Respawn Button
            GameObject btn = new GameObject("RespawnButton");
            btn.transform.SetParent(panel.transform, false);
            var bImg = btn.AddComponent<Image>();
            var btnComp = btn.AddComponent<Button>();
            btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -50);
            btn.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 40);
            
            GameObject btnText = new GameObject("Text");
            btnText.transform.SetParent(btn.transform, false);
            var bt = btnText.AddComponent<Text>();
            bt.text = "RESPAWN";
            bt.color = Color.black;
            bt.alignment = TextAnchor.MiddleCenter;
            ((RectTransform)bt.transform).anchorMin = Vector2.zero;
            ((RectTransform)bt.transform).anchorMax = Vector2.one;
            ((RectTransform)bt.transform).offsetMin = Vector2.zero;
            ((RectTransform)bt.transform).offsetMax = Vector2.zero;

            // Link to Manager
            SerializedObject so = new SerializedObject(uiManager);
            so.FindProperty("deathPanel").objectReferenceValue = panel;
            so.FindProperty("respawnButton").objectReferenceValue = btnComp;
            so.ApplyModifiedProperties();
        }
    }

    private static void RegisterNetworkPrefabs(NetworkManager netManager, GameObject playerPrefab, GameObject enemyPrefab)
    {
        SerializedObject so = new SerializedObject(netManager);
        SerializedProperty configProp = so.FindProperty("NetworkConfig");
        
        if (configProp != null)
        {
            // Set PlayerPrefab
            SerializedProperty playerPrefabProp = configProp.FindPropertyRelative("PlayerPrefab");
            if (playerPrefabProp != null) playerPrefabProp.objectReferenceValue = playerPrefab;

            // Add Prefabs
            SerializedProperty prefabsProp = configProp.FindPropertyRelative("Prefabs");
            if (prefabsProp != null)
            {
                SerializedProperty listProp = prefabsProp.FindPropertyRelative("m_Prefabs");
                if (listProp != null)
                {
                    listProp.ClearArray();
                    
                    // Add Enemy
                    listProp.InsertArrayElementAtIndex(0);
                    SerializedProperty p1 = listProp.GetArrayElementAtIndex(0);
                    p1.FindPropertyRelative("Prefab").objectReferenceValue = enemyPrefab;
                }
            }
        }
        so.ApplyModifiedProperties();
    }
}
