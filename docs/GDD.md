# Absolute Basketball — Documento de Design (especificação do usuário)

> Fonte da verdade das regras. Texto original do usuário preservado abaixo; decisões de implementação
> e interpretações ficam na seção "Decisões" no fim. Não mudar regras sem o usuário pedir.

## Visão geral
- Basquete 3D, terceira pessoa, multijogador online, **Godot 4 C#**. Arte final no **Blender**.
- Estilo visual levemente inspirado em *Just Volleyball* (colorido, ensolarado, personagens cartunescos).
- Bots decentes assim que o jogo for minimamente jogável.
- Times divididos de qualquer jeito (2v2, 1v3, 5v4…). Tamanho da quadra escolhido pelo host antes da partida:
  **pequena ≤ 6 jogadores, média ≤ 8, grande ≤ 10**. Quadras bem menores que as reais.
- Pontos para vencer escolhidos antes da partida (padrão **21**). Opção **por tempo** (sem limite de pontos; host escolhe o tempo).
- Configurações: volume, fullscreen (começa ativo), seção "Controles" com remapeamento, sensibilidade da câmera e corrida alternar/segurar.

## Mecânicas (texto do usuário)
**Gameplay:** O jogador que está com a bola quica ela automaticamente enquanto se mexe. Quando a bola está no chão, qualquer jogador que passar por ela a pega. Cestas de dentro da área de 3 valem 2 pontos e de fora valem 3. Estamina só é gasta ao correr e recarrega rápido; jogadores não são tão rápidos no geral. Não tem fora; quando um time faz cesta o round reinicia e a bola vai para o outro time. Sempre que um round reinicia (falta ou cesta) ou a partida começa, há contagem regressiva de 3 s antes de qualquer ação.

**Arremesso:** Segurar o botão carrega; quanto mais tempo, mais longe e alta a bola vai (até um limite); soltar arremessa. Indicativo visual da trajetória para quem está carregando. Apertar **variação** durante a carga ou carregar por **4 s** cancela (continua com a bola). Não dá para bloquear arremessos sem saltar, mas dá para pegar a bola no ar se ela já estiver **caindo** (não subindo).

**Passe:** Segurar carrega um passe para qualquer aliado dentro do alcance. Toque rápido passa para o **aliado livre mais próximo**; segurar não muda o alcance, mas passa para o aliado **mais na direção em que você olha** ao soltar (alvo visual no aliado enquanto carrega). Não pode mirar aliado com algo bloqueando a trajetória, mas qualquer jogador que entrar na frente depois do passe pega a bola. Variação durante a carga troca para **passe alto**: quem antes não podia receber passa a poder (se nada bloquear a trajetória mesmo assim).

**Salto:** Apertar pula em altura mediana e **bloqueia arremessos e passes altos** (a bola quica no corpo e cai; o bloqueador não pega). Segurar aumenta um pouco a altura (rápido; parado enquanto carrega) e salta ao soltar. Quase sem controle aéreo, mas a direção do salto vem do movimento antes de pular. Ao pousar: **0,5 s** sem mover nem agir.

**Enterrada:** Normalmente não dá para saltar com a bola, mas correndo na direção da cesta adversária e dentro de **3 m** dela, pode saltar para enterrar. Para acertar, apertar o **botão esquerdo** (arremesso) precisamente ao tocar a cesta — skill check estilo *Dead by Daylight*, porém mais difícil. Errando: bola cai do lado da cesta, jogador cai e fica **atordoado 2 s**. Saltos podem bloquear enterradas, mas é **falta**: round reinicia com o jogador que sofreu a falta começando com a bola.

**Roubo:** Sem a bola, apertar roubo dá um pequeno impulso para frente e rouba a bola de quem acertar (exceto se ele estiver driblando ou no ar). Errando: **2 s** com **20%** da velocidade, sem correr, pular ou roubar. Não dá para roubar quem está carregando arremesso.

**Drible:** Com a bola, drible por **1 s**: imune a roubos e **50% mais lento**, sem outras ações além de mover. **2 cargas**; recupera 1 a cada **10 s** (o timer corre sempre que houver ao menos 1 carga faltando), máximo 2.

