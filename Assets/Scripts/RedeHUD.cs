using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Parte 4/5 - Botões Start Host / Start Client / Shutdown na tela, úteis em builds
/// (no Editor, o próprio componente NetworkManager também mostra esses botões em Play Mode).
/// </summary>
public class RedeHUD : MonoBehaviour
{
    void OnGUI()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        GUILayout.BeginArea(new Rect(10, 10, 240, 200));

        if (!nm.IsClient && !nm.IsServer)
        {
            if (GUILayout.Button("Start Host", GUILayout.Height(40))) nm.StartHost();
            if (GUILayout.Button("Start Client", GUILayout.Height(40))) nm.StartClient();
        }
        else
        {
            string modo = nm.IsHost ? "Host" : nm.IsServer ? "Server" : "Client";
            GUILayout.Label($"Modo: {modo}");
            GUILayout.Label($"Conectados: {nm.ConnectedClientsIds.Count}");
            if (GUILayout.Button("Shutdown", GUILayout.Height(30))) nm.Shutdown();
        }

        GUILayout.EndArea();
    }
}
