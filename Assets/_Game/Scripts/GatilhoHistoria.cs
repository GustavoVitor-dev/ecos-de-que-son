using UnityEngine;

/// Área que avisa a Historia quando o jogador entra. Quem decide o que
/// acontece é a Historia — o gatilho só diz "ele chegou em <id>".
[RequireComponent(typeof(Collider))]
public class GatilhoHistoria : MonoBehaviour
{
    public string id;

    void Reset() { GetComponent<Collider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<SoldierController>() == null) return;
        if (Historia.Instance != null) Historia.Instance.Evento(id);
    }

    void OnDrawGizmos()
    {
        var c = GetComponent<Collider>(); if (c == null) return;
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.25f);
        Gizmos.DrawCube(c.bounds.center, c.bounds.size);
    }
}
