using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// A pergunta que decide se um bloco de stealth e tenso ou decorativo:
// QUANTO DO MAPA ESTA SENDO OLHADO AGORA?
//
// Nao "varrido em algum momento da ronda" — essa metrica engana feio. A Selva
// marcava 61% varrida e mesmo assim se atravessava em um minuto, porque num
// instante qualquer so 6% estava sob olho. O jogador nao anda no tempo todo da
// ronda; ele anda AGORA, e o que importa e quanto risco existe agora.
//
// Referencia pratica pra este projeto: abaixo de 10% o bloco e uma caminhada;
// 15 a 25% obriga a ler as rondas; acima de 35% vira tentativa e erro.
public static class VigilanciaCheck
{
    [MenuItem("Ecos/Medir vigilancia da cena aberta")]
    static void Rodar()
    {
        var jogador = Object.FindFirstObjectByType<SoldierController>();
        var cols = jogador != null ? jogador.GetComponentsInChildren<Collider>(true) : new Collider[0];
        foreach (var c in cols) c.enabled = false;

        try { Medir(); }
        finally { foreach (var c in cols) c.enabled = true; }
    }

    class Ronda
    {
        public Vector3[] pontos;
        public float alcance, meioAngulo, velocidade, pausa;
        public float ciclo;
        public float[] marcas;
        public bool parado;

        // sentinela: nao anda, varre um arco em volta de uma direcao autorada
        public bool posto;
        public float direcao, arco, velocidadeDoArco;

        // ouvido: raio simples, sem parede no meio — som atravessa, e o jogo trata assim
        public float raioDeEscuta, raioDeEscutaCorrendo;

        public void Preparar()
        {
            if (pontos.Length < 2) { parado = true; ciclo = 1f; return; }

            marcas = new float[pontos.Length + 1];
            float t = 0f;
            for (int i = 0; i < pontos.Length; i++)
            {
                marcas[i] = t;
                t += Vector3.Distance(pontos[i], pontos[(i + 1) % pontos.Length]) / Mathf.Max(0.1f, velocidade) + pausa;
            }
            marcas[pontos.Length] = t;
            ciclo = Mathf.Max(1f, t);
        }

        public void Em(float t, out Vector3 pos, out Vector3 frente)
        {
            if (posto)
            {
                pos = pontos[0];
                float yaw = direcao + Mathf.Sin(t * velocidadeDoArco) * arco;
                frente = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                return;
            }

            if (parado) { pos = pontos[0]; frente = Vector3.forward; return; }

            t = Mathf.Repeat(t, ciclo);
            for (int i = 0; i < pontos.Length; i++)
            {
                if (t > marcas[i + 1]) continue;

                Vector3 a = pontos[i], b = pontos[(i + 1) % pontos.Length];
                Vector3 dir = b - a; dir.y = 0f;
                frente = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;

                // a pausa no fim da perna e onde ele varre o arco parado
                float andando = (marcas[i + 1] - marcas[i]) - pausa;
                pos = (t - marcas[i] >= andando || andando < 0.01f)
                    ? b
                    : Vector3.Lerp(a, b, (t - marcas[i]) / andando);
                return;
            }
            pos = pontos[0]; frente = Vector3.forward;
        }
    }

