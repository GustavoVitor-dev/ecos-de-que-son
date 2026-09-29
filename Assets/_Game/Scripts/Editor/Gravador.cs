using System;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

/// Grava a gameplay direto do editor, com som, apertando F9.
///
/// Serve pra mostrar o bloco pro grupo sem precisar de OBS nem de build: o
/// que entra no arquivo é exatamente a janela Game, no tamanho 1080p, com o
/// áudio do jogo — que neste jogo é metade da coisa (o rádio, a chuva, o
/// engasgo). Sai em MP4 em `Gravacoes/`, FORA de Assets: vídeo dentro de
/// Assets vira asset importado e entope o projeto.
///
/// Modo manual de propósito: começa e termina onde você mandar, não no
/// primeiro quadro do play.
public static class Gravador
{
    static RecorderController controlador;
    static string arquivoAtual;

    public static bool Gravando => controlador != null && controlador.IsRecording();

    [MenuItem("Ecos/Gravação/Gravar ou parar _F9", false, 0)]
    public static void Alternar()
    {
        if (Gravando) Parar();
        else Comecar();
    }

    // sem validador de propósito: fora do play o F9 tem que AVISAR que
    // precisa entrar no play, e não virar tecla morta sem resposta

    [MenuItem("Ecos/Gravação/Abrir a pasta", false, 20)]
    public static void AbrirPasta()
    {
        var pasta = Pasta();
        Directory.CreateDirectory(pasta);
        EditorUtility.RevealInFinder(pasta + "/");
    }

    /// Área de trabalho: é onde ele vai pegar o arquivo pra mandar pro grupo.
    /// Pelo SpecialFolder e não por caminho fixo — aqui a Área de Trabalho
    /// está redirecionada pro OneDrive, e "C:/Users/<nome>/Desktop" não existe.
    static string Pasta()
    {
        var mesa = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrEmpty(mesa) || !Directory.Exists(mesa))
            mesa = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Gravacoes"));
        return mesa.Replace('\\', '/');
    }

    static void Comecar()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[GRAVADOR] entre em play primeiro (Ctrl+P) e aperte F9.");
            return;
        }

        var pasta = Pasta();
        Directory.CreateDirectory(pasta);
        string nome = "ecos_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        arquivoAtual = pasta + "/" + nome + ".mp4";

        var ajustes = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        ajustes.SetRecordModeToManual();
        ajustes.FrameRate = 30f;
        ajustes.CapFrameRate = false;      // não segura o jogo se o PC não acompanhar

        var filme = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        filme.name = "Gameplay";
        filme.Enabled = true;
        filme.OutputFile = pasta + "/" + nome;          // o Recorder põe a extensão
        filme.EncoderSettings = new CoreEncoderSettings
        {
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
            Codec = CoreEncoderSettings.OutputCodec.MP4,
        };
        filme.ImageInputSettings = new GameViewInputSettings
        {
            OutputWidth = 1920,
            OutputHeight = 1080,
        };
        filme.AudioInputSettings.PreserveAudio = true;  // o rádio e a chuva vão junto

        ajustes.AddRecorderSettings(filme);
        RecorderOptions.VerboseMode = false;

        controlador = new RecorderController(ajustes);
        controlador.PrepareRecording();
        controlador.StartRecording();

        EditorApplication.playModeStateChanged -= AoSairDoPlay;
        EditorApplication.playModeStateChanged += AoSairDoPlay;

        Debug.Log("[GRAVADOR] gravando -> " + arquivoAtual + "   (F9 pra parar)");
        Avisar("● GRAVANDO — F9 pra parar");
    }

    public static void Parar()
    {
        if (controlador == null) return;
        if (controlador.IsRecording()) controlador.StopRecording();
        controlador = null;
        EditorApplication.playModeStateChanged -= AoSairDoPlay;

        Debug.Log("[GRAVADOR] pronto: " + arquivoAtual);
        Avisar("Gravação salva");
        // não abre a pasta sozinho: se ele parou pra continuar jogando, uma
        // janela do Explorer roubando o foco atrapalha mais do que ajuda
    }

    /// Sair do play com a gravação aberta deixaria um MP4 sem finalizar (e
    /// ilegível). O Recorder precisa do StopRecording antes de o play morrer.
    static void AoSairDoPlay(PlayModeStateChange estado)
    {
        if (estado == PlayModeStateChange.ExitingPlayMode && Gravando) Parar();
    }

    static void Avisar(string texto)
    {
        foreach (var j in Resources.FindObjectsOfTypeAll<EditorWindow>())
            if (j.GetType().Name == "GameView") { j.ShowNotification(new GUIContent(texto)); return; }
    }
}
