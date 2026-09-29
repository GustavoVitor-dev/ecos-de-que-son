# Testes pendentes — Ecos de Quế Sơn

Acumulados enquanto o jogo é construído. A ideia é rodar em lote quando a slice estiver jogável de ponta a ponta, em vez de parar a cada peça.

Cada item traz **o que testar** e **o que a resposta muda** — teste que não muda decisão não vale o tempo.

---

## Antes de qualquer playtest: `Ecos > Testar travessia da cena aberta`

Roda em 2 s e responde a única pergunta que invalida todo o resto: **dá pra chegar lá?** Faz um flood fill com a cápsula do jogador a partir do `SpawnPoint` e acusa cada beat, objetivo e a decisão final que ficarem inacessíveis.

Existe porque três blocos foram entregues impossíveis de terminar sem que ninguém percebesse: a rampa do mirante, as junções dos túneis (cada segmento era uma caixa fechada e a parede de topo de um cortava o interior do seguinte) e o portão de cipó da Selva e do Acampamento (7 cipós de 0,7 m a cada 1,2 m — sobrava 0,5 m de vão para um jogador de 1,0 m de largura).

Estado em 2026-09-07: Selva 11/11 marcos, Acampamento 6/6, Túneis 6/6.

## E a segunda: `Ecos > Medir vigilancia da cena aberta`

Responde **quanto do mapa está sendo olhado num instante qualquer** — não "varrido em algum momento da ronda", que engana feio: a Selva marcava 61% varrida e se atravessava andando, porque num instante só 6% estava sob olho.

Referência: abaixo de 10% é caminhada; 15–25% obriga a ler as rondas; acima de 35% vira tentativa e erro.

A ferramenta mede **dois sentidos**, porque os blocos não usam o mesmo:

| bloco | guardas | visto agora | ouvido andando | ouvido correndo |
|---|---|---|---|---|
| Selva | 6 | **12,4%** | 9,4% | 35,2% |
| Acampamento | 10 | **16,2%** | 14,1% | 51,2% |
| Túneis | 4 cegos | 1,3% | **66,3%** | **96,2%** |

A curva é intencional. Acima do chão a ameaça são olhos, e o número que vale é o da visão. Nos túneis os cegos enxergam 3 m e escutam ao dobro: 1,3% de visão **não quer dizer bloco fácil**, quer dizer métrica errada para o bloco. Lá o que cobra é o ruído — dois terços do túnel te escuta se você andar em pé.

---

## Bloqueantes: coisas que podem estar quebradas

### T1. Travessia completa da Selva
Do spawn até a saída, jogando de verdade. **Cronometrar.**
→ Alvo é 4:30. Se der menos de 2 min, falta motivo pra parar. Se passar de 8, come o orçamento dos outros blocos.

### T2. Rampa do mirante
Subir e descer pelas duas bordas extremas (x=−8 e x=+8), não só pelo meio.
→ Foi corrigida mas nunca jogada. Se travar de novo, o problema é o `CharacterController`, não a geometria.

### T3. Travessia completa do Acampamento
Idem, do spawn até a saída norte.
→ Alvo é 5:30. É o bloco mais longo do roteiro.

### T4. Rota de falha zero — os dois blocos
Atravessar **sem ser detectado nenhuma vez**, à mão.
→ É a regra que vem antes de todas. Validamos NavMesh, que prova navegação e não viabilidade. Se não existir rota limpa, o nível está quebrado por definição, não difícil.

---

## Ritmo e ensino

### T5. O contraste ensina?
Na Selva: ao sair do mato depois da clareira aberta de 24 m, dá pra sentir a diferença?
→ Se sim, o ritmo amigável/hostil está funcionando como didática. Se não, o contraste precisa ser mais agressivo.

### T6. As três passagens do Acampamento são diferentes?
Escolher uma, terminar, e perguntar: teria sido melhor por outra?
→ Se as três parecerem equivalentes, o problema é distribuição de luz e cobertura, não a planta.

### T7. Parar no centro da arena da Selva
O trecho hostil (z=8 a 18) faz você esperar?
→ Se atravessar direto, a janela da patrulha está larga demais.

---

## Ferramentas

### T8. A isca vale a pena?
Puxar a patrulha e passar por trás. Três unidades, alcance 14 m, ruído 20 m.
→ Se não mudar sua rota, a ferramenta é decorativa.

### T9. O ouvinte estraga a isca?
Ele escuta a 32 m — quase o mapa inteiro. Jogar isca perto dele provavelmente faz ele gritar e chamar todo mundo.
→ Pode ser profundidade boa (a ferramenta tem contraindicação) ou frustração. Sentir antes de eu mexer.

### T10. O preço da faca é sentido?
Matar dois e observar o estresse.
→ Duas mortes (+32 cada) atravessam o limiar de Ruptura em 60. O mundo deve começar a mentir. Se você não ligar os pontos, o preço não está comunicado.

---

## Horror

### T11. A piscada irrita ou tensiona?
Provocar um pico de propósito — deixar um guarda te avistar, ou usar a faca — e ver se a lacuna que vem em seguida lê como consequência daquilo. **Backlog 2.**
→ O gatilho virou evento: salto de 12 ou mais no estresse, cooldown de 6 s, silenciado na faixa Calmo. Se ainda parecer aleatória, o que falta é o som marcando entrada e saída, não o gatilho.

### T12. A regra da entidade fica clara?
Depois de 3 ou 4 aparições, dá pra sentir que ela nunca surge nem some enquanto você olha?
→ Não precisa ser consciente. Se der a impressão de "eu nunca vejo acontecer", funcionou — e é essa regra que vai ser quebrada uma vez lá no fim.

### T13. O pânico do acampamento assusta?
Ao sair da tenda de comando, os três guardas abandonam o posto e correm pro leste.
→ É a primeira vez que ela age no mundo, e age a seu favor. Se ler como bug em vez de evento, precisa de mais sinalização.

---

## Legibilidade

### T14. Dá pra jogar no modo Jogo?
A Selva e o Acampamento no escuro real, sem trocar pro modo Autoria.
→ Se precisar do modo autoria pra não se perder, a iluminação de jogo está apertada demais.

### T15. A trilha orienta?
Seguir as marcas sem olhar o minimapa mental.
→ Ela guia até o mirante e para ali de propósito. Se você se perder depois disso, falta landmark dentro da arena.

## Túneis e final

### T16. A sala em loop funciona ou lê como bug?
Atravessar a sala três vezes. A cada volta você reaparece na entrada sul com o lugar diferente: a lâmpada morreu, as caixas e as marcas de trilha mudaram de lugar, e na terceira ela está lá.
→ **É a beat mais arriscada do jogo.** Se ler como teleporte ou como falha, o efeito morre. A troca acontece dentro da lacuna justamente pra você nunca ver o mundo mudar — se você *ver* acontecer, o timing está errado.

### T17. As marcas de trilha mudando registram?
Na volta 2, elas trocam do lado esquerdo pro direito.
→ Só funciona porque a trilha foi estabelecida como confiável nos blocos 1 e 2. Se você não tinha reparado nelas antes, a mudança não significa nada — e aí o problema está lá atrás, não aqui.

### T18. A decisão final parece decisão?
Na câmara, `[R]` responder ou `[Q]` desligar. O rádio insiste a cada 12 s mas não decide por você.
Era `[D]` — que é andar pra direita. Dentro da câmara o jogador continua andando, então dar um passo pro lado encerrava o jogo. Trocado antes de qualquer um jogar.
→ Se a escolha parecer arbitrária, falta peso nas consequências. Se parecer óbvia, uma das duas está mal desenhada.

### T19. A slice roda inteira sem quebrar?
Selva → Acampamento → Túneis → decisão, de uma vez, cronometrando o total.
→ **Alvo: 15 minutos.** É o número do marco do pitch. E é a primeira vez que dá pra medir de verdade.

### ~~T20. O estresse acumula pela slice inteira?~~ — RESOLVIDO em 2026-09-07
`RunState` carrega estresse, iscas e mortes entre os blocos. Zera só quando a run recomeça de verdade: ao entrar na Selva vindo de outro lugar.

### T21. A persistência entre blocos se sente?
Anotar o estresse ao sair da Selva e conferir se é o mesmo ao entrar no Acampamento. Gastar uma isca no bloco 1 e checar se restam duas no bloco 2.
→ É o que faz a progressão de terror existir. E as três iscas serem **da run inteira** é decisão de design, não bug: recurso que se renova a cada cena vira rotina, não decisão. Se der aperto demais, o número sobe — mas o escopo continua sendo a run.

## Selva reconstruída (2026-09-07)

### T22. A arena virou decisão?
O rádio saiu do caminho e foi pro posto avançado em (-11, 19). São 12 m de desvio pra dentro de terreno patrulhado, e o mesmo tanto de volta.
→ Medido: 61% da arena é varrida por alguma ronda em algum momento, e os 39% nunca varridos **não conectam** nem até o rádio nem até a saída. Não existe mais corredor seguro o tempo todo — a rota de falha zero passou a existir no tempo, não no espaço. Se ficar frustrante em vez de tenso, o ajuste é o ângulo de visão dos guardas, não o número deles.

### T23. Pegar o rádio e sair vivo
A coleta alerta os três guardas. São 24 m até o portão com a patrulha convergindo.
→ É a parte que deveria ser a mais tensa do bloco e antes era a mais vazia. Se der pra sair andando, o alerta está fraco demais.

### T24. A abertura funciona ou atrapalha?
Quatro planos, ~19 s no total, câmera de jogo com tarjas. `Esc` ou `Espaço` pula.
→ Se der vontade de pular na primeira vez, está longa. Se você pular e não sentir falta de nada, ela não está contando o que precisa.

### T25. A cartela entre blocos dá respiro?
Fade de 1,2 s, cartela de 2,8 s ("II. O ACAMPAMENTO"), abertura de 1,6 s.
→ Antes era corte seco. Se parecer lento, corta o tempo da cartela; se parecer que travou, o problema é o fade não estar visível.

### T26. A regra da entidade fica dita?
A primeira aparição é **enquadrada**: ela fica na brecha da mata pela qual você vai descer do mirante. E o HUD diz uma vez só: "ELA NÃO ESTAVA AÍ ANTES DE VOCÊ PISCAR."
→ A regra estava implementada e invisível desde o começo. Se depois dessas duas frases você ainda achar que as aparições são aleatórias, o problema é a piscada não registrar (backlog 2, o som).

## Acampamento reconstruído (2026-09-07)

### T27. As sentinelas mudam o jeito de jogar?
Cinco postos fixos olhando para o **sul**, de onde você vem: as três passagens do arame, a boca da tenda de comando e o corredor norte. Guarda que anda olha para onde anda — por isso as rondas leste-oeste nunca olhavam para você.
→ Se der pra passar reto por um posto, o arco ou a direção dele estão errados. Se os cinco parecerem parede, reduzir o arco antes de tirar gente.

### T28. O alcance de 12 m parece injusto?
Era 8,5 m. **Isto é o que você reclamou uma vez** ("ele me pegou de longe demais") — mas aquilo era a *velocidade* de detecção a distância, e a curva quadrática já resolveu: na borda do cone ainda são ~6 s para encher a barra.
→ Se voltar a parecer injusto, o problema é a curva, não o alcance. Sinal claro: ser pego **sem ter tido tempo de sair da linha de visão**.

### T29. As duas patrulhas norte-sul cruzam sua marcha?
`Guarda_Flanco_O` e `Guarda_Flanco_L` sobem e descem os flancos internos, então os cones cortam a direção em que você anda.
→ É o conserto do problema estrutural. Se ainda der pra subir colado numa borda, faltou fechar mais flanco.

