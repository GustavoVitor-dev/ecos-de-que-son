# Backlog — Ecos de Quế Sơn

Coisas adiadas de propósito durante o protótipo. Cada item traz **por que foi adiado** e **qual é o conserto**, pra ninguém precisar reconstruir o raciocínio depois.

Não é roadmap. Roadmap é o que falta construir (entidade, nível, rádio). Isto é o que já existe e precisa melhorar.

---

## Alta prioridade

### ~~1. A piscada não tem causa perceptível~~ — RESOLVIDO em 2026-09-07

O gatilho deixou de ser temporizador e virou **evento**. `StressSystem` expõe `event Action<float> Salto`, disparado quando o estresse sobe `limiarDeSalto` (12) ou mais de uma vez só. `BlinkSystem` assina isso em `AoSaltar`, com `cooldownDePico` de 6 s pra não empilhar piscada em cima de piscada, e ignora saltos que aconteçam dentro da faixa Calmo.

Na prática a lacuna passou a ser consequência do que o jogador fez: ser avistado, um guarda passar raspando, usar a faca (+32), ou uma beat de roteiro chamando `PicoDeEstresse`.

O ritmo de fundo continua existindo, mas recuado pra não competir com o gatilho por evento: **Alerta 0 (nunca), Ruptura 35 s, Hiper 14 s** — antes era 45/18/7.

**Falta o par disto:** item 2 abaixo. Sem som marcando entrada e saída, 0,13 s ainda pode passar batido mesmo com causa clara.


### 2. A lacuna não tem som

**Sintoma:** 0,13 s pareceu curto demais no teste, mas alongar além de ~0,25 s faz virar tela de carregamento.

**Causa:** o problema não é duração, é ausência de marcação. Só imagem, sem áudio.

**Conserto:** um som curto de corte de sinal na entrada e na saída da lacuna. Com áudio marcando, 0,13 s passa a registrar. Ver `stress_blink` em [AUDIO.md](AUDIO.md).

### ~~3. O guarda anda em linha reta, sem pathfinding~~ — RESOLVIDO em 2026-09-02

`GuardAI.MoveTowards` agora usa `NavMeshAgent`. Malha assada por um `NavMeshSurface` no objeto `Navegacao` (125 triângulos), agente com raio 0,45 e `baseOffset` 1 — esse offset é obrigatório porque a cápsula tem o centro na origem e afundaria meio corpo na malha sem ele.

Validado: as 6 pernas da patrulha são `PathComplete` e a rota do guarda até os documentos tem 9 curvas, contornando os barracões.

**Cuidado ao mexer no nível:** o NavMesh precisa de re-bake sempre que a geometria mudar. Se um guarda parar de andar depois de você editar o mapa, é isso. E `NavMesh.CalculatePath` falha se os pontos estiverem acima da malha — fazer `SamplePosition` antes de calcular.

---

## Média prioridade

### 4. Agachar não reduz visibilidade em campo aberto

Hoje agachar só esconde atrás de cobertura baixa (o alvo desce abaixo do topo das caixas). Em campo aberto, agachado e em pé são igualmente visíveis. Faltaria reduzir também o alcance ou a velocidade de detecção quando agachado. Ficou de fora porque cobertura já dava função à mecânica e empilhar sistema antes do básico assentar é como se perde controle do tuning.

### 5. Áudio é placeholder sintetizado

Todo som vem de `ProtoAudio.cs`, gerado em runtime. Lista completa de substituição em [AUDIO.md](AUDIO.md) — 76 sons, ~184 arquivos. Ao trocar, apagar `ProtoAudio.cs` inteiro.

### 6. HUD de debug precisa sumir ou virar interface

`PrototypeHUD.cs` mostra ruído, detecção, estresse e estado da IA em texto cru via `OnGUI`. Serve pra afinar número, não pra jogar. A intenção de design é que **quase nada** disso vire interface: a cor da lanterna do guarda já comunica o estado dele, o som já comunica o ruído. O que talvez sobreviva é uma representação diegética do estresse.

### 7. Nomes de protótipo

`PrototypeDirector`, `PrototypeHUD`, `Prototype_Greybox`. Renomear quando estabilizar. `SoldierController` tem esse nome porque `PlayerController` colide com uma classe dos exemplos do Visual Scripting — **esse não muda.**

---

## Baixa prioridade

### 8. Asset de URP dentro do pacote foi alterado por engano

Ao ligar sombra de luz adicional, o `FindAssets` pegou também `Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRenderPipelineAsset.asset`, que é template do pacote. Fica em `Library/PackageCache`, que é regenerável, e não afeta a renderização deste projeto — só o padrão de assets URP criados do zero. Some sozinho num resolve de pacote. Já corrigido no script pra buscar só em `Assets/`.

### 9. Checkpoints

Não existem. A run tem 60 segundos e reset instantâneo, que é melhor pra iterar. Vira necessário quando a slice chegar perto dos 15 minutos: checkpoint a cada 60–90 s de tempo-designer, restaurando posição e **estresse do checkpoint mais um incremento pequeno** — cicatriz sem espiral de morte.

### ~~10. `Noise.Emit` dispara áudio para qualquer emissor~~ — RESOLVIDO em 2026-09-07

O canal de ruído carrega origem agora: `Noise.Emit(pos, raio, NoiseSource)` com `Jogador / Isca / Entidade / Outro`. `NoiseAudio` só toca passo para `Jogador`. Os guardas continuam ignorando a origem — barulho é barulho para quem investiga.

Forçado pela isca sonora: sem isso, a isca caindo a 12 m tocaria som de passo do jogador.

---

Atualizado em 2026-09-07.
