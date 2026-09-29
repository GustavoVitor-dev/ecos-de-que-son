using UnityEngine;
using UnityEngine.InputSystem;

/// HUD de debug. Existe pra tornar visivel o que e invisivel: ruido e deteccao.
/// Sem isso nao da pra afinar o jogo, so adivinhar.
public class PrototypeHUD : MonoBehaviour
{
    SoldierController player;
    CameraRig rig;
    GuardAI[] guards;

    void Start()
    {
        player = FindFirstObjectByType<SoldierController>();
        rig = FindFirstObjectByType<CameraRig>();
        guards = FindObjectsByType<GuardAI>(FindObjectsSortMode.None);
    }

    /// Escondido por padrão: é ferramenta de afinar número, não interface.
    /// [F1] liga e desliga.
    public static bool Visivel;

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) Visivel = !Visivel;
    }

    void OnGUI()
    {
        if (!Visivel) return;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        style.normal.textColor = Color.white;

        float y = 10f;
        void Line(string s)
        {
            GUI.Label(new Rect(14f, y, 700f, 20f), s, style);
            y += 18f;
        }

        var m = MissionDirector.Instance;
        if (m != null)
        {
            Line($"OBJETIVO: {m.StatusLine}");
            Line($"TEMPO: {m.Elapsed:00.0}s");
            y += 6f;
        }

        var st = StressSystem.Instance;
        if (st != null)
        {
            string bar = new string('#', Mathf.RoundToInt(st.Value / 5f)).PadRight(20, '.');
            Line($"ESTRESSE [{bar}] {st.Value:000}  {st.Current.ToString().ToUpper()}");
        }

        var pc = FindFirstObjectByType<PlayerConcealment>();
        if (pc != null)
            Line($"VISIBILIDADE: {(pc.VisibilityMultiplier * 100f):000}%   {(pc.InConcealment ? "[NO MATO]" : "")}");

        var mk = FindFirstObjectByType<MeleeKill>();
        if (mk != null)
            Line(mk.Target != null
                ? $"[E] FACA  ->  {mk.Target.name}      mortes: {mk.Kills}"
                : $"FACA: sem alvo      mortes: {mk.Kills}");

        var bt = FindFirstObjectByType<BaitThrower>();
        if (bt != null)
            Line($"ISCAS: {bt.Remaining}   [G] segura pra mirar, solta pra jogar {(bt.Aiming ? "  << MIRANDO" : "")}");

        Line(rig != null ? $"CAMERA: {rig.mode}      [Tab] alterna" : "CAMERA: sem rig");

        if (player != null)
            Line($"RUIDO: {player.CurrentNoiseRadius:00.0} m   {(player.IsCrouching ? "[AGACHADO]" : "")}      [Shift] correr   [Ctrl] agachar   [F] lanterna");

        if (guards != null)
            foreach (var g in guards)
                if (g != null)
                    Line($"{g.name}:  {g.CurrentState}   deteccao {(g.Detection * 100f):00}%");

        foreach (var li in FindObjectsByType<Listener>(FindObjectsSortMode.None))
            if (li != null) Line($"{li.name}: {(li.Alarmed ? "ALARMADO" : "escutando")}");

        var ed = FindFirstObjectByType<EntityDirector>();
        if (ed != null)
            Line($"ENTIDADE: {ed.Count} aparicoes    [1] periferica  [2] confirmada  [3] entre eles");

        if (PrototypeDirector.Instance != null)
            Line($"PEGO: {PrototypeDirector.Instance.caughtCount}x");
    }
}