## Túneis: o bloco do som (2026-09-07)

### T30. A inversão de sentidos se comunica?
Na boca do túnel: "AQUI EMBAIXO ELES NÃO ENXERGAM. ELES ESCUTAM." e, 4,5 s depois, "[CTRL] AGACHADO VOCÊ NÃO FAZ RUÍDO NENHUM."
→ Sem isso o jogador leva a tática do acampamento para baixo da terra — andar pela sombra, esconder da luz — e morre sem entender por quê. Se a mensagem passar batido, ela precisa virar evento, não texto.

### T31. Agachar o bloco inteiro é chato?
66% do túnel te escuta andando. Agachado o ruído é zero, mas a velocidade cai para 1,4 m/s.
→ **É o risco real deste bloco.** Se atravessar agachado for tedioso em vez de tenso, o conserto é reduzir o `hearingSensitivity` de 1,7 ou encurtar os túneis — não tirar os cegos.

### T32. Dá para ouvir onde eles estão?
Os cegos têm uma luz vermelha fraca de 3 m e nada mais. No escuro você só sabe da existência deles pelo som dos passos.
→ Se não der para localizá-los pelo áudio, isso vira injustiça. É o teste que mais depende de o áudio placeholder ser substituído (backlog 5).

### T33. A lanterna ainda serve para alguma coisa?
Contra quem não enxerga, ela não te entrega (`flashlightRangeMultiplier` = 1 nos cegos). Serve só para você não esbarrar nas coisas.
→ Se isso ficar claro sozinho, o bloco ensinou a própria regra. Se o jogador continuar com medo de acender, a leitura falhou.

## O que o piloto automático achou (2026-09-07)

`AutoPlay` no `Managers` atravessa o bloco sozinho pela NavMesh, fazendo o ruído da marcha escolhida, e anota beats, detecção, estresse, aparições e voltas do loop. **Não substitui playtest** — ele não sente medo nem acha nada chato. Pega a outra metade.

### Sentinela que não voltava ao posto
Primeira passada em pé: **33 mortes em 90 s**, sempre pelo mesmo guarda, nunca passando de z=−18. O modo sentinela só mandava o agente parar; ao investigar um ruído ela saía do posto e ficava de guarda onde parou. Uma delas caminhou até a rota obrigatória e virou muro permanente.
→ `Vigiar()` agora volta ao posto antes de varrer. Segunda passada: 10 capturas em 300 s, espaçadas ~30 s.

### `Travado` é um bool só
Cutscene e piloto escrevem nele, e quem trava por último ganha — o piloto dirigiu por cima da abertura. No jogo isso é a mesma fragilidade entre transição e cutscene sobrepostas. **Ainda não consertado**: precisa virar contagem de travas, não um booleano.

### Cobertura não vê gargalo
Medi quanto da rota obrigatória (96 m) cada guarda cobre, e apareceu o que os 12,4% escondiam:

| guarda | cobre | maior trecho seguido |
|---|---|---|
| Guarda_Patrulha | 41 m | 22 m |
| Guarda_Portao | 19 m | 14 m |
| Sentinela_Portao | 12 m | 12 m |

**Posto vigia sempre; ronda tem janela.** Uma sentinela cobrindo 12 m seguidos do único corredor não é dificuldade, é parede. A `Sentinela_Portao` saiu da linha do corredor e passou a vigiar de través.

### T34. Regra de posicionamento de sentinela
Nenhum posto pode cobrir mais de ~6 m seguidos da rota obrigatória, e nenhum posto deve olhar **para dentro** do corredor que o jogador precisa usar — sempre de través, com o jogador podendo entrar pela borda oposta.
→ É a regra que faltava quando eu espalhei as sentinelas pela primeira vez.


---

## Passada de assets reais — 2026-09-08

O usuário rejeitou o visual três vezes. A terceira reclamação foi específica:
*"arbustos, modelos de soldados, muita coisa low poly ainda"*. Medi antes de
mexer, e o diagnóstico não era o que eu vinha tentando consertar.

### T35. O nível inteiro ainda era graybox
Inventário de material por cena. As seis texturas PBR do Poly Haven estavam
baixadas desde a sessão anterior e **não estavam ligadas em nada**:

| material | usos | era | virou |
|---|---|---|---|
| `gb_Chao` | o chão inteiro, 40 × 90 m | cor chapada | `forest_floor` |
| `M_ParedeDeRocha` | 95 (túneis inteiros) | cor chapada | `cliff_side` |
| `M_Madeira` | 63 (cerca, caixotes) | cor chapada | `brown_planks_07` |
| `M_Casca`, `M_Pedra`, `M_Limite`, `M_ChaoDeTerra` | 683 | cor chapada | bark / cliff / mud / dirt |

A "placa verde no meio da cena" que eu estava caçando como bug era o chão sem
textura ocupando meia tela. **A lição:** quando alguém diz que está feio, medir
o que existe antes de comprar asset novo. Eu tinha baixado 17 modelos
fotoscaneados e nenhuma textura estava aplicada em nada.

### T36. Árvore realista não cabe por instância
`pachira_aquatica_01` tem 77 mil triângulos. 638 árvores dariam 49 milhões.
Regra adotada: **modelo fotoscaneado a até 9 m da área andável, teto de 150 por
cena**; o resto continua Quaternius, escurecido pra virar silhueta. O que lia
como desenho não era a malha, era o verde chapado aceso — no escuro, mata
distante é recorte, não detalhe.

`root_cluster_01` foi cortado da palheta: 225 mil triângulos por peça, 82 peças,
18 milhões só nele. Não paga o que entrega no chão.

### T37. Densidade tem que respeitar a leitura de esconderijo
Rasteira (musgo, capim, gravetos — até 0,3 m) espalhada pelo chão todo: 900 por
cena. Arbusto alto (`shrub_02`, `shrub_04`, samambaia) **só dentro das moitas**.
Se arbusto alto virar decoração de chão, o jogador perde o sinal de "aqui dá pra
sumir" e o volume de ocultação deixa de significar alguma coisa.

### T38. Copa e câmera de cima são um conflito de desenho
Selva com árvore de verdade e câmera a 62° = tela 100% folha, personagem
invisível. Três tentativas:

1. **Túnel por raycast entre câmera e jogador** — abriu 9 copas de 132. Inútil:
   a tela toda é copa, não só a linha da câmera.
2. **Copa fora da culling mask da câmera de cima** — jogável, mas a selva virou
   floresta de galho seco, porque de cima só sobrava tronco.
3. **Clareira de raio em volta do jogador** (6,5 / 12 / 18 m) — não funciona por
   geometria: a 62° a tela cobre ~34 m de profundidade, então nenhum raio
   razoável limpa o quadro sem apagar a mata inteira.
4. **Copa apagada onde a área jogável passa, mantida onde o jogador nunca vai** —
   o critério certo não é distância ao jogador, é distância ao corredor. É
   estático: não custa nada rodando e não pisca. Raio de corredor 11 m, medido
   pela mesma `TravessiaCheck.AreaJogavel` dos testes.

Só a **folha** some; o tronco fica, porque é ele que diz onde tem cobertura.
Layer `Copa` (8) separa folha de tronco, e a câmera de primeira pessoa continua
vendo tudo.

### T39. Soldado nu com uniforme pintado continua soldado nu
Primeira tentativa: tingir o material do `Superhero_Male` de verde-oliva. O mesh
base do Quaternius é um corpo **sem roupa** — pintar não veste. E trocar o
material por cor chapada ainda apagou a textura e deixou um manequim liso.

Conserto: trocar o corpo por `Swat` do *Ultimate Modular Men*, que é o mesmo
esqueleto universal do Quaternius e cai direto no `AC_Guarda` sem retarget.
Jogador virou `Adventurer` pra ser distinguível de cima. Rifle (`AssaultRifle_*`)
preso no osso `RightHand` do avatar humanoide, sem colisor — arma com colisor
entraria no flood de travessia e na linha de visão dos guardas como parede.

### T40. Bounds de SkinnedMeshRenderer mentem no editor
Escalei os corpos por `renderer.bounds.size.y` e todos os 10 guardas reportaram
altura idêntica: o bounds vem da bind pose, não da pose atual. O que não mente é
o **osso** — `Head.position.y - LeftFoot.position.y`. Corpos ficaram a 1,72 m.

### T41. Os três testes depois da passada
Rodados nas três cenas com tudo aplicado:

| bloco | travessia | visto agora | ouvido andando | gargalos |
|---|---|---|---|---|
| Selva | 11/11 marcos, 1529 m² | 10,0% | 8,1% | nenhum tranca |
| Acampamento | 6/6 marcos, 1554 m² | 14,9% | 12,2% | nenhum tranca |
| Túneis | 6/6 marcos, 817 m² | 0,0% | 68,6% | nenhum tranca |

### Custo do que entrou
Selva 29,8 M triângulos / 11.312 renderers; Acampamento 26,8 M / 10.506.
Instancing ligado em 63 materiais; 8.091 malhas rasteiras deixaram de projetar
sombra na Selva (musgo de 10 cm não precisa). **Isto ainda não foi medido
rodando** — se travar, o corte mais barato é baixar o teto de 150 pachiras.


### T42. O mesmo raycast que enterrou as árvores agora fez elas voarem
O usuário perguntou por que as árvores estavam flutuando. Estavam: **149 das 150
subiram de 6 a 8,7 m.**

Pra achar o chão eu joguei um raio de cima pra baixo a partir da posição da
árvore — e ele bateu no **colisor de tronco da própria árvore**, que tem a altura
inteira. O topo do cilindro virou "chão". Mesma família do bug que enterrou as
árvores na sessão passada, invertida.

Pegou também 33 plantas da folhagem. A rasteira escapou por acidente: eu tinha um
filtro `if (h.point.y > 0.4f) continue` que descartava qualquer acerto alto.

**Regra:** antes de qualquer raio de assentamento, desligar os colisores de tudo
que é alto e fino na cena — não só o do objeto corrente, porque o vizinho
encostado também engana. E conferir depois: contar quantos ficaram flutuando,
enterrados e no chão, em vez de confiar que a conta deu certo.

Verificação final, com o colisor de tronco desligado e o raio partindo de 1 m
acima do pé (pra não bater no teto do túnel):

| bloco | árvores | folhagem | rasteira | corpos |
|---|---|---|---|---|
| Selva | 150 no chão | 458 no chão | 900 no chão | 8/8 |
| Acampamento | 150 no chão | 206 no chão | 900 no chão | 12/12 |
| Túneis | — | — | — | 5/5 |

### T43. Ouvinte não é guarda
`Ouvinte_Objetivo` e `Ouvinte_Comando` têm `Listener`, não `GuardAI` — escaparam
da troca de corpo e continuaram manequim nu, um deles 1 m enterrado. Trocar corpo
por tipo de componente só pega quem tem aquele componente; o certo é varrer por
`Animator` humanoide e decidir depois.


---

## Passada de level design na Selva — 2026-09-09

Reclamação do usuário, literal: *"dá pra passar tudo pelo lado ignorando tudo,
não tem objetivo, não tem desafio, só uma linha reta"*. Ele estava certo em
todos os pontos, e a minha própria ferramenta vinha dizendo isso a cada rodada:

```
[VIGILANCIA] VISTO agora: 10.0% — FROUXO — da pra atravessar sem prestar atencao
[GARGALO]    nenhum guarda tranca a rota obrigatoria.
```

