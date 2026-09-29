using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// A terceira pergunta, e a que mais custou caro: DÁ PRA PASSAR?
//
// Cobertura em porcentagem diz QUANTO do mapa esta sob olho. Nao diz ONDE.
// A Selva marcava 12,4% — numero de bloco frouxo — e era intransponivel:
// uma sentinela cobria 12 m seguidos do unico corredor. O piloto automatico
// morreu 33 vezes em 90 s no mesmo ponto antes de alguem perceber.
//
// A diferenca que importa: RONDA tem janela, POSTO nao tem. Um patrulheiro
// cruzando a rota e um problema de tempo, que o jogador resolve esperando.
// Uma sentinela olhando pra dentro do corredor obrigatorio e uma parede.
//
// Regra adotada (T34): nenhum posto cobre mais de 6 m seguidos da rota
// obrigatoria, e nenhum posto olha pra dentro do corredor que o jogador
// precisa usar — sempre de traves, com entrada possivel pela borda oposta.
public static class GargaloCheck
{
    const float Passo = 0.5f;
    const float LimitePosto = 6f;
    // 16 m era chute meu e obrigava a distorcer o nivel: num acampamento de 24 m
    // de largura, qualquer ronda fica a menos de 8 m da rota em algum momento.
    // O que importa e a distincao posto x ronda — ronda varre e passa, e o
    // jogador espera. 24 m e o ponto em que uma ronda deixa de ter janela util.
    const float LimiteRonda = 24f;

    [MenuItem("Ecos/Medir gargalos da rota")]
    static void Rodar()
    {
        var jogador = Object.FindFirstObjectByType<SoldierController>();
        var cols = jogador != null ? jogador.GetComponentsInChildren<Collider>(true) : new Collider[0];
        foreach (var c in cols) c.enabled = false;

        try { Medir(); }
        finally { foreach (var c in cols) c.enabled = true; }
    }

    static void Medir()
    {
        Physics.SyncTransforms();

        var rota = RotaObrigatoria(out string descricao);
        if (rota.Count == 0) { Debug.LogWarning("[GARGALO] nao consegui montar a rota obrigatoria."); return; }

        Debug.Log($"[GARGALO] rota obrigatoria: {descricao} — {rota.Count * Passo:0} m");

        int paredes = 0;

        foreach (var g in Object.FindObjectsByType<GuardAI>(FindObjectsSortMode.None))
        {
            int dentro = 0, seguido = 0, maior = 0;

            foreach (var p in rota)
            {
                bool visto = g.sentinela ? VePosto(g, p) : VeRonda(g, p);
                if (visto) { dentro++; seguido++; maior = Mathf.Max(maior, seguido); }
                else seguido = 0;
            }

            float total = dentro * Passo, trecho = maior * Passo;
            float limite = g.sentinela ? LimitePosto : LimiteRonda;

            string veredito = "";
            if (trecho > limite)
            {
                paredes++;
                veredito = g.sentinela
                    ? $"   *** PAREDE: posto nao pode cobrir mais de {LimitePosto:0} m seguidos ***"
                    : $"   *** trecho longo demais para uma ronda ({LimiteRonda:0} m) ***";
            }

            Debug.Log($"[GARGALO] {g.gameObject.name}{(g.sentinela ? " [posto]" : "")}: " +
                      $"{total:0} m da rota, maior trecho seguido {trecho:0} m{veredito}");
        }

        if (paredes == 0)
            Debug.Log("[GARGALO] nenhum guarda tranca a rota obrigatoria.");
        else
            Debug.LogError($"[GARGALO] {paredes} guarda(s) trancam a rota. " +
                           "Tirar da linha do corredor, virar de traves ou encurtar o arco.");
    }

