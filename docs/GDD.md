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

## Revisões do usuário (v0.9) — prevalecem sobre as anteriores
- **Início de round:** estamina e todos os cooldowns (drible, roubo, atordoado, trava do pouso) resetam.
- **Skill check (enterrada e lance livre):** apertou na hora errada → o anel some e o jogador erra NA HORA (não espera o ponteiro).
- **Lance livre errado:** o jogador ainda arremessa, mas erra garantido. Errou o 1º → o 2º começa quando a bola toca o chão.
- **Drible útil contra bots:** bots caem na finta e dão o bote durante o drible (e erram, ficando lentos).
- **Ninguém fica em cima de outro jogador.**
- Investigar o crash (0xc000001d no motor): guardas contra NaN e logs preservados.

## Revisões do usuário (v0.10) — prevalecem sobre as anteriores
- **Arremesso no pulo:** carregando o arremesso dá para pular, em qualquer direção (a do movimento). No ar, soltar o botão arremessa dali; se tocar o chão ainda carregando, arremessa automaticamente.
- **Pulo mais rápido:** carga do salto 0,18 s (antes 0,3); um toque pula ao soltar e, segurando, pula sozinho quando a carga enche (não fica mais agachado esperando). A trava ao pousar não mudou.
- Rodapé de controles menor.

## Revisões do usuário (v0.11) — prevalecem sobre as anteriores
- **Enterrada também no X do controle** (o botão de roubo, que com a bola não fazia nada). No teclado continua só o Espaço (lá o E é drible e roubo).
- **Pulo só sai ao soltar o botão** (segurando, fica carregado até soltar — desfeito o pulo automático da v0.10). Carga continua 0,18 s.
- Etapa 7 (arte) começa: guia em `docs/ARTE.md`.


## Revisões do usuário (v0.13) — prevalecem sobre as anteriores
- Os valores que o usuário ajustou no artifact (`Build/tuning.json`) viraram os padrões do `Tuning.cs` (29 valores, entre eles aro a 4,3 m e tabela 3,3 × 1,85).
  **Sempre que o usuário pedir para "aplicar o artifact", aplicar no código E publicar a release** (os amigos só apertam Atualizar).
- Passe rápido: a checagem de corredor livre (`Passing.LaneClear`) agora simula igual à recepção real: o recebedor anda até o ponto
  previsto, aliados no caminho não bloqueiam, adversário só bloqueia se estiver a menos de `PassLaneRadius` da bola E mais perto dela
  que o recebedor. Aperto do passe guardado por `PassBufferTime` (sai assim que der: pegando a bola, pousando...).

## Revisões do usuário (v0.21) — prevalecem sobre as anteriores
- 1º personagem: **o Rei** (concept `art_source/concepts/rei.png`). Corpo, cabeça, coroa e mãos são objetos separados; **as mãos não têm
  braços nem osso** — flutuam e são movidas por código (vão até a bola e até o aro).
- **Controle: drible e roubo no mesmo botão (X)**, como no teclado (E). A enterrada fica só no pulo (o X com a bola é drible).
- **Roda de expressões no B (controle) / Q (teclado)**: segura, aponta com o analógico esquerdo ou o mouse e solta; a expressão fica
  5 s (`EmoteDuration`) ou até outra. 6 expressões: Sério, Focado, Sorriso frio, Bravo (as 4 da concept), Feliz e Surpreso.
- Enterrada com impacto: clarão, ondas, faíscas, som grave, aro e câmera tremendo; o jogador **fica pendurado no aro** `DunkHangTime`
  (0,35 s) e depois cai.

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

## Revisões do usuário (v0.27) — prevalecem sobre as anteriores

