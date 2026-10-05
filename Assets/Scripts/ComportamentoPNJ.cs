using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Parte 3 - Humano Virtual / PNJ autônomo.
/// Se o usuário se afastar mais de 'distanciaMaxima' metros, o PNJ caminha até ele (NavMesh).
/// Quando o usuário está perto, o PNJ para e se vira para ele.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class ComportamentoPNJ : MonoBehaviour
{
    public Transform alvoUsuario;            // Main Camera do XR Origin (preenchida automaticamente se vazia)
    public float distanciaMaxima = 3.0f;     // limite pedido no enunciado
    public float velocidadeGiro = 5f;

    private NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (alvoUsuario == null && Camera.main != null)
            alvoUsuario = Camera.main.transform;

        if (alvoUsuario == null)
            Debug.LogWarning("[ComportamentoPNJ] Arraste a Main Camera do XR Origin para 'alvoUsuario'.", this);

        if (!agent.isOnNavMesh)
            Debug.LogWarning("[ComportamentoPNJ] O agente não está sobre um NavMesh. Faça o Bake do NavMeshSurface.", this);
    }

    void Update()
    {
        if (alvoUsuario == null || !agent.isOnNavMesh) return;

        // Distância no plano horizontal (a câmera fica na altura da cabeça, o PNJ no chão)
        Vector3 delta = alvoUsuario.position - transform.position;
        delta.y = 0f;

        if (delta.magnitude > distanciaMaxima)
        {
            // Usuário se afastou: caminha até ele.
            // O destino é o ponto do NavMesh sob o usuário (a câmera está no ar, a ~1,6 m do chão).
            Vector3 sobOUsuario = alvoUsuario.position;
            sobOUsuario.y = transform.position.y - agent.baseOffset;   // altura do chão onde o PNJ está

            if (NavMesh.SamplePosition(sobOUsuario, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }
        else
        {
            // Usuário próximo: para e olha para ele
            if (agent.hasPath) agent.ResetPath();

            if (delta.sqrMagnitude > 0.001f)
            {
                Quaternion alvo = Quaternion.LookRotation(delta.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, alvo, Time.deltaTime * velocidadeGiro);
            }
        }
    }
}
