# Etapa 7 — Arte (modelos e animações no Blender)

Guia para o chat que vai receber as concept arts. **Leia também o `CLAUDE.md`** (seções "Regras de arte 3D" e "Regras de animação" — são do usuário e obrigatórias).

## Fluxo de trabalho
1. O usuário manda a concept art de UM modelo. Salve a imagem em `art_source/concepts/<nome>.png`.
2. Escreva `art_source/scripts/make_<nome>.py`. O modelo é gerado 100% por código, para ser reproduzível.
   - Use `ab_common.py`: `reset_scene`, `material`, `finalize`, `set_origin_to_feet`, `export_glb`, `save_blend`, `new_action`, `key_bezier`.
   - O `pipeline_test.py` é o exemplo de personagem preso ao `rig_base`, com animações.
3. Rode o script: `powershell -ExecutionPolicy Bypass -File tools\blender.ps1 art_source\scripts\make_<nome>.py`. Ele gera:
   - `assets/models/<nome>.glb`
   - `art_source/blend/<nome>.blend`
4. Confira o modelo e compare com a concept art. Itere até ficar fiel; ignore sombras e brilhos pintados na concept, foque em forma e cor.
   - Regras: `python tools/inspect_glb.py assets/models/<nome>.glb` (tamanho em metros, UV, materiais, ossos, animações).
   - Prévia: `powershell -ExecutionPolicy Bypass -File tools\blender.ps1 art_source\scripts\render_preview.py assets\models\<nome>.glb`
     - Gera `art_source/previews/<nome>.png` com as vistas frente | 3/4 | lado | costas.
     - Põe uma cápsula de 1,9 m ao lado para comparar a escala. Leia o PNG para ver o resultado.
     - `--frame N` mostra um frame de animação.
5. Na Godot:
   - Importe: `<godot_console> --headless --path . --import`
   - Confira: `<godot_console> --headless --path . -- --inspect-model assets/models/<nome>.glb`, que mostra o tamanho na Godot, os ossos e as animações com loop.
6. Encaixe no jogo (seção "Integração" abaixo), gere a build (`tools\build.ps1`) e entregue o roteiro de testes, como em toda mudança.

O pipeline foi testado de ponta a ponta na v0.11 com Blender 5.3 e Godot 4.7.2: rig, malha com pele, Actions exportadas e `-loop` virando loop na Godot.
- A Godot **tira o sufixo `-loop`** do nome e liga o loop sozinha: `idle-loop` vira a animação `idle` com loop Linear.
- `export_glb` filtra as opções do exportador glTF que não existem na versão instalada (os nomes mudam entre versões do Blender).
- `art_source/` tem `.gdignore`: a Godot não tenta importar `.blend` nem as concept arts. Só `assets/models/*.glb` entra no jogo.

## Convenções (valem para todos os modelos)
- **Escala e eixos:** 1 unidade = 1 m; Z para cima no Blender; exportar com +Y Up (o `export_glb` já faz).
- **Frente:** personagens e objetos com "frente" olham para **−Y** no Blender (a vista frontal do numpad 1 mostra a frente). O encaixe na Godot gira o que for preciso.
- **Origem:**
  - personagens e objetos apoiados no chão: nos pés / na base, no chão (Z = 0);
  - bola: no centro.
- **Materiais:** Principled BSDF simples. O jogo usa visual toon com contorno: os materiais do .glb serão trocados pelo shader toon do jogo, guiado pela cor base e pelo NOME do material. Use estes nomes:
  - `Uniforme` e `Uniforme_Detalhe`: o jogo pinta com a cor do time (azul/laranja). Modele com qualquer cor clara.
  - `Pele`, `Cabelo`, `Tenis`, `Olhos`, `Metal`, `Borracha`, `Madeira`, `Vidro`... (cor própria).
- **UV:** sempre (o `finalize` faz Smart UV se faltar). Texturas só se a concept pedir; prefira cores lisas.
- **Polígonos:** estilo low-poly limpo, como o placeholder. Referência: personagem até ~8 mil triângulos, bola ~1 mil, cesta ~3 mil.

## Medidas do jogo (valores padrão do tuning)
O jogo é estilizado: aro e bola são maiores que os reais. A física usa os valores do `Tuning.cs`, e o modelo tem que bater com eles.