- Menus mais bonitos (tema novo, fundo animado). Linha da trajetória mais bonita, mas simples.
- "Informações" sai das Configurações e vira botão no menu principal e no de pausa.
- Bots/bonecos se chamam pelo personagem; repetidos no mesmo time ganham número a partir do 2 ("Bolt", "Bolt 2"). Humanos online usam o nick da Steam.
- Presença Implacável vale para enterradas: sem skill check e erro garantido.
- Duelo: raio dos olhos mais fino e sutil, sem a linha central tremendo; enterrada sem as mãos atravessarem o aro; dois bots duelando = no máximo 5 s de troca de skill checks.
- Modo de teste (offline e online): partida infinita e aba de Trapaças (trocar personagem, recarregar habilidades/ult, adicionar bots e trocar o personagem deles, remover, sem recarga, estamina infinita, pegar a bola, zerar placar).
- "Não me Decepciona": trevo só para o Luck e aliados; o outro time só sabe na cesta, quando todos recebem um aviso claro de que os pontos foram para o outro time. O menu de escolha abre um painel com o cursor e escolhe clicando.
- Som novo do Impulso Descuidado. Drible Relâmpago é um impulso reto (atravessa o adversário).
- Arranha-Céu pega tudo que um pulo normal pegaria e mais.
- Swap: troca ao apertar (não segura mais); o alvo fica com o anel do chão bem mais claro enquanto você é o Bolt e o Swap está pronto.
- Ícone do Duelo: duas espadas se cruzando com um raio.
- Enterradas carregam mais a ultimate (`UltBonusDunk`) que outras cestas (`UltBonusScore`).

## Revisões do usuário (v0.26) — personagens com atributos, habilidades e ultimates

**Pedido do usuário (resumo fiel):** antes da partida cada jogador escolhe um personagem. Há um modo **sem habilidades**
(ainda escolhe personagem, mas nenhuma habilidade nem ultimate funciona). Cada personagem tem atributos em **estrelas
(1 a 5, mostradas na seleção)**, 3 habilidades e uma ultimate. Botões: teclado **1, 2, 3** e **4** (ultimate); controle
**← ↑ →** e **↓** (ultimate). Barra da ultimate enche bem devagar com o tempo; **toco, passe, roubo e principalmente cesta**
dão bônus na hora. Algumas ultimates são **cutscenes** (às vezes interativas) mostradas para todos, que **não contam no
relógio nem na carga de outras ultimates**; os jogadores são posicionados onde convém para a cena sem mudar a posição real;
faltando jogadores, entram **bonecos com visual de personagem aleatório** só na cutscene. Inspiração: animações e vibe das
cutscenes do Basketball Zero (Roblox), não os gráficos. **Recargas não voltam no começo de um round.** Os controles **não
ficam mais na tela**: vão para Configurações → **Informações**, junto com a descrição completa das habilidades do personagem
atual; no rodapé ficam só os ícones das habilidades com o botão, pequenos, e uma **rodela que enche** durante a recarga.
Artifact: **uma aba por personagem** — estrelas no topo, depois valores das habilidades, por último avançados.

**Atributos (estrela → valor):** cada atributo vale `1 + (estrelas − 2,5) × passo%` (Tuning "Estrelas (valores)"). O jogo
antigo fica entre 2 e 3 estrelas. Passos padrão: Velocidade 7%, Agilidade 5,5% (um pouco menos), Estamina 9% (um pouco
mais), Precisão 8%, Salto 6%. Valores em que menos é melhor são divididos.
- Velocidade: velocidade andando e correndo.
- Agilidade: velocidade do bote, penalidade do roubo errado, velocidade driblando; cargas de drible 1★ = −1, 2–3★ = 0, 4–5★ = +1.
- Estamina: estamina máxima e recarga.
- Precisão: alcance do arremesso, alcance do passe rápido, velocidade do passe, altura do passe alto.
- Salto: impulso do salto, travamento ao pousar, tempo de carga, distância para enterrar.