## Controles
| Ação | Teclado/Mouse | Controle Xbox |
|---|---|---|
| Mira | Mouse | Analógico direito |
| Movimento | WASD | Analógico esquerdo |
| Correr | Shift | LT |
| Arremesso | Botão esquerdo | RT |
| Passe | Botão direito | RB |
| Salto | Espaço | A |
| Drible | Q | B |
| Roubar | E | X |
| Variação | R | Y |

Teclado e controle funcionam ao mesmo tempo; dá para trocar no meio da partida.

## Revisões do usuário (v0.2) — substituem o texto acima quando conflitarem
- Quadras **bem maiores**: a pequena é maior que a grande da v0.1 (agora P 28×16, M 34×19, G 40×22 m).
- Bola **maior** (estilizada, proporcional ao boneco); cesta **maior e mais alta** (aro r=0,5 m a 4 m).
- **Não dá para arremessar de um lado ao outro da quadra** (alcance máximo 13 m).
- **Arremesso não pode mais ser cancelado.** Com **3 s** de carga o jogador arremessa sozinho.
- Assistência de mira leve, **só na cesta adversária**. Dá para fazer cesta contra (ponto para o outro time), sem assistência.
- **Câmera para baixo encurta o arremesso**; com a câmera no meio da tela ou acima, usa todo o alcance carregado.
  O alcance cresce com a carga (mais devagar que na v0.1). Bola um pouco mais rápida.
- Dentro da **área de 3 m da cesta adversária** o arremesso vira **bandeja** (mira e distância automáticas, arco alto).
- O jogador **quica a bola mesmo parado**; som do quique sutil.
- **Multiplayer será pela Steam** (sem publicar o jogo — usar o AppID de teste 480/Spacewar como em outro projeto do usuário). Etapa 5.

## Revisões do usuário (v0.3) — prevalecem sobre as anteriores
- Assistência **sutil**: só corrige quando a mira já está bem perto (direção e distância/altura), sem "teletransportar" a mira.
- **Distância do arremesso = inclinação da câmera.** O meio da tela pede o alcance inicial; subir a câmera pede mais longe;
  descer pede mais curto. A carga **só aumenta o limite máximo** (a distância não cresce sozinha). Carga um pouco mais rápida.
- Quadras ainda maiores, com diferença bem maior entre tamanhos (P 44×24, M 58×32, G 80×44 m).
- Menos bola batendo no aro sem entrar (ainda possível de vez em quando).
- **Contagem de 3 s só no início da partida**; depois de cesta/falta o round recomeça direto.
- **Quadrado da tabela útil:** acertar nele é um bom jeito de fazer cesta (tabelinha, como no basquete real).
- Corrida um pouco mais rápida.

## Revisões do usuário (v0.4) — prevalecem sobre as anteriores
- Estamina: recarga um pouco menor, estamina máxima maior.
- **Sem bandeja/ajuda de perto da cesta** (removida).
- Tabelinha ajuda só um pouco (não puxa a bola totalmente para a cesta).
- **Arremesso:** segurar NÃO aumenta mais nada; o alcance máximo já está disponível desde o início. A distância vem só da
  mira (câmera); a "força" mostrada é a distância usada. Arremesso automático depois de **4 s**.
- **Passe carregado:** não escolhe aliado; lança a bola na direção em que você olha (mesmo sem aliado/fora de alcance).
  É possível fazer cesta assim, mas é difícil e sem assistência. Passe automático depois de 4 s segurando.
  Carregando o passe: 25% da velocidade e não pode ter a bola roubada.
- Passe rápido (toque): alcance um pouco maior; passes mais rápidos e o normal um pouco mais reto.
- **Cesta por baixo não vale.**
- Mouse escondido durante o jogo; cursor não pode "congelar" nos menus depois do alt-tab.
- **Sem avisos de texto no meio do jogo** (ROUBOU, TOCO, metros etc.). Só mensagens de ponto/prorrogação/fim. Não
  adicionar novos avisos sem o usuário pedir.