| Peça | Medida | Observação |
|---|---|---|
| Personagem | altura total ~1,90 m (topo da cabeça), ombros ~1,48 m, quadril ~0,98 m | colisão: cápsula de raio 0,35 m e altura 1,8 m. O corpo (sem braços) cabe em ~0,7 m de largura |
| Bola | raio **0,22 m** (diâmetro 0,44) | origem no centro; gira pelo centro ao rolar |
| Aro | raio interno **0,55 m**, tubo ~0,035 m, centro a **4,3 m** do chão (`HoopHeight`, v0.13) | conector curto até a tabela |
| Rede | topo = raio do aro; fundo ~0,34 m de raio; comprimento ~0,94 m | será animada (balanço) na Godot |
| Tabela | **3,3 × 1,85 × 0,08 m**, base a 4,025 m do chão | face da frente a **0,80 m** atrás do centro do aro; quadrado de mira centrado no aro |
| Poste | raio ~0,16 m, altura ~5,7 m, a **2,6 m** atrás do centro do aro | braço horizontal até a tabela, na altura do centro dela |
| Base do poste | ~0,7 × 1,4 × 0,7 m (acolchoada) | a colisão é uma caixa de 0,7 × 4 × 0,7 m |
| Quadra | pequena 44 × 24 m (3 pts 8,5 m) · média 58 × 32 (9,5) · grande 80 × 44 (10,5) | **v0.18:** cena `Scenes/Court.tscn` (`Court.cs`, [Export] de medidas/cores). Linhas pelo shader `Shaders/court_floor.gdshader` (SDF em metros; garrafão/círculos/arco fixos, retas esticam); madeira = `assets/textures/wood_floor.png` (gerada por `art_source/scripts/make_wood_floor.py`, sem Blender) com UV de mundo (`WoodTileSize` m por repetição). Conferir com `--court-shot <png> [--fiba]`. Modelar só o que fica em volta: arquibancada, mureta, árvores, etc. |

**A cesta é um modelo só (`hoop.glb`) com objetos separados por nome:** `Aro`, `Rede`, `Tabela`, `Quadrado` (marcação), `Braco`, `Poste`, `Base`.
- Origem no chão, logo abaixo do centro do aro. O aro fica à frente (−Y) e a tabela atrás (+Y).
- Objetos separados permitem que o jogo reposicione cada peça se o tuning mudar o aro ou a tabela.

## Esqueleto: `art_source/blend/rig_base.blend`
- Gerado por `art_source/scripts/make_rig_base.py`, com 23 ossos.
- Nomes do perfil humanoide da Godot: `Root, Hips, Spine, Chest, UpperChest, Neck, Head`, e `Left/Right` + `Shoulder, UpperArm, LowerArm, Hand, UpperLeg, LowerLeg, Foot, Toes`.
- T-pose, olhando para −Y, pés na origem.
- **Quando chegar a concept do personagem:** ajuste só as alturas e larguras em `PROP` (no topo do script) e rode de novo. **Não renomeie nem acrescente ossos sem atualizar** `tools/inspect_glb.py` (`RIG_BONES`) e este guia.
  - Todos os personagens usam o mesmo esqueleto, então as animações servem para todos.
- Dedos: não há (mão fechada / luva). Se a concept pedir dedos, avise antes, porque isso muda o rig de todos.
- Animações: arquivo `art_source/scripts/anim_<nome>.py`, que abre o personagem e cria a Action.
  - Exporte o personagem com todas as Actions juntas (`export_glb(nome, animations=True)`).

## Animações necessárias (estados do jogo → nome da Action)
O jogo hoje anima o boneco por código (`Scripts/Game/PlayerRig.cs`). Cada estado abaixo já existe no jogo e vira uma Action. Todas a 30 fps, in place, com Bézier e ease. Durações são sugestões e alinham com os tempos do tuning.

