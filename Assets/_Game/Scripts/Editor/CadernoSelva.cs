using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Desenha o caderno do Miller a partir do mapa DE VERDADE: o córrego pela
/// mesma função do terreno, as trilhas pela mesma lista, as crateras, a cerca,
/// o posto, a aldeia — e um X vermelho onde os guardas da cena fazem ronda.
/// Se o mapa mudar, rodar a montagem de novo refaz o desenho.
///
/// Traço de caneta de mão: cada linha é uma fileira de carimbos redondos com
/// o centro tremido por Perlin e a espessura variando.
public static class CadernoSelva
{
    const string Pasta = "Assets/_Game/Texturas/Caderno/";
    // recorte do mundo que cabe na página (um pouco além da área jogável)
    const float X0 = -43f, X1 = 43f, Z0 = -42.5f, Z1 = 47.5f;
    const int W = 860;
    static int H => Mathf.RoundToInt(W * (Z1 - Z0) / (X1 - X0));

    static readonly Color Grafite = new Color(0.14f, 0.13f, 0.12f);
    static readonly Color Vermelho = new Color(0.55f, 0.1f, 0.07f);
    static readonly Color Azul = new Color(0.14f, 0.22f, 0.45f);

    static Color[] px;
    static int w, h;
    static System.Random rnd;

    public static void Montar(Caderno c)
    {
        Directory.CreateDirectory(Pasta);
        c.papel = Salvar("Caderno_Papel", Papel(520, 720, 11, false));
        c.rasgada = Salvar("Caderno_PaginaArrancada", PaginaArrancada(520, 720));
        c.mapa = Salvar("Caderno_Mapa", Mapa(out var rotulos));
        c.rotulos = rotulos;
        c.letra = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Game/Fontes/InkFree.ttf");

        c.batedores =
            "O que os batedores passaram\n" +
            "(rádio, dia 1 — antes de sumir)\n\n" +
            "- o córrego corta tudo, leste a oeste. barranco mole, não dá pé.\n" +
            "  só passa em 3 lugares: o VAU (oeste), a PASSAGEM do meio e um TRONCO caído (leste)\n" +
            "- vau: água no joelho. faz barulho.\n" +
            "- posto de caixotes largado no noroeste.\n" +
            "- aldeia no fim do leste. vazia. tem um poço.\n" +
            "- base de fogo no norte. portão com corrente.\n" +
            "- <color=#8a1a12>X = viram eles. lanterna de noite. andam em roda.</color>\n\n" +
            "última transmissão, 23h40:\n" +
            "\"tem mais deles do que disseram\"\n" +
            "depois, nada.";

        c.operacao =
            "OPERAÇÃO — 2ª tropa (a nova)\n" +
            "dia 2, noite\n\n" +
            "<color=#23386f>SGT. WARD</color> — segura o posto dos caixotes (NO).\n" +
            "      leva a chave do portão.\n" +
            "<color=#23386f>DOYLE</color> — vai com a equipe pra aldeia (L).\n" +
            "      encontro: o poço.\n" +
            "MILLER (eu) — rádio. fico com o sargento.\n\n" +
            "ao clarear: todo mundo no portão.\n" +
            "base de fogo → ZP ao norte.\n" +
            "o helicóptero não espera.\n\n" +
            "freq. 46.50\n" +
            "senha: TROVÃO   resposta: CHUVA";

        // o que sobrou da página, no toco perto da costura
        c.restoDaPagina = "...ele\n\n...mando\n\n...não\n\n...olta\n\n\n...rri";
        EditorUtility.SetDirty(c);
    }

    // ---------------------------------------------------------------- papel

