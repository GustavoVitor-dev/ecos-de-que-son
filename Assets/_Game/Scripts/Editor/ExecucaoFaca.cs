using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// Monta a execução da faca: os dois controladores do par de animações
/// (atacante e vítima) e a faca que aparece na mão do Miller.
///
/// As animações vêm do add-on Stealth Kill do Invector, que já está no
/// projeto: ambas humanoides e do mesmo par (3,25 s), então retargetam nos
/// nossos dois modelos (o jogador e o ToonSoldier dos guardas).
public static class ExecucaoFaca
{
    const string Ativo = "Assets/Invector-3rdPersonController/Add-ons/StealthKill/Animations/Stealth_Kill_v1_Active.fbx";
    const string Passivo = "Assets/Invector-3rdPersonController/Add-ons/StealthKill/Animations/Stealth_Kill_v1_Passive.fbx";
    const string Pasta = "Assets/_Game/Modelos/Q_Animacoes/";
    const string PastaPrefab = "Assets/_Game/Prefabs/";

    public static void Montar(MeleeKill faca, Transform raizTemporaria)
    {
        if (faca == null) return;
        Undo.RecordObject(faca, "execucao");
        faca.controladorDeExecucao = Controlador("AC_Execucao", Ativo, "Stealth_Kill_Active", "Executar", false, true);
        faca.controladorDaVitima = Controlador("AC_Executado", Passivo, "Stealth_Kill_Passive", "Executado", true);
        faca.facaNaMao = FacaDeMao(raizTemporaria);
        // de frente: a estocada do Miller e a cabeca do outro indo pra tras
        faca.controladorDaFacada = Simples("AC_Facada", "Armature|Punch_Jab", "Facada", "Armature|Idle_Loop", "Saida", 0.85f, 0.2f);
        // o empurra-empurra: os dois em cima da faca enquanto o botao e martelado
        faca.controladorDoForcejo = Laco("AC_Forcejo", "Armature|Push_Loop", "Forcejo");
        // o guarda NÃO faz o mesmo que o Miller: ele agarra a lâmina com as
        // duas mãos. Com o mesmo clipe nos dois, a luta parecia dois bonecos
        // no mesmo motor, defasados.
        faca.controladorDoForcejoDoGuarda = Laco("AC_ForcejoGuarda", "Armature|Sword_Block", "Forcejo", true);
        faca.controladorDoEmpurrao = Simples("AC_Empurrao", "Armature|Hit_Knockback", "Empurrado", "Armature|Idle_Loop", "Saida", 0.8f, 0.2f);
        faca.controladorDoOlho = Simples("AC_NoOlho", "Armature|Hit_Head", "Levou", null, "Caido", 0.8f, 0.15f);
        // o par para de se mexer em 80% de 3,25 s: dali em diante e emenda
        faca.duracaoDaExecucao = 2.95f;

        var sons = new System.Collections.Generic.List<AudioClip>();
        foreach (var arq in System.IO.Directory.GetFiles("Assets/_Game/Audio"))
        {
            if (!arq.EndsWith(".wav") || !arq.Contains("Engasgo")) continue;
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(arq.Replace('\\', '/'));
            if (c != null) sons.Add(c);
        }
        faca.engasgos = sons.ToArray();

        var esforcos = new System.Collections.Generic.List<AudioClip>();
        foreach (var arq in System.IO.Directory.GetFiles("Assets/_Game/Audio"))
        {
            if (!arq.EndsWith(".wav") || !arq.Contains("Esforco")) continue;
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(arq.Replace('\\', '/'));
            if (c != null) esforcos.Add(c);
        }
        faca.esforcos = esforcos.ToArray();

        // o jogador some quando entra na moita
        if (faca.GetComponent<JogadorNaMoita>() == null) Undo.AddComponent<JogadorNaMoita>(faca.gameObject);
        // vida: pouca e sem cura no capítulo (os guardas agora atiram)
        if (faca.GetComponent<Vida>() == null) Undo.AddComponent<Vida>(faca.gameObject);
        EditorUtility.SetDirty(faca);
        Debug.Log("[FACA] execução montada: " + (faca.controladorDeExecucao != null) + "/" + (faca.controladorDaVitima != null) + " faca " + (faca.facaNaMao != null));
    }

    /// Os dois clipes do par PARAM de se mexer em 80% e ficam 0,65 s de pé,
    /// congelados — o add-on original emenda outra animação aí. Por isso o
    /// corte é em 0,80: a vítima cai a partir dali e o Miller volta pro idle.
    const float Corte = 0.8f;

