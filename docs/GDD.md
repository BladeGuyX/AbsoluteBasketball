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

## Decisões de implementação (Claude)
- **Rede:** host autoritativo. Toda a simulação (jogadores, bola, regras) roda no host a partir de `PlayerInput`
  (humano local, bot ou cliente remoto). Offline = host sem clientes. Plano de rede na Etapa 5.
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
- **Drible especial:** botão de drible com a bola (cargas; recarga contínua). A bola troca de mão; imune a roubo; mais lento;
  sem outras ações.
- **Por tempo empatado:** prorrogação "próxima cesta vence".
- Cesta 0 fica em −X, cesta 1 em +X. Time 0 (AZUL) ataca a cesta 1; time 1 (LARANJA) ataca a cesta 0.
