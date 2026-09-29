using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// Piloto automático: atravessa o bloco sozinho e anota o que os sistemas fizeram.
///
/// Não substitui playtest — ele não sente medo, não acha nada chato e não tem
/// opinião sobre ritmo. O que ele pega é a outra metade: beat que não dispara,
/// rota que trava, cena que não carrega, sala em loop que não fecha, entidade
/// que aparece onde não devia. Coisas que dão pra afirmar sem jogar, e que hoje
/// só apareciam depois de alguém perder dez minutos descobrindo.
///
/// Ele anda pela NavMesh, então segue o mesmo mundo que os guardas seguem.
public class AutoPlay : MonoBehaviour
{
    public enum Marcha { Agachado, Andando, Correndo }

    [Header("Como atravessar")]
    public Marcha marcha = Marcha.Andando;
    [Tooltip("Segundos antes de comecar, pra cutscene e transicao terminarem.")]
    public float esperaInicial = 1f;
    [Tooltip("Aborta o bloco depois disto, pra um travamento nao rodar pra sempre.")]
    public float limiteDeTempo = 240f;

    [Header("Chegada")]
    public float raioDeChegada = 1.4f;
    [Tooltip("Se ficar parado este tempo sem encostar no alvo, pula pro proximo.")]
    public float paciencia = 12f;

    SoldierController jogador;
    CharacterController cc;
    StressSystem estresse;

    float relogio;
    float ruidoTimer;
    readonly List<string> beatsVistos = new List<string>();

    void Start() => StartCoroutine(Rodar());

    IEnumerator Rodar()
    {
        jogador = FindFirstObjectByType<SoldierController>();
        if (jogador == null) { Debug.LogError("[AUTOPLAY] sem jogador na cena"); yield break; }

        cc = jogador.GetComponent<CharacterController>();
        estresse = StressSystem.Instance;

        // o controlador para de ler teclado; quem dirige daqui pra frente sou eu
        jogador.Travado = true;

        string bloco = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Debug.Log($"[AUTOPLAY] === {bloco} — marcha {marcha} — inicio");

        while (Transicao.Instance != null && Transicao.Instance.EmTransicao) yield return null;

        // esperar a cutscene tambem: Travado e um bool so, e quem trava por
        // ultimo ganha. Sem esta espera o piloto dirige por cima da abertura e
        // o teste mede um jogo que ninguem joga.
        var abertura = FindFirstObjectByType<Cutscene>();
        while (abertura != null && (abertura.Tocando || !abertura.Tocou)
               && Time.timeSinceLevelLoad < 40f) yield return null;

        yield return new WaitForSeconds(esperaInicial);

        var rota = Rota();
        Debug.Log($"[AUTOPLAY] {rota.Count} marcos na rota");

        float maiorDeteccao = 0f;
        int aparicoesNoInicio = Aparicoes();

        foreach (var alvo in rota)
        {
            float parado = 0f;
            Vector3 antes = jogador.transform.position;

            while (Plano(jogador.transform.position, alvo) > raioDeChegada)
            {
                if (relogio > limiteDeTempo)
                {
                    Debug.LogError($"[AUTOPLAY] {bloco}: estourou {limiteDeTempo}s. " +
                                   $"Travou indo para {alvo}, parado em {jogador.transform.position}.");
                    Relatorio(bloco, maiorDeteccao, aparicoesNoInicio);
                    yield break;
                }

                Passo(alvo);
                maiorDeteccao = Mathf.Max(maiorDeteccao, MaiorDeteccao());
                RegistrarBeats();

                relogio += Time.deltaTime;
                if ((jogador.transform.position - antes).sqrMagnitude < 0.0004f) parado += Time.deltaTime;
                else { parado = 0f; antes = jogador.transform.position; }

                if (parado > paciencia)
                {
                    Debug.LogWarning($"[AUTOPLAY] {bloco}: emperrado {paciencia:0}s em " +
                                     $"{jogador.transform.position} indo para {alvo}. Pulando este marco.");
                    break;
                }

                yield return null;
            }
        }

        Relatorio(bloco, maiorDeteccao, aparicoesNoInicio);
    }

