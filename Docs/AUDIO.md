# Ecos de Quế Sơn — Lista de Áudio

Documento de produção de som para a vertical slice de 15 minutos.
Todo áudio que existe hoje no projeto é **sintetizado em runtime** por `ProtoAudio.cs` e é descartável.

**Status:** levantado em 2026-09-02, na fase de greybox.
**Fontes recomendadas:** Sonniss GDC Bundle, BBC Sound Effects (uso educacional cobre o trabalho), Freesound. Ver o catálogo completo de fontes na memória do projeto.

---

## 1. Como substituir

O código não referencia arquivo nenhum hoje — os clipes nascem de função. Para trocar por áudio real:

1. Criar `Assets/_Game/Audio/` com as subpastas desta lista.
2. Trocar os campos `AudioClip` dos componentes para arrays serializados no Inspector.
3. Apagar `ProtoAudio.cs` inteiro.

Componentes que hoje geram som procedural:

| Script | O que gera | Onde está |
|---|---|---|
| `NoiseAudio.cs` | 3 passos do jogador | no `Player` |
| `GuardAudio.cs` | 3 passos do guarda + 1 grunhido | no `Guarda_01` |
| `Ambience.cs` | drone de ambiente em loop | nos `Managers` |

---

## 2. Especificação técnica

Estes pontos não são preferência, são o que quebra se ignorado:

- **Tudo que é 3D precisa ser MONO.** Fonte espacializada em estéreo perde a direção — e a direção é o que faz o jogador rastrear o guarda no escuro. Vale para passos de inimigo, entidade, rádio no mundo.
- Ambiente e música podem ser estéreo (`spatialBlend = 0`).
- 44.1 kHz, 16 bit. WAV para efeito curto, Vorbis para ambiente longo.
- **Load Type:** `Decompress on Load` para efeito curto, `Streaming` para ambiente acima de 30 s.
- **Variações:** mínimo 4 por passo. Menos que isso o ouvido reconhece o padrão e vira metralhadora.
- Ambiente precisa de **loop sem emenda** — cortar no zero-crossing ou fazer crossfade.
- Normalizar o pico, deixar headroom de 3 dB. Não comprimir na fonte: a mixagem é feita na engine.

---

## 3. Jogador

O ruído do jogador é a mecânica central. O volume tem que comunicar o raio de ruído que o guarda escuta, senão o jogador não aprende o sistema.

| ID | Descrição | Duração | Variações | Nota |
|---|---|---|---|---|
| `pl_step_mud_walk` | Passo em lama, andando. Sucção úmida, grave | 0,2 s | 5 | Bloco SELVA |
| `pl_step_mud_run` | Idem, correndo. Mais violento, chapinhado | 0,2 s | 5 | |
| `pl_step_mud_crouch` | Agachado na lama. Quase inaudível, roçar de tecido | 0,3 s | 4 | **Tem que soar sem denunciar** |
| `pl_step_dirt_walk` | Terra batida seca, andando | 0,2 s | 5 | Bloco ACAMPAMENTO |
| `pl_step_dirt_run` | Terra batida, correndo | 0,2 s | 5 | |
| `pl_step_dirt_crouch` | Terra batida, agachado | 0,3 s | 4 | |
| `pl_step_stone_walk` | Pedra e cascalho de túnel. Com cauda de reverb natural | 0,3 s | 5 | Bloco TÚNEIS |
| `pl_step_stone_run` | Pedra, correndo. Eco pronunciado | 0,3 s | 5 | |
| `pl_step_stone_crouch` | Pedra, agachado. Cascalho deslocando | 0,4 s | 4 | |
| `pl_breath_calm` | Respiração controlada, em loop | 4 s | 1 | Camada de estresse CALMO |
| `pl_breath_alert` | Respiração acelerada, em loop | 3 s | 1 | Camada ALERTA |
| `pl_breath_panic` | Respiração descontrolada, quase soluço | 2,5 s | 1 | Camada RUPTURA |
| `pl_heartbeat` | Batimento em loop, grave, sentido no peito | 1 s | 1 | Velocidade escala com estresse |
| `pl_cloth_crouch_in` | Roupa e equipamento ao agachar | 0,4 s | 2 | |
| `pl_cloth_crouch_out` | Ao levantar | 0,4 s | 2 | |
| `pl_hurt` | Impacto e grunhido curto ao ser pego | 0,6 s | 3 | |

---

## 4. Ferramentas

As cinco do pitch. Cada uma precisa soar o próprio custo — a faca é silenciosa, o sinalizador é um erro sonoro.