    static bool VePosto(GuardAI g, Vector3 alvo)
    {
        // varre o arco inteiro: o posto vai passar por todos esses angulos
        for (float a = -g.arcoDoPosto; a <= g.arcoDoPosto; a += 10f)
        {
            Vector3 frente = Quaternion.Euler(0f, g.direcaoDoPosto + a, 0f) * Vector3.forward;
            if (Ve(g.transform.position, frente, g.viewRange, g.viewAngle * 0.5f, alvo)) return true;
        }
        return false;
    }

    static bool VeRonda(GuardAI g, Vector3 alvo)
    {
        if (g.waypoints == null || g.waypoints.Length < 2) return false;

        for (int i = 0; i < g.waypoints.Length; i++)
        {
            if (g.waypoints[i] == null) continue;
            Vector3 a = g.waypoints[i].position;
            Vector3 b = g.waypoints[(i + 1) % g.waypoints.Length].position;
            Vector3 d = b - a; d.y = 0f;
            float comp = d.magnitude;
            if (comp < 0.1f) continue;
            d /= comp;

            for (float t = 0f; t < comp; t += 1.5f)
                if (Ve(a + d * t, d, g.viewRange, g.viewAngle * 0.5f, alvo)) return true;
        }
        return false;
    }

    static bool Ve(Vector3 pos, Vector3 frente, float alcance, float meio, Vector3 alvo)
    {
        Vector3 olho = pos + Vector3.up * 0.6f;
        Vector3 v = alvo - olho; v.y = 0f;
        if (v.magnitude > alcance || Vector3.Angle(frente, v) > meio) return false;
        return !Physics.Linecast(olho, alvo + Vector3.up * 0.9f, ~0, QueryTriggerInteraction.Ignore);
    }

    /// spawn -> objetivo -> saida, pelo caminho mais curto da NavMesh.
    /// E o que o jogador é OBRIGADO a percorrer; o resto do mapa é opcional.
    static List<Vector3> RotaObrigatoria(out string descricao)
    {
        var pontos = new List<Vector3>();
        descricao = "";

        var spawn = GameObject.Find("SpawnPoint");
        if (spawn == null) return pontos;

        var marcos = new List<Vector3> { spawn.transform.position };
        var nomes = new List<string> { "spawn" };

        var objetivo = GameObject.Find("Radio") ?? GameObject.Find("Documentos");
        if (objetivo != null) { marcos.Add(objetivo.transform.position); nomes.Add(objetivo.name); }

        BeatTrigger saida = null;
        foreach (var bt in Object.FindObjectsByType<BeatTrigger>(FindObjectsSortMode.None))
            foreach (var a in bt.actions)
                if (a.kind == BeatActionKind.CarregarCena) saida = bt;

        // sem beat de saida (bloco final), o alvo e a decisao
        if (saida != null) { marcos.Add(saida.transform.position); nomes.Add("saida"); }
        else
        {
            var fc = Object.FindFirstObjectByType<FinalChoice>();
            if (fc != null) { marcos.Add(fc.transform.position); nomes.Add("decisao"); }
        }

        for (int i = 0; i < marcos.Count - 1; i++)
        {
            NavMesh.SamplePosition(marcos[i], out NavMeshHit a, 8f, NavMesh.AllAreas);
            NavMesh.SamplePosition(marcos[i + 1], out NavMeshHit b, 8f, NavMesh.AllAreas);

            var caminho = new NavMeshPath();
            if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, caminho)
                || caminho.status != NavMeshPathStatus.PathComplete)
            {
                Debug.LogError($"[GARGALO] sem caminho de {nomes[i]} para {nomes[i + 1]}.");
                continue;
            }

            for (int c = 0; c < caminho.corners.Length - 1; c++)
            {
                Vector3 p0 = caminho.corners[c], p1 = caminho.corners[c + 1];
                float d = Vector3.Distance(p0, p1);
                for (float t = 0f; t < d; t += Passo) pontos.Add(Vector3.Lerp(p0, p1, t / d));
            }
        }

        descricao = string.Join(" -> ", nomes);
        return pontos;
    }
}
