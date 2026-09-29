using UnityEngine;
using UnityEngine.AI;

/// Liga a velocidade real de quem anda ao parâmetro do Animator.
///
/// Sem isto o soldado desliza pelo chão em pose parada — que é a mesma
/// ilegibilidade da cápsula, só que com braços. A reclamação de "movimentação
/// dos inimigos confusa" nasce exatamente daí: sem perna andando, o jogador não
/// tem como saber se aquilo está indo, parando ou virando.
///
/// Serve para guarda (NavMeshAgent) e para o jogador (CharacterController).
[DisallowMultipleComponent]
public class CorpoAnimado : MonoBehaviour
{
    [Tooltip("Acima desta velocidade o corpo entra na animação de andar.")]
    public float limiar = 0.15f;

    [Tooltip("A que velocidade a animação de andar foi feita, em m/s. Os clipes " +
             "do pacote são in-place (averageSpeed = 0), então não dá pra ler isso " +
             "do arquivo: 1,4 m/s é a estimativa para um ciclo de 2 s com duas " +
             "passadas. Se o pé patinar, ajustar aqui.")]
    public float velocidadeDaCaminhada = 1.4f;
    [Tooltip("Limites do ajuste, pra correria não virar desenho acelerado.")]
    public float ritmoMinimo = 0.7f;
    public float ritmoMaximo = 1.6f;
    [Tooltip("Desligar quando o controller já troca de clipe pela velocidade " +
             "(blend tree andar/correr): aí acelerar o clipe só atrapalha.")]
    public bool ajustarRitmo = true;

    static readonly int Agachado = Animator.StringToHash("Agachado");

    /// Ligado quando quem anda esta indo PRA TRAS (o guarda recuando sob tiro):
    /// o ciclo roda ao contrario, senao ele desliza de costas com as pernas
    /// indo pra frente.
    [System.NonSerialized] public bool DeCostas;

    Animator animator;
    NavMeshAgent agente;
    CharacterController controlador;
    SoldierController soldado;
    bool temAgachado;
    Vector3 anterior;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        agente = GetComponent<NavMeshAgent>();
        controlador = GetComponent<CharacterController>();
        soldado = GetComponent<SoldierController>();
        anterior = transform.position;
        if (animator != null)
            foreach (var p in animator.parameters)
                if (p.nameHash == Agachado && p.type == AnimatorControllerParameterType.Bool) temAgachado = true;
    }

    void Update()
    {
        if (animator == null) return;

        float v;
        if (agente != null && agente.isOnNavMesh) v = agente.velocity.magnitude;
        else if (controlador != null) v = controlador.velocity.magnitude;
        else
        {
            // último recurso: deriva a velocidade do próprio deslocamento
            Vector3 d = transform.position - anterior;
            d.y = 0f;
            v = Time.deltaTime > 0f ? d.magnitude / Time.deltaTime : 0f;
            anterior = transform.position;
        }

        // horizontal só: o CharacterController soma a gravidade na velocidade
        if (controlador != null && agente == null) { var h = controlador.velocity; h.y = 0f; v = h.magnitude; }

        animator.SetFloat("Velocidade", v);
        if (temAgachado && soldado != null) animator.SetBool(Agachado, soldado.IsCrouching);
        if (!ajustarRitmo) { animator.speed = (1f) * (DeCostas ? -1f : 1f); return; }

        // Sincroniza a passada com o quanto o corpo de fato anda. Sem isto o
        // guarda desliza: o clipe roda sempre no mesmo ritmo, não importa se ele
        // está a 1,5 m/s numa ronda ou a 3 m/s numa perseguição.
        animator.speed = (v > limiar
            ? Mathf.Clamp(v / Mathf.Max(0.1f, velocidadeDaCaminhada), ritmoMinimo, ritmoMaximo)
            : 1f) * (DeCostas ? -1f : 1f);
    }
}
