using UnityEngine;

/// Vegetacao, lama, mato alto: lugares onde voce se OCULTA.
///
/// Diferente de cobertura. Cobertura (as caixas) bloqueia a linha de visao —
/// ou ele te ve ou nao ve. Ocultacao nao bloqueia nada: voce continua no campo
/// de visao dele, so fica dificil de distinguir. Por isso o colisor e trigger:
/// o Linecast do guarda passa reto por aqui.
///
/// A consequencia de design e que ocultacao e um esconderijo NEGOCIAVEL —
/// funciona enquanto voce fica quieto, e te trai no instante em que voce corre.
[RequireComponent(typeof(Collider))]
public class ConcealmentVolume : MonoBehaviour
{
    [Tooltip("Quanto este volume esconde. 1 = mato ralo, quase nada. 0 = invisivel.")]
    [Range(0f, 1f)] public float strength = 0.35f;

    [Tooltip("Raio do ruido ao se mexer aqui dentro. Mato denuncia quem passa.")]
    public float rustleRadius = 5f;

    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    void OnDrawGizmos()
    {
        var c = GetComponent<Collider>();
        if (c == null) return;
        Gizmos.color = new Color(0.35f, 0.75f, 0.35f, 0.25f);
        Gizmos.DrawCube(c.bounds.center, c.bounds.size);
    }
}
