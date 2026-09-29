using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// Piloto automático do capítulo: joga a corrente da Historia sozinho, com os
/// guardas ligados. Vai pela NavMesh até o próximo elo, aperta (ou segura) o
/// [E], espera o rádio terminar, e segue. Quando é pego, renasce no checkpoint
/// e continua — o que interessa é se a corrente FECHA com guarda no mapa e
/// onde ela machuca.
///
/// Não é playtest: não se esconde, não espera patrulha passar. É o pior
/// jogador furtivo possível — se ele termina, um jogador de verdade termina.
public class AutoHistoria : MonoBehaviour
{
    public AutoPlay.Marcha marcha = AutoPlay.Marcha.Agachado;
    public float limiteDeTempo = 900f;
    public float paciencia = 15f;

    SoldierController jogador;
    CharacterController cc;
    Historia hist;
    float relogio;
    int pegoAntes;
    readonly System.Text.StringBuilder log = new System.Text.StringBuilder();

    void Start() => StartCoroutine(Rodar());

    IEnumerator Rodar()
    {
        jogador = FindFirstObjectByType<SoldierController>();
        hist = FindFirstObjectByType<Historia>();
        if (jogador == null || hist == null) { Debug.LogError("[AUTOHIST] sem jogador ou sem Historia"); yield break; }
        cc = jogador.GetComponent<CharacterController>();
        Inspecao.Pular = true;   // o piloto não lê: pega e segue

        var abertura = FindFirstObjectByType<Cutscene>();
        while (abertura != null && (abertura.Tocando || !abertura.Tocou) && Time.timeSinceLevelLoad < 40f) yield return null;
        yield return new WaitForSeconds(1f);
        jogador.Travado = true;
        jogador.ForcarAgachado = marcha == AutoPlay.Marcha.Agachado;

        var dir = FindFirstObjectByType<PrototypeDirector>();
        pegoAntes = dir != null ? dir.caughtCount : 0;
        Registrar("inicio " + hist.Estado);

        Historia.Passo passoAntes = hist.Atual;
        float parado = 0f; Vector3 antes = jogador.transform.position;

        while (relogio < limiteDeTempo && hist.Atual != Historia.Passo.Saindo)
        {
            relogio += Time.deltaTime;
            if (hist.Atual != passoAntes) { Registrar("passo " + hist.Atual + " aos " + relogio.ToString("0") + "s"); passoAntes = hist.Atual; }

            if (dir != null && dir.caughtCount > pegoAntes)
            {
                pegoAntes = dir.caughtCount;
                Registrar("PEGO em " + jogador.transform.position.ToString("F0") + " no passo " + hist.Atual + " aos " + relogio.ToString("0") + "s");
                caminho = null;
            }

            if (!hist.ProximoElo(out Vector3 alvo, out string id, out bool segurar)) { yield return null; continue; }

            float d = Plano(jogador.transform.position, alvo);
            // o alcance do [E] daquele objeto (o sargento está cercado de
            // caixotes: exigir 1,3 m deixava o piloto se remexendo a 2,5 m)
            float raio = 1.3f;
            if (id != null)
                foreach (var it in Interagivel.Todos)
                    if (it.id == id) { raio = Mathf.Max(1.1f, it.raio - 0.25f); break; }

            // furtivo mínimo: guarda perto e virado pra cá = parar; detecção
            // subindo = recuar pro lado oposto. É o que um jogador faz.
            var perigo = Perigo(out GuardAI quem);
            if (perigo == 2) { Recuar(quem); esperas += Time.deltaTime; yield return null; continue; }
            if (perigo == 1) { DeLado(quem); esperas += Time.deltaTime; yield return null; continue; }

            if (d > raio)
            {
                Passo(Rota(alvo));
                if ((jogador.transform.position - antes).sqrMagnitude < 0.0004f) parado += Time.deltaTime; else { parado = 0f; antes = jogador.transform.position; }
                if (parado > paciencia) { Registrar("EMPERRADO em " + jogador.transform.position.ToString("F1") + " indo pra " + (id ?? "area") + " " + alvo.ToString("F1")); parado = 0f; caminho = null; }
            }
            else if (id != null && hist.Livre(id))
            {
                if (segurar) hist.BotSegurar(Time.deltaTime);
                else if (!hist.Ocupada || id == "portao") { hist.Testar(id); yield return new WaitForSeconds(0.5f); }
            }
            yield return null;
        }

        Registrar(hist.Atual == Historia.Passo.Saindo ? "TERMINOU em " + relogio.ToString("0") + "s" : "NAO TERMINOU: parou em " + hist.Estado);
        Registrar("pego " + (dir != null ? dir.caughtCount : 0) + "x, " + esperas.ToString("0") + "s parado esperando guarda");
        Debug.Log("[AUTOHIST]\n" + log);
        System.IO.File.WriteAllText("Temp/autohistoria.txt", log.ToString());
        jogador.Travado = false;
        jogador.ForcarAgachado = false;
    }

    float esperas;