| Action | Quando toca | Duração sugerida |
|---|---|---|
| `idle-loop` | parado, sem bola | 2 s |
| `idle_ball-loop` | parado com a bola, quicando com a mão direita | 1 quique ≈ 0,5 s |
| `walk-loop` | andando, sem bola | 1 ciclo ≈ 1 s |
| `run-loop` | correndo, sem bola | 1 ciclo ≈ 0,7 s |
| `walk_ball-loop` / `run_ball-loop` | andando/correndo com a bola | iguais às de cima |
| `defense-loop` | marcando de perto quem tem a bola (base baixa, braços abertos) | 1 s |
| `shoot_charge-loop` | segurando o arremesso (bola acima da cabeça, mira) | 1 s, quase parado |
| `shoot` | soltou o arremesso (no chão ou no ar) | 0,4 s |
| `pass_charge-loop` | segurando o passe | 1 s |
| `pass` | soltou o passe (peito) | 0,3 s |
| `pass_high` | passe alto (por cima da cabeça) | 0,35 s |
| `jump_charge` | agachando para pular (segura até soltar) | 0,2 s; segurar o último frame |
| `jump-loop` | no ar (sobe/desce); braços para cima = bloqueio | 0,6 s |
| `land` | pousou (trava ao pousar) | 0,3 s |
| `dribble_special-loop` | drible especial (agachado, quique rápido, protegendo a bola) | 0,4 s |
| `steal` | bote do roubo (impulso para a frente, braço esticado) | 0,25 s |
| `steal_fail-loop` | errou o roubo: lento e cansado | 1 s |
| `dunk` | enterrada (voo roteirizado: sobe, segura no aro, desce) | igual ao voo do jogo (0,8 s ± 0,2 s de variação; `DunkFlightTime`/`DunkFlightJitter` no tuning) — a Godot ajusta a velocidade |
| `stunned-loop` | atordoado (cambaleando, estrelinhas já são do jogo) | 1,2 s |
| `catch` | recebeu a bola | 0,25 s |
| `free_throw_idle-loop` | esperando o skill check do lance livre | 1,5 s |
| `celebrate-loop` | ponto do próprio time / vitória | 2 s |
| `sad-loop` | derrota | 2 s |

**Bola nas mãos:** a posição da bola é calculada pela física do jogo. Na Godot, os braços serão puxados até a bola por IK (como o placeholder já faz), por cima da animação. Então:
- as animações com bola só precisam deixar a mão no lugar aproximado;
- **não** anime a bola dentro do .blend.

## Modelos prontos
- **v0.32 — cenário da praia:** `art_source/scripts/make_scenery.py` (sem concept) → `palmeira_a/b`, `arquibancada` (material
  `Assento` pintado com a cor do time na Godot), `guarda_sol`, `espreguicadeira`, `torre_salva_vidas`, `poste_luz` (`Lampada`
  vira unshaded), `pedra_a/b`, `arbusto`, `prancha`. Montagem em `Scripts/Game/Scenery.cs` (posições sorteadas com semente fixa,
  mar por shader no fundo +X, areia com ruído). Frente dos objetos = −Y no Blender (+Z na Godot).
| Modelo | Script | Concept | Encaixe | Versão |
|---|---|---|---|---|
| Bola (`ball.glb`) | `make_ball.py` | `concepts/ball.png` | `Ball._Ready`: instancia o .glb escalado para `Tuning.BallRadius` (o modelo tem raio 0,22 m). Materiais `Borracha` (contorno 0,012) e `Costura` (sem contorno). Se o .glb faltar, usa a esfera placeholder. | v0.14 |
| Cesta (`hoop_azul.glb`, `hoop_laranja.glb`) | `make_hoop.py` (gera as duas) | `concepts/hoop.png` | `HoopVisual.Create` (chamado no `CourtBuilder.BuildHoop`; sem o .glb usa o placeholder). Hoop 0 = azul, hoop 1 = laranja (cor de quem DEFENDE). Cada peça é posicionada/escalada pela geometria do tuning (`CourtGeo`). Aro e Rede ficam num `RimPivot` (dobradiça na frente da tabela) que gira para tremer. Rede: shader próprio com balanço (`push`, `stretch`, `flare`) + contorno; gatilhos via `Sfx.Played` ("swish", "rim", e a deixa muda "fx_dunk"), que também dispara no cliente online. Tuning: `NetSwishStrength`, `RimDunkShake`, `RimHitShake`. | v0.17 |