    static Color[] Papel(int pw, int ph, int semente, bool pautaFraca)
    {
        var r = new System.Random(semente);
        float ox = (float)r.NextDouble() * 100f, oy = (float)r.NextDouble() * 100f;
        var p = new Color[pw * ph];
        var baseC = new Color(0.77f, 0.71f, 0.56f);
        for (int y = 0; y < ph; y++)
            for (int x = 0; x < pw; x++)
            {
                float u = x / (float)pw, v = y / (float)ph;
                float mancha = Mathf.PerlinNoise(ox + u * 4f, oy + v * 5f);
                float fibra = Mathf.PerlinNoise(ox + u * 90f, oy + v * 12f);
                var col = baseC * (0.86f + 0.14f * mancha) * (0.97f + 0.05f * fibra);
                // borda escurecida e amarelada (manuseio, água)
                float borda = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                col = Color.Lerp(col, new Color(0.5f, 0.4f, 0.26f), Mathf.Clamp01(1f - borda * 14f) * 0.55f);
                // mofo
                float mofo = Mathf.PerlinNoise(ox + u * 9f + 40f, oy + v * 9f);
                if (mofo > 0.72f) col = Color.Lerp(col, new Color(0.45f, 0.47f, 0.34f), (mofo - 0.72f) * 2.2f);
                // pauta azul apagada e margem vermelha
                float linha = (ph - y) % 30;
                if (y < ph - 70 && linha < 1.5f) col = Color.Lerp(col, new Color(0.45f, 0.55f, 0.66f), 0.28f);
                if (Mathf.Abs(x - pw * 0.09f) < 1.2f) col = Color.Lerp(col, new Color(0.7f, 0.3f, 0.28f), 0.3f);
                col.a = 1f;
                p[y * pw + x] = col;
            }
        // anéis de água (caneca, chuva)
        for (int k = 0; k < 2; k++)
        {
            float cx = (float)r.NextDouble() * pw, cy = (float)r.NextDouble() * ph, raio = 50f + (float)r.NextDouble() * 60f;
            for (int y = 0; y < ph; y++)
                for (int x = 0; x < pw; x++)
                {
                    float d = Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - raio);
                    if (d < 4f) p[y * pw + x] = Color.Lerp(p[y * pw + x], new Color(0.52f, 0.42f, 0.28f), (1f - d / 4f) * 0.35f);
                }
        }
        return p;
    }

    static Color[] PaginaArrancada(int pw, int ph)
    {
        // por baixo: a página seguinte (sem pauta de texto) com o sangue que passou
        var p = Papel(pw, ph, 29, true);
        var r = new System.Random(66);
        w = pw; h = ph; px = p; rnd = r;
        // mão arrastada saindo do rasgo: quatro dedos juntos, a tinta acabando
        // no caminho (forte no começo, falhando no fim)
        var sangue = new Color(0.3f, 0.04f, 0.03f);
        var ini = new Vector2(pw * 0.15f, ph * 0.52f);
        var dir = new Vector2(1f, -0.32f).normalized; var lado = new Vector2(-dir.y, dir.x);
        for (int dedo = 0; dedo < 4; dedo++)
        {
            float comp = pw * (0.3f + 0.12f * (float)r.NextDouble()) * (dedo == 3 ? 0.7f : 1f);
            var o = ini + lado * (dedo * 17f - 25f) + dir * (dedo == 0 || dedo == 3 ? 8f : 0f);
            for (float t = 0f; t < comp; t += 1.2f)
            {
                float u = t / comp;
                float falha = Mathf.PerlinNoise(dedo * 7.1f, t * 0.09f);
                if (u > 0.35f && falha < 0.25f + u * 0.45f) continue;
                float forca = 0.6f * Mathf.Pow(1f - u, 0.8f);
                float curva = Mathf.Sin(u * 2.2f) * 10f;
                Carimbo(o + dir * t + lado * curva, (6.5f - u * 3f) * (0.8f + 0.4f * falha), sangue, forca * 0.35f);
            }
        }
        // pingos: gota grande e respingo em volta
        for (int i = 0; i < 7; i++)
        {
            var c = new Vector2(pw * (0.25f + 0.6f * (float)r.NextDouble()), ph * (0.12f + 0.75f * (float)r.NextDouble()));
            float g = 3f + (float)r.NextDouble() * 5f;
            Carimbo(c, g, new Color(0.28f, 0.03f, 0.02f), 0.75f);
            for (int k = 0; k < 6; k++)
            {
                float a = (float)r.NextDouble() * Mathf.PI * 2f, d = g * (1.3f + (float)r.NextDouble() * 2.2f);
                Carimbo(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d, 0.8f + (float)r.NextDouble() * 1.6f, sangue, 0.7f);
            }
        }
        // digital
        var dg = new Vector2(pw * 0.62f, ph * 0.22f);
        for (int k = 0; k < 7; k++) Anel(dg, 6f + k * 3.2f, 1.3f, new Color(0.35f, 0.05f, 0.04f), 0.5f, 0.75f);

        // o toco da página arrancada, perto da costura, com a borda rasgada
        float ox = (float)r.NextDouble() * 50f;
        for (int y = 0; y < ph; y++)
        {
            float rasgo = pw * (0.13f + 0.035f * Mathf.PerlinNoise(ox, y * 0.045f) + 0.02f * Mathf.PerlinNoise(ox + 9f, y * 0.3f));
            for (int x = 0; x < pw; x++)
            {
                int i = y * pw + x;
                if (x < rasgo)
                {
                    float fibra = Mathf.PerlinNoise(x * 0.3f, y * 0.3f);
                    var col = new Color(0.8f, 0.74f, 0.6f) * (0.9f + 0.1f * fibra);
                    if (rasgo - x < 3f) col = new Color(0.9f, 0.86f, 0.74f);     // fibra branca do rasgo
                    // sangue no toco, mais forte na borda
                    float s = Mathf.PerlinNoise(x * 0.04f + 3f, y * 0.02f);
                    if (s > 0.5f && y > ph * 0.25f && y < ph * 0.8f) col = Color.Lerp(col, new Color(0.3f, 0.04f, 0.03f), Mathf.Clamp01((s - 0.5f) * 3f) * (0.4f + 0.6f * x / rasgo));
                    col.a = 1f;
                    p[i] = col;
                }
                else if (x < rasgo + 7f)
                {
                    p[i] = Color.Lerp(p[i], Color.black, (1f - (x - rasgo) / 7f) * 0.35f);   // sombra do toco
                }
            }
        }
        return p;
    }

    // ---------------------------------------------------------------- mapa

    static Color[] Mapa(out List<Caderno.Rotulo> rotulos)
    {
        w = W; h = H; px = new Color[w * h]; rnd = new System.Random(1968);
        var lista = new List<Caderno.Rotulo>();
        rotulos = lista;

        // moldura
        var cantos = new[] { M(-41.5f, -41.3f), M(41.5f, -41.3f), M(41.5f, 46.3f), M(-41.5f, 46.3f) };
        for (int i = 0; i < 4; i++) Traco(cantos[i], cantos[(i + 1) % 4], 2.2f, Grafite, 0.8f, 1.2f, 0f);

        // mata: rabiscos de copa nas beiradas e manchas no meio
        for (int i = 0; i < 260; i++)
        {
            float x = Mathf.Lerp(-40f, 40f, (float)rnd.NextDouble()), z = Mathf.Lerp(-40f, 45f, (float)rnd.NextDouble());
            bool beirada = Mathf.Abs(x) > 17f;
            if (!beirada && rnd.NextDouble() > 0.28) continue;
            if (Mathf.Abs(z - FlorestaSelva.ZCorrego(x)) < 3.5f || DistTrilha(x, z) < 2.4f || Lugar(x, z) < 5f) continue;
            Copa(M(x, z), 5f + (float)rnd.NextDouble() * 5f, new Color(Grafite.r, Grafite.g, Grafite.b), 0.35f);
        }

        // córrego: duas margens e ondinhas
        float passo = 0.6f;
        for (float x = -41f; x < 41f; x += passo)
        {
            foreach (float lado in new[] { -1f, 1f })
            {
                float z0 = FlorestaSelva.ZCorrego(x) + lado * FlorestaSelva.LarguraCorrego(x);
                float z1 = FlorestaSelva.ZCorrego(x + passo) + lado * FlorestaSelva.LarguraCorrego(x + passo);
                if (Travessia(x) && lado > 0f && Mathf.Repeat(x, 1.8f) > 0.9f) continue;
                Traco(M(x, z0), M(x + passo, z1), 1.9f, Grafite, 0.85f, 0.5f, 0f);
            }
            if (Mathf.Repeat(x, 3.6f) < passo && !Travessia(x))
            {
                var c = M(x + 0.3f, FlorestaSelva.ZCorrego(x));
                Traco(c + new Vector2(-6f, 0f), c + new Vector2(-2f, 2.5f), 1.1f, Grafite, 0.5f, 0.3f, 0f);
                Traco(c + new Vector2(-2f, 2.5f), c + new Vector2(3f, -1f), 1.1f, Grafite, 0.5f, 0.3f, 0f);
            }
        }

        // trilhas: tracejadas
        foreach (var t in FlorestaSelva.Trilhas)
            for (int i = 0; i + 1 < t.Length; i++)
            {
                if (Fora(t[i]) && Fora(t[i + 1])) continue;
                Traco(M(t[i].x, t[i].y), M(t[i + 1].x, t[i + 1].y), 1.4f, Grafite, 0.7f, 0.9f, 7f);
            }

        // travessias
        var vau = M(-26.4f, FlorestaSelva.ZCorrego(-26.4f));
        for (int k = -2; k <= 2; k++) Carimbo(vau + new Vector2(k * 6f, (k % 2) * 3f), 2.6f, Grafite, 0.8f);
        var tronco = M(11.9f, FlorestaSelva.ZCorrego(11.9f));
        Traco(tronco + new Vector2(-4f, -22f), tronco + new Vector2(4f, 22f), 5f, Grafite, 0.85f, 0.6f, 0f);
        var meio = M(0.5f, FlorestaSelva.ZCorrego(0.5f));
        Traco(meio + new Vector2(-10f, -16f), meio + new Vector2(-10f, 16f), 1.2f, Grafite, 0.7f, 0.5f, 3f);
        Traco(meio + new Vector2(10f, -16f), meio + new Vector2(10f, 16f), 1.2f, Grafite, 0.7f, 0.5f, 3f);

        // crateras
        foreach (var c in FlorestaSelva.Crateras)
        {
            if (Fora(new Vector2(c.x, c.y))) continue;
            var m = M(c.x, c.y); float raio = c.z / (X1 - X0) * w;
            Anel(m, raio, 1.6f, Grafite, 0.8f, 1f);
            for (int k = 0; k < 5; k++) { float a = k * 1.1f; Traco(m + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * raio * 0.3f, m + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * raio * 0.85f, 1f, Grafite, 0.5f, 0.2f, 0f); }
        }

        // cerca norte com o portão
        float zc = 38f;
        for (float x = -40f; x < 40f; x += 1.6f)
        {
            if (Mathf.Abs(x - 1f) < 2.3f) continue;
            Traco(M(x, zc), M(Mathf.Min(x + 1.6f, 40f), zc), 1.6f, Grafite, 0.85f, 0.4f, 0f);
            var p = M(x + 0.8f, zc);
            Traco(p + new Vector2(-3f, -3f), p + new Vector2(3f, 3f), 1f, Grafite, 0.6f, 0.2f, 0f);
            Traco(p + new Vector2(-3f, 3f), p + new Vector2(3f, -3f), 1f, Grafite, 0.6f, 0.2f, 0f);
        }
        Retangulo(M(-1.5f, 37.4f), M(3.5f, 38.6f), 2.2f, Grafite);
        // corrente
        for (int k = 0; k < 4; k++) Anel(M(0.2f + k * 0.55f, 38f), 3f, 1.1f, Grafite, 0.8f, 1f);

        // posto dos caixotes
        var posto = new Vector2(-12f, 20f);
        for (int k = 0; k < 4; k++)
        {
            var o = new Vector2((k % 2) * 1.3f - 0.6f, (k / 2) * 1.3f - 0.6f);
            Retangulo(M(posto.x + o.x - 0.5f, posto.y + o.y - 0.5f), M(posto.x + o.x + 0.5f, posto.y + o.y + 0.5f), 1.5f, Grafite);
        }
        // aldeia: casas e o poço
        foreach (var casa in new[] { new Vector2(35f, 7.5f), new Vector2(33.5f, 11.5f), new Vector2(37.5f, 3f), new Vector2(27.5f, 9.5f) })
        {
            Retangulo(M(casa.x - 1.2f, casa.y - 1f), M(casa.x + 1.2f, casa.y + 1f), 1.6f, Grafite);
            Traco(M(casa.x - 1.2f, casa.y - 1f), M(casa.x + 1.2f, casa.y + 1f), 1f, Grafite, 0.6f, 0.2f, 0f);
            Traco(M(casa.x - 1.2f, casa.y + 1f), M(casa.x + 1.2f, casa.y - 1f), 1f, Grafite, 0.6f, 0.2f, 0f);
        }
        Anel(M(30.8f, 4.6f), 6f, 1.8f, Grafite, 0.9f, 1f);
        Carimbo(M(30.8f, 4.6f), 3f, Grafite, 0.9f);

        // seta do norte
        var sn = M(36f, 41f);
        Traco(sn + new Vector2(0f, -22f), sn + new Vector2(0f, 22f), 2f, Grafite, 0.9f, 0.3f, 0f);
        Traco(sn + new Vector2(0f, 22f), sn + new Vector2(-6f, 12f), 2f, Grafite, 0.9f, 0.3f, 0f);
        Traco(sn + new Vector2(0f, 22f), sn + new Vector2(6f, 12f), 2f, Grafite, 0.9f, 0.3f, 0f);

        // onde viram inimigo: X vermelho no meio da ronda de cada guarda da cena
        var vistos = new List<Vector2>();
        foreach (var g in Object.FindObjectsByType<GuardAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Vector3 soma = Vector3.zero; int n = 0; float espalho = 0f;
            if (g.waypoints != null) foreach (var wp in g.waypoints) if (wp != null) { soma += wp.position; n++; }
            Vector3 c = n > 0 ? soma / n : g.transform.position;
            if (g.waypoints != null) foreach (var wp in g.waypoints) if (wp != null) espalho = Mathf.Max(espalho, Vector3.Distance(wp.position, c));
            if (Fora(new Vector2(c.x, c.z))) continue;
            // anotação de batedor não é exata: um metro ou dois pra lá
            var q = new Vector2(c.x + ((float)rnd.NextDouble() - 0.5f) * 2.5f, c.z + ((float)rnd.NextDouble() - 0.5f) * 2.5f);
            vistos.Add(q);
            var m = M(q.x, q.y);
            Traco(m + new Vector2(-7f, -7f), m + new Vector2(7f, 7f), 2.4f, Vermelho, 0.9f, 0.6f, 0f);
            Traco(m + new Vector2(-7f, 7f), m + new Vector2(7f, -7f), 2.4f, Vermelho, 0.9f, 0.6f, 0f);
            if (espalho > 2f) Anel(m, espalho / (X1 - X0) * w, 1.2f, Vermelho, 0.6f, 0.55f);
        }

        // onde a tropa nova ia ficar: círculos azuis
        Anel(M(-12f, 20f), 28f, 2f, Azul, 0.85f, 1f);
        Anel(M(31f, 6f), 34f, 2f, Azul, 0.85f, 1f);

        // sujeira: dedo de lama, sangue num canto, pingo de chuva borrando
        Carimbo(M(-36f, -37f), 16f, new Color(0.3f, 0.2f, 0.1f), 0.18f);
        for (int k = 0; k < 6; k++) Anel(M(38f, -38f), 5f + k * 2.6f, 1.1f, new Color(0.35f, 0.05f, 0.04f), 0.45f, 0.7f);
        for (int k = 0; k < 5; k++) Carimbo(M(Mathf.Lerp(-35f, 35f, (float)rnd.NextDouble()), Mathf.Lerp(-35f, 42f, (float)rnd.NextDouble())), 8f + (float)rnd.NextDouble() * 8f, new Color(0.35f, 0.3f, 0.22f), 0.08f);

        // rótulos (texto é na letra de mão, desenhado pelo Caderno por cima)
        void R(string t, float x, float z, float giro, float tam = 17f, int cor = 0)
            => lista.Add(new Caderno.Rotulo { texto = t, pos = new Vector2((x - X0) / (X1 - X0), (z - Z0) / (Z1 - Z0)), giro = giro, tamanho = tam, cor = cor });
        R("CÓRREGO", -14f, FlorestaSelva.ZCorrego(-14f) - 3.4f, -4f, 20f);
        R("vau — barulho!", -27f, FlorestaSelva.ZCorrego(-26.4f) + 4.2f, 3f, 15f);
        R("tronco", 15.5f, FlorestaSelva.ZCorrego(11.9f) - 3.2f, -2f, 15f);
        R("passagem", 4.8f, FlorestaSelva.ZCorrego(0.5f) + 3.2f, 2f, 15f);
        R("POSTO\n(caixotes)", -18.5f, 19.5f, -3f, 16f);
        R("ALDEIA", 33f, 15.5f, 2f, 19f);
        R("poço", 27.2f, 2.3f, -5f, 14f);
        R("BASE DE FOGO", -2f, 43.3f, -1f, 18f);
        R("portão — corrente", 11f, 40.2f, 2f, 14f);
        R("N", 36f, 45.3f, 0f, 20f);
        R("ZP ↑", 24f, 44.2f, -2f, 16f);
        R("mata fechada", -33f, -5f, 78f, 14f);
        R("mata fechada", 35f, -18f, -80f, 14f);
        R("cratera", -34f, -24.5f, -6f, 13f);
        R("SGT. WARD", -12f, 26.8f, -3f, 16f, 2);
        R("DOYLE", 31f, -2.4f, 4f, 16f, 2);
        if (vistos.Count > 0)
        {
            // uma nota vermelha só, perto do X mais ao sul (o primeiro que o jogador cruza)
            vistos.Sort((a, b) => a.y.CompareTo(b.y));
            var v = vistos[0];
            R("lanterna.\nandam em roda", v.x + 6.5f, v.y - 1.5f, -6f, 13f, 1);
            if (vistos.Count > 3) R("??", vistos[3].x + 3f, vistos[3].y + 3f, 10f, 15f, 1);
        }
        return px;
    }

    static float DistTrilha(float x, float z)
    {
        float m = float.MaxValue; var p = new Vector2(x, z);
        foreach (var t in FlorestaSelva.Trilhas)
            for (int i = 0; i + 1 < t.Length; i++)
            {
                Vector2 a = t[i], ab = t[i + 1] - a;
                float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-5f));
                m = Mathf.Min(m, Vector2.Distance(p, a + ab * k));
            }
        return m;
    }

    static float Lugar(float x, float z)
    {
        float m = float.MaxValue; var p = new Vector2(x, z);
        foreach (var l in new[] { new Vector2(-12f, 20f), new Vector2(31f, 6f), new Vector2(1f, 38f) }) m = Mathf.Min(m, Vector2.Distance(p, l));
        return m;
    }

    static bool Travessia(float x)
    {
        foreach (var t in FlorestaSelva.Travessias) if (x > t.x - 0.4f && x < t.y + 0.4f) return true;
        return false;
    }

    static bool Fora(Vector2 p) => p.x < X0 + 1f || p.x > X1 - 1f || p.y < Z0 + 1f || p.y > Z1 - 1f;

    static Vector2 M(float x, float z) => new Vector2((x - X0) / (X1 - X0) * w, (z - Z0) / (Z1 - Z0) * h);

    // ---------------------------------------------------------------- caneta

    static void Carimbo(Vector2 c, float raio, Color cor, float forca)
    {
        int x0 = Mathf.Max(0, (int)(c.x - raio - 1)), x1 = Mathf.Min(w - 1, (int)(c.x + raio + 1));
        int y0 = Mathf.Max(0, (int)(c.y - raio - 1)), y1 = Mathf.Min(h - 1, (int)(c.y + raio + 1));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Mathf.Sqrt((x - c.x) * (x - c.x) + (y - c.y) * (y - c.y));
                float a = Mathf.Clamp01(raio + 0.5f - d) * forca;
                if (a <= 0f) continue;
                int i = y * w + x;
                var o = px[i];
                // tinta sobre tinta escurece, não clareia: soma alfa, mistura cor
                float na = o.a + a * (1f - o.a);
                var nc = na > 0f ? (new Color(o.r, o.g, o.b) * o.a * (1f - a) + cor * a) / Mathf.Max(0.0001f, o.a * (1f - a) + a) : cor;
                px[i] = new Color(nc.r, nc.g, nc.b, na);
            }
    }

    /// Linha de mão: tremida, espessura variando, opcionalmente tracejada.
    static void Traco(Vector2 a, Vector2 b, float largura, Color cor, float forca, float tremor, float traco)
    {
        float comp = Vector2.Distance(a, b);
        if (comp < 0.01f) return;
        var dir = (b - a) / comp; var lado = new Vector2(-dir.y, dir.x);
        float semente = (float)rnd.NextDouble() * 100f;
        for (float s = 0f; s <= comp; s += 0.6f)
        {
            if (traco > 0f && Mathf.Repeat(s, traco * 2f) > traco) continue;
            float trem = (Mathf.PerlinNoise(semente, s * 0.05f) - 0.5f) * 2f * tremor * 2f;
            float esp = largura * (0.75f + 0.5f * Mathf.PerlinNoise(semente + 7f, s * 0.08f));
            Carimbo(a + dir * s + lado * trem, esp * 0.5f, cor, forca * 0.6f);
        }
    }

    static void Anel(Vector2 c, float raio, float largura, Color cor, float forca, float fecha)
    {
        int n = Mathf.Max(12, (int)(raio * 0.9f));
        float ini = (float)rnd.NextDouble() * Mathf.PI * 2f;
        Vector2 ant = Vector2.zero;
        for (int i = 0; i <= n * fecha; i++)
        {
            float a = ini + i / (float)n * Mathf.PI * 2f;
            float r = raio * (1f + 0.06f * Mathf.Sin(a * 3f + ini));
            var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            if (i > 0) Traco(ant, p, largura, cor, forca, 0.2f, 0f);
            ant = p;
        }
    }

    static void Copa(Vector2 c, float raio, Color cor, float forca)
    {
        // nuvenzinha de árvore: 4-6 arcos em volta
        int n = 4 + rnd.Next(3);
        for (int k = 0; k < n; k++)
        {
            float a = k / (float)n * Mathf.PI * 2f;
            Anel(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * raio * 0.6f, raio * 0.45f, 1f, cor, forca, 0.6f);
        }
    }

    static void Retangulo(Vector2 a, Vector2 b, float largura, Color cor)
    {
        Traco(new Vector2(a.x, a.y), new Vector2(b.x, a.y), largura, cor, 0.85f, 0.3f, 0f);
        Traco(new Vector2(b.x, a.y), new Vector2(b.x, b.y), largura, cor, 0.85f, 0.3f, 0f);
        Traco(new Vector2(b.x, b.y), new Vector2(a.x, b.y), largura, cor, 0.85f, 0.3f, 0f);
        Traco(new Vector2(a.x, b.y), new Vector2(a.x, a.y), largura, cor, 0.85f, 0.3f, 0f);
    }

    // ---------------------------------------------------------------- disco

    static Texture2D Salvar(string nome, Color[] p)
    {
        int tw = nome == "Caderno_Mapa" ? W : 520, th = nome == "Caderno_Mapa" ? H : 720;
        var t = new Texture2D(tw, th, TextureFormat.RGBA32, false);
        t.SetPixels(p); t.Apply();
        string arq = Pasta + nome + ".png";
        File.WriteAllBytes(arq, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(arq, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(arq);
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(arq);
    }
}
