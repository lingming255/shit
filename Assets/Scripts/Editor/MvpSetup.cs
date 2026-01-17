using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using System.Collections.Generic;

public class MvpSetup : EditorWindow
{
    [MenuItem("MVP/Setup Networking (Relay)")]
    public static void Setup()
    {
        // 1. Create NetworkManager
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

        // Ensure transport is assigned
        netManager.NetworkConfig.NetworkTransport = transport;

        // 2. Create RelayBootstrap
        GameObject bootGo = GameObject.Find("RelayBootstrap");
        if (bootGo == null)
        {
            bootGo = new GameObject("RelayBootstrap");
            Undo.RegisterCreatedObjectUndo(bootGo, "Create RelayBootstrap");
        }
        if (bootGo.GetComponent<RelayBootstrap>() == null) Undo.AddComponent<RelayBootstrap>(bootGo);

        // 3. Create Player Prefab
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        string prefabPath = "Assets/Resources/SimpleNetPlayer.prefab";
        GameObject playerTemp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        playerTemp.name = "SimpleNetPlayer";
        
        // Add Components
        if (playerTemp.GetComponent<NetworkObject>() == null) playerTemp.AddComponent<NetworkObject>();
        if (playerTemp.GetComponent<SimpleNetworkPlayer>() == null) playerTemp.AddComponent<SimpleNetworkPlayer>();
        if (playerTemp.GetComponent<NetworkTransform>() == null) playerTemp.AddComponent<NetworkTransform>();

        // Save Prefab
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(playerTemp, prefabPath);
        GameObject.DestroyImmediate(playerTemp);

        // 4. Configure NetworkManager (Using SerializedObject for reliability)
        SerializedObject so = new SerializedObject(netManager);
        SerializedProperty configProp = so.FindProperty("NetworkConfig");
        
        if (configProp != null)
        {
            // Set PlayerPrefab
            SerializedProperty playerPrefabProp = configProp.FindPropertyRelative("PlayerPrefab");
            if (playerPrefabProp != null)
            {
                playerPrefabProp.objectReferenceValue = prefab;
            }

            // Add to Prefab List
            SerializedProperty prefabsProp = configProp.FindPropertyRelative("Prefabs");
            if (prefabsProp != null)
            {
                SerializedProperty listProp = prefabsProp.FindPropertyRelative("m_Prefabs");
                if (listProp != null)
                {
                    listProp.ClearArray();
                    listProp.InsertArrayElementAtIndex(0);
                    SerializedProperty element = listProp.GetArrayElementAtIndex(0);
                    SerializedProperty prefabProp = element.FindPropertyRelative("Prefab");
                    if (prefabProp != null) prefabProp.objectReferenceValue = prefab;
                }
            }
        }
        
        so.ApplyModifiedProperties();

        Debug.Log("✅ MVP Networking Setup Complete! NetworkManager & RelayBootstrap created. Player Prefab assigned.");
        Selection.activeGameObject = nmGo;
    }
}
