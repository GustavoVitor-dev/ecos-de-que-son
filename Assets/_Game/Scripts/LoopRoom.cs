using UnityEngine;

/// Uma volta da sala em loop: o que muda desta passagem para a proxima.
[System.Serializable]
public class LoopVariation
{
    [Tooltip("So para o designer lembrar do que se trata.")]
    public string nota;
    public GameObject[] ligar;
    public GameObject[] desligar;
    public bool manifestarEntidade;
    public EntityPresence.Manifestation manifestacao = EntityPresence.Manifestation.EntreEles;
    public float estresse;
    [Tooltip("Segundos de silencio ao completar esta volta.")]
    public float silencio;
}

/// A sala que se repete.
///
/// Sair pelo norte devolve voce a entrada sul, com o lugar levemente diferente.
/// Uma luz que morreu, marcas de trilha que mudaram de lugar, ela parada num canto.
///
/// O ponto e QUANDO a troca acontece: durante a lacuna de percepcao. E a mesma
/// regra da entidade aplicada ao proprio nivel — voce nunca ve o mundo mudar,
/// so constata que mudou. Sem isso seria um teleporte; com isso e uma duvida.
///
/// Custa uma sala e algumas variacoes, e e o efeito mais barato de horror que existe.
public class LoopRoom : MonoBehaviour
{
    [Header("Estrutura")]
    [Tooltip("Para onde o jogador volta ao completar uma volta.")]
    public Transform entrada;
    [Tooltip("Quantas voltas antes da saida se abrir de verdade.")]
    public int voltas = 3;

    [Header("O que muda a cada volta")]
    public LoopVariation[] variacoes;

    public int VoltasDadas { get; private set; }
    public bool Liberado => VoltasDadas >= voltas;

    BlinkSystem blink;
    SoldierController jogador;
    float travar;
    bool pendente;

    void Start()
    {
        blink = FindFirstObjectByType<BlinkSystem>();
        jogador = FindFirstObjectByType<SoldierController>();
        if (blink != null) blink.Blinked += NaLacuna;
    }

    void OnDestroy()
    {
        if (blink != null) blink.Blinked -= NaLacuna;
    }

    void Update()
    {
        if (travar > 0f) travar -= Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (Liberado || pendente || travar > 0f) return;
        if (other.GetComponentInParent<SoldierController>() == null) return;

        pendente = true;
        if (blink != null) blink.ForceBlink();
        else NaLacuna();                  // sem sistema de lacuna, ao menos nao trava
    }

    /// Tudo acontece aqui: com a tela preta, o jogador volta e a sala muda.
    void NaLacuna()
    {
        if (!pendente) return;
        pendente = false;
        travar = 1.2f;

        if (jogador != null && entrada != null)
        {
            var cc = jogador.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            jogador.transform.SetPositionAndRotation(entrada.position, entrada.rotation);
            if (cc != null) cc.enabled = true;
        }

        Aplicar(VoltasDadas);
        VoltasDadas++;

        Debug.Log($"[LOOP] volta {VoltasDadas} de {voltas}" + (Liberado ? " — saida liberada" : ""));

        if (Liberado && MissionDirector.Instance != null)
            MissionDirector.Instance.SetStatus("A PASSAGEM ESTA ABERTA AGORA.");
    }

    void Aplicar(int indice)
    {
        if (variacoes == null || indice < 0 || indice >= variacoes.Length) return;
        var v = variacoes[indice];

        if (v.desligar != null)
            foreach (var g in v.desligar) if (g != null) g.SetActive(false);
        if (v.ligar != null)
            foreach (var g in v.ligar) if (g != null) g.SetActive(true);

        if (v.estresse > 0f && StressSystem.Instance != null)
            StressSystem.Instance.Spike(v.estresse);

        if (v.silencio > 0f)
        {
            var amb = FindFirstObjectByType<Ambience>();
            if (amb != null) amb.Hush(v.silencio);
        }

        if (v.manifestarEntidade)
        {
            var dir = FindFirstObjectByType<EntityDirector>();
            if (dir != null) dir.Trigger(v.manifestacao);
        }
    }

    void OnDrawGizmos()
    {
        var c = GetComponent<Collider>();
        if (c == null) return;
        Gizmos.color = Liberado ? new Color(0.3f, 1f, 0.5f, 0.3f) : new Color(1f, 0.4f, 0.9f, 0.3f);
        Gizmos.DrawCube(c.bounds.center, c.bounds.size);
        if (entrada != null)
        {
            Gizmos.color = new Color(1f, 0.4f, 0.9f, 0.8f);
            Gizmos.DrawLine(c.bounds.center, entrada.position);
        }
    }
}