Eu tratei "nenhum guarda tranca a rota" como **aprovação**. Não é. É metade da
frase: quer dizer que não há parede *e* que não há gate nenhum.

### T44. Existia uma faixa livre de 13 m do spawn até a saída
Os 6 guardas estavam todos em x negativo (de −15 a −4) num mapa que vai de
x=−20 a x=+20. O lado leste inteiro, de z=−43 a z=48, não tinha ninguém.
Dava pra encostar em x=+15 e andar reto até o fim.

**Regra:** medir a cobertura por FAIXA do mapa, não só a média. Média de 10%
esconde um corredor de 13 m com 0%.

### T45. O objetivo não existia como mecânica
Não havia **uma** `MissionZone` na cena. O Rádio era um objeto decorativo, a
fase da missão nunca saía de `Documentos`, e `Beat_9_Saida` disparava
`CarregarCena` sem olhar nada. Andar até z=43 encerrava o bloco.

Consertado com: `Zona_Radio` (Documentos), `Zona_Extracao` em z=41 (2 m antes
da saída, pra ordem ser garantida pela geometria e não pela ordem de
`OnTriggerEnter`), e um campo novo `exigeFaseDaMissao` no `BeatTrigger`.

Verificado **rodando**, os três estados:

| ação | disparou? | HUD |
|---|---|---|
| sair sem o rádio | não | "O RADIO AINDA ESTA LA. NAO SAIO SEM ELE." |
| sair só com o rádio | não | "SAIR VIVO" |
| depois da extração | condição passa | "MISSAO CONCLUIDA" |

### T46. Campo aberto não é nível
A Selva era 40 m de largura constante — sem forma, sem ritmo. Construí um perfil
de meia-largura ao longo de z, alternando aperto e abertura:
6 m no spawn → 11 na primeira área → **6 (aperto)** → 9 no mirante →
**6 (aperto)** → 14 na bacia do objetivo → **7 (aperto do portão)** → 6 na saída.

92 volumes de colisão invisíveis (a mata densa já é o visual), NavMesh reassada.
Área andável caiu de 1529 para 1206 m².

O teste de travessia pegou na primeira tentativa: o aperto do portão cortou o
Rádio (x=−10,2) para fora por 20 cm. **É pra isso que a ferramenta existe.**

### T47. Posto aperta, ronda pressiona
Subir a cobertura com posto vira parede: na primeira passada, 3 dos 3 postos
cobriam 7 a 12 m seguidos da rota. Apertar arco e alcance zerou as paredes mas
derrubou a vigilância para 13,4% — de volta a "FROUXO".

**A pressão veio de ronda, não de posto.** Dois batedores novos nos trechos que
não tinham ninguém (z −24..−16 e a bacia oeste) levaram a 18,5% sem trancar nada.

| | antes | depois |
|---|---|---|
| visto agora | 10,0% FROUXO | **18,5% BOM — obriga a ler as rondas** |
| guardas que trancam | 0 (porque não havia gate) | 0 (com gate) |
| guardas | 6, todos em x<0 | 8, cobrindo os dois lados |

### T48. Waypoint na NavMesh não prova conexão
`Guarda_Bacia` saiu com 1 de 4 pernas navegáveis. `NavMesh.SamplePosition` acha
qualquer retalho de malha, inclusive ilha solta atrás de pedra. O que prova
conexão é a área que o flood fill do jogador alcança a pé — ancorar os waypoints
em `TravessiaCheck.AreaJogavel` resolveu (4/4).

### T49. Uma planta do Poly Haven são seis plantas
O FBX traz as variantes como malhas irmãs (`nettle_plant` tem medium_a/b,
small_a/b, tall_a/b). Eu instanciava o prefab inteiro: **seis moitas sobrepostas
no mesmo ponto**, 31 mil triângulos por "planta", e visual de amontoado.

Reduzir para uma variante sorteada por instância: 10.989 malhas apagadas,
**48,5M → 25,9M triângulos, 16.103 → 5.114 renderers.**

### T50. Vegetação é estrutura, não enfeite
*"Árvores são basicamente paredes que você atravessa"* (Level Design Book).
Distribuição por papel, com o gradiente medido a partir da **trilha autorada**:

| distância da trilha | papel |
|---|---|
| >8 m | 204 moitas densas — fecham o mapa |
| 4–8 m | 103 moitas de borda |
| 2–4 m | 52 moitas ralas |
| <2,2 m | **limpo** — caminho batido não tem mato |

Cada moita é dominada por uma espécie (75%) e cresce mais no centro que na borda.
Primeira tentativa saiu invertida porque medi pela área andável em vez da trilha.

### T51. Não existe animação de carregar fuzil
`Idle_Lantern_Loop` é mão caída com objeto pequeno; `Walk_Carry_Loop` é carregar
objeto. Encaixar fuzil nessas mãos ia dar errado em qualquer ângulo — foram três
tentativas de rotação antes de eu aceitar isso.

Solução: **fuzil a tiracolo nas costas**, preso ao osso do peito, diagonal com o
cano subindo pelo ombro. Não depende da pose da mão. O jogador ficou sem rifle
(ele tem faca e lanterna, não tem tiro) e carrega o lampião, que é exatamente o
que a animação dele pede.

### Luz
Ambiente (0.018, 0.020, 0.028) e luar 0.05 deixavam o cenário novo invisível.
Subiu para (0.055, 0.065, 0.088) e 0.22, luar azulado. Continua noite fechada,
mas a superfície tem leitura.

---

Atualizado em 2026-09-09.

### T52 — Soldados Toon no lugar do modelo antigo (2026-09-18)
Guardas e cegos das três cenas (Selva 9, Acampamento 11, Túneis 8) trocados pelo
ToonSoldier_WW2. Controlador novo `AC_Toon`: parado = infantry_guard_idle,
andando = Walk_Carry_Loop. O fuzil já vem no modelo, nas duas mãos, então a
bandoleira improvisada saiu junto com o corpo antigo.
Medido **em play**: pé contra NavMesh 0,00 m em todos os guardas da Selva (um em
−0,08); tornozelo 7–9 cm acima do chão nos 8 cegos dos túneis. Patrulhas entram
em "Andando" a 1,5 m/s. Foto de perto confirma uniforme oliva, capacete e fuzil
segurado corretamente. O jogador continua Adventurer com lampião, sem fuzil.

### T53 — Estilo Darkwood: cone de visão e folha que desbota (2026-09-18)
`VisaoDarkwood`: 240 raios por frame a partir do jogador; cone de 100° e 16 m
para onde o mouse aponta, mais um círculo de 1,8 m em volta do corpo. Fora disso,
malha escura com grão (shader `Ecos/Escuridao`). Guardas, cegos, entidade e o
rastejante fora do cone somem (`forceRenderingOff`) — dá pra ouvir, não ver.
Medido em play na Selva: 7 de 8 guardas escondidos com o jogador olhando pro norte.
Paredes, troncos e colisores de mata cortam o cone.
`FolhagemTransparente`: planta entre câmera e jogador troca por cópia
transparente do material e desce a 22% de alfa; volta sozinha. Em play, dentro
do Mato_005: 2 arbustos desbotados, folha visivelmente transparente na foto A/B.
Primeira versão não pegava nada: arbusto do Poly Haven mistura folha e galho na
mesma malha, e o filtro exigia só folha.
Luz de jogo subiu (Selva ×4, Acampamento e Túneis ×3) porque a escuridão agora
é do cone; sem isso o cone só mostrava o círculo do lampião.
Câmera 80° (era 62/70), fundo preto, pós-processamento `VP_Darkwood`
(dessaturado, vinheta, grão).
O AOS Fog of War não foi usado: grade máx. 128×128 de 1 m vira bloco borrado
com esse zoom e só faz círculo.

### T54 — Tela fecha dentro do mato (2026-09-18)
`MatoNaTela`: com `PlayerConcealment.InConcealment`, 14 silhuetas de folha
(textura de alfa da pachira, `T_FolhaNaTela`) entram pelas bordas e um volume
local escurece (exposição −0,45), dessatura, aperta a vinheta e puxa um pouco de
aberração. Força = quanto ele está escondido: parado no mato 0,85, correndo cai
pra 0,45. Em play, dentro do Mato_005: peso 0,85, folhas visíveis na foto A/B.
Primeira versão cobria o jogador; segunda deixava as folhas só na borda, onde o
cone já é preto e elas sumiam. Ficou: folha invade a parte iluminada e para num
raio livre de 150 px em volta do centro.

### T55 — Folha rosa (2026-09-18)
Relato do usuário: folha rosa no jogo. Causa: `FolhagemTransparente` guardava as
cópias transparentes num cache `static`; o projeto entra no Play sem recarregar
domínio (EnterPlayModeOptions = 3), então da 2ª sessão em diante o cache
devolvia material destruído e a folha ficava com material nulo = rosa.
Correção: cache por instância, checagem de nulo, cópias destruídas no OnDestroy.
Verificado em duas sessões de Play seguidas no Mato_005: 0 materiais nulos em
ambas (2 e 26 folhas desbotadas), sem rosa na imagem.

### T56 — Personagens: escala, fuzil com duas mãos, jogador novo (2026-09-18)
Reclamação: soldados gigantes, fuzil numa mão só, animação do jogador bugada.
Medido: malha do soldado Toon 2,0 m contra 1,68 m do jogador (proporção de
desenho: cabeça e capacete grandes). Andando, o soldado usava Walk_Carry (andar
carregando caixa): o fuzil é pesado só no osso da mão direita, então a esquerda
soltava.
- Soldados −14% (28 nas três cenas). `AC_Toon` ganhou camada "Bracos" com máscara
  de tronco/braços tocando infantry_guard_idle: pernas andam, braços seguram.
  Em play: mãos a 0,38 m (antes 0,44–0,66), fuzil nas duas mãos na foto.
- Jogador virou o mesmo soldado Toon, com malha própria sem as 6 peças do fuzil
  (`Toon_Jogador.asset`) e farda mais clara. Tirar o capacete deixou buraco na
  cabeça vista de cima: capacete voltou.
- `AC_Jogador`: parado/andar/trote/corrida por velocidade e agachado
  (parado/andando) pelo Ctrl — antes não existia agachado no corpo.
  Braço direito com Idle_Lantern.
- `SegurarLanterna`: lampião pendurado em pé na mão, balançando com o passo
  (antes nascia dentro da palma e não aparecia). 0,40 m em pé.
Em play, lado a lado com a Sentinela_Portao: 1,75 m contra 1,67 m.

### T57 — Selva vira floresta de verdade (2026-09-18)
Reclamação: chão igual do começo ao fim, só árvore, sem relevo nem construção.
Novo construtor `Ecos/Selva/Construir floresta` (`Editor/FlorestaSelva.cs`),
re-executável (alturas originais gravadas em `_Game/Terreno/*.json`):
- Terreno 180 × 200 m no lugar do cubo de 40 × 90: micro-relevo, mirante de
  caixote vira morro de 2 m com barranco de pedra, córrego com água cortando o
  bloco (e virando vale lá fora), 5 crateras de bomba com poça dentro do bloco
  e 8 fora, montes, morros e serra em volta.
- Chão em 8 camadas (texturas do ForestVision): serapilheira, cobertura verde,
  trilha de terra batida seguindo o traçado real, raízes, lama, terra de barranco,
  rocha com musgo por inclinação, musgo.
- Dentro do bloco: 16 troncos caídos, 14 pedras grandes + 70 pequenas, 18 tocos,
  5 raízes, 8 árvores mortas, galhos, cascas, 40 cogumelos, 14 samambaias
  arbóreas, palmeiras (fronde na camada Copa), lixo de guerra e arame farpado.
