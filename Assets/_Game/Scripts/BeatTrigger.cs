using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// O que um beat pode fazer. Tudo aqui ja existe como sistema —
/// isto e so o controle remoto deles.
public enum BeatActionKind
{
    PicoDeEstresse,
    DefinirEstresse,
    ForcarLacuna,
    AparicaoEntidade,
    SilenciarAmbiente,
    AlertarGuardas,
    AtivarObjeto,
    DesativarObjeto,
    MensagemObjetivo,
    Registrar,
    CarregarCena,
    TocarCutscene
}

[System.Serializable]
public class BeatAction
{
    public BeatActionKind kind;
    [Tooltip("Segundos de espera ANTES desta acao. Encadeia sem precisar de varios triggers.")]
    public float delay;

    [Tooltip("Valor numerico: quanto de estresse, quantos segundos de silencio.")]
    public float value = 1f;

    public EntityPresence.Manifestation manifestation = EntityPresence.Manifestation.Periferica;
    public GameObject target;
    [TextArea] public string text;
}

/// Um beat do roteiro: quando o jogador passa por aqui (ou depois de N segundos),
/// acontece uma sequencia de coisas.
///
/// Existe pra o beat sheet dos 15 minutos poder ser AUTORADO na cena em vez de
/// escrito em codigo. Sem isso, mudar "a entidade aparece aos 1:40" para 2:10
/// seria recompilar; com isso, e arrastar um objeto.
[RequireComponent(typeof(Collider))]
public class BeatTrigger : MonoBehaviour
{
    public enum Mode { AoEntrar, AposTempo }

    [Header("Disparo")]
    public Mode mode = Mode.AoEntrar;
    [Tooltip("Segundos apos o inicio da cena, quando o modo e AposTempo.")]
    public float atTime = 10f;
    [Tooltip("Dispara uma vez so. Desligue para beats que podem repetir.")]
    public bool once = true;

    [Header("Condição")]
    [Tooltip("Só dispara se a missão já estiver na fase pedida. É o gate duro do bloco: " +
             "sem isto o beat de saída carrega a próxima cena mesmo com o objetivo intacto.")]
    public bool exigeFaseDaMissao;
    public MissionDirector.Phase faseExigida = MissionDirector.Phase.Extracao;
    [Tooltip("O que o jogador lê quando encosta no gate sem ter cumprido o objetivo.")]
    public string recadoSeBloqueado = "NAO VOU SAIR SEM O RADIO";

    [Header("Roteiro")]
    public List<BeatAction> actions = new List<BeatAction>();

    public bool Fired { get; private set; }

    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    void Update()
    {
        if (mode != Mode.AposTempo || Fired) return;
        if (Time.timeSinceLevelLoad >= atTime) Fire();
    }

    void OnTriggerEnter(Collider other)
    {
        if (mode != Mode.AoEntrar) return;
        if (other.GetComponentInParent<SoldierController>() == null) return;
        Fire();
    }

    public void Fire()
    {
        if (once && Fired) return;

        // Gate duro. O bloco inteiro era atravessável ignorando tudo porque a saída
        // carregava a próxima cena sem olhar o estado da missão: dava pra andar em
        // linha reta do spawn até o fim. Um objetivo que não tranca nada não é
        // objetivo, é decoração.
        if (exigeFaseDaMissao)
        {
            var m = MissionDirector.Instance;
            if (m == null || m.Current < faseExigida)
            {
                if (m != null && !string.IsNullOrEmpty(recadoSeBloqueado)) m.SetStatus(recadoSeBloqueado);
                return;
            }
        }

        Fired = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        foreach (var a in actions)
        {
            if (a.delay > 0f) yield return new WaitForSeconds(a.delay);
            Execute(a);
        }
    }

    void Execute(BeatAction a)
    {
        switch (a.kind)
        {
            case BeatActionKind.PicoDeEstresse:
                StressSystem.Instance?.Spike(a.value);
                break;

            case BeatActionKind.DefinirEstresse:
                if (StressSystem.Instance != null)
                {
                    StressSystem.Instance.ResetStress();
                    StressSystem.Instance.Spike(a.value);
                }
                break;

            case BeatActionKind.ForcarLacuna:
                FindFirstObjectByType<BlinkSystem>()?.ForceBlink();
                break;

            // com alvo, o beat escolhe o lugar: enquadramento de roteiro.
            // sem alvo, a entidade se vira sozinha como sempre.
            case BeatActionKind.AparicaoEntidade:
            {
                var dir = FindFirstObjectByType<EntityDirector>();
                if (dir != null) dir.Trigger(a.manifestation, a.target != null ? a.target.transform : null);
                break;
            }

            case BeatActionKind.SilenciarAmbiente:
                FindFirstObjectByType<Ambience>()?.Hush(a.value);
                break;

            case BeatActionKind.AlertarGuardas:
            {
                Vector3 ponto = a.target != null ? a.target.transform.position : transform.position;
                foreach (var g in FindObjectsByType<GuardAI>(FindObjectsSortMode.None))
                    g.AlertTo(ponto);
                break;
            }

            case BeatActionKind.AtivarObjeto:
                if (a.target != null) a.target.SetActive(true);
                break;

            case BeatActionKind.DesativarObjeto:
                if (a.target != null) a.target.SetActive(false);
                break;

            case BeatActionKind.MensagemObjetivo:
                MissionDirector.Instance?.SetStatus(a.text);
                break;

            case BeatActionKind.Registrar:
                Debug.Log($"[BEAT] {name}: {a.text}");
                break;

            // e o que costura os quatro blocos num jogo so.
            // O nome da cena vai no campo de texto.
            // O texto aceita "Cena|TITULO|legenda". Com a Transicao presente vai
            // com fade e cartela; sem ela, cai no load seco de antes pra nao
            // travar o jogo numa cena que ainda nao tenha o sistema montado.
            case BeatActionKind.CarregarCena:
                if (!string.IsNullOrEmpty(a.text))
                {
                    Debug.Log($"[BEAT] carregando cena: {a.text}");
                    if (Transicao.Instance != null) Transicao.Instance.Ir(a.text);
                    else UnityEngine.SceneManagement.SceneManager.LoadScene(a.text.Split('|')[0].Trim());
                }
                break;

            case BeatActionKind.TocarCutscene:
            {
                Cutscene cs = a.target != null ? a.target.GetComponent<Cutscene>() : null;
                if (cs == null) cs = FindFirstObjectByType<Cutscene>();
                if (cs != null) cs.Tocar();
                else Debug.Log($"[BEAT] {name}: nenhuma Cutscene encontrada");
                break;
            }
        }
    }

    void OnDrawGizmos()
    {
        var c = GetComponent<Collider>();
        if (c == null) return;

        Gizmos.color = Fired
            ? new Color(0.4f, 0.4f, 0.4f, 0.2f)
            : new Color(0.9f, 0.35f, 0.85f, 0.3f);
        Gizmos.DrawCube(c.bounds.center, c.bounds.size);
    }
}