    static AnimatorController Controlador(string nome, string fbx, string clipe, string estado, bool emendarQueda = false, bool emendarIdle = false)
    {
        var alvo = Clipe(fbx, clipe);
        if (alvo == null) { Debug.LogWarning("[FACA] sem clipe " + clipe); return null; }
        string arq = Pasta + nome + ".controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(arq);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(arq);
        var maquina = ctrl.layers[0].stateMachine;
        AnimatorState st = null;
        foreach (var filho in maquina.states) if (filho.state.name == estado) st = filho.state;
        if (st == null) st = maquina.AddState(estado);
        st.motion = alvo;
        st.speed = 1f;
        maquina.defaultState = st;

        // A vítima termina EM PÉ e parada: a queda vem da mesma animação de
        // morte que todo corpo do jogo usa (Death01), emendada no corte.
        if (emendarQueda)
        {
            var queda = FlorestaSelva.ControladorDeMorte();
            AnimationClip clipeQueda = null;
            if (queda != null) foreach (var c in queda.animationClips) clipeQueda = c;
            if (clipeQueda != null)
            {
                AnimatorState caido = null;
                foreach (var filho in maquina.states) if (filho.state.name == "Caido") caido = filho.state;
                if (caido == null) caido = maquina.AddState("Caido");
                caido.motion = clipeQueda;
                Emendar(st, caido, 0.22f);
            }
        }
        // O atacante sai do golpe direto pro idle: assim, quando o controlador
        // volta a ser o de andar, a pose já é a de parado e ninguém vê o troco.
        if (emendarIdle)
        {
            AnimationClip parado = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Modelos/Q_Animacoes/UAL1_Standard.fbx"))
                if (o is AnimationClip c && c.name == "Armature|Idle_Loop") parado = c;
            if (parado != null)
            {
                AnimatorState saida = null;
                foreach (var filho in maquina.states) if (filho.state.name == "Saida") saida = filho.state;
                if (saida == null) saida = maquina.AddState("Saida");
                saida.motion = parado;
                Emendar(st, saida, 0.3f);
            }
        }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    /// Liga (ou reajusta) a transição do golpe pro que vem depois. Reajustar
    /// importa: o controlador fica salvo no disco e uma transição velha, com o
    /// corte no lugar errado, sobreviveria à remontagem.
    static void Emendar(AnimatorState de, AnimatorState para, float mistura)
    {
        AnimatorStateTransition tr = null;
        foreach (var t in de.transitions) if (t.destinationState == para) tr = t;
        if (tr == null) tr = de.AddTransition(para);
        tr.hasExitTime = true;
        tr.exitTime = Corte;
        tr.duration = mistura;
        tr.hasFixedDuration = true;
        tr.interruptionSource = TransitionInterruptionSource.None;
    }

    /// Controlador de um golpe so: um estado que toca o clipe e emenda no
    /// seguinte. Serve pra estocada (vai pro idle) e pro que leva a facada
    /// (vai pra queda).
    static AnimatorController Simples(string nome, string clipeA, string estadoA, string clipeB, string estadoB, float corte, float mistura)
    {
        var a = ClipeDaBiblioteca(clipeA);
        if (a == null) { Debug.LogWarning("[FACA] sem clipe " + clipeA); return null; }
        string arq = Pasta + nome + ".controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(arq);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(arq);
        var maquina = ctrl.layers[0].stateMachine;

        AnimatorState Estado(string n, Motion m)
        {
            AnimatorState st = null;
            foreach (var f in maquina.states) if (f.state.name == n) st = f.state;
            if (st == null) st = maquina.AddState(n);
            if (m != null) st.motion = m;
            st.speed = 1f;
            return st;
        }

        var primeiro = Estado(estadoA, a);
        maquina.defaultState = primeiro;

        // o segundo estado: idle (nome do clipe) ou a queda da biblioteca
        Motion b = clipeB != null ? ClipeDaBiblioteca(clipeB) : null;
        if (b == null)
        {
            var queda = FlorestaSelva.ControladorDeMorte();
            if (queda != null) foreach (var c in queda.animationClips) b = c;
        }
        if (b != null)
        {
            var segundo = Estado(estadoB, b);
            AnimatorStateTransition tr = null;
            foreach (var t in primeiro.transitions) if (t.destinationState == segundo) tr = t;
            if (tr == null) tr = primeiro.AddTransition(segundo);
            tr.hasExitTime = true;
            tr.exitTime = corte;
            tr.duration = mistura;
            tr.hasFixedDuration = true;
        }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    /// Um estado so, em laco: serve pra pose continua (o forcejo).
    /// `costurar` recomeça o estado quando o clipe acaba: serve pros clipes
    /// que não vêm marcados como laço (Sword_Block) e que sem isso congelam
    /// no último quadro no meio da briga.
    static AnimatorController Laco(string nome, string clipe, string estado, bool costurar = false)
    {
        var c = ClipeDaBiblioteca(clipe);
        if (c == null) { Debug.LogWarning("[FACA] sem clipe " + clipe); return null; }
        string arq = Pasta + nome + ".controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(arq);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(arq);
        var maquina = ctrl.layers[0].stateMachine;
        AnimatorState st = null;
        foreach (var f in maquina.states) if (f.state.name == estado) st = f.state;
        if (st == null) st = maquina.AddState(estado);
        st.motion = c;
        st.speed = 1f;
        maquina.defaultState = st;
        if (costurar && !c.isLooping)
        {
            foreach (var t in st.transitions) st.RemoveTransition(t);
            var volta = st.AddTransition(st);
            volta.hasExitTime = true;
            volta.exitTime = 0.92f;
            volta.duration = 0.18f;       // costura macia: sem salto no recomeço
            volta.hasFixedDuration = true;
        }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    /// Clipe pelo nome, procurando nos dois arquivos da biblioteca.
    static AnimationClip ClipeDaBiblioteca(string nome)
    {
        foreach (var fbx in new[] { Pasta + "UAL1_Standard.fbx", Pasta + "UAL2_Standard.fbx" })
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbx))
                if (o is AnimationClip c && c.name == nome) return c;
        return null;
    }

    /// O guarda ganha um estado de ATIRAR na camada dos bracos: as pernas
    /// continuam andando/paradas e so o tronco levanta a arma e dispara.
    /// Sem isto ele matava voce em pose de caminhada, que nao conta nada.
    public static void ArmarGuardas()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Pasta + "AC_Toon.controller");
        if (ctrl == null) { Debug.LogWarning("[GUARDA] sem AC_Toon"); return; }
        var tiro = ClipeDaBiblioteca("Armature|Pistol_Shoot");
        if (tiro == null) { Debug.LogWarning("[GUARDA] sem clipe de tiro"); return; }