- Drible: boneco agacha um pouco e quica mais rápido.
- Carregando o salto, o boneco olha para a direção da câmera (pulos para os lados).
- Botão padrão do drible no teclado: **E** (o mesmo do roubo; drible só com a bola, roubo só sem).

## Revisões do usuário (v0.5) — prevalecem sobre as anteriores
- Sem texto e nada chamativo, mas **com indicativos discretos** (ex.: se o passe é alto ou não).
- Prévia da trajetória do arremesso precisa bater com o arremesso real.
- Cursor nunca visível durante o jogo e nunca "congelado" depois do alt-tab (tela cheia).
- Passe carregado um pouco mais fraco.
- **Tapinha (novo):** bola bem pertinho da cesta + jogador do time que pontua nessa cesta pula na direção dela →
  completa a cesta, sem skill check. Bloquear isso com salto **não** é falta (só bloquear enterrada é falta).

## Revisões do usuário (v0.6) — prevalecem sobre as anteriores
- Alcance da enterrada bem maior (agora 7 m).
- Skill check bem mais difícil; **Espaço também vale** no skill check (além do botão de arremesso).
- **Tapinha removido.**

## Revisões do usuário (v0.7) — prevalecem sobre as anteriores
- **Espaço não vale mais** no skill check da enterrada (só o botão de arremesso).
- Indicação de quando dá para enterrar (setinhas verdes acima da mira — sem texto).
- Janela certa do skill check = **0,06 s**.
- Arremesso automático e passe automático: **3 s** (1 s a menos).
- **Não precisa mais estar correndo** para enterrar.
- Redução de velocidade de quem tem a bola **+5%** (multiplicador 0,92 → 0,87).
- **Falta = 2 lances livres** de 1 ponto, sem ninguém interferir. Errou o 1º → vai direto para o 2º. Errou o 2º → todos se mexem e o
  jogo continua (rebote). Os lances livres são **só skill checks** tão difíceis quanto o da enterrada; acertou = arremesso automático
  que entra garantido.
- Velocidade driblando = **70% da velocidade andando**.
- Bots pulavam em lugares que não impediam a bola → corrigido.
- Bots/bonecos sem a bola e sem marcar ninguém devem agir de forma mais natural (mesmo espalhados).

## Decisões de implementação (Claude)
- **Rede (Etapa 5, v0.8):** host autoritativo. Toda a simulação (jogadores, bola, regras) roda no host a partir de
  `PlayerInput` (humano local, bot ou cliente remoto). Offline = sem sessão.
  - **Transporte trocável** (`INetTransport`): `SteamTransport` (SteamNetworkingSockets P2P pelos relays da Steam,
    Steamworks.NET 2025.164.1, AppID 480 de teste) e `EnetTransport` (IP direto, para testar com duas janelas no mesmo PC /
    rede local e para o teste automático).
  - **Lobby** mantido pelo host (`NetSession`): membros (nome, time), configurações (quadra, vitória, pontos/tempo,
    jogadores por time, dificuldade dos bots). Vagas vazias viram bots. Steam: lobby só amigos ou público, código do lobby,
    lista de públicos, convite direto pela Steam (sem overlay) ou pela janela do overlay; "Entrar no jogo" no perfil
    (rich presence `+connect_lobby`). O jogo se reabre uma vez ao iniciar para a Steam colocar o overlay (truque do Just Fall).
  - **Partida:** o host manda `Start` (configurações, escalação na mesma ordem para todos → mesmos `Player.Id`, e o tuning
    do host, aplicado só em memória no cliente). Cliente manda input a cada tick (não confiável; apertos como contadores por
    botão → pacote perdido não perde aperto). Host manda snapshot a cada 2 ticks (~30/s, não confiável): fase, placar,
    relógio, lances livres, bola e o estado de cada jogador (`Player.NetState`) + os sons tocados. Mensagens da HUD
    (contagem, placar, prorrogação, fim) vão confiáveis.
  - **Cliente não simula:** desenha ~5 ticks (~83 ms) atrás do último snapshot, interpolando posição/rotação; a bola na
    mão é calculada pela mão do dono. Ainda **sem predição** do jogador local (ele responde com o atraso da rede).
  - Online o jogo não pausa (Esc só abre o menu). Cliente que cai: um bot assume o jogador dele. Host que sai: a partida
    acaba para todos. Fim de jogo: o host escolhe "Jogar de novo" ou "Voltar ao lobby".
