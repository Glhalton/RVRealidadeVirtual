using System.Collections;
using UnityEngine;

/// <summary>
/// Ao dar Play no Editor (simulador XR), ajusta o XR Origin para que os olhos do usuário
/// fiquem a 'alturaOlhos' metros acima do chão. Não faz nada em builds para headset.
/// </summary>
public class AlturaDoUsuario : MonoBehaviour
{
    public float alturaOlhos = 1.6f;
    public float alturaDoChao = 0f;

    IEnumerator Start()
    {
#if UNITY_EDITOR
        // espera o simulador posicionar a câmera
        for (int i = 0; i < 5; i++) yield return null;

        Camera cam = Camera.main;
        while (cam == null)
        {
            yield return null;
            cam = Camera.main;
        }

        float antes = cam.transform.position.y;
        float delta = (alturaDoChao + alturaOlhos) - antes;
        transform.position += Vector3.up * delta;

        Debug.Log($"[AlturaDoUsuario] olhos estavam a {antes:F2} m do mundo y=0; ajuste de {delta:F2} m; agora {cam.transform.position.y:F2} m.");
#else
        yield break;
#endif
    }
}