- Fora: 900 árvores em moitas + 700 arbustos nos morros.
- Protótipos trocados mantendo o colisor: rochedo → rochas, ruínas → cabana e
  rochedo, caixotes → tambores verde-oliva e munição, 21 colunas → árvores,
  rádio → rádio, cápsula do corpo → soldado caído (Death01 congelado, `MortoParado`),
  marcas de trilha e paredes de limite invisíveis. Só o portão de cipó segue
  cilindro (é o portão da missão).
- 3192 objetos da cena reassentados no chão novo; 143 plantas tiradas da água.
- Luz: neblina 0,03 → 0,006 (era por distância da câmera, 25 m acima), pós com
  exposição 0,8 e contraste 10, ambiente 0,16/0,18/0,22. Medido: a névoa e o pós
  cortavam metade do brilho dentro do cone.
Verificado: travessia 13/13 marcos alcançáveis (área 1529 → 1193 m²: tronco e
pedra agora ocupam chão). Vigilância: "FROUXO" (visto 14,5%, antes 10%) e ouvido
"ANDAR É SEGURO" (12%) — o bloco NÃO está fechado em jogabilidade, é visual.
Em play: córrego, cabana, posto com corpo e tambores legíveis na câmera do jogo.

### T58 — Mata organizada, caminho legível e morte de verdade (2026-09-20)
Reclamações: volume de árvore alto e aleatório, caminho não se lê, falta coisa
de floresta, falta canto de tela fechado, e o morto virava placeholder.
- `Reorganizar` no construtor: a árvore não some, **muda de lugar**. 90 a 189
  (depende da rodada) saem de perto da rota e vão para (a) parede lateral,
  (b) "portas" — pares de grupos apertando a rota com vão de ~6,8 m em 9 pontos,
  (c) clareira em volta de rádio, portão, rochedo e extração. Árvore COM copa vai
  para o anel de 9,4–12,6 m, que é o que entra pelo canto da tela: 68 copas nesse
  anel depois da última rodada.
- `LimparChao`: 161 plantas rasteiras saíram de cima da trilha (antes a trilha
  pintada ficava escondida debaixo da folhagem) e 144 saíram da água.
- Acrescentado: 150 arbustos (densos na borda, ralos no meio), 55 pilhas de folha
  seca (arbusto achatado com material marrom), beira de trilha com pedra/tora/toco
  alternando os lados, 26 troncos caídos, 16 árvores mortas, 6 soldados caídos,
  3 covas com tábuas cruzadas, 7 focos de bruma rente ao chão (partículas).
- Trilha mais clara no chão (tinta da camada de terra batida 0,95) e faixa de
  pintura mais larga.
- Morte: o pacote de soldados é a versão DEMO e só tem 4 animações (guarda parado,
  combate parado, correr, atirar) — não tem morte nem cadáver, isso é da versão
  paga. A morte usa Death01 da biblioteca do projeto. `MeleeKill.Abater` destrói
  IA/agente, desliga colisores e luz, troca o controlador por AC_Morto e congela no
  último quadro (`MortoParado`). Medido em play: clipe em 1,00, velocidade 0,
  Listener destruído, colisor desligado. Antes virava cápsula branca.
- Tambor vermelho de posto de gasolina saiu; ficou só o tambor escuro.
Verificado: travessia 13/13 marcos, 1259 m². Vigilância passou de "FROUXO" para
**"BOM — obriga a ler as rondas"** (visto 18,5%). Ouvido segue "ANDAR É SEGURO".

### T59 — Espécies de selva, chão 2D, galhos e visão presa à lanterna (2026-09-20)
Reclamações: árvore de desenho verde e retorcida, folha quadrada, montes
vermelhos no chão, chão vazio, círculo do personagem grande demais, visão boa
demais sem lanterna, e falta de galho/copa fechando a tela.
- **Folha quadrada / árvore feia**: eram as 418 `CommonTree` (Quaternius). Fotografei
  o catálogo: a folha delas é um quadrado verde chapado. `TrocarEspecies` troca o
  modelo (mantendo a cápsula-tronco com colisor) por mata de selva do ForestVision,
  escolhida por **zona**: palmeira/samambaia arbórea na beira do córrego, copa alta
  (TallJungle/Kapok/Oaky) na encosta, árvore morta em volta das crateras, e a
  espécie vem de ruído de baixa frequência, então sai em manchas, não sorteada uma
  a uma. 418 trocadas; 280 folhas de pachira que estavam na camada errada foram pra
  camada Copa (sem isso a copa não abria em volta do jogador).
- **Montes vermelhos**: eram minhas pilhas de folha (arbusto achatado com material
  de fern, UV errado). Trocadas por folhiço 3D de galho e casca.
- **Chão vazio**: 340 manchas 2D no chão (folhiço, musgo, terra, raiz), geradas a
  partir das texturas do ForestVision com alfa de ruído, deitadas no relevo.
- **Canto da tela**: 10 grupos de galho pendurados a 3,4–5,2 m nas laterais do
  corredor, camada Copa (abrem quando o jogador passa embaixo), tingidos de verde
  escuro (vinham outonais).
- **Amontoado**: ao mover árvore agora há espaçamento mínimo de 2,1 m.
- **Visão**: raio do corpo 1,8 → 1,05 m; luz de presença 4,5 → 2,4 m nas 3 cenas.
  Malha da escuridão passou de 3 para 5 anéis pra o escuro do cone ser uniforme
  (com 4 anéis virava degradê).
- **Lanterna vira mecânica**: com ela apagada o cone escurece 82% e deixa de
  entregar guarda (só o raio do corpo revela). Medido em play no mesmo ponto:
  brilho do cone 0,220 acesa → 0,081 apagada (−63%); o círculo do corpo fica igual
  (0,293 → 0,291).
Pendente: reduzir a QUANTIDADE de árvore (a troca mudou a cara, não o número).

### T60 — Paralaxe de copa e galho por cima da tela (2026-09-20)
Pedido: galhos e folhas no mapa, parecidos com o efeito de mato, simulando copa
alta, com paralaxe.
- `CopaParalaxe` (runtime) + etapa `Paralaxe` no construtor. Silhuetas
  fotografadas DE CIMA das árvores do projeto (Kapok, Oaky, palmeira, samambaia
  arbórea, galho do ForestVision), recortadas por fundo magenta e pintadas quase
  pretas. A pachira ficou de fora: o FBX dela traz 4 variações lado a lado.
- A câmera da Selva é ortográfica: altura não gera paralaxe. Cada silhueta é
  redesenhada por quadro a (1 + fator) × a distância real até o foco da câmera.
  Duas camadas: copa (fator 0,28–0,42, 37 peças) e galho (0,7–0,9, 17 peças).
  Medido em play: andando 4 m, o chão andou 153 px na tela e a copa 213 px —
  razão 1,40, igual ao fator da camada.
- Erros achados no caminho: (1) o Unlit do URP devolvia a fila pra 3000 e a
  escuridão do cone (3100) pintava por cima → shader próprio `Ecos/Silhueta` em
  Transparent+200; (2) a paralaxe empurra a peça pra fora, então âncora na borda
  do mapa saía da tela → âncora a 5–11 m do caminho, já compensando; (3) sumir
  pelo centro não protegia o personagem (peça de 10 m) → some pela borda;
  (4) os galhos 3D da rodada anterior tinham material opaco e cobriam o jogador
  → removidos, a paralaxe faz o papel.
- Com as silhuetas, 8,4% da tela muda. Leitura: a copa aparece como massa negra
  cortando o cone iluminado; fora do cone, onde a escuridão já cobre 90%, ela é
  sutil. Versão com silhueta mais clara que a escuridão virava névoa cinza.

### T61 — Rede de trilhas, mata até a beira, copa sobre o caminho (2026-09-20)
- 13 trilhas: a principal, 2 laços (volta da patrulha, laço leste do começo),
  os desvios de escolha e o rádio, e 7 **sem saída**, cada uma terminando em algo:
  cratera + corpo, cratera oeste, lombada leste, cabana + covas, barranco norte,
  cratera + corpo leste, covas antes do portão. 3 pontas ganharam lampião tombado
  com luz fraca (isca pra seguir).
- Trilha sinuosa: cada trecho subdividido a 0,8 m e ondulado ±0,8 m, reto nas
  pontas pra bifurcação encontrar a trilha-mãe. Pintura estreita e nítida
  (núcleo ~1,1 m) com faixa de raiz escura na margem; terra solta fora da trilha
  quase some; manchas 2D de terra só no leito.
- Mata até a beira: árvore só sai se estiver a menos de 1,8 m da trilha (antes 3,6 m
  + raleio até 9 m, que fazia o "corredor vazio"). Árvore sem lugar pra ir ganha
  segunda tentativa sem espaçamento: 0 presas em cima da trilha. 152 moitas baixas
  na margem das trilhas, com falhas.
- Copa sobre as trilhas: camada nova de silhueta em cima do caminho (some em volta
  do jogador). Total 63 copas + 17 galhos.
- Escuridão fora do cone 90% → 78% (a copa preta passa a se destacar nos cantos).
- **Monte vermelho, causa real**: era o `Bush_Common` de fábrica (vermelho, folha
  quadrada) na lista de arbustos. Saiu. Colunas do bosque e mata distante também
  deixaram de usar CommonTree. Conferido: 0 Bush_Common e 0 CommonTree na cena.
Verificado: travessia 13/13 marcos, 1263 m²; vigilância "BOM" (17,1%).

### T62 — Trilha gasta, integrada ao chão (2026-09-20)
Pedido: dar pra saber o que é trilha e o que é terreno, sem faixa de cor.
A trilha passa a ser marcada pelo USO, não pela cor:
- Relevo: leito afundado 12 cm, com beirada de terra empurrada subindo 5 cm.
- Brilho: camada de terra batida com lisura 0,42 (as outras 0,1) — compactada e
  úmida, pega a luz da lanterna; folhiço em volta fica fosco.
- Borda recortada por dois ruídos (largo desloca a margem, fino esfarela em
  manchas) — sem linha reta.
- Folha seca acumulada na beirada (serapilheira reforçada em 0,75–2 m) e raiz
  exposta logo depois.
- 30 marcas de uso no leito: pedrinha solta e graveto quebrado.
Verificado: travessia 13/13 marcos, 1281 m². Foto de estúdio: caminho legível,
borda natural. À noite o contraste depende da lanterna: dentro do cone ele lê,
fora dele a trilha quase some (é o que a escuridão deve fazer).

### T63 — Contraste da trilha medido (2026-09-20)
Reclamação: ainda não dá pra diferenciar trilha de terreno.
Medida nova: na câmera do jogo, à noite, com lanterna, brilho médio dos pixels de
chão sobre a trilha (terra batida > 0,6) dividido pelo dos pixels de chão a
1,5–2,5 m dela, só dentro do cone e só onde nenhum objeto tapa o chão.
- Antes: trilha 0,060, margem 0,056 → **1,08×** (indistinguível, como relatado).
  Causa: textura de trilha marrom-média (terra com raiz) e, na rodada anterior,
  folha seca CLARA reforçada justamente na margem.
- Troca: trilha com terra fina clara (gnd_ForestDirt, tinta 1,15/1,03/0,88,
  lisura 0,42); margem escura com folhagem verde e raiz (folhiço claro cai pra 25%
  colado na trilha); manchas 2D não pousam mais na faixa de 1,1–1,9 m.
- Depois: trilha 0,117, margem 0,071 → **1,65×**. Foto: caminho claro entre
  margens escuras, sem borda reta.