**King** (era o Rei) — Vel 3, Agi 3, Est 2, Prec 5, Salto 2.
1. Olho do Rei: 7 s ativo; o próximo passe/arremesso (inclusive passe rápido) gasta o efeito e tem 2× o alcance; bola com
   rastro vermelho sutil. Recarga 15 s. *Implementação:* no arremesso a mira normal não muda — subir a câmera ALÉM da
   inclinação do alcance máximo leva até o alcance dobrado (`ShotMath.PitchDistance(extendMax)`); passe carregado sai √2 mais rápido.
2. Presença Implacável: 5 s; adversário a até 7 m que **soltar** um arremesso erra com certeza (trajetória desviada para o
   aro + a bola não pode virar cesta até tocar o chão). Partículas vermelhas no King; borda vermelha na tela de quem foi afetado. Recarga 25 s.
3. Drible Perfeito: só com a bola; drible na hora, +0,5 s, não gasta carga, aura vermelha na bola; quem tentar roubar
   (bote ou impulso do Luck) cai atordoado 4 s. Recarga 15 s.
- Ult "Sua Derrota é Absoluta" (só com a bola): cutscene — drible no meio da quadra (skill check; errou = adversário rouba,
  rodada reinicia com a bola dele) → corre para a linha de 3 → outro adversário pula para bloquear → o King dá a volta e
  arremessa pelo lado da cesta (skill check mais difícil). Acertou = 7 pontos; errou = bola bate no aro e sai, rodada
  reinicia com a bola do adversário.

**Luck** (era a Ametista) — Vel 4, Agi 2, Est 5, Prec 2, Salto 2.
1. Arremesso Arriscado: com a bola e no lado adversário; arremessa na hora direto na cesta; pode ser bloqueado; 20% de errar. Recarga 15 s.
2. Não me Decepciona: menu para escolher um adversário; se ele fizer cesta em 20 s, os pontos vão para o time do Luck. Recarga 30 s.
3. Impulso Descuidado: 75% de impulso para onde está olhando (sem a bola, passar por quem tem a bola rouba); senão tropeça,
   atordoado 2 s. Recarga 7 s. *(O pedido dizia "Se der os 20%"; foi usado 25%, o complemento de 75% — ajustável no Tuning.)*
- Ult "Sorte Infinita": 10 s de modo apostador: +15% em todos os valores de estrela, cestas dele +1 ponto, partículas verdes.
  Apertando de novo durante o modo = aposta: 1ª vez 50% de ganhar (dobra o bônus dos atributos, +3 s, +1 ponto por cesta,
  efeito especial) ou perder (modo acaba, atordoado 4 s); cada aposta seguinte +15% de chance de perder (máx. 95%).
  *Implementação:* o bônus dobra a cada vitória até o teto `LuckModeBonusMax` (+100%) para não virar absurdo.

**Bolt** (era o Raio) — Vel 5, Agi 4, Est 4, Prec 1, Salto 3.
1. Drible Relâmpago: com a bola, olhando para um adversário a até 5 m; impulso de raio que passa pelo alvo e sai 3 m na
   frente; alvo atordoado 2 s. Recarga 15 s.
2. Swap: troca de lugar com o jogador (aliado ou adversário) mais perto de onde olha; segurar destaca quem vai trocar e a
   troca acontece ao soltar; partículas de raio nos dois. Recarga 20 s.
3. Arranha-Céu: sem a bola; pulo reto altíssimo com trovão; área de bloqueio bem maior; quem estava a até 3 m fica atordoado
   2 s (*implementação: só adversários*). Recarga 25 s.
- Ult "Duelo" (com a bola): escolhe um adversário; cutscene com os dois se encarando (raio saindo dos olhos); skill checks
  em sequência para os dois, cada vez mais rápidos. Bolt erra primeiro (ou os dois erram juntos) = o adversário rouba e o
  jogo volta com a bola na mão dele, onde ele estava. Adversário erra = Bolt passa como trovão e faz cesta de 7 pontos.

**Decisões de implementação (v0.26):**
- Modo sem habilidades: opção "Habilidades" na Nova partida e no lobby (host). Os atributos de estrela continuam valendo.
- Seleção: "Escolher personagem e jogar" na Nova partida; "Escolher personagem" no lobby (vai para o host, `Msg.SetCharacter`).
  Bots: personagem alternado pela vaga e usam as habilidades (chance por dificuldade).