- **Física da bola própria** (não RigidBody): determinística, a mesma função alimenta o voo real e a prévia de trajetória.
- **Arremesso (v0.2):** direção = yaw da câmera (+ assistência na cesta adversária). Distância alvo = alcance da carga ×
  fator da inclinação da câmera, com assistência de distância perto da cesta adversária. A velocidade é calculada para a
  bola cruzar a altura do aro nessa distância, com ângulo entre "perto" e "longe" e um **ângulo mínimo de entrada**
  (a bola é grande; sem isso raspa no aro). Prévia desenha até o primeiro contato.
- **Pontuação:** bola que entra na cesta vale 3 se o arremesso saiu de fora da linha (posição dos pés na soltura) e
  a bola ainda estava "em arremesso" (não tocou o chão). Qualquer outra entrada vale 2. Ponto vai para o time que ataca aquela cesta.
- **Treino:** sem adversários, a bola sempre volta para o jogador após a cesta. Bonecos de treino configuráveis no menu
  (aliado devolve passes; adversário pula contra arremessos, dribla quando você chega perto e ataca com a bola).
- **Aro amigável / tabelinha:** batidas por dentro do aro perdem velocidade para fora; bola que bate no quadrado da tabela
  (área desenhada = área ativa) é desviada para a cesta (`BankAssist`).
- **Passe:** toque (< `PassTapTime`) = aliado livre mais próximo; segurando = aliado mais na direção da câmera (marcador
  amarelo; rosa = passe alto). Corredor bloqueado por jogador a menos de `PassLaneRadius` da linha. Passe normal: qualquer um
  que tocar pega (interceptação). Passe alto: arco até `HighPassApex`, só pega caindo; bloqueável por quem está no ar.
- **Salto:** sem a bola. Toque = salto rápido; segurar agacha (parado) e salta mais alto ao soltar. Direção = movimento antes
  de pular. Bloqueia arremessos e passes altos do adversário (bola quica e cai; bloqueador não pega). Trava ao pousar.
- **Roubo:** bote para frente; acerta quem tem a bola dentro de `StealReach`/`StealAngleDeg`. Alvo driblando, no ar ou
  carregando arremesso = falha. Falha = penalidade (lento, sem correr/pular/roubar).
- **Enterrada (Etapa 3):** com a bola, **correndo (Shift)** na direção da cesta adversária (≤ `DunkMaxAngleDeg`) e a até
  `DunkRange` do aro, o salto vira enterrada: voo roteirizado de `DunkFlightTime` até a bola ficar sobre o aro. Skill check:
  anel em volta da mira, ponteiro vermelho gira 270° até o toque na cesta; zona branca = janela `DunkWindow` centrada no toque.
  Apertar arremesso dentro da janela = cesta (2 pts). Apertar cedo = erra ao tocar; não apertar = erra no fim da janela.
  Erro: bola cai do lado do aro, jogador cai e fica atordoado `DunkStunTime` (estrelinhas, anel cinza).
- **Falta:** adversário NO AR encostando na mão/bola ou no corpo de quem está enterrando = falta: apito, pausa `FoulDelay`,
  depois **2 lances livres** (v0.7, ver abaixo).
- **Lances livres (v0.7, `Match` fase `FreeThrow`):** quem sofreu vai para a linha (`FreeThrowDistance` do aro); os outros ficam
  nos lados do garrafão (defensores mais perto da cesta); ninguém se mexe nem age. Cada lance: preparação `FreeThrowPrep`,
  depois o mesmo anel do skill check da enterrada (duração `FreeThrowSkillTime` ± `DunkFlightJitter`, janela `DunkWindow`,
  só botão de arremesso). Acertou: bola sai sozinha numa parábola calculada até o centro do aro (`FreeThrowFlightTime`) =
  `FreeThrowPoints` (1). Errou o 1º: pausa `FreeThrowGap` e 2º lance. Converteu o 2º: round reinicia com o outro time.
  Errou o 2º: a bola é lançada contra a frente do aro e a fase volta a `Playing` (rebote valendo). Se a bola do lance
  livre errado ainda entrar, vale 1 ponto (`Ball.FreeThrow`).
