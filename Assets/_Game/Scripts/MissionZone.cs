using UnityEngine;

/// Gatilho de missao. Duas variacoes: onde estao os documentos, e por onde se sai.
[RequireComponent(typeof(Collider))]
public class MissionZone : MonoBehaviour
{
    public enum Kind { Documentos, Extracao }

    public Kind kind = Kind.Documentos;
    [Tooltip("Objeto que some quando a zona e cumprida (o cubo dos documentos, por exemplo).")]
    public GameObject visual;

    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<SoldierController>() == null) return;

        var m = MissionDirector.Instance;
        if (m == null) return;

        if (kind == Kind.Documentos)
        {
            if (m.Current != MissionDirector.Phase.Documentos) return;
            m.CollectDocuments();
            if (visual != null) visual.SetActive(false);
        }
        else
        {
            m.Extract();
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = kind == Kind.Documentos
            ? new Color(1f, 0.85f, 0.2f, 0.35f)
            : new Color(0.3f, 1f, 0.55f, 0.35f);

        var c = GetComponent<Collider>();
        if (c != null) Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}