| Rei (`rei.glb`) — 1º personagem | `make_rei.py` | `concepts/rei.png` | `CharacterVisual` (ver seção "Personagem: o Rei"). | v0.21 |
| Raio (`raio.glb`) — 3º personagem (nome provisório) | `make_raio.py` (importa `make_rei` e `make_ametista`) | `concepts/raio.png` | `CharacterVisual` via `Characters.All[2]`. | v0.25 |
| Ametista (`ametista.glb`) — 2º personagem (nome provisório) | `make_ametista.py` (importa funções do `make_rei.py`) | `concepts/ametista.png` | `CharacterVisual` via `Characters.All[1]` (ver "Novos personagens"). | v0.24 |

- `Mat.ApplyToon(raiz, nome => espessura)` troca os materiais importados pelo toon do jogo, com a cor base de cada material do .glb. A função recebe o nome do material e devolve a espessura do contorno (0 = sem contorno). Use também nos próximos modelos.
- Bola: as costuras são faixas em relevo de 2,5 mm na mesma malha (~1,9 mil triângulos). Gomos e textura da concept foram ignorados de propósito (visual toon liso).

## Personagem: o Rei (v0.21)
- **Modelo** (`make_rei.py`): chibi de 1,4 m até o topo da coroa (como na concept). Objetos: `Corpo` (casaco com forro por
  Solidify, gola, botões, calça, botas), `Cabeca` (cabeça, pescoço, orelhas, cabelo), `Coroa` (borda e faixa `CoroaBrilho`, `Gema`),
  `Rosto_Serio/Focado/SorrisoFrio/Bravo/Feliz/Surpreso` (decalques projetados na cara) — todos com pele no rig — e
  `MaoEsquerda`/`MaoDireita` (esferas SEM osso). ~14 mil triângulos (o cabelo espetado e o casaco com espessura pesam).
- **Esqueleto:** `make_rig_base.PROP` agora tem as proporções chibi (quadril 0,45 m, cabeça a partir de 0,86 m). `build(prop)` aceita
  proporções de outro personagem com os MESMOS nomes de ossos. Os ossos dos braços existem mas o Rei não usa (sem braços).
- **Animações: feitas na Godot** (regra do usuário "tente criar as animações na própria Godot"), em `Scripts/Game/CharacterVisual.cs`:
  pose por osso em graus nos eixos do personagem (helpers `Lean`, `Look`, `Leg`, `Shrug`, `Crouch`), camadas com peso suave
  (locomoção parado/andar/correr pela velocidade e passada do tuning; por cima: quique, marcação, comemoração, triste, roubo errado,
  drible especial, passe, arremesso, carga do salto, no ar, pouso, soltar passe/arremesso, bote, atordoado, enterrada por curva de
  chaves no progresso do voo, pendurado no aro). Tudo sincronizado com a física (fase do quique, `FlightT/FlightDur`...).
- **Mãos:** alvo por estado (pose) e, com a bola, presas a ela (quique: encontra no alto, empurra e espera; arremesso/passe/enterrada:
  pegada com as duas mãos); pendurado: na frente do aro. Suavização `HandFollow`.
- **Rosto:** expressão da roda (`Player.Emote`, replicada) ou automática pelo estado (`UpdateFace`).
- **Escala:** `Tuning.CharacterScale` (padrão 1,3 → ~1,8 m, a altura da colisão). A física não muda com a escala.
- **Conferir:** `<godot_console> --path . -- --pose-shot <png> [--side]` → `_a`.. `_e` (todos os estados, 3/4 ou de lado) e `_faces`.
  No Blender: `render_preview.py assets/models/rei.glb --no-ref --hide Rosto_Focado,Rosto_Sorr,Rosto_Bravo,Rosto_Feliz,Rosto_Surp`.
- Sem o .glb, o jogo volta para o boneco placeholder (`PlayerRig`); os dois implementam `IPlayerVisual`.