| ID | Descrição | Duração | Variações | Nota |
|---|---|---|---|---|
| `tool_flashlight_on` | Clique de lanterna militar ligando. Seco, mecânico | 0,15 s | 2 | Tecla F |
| `tool_flashlight_off` | Clique desligando, tom levemente diferente | 0,15 s | 2 | |
| `tool_flashlight_flicker` | Falha de contato, luz piscando | 0,8 s | 3 | Retirada de agência no pico |
| `tool_knife_draw` | Faca saindo da bainha. Deslize de metal contido | 0,5 s | 2 | |
| `tool_knife_kill` | Impacto íntimo. Curto, abafado, **sem estilização** | 0,8 s | 3 | "Silenciosa. Íntima. Irreversível." |
| `tool_pistol_shot` | Tiro de pistola. Seco, sem cauda heroica | 1,5 s | 3 | Ruído alto: erro tático |
| `tool_pistol_reload` | Recarga, manipulação de carregador | 2 s | 2 | |
| `tool_bait_throw` | Arremesso da isca sonora, assobio no ar | 0,4 s | 3 | |
| `tool_bait_land` | Impacto da isca no chão. **É o som que atrai** | 0,5 s | 4 | Precisa ser localizável |
| `tool_flare_ignite` | Sinalizador acendendo. Chiado químico | 1,2 s | 2 | |
| `tool_flare_burn` | Queima contínua, em loop | 3 s | 1 | |

---

## 5. Rádio

O rádio é ferramenta, ameaça e a decisão final. É o objeto mais carregado do jogo — merece o maior cuidado de som.

| ID | Descrição | Duração | Variações | Nota |
|---|---|---|---|---|
| `radio_static_low` | Chiado de fundo, em loop. Estático limpo | 4 s | 1 | Estado base |
| `radio_static_heavy` | Chiado denso e instável | 4 s | 1 | Sobe com o estresse |
| `radio_tune` | Varredura de sintonia, assobio de portadora | 1,5 s | 3 | |
| `radio_squelch_open` | Abertura de canal antes da voz | 0,3 s | 3 | |
| `radio_voice_human_01` | Voz de soldado, ordem quebrada por interferência | 3 s | — | Frases no roteiro do Callebe |
| `radio_voice_human_02` | Segunda transmissão, mais desesperada | 4 s | — | |
| `radio_voice_wrong_01` | **A mesma voz, com prosódia errada.** Palavras certas, cadência de quem aprendeu o som sem entender | 4 s | — | Aparição 4 da entidade |
| `radio_voice_wrong_02` | Idem, pior. A voz do próprio jogador | 4 s | — | Bloco CONTATO |
| `radio_answer` | Clique de PTT ao responder | 0,3 s | 1 | **Decisão final: responder** |
| `radio_off` | Desligamento. Corte seco pro silêncio | 0,5 s | 1 | **Decisão final: desligar** |

---

## 6. Inimigo humano

Arquétipo A é o patrulheiro que anda. Arquétipo B é o ouvinte estático que chama os outros.

| ID | Descrição | Duração | Variações | Nota |
|---|---|---|---|---|
| `enemy_step_walk` | Passo de bota, patrulha calma | 0,25 s | 5 | 3D, mono |
| `enemy_step_alert` | Passo apressado, investigando | 0,2 s | 5 | 3D, mono |
| `enemy_gear` | Equipamento balançando, em loop enquanto anda | 1 s | 3 | Denuncia posição antes do passo |
| `enemy_grunt_hear` | Reação curta a ruído. "Hm?" gutural | 0,4 s | 4 | **Ao entrar em Investigando** |
| `enemy_call_alert` | Grito de alerta chamando os outros | 1,2 s | 3 | Arquétipo B, ao disparar alarme |
| `enemy_idle_mutter` | Resmungo aleatório em patrulha | 2 s | 5 | Vietnamita; validar com o Callebe |
| `enemy_radio_chatter` | Rádio do inimigo ao longe, ininteligível | 4 s | 4 | Marca acampamento habitado |
| `enemy_shot` | Tiro de fuzil inimigo. Morte do jogador | 1,5 s | 3 | |
| `enemy_death` | Queda de corpo e equipamento | 1 s | 3 | |

---

## 7. Ambiente por bloco

Cada bloco do beat sheet precisa da própria camada. A transição entre elas é metade da narrativa.

| ID | Descrição | Duração | Nota |
|---|---|---|---|
| `amb_jungle_night` | Selva noturna: insetos, folhagem, vento distante | 60 s | Bloco 0:00–4:30 |
| `amb_jungle_wrong` | A mesma selva **com os insetos calados** | 30 s | Cruzar com a de cima quando a entidade se aproxima |
| `amb_camp` | Acampamento: gerador, lona batendo, fogo distante | 60 s | Bloco 4:30–10:00 |
| `amb_camp_panic` | Acampamento em pânico: gritos, correria, tiros ao longe | 20 s | Aparição 4, aos 9:00 |
| `amb_tunnel` | Túnel: gotejamento, pressão, respiração da terra | 60 s | Bloco 10:00–14:00 |
| `amb_tunnel_deep` | Mais fundo: infrassom, quase sem alta frequência | 60 s | 13:00 em diante |
| `amb_wind_layer` | Camada de vento isolada, para cruzar com as outras | 30 s | |
| `amb_distant_war` | Artilharia e helicóptero muito ao longe | 45 s | Lembra que a guerra continua sem você |