    // 0 = livre, 1 = esperar, 2 = recuar
    int Perigo(out GuardAI quem)
    {
        quem = null; int pior = 0;
        foreach (var g in FindObjectsByType<GuardAI>(FindObjectsSortMode.None))
        {
            if (g.Detection > 0.03f) { quem = g; return 2; }
            var v = jogador.transform.position - g.transform.position; v.y = 0f;
            float dist = v.magnitude;
            if (dist > g.EffectiveRange + 2.5f) continue;
            float ang = Vector3.Angle(g.transform.forward, v);
            if (ang < g.viewAngle * 0.5f + 20f) { quem = g; pior = 1; }
        }
        return pior;
    }

    // Rota: ao sul do córrego com o alvo ao norte, vai pelo vau (oeste) — a
    // travessia sem guarda. O caminho mais curto passa pela ronda do Batedor
    // e o piloto ficava preso num ciclo de 40 s sendo pego no mesmo lugar.
    static readonly Vector3[] PeloVau = { new Vector3(-7f, 0f, -31f), new Vector3(-15f, 0f, -27f), new Vector3(-22f, 0f, -17f),
                                          new Vector3(-26f, 0f, -7f), new Vector3(-26.4f, 0f, -0.4f), new Vector3(-24f, 0f, 8f) };
    int noVau;

    Vector3 Rota(Vector3 alvo)
    {
        var p = jogador.transform.position;
        bool sul = p.z < FlorestaSelva_Corrego(p.x) - 1f, alvoNorte = alvo.z > FlorestaSelva_Corrego(alvo.x) + 1f;
        if (!(sul && alvoNorte)) { noVau = 0; return alvo; }
        while (noVau < PeloVau.Length && Plano(p, PeloVau[noVau]) < 1.6f) noVau++;
        return noVau < PeloVau.Length ? PeloVau[noVau] : alvo;
    }

    // mesma curva do córrego do construtor (FlorestaSelva.ZCorrego é de editor)
    static float FlorestaSelva_Corrego(float x) => -1.5f + 1.3f * Mathf.Sin(x * 0.16f + 0.7f) + 0.6f * Mathf.Sin(x * 0.41f);

    void DeLado(GuardAI g)
    {
        // sai da linha de visão: passo lateral pro lado mais longe do olhar dele
        var f = g.transform.forward; f.y = 0f;
        var v = jogador.transform.position - g.transform.position; v.y = 0f;
        var lado = Vector3.Cross(Vector3.up, f).normalized;
        if (Vector3.Dot(lado, v) < 0f) lado = -lado;
        Passo(jogador.transform.position + lado * 3f);
    }

    void Recuar(GuardAI g)
    {
        var fuga = jogador.transform.position - g.transform.position; fuga.y = 0f;
        if (fuga.sqrMagnitude < 0.01f) return;
        Passo(jogador.transform.position + fuga.normalized * 4f);
    }

    void Registrar(string s) { log.AppendLine(s); Debug.Log("[AUTOHIST] " + s); }

    // ---------------------------------------------------------------- andar pela NavMesh

    NavMeshPath caminho;
    int canto;
    Vector3 alvoDoCaminho, ondeCalculei;
    float recalculo, ruido;

    void Passo(Vector3 destino)
    {
        destino = Ancorar(destino);
        recalculo -= Time.deltaTime;
        bool pulou = (jogador.transform.position - ondeCalculei).sqrMagnitude > 25f;
        if (caminho == null || recalculo <= 0f || pulou || (destino - alvoDoCaminho).sqrMagnitude > 0.25f)
        {
            caminho = new NavMeshPath();
            alvoDoCaminho = destino; canto = 1; recalculo = 0.5f;
            NavMesh.CalculatePath(Ancorar(jogador.transform.position), destino, NavMesh.AllAreas, caminho);
        }
        ondeCalculei = jogador.transform.position;

        Vector3 proximo = destino;
        if (caminho.corners.Length > 1)
        {
            if (canto >= caminho.corners.Length) canto = caminho.corners.Length - 1;
            proximo = caminho.corners[canto];
            if (Plano(jogador.transform.position, proximo) < 0.8f && canto < caminho.corners.Length - 1) canto++;
        }
        Vector3 dir = proximo - jogador.transform.position; dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f) { dir.Normalize(); jogador.transform.rotation = Quaternion.LookRotation(dir); }

        float v = marcha == AutoPlay.Marcha.Agachado ? jogador.crouchSpeed : marcha == AutoPlay.Marcha.Correndo ? jogador.runSpeed : jogador.walkSpeed;
        cc.Move((dir * v + Vector3.down * 9.81f) * Time.deltaTime);

        float r = marcha == AutoPlay.Marcha.Agachado ? jogador.crouchNoise : marcha == AutoPlay.Marcha.Correndo ? jogador.runNoise : jogador.walkNoise;
        ruido -= Time.deltaTime;
        if (r > 0.01f && ruido <= 0f) { ruido = jogador.noiseInterval; Noise.Emit(jogador.transform.position, r); }
    }

    static Vector3 Ancorar(Vector3 p) => NavMesh.SamplePosition(p, out NavMeshHit h, 6f, NavMesh.AllAreas) ? h.position : p;
    static float Plano(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
}