    static void Medir()
    {
        Physics.SyncTransforms();

        // so onde o jogador de fato consegue chegar. Medir sobre o mapa inteiro
        // inclui a floresta em volta, que ninguem patrulha e ninguem pisa — e ai
        // o percentual so diz o tamanho da mata, nao o risco do bloco.
        var celulas = TravessiaCheck.AreaJogavel(1f);

        var rondas = new List<Ronda>();

        // o ruido do jogador sai do proprio controlador, nao de numero chutado
        var sol = Object.FindFirstObjectByType<SoldierController>();
        float ruidoAndando = sol != null ? sol.walkNoise : 6f;
        float ruidoCorrendo = sol != null ? sol.runNoise : 14f;

        foreach (var g in Object.FindObjectsByType<GuardAI>(FindObjectsSortMode.None))
        {
            Vector3[] pts;
            if (g.waypoints != null && g.waypoints.Length >= 2)
            {
                pts = new Vector3[g.waypoints.Length];
                for (int i = 0; i < pts.Length; i++) pts[i] = g.waypoints[i].position;
            }
            else pts = new[] { g.transform.position };

            var r = new Ronda
            {
                pontos = pts,
                alcance = g.viewRange,
                meioAngulo = g.viewAngle * 0.5f,
                velocidade = g.moveSpeed,
                pausa = g.waypointPause,
                posto = g.sentinela,
                direcao = g.direcaoDoPosto,
                arco = g.arcoDoPosto,
                velocidadeDoArco = g.velocidadeDoPosto,
                raioDeEscuta = ruidoAndando * g.hearingSensitivity,
                raioDeEscutaCorrendo = ruidoCorrendo * g.hearingSensitivity
            };
            if (g.sentinela) r.pontos = new[] { g.transform.position };
            r.Preparar();
            // o ciclo da sentinela e o vaivem do arco, nao uma volta de ronda
            if (g.sentinela) r.ciclo = Mathf.Max(1f, 2f * Mathf.PI / Mathf.Max(0.05f, g.velocidadeDoPosto));
            rondas.Add(r);
        }

        if (rondas.Count == 0 || celulas.Count == 0)
        {
            Debug.LogWarning($"[VIGILANCIA] nada a medir: {rondas.Count} guardas, {celulas.Count} celulas.");
            return;
        }

        float maior = 0f;
        foreach (var r in rondas) maior = Mathf.Max(maior, r.ciclo);

        float soma = 0f, pico = 0f, vale = 100f;
        float somaAndando = 0f, somaCorrendo = 0f;
        int amostras = 0;

        for (float t = 0f; t < maior; t += 1f)
        {
            int vistas = 0, ouvidasAndando = 0, ouvidasCorrendo = 0;

            foreach (var celula in celulas)
            {
                bool viu = false, ouviuAndando = false, ouviuCorrendo = false;

                foreach (var r in rondas)
                {
                    r.Em(t, out Vector3 pos, out Vector3 frente);

                    Vector3 plano = celula - pos; plano.y = 0f;
                    float d = plano.magnitude;

                    if (!ouviuCorrendo && d <= r.raioDeEscutaCorrendo) ouviuCorrendo = true;
                    if (!ouviuAndando && d <= r.raioDeEscuta) ouviuAndando = true;

                    if (!viu)
                    {
                        Vector3 olho = pos + Vector3.up * 0.6f;
                        Vector3 v = celula - olho; v.y = 0f;
                        if (v.magnitude <= r.alcance && Vector3.Angle(frente, v) <= r.meioAngulo
                            && !Physics.Linecast(olho, celula, ~0, QueryTriggerInteraction.Ignore))
                            viu = true;
                    }

                    if (viu && ouviuAndando && ouviuCorrendo) break;
                }

                if (viu) vistas++;
                if (ouviuAndando) ouvidasAndando++;
                if (ouviuCorrendo) ouvidasCorrendo++;
            }

            float f = 100f * vistas / celulas.Count;
            soma += f;
            somaAndando += 100f * ouvidasAndando / celulas.Count;
            somaCorrendo += 100f * ouvidasCorrendo / celulas.Count;
            pico = Mathf.Max(pico, f);
            vale = Mathf.Min(vale, f);
            amostras++;
        }

        float media = soma / amostras;
        float mediaAndando = somaAndando / amostras;
        float mediaCorrendo = somaCorrendo / amostras;

        string veredito =
            media < 10f ? "CAMINHADA — nao ha risco suficiente pra existir decisao" :
            media < 15f ? "FROUXO — da pra atravessar sem prestar atencao" :
            media <= 25f ? "BOM — obriga a ler as rondas" :
            media <= 35f ? "APERTADO — pode virar frustracao, conferir jogando" :
                           "TENTATIVA E ERRO — cobertura alta demais";

        // Dois sentidos, dois numeros. Acima do chao a ameaca sao olhos e o que
        // vale e a visao; nos tuneis os cegos escutam, a visao marca ~1% e isso
        // nao quer dizer bloco facil — quer dizer metrica errada pro bloco.
        string vereditoSom =
            mediaAndando < 25f ? "ANDAR E SEGURO — o ouvido nao esta cobrando nada" :
            mediaAndando < 50f ? "ANDAR CUSTA — da pra escolher onde fazer barulho" :
            mediaAndando < 80f ? "ANDAR E ARRISCADO — agachar vira o padrao" :
                                 "SO AGACHADO — andar em pe e suicidio no bloco inteiro";

        Debug.Log($"[VIGILANCIA] {rondas.Count} guardas, ciclo de {maior:0}s, {celulas.Count} celulas andaveis.");
        Debug.Log($"[VIGILANCIA] VISTO agora: {media:0.0}%  (vale {vale:0.0}%  pico {pico:0.0}%) — {veredito}");
        Debug.Log($"[VIGILANCIA] OUVIDO agora: andando {mediaAndando:0.0}%  correndo {mediaCorrendo:0.0}% — {vereditoSom}");
    }
}
