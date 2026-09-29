using UnityEngine;

/// Lampião pendurado na mão: a alça fica na mão, o corpo do lampião fica em pé
/// e balança um pouco com o passo.
///
/// Antes ele era filho do osso da mão, com deslocamento zero: o lampião nascia
/// dentro da palma, girava junto com o punho e não aparecia. Lampião de alça não
/// segue o punho — ele pendura. Então a posição vem da mão e a rotação vem do
/// mundo (em pé, virado para onde o jogador olha).
[DisallowMultipleComponent]
public class SegurarLanterna : MonoBehaviour
{
    public Transform mao;
    public Transform lanterna;
    [Tooltip("Quanto balança, em graus, andando.")]
    public float balanco = 9f;

    float alturaDaAlca;
    Vector3 anterior;
    float fase;

    void Start()
    {
        if (mao == null || lanterna == null) { enabled = false; return; }

        // mede do pivô até o topo do lampião com ele em pé: é onde fica a alça
        lanterna.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        float topo = float.MinValue;
        foreach (var r in lanterna.GetComponentsInChildren<Renderer>()) topo = Mathf.Max(topo, r.bounds.max.y);
        alturaDaAlca = topo > float.MinValue ? topo - lanterna.position.y : 0.2f;
        anterior = transform.position;
    }

    void LateUpdate()
    {
        Vector3 d = transform.position - anterior; d.y = 0f;
        float v = Time.deltaTime > 0f ? d.magnitude / Time.deltaTime : 0f;
        anterior = transform.position;
        fase += Time.deltaTime * (2f + v * 2.2f);
        float amp = Mathf.Clamp01(v / 2f) * balanco;

        lanterna.rotation = Quaternion.Euler(Mathf.Sin(fase) * amp, transform.eulerAngles.y, Mathf.Cos(fase * 0.5f) * amp * 0.4f);
        lanterna.position = mao.position - lanterna.up * alturaDaAlca;
    }
}
