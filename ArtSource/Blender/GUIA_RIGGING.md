# Guia de rigging do Equilibrista (Blender 5.2 → Unity 6)

O script `gerar_personagem.py` faz tudo isto automaticamente. Este guia explica cada etapa e como repeti-la à mão na interface do Blender.

- **Modelo:** manequim humanoide bípede e estilizado, sempre segurando uma vara.
- **Rig:** genérico (Generic na Unity), com 18 ossos.
- **Animações:** Idle, Walk, RopeIdle, RopeWalk, Jump e Fall.

Para regerar o personagem:

```bash
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --python ArtSource/Blender/gerar_personagem.py
```

Depois, rode **Travessia > Construir Tudo** na Unity.

---

## 1. Estrutura de ossos (Armature)

```
Hips                      (raiz, no quadril, aponta para cima)
├── Spine
│   └── Chest
│       ├── Neck
│       │   └── Head
│       └── ShoulderRig   (pivô entre os ombros; o jogo gira este osso com a vara)
│           ├── UpperArm.L → LowerArm.L → Hand.L
│           └── UpperArm.R → LowerArm.R → Hand.R
├── UpperLeg.L → LowerLeg.L → Foot.L
└── UpperLeg.R → LowerLeg.R → Foot.R
```

**Pontos críticos:**

- **Ombros (`ShoulderRig`):** um osso extra entre o peito e os braços. Os dois braços são filhos dele, então girar esse osso levanta um lado da vara e abaixa o outro, e os braços acompanham sem deformar o tronco.
- **Bacia (`Hips`):** é a raiz. Tronco e pernas saem dela, e o sobe-e-desce do passo é feito movendo só esse osso.
- **Joelhos:** o joelho fica 1,5 cm à frente da linha quadril-tornozelo. Essa leve dobra define o sentido natural da flexão; sem ela, a perna pode dobrar para o lado errado.

**Na interface:** `Add > Armature`, depois `Tab` (Edit Mode). Use `E` para extrudar ossos e `Ctrl+P > Keep Offset` para definir os pais. Nomeie com `.L`/`.R` para que `Pose > Paste Pose Flipped` espelhe as poses.

**Alternativa manual (Rigify):**

1. Ative o add-on Rigify.
2. Use `Add > Armature > Human (Meta-Rig)` e ajuste os ossos ao corpo.
3. Clique em `Generate Rig`.

O Rigify gera controles prontos de IK/FK. Para a Unity, porém, é preciso exportar só os ossos `DEF-*`, o que dá mais trabalho. Por isso o projeto usa um esqueleto simples.

## 2. Skinning (pesos)

- **Comando:** selecione a malha, depois o armature com Shift, e use `Ctrl+P > With Automatic Weights` (no script, `parent_set(type="ARMATURE_AUTO")`).
- **A malha precisa ser fechada e sem sobreposições.** Por isso o corpo é fundido por *Remesh Voxel* antes do skinning. Peças soltas e sobrepostas fazem o cálculo de pesos falhar ("Bone Heat Weighting failed").
- **Membros afastados:** cotovelos longe do tronco e pernas separadas. Se partes encostam, o peso "vaza" (por exemplo, o braço puxando a pele da cintura).
- **Conferência:** abra `Weight Paint`, selecione cada osso e procure manchas fora do lugar. Corrija com o pincel `Subtract` ou com `Smooth`.
- **Teste de estresse:** em Pose Mode, gire coxa e joelho a 90° e o ombro a 30°. Se a dobra afinar demais, adicione geometria na articulação ou suavize os pesos.
- O script imprime `Grupos sem pesos: []` quando todos os ossos receberam pesos.

## 3. Controles e animação (IK/FK)

- As animações foram feitas em **FK** (rotação direta dos ossos). O passo é um ciclo simples e a Unity recebe só rotações, então FK basta e exporta sem perdas.
- **Para animar à mão com IK nas pernas:**
  1. Crie um osso `IK_Foot.L` sem pai, no calcanhar.
  2. Selecione `LowerLeg.L`, `Bone Constraints > Inverse Kinematics`, com Target = armature, Bone = `IK_Foot.L` e Chain Length = 2.
  3. Adicione um *pole target* à frente do joelho para controlar a direção da dobra.
  4. Antes de exportar, use `Pose > Animation > Bake Action` com *Visual Keying* marcado, para gravar o resultado em FK.
- **Braços:** as mãos ficam presas à vara. No jogo, quem move os braços é o osso `ShoulderRig`; nos clipes, os braços quase não se mexem.
- **Ciclos:**
  - Walk: 24 quadros. Contato no quadro 1, passagem no 7, contato oposto no 13, passagem oposta no 19, e o quadro 25 repete o 1.
  - RopeWalk: 40 quadros, com passos curtos e os pés girados para dentro (para a linha da corda).
- Cada ação fica numa faixa NLA própria, e o FBX exporta com `Bake Animation > NLA Strips`.

## 4. Pronto para a Unity

- **Nomes:** sem espaços nem acentos. O sufixo `.L`/`.R` funciona como Generic. Para usar Humanoid no futuro, a Unity reconhece esses nomes no mapeamento automático.
- **Eixos:** o personagem é modelado olhando para **-Y** no Blender. A exportação usa *Forward -Z, Up Y* e *Apply Scale: FBX All*, e na Unity o importador tem **Bake Axis Conversion** ligado. O modelo chega olhando para -Z, e o prefab o gira 180° para nascer de frente para o percurso (+Z).
- **Exportação:** *Add Leaf Bones* desligado e *Armature FBXNode Type: Null*. Sem isso aparecem ossos extras.
- **Importador (configurado pelo construtor):**
  - Rig: Generic.
  - Animações importadas e nomeadas pelo take.
  - *Loop Time* ligado em todos os clipes, exceto Jump.
- **Controle no jogo:** em `PlayerController.UpdateAnimation`, o estado do movimento escolhe o clipe com `CrossFade`.
  - Caminhar: Walk.
  - Na corda: RopeIdle ou RopeWalk.
  - No ar: Jump.
  - Caindo: Fall.
  - A velocidade da animação acompanha a velocidade do personagem.
- **Vara:** em `LateUpdate` (depois do Animator), o osso `ShoulderRig` gira pelo ângulo da vara calculado em `BalanceController`.

## Fontes

- `gerar_personagem.py`: malha, esqueleto, pesos, animações e exportação.
- `Equilibrista_Rig.blend`: arquivo salvo com rig e ações, para edição manual.
- `ArtSource/Previews/anim_*.png`: quadros de conferência de cada animação.
