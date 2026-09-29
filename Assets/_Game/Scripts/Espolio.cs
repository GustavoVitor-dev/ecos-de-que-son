using UnityEngine;

/// Peça escondida dentro de um corpo. Invisível até ser revistada; aí a
/// Historia mostra a inspeção dela e conta pro conserto do rádio.
public class Espolio : MonoBehaviour
{
    public string nome = "PEÇA";
    [TextArea(2, 5)] public string texto;
}