### T64 — Trilha legível com a lanterna: terra compactada (2026-09-21)
Reclamação: com a lanterna ainda é difícil distinguir o caminho; pedido de
sensação de terra compactada, elementos de caminho marcantes sem roubar a cena.
Ponto de teste plano (1,8; 28,5), rumo -4°, noite, lanterna ligada.
- Elementos de caminho: 315 pedras meio enterradas nas bordas, 5 poças no leito,
  148 pegadas de bota em trechos, pedrinha/graveto no leito, 711 plantas de margem
  escurecidas + 478 plantas já existentes a 0,95–2,8 m escurecidas.
  Resultado: perto 1,19× / meio 2,11× — as plantas escuras não moveram o número.
- Diagnóstico (A/B por camada): a 4 m à frente a terra batida tinha peso 0,52;
  o resto era cobertura 0,29 + raiz 0,15. A trilha virava meio folhiço porque
  a pintura só SOMAVA terra batida, sem tirar as camadas de mato do leito.
  Luz sozinha (tudo cinza) dava 1,02× — não era a lanterna.
- Troca: miolo da trilha multiplica folhiço/cobertura/raiz/musgo/terra/sombra por
  (1 − 0,94·trilha); terra batida tinta quente 1,5/1,26/0,92; folhiço e cobertura
  ~20% mais escuros e frios.
- Depois: terra batida a 4 m = 0,95. **Perto 1,78×** (antes 1,19×), meio 2,12×.
  Segundo ponto (0; -46): perto 2,18× (o meio lá é cone cortado por arbusto).
  Travessia 13/13, 1286 m².
Armadilha de medição: Renderer.enabled=false e cam.Render() no MESMO comando não
esconde nada (o desenho só atualiza no frame seguinte). Os A/B "sem grupo X" da
primeira rodada eram inválidos; refeitos com SetActive num comando e medição no
seguinte.

### T65 — Galho retorcido, arbusto aleatório e guarda que vê de fora da tela (2026-09-21)
Reclamações: galhos verdes retorcidos por todo o mapa; arbustos aleatórios, esconderijo
que "é" um galho retorcido ou nada; guarda pega antes de aparecer na tela.
- Inventário por prefab: 202 pachira_aquatica (o arquivo traz 4 plantas de vaso
  enfileiradas, escala 4–5×, tronco trançado + folha) e 167 DeadTree (galhada
  retorcida) no Nivel/Arvores, mata distante, tocos e colunas do bosque; 137 feixes
  de galho seco salpicados. Depois: 0 pachira, 0 DeadTree; cratera ganha toco
  (tree_stump), galho seco só ao pé de tronco/toco (40).
- Esconderijos: dos 52 volumes, 35 ativos e nenhum com visual próprio — mostravam
  pachira, galho, casca, caixa de munição ou nada (Mato_021/025 vazios). Agora cada
  volume vira uma moita de receita fixa (feto grande no miolo, arbusto escuro e feto
  no anel, capim na borda) e o que caiu dentro sai: 33 moitas.
- Arbusto solto em moitas (30 moitas, 4–7 plantas) em vez de 150 pés salpicados.
- Guardas: alcance real na cena era 12 m × lanterna 1,7 = 20 m; a câmera (orto 4,5)
  mostrava ~4,5 m pra cima. Agora: regra `soNaTela` (fora do enquadramento não
  acumula detecção), patrulha 7,5 m, sentinelas ≤ 7 m, lanterna ×1,35, câmera orto 6.
  Teste em play, lanterna ligada, guarda parado olhando o jogador:
  4,5 m / viewport y 0,88 → detecção 0,31 e subindo; 6,5 m / y 1,05 (fora da tela,
  dentro do alcance 10,1) → 0,00 depois de 14 s.
- Travessia 13/13, 1280 m². Valores de guarda mudados só na cena Selva (a regra
  soNaTela vale em todas por padrão).

### T66 — Mato preso ao renascer, piscada, moita invisível, chuva e trovão (2026-09-21)
- Renascer com mato na tela: `PlayerConcealment` só tirava o volume no OnTriggerExit, que
  teleporte não dispara. Agora confere a posição contra o volume todo quadro e o
  `PrototypeDirector` chama `Limpar()` ao renascer. Teste em play: dentro da moita
  (vis 0,28) → pego → renasce em (0;-36) com no mato=False, efeito 0,00, folhas 0,00.
- Piscada: era o corte preto do `BlinkSystem` em todo salto de estresse (abate +32) e a
  cada ~14 s na hipervigilância. `corteVisivel=false`: sem desenho, sem lacuna automática;
  a lacuna virou o instante depois do relâmpago (`LacunaSilenciosa`).
- Moita invisível: a moita tinha 4 massas verdes, mas 4 "Foliage 1" (feto-estrela de
  5–7 m, fronde preta espetada) cobriam tudo. Saiu dos esconderijos, das moitas soltas,
  da margem da trilha (143) e da mata distante. Esconderijo = massa de folha
  (Opt_SmallBranching ~1,8 m) no miolo e no anel + feto verde baixo. Foto de cima: moita
  verde densa, legível.
- Tempestade nova: chuva (partícula + respingo + som gerado), relâmpago a cada 28–55 s
  (14–26 s em ruptura) que levanta a escuridão da tela inteira (_EcosRelampago no shader),
  trovão 0,4–2,6 s depois, e em 20% (55% tenso) silhuetas de soldado paradas fora do cone
  olhando o jogador, que somem com a luz. Respingo saía quadrado (shader ignorava a
  textura sem as chaves de transparência): corrigido.
- Travessia 13/13.

### T67 — Chuva com VFX de verdade (2026-09-21)
Reclamação: "essa chuva tá zoada". Era um tracinho cinza Unlit igual por cima de tudo,
inclusive da escuridão — estática de TV.
- Gotas Simple Lit abaixo da escuridão: só acendem onde há luz (facho da lanterna,
  relâmpago). Véu Unlit de 7% por cima do escuro. Coroa de gotículas pulando + anel
  abrindo no chão, também iluminados. Vento em rajadas (Perlin) inclina, adensa e sobe
  o som.
- Primeira versão iluminada saiu fraca demais: gotas 1,6× mais claras, 0,03–0,05 m de
  largura, rastro 0,08; coroa e anel 2×.
- Chão molhado: lisura das camadas 0,10 → 0,26 (terra batida 0,42 → 0,58). Acinzentou a
  tela inteira porque refletia o skybox (reflectionIntensity 1); `reflexoDoCeu` 0,08
  na tempestade — o molhado só brilha onde a lanterna bate.
- Contraste da trilha no ponto de teste, com chuva: perto 1,77× (antes 1,78×), meio
  2,82× (antes 2,12×).

### T68 — Capítulo I jogável: a corrente do Doyle (2026-09-21)
História aprovada em Docs/HISTORIA_SELVA.md. Construído:
- `RadioDoyle`: narrador. Legenda com rótulo (RÁDIO — DOYLE / RÁDIO — MILLER / pensamento
  em itálico), estalo de abrir/fechar canal, chiado, murmúrio de voz sintetizado no
  tamanho da frase. Fila: uma fala espera a outra.
- `Interagivel` + `GatilhoHistoria` + `Historia` (diretor da corrente): [E] pega/usa
  (cede a vez à faca quando ela tem alvo), segurar [E] na manivela, carta na tela,
  checkpoint de renascimento no começo de cada elo, a voz que avisa guarda de verdade
  (2x) e depois às vezes mente, a mentira da trilha do meio manda os dois patrulheiros
  mais perto pra ela.
- `HistoriaSelva` (editor, também roda no fim do FlorestaSelva): cerca de arame no norte
  com portão de madeira + corrente, parede invisível; fuzil do Miller ao lado do Ward
  (Corpo_Unidade); aldeia a leste com cabana (galpão 0,46×), mochila e poço montado
  (pedra, postes, eixo, corda, balde); corpo do Doyle escondido; gatilhos portão /
  aldeia / saída. Desliga os 11 beats de tutorial, o rádio no chão, a zona de extração
  e os cipós. Abertura reescrita (4 legendas).
- Travessia agora confere os elos: 8 alcançáveis, e a SAÍDA inalcançável com o portão
  fechado (prova que o portão tranca).
Teste em play (guardas desligados, [E] simulado por `Historia.Testar`):
Acordar → portão (Doyle fala, pista "a chave: com o Ward…") → Ward + fuzil (IrAldeia,
pista do poço, checkpoint no Ward) → mochila (desculpa do Doyle, manivela, Poco) → poço
7,5 s (3 falas, balde sobe, corpo aparece) → corpo (chave, plaqueta, "Solta, Doyle!",
Portao) → portão. Todos os estados na ordem.
Bugs achados e corrigidos no teste:
- Portão deitado de lado: o prefab tem giro de importação e o código substituía em vez
  de somar; a moldura fechava a passagem (jogador parava em z=37,3). Agora soma o giro,
  alinha pelos limites e a folha gira em torno do eixo vertical do mundo na dobradiça.
  Depois: atravessa até z=45,3.
- Erro de compilação CS0136 no TravessiaCheck não apareceu no console do MCP; achado
  em Library/Bee/tundra.log.json.
Falta testar com guardas ligados (a mentira, os avisos, ser pego no meio da corrente).

### T69 — Mapa largo + corrente com guardas ligados (2026-09-21)
- Mapa 36 m → 80 m (JX 18 → 40): paredes-limite, MataNova (137 árvores com tronco),
  córrego como barreira com 3 travessias (meio, tronco-ponte x≈12, vau barulhento x≈−26),
  aldeia no extremo leste, 3 lugares de exploração (lembrança + isca), 2 guardas novos.
  Travessia: 4.637 m² (antes 1.200), 12/12 marcos, saída trancada com portão fechado;
  com as 3 travessias tapadas, todo o norte fica inalcançável (o córrego tranca).
- MataFechada (paredes do corredor antigo) cortava as trilhas novas: desligada.
- Piloto automático `AutoHistoria` (agachado, para/desvia de guarda, rota pelo vau).
  Problemas que ele achou, todos também de jogador:
  1. checkpoint gravado dentro da ronda do guarda do portão → morte em ciclo. Agora é o
     último lugar seguro (longe de toda ronda) + 3 s de fôlego ao renascer.
  2. Ouvinte chamava os 10 guardas do mapa → 22 m de alcance.
  3. Guarda_Posto fazia ronda no caminho posto→aldeia → foi pra cabeça do tronco-ponte.
  4. agachado farfalhava na moita do sargento, ao lado do Ouvinte → agachado não farfalha.
  5. controlador tratava o piloto como de pé; interação exigia 1,3 m (sargento cercado
     de caixotes) → corrigidos no piloto.
- Doyle sem ordem tática (usuário: "qual o sentido se ele tá no fundo do poço?"):
  saíram os avisos de guarda, a "mentira da trilha" e a desculpa. Ele só sabe o que
  sabia antes de morrer e onde está.
- Última rodada com guardas: Acordar → portão 91 s → chave do Ward 141 s → aldeia,
  mochila, poço, corpo → chave aos 455 s. Não passou do portão: a ronda do Guarda_Portao
  passa na porta e o piloto não sabe esperar o guarda se afastar (14 capturas ali).
  Desafio de tempo pra jogador humano; falta playtest de verdade nesse trecho.

