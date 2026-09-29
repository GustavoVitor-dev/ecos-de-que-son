using System.Collections.Generic;
using UnityEngine;

/// Abre uma clareira na copa em volta do jogador.
///
/// Duas tentativas erradas antes desta, e as duas ensinaram o formato do
/// problema. Primeiro tentei abrir um túnel fino entre a câmera e o jogador:
/// abriu 9 copas de 132 e a tela continuou uma parede de folha. Depois tirei a
/// copa inteira da câmera de cima: aí dava pra jogar, mas a selva virou uma
/// floresta de galho seco, porque de cima só sobrava tronco.
///
/// O que funciona é o meio: a copa existe, cobre a tela, e abre num raio em
/// volta do jogador. O jogador enxerga onde pisa e quem vem, a moldura da tela
/// continua sendo mata fechada, e a clareira anda junto com ele — que é o que
/// dá a sensação de estar dentro do mato, não olhando um mapa.
///
/// Só some a FOLHA. O tronco fica: é ele que diz onde tem cobertura.
[DisallowMultipleComponent]
public class OclusaoDeCopa : MonoBehaviour
{
    [Tooltip("Raio da clareira, em metros. A câmera mostra uns 16 x 9 m.")]
    public float raio = 6.5f;
    [Tooltip("Faixa em que a copa vai sumindo, pra não piscar de uma vez.")]
    public float margem = 1.5f;
    [Tooltip("Quantas vezes por segundo reavaliar.")]
    public float taxa = 10f;

    Transform jogador;
    readonly List<Transform> copas = new List<Transform>();
    readonly List<Renderer[]> malhas = new List<Renderer[]>();
    readonly List<bool> visivel = new List<bool>();
    float relogio;

    void Start()
    {
        var sol = FindFirstObjectByType<SoldierController>();
        if (sol != null) jogador = sol.transform;

        int copa = LayerMask.NameToLayer("Copa");
        var arvores = GameObject.Find("Nivel/Arvores");
        if (arvores == null || copa < 0) { enabled = false; return; }

        // agrupa por árvore: uma pachira tem várias malhas de folha e some junto
        foreach (Transform arvore in arvores.transform)
        {
            var lista = new List<Renderer>();
            foreach (var r in arvore.GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.layer == copa) lista.Add(r);

            if (lista.Count == 0) continue;
            copas.Add(arvore);
            malhas.Add(lista.ToArray());
            visivel.Add(true);
        }
    }

    void LateUpdate()
    {
        if (jogador == null || copas.Count == 0) return;

        relogio -= Time.deltaTime;
        if (relogio > 0f) return;
        relogio = 1f / Mathf.Max(1f, taxa);

        Vector3 p = jogador.position;
        float dentro = raio * raio;
        float fora = (raio + margem) * (raio + margem);

        for (int i = 0; i < copas.Count; i++)
        {
            Vector3 d = copas[i].position - p;
            d.y = 0f;
            float q = d.sqrMagnitude;

            // histerese: some passando do raio, só volta depois da margem.
            // Sem isso a copa pisca quando o jogador anda na borda exata.
            bool quer = visivel[i] ? q >= dentro : q >= fora;
            if (quer == visivel[i]) continue;

            visivel[i] = quer;
            foreach (var r in malhas[i]) if (r != null) r.enabled = quer;
        }
    }
}