        bool temParam = false;
        foreach (var p in ctrl.parameters) if (p.name == "Atirar") temParam = true;
        if (!temParam) ctrl.AddParameter("Atirar", AnimatorControllerParameterType.Trigger);

        // camada dos bracos (a que ja segura o fuzil)
        int camada = ctrl.layers.Length > 1 ? 1 : 0;
        var maquina = ctrl.layers[camada].stateMachine;
        AnimatorState segurando = null, atirando = null;
        foreach (var f in maquina.states)
        {
            if (f.state.name == "SegurandoFuzil") segurando = f.state;
            if (f.state.name == "Atirando") atirando = f.state;
        }
        if (segurando == null) segurando = maquina.defaultState;
        if (atirando == null) atirando = maquina.AddState("Atirando");
        atirando.motion = tiro;
        atirando.speed = 1.15f;

        if (segurando != null)
        {
            AnimatorStateTransition ida = null;
            foreach (var t in segurando.transitions) if (t.destinationState == atirando) ida = t;
            if (ida == null) ida = segurando.AddTransition(atirando);
            ida.hasExitTime = false;
            ida.duration = 0.05f;
            ida.hasFixedDuration = true;
            ida.conditions = new[] { new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "Atirar", threshold = 0f } };

            AnimatorStateTransition volta = null;
            foreach (var t in atirando.transitions) if (t.destinationState == segurando) volta = t;
            if (volta == null) volta = atirando.AddTransition(segurando);
            volta.hasExitTime = true;
            volta.exitTime = 0.85f;
            volta.duration = 0.15f;
            volta.hasFixedDuration = true;
            volta.conditions = new AnimatorCondition[0];
        }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log("[GUARDA] animacao de tiro montada no AC_Toon");
    }

    static AnimationClip Clipe(string fbx, string nome)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbx))
            if (o is AnimationClip c && c.name == nome) return c;
        return null;
    }

    /// A mesma faca do chão, virada prefab pra aparecer na mão durante o golpe.
    static GameObject FacaDeMao(Transform raizTemporaria)
    {
        System.IO.Directory.CreateDirectory(PastaPrefab);
        var modelo = ItensSelva.Faca(raizTemporaria);
        modelo.name = "Faca_Mao";
        string arq = PastaPrefab + "Faca_Mao.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(modelo, arq);
        Object.DestroyImmediate(modelo);
        return prefab;
    }
}
