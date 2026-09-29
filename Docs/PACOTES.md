# Pacotes que NÃO estão no git

O repositório guarda só o que é nosso (~682 MB): scripts, cenas, áudio, modelos
e texturas geradas. Os pacotes abaixo somam **5,7 GB** e ficaram de fora.

## Como montar o projeto no outro PC

1. Instale o **Unity 6000.6** (a mesma versão — o `ProjectSettings` está no repo).
2. `git clone` do repositório.
3. Abra o projeto no Unity. Ele vai reclamar de coisa faltando: é esperado,
   ainda não tem os pacotes.
4. No Package Manager, aba **My Assets**, baixe e importe os pacotes da tabela
   **com a mesma conta da Unity**.
5. Feche e reabra o projeto. A cena `Assets/_Game/Scenes/Selva.unity` é a principal.

> **Por que tem que ser a mesma conta e o mesmo pacote:** ao reimportar da Asset
> Store, os arquivos voltam com os **mesmos GUIDs**. É por GUID que a cena
> encontra cada prefab, material e animação. Se você baixar o mesmo modelo de
> outro lugar, ou outra versão do pacote, os GUIDs mudam e a cena abre cheia de
> buraco — objetos "Missing" que não dá pra recuperar sem refazer a mão.

## A lista

| pasta em Assets/ | tamanho |
|---|---|
| ForestVision/ | 2.1G |
| MonsterMutant 7/ | 1.1G |
| Dnk_Dev/ | 370M |
| JP Environmental Asset Pack/ | 345M |
| NikolayFedorov/ | 303M |
| Blackant Master Studio/ | 303M |
| RockFREE/ | 129M |
| sity/ | 93M |
| Tower/ | 93M |
| Survival Tools/ | 69M |
| Tensori/ | 64M |
| Assets_FenceChained/ | 55M |
| Coconut Palm Tree Pack/ | 53M |
| Keypad/ | 14M |
| AOSFogWar/ | 2.8M |
| ToonSoldiers_WW2_demo/ | 2.3M |
| Invector-3rdPersonController/ | 2.3M |
| AngeloMaN87/ | 462K |
| TutorialInfo/ | 91K |
| _TerrainAutoUpgrade/ | 28K |