- Cutscene: fase `MatchPhase.Cutscene`; ninguém é simulado; bonecos visuais em `Cutscene.cs`; skill checks em `SkillCheckState`
  (online: mesmo esquema da enterrada — SkillT do cliente + atraso medido). Título da ultimate aparece na entrada da cena
  (faz parte da cutscene; se não quiser, é só pedir para tirar).
- Ult: barra em `PlayerAbilities.Ult` (0–1), passiva `UltChargeTime` (240 s), bônus `UltBonus*`; não enche durante a própria ult ativa.
- Testes antigos jogam com atributos neutros e sem habilidades (resultado igual ao de antes); `--smoke-abilities` testa tudo novo.

## Iron (v0.37) — especificação do usuário
Atributos: Velocidade 1, Agilidade 2, Estamina 4, Precisão 3, Salto 5.
1. **Muralha de Aço** — muralha prateada meio transparente, bem alta (barra qualquer arremesso normal), exatamente em cima da linha de 3 completa do time do Iron. Jogadores atravessam (mesmo com a bola); a bola bate como numa parede (arremessos e passes). O adversário pode entrar com a bola e arremessar de dentro. Dura 5 s, surge na hora. Recarga 40 s.
2. **Pancada** — só com um adversário a 3 m ou menos. Pancada na cabeça do adversário mais perto de onde o Iron olha (dentro do alcance): atordoado 3 s. Não vale em quem está com a bola; o Iron pode usar estando com ela. Recarga 30 s.
3. **Investida** — só sem a bola. Começa não tão rápida e acelera; segue para a frente sozinha, a direção muda um pouco andando para os lados. Todos no caminho (até aliados) são jogados para o lado e atordoados 3 s; quem tinha a bola a deixa cair. Acaba ao fim de 3 s ou ao bater numa parede; nos dois casos o Iron fica imobilizado 2 s — no fim do tempo ele fica **cansado** (não atordoado, animação diferente). Recarga 40 s.
4. **Ultimate: Indestrutível** — 20 s imune a todas as habilidades dos adversários, mais rápido (velocidade de 3 estrelas) e estamina infinita. Driblando, quem tentar roubar fica atordoado 1 s a mais. Bloqueando um arremesso no pulo, o Iron pega a bola no ar (e fica imune a roubo até estar no chão e poder driblar).

Implementação (Claude): muralha = `CourtGeo.Wall[cesta]` + `ThreeDist` (mesma regra da linha de 3 do `IsThree`: arco na frente do aro, retas |dz| = raio até o fim da quadra), colisão em `BallPhys.Step` só para a bola, altura `IronWallHeight` 14 m; visual `SteelWall`. "Parede" da Investida = colisão com algo que não é jogador ou o limite da quadra. "Atordoado 1 s a mais" no roubo = `Stun` de `IronUltStealStun` + a penalidade normal do roubo errado aumentada desse tempo. Cena da ult (pedido v0.36): bigorna cai na cabeça e quica, ele nem pisca e solta o grito de guerra.

## Revisões do usuário (v0.38) — prevalecem sobre as anteriores
- Iron, Investida: impulso inicial (começa um pouco mais rápida que a velocidade base); a direção é controlada pela câmera (dá para controlar mais); os botões de movimento não fazem nada.
- Iron, Indestrutível: +10 s (30 s); bloquear enterradas com ela ativa não causa falta (implementação: a enterrada é bloqueada e o Iron fica com a bola); diminui 15% as recargas das habilidades enquanto ativa (implementação: recargas começadas durante a ult são 15% mais curtas); não há mais tempo extra de atordoamento no roubo durante o drible.
- Luck, Não me Decepciona: se o alvo não fizer cesta nos 5 s, a próxima cesta dele vale o dobro (para o time dele).
