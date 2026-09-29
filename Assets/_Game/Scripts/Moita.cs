using UnityEngine;

/// Uma moita de verdade: um aglomerado de plantas com uma zona de camuflagem
/// do tamanho dele.
///
/// Antes era o contrário — a cena tinha 52 caixas invisíveis de camuflagem
/// espalhadas à mão e a vegetação por cima nunca chegou a ser gerada. Dava
/// camuflagem no meio do nada, que é a pior coisa que um jogo furtivo pode
/// fazer: a regra do mundo fica invisível. Agora quem manda é a planta: se
/// tem moita na tela, esconde; se não tem, não esconde.
///
/// O componente também serve de alça pro resto do jogo: a folhagem
/// transparente ignora quem é moita (senão o mato onde você está some e te
/// entrega) e a faca usa o centro dela pra puxar o inimigo pra dentro.
[DisallowMultipleComponent]
public class Moita : MonoBehaviour
{
    [Tooltip("Raio da massa de folha, em metros.")]
    public float raio = 1.4f;
    [Tooltip("Zona de camuflagem (trigger) desta moita.")]
    public ConcealmentVolume zona;

    public Vector3 Centro => transform.position;

    /// A moita mais perto de um ponto, dentro de um limite.
    public static Moita MaisPerto(Vector3 p, float limite)
    {
        Moita melhor = null;
        float d = limite;
        foreach (var m in FindObjectsByType<Moita>(FindObjectsSortMode.None))
        {
            var v = m.transform.position - p; v.y = 0f;
            if (v.magnitude < d) { d = v.magnitude; melhor = m; }
        }
        return melhor;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.35f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.8f, raio);
    }
}
