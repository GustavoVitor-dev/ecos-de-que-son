using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Duas vezes um bloco foi entregue impossivel de atravessar: a rampa do mirante
// e as junções dos tuneis, que nasceram seladas porque cada segmento foi
// construido como uma caixa fechada e a parede de topo de um cortava o interior
// do seguinte. Nos dois casos a geometria estava certa no papel e o jogador
// batia numa parede.
//
// Isto e o teste que faltava: um flood fill com a capsula do jogador, partindo
// do SpawnPoint, dizendo o que da e o que nao da pra alcancar. Roda em 2 s e
// vem ANTES de qualquer playtest. Menu: Ecos > Testar travessia da cena aberta.
public static class TravessiaCheck
{
    const float Passo = 0.5f;
    // 45 graus de rampa em meio metro da 0,5 de subida. degrau baixo passa junto.
    const float DesnivelMax = 0.55f;
    // metade da grade, em metros. Era 90: o labirinto dos tuneis tem 200 m e o
    // spawn fica em z=-94, fora da grade — o flood morria na primeira celula e
    // acusava os 6 marcos como inacessiveis sem o mapa ter nada de errado.
    const float Alcance = 160f;

    [MenuItem("Ecos/Testar travessia da cena aberta")]
    static void Rodar()
    {
        // o proprio jogador fica parado no spawn dentro do editor, e o raycast
        // de cima batia no topo da capsula dele: o chao do spawn virava 1,9 m e
        // o flood nao conseguia sair da primeira celula. Ele nao e cenario.
        Collider[] doJogador = System.Array.Empty<Collider>();
        var jogador = Object.FindFirstObjectByType<SoldierController>();
        if (jogador != null) doJogador = jogador.GetComponentsInChildren<Collider>(true);
        foreach (var c in doJogador) c.enabled = false;

        try { Medir(); }
        finally { foreach (var c in doJogador) c.enabled = true; }
    }