- **Skill check (v0.7):** janela `DunkWindow` = 0,06 s; a duração do voo varia ± `DunkFlightJitter` a cada enterrada
  (o ponteiro muda de velocidade, não dá para decorar). **Só o botão de arremesso** (Espaço não vale desde a v0.7).
- **Enterrada sem corrida (v0.7):** `Player.CanDunk()` = com a bola, no chão, a até `DunkRange` do aro, movimento (ou, parado,
  a frente do jogador) dentro de `DunkMaxAngleDeg` da cesta. Quando é verdade, a HUD mostra **duas setinhas verdes** acima da mira.
- **Drible (v0.7):** velocidade = `WalkSpeed × DribbleSpeedMult` (0,7), sem correr e sem o multiplicador de quem tem a bola.
- **Bots (Etapa 4, `BotBrain`):** com a bola: decide a cada ~0,25 s — drible especial sob pressão, infiltração para enterrar
  (caminho livre), passe carregado para o aliado mais livre/mais perto da cesta (alto se o corredor estiver fechado),
  arremesso livre dentro de `BotShootRange` (marcado só com pequena chance; impaciente após `BotMaxHoldTime`).
  Erros de mira/distância/skill check sorteados (triangulares) e multiplicados pela dificuldade (Fácil ×1,8, Difícil ×0,55).
  Sem a bola no ataque (v0.7): escolhe um espaço livre em volta da cesta (longe de defensores/aliados/da bola), troca de
  lugar a cada ~`BotRoamInterval`, às vezes corta para a cesta e, parado, faz passos laterais. Defesa: marcação individual
  gulosa (quem tem a bola primeiro), entre o atacante e a cesta; sem marcação = zona entre a bola e a cesta.
  **Bloqueio (v0.7):** contra arremesso, o bot lê o plano do arremesso (`Player.GetShotPlan`), se coloca ~1 m à frente de quem
  arremessa NA trajetória (erro `BotBlockPosError`) e, quando a bola sai, pula após `BotBlockReaction` — só se a simulação do
  salto (com o embalo herdado, mesma regra do `CheckBlocks`) alcança a bola; escolhe pular parado ou na direção da bola.
  Contra enterrada: pula na direção de quem voa (gera falta). Teste: `--smoke-block`.
  Lances livres: mesmo erro do skill check da enterrada. Bola solta/no ar: o mais perto de cada time vai no rebote.
- **Bonecos de treino (v0.7):** sem a bola e sem marcar, passeiam devagar perto da posição inicial (`DummyWanderRadius`),
  com pausas; o adversário volta para a posição quando o ataque se aproxima da cesta dele.
  Os aliados bots passam para você também. Menu: "Outros jogadores" = Bots ou Bonecos de treino; dificuldade.
- **Indicativos sem texto (`AimIndicator`):** passe carregado: seta reta amarela (normal) / arco rosa (alto) abaixo da mira +
  anel até o passe automático; arremesso: anel laranja até o automático; anéis curtos que esvaziam: drible (laranja),
  roubo errado (vermelho), atordoado/pousando (cinza).
- **Cursor:** o do Windows é sempre transparente; o visível é desenhado pelo jogo (`SoftCursor`). Modo do mouse só em
  `GameRoot.UpdateMouse()`; ao voltar do foco reaplica o modo.
- **Drible especial:** botão de drible com a bola (cargas; recarga contínua). A bola troca de mão; imune a roubo; mais lento;
  sem outras ações.
- **Por tempo empatado:** prorrogação "próxima cesta vence".
- Cesta 0 fica em −X, cesta 1 em +X. Time 0 (AZUL) ataca a cesta 1; time 1 (LARANJA) ataca a cesta 0.