## Novos personagens (v0.24)
- Script `make_<nome>.py` que importa `make_rei as rei` e reaproveita `make_head` (troque `rei.make_hair` pelo cabelo novo), `Face`/`eye`/`brow`, `skin`, pesos, `make_legs`, `rounded_box`, `tube`. Materiais: preencha `rei.M` com a paleta nova.
- **Sempre objetos separados:** `Corpo`, `Cabeca`, `Coroa` (só se o personagem tiver), `Rosto_*` (os 6 MESMOS nomes: Serio, Focado, SorrisoFrio, Bravo, Feliz, Surpreso) e `MaoEsquerda`/`MaoDireita` sem osso.
- Registrar em `Scripts/Game/Characters.cs` (nome, .glb, rótulos da roda). Materiais novos: incluir o nome no `switch` de `CharacterVisual.ApplyMaterials` para ganhar contorno.
- Conferir: `render_preview.py assets/models/<nome>.glb --no-ref --hide Rosto_Focado,...` e `--pose-shot <png> --char N`.
- Raio (v0.25, `make_raio.py`, concept `concepts/raio.png`): jaqueta bomber fechada (`J_OPEN`, estufada por `jr`) com punho na cintura; decalques neon por `jacket_poly`/`jacket_strip` (coordenadas em metros sobre a jaqueta), zigue-zague nas pernas (`make_leg_stripes`), raio em relevo no tênis; reaproveita camiseta/quadril/barra/tênis do `make_ametista.py` trocando os nomes dos materiais pelo dicionário `PAL` (chave usada pelas funções → nome no .glb: `TenisVerde`→`TenisDetalhe`, `Sola`→`SolaNeon`). Cabelo = espetos `strand` em anéis (`hair_spike`). Rostos: Serio = "Tranquilo", SorrisoFrio = "Sorriso confiante", Bravo = "Irritado", Feliz = "Empolgado". `Neon`/`SolaNeon` são sem sombra na Godot (brilham).
- Pele do Rei: `#DFAA84` desde a v0.25 (antes `#F5E6D3`, pálida demais no toon). O toon clareia bastante: escolha a pele uns tons mais escura do que quer ver.
- Ametista: casaco aberto (`FRONT_OPEN`), camiseta, quadril de calça por baixo, barra dobrada, tênis; cabelo = mechas Bézier (`strand`) + volume do topete; rostos: Serio = "Relaxado", SorrisoFrio = "Sorriso convencido", Feliz = "Rindo".

- **v0.26:** personagem novo também precisa de estrelas no Tuning (`<Nome>Star*`, seção com `Sub = "Estrelas"` — vira aba no artifact), 4 `AbilityDef` em `Characters.cs` e a lógica em `PlayerAbilities` (+ bots em `BotBrain`). Nomes atuais: King (rei.glb), Luck (ametista.glb), Bolt (raio.glb).

## Integração na Godot (fazer quando o 1º modelo chegar)
- **Bola:** ✅ feito na v0.14 (ver "Modelos prontos").
- **Cesta:** ✅ feito na v0.17 (ver "Modelos prontos"). Texto antigo: `CourtBuilder.BuildHoop` cria as peças por código. Trocar por `hoop.glb`, posicionando cada peça pelo nome conforme `CourtGeo` (`RimCenter`, `Board`, `PoleBase`). As colisões continuam as do código.
  - Hoop 0 fica em −X e hoop 1 em +X: girar ±90° para a frente (−Y do Blender = −Z na Godot depois do +Y Up) apontar para o centro da quadra.
- **Personagem:** criar `CharacterVisual` com a mesma interface do `PlayerRig` (`Update(RigState)`), carregando o .glb.
  - Usar `AnimationTree` (máquina de estados + blend de locomoção pela velocidade), IK dos braços até a bola (`SkeletonIK3D` ou modifier) e cor do time no material `Uniforme`.
  - Manter o `PlayerRig` como reserva enquanto o modelo não existir: se o .glb não estiver em `assets/models/`, usa o placeholder.
  - Giro: o modelo olha para −Y no Blender, que vira +Z na Godot; o jogo usa frente = −Z, então girar 180° no encaixe.
- **Cenário:** arquibancada, mureta e árvores em `CourtBuilder.BuildDecor`/`BuildWalls`.
- Contorno toon: o jogo usa `Mat.ToonOutlined`. No encaixe, trocar os materiais importados por toon com a cor base de cada um (por nome).

## Ordem sugerida
1. Personagem base (T-pose) + ajuste do `rig_base` às proporções da concept.
2. ~~Bola~~ ✅ (v0.14).
3. ~~Cesta~~ ✅ (v0.17).
4. Animações: primeiro as de locomoção e arremesso (`idle`, `walk`, `run`, `*_ball`, `shoot_charge`, `shoot`), depois o resto.
5. Cenário.

## Pendências fora da arte (lembrar o usuário)
- **Predição do próprio jogador no cliente online**: o boneco do amigo responde com o atraso da rede. Não fazer até o usuário pedir.