    static void Medir()
    {
        Physics.SyncTransforms();

        Vector3 origem;
        var spawn = GameObject.Find("SpawnPoint");
        if (spawn != null) origem = spawn.transform.position;
        else
        {
            var sol = Object.FindFirstObjectByType<SoldierController>();
            if (sol == null) { Debug.LogError("[TRAVESSIA] sem SpawnPoint e sem jogador na cena"); return; }
            origem = sol.transform.position;
        }

        var cc = Object.FindFirstObjectByType<CharacterController>();
        float raio = cc != null ? cc.radius : 0.5f;
        float altura = cc != null ? cc.height : 2f;

        var chao = new Dictionary<long, float>();
        var visto = new HashSet<long>();

        int sx = Mathf.RoundToInt(origem.x / Passo);
        int sz = Mathf.RoundToInt(origem.z / Passo);

        if (!Pisavel(sx, sz, raio, altura, chao))
        {
            Debug.LogError($"[TRAVESSIA] o proprio spawn {origem} nao e pisavel. " +
                           "Ou esta dentro de geometria, ou nao ha chao embaixo.");
            return;
        }

        var fila = new Queue<(int, int)>();
        fila.Enqueue((sx, sz));
        visto.Add(Chave(sx, sz));

        int[] dx = { 1, -1, 0, 0 };
        int[] dz = { 0, 0, 1, -1 };
        int limite = Mathf.RoundToInt(Alcance / Passo);

        while (fila.Count > 0 && visto.Count < 200000)
        {
            var (ix, iz) = fila.Dequeue();
            float yAqui = chao[Chave(ix, iz)];

            for (int k = 0; k < 4; k++)
            {
                int nx = ix + dx[k], nz = iz + dz[k];
                if (Mathf.Abs(nx) > limite || Mathf.Abs(nz) > limite) continue;

                long c = Chave(nx, nz);
                if (visto.Contains(c)) continue;
                if (!Pisavel(nx, nz, raio, altura, chao)) continue;
                // degrau alto demais nao se sobe: e o que separa rampa de parede
                if (Mathf.Abs(chao[c] - yAqui) > DesnivelMax) continue;

                visto.Add(c);
                fila.Enqueue((nx, nz));
            }
        }

        Debug.Log($"[TRAVESSIA] {visto.Count} celulas alcancaveis a partir de {origem} " +
                  $"({visto.Count * Passo * Passo:0} m2 de piso).");

        int ok = 0, falta = 0;

        void Conferir(string nome, Vector3 p)
        {
            int ax = Mathf.RoundToInt(p.x / Passo), az = Mathf.RoundToInt(p.z / Passo);
            bool alcanca = visto.Contains(Chave(ax, az));

            // um marco pode ter o centro dentro de parede; aceita se a vizinhanca alcanca
            if (!alcanca)
                for (int a = -3; a <= 3 && !alcanca; a++)
                    for (int b = -3; b <= 3 && !alcanca; b++)
                        alcanca = visto.Contains(Chave(ax + a, az + b));

            if (alcanca) ok++;
            else { falta++; Debug.LogError($"[TRAVESSIA] NAO ALCANCA: {nome} em {p}"); }
        }

        foreach (var bt in Object.FindObjectsByType<BeatTrigger>(FindObjectsSortMode.None))
            Conferir("beat " + bt.gameObject.name, bt.transform.position);

        foreach (var mz in Object.FindObjectsByType<MissionZone>(FindObjectsSortMode.None))
            Conferir("objetivo " + mz.gameObject.name, mz.transform.position);

        var fc = Object.FindFirstObjectByType<FinalChoice>();
        if (fc != null) Conferir("decisao final", fc.transform.position);

        // a corrente do capítulo: todo elo tem que ser alcançável a pé...
        foreach (var it in Object.FindObjectsByType<Interagivel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Conferir("elo " + it.id, it.transform.position);
        foreach (var g in Object.FindObjectsByType<GatilhoHistoria>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (g.id == "saida")
            {
                // ...menos a saída: com o portão fechado ela TEM que estar fora.
                // Se alcança, o portão não tranca nada e a corrente é opcional.
                int saidaX = Mathf.RoundToInt(g.transform.position.x / Passo), saidaZ = Mathf.RoundToInt(g.transform.position.z / Passo);
                if (visto.Contains(Chave(saidaX, saidaZ))) { falta++; Debug.LogError("[TRAVESSIA] a SAIDA e alcancavel com o portao fechado: o portao nao tranca"); }
                else { ok++; Debug.Log("[TRAVESSIA] saida fora de alcance com o portao fechado (certo)"); }
                continue;
            }
            Conferir("gatilho " + g.id, g.transform.position);
        }

        if (falta == 0) Debug.Log($"[TRAVESSIA] os {ok} marcos da cena sao alcancaveis a pe.");
        else Debug.LogError($"[TRAVESSIA] {falta} de {ok + falta} marcos estao inacessiveis. " +
                            "A cena nao esta jogavel — nao vale a pena playtest ainda.");
    }

    static long Chave(int a, int b) => (long)(a + 1000) * 100000 + (b + 1000);

    /// Onde o jogador PODE estar, andando a partir do spawn.
    ///
    /// Publico porque a medicao de vigilancia precisa exatamente da mesma
    /// definicao: medir cobertura sobre o mapa inteiro conta a floresta em volta,
    /// onde ninguem patrulha e ninguem passa, e o percentual vira ficcao.
    /// Devolve o centro de cada celula alcancavel, ja na altura do peito.
    public static List<Vector3> AreaJogavel(float passo)
    {
        var saida = new List<Vector3>();

        Vector3 origem;
        var spawn = GameObject.Find("SpawnPoint");
        if (spawn != null) origem = spawn.transform.position;
        else
        {
            var sol = Object.FindFirstObjectByType<SoldierController>();
            if (sol == null) return saida;
            origem = sol.transform.position;
        }

        var cc = Object.FindFirstObjectByType<CharacterController>();
        float raio = cc != null ? cc.radius : 0.5f;
        float altura = cc != null ? cc.height : 2f;

        var chao = new Dictionary<long, float>();
        var visto = new HashSet<long>();

        bool Livre(int ix, int iz)
        {
            long c = Chave(ix, iz);
            if (chao.TryGetValue(c, out float v)) return !float.IsNaN(v);

            Vector3 col = new Vector3(ix * passo, 0f, iz * passo);
            if (!Physics.Raycast(col + Vector3.up * 40f, Vector3.down, out RaycastHit h, 80f, ~0, QueryTriggerInteraction.Ignore))
            { chao[c] = float.NaN; return false; }

            Vector3 p = new Vector3(col.x, h.point.y, col.z);
            bool ok = !Physics.CheckCapsule(p + Vector3.up * (raio + 0.1f),
                                            p + Vector3.up * (altura - raio + 0.1f),
                                            raio * 0.9f, ~0, QueryTriggerInteraction.Ignore);
            chao[c] = ok ? h.point.y : float.NaN;
            return ok;
        }

        int sx = Mathf.RoundToInt(origem.x / passo), sz = Mathf.RoundToInt(origem.z / passo);
        if (!Livre(sx, sz)) return saida;

        var fila = new Queue<(int, int)>();
        fila.Enqueue((sx, sz)); visto.Add(Chave(sx, sz));

        int[] dx = { 1, -1, 0, 0 }, dz = { 0, 0, 1, -1 };
        int limite = Mathf.RoundToInt(Alcance / passo);

        while (fila.Count > 0 && visto.Count < 200000)
        {
            var (ix, iz) = fila.Dequeue();
            float ya = chao[Chave(ix, iz)];
            for (int k = 0; k < 4; k++)
            {
                int nx = ix + dx[k], nz = iz + dz[k];
                if (Mathf.Abs(nx) > limite || Mathf.Abs(nz) > limite) continue;
                long c = Chave(nx, nz);
                if (visto.Contains(c) || !Livre(nx, nz)) continue;
                if (Mathf.Abs(chao[c] - ya) > DesnivelMax) continue;
                visto.Add(c); fila.Enqueue((nx, nz));
            }
        }

        foreach (var c in visto)
        {
            int ix = (int)(c / 100000) - 1000, iz = (int)(c % 100000) - 1000;
            saida.Add(new Vector3(ix * passo, chao[c] + 0.9f, iz * passo));
        }
        return saida;
    }

    static bool Pisavel(int ix, int iz, float raio, float altura, Dictionary<long, float> chao)
    {
        long c = Chave(ix, iz);
        if (chao.TryGetValue(c, out float cache)) return !float.IsNaN(cache);

        Vector3 col = new Vector3(ix * Passo, 0f, iz * Passo);

        if (!Physics.Raycast(col + Vector3.up * 40f, Vector3.down, out RaycastHit hit,
                             80f, ~0, QueryTriggerInteraction.Ignore))
        { chao[c] = float.NaN; return false; }

        float y = hit.point.y;
        Vector3 p = new Vector3(col.x, y, col.z);

        bool livre = !Physics.CheckCapsule(p + Vector3.up * (raio + 0.1f),
                                           p + Vector3.up * (altura - raio + 0.1f),
                                           raio * 0.9f, ~0, QueryTriggerInteraction.Ignore);

        chao[c] = livre ? y : float.NaN;
        return livre;
    }
}