### T70 — Rádio quebrado, caderno, espólios e a execução da faca (2026-09-22)
Abertura: saíram as legendas "você acorda na lama", "a zona de pouso ao norte" e "o rádio
levou um tiro". Ficou o título; a câmera desce até ele e as três coisas na lama; ele tenta
o rádio (chiado de 7 s com a palavra "ajuda" enterrada, voz masculina Daniel do Windows
grave + filtro de rádio, e um gutural de criatura no fim); só então a primeira frase dele:
"Merda de rádio! O receptor está estraçalhado...".
- **Três itens no chão** (rádio, faca, caderno) com tela de inspeção: o modelo do próprio
  chão, girando, com nome e uma linha do Miller. Pausa o jogo (timeScale 0).
  Bugs corrigidos no teste: o modelo saía minúsculo (a antena do rádio mandava na escala →
  parâmetro de zoom por item) e virado de costas (Euler(x,y,0) girava no eixo errado →
  gira no próprio eixo e depois inclina pra câmera); luzes fracas demais (3,2 → 11).
- **Caderno [N]**: mapa desenhado à mão gerado do mapa de verdade (córrego pela mesma
  função, trilhas, crateras, cerca, posto, aldeia, poço), X vermelho no centro da ronda de
  cada guarda da cena, círculos azuis do Ward e do Doyle, notas dos batedores, plano da
  operação e a página arrancada com sangue. Bugs: rótulos minúsculos e as letras da bússola
  saindo do mostrador (RotateAroundPivot aninhado usa pivô de TELA, não do quadro girado).
- **Primeira missão**: 4 corpos inimigos ao sul do córrego; 3 com peça (fita, válvula, fio),
  1 vazio. Com as 3, o Miller conserta e só então o Doyle fala pela primeira vez.
- **Final**: no corpo do Doyle ele abre a tampa da bateria (inspeção: inchada, corroída) —
  "em ruínas faz semanas", "esse rádio nunca funcionou".
- Travessia: 19 marcos alcançáveis a pé, saída trancada com o portão fechado.
- Piloto automático com guardas: pegou os 3 itens, revistou os corpos, consertou o rádio e
  chegou ao portão **com a chave**, 9 capturas no caminho (antes eram 17).

**Bússola com física** (pedido do usuário: "gire igual a uma bússola real"): a rosa é um
ímã em líquido — torque ∝ sen(erro), amortecimento do líquido, tranco do passo e atrito do
pino. Ela atrasa, passa do ponto, volta e assenta; virada de 180° demora mais que uma de 20°.

**Execução da faca** (pedido: "sem animação está horrível"): par de animações do add-on
Stealth Kill do Invector (Active/Passive, humanoides, 3,25 s), que já estava no projeto.
O Miller desliza pras costas do alvo (0,16 s), os dois tocam juntos, a faca aparece na mão
(prefab do mesmo modelo do chão), som de golpe e baque, estresse crava no meio do golpe.
Bugs corrigidos: sem root motion os dois terminavam de pé um dentro do outro (a queda está
no deslocamento da raiz) → root motion nos dois, e no fim o deslocamento do filho é
devolvido pro pai (o CharacterController fica no pai). A animação da vítima acaba
AJOELHADA: o controlador dela emenda na mesma queda dos outros corpos (Death01) e congela
no último quadro, deitado.

