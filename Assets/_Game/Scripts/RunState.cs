using UnityEngine;

/// O que atravessa os blocos.
///
/// Cada cena instancia o _Setup do zero, entao sem isto o jogador termina a selva
/// em Ruptura e chega no acampamento calmo — a progressao de terror dos 15 minutos
/// simplesmente nao existiria.
///
/// E vale para recurso tambem: tres iscas sao da RUN inteira, nao tres por bloco.
/// Recurso que se renova a cada cena nao gera decisao, gera rotina.
public static class RunState
{
    public static float Estresse;
    public static int Iscas = -1;          // -1 = ainda nao inicializado nesta run
    public static int Mortes;
    public static float Tempo;
    public static string UltimoBloco = "";

    /// Ligada no instante antes do LoadScene: diz a proxima cena que ela deve
    /// nascer preta e abrir. Nao entra no Zerar de proposito — zerar a run e
    /// uma coisa, estar no meio de uma transicao e outra.
    public static bool Transicionando;

    /// O bloco onde uma run comeca. Entrar nele do zero significa run nova.
    public const string BlocoInicial = "Selva";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void AoIniciarPlay() => Zerar();

    public static void Zerar()
    {
        Estresse = 0f;
        Iscas = -1;
        Mortes = 0;
        Tempo = 0f;
        UltimoBloco = "";
    }

    /// Chamado por quem carrega. Zera so quando a run realmente recomeca —
    /// ou seja, ao entrar no bloco inicial vindo de outro lugar (ou de lugar nenhum).
    public static void AoEntrarNoBloco(string bloco)
    {
        if (bloco == BlocoInicial && UltimoBloco != BlocoInicial && UltimoBloco != "")
            Zerar();

        UltimoBloco = bloco;
    }

    public static string Resumo()
        => $"estresse {Estresse:0} | iscas {Iscas} | mortes {Mortes} | {Tempo:0}s";
}