    /// A rota é o próprio roteiro: os beats na ordem de z, mais a saída.
    /// Assim o piloto passa obrigatoriamente por tudo que foi autorado.
    List<Vector3> Rota()
    {
        var beats = new List<BeatTrigger>(FindObjectsByType<BeatTrigger>(FindObjectsSortMode.None));
        beats.RemoveAll(b => b.mode != BeatTrigger.Mode.AoEntrar);
        beats.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));

        var pontos = new List<Vector3>();
        foreach (var b in beats) pontos.Add(Ancorar(b.transform.position));

        // o objetivo pode estar fora do caminho: se existir, entra antes da saida
        var radio = GameObject.Find("Radio");
        var docs = GameObject.Find("Documentos");
        var objetivo = radio != null ? radio : docs;
        if (objetivo != null && pontos.Count > 1)
            pontos.Insert(Mathf.Max(0, pontos.Count - 1), Ancorar(objetivo.transform.position));

        return pontos;
    }

    static Vector3 Ancorar(Vector3 p)
        => NavMesh.SamplePosition(p, out NavMeshHit h, 8f, NavMesh.AllAreas) ? h.position : p;

    // segue o caminho da NavMesh em vez de ir reto: o piloto contorna geometria
    // igual aos guardas, senao ele encosta na parede e "trava" sem o nivel estar quebrado
    NavMeshPath caminho;
    int canto;
    Vector3 alvoDoCaminho;
    Vector3 ondeCalculei;
    float recalculo;

    void Passo(Vector3 destino)
    {
        // Ser pego teleporta o jogador de volta pro spawn. Sem refazer a rota, o
        // piloto continuava seguindo um caminho tracado de onde ele ESTAVA e ficava
        // empurrando parede pra sempre — 12 s de "emperrado" que nao eram culpa do
        // nivel. Refaz por tempo e sempre que a posicao pular de repente.
        recalculo -= Time.deltaTime;
        bool pulou = (jogador.transform.position - ondeCalculei).sqrMagnitude > 25f;

        if (caminho == null || recalculo <= 0f || pulou
            || (destino - alvoDoCaminho).sqrMagnitude > 0.25f)
        {
            caminho = new NavMeshPath();
            alvoDoCaminho = destino;
            canto = 1;
            recalculo = 0.5f;
            ondeCalculei = jogador.transform.position;
            NavMesh.CalculatePath(Ancorar(jogador.transform.position), destino, NavMesh.AllAreas, caminho);
        }
        else ondeCalculei = jogador.transform.position;

        Vector3 proximo = destino;
        if (caminho.corners.Length > 1)
        {
            if (canto >= caminho.corners.Length) canto = caminho.corners.Length - 1;
            proximo = caminho.corners[canto];
            if (Plano(jogador.transform.position, proximo) < 0.8f && canto < caminho.corners.Length - 1) canto++;
        }

        Vector3 dir = proximo - jogador.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            dir.Normalize();
            jogador.transform.rotation = Quaternion.LookRotation(dir);
        }

        float v = marcha == Marcha.Agachado ? jogador.crouchSpeed
                : marcha == Marcha.Correndo ? jogador.runSpeed
                                            : jogador.walkSpeed;

        cc.Move((dir * v + Vector3.down * 9.81f) * Time.deltaTime);

        // o mesmo ruido que o jogador faria nesta marcha: sem isto os cegos
        // dos tuneis nunca reagiriam e o teste mentiria sobre o bloco
        float raio = marcha == Marcha.Agachado ? jogador.crouchNoise
                   : marcha == Marcha.Correndo ? jogador.runNoise
                                               : jogador.walkNoise;

        ruidoTimer -= Time.deltaTime;
        if (raio > 0.01f && ruidoTimer <= 0f)
        {
            ruidoTimer = jogador.noiseInterval;
            Noise.Emit(jogador.transform.position, raio);
        }
    }

    static float Plano(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    float MaiorDeteccao()
    {
        float m = 0f;
        foreach (var g in FindObjectsByType<GuardAI>(FindObjectsSortMode.None))
            m = Mathf.Max(m, g.Detection);
        return m;
    }

    int Aparicoes()
    {
        var d = FindFirstObjectByType<EntityDirector>();
        return d != null ? d.Count : 0;
    }

    void RegistrarBeats()
    {
        foreach (var b in FindObjectsByType<BeatTrigger>(FindObjectsSortMode.None))
            if (b.Fired && !beatsVistos.Contains(b.gameObject.name))
            {
                beatsVistos.Add(b.gameObject.name);
                Debug.Log($"[AUTOPLAY] beat {b.gameObject.name} aos {relogio:0.0}s");
            }
    }

    void Relatorio(string bloco, float maiorDeteccao, int aparicoesNoInicio)
    {
        var loop = FindFirstObjectByType<LoopRoom>();
        var faca = FindFirstObjectByType<MeleeKill>();

        Debug.Log($"[AUTOPLAY] === {bloco}: {relogio:0.0}s de travessia");
        Debug.Log($"[AUTOPLAY]   beats disparados: {beatsVistos.Count} — {string.Join(", ", beatsVistos)}");
        Debug.Log($"[AUTOPLAY]   deteccao maxima: {maiorDeteccao:0.00}  " +
                  (maiorDeteccao >= 0.99f ? "(FOI PEGO)" : maiorDeteccao > 0.5f ? "(quase)" : "(passou limpo)"));
        Debug.Log($"[AUTOPLAY]   estresse final: {(estresse != null ? estresse.Value : 0f):0} " +
                  $"({(estresse != null ? estresse.Current.ToString() : "-")})");
        Debug.Log($"[AUTOPLAY]   aparicoes da entidade: {Aparicoes() - aparicoesNoInicio}");
        if (loop != null)
            Debug.Log($"[AUTOPLAY]   sala em loop: {loop.VoltasDadas}/{loop.voltas} voltas" +
                      (loop.Liberado ? " — saida liberada" : " — AINDA FECHADA"));
        if (faca != null) Debug.Log($"[AUTOPLAY]   mortes: {faca.Kills}");

        jogador.Travado = false;
    }
}
