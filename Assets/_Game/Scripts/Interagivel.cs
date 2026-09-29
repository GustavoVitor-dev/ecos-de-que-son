using System.Collections.Generic;
using UnityEngine;

/// Coisa do mundo que o jogador pega, lê ou usa com [E].
/// Não decide nada sozinha: avisa a Historia, que sabe em que ponto da
/// corrente o jogador está e o que isso significa agora.
public class Interagivel : MonoBehaviour
{
    [Tooltip("Nome que a Historia usa pra saber o que é.")]
    public string id;
    [Tooltip("O que aparece no aviso: [E] <rotulo>.")]
    public string rotulo = "EXAMINAR";
    public float raio = 1.8f;
    [Tooltip("Segurar [E] em vez de apertar (manivela).")]
    public bool segurar;
    public float tempoSegurando = 7f;

    public static readonly List<Interagivel> Todos = new List<Interagivel>();

    void OnEnable() => Todos.Add(this);
    void OnDisable() => Todos.Remove(this);

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, raio);
    }
}
