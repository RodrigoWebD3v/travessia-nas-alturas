# Travessia nas Alturas

Jogo de plataforma 3D com travessias de equilíbrio sobre cordas, feito em Unity 6 (6000.6.5f1, URP).
O Equilibrista usa uma vara longa para se equilibrar, coleta moedas e alcança a bandeira antes do tempo acabar.

## Controles

| Tecla | Plataforma | Corda |
|---|---|---|
| W/S | Avançar/recuar | Avançar/recuar |
| A/D | Mover para os lados | Levantar o lado esquerdo/direito da vara |
| Espaço | Pular | — |
| Ctrl (segurar) | Abaixar | Abaixar para desviar dos pássaros |
| Enter | Reiniciar após Game Over | |

## Estrutura

- `Assets/Scripts`: código do jogo (jogador, equilíbrio, corda, itens, HUD, áudio, pássaros).
- `Assets/Editor/TravessiaBuilder.cs`: monta materiais, prefabs, HUD e as duas fases (menu **Travessia > Construir Tudo**).
- `Assets/Art`: modelos FBX, texturas, materiais e animações.
- `Assets/Audio`: trilhas e efeitos sonoros próprios.
- `ArtSource/Blender`: scripts Python e arquivos `.blend` que geram todos os modelos, o rig e as animações.
- `ArtSource/Audio`: script que sintetiza as trilhas e os efeitos.

## Como abrir

1. Instale o Git LFS e clone o repositório (`git lfs install` antes do clone).
2. Abra a pasta no Unity Hub com a versão 6000.6.5f1.
3. Abra `Assets/Scenes/Fase1_JardimSuspenso.unity` e aperte Play.