**Correção do travamento no ar** (usuário: "após o estrangulamento o inimigo trava no ar
antes de cair"). Medi o movimento dos dois clipes quadro a quadro: **ambos param de se
mexer em 80%** (2,6 s de 3,25 s) e ficam 0,65 s parados EM PÉ — o add-on original emenda
outra animação aí. Agora o corte é em 0,80: a vítima entra na queda (Death01, mistura de
0,22 s) e o Miller entra no idle (mistura de 0,3 s), e o controlador dele só volta pro de
andar quando a pose já é a de parado. O jogador fica preso 2,95 s em vez de 3,25 s.
Medido depois: luta em t=0,74 (quadril a 0,69 m) → queda em t=0,51 (quadril a 0,14 m),
sem parada no meio; o corpo descansa na mesma altura dos corpos de cenário.

### T71 — Moitas de verdade, sangue e a morte na moita (2026-09-22)

**A raiz do bug das moitas.** A cena tinha **52 caixas invisíveis** com `ConcealmentVolume`
(`Mato_001…052`, renderer desligado), feitas à mão, e o grupo `Floresta/Esconderijos`, que
deveria pôr vegetação em cima delas, estava **vazio**. Resultado: camuflagem no meio do
nada — a regra do mundo ficava invisível pro jogador.
Agora é o contrário: `MoitasSelva.Montar` cria a moita (aglomerado de plantas) e a zona de
camuflagem **como filha dela**, do tamanho da folhagem. 39 moitas, 1823 plantas, 0 caixas
soltas. Elas nascem na beira das trilhas e em volta da ronda dos guardas, longe do córrego,
das crateras e dos lugares da história.

**Plantas novas** (Poly Haven, CC0, baixadas direto): `shrub_03`, `anthurium_botany_01`,
`calathea_orbifolia_01`, `weed_plant_02`, `island_tree_02`. O importador
(`Ecos/Selva/Importar plantas novas`) junta cor + recorte numa PNG só e monta o material
URP com corte de alfa e dois lados — sem isso cada folha vira um retângulo.

**O jogador some na moita** (`JogadorNaMoita`): o corpo apaga até virar vulto (alfa 0,14) e
volta se ele correr. `FolhagemTransparente` agora **ignora** quem é moita: antes o mato onde
você se esconde ficava transparente e te entregava. O efeito de folha na tela foi aliviado
(14 → 8 folhas, alfa 0,96 → 0,66, raio livre 150 → 250 px): estava cegando quem se escondia.
`PlayerConcealment` passou a conferir por posição 4×/s — teleporte/renascer dentro de uma
moita não disparava o trigger e o jogador ficava visível no meio do mato.

**Sangue** (`Sangue.cs`, tudo gerado em código): jato no corte (partículas esticadas),
poça que cresce embaixo do corpo e escurece enquanto seca, pingos.
Bugs achados: as três curvas de velocidade tinham que estar no mesmo modo (erro no console a
cada partícula); a poça usava material iluminado e ficava **preta** na noite fechada (virou
Unlit); gota de 1,5 cm era sub-pixel na câmera de cima (6–18 cm agora); e o osso "peito"
**não existe** no avatar do soldado, então `GetBoneTransform` devolvia nulo e a poça nunca
era criada — sem erro nenhum. Agora tem plano B de osso (peito → coluna → quadril).

**Engasgo** (`Engasgo_1..3.wav`, sintetizados): fry vocal estrangulado + ar forçado em
golfadas + bolhas de líquido, tudo abafado num passa-baixa. Toca **no corpo** (fonte 3D,
16 m de alcance), começando no corte.

**Morte na moita**: se o Miller estiver escondido quando apertar [E], a execução termina com
ele puxando o corpo pra dentro da folhagem — o corpo afunda 0,55 m, some, e ficam a poça na
borda da moita, o rastro no caminho e o sangue pingando por 6 s.

**Erro de processo:** rodei um teste achando que o play tinha começado e ele executou em
modo de edição, mexendo na cena (guarda renomeado, componentes destruídos). Recuperei
recarregando a cena do disco. Todo teste agora começa conferindo `EditorApplication.isPlaying`.

### T72 — Fim dos bloqueadores invisíveis (2026-09-22)
Auditoria de colisores sem malha visível na cena: **57**, todos do mesmo lugar —
`Corrego_Travessias/Barreira`, as caixas de 1,35 × 3 × 0,8 m que faziam o córrego ser
barreira. Era isso que o jogador sentia como "parede invisível no meio do mato".

Agora quem barra é objeto de verdade: **20 pedras + 11 troncos caídos** no leito do córrego,
e o colisor mora dentro da pedra/tronco (caixa do tamanho da malha, +20 cm pra não sobrar
fresta entre uma e outra). Pedras e troncos vieram de pacotes leves (JP Large/Small Rock,
92–328 triângulos; o log01 da vila), não dos modelos do Poly Haven — aqueles têm 100 mil
triângulos por pedra e são inviáveis aos montes.

O gerador agora **se confere sozinho**: anda pela linha do córrego de 35 em 35 cm e, onde a
cápsula do jogador (1 m) passaria, planta uma pedra curta empurrada pra fora da travessia.

Medições (ignorando o terreno, que antes dava falso positivo):
- as 3 travessias PASSAM (meio, tronco-ponte, vau);
- vãos abertos fora das travessias: **0**;
- colisores invisíveis na cena inteira: **0**;
- travessia: 19/19 marcos alcançáveis, saída trancada com o portão fechado;
- com as 3 travessias tapadas de propósito, 10 dos 19 marcos ficam inalcançáveis — prova
  de que a barreira nova segura.

Erro de método anotado: a primeira verificação usava `Physics.CheckSphere` sem filtrar o
`TerrainCollider` e dava "0 vãos" mesmo com buraco — o terreno contava como obstáculo.

### T73 — Vida, tiro, alerta e o forcejo de faca (2026-09-22)
Usuário: "ser pego e morrer instantaneamente é impossível, é difícil visualizar o inimigo
antes de tomar um IK dele".

- **Vida** (`Vida.cs`): 3 tiros, **sem cura no capítulo**. Plaquetas no canto e borda
  vermelha quando está por um fio. Morrer devolve ao checkpoint com a vida cheia.
- **Tiro** (`Tiro.cs` + `GuardAI`): ao te detectar ele grita (chama quem está a 22 m),
  ergue a arma por 0,8 s e dispara a cada ~1,1 s. Clarão no cano, traçante, estouro
  sintetizado e poeira onde a bala bate — errar também informa. Pontaria: 65% colado,
  20% no limite; agachado −15%, dentro do mato até −45%.
- **Alerta → caça → esfria**: te vendo, encara e mantém distância de tiro; te perdendo,
  **caça** por 18 s (mais rápido, cone 1,35×, vasculhando em volta do último lugar); sem
  achar, volta à ronda. Medido em play: Patrulha → Alerta (vida 3→2→1) → morte → renasce →
  guarda de volta à ronda; e sumindo da vista, o estado virou Caçando.
- **Forcejo de frente** (`MeleeKill.Forcejar`): atacar de frente vira luta pela faca —
  barra que sobe a cada toque em [E] e cai sozinha, 2,6 s. Ganhou: **a faca no olho**
  (estocada `Punch_Jab` + `Hit_Head` emendado na queda). Perdeu: 1 de dano, empurrão e ele
  chama todo mundo.
- **Animações do guarda**: estado `Atirando` (Pistol_Shoot) na camada dos braços do
  AC_Toon — as pernas seguem andando e só o tronco atira; e **recuo de costas** com o
  ciclo de caminhada invertido (`CorpoAnimado.DeCostas`), senão ele fazia moonwalk.

### T74 — "Fico preso em algo que não sei o que me prendeu"
Auditoria: **698 objetos desbotavam E bloqueavam** ao mesmo tempo — 419 deles árvores com
cápsula de 0,9 m. O `FolhagemTransparente` apagava a árvore até 0,22 de alfa e o colisor
continuava lá: parede invisível criada pelo próprio efeito de visibilidade.
Regra nova: **quem tem colisor não some** — desbota só até 0,55 (`alfaDeQuemBloqueia`) e
segue legível. Copa e folhagem sem colisor continuam sumindo como antes.
A cápsula do jogador também encolheu: raio 0,5 → **0,42** e degrau 0,3 → **0,45**, porque
1 m de largura enganchava em pedra pequena e raiz.

**Animação durante o forcejo** (usuário: "enquanto o E é pressionado também deve ter
animação, não apenas na morte"). Antes os dois ficavam na pose parada, só tremendo.
Agora os dois entram em `Push_Loop` (empurra-empurra, AC_Forcejo), defasados entre si pra
não parecerem gêmeos, e o **ritmo da animação segue a barra**: quanto mais você martela,
mais rápido o Miller empurra e mais devagar o outro resiste. Perder tem animação própria
(`Hit_Knockback`, AC_Empurrao): ele te arremessa e você cambaleia 1,3 m pra trás.
O `CorpoAnimado` é desligado nos dois durante a luta — ele reescreve a velocidade da
animação todo quadro e anulava o efeito.
Teste em play: o par entra na pose, os controladores voltam ao normal no fim (AC_Jogador /
AC_Toon), perder custou 1 de vida e ganhar deixou o corpo caído com o jato de sangue na
altura da cabeça.

### T75 — O corpo do poço (2026-09-22)
Usuário: "troca o corpo do cara do poço por aquele zumbi sem pele, bem pior, meio inchado
como um cadáver em putrefação — é mais impactante que um placeholder de soldado".

Era o mesmo boneco de soldado dos outros mortos: o balde subia e o que aparecia era mais um
placeholder deitado. Agora é o **zumbi sem pele** (pacote Tensori, humanoide, 28 mil
triângulos), com:
- **inchaço nos ossos**, não na escala do objeto: barriga (1,3×), peito, cabeça, antebraços
  e mãos — incha onde afogado incha e continua com tamanho de gente (1,78 m);
- **materiais próprios** (`M_Doyle_Pele` / `M_Doyle_Pano`): verde-acinzentado de dois dias
  na água, com brilho de molhado (smoothness 0,62);
- a mesma queda congelada dos outros corpos (AC_Morto, último quadro);
- quando o balde sobe: **poça**, sangue pingando por 5 s e uma **nuvem de moscas**
  (`Moscas.cs`, partículas com ruído, cinza-sujo pra ler contra o chão escuro).

Ajustes depois de ver na tela: ele flutuava em cima do anel de pedra → foi pro chão ao lado
do poço; a poça estava vermelho-refrigerante → mais marrom; as moscas eram pretas sobre
chão preto → cinza e um pouco maiores.

**Bug: o Doyle subia do poço EM PÉ.** `MortoParado` congelava o corpo com
`Animator.Play(estado, 0, 1f); Update(0f); speed = 0`. Isso funciona em corpo que já estava
ativo desde o começo da cena (os mortos do cenário), mas **falha** quando o objeto acabou
de ser ligado por `SetActive` e o rig é humanoide: o Animator só se inicializa no fim do
quadro e a pose fica no primeiro frame do clipe. Medido: estado "Caido" congelado em
**t = 0,14**, quadril a 0,77 m e cabeça a 1,41 m do chão (de pé).
Corrigido aplicando no `OnEnable` e repetindo com delta de verdade (`Update(0.001f)`) por
0,4 s. Depois: t = 1,00, quadril 0,85 e cabeça 0,99 — deitado, como os outros corpos.

### T76 — O corpo cai do poço (2026-09-22)
Usuário: "deixa esse corpo mais parecido com um morto, faz uma mini cutscene dele caindo no
chão do poço, com barulho de mosca e moscas em volta; não um zumbi duro deitado, e a
animação tem que ser mais fluida".

- **Ragdoll** (`Cadaver.cs`): 12 corpos rígidos, 11 juntas e cápsulas nos ossos, montados a
  partir do Animator humanoide (serve pra qualquer modelo humanoide do projeto). Ele sobe do
  poço **na pose de morto** (MortoParado congelado) e, ao tombar da borda, a **física assume**:
  cada perna cai onde cai, o braço dobra onde bateu, e a pose final nunca é simétrica.
  Assentou = tudo vira cinemático (custo zero depois).
- **Mini cutscene** (`Historia.OBaldeSobe`): a câmera fecha no poço (1,1 s), o corpo sobe do
  fundo por 2,2 s escorrendo água podre, a corda range chamando atenção, ele tomba, cai,
  forma poça — e só então a câmera volta e o Miller diz "Doyle."
- **Moscas com zumbido** (`Moscas.wav`, sintetizado): 7 moscas com frequências e batimentos
  diferentes, cortes secos de pouso, chiado de asa e passa-baixa. Laço de 3,75 s, emenda
  cruzada. Fonte 3D presa no corpo, 14 m de alcance — o zumbido chega antes de você ver o
  que é. Cópia em `Resources/` pra funcionar em build.

Medições: primeira tentativa **arremessou o corpo 3 m** (impulso por massa em cada osso) e
ele sumiu do enquadramento; com `VelocityChange` e mais amortecimento, ele anda **0,37 m** do
ponto de queda, quadril a 0,13 m e cabeça a 0,29 m do chão, cabeça→pé 1,47 m (corpo inteiro,
sem explodir). Zumbido tocando em laço e 45 partículas de mosca vivas em volta.

## T77 — Cutscene do poço: enquadramento e a queda do Doyle (2026-09-22)

**Reclamação:** "a camera ficou no lugar errado, não da pra vizualizar" + "o doyle
cai e começa a se mexer loucamente" + "queria a camera mais próxima, terceira pessoa".

**Câmera.** A câmera do jogo é ORTOGRÁFICA com pitch 80°. A versão anterior mexia na
ALTURA e no `orthographicSize` sem tocar na rotação — em ortográfica altura não
aproxima nada, e com 80° o centro da tela caía 3,9 m à frente do poço (fora do quadro).
Agora: pitch 45°, `size` 1,95 e a posição CALCULADA a partir de onde o raio da câmera
cruza o chão (`pos = alvo + (0, h, -h/tan(pitch))`). Plano final enquadra corpo e poço
(lerp 0,3 do corpo pro poço), porque seguindo só o corpo o poço ia pra borda.

**Por que o corpo se debatia — três causas, todas medidas:**
1. `MortoParado` e `Cadaver` no MESMO objeto, reescrevendo os mesmos ossos no mesmo
   quadro (um pela pose congelada, outro pela física). `MortoParado` removido do Doyle.
2. Os pedaços do ragdoll colidiam entre si (as duas coxas nascem encavaladas no
   quadril). Agora `Physics.IgnoreCollision` em todos os pares do próprio corpo.
3. Ele era solto ainda PENDURADO EM PÉ: desabava dobrando o corpo por 4,45 s e
   derivava 0,94 m — chegou a parar a 0,73 m do centro do poço, atravessando as pedras.
   Agora `Cadaver.Deitar()` segura quadril e peito nos dois lados do ponto de queda,
   o corpo sai da corda na horizontal e cai reto.

**Outros achados:**
- `Assentado` por VELOCIDADE mentia: o corpo ficava imóvel (quadril parado em
  (31.34, 0.14, 3.88) por 3 s) com velocidade oscilando 0,11–0,25 m/s — tremor de
  contato do PhysX. Só congelava pela rede de segurança, 5 s depois. Trocado por
  DESLOCAMENTO (< 1,5 cm por 0,1 s). Medido depois: tremor pós-assentado = 0,0000 m.
- Som novo: `Baque.wav` (thump 78→45 Hz + tapa de carne + terra), toca no primeiro
  contato. O impacto também mata a inércia (velocidade ×0,18, damping 3,2).
- Os anéis de respingo da chuva (`Aneis`, 0,22 m, material padrão do URP) ficam do
  tamanho de argolas num close de 3x: emissão desligada durante a cutscene e religada
  no fim.
- Luz `LuzDoPoco` (point, fria, 2,6) nasce com o corpo e é destruída no fim: sem ela
  o plano fechado fica ilegível se o jogador não estiver apontando a lanterna.

**Medição final:** soltou t=4,57 → assentou t=6,24 (1,67 s), corpo a 1,79 m do poço,
deitado (quadril y=0,21 / pé y=0,11), escorregão pós-baque 0,54 m, luz removida,
timeScale=1. Gancho de teste: `Historia.TestarPoco()`.

**Erro meu no teste:** na primeira medição teleportei o jogador pra y=0,1 com o
CharacterController ligado e ele caiu pro y=-519 — as fotos saíram muito mais escuras
que o jogo, porque a lanterna DELE é a luz principal da cena. Teleporte de jogador
sempre com raycast pro chão.

## T78 — Doyle sem as pernas, com as tripas penduradas (2026-09-22)

**Pedido:** "faz ele sem as perna, com umas visceras penduradas, e melhora a animação
dele subindo do poço e no inicio da animação de cair, pq tá zoado".

**O corte.** `CorpoRasgado.Estripar()` (editor) joga fora todo triângulo cujos vértices
pesam >50% em osso de perna, em todos os 4 LODs das 2 malhas, e salva as malhas novas em
`Assets/_Game/Modelos/`. 28.123 → 14.130 triângulos. Não é escala de osso em zero: aquilo
deixa a pele esticada num bico e se vê que é truque; aqui sobra coto aberto.
A física das pernas é destruída junto e `Cadaver.semPernas` passa a pular esses ossos.

**Armadilha:** a primeira passada cortou 0 triângulos em silêncio — o FBX vinha com
**Read/Write desligado** (`mesh.isReadable == false`), então `boneWeights`/`triangles`
não valiam nada. Ligado via `ModelImporter.isReadable` + reimport.

**Tripas.** 3 fios (7/5/5 gomos) de cápsula com CharacterJoint, pendurados no quadril,
com o componente `Viscera` — o `Cadaver` os adota na mesma lista dos ossos, então eles
ignoram o corpo, caem junto e congelam junto. Primeira versão ficou um COLAR DE CONTAS
(gomo fino e comprido, junta no topo exato): corrigido com gomo gordo e curto
(comp 0,15-0,185 / raio 0,052-0,068), âncora em 0,82 e 25% de sobreposição.

**Subida.** Era um lerp liso e o corpo parecia flutuar pra fora do buraco. Agora sobe em
7 PUXADAS de manivela (62% de movimento, 38% de descanso), cada uma com rangido novo
(`Corda.wav`: 37 fibras cedendo + ressonância da madeira em 190/430/880 Hz) e a corda do
poço ESTICANDO até ele (`EsticarCorda`), com o balde escondido — quem sobe é ele.

**Início da queda.** O tranco vinha de `Deitar()` interpolar quadril e peito em LINHA
RETA: a distância entre os dois encurta no meio do caminho (corda contra arco), a junta
estica e volta com tranco. Virou rotação rígida — Slerp da direção + Lerp do centro,
mantendo a distância.

**Medido:** sobe de y=-1,90 a 1,05 em 2,6 s; solta em t=5,46; assenta em 1,77 s; para a
1,70 m do poço; tripa mais baixa y=0,05 (não afunda); balde de volta; timeScale=1.

## T79 — Cutscene de abertura: chuva num retângulo, sombras voando, mata ilegível (2026-09-22)

**Uma causa só para as três coisas:** meio jogo seguia o JOGADOR pra decidir onde chover,
onde abrir a copa e onde desbotar folha. Enquanto a câmera é a do jogo dá no mesmo. Na
cutscene não: o primeiro plano abre o zoom pra **22** (contra 6 do jogo) e a tela passa a
mostrar **104 x 44 m**.

1. **Chuva num retângulo.** A caixa de emissão tinha 30x22 m fixos, grudada no jogador:
   cobria 29% da largura da tela e dava pra ver a borda da chuva. Agora ela segue
   `CameraRig.Foco` e cresce com o quadro (teto de 3,9x = 115 m), com a taxa de emissão
   subindo junto (teto 2,8x) — senão a chuva ralearia na mesma proporção.
2. **Sombras voando.** As 103 silhuetas de `CopaParalaxe` são primeiro plano: existem pra
   passar rente à lente num plano FECHADO, e andam 1,35x o que a câmera anda. Num plano
   de 104 m viravam manchas escuras atravessando a tela. Agora somem acima de 1,15x de
   abertura e não existem em 1,8x (voltam sozinhas no zoom de jogo).
3. **Mata ilegível.** Era consequência das duas acima: as manchas cobriam a copa. Sem
   elas, o plano mostra a clareira, a trilha e a lanterna do Miller.

**Novo em CameraRig:** `Foco` (ponto do chão que a câmera enquadra AGORA), `Lente` e
`Cena`. Medidos no fim do LateUpdate, depois de mover a câmera. `FolhagemTransparente`
também passou a abrir o mato no foco durante cutscene — antes abria em volta do jogador,
fora do quadro.

**Medido, plano aberto:** tela 104 m / caixa de chuva 115 m (cobre), 1.880-2.435 gotas
vivas, copas desligadas. Zoom de jogo: caixa 31 m, 628 gotas, copas ligadas. timeScale=1.

## T80 — Silhuetas de copa: adesivo preto virou folhagem (2026-09-22)

**Reclamação:** "as folhas em volta da tela ficaram cartoonescas e bem feias".

**O que era.** `FlorestaSelva.Silhueta()` fotografava a árvore de cima em 512px, recortava
o fundo magenta com corte seco (`a = (dist - 0.25) / 0.45`) e pintava de preto quase
puro. Resultado, olhando o PNG: uma MANCHA PRETA CHAPADA, miolo 100% opaco, borda
serrilhada. Papel recortado — e são 103 delas em volta da tela.

**O que passou a ser:**
- fotografa em **1536 com MSAA 4x** e reduz pra 512 fazendo média 3x3: borda com
  meio-tom em vez de escada;
- **dois borrões** da cobertura: um curto (raio 2 — folha encostada na lente está fora de
  foco) e um largo (raio 9), que mede densidade local. A franja fina das pontas de galho
  rareia (`0,28 + 0,72 * densidade`) em vez de terminar em corte de tesoura;
- a **luz que bateu na folha** abre o miolo (`a *= 1 - 0,42 * luz`): é isso que tira o
  aspecto de mancha sólida, porque passa um fio de céu entre os galhos;
- cor: verde quase preto que varia com a luz (`0,45 + 1,55 * luz`) em vez de preto puro,
  que na tela vira buraco recortado;
- import com **mipmap + trilinear + aniso 4**: a mesma silhueta aparece em tamanhos bem
  diferentes conforme o zoom.

O shader `Ecos/Silhueta` já era alpha blend (SrcAlpha/OneMinusSrcAlpha), então o alfa
macio funciona sem dithering.

**Como refazer sem remontar a floresta:** `FlorestaSelva.RefazerSilhuetas()` — os
materiais apontam pro mesmo arquivo PNG, então trocar o arquivo basta. Os 5 PNGs
(Kapok, Oaky, Palmeira, Samambaia, Galhos) foram regerados e verificados em play.

**Sobra pra depois:** são 5 texturas para 103 quads. A repetição é disfarçada por giro e
escala aleatórios, mas gerar 2-3 rotações por espécie mataria de vez.

## T81 — Gravar a gameplay pelo editor (2026-09-29)

Pacote `com.unity.recorder` 5.1.7 instalado e `Editor/Gravador.cs`:

- **F9** começa e para a gravação (menu `Ecos/Gravação`), em modo MANUAL — não grava
  do primeiro quadro do play, grava de onde você mandar;
- saída em `Gravacoes/` na raiz do projeto, **fora de Assets** (vídeo dentro de Assets
  vira asset importado e entope o projeto);
- 1920x1080, 30 fps, H.264 + **áudio AAC estéreo 48 kHz** — neste jogo o som é metade
  da coisa;
- sair do play com a gravação aberta finalizaria um MP4 ilegível, então
  `playModeStateChanged` fecha o arquivo antes.

**Verificado:** 36,3 s gravados, ffprobe confirma h264 1920x1080 30/1 + aac 2ch 48 kHz.

## T82 — O Doyle falando depois de morto: virar terror, não furo (2026-09-29)

**Reclamação:** "o doyle continua falando no radio mesmo depois do cadaver dele ter
subido, tem muito furo".

**Não era furo — era a premissa sem marcação.** O roteiro já revela no fim (`OCorpo`)
que a bateria está podre faz semanas e "esse rádio nunca funcionou". O problema: entre
o corpo subir e o jogador interagir com ele, o "Doyle" seguia conversando normalmente,
no mesmo tom, e isso lê como bug. Decisão do Gustavo: marcar como horror.

**`RadioDoyle.VozQuebrada`**, ligada no instante em que o corpo assenta:
- voz cai pra pitch 0,78 (±0,05, instável), chiado 2,3x mais alto que a fala, canal
  demora 0,75 s pra "achar" o outro lado (era 0,25);
- a legenda dele pisca (ruído Perlin no alfa): sinal ruim;
- **só repete**: qualquer fala nova do Doyle é trocada por uma que ele já disse
  (`jaDitas`, em ciclo). Quem está do outro lado não tem o que dizer de novo.
- `DoyleInedito()` passa por cima disso — usado na última fala do capítulo, que
  precisa ser nova justamente porque quebra a própria regra.

**A virada:** corpo assenta → "Doyle." → surto de chiado (2,2 s) → a voz quebra →
"Miller? Cadê você?" → "Ele tá na minha frente."

**Medido em play:** antes do poço VozQuebrada=False; quebra em t=13,9 s da cena;
pedir frase inédita depois disso devolve "Tô no poço. Na aldeia, a leste." (repetição
funcionando); `DoyleInedito` passa direto.

## T83 — Luta de frente: tirar o robô e trocar [E] por ESPAÇO (2026-09-29)

**Reclamação:** "a animacao de matar de frente ta muito robotica e ruim, nao esta fiel;
troque o botao E por espaco".

**Por que parecia robô — quatro causas, todas no mesmo laço:**
1. **Os dois tocavam o MESMO clipe** (`Push_Loop`), só defasados 0,35 s: dois bonecos no
   mesmo motor. Agora o guarda usa `Sword_Block` (`AC_ForcejoGuarda`) — ele segura a
   lâmina com as duas mãos enquanto o Miller empurra. Corpos diferentes.
   `Sword_Block` não é laço, então o `Laco()` ganhou `costurar`: transição do estado pra
   ele mesmo em 92% com 0,18 s de mistura, senão congela no último quadro.
2. **O avanço era linear** (`(Forcejo-0,5)*0,22`): deslize suave. Virou MOLA com
   impulso — cada toque soma 1,9 de velocidade, a mola puxa de volta com atrito 7,5.
   Avança com solavanco e recua sozinho quando você para de bater.
3. **O tremor era um seno único aplicado igual nos dois** (`Sin(t*34)*0,02`), ou seja,
   sincronia perfeita. Agora é Perlin em 3 eixos, com frequências e sementes
   diferentes por corpo.
4. **Nada respondia ao toque.** Agora cada toque: pulso na velocidade da animação
   (+1,8, decaindo), torção do tronco, grunhido e tranco no zoom da câmera.

**Mais:** os dois se APROXIMAM conforme você ganha (`Forcejo*0,13` m) — a lâmina fecha
na cara dele, então dá pra ler quem está ganhando sem olhar a barra. A câmera fecha
0,55 de `orthographicSize` (entrada suave em 0,4 s) e treme; restaurada ao fim do laço.

**Som novo:** `Esforco_1..3.wav` — grunhido de glote fechada (harmônicos ímpares +
tremor de 22-34 Hz + formantes em 620/1180 Hz). O jogador toca no toque (trava de
0,16 s pra não metralhar); o guarda toca sozinho a cada 0,55-1,1 s com pitch 0,72.

**Tecla:** campo novo `teclaDoForcejo = Key.Space`, separado do `key = Key.E`. O [E]
começa o ataque, o ESPAÇO é a briga. O HUD mostra "[ESPAÇO]". Espaço não conflita:
só `Cutscene.Pular` e `Inspecao` usam, e nenhum dos dois está ativo numa luta.

**Pendente:** o console cospe `Setting the duration while system is still playing` toda
vez que sai sangue (`Sangue.Jato` linha 30 mexe em `duration` com o sistema tocando).
Não é fatal, mas suja o console — corrigir fora do play.

## T84 — A luta de faca não era animação: era CÂMERA (2026-09-29)

Depois do T83 ele disse que a luta e a kill continuavam ruins. Dessa vez fotografei em
vez de deduzir — e o diagnóstico mudou tudo.

**A luta acontecia a 24 m de altura, olhando 80° pra baixo, com enquadramento 5,45:
dois homens de 1,80 m ocupavam 3% da altura da tela.** Torção de tronco, tranco por
toque, lâmina fechando na cara do outro — tudo o que eu tinha ajustado no T83 era
SUB-PIXEL. Não havia animação que resolvesse: não dava pra ver.

**`MeleeKill.CameraDaLuta()`**: a briga ganha o tratamento que funcionou na cena do
poço — **48° em vez de 80°, enquadramento 2,3 em vez de 6, e DE LADO** (de perfil se
vê dois corpos; de cima, duas cabeças). Entra em 0,45 s, segura até o fim da facada, e
volta deslizando pro rig em 0,5 s. O lado é escolhido pelo mais próximo de onde a
câmera já está, pra o corte ser curto. O tranco de cada toque agora entra como pulso
no zoom (`trancoDaCamera`), não mais direto na lente.

**O que o close revelou (invisível antes):**
- A **chuva** tapava a briga: a gota tem 0,7 m de risco, calibrada pro enquadramento 6.
  Em 2,3 vira barra de néon. Agora `Tempestade.EscalaDaChuva()` mantém o tamanho NA
  TELA (escala startSize e o `velocityScale` do modo Stretch por
  `orthographicSize / CameraRig.TamanhoDeJogo`), e a densidade cai pra 38% no close —
  no tamanho certo ainda eram riscos demais por metro quadrado de tela.
- As **silhuetas de copa** também: já sumiam no plano aberto (T79), agora somem
  também no plano MUITO fechado. São calibradas pro zoom de jogo, nos dois sentidos.
- O **HUD do forcejo** era uma tarja preta de 420 px com "!!!" em corpo 30 — maior que
  os dois homens, lia como interface de depuração. Agora: barra de 190x7 px e a
  palavra ESPAÇO embaixo.

**Novo em CameraRig:** `TamanhoDeJogo` (último enquadramento fora de cutscene). Quem
dimensiona efeito na tela compara com isso, não com um número fixo.

**Medido:** zoom 6,00 → 2,30, pitch 80 → 48, altura 24 m → 8,8 m; volta ao normal no
fim. Verificado por foto: a briga é legível, a chuva não tapa.

**Armadilhas do teste (anotar):** `FindObjectsByType<GuardAI>` não devolve componente
DESABILITADO, e o `MeleeKill` nasce desligado até o Miller pegar a faca — meu teste
passou 4 rodadas com "alvo=NENHUM" por causa disso. Teleportar guarda com NavMeshAgent
exige `Warp()`, senão o agente devolve o transform. E tecla sintética via
`InputSystem.QueueStateEvent` não dispara `wasPressedThisFrame` de forma confiável:
use `MeleeKill.ExecutarAgora()`.

**NÃO verificado:** o desfecho VENCENDO (a facada no olho). Como a tecla sintética não
registra, só consegui observar o forcejo perdido. A câmera é a mesma nos dois, mas a
facada em si precisa de olho humano.