---

## 8. Entidade

Sete aparições, escalando em **agência** e não em volume. O som de cada uma tem que ser diferente do anterior — som repetido é o que mata o medo mais rápido que imagem repetida.

| ID | Descrição | Duração | Aparição |
|---|---|---|---|
| `ent_presence_01` | Quase nada. Uma frequência que você não sabe se ouviu | 3 s | 1 — percebida, ~1:40 |
| `ent_step_wrong` | Passo que não bate com nenhum inimigo visível. Peso errado, intervalo errado | 0,4 s ×4 | 2 — confirmada, ~4:00 |
| `ent_breath` | Respiração que não é a sua nem a de um humano | 4 s | 3 — contextualizada, ~6:30 |
| `ent_scream_distant` | O que faz o acampamento entrar em pânico. Ouvido de longe, abafado | 3 s | 4 — provada, ~9:00 |
| `ent_watch` | Presença sustentada, sem se mover. Drone com batimento irregular | 20 s | 5 — observando, ~11:30 |
| `ent_charge` | Ela fecha distância. **A quebra da regra** | 2,5 s | 6 — caçando, ~13:00 |
| `ent_contact` | Presença completa. Sem música, sem stinger de filme | 30 s | 7 — CONTATO, 14:00 |
| `ent_mimic_voice` | Voz humana reproduzida com fonemas quase certos | 3 s | ×3 | Usa nas 4, 6 e 7 |
| `ent_kill` | Morte pela entidade. Curto e sem catarse | 1,5 s | |

---

## 9. Percepção e estresse

O sistema que reescreve a experiência. Aqui o som **mente de propósito** — estes clipes existem para o jogador não conseguir separar o real do fabricado.

| ID | Descrição | Duração | Nota |
|---|---|---|---|
| `stress_tinnitus` | Zumbido agudo, em loop | 5 s | Entra em RUPTURA |
| `stress_pulse_low` | Pulsação surda, subgrave | 2 s | Camada por faixa de estresse |
| `stress_whisper` | Sussurro sem palavra reconhecível | 3 s ×5 | Alucinação sonora |
| `stress_false_step` | **Passo falso**: idêntico ao `enemy_step_walk`, disparado sem inimigo | 0,25 s ×5 | Precisa ser indistinguível do real |
| `stress_false_call` | Grito de alerta falso | 1,2 s ×3 | Faz o jogador fugir de nada |
| `stress_blink` | O piscar. Pálpebra e corte abafado do mundo | 0,25 s ×3 | **Verbo central do jogo** |
| `stress_hush` | Não é clipe: é o corte de todo o resto | — | `Ambience.Hush()` já implementado |
| `stress_return` | Volta do som depois do silêncio. Mais alto que antes | 2 s | |

---

## 10. Interface e feedback

Mínimo possível. O jogo comunica por diegese; interface só onde a diegese não alcança.

| ID | Descrição | Duração | Nota |
|---|---|---|---|
| `ui_detect_tick` | Tique sutil enquanto a barra de detecção sobe | 0,1 s | Frequência escala com a barra |
| `ui_detect_lost` | Descida da suspeita. Alívio | 0,5 s | |
| `ui_caught` | Ser pego. Impacto seco, sem música de derrota | 1 s | |
| `ui_document` | Recolher documento. Papel manuseado | 0,8 s | |
| `ui_checkpoint` | Quase inaudível. Confirmação de que o progresso ficou | 0,4 s | |

---

## Contagem

| Categoria | Itens | Com variações |
|---|---|---|
| Jogador | 16 | ~60 arquivos |
| Ferramentas | 11 | ~28 |
| Rádio | 10 | ~14 |
| Inimigo humano | 9 | ~35 |
| Ambiente | 8 | 8 |
| Entidade | 9 | ~14 |
| Percepção | 8 | ~20 |
| Interface | 5 | 5 |
| **Total** | **76 sons** | **~184 arquivos** |

## Prioridade

Se o tempo apertar, esta é a ordem em que eu cortaria de trás pra frente:

1. **Inadiável** — passos do jogador nas 3 superfícies, passos e grunhido do inimigo, os 3 ambientes de bloco, o piscar. Sem isso o jogo não funciona, só roda.
2. **Alto impacto** — rádio completo, entidade nas aparições 4, 6 e 7, silêncio e retorno.
3. **Enriquecimento** — variações extras, resmungos de patrulha, camadas de respiração e batimento.
4. **Cosmético** — interface, artilharia distante, queima do sinalizador.

Nota de escopo: passo com 3 superfícies × 3 marchas × 5 variações já são 45 arquivos só de passo. Se a slice não usar as três superfícies, corta uma e economiza 15.
