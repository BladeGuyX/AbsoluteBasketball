# Roteiros de teste (manual)

Rodar sempre pelo `Build\AbsoluteBasketball.exe`. Os mais recentes ficam no topo.

---
## v0.18 — Quadra nova (cena Court, linhas por shader, piso de madeira)

**Atualizar:** menu principal → "Atualizar para v0.18".

1. Comece uma partida (qualquer tamanho): o piso é de **tábuas de madeira** (clarinhas e algumas mais escuras), com uma **faixa azul-marinho** em volta das linhas até a mureta, e uma borda chanfrada escura descendo até a areia.
2. Linhas brancas: laterais, fundo, meio, círculo central, **garrafão retangular**, **semicírculo do lance livre** (metade de fora contínua, metade de dentro tracejada) e **linha de 3** (arco na frente, retas nos cantos).
3. No círculo central há um **anel azul com uma estrela**.
4. Troque entre quadra pequena, média e grande: as linhas laterais/de fundo e o piso acompanham o tamanho, mas **garrafão, círculos e o desenho do logo têm sempre o mesmo tamanho** (ficam ancorados no fundo e no centro). A madeira **não estica**: as tábuas têm o mesmo tamanho em todas as quadras.
5. Ande perto das linhas e longe delas: as linhas ficam nítidas, sem serrilhado forte nem "piscar" à distância.
6. Arremesse com os pés **logo atrás da linha de 3** (no arco e no canto): vale 3. Logo à frente: vale 2. A linha desenhada é exatamente a da regra.
7. Lance livre: o arremessador fica em cima da linha de lance livre do garrafão.
8. As cestas ficam um pouco mais perto da linha de fundo (1,575 m, medida FIBA; antes 1,8 m). No artifact, seção Quadra: **"Distância do aro à linha de fundo"** agora tem padrão 1,575.
9. As paredes invisíveis continuam na mureta branca, e a bola continua batendo no aro/tabela/poste normalmente.
10. Online: o amigo vê a mesma quadra.

---
## v0.17 — Cesta nova (modelo do Blender), rede que balança e aro que treme

**Atualizar:** menu principal → "Atualizar para v0.17".

1. Comece uma partida: as duas cestas são o modelo novo. A cesta que o AZUL defende tem poste/base/moldura azul-marinho e vidro azul-claro. A que o LARANJA defende tem tudo em vermelho/laranja e vidro pêssego.
2. Olhe a cesta de perto: aro vermelho com ganchinhos, rede branca em losango, tabela com moldura escura e acolchoado embaixo, quadrado branco, braço com diagonal, base acolchoada com faixa clara, estrela e rodinhas.
3. Arremesse e acerte (de preferência sem tocar no aro): a rede estica para baixo, abre e balança, e o aro dá uma tremidinha.
4. Arremesse e bata no aro: o aro treme (mais forte quanto mais forte a batida) e a rede balança um pouco para o lado.
5. Faça uma enterrada certa: o aro entorta para baixo, volta tremendo e a rede balança forte.
6. Enterrada errada: só a tremidinha de batida no aro, sem o tranco forte.
7. A física não mudou: a bola bate no aro, na tabela e no poste nos mesmos lugares de antes (o modelo segue as medidas do tuning).
8. No artifact, seção Quadra: **"Balanço da rede"** (padrão 1), **"Tremor do aro na enterrada"** (padrão 9 graus) e **"Tremor do aro na batida"** (padrão 1). 0 desliga. Aperte F5 e comece outra partida.
9. Mude "Raio do aro", "Altura do aro" ou o tamanho da tabela no tuning: o modelo acompanha (aro, rede, tabela, quadrado e braço se ajustam).
10. Online: o amigo também vê a rede balançar e o aro tremer, inclusive na enterrada.

---
## v0.16 — Bola na mão não gira

**Atualizar:** menu principal → "Atualizar para v0.16".

1. Parado, andando, correndo, driblando, virando no lugar ou trocando de mão: a bola na mão **não gira**, só sobe e desce.
2. Arremesse: a bola gira no voo.
3. Faça um passe: a bola gira no voo.
4. Deixe a bola solta rolar ou quicar no chão: ela gira conforme anda.
5. Ao pegar a bola, ela para de girar na hora, sem dar um tranco.
6. No artifact, "Giro da bola na mão" agora tem padrão 0. Se quiser um pouco de giro, suba o valor e aperte F5.

---
## v0.15 — Giro da bola no quique mais natural

**Atualizar:** menu principal → "Atualizar para v0.15".

1. Parado quicando a bola: ela quase não gira, só acompanha a mão (antes girava muito rápido).
2. Andando e correndo com a bola: ela gira devagar no sentido do movimento, sem "rodopiar".
3. Gire a câmera ou o boneco no lugar com a bola: ela não dá voltas rápidas.
4. Arremesse, passe e deixe a bola rolar solta no chão: aí ela continua girando normal (como rolando).
5. No artifact, seção Bola, ajuste **"Giro da bola na mão"** (padrão 0,15). 0 = não gira; 1 = como era antes. Aperte F5 no jogo para aplicar.
6. Online, o amigo também vê a bola girando devagar na mão.

---
## v0.14 — Bola nova (primeiro modelo do Blender)

**Atualizar:** menu principal → "Atualizar para v0.14" (ou baixe o .zip da release).

1. Comece uma partida: a bola tem o modelo novo, laranja (#E8742A) com as costuras escuras da concept. Vista de frente, mostra uma cruz e duas costuras curvas nas laterais, em ") | (".
2. A bola continua do mesmo tamanho e com contorno preto (visual toon), igual aos bonecos.
3. Role a bola no chão e arremesse: ela gira ao rolar e no voo. As costuras giram junto, sem "escorregar" pela superfície.
4. Olhe a bola de perto, em vários ângulos: as costuras não piscam e não somem, e não aparece fresta entre a costura e a bola.
5. Na sombra e no sol, as cores continuam legíveis (laranja e preto, sem ficar marrom demais).
6. Jogue online (host e amigo): os dois veem a bola nova.

---
## v0.13 — Seus valores do artifact viram padrão + passe rápido consertado

**Atualizar:** menu principal → "Atualizar para v0.13" (ou baixe o .zip da release).

**Valores do artifact**
1. Depois de atualizar, os valores que você tinha no `tuning.json` agora são os padrões do jogo (aro a 4,3 m, tabela menor, pulo e drible ajustados etc.). Os seus amigos recebem os mesmos valores.
2. Abra o artifact de tuning: os padrões dele batem com o que você salvou.

**Passe rápido**
3. Toque o passe com um aliado **andando/correndo** perto de você: a bola sai sempre e chega nele (antes falhava com o aliado em movimento).
4. Aperte o passe **um instante antes** de a bola chegar na sua mão (ou ainda no ar, logo antes de pousar): o passe sai assim que der, sem precisar apertar de novo. No artifact: "Memória do aperto do passe" (0,25 s).
5. Com um adversário bem no caminho, entre você e o aliado, o passe rápido continua não saindo (regra do corredor livre). Com o adversário ao lado do aliado, mas mais longe da bola que ele, agora sai.
6. Os bots continuam passando normalmente (eles usam a mesma checagem).

---
## v0.12 — Publicado no GitHub + botão Atualizar

**Primeira instalação (você e seus amigos)**
1. Baixe o `AbsoluteBasketball-windows.zip` em https://github.com/BladeGuyX/AbsoluteBasketball/releases/latest, extraia numa pasta (fora de "Arquivos de Programas") e abra o `AbsoluteBasketball.exe`.
2. No menu principal aparece o botão **"Atualizado (v0.12)"** (depois de "Procurando atualização..."). No canto: "v0.12 · Etapa 7".

**Quando sair uma versão nova**
3. Abra o jogo: o botão fica verde, **"Atualizar para vX"**. Clique: mostra "Baixando... %", o jogo fecha, abre uma janelinha azul "Atualizando...", e o jogo reabre já na versão nova.
4. Se der erro: o botão mostra o motivo; o log fica em `%TEMP%\AbsoluteBasketball-update.log`. Dá sempre para baixar o .zip à mão no link acima.
5. Seus valores de `tuning.json` ficam guardados em `tuning.old.json` (a versão nova começa com os valores padrão dela).

---
## v0.11 — Enterrada no X, pulo só ao soltar (início da Etapa 7)

1. Com controle: corra para a cesta com a bola até aparecerem as setinhas verdes e aperte **X** — sai a enterrada (o skill check continua no RT). O A também enterra.
2. No teclado: o E com a bola continua sendo drible (não enterra); a enterrada é o Espaço.
3. No rodapé, com controle, o X aparece como "roubo / enterrada".
4. Segure o pulo: o jogador fica agachado carregando até você soltar (não pula mais sozinho). Toque rápido: pula ao soltar.
5. O resto do jogo não mudou (a Etapa 7 até aqui é só preparação: scripts do Blender, esqueleto base e guia `docs/ARTE.md`).

---
## v0.10 — Arremesso no pulo, pulo mais rápido, rodapé menor, controle

**Antes:** apague o `Build\tuning.json` antigo (o tempo de carga do salto mudou).

**Arremesso no pulo**
1. Com a bola, segure o arremesso e aperte o pulo andando para qualquer lado: o jogador pula naquela direção, ainda mirando pela câmera.
2. Solte o arremesso no alto do pulo: a bola sai do ar (a prévia acompanha a altura).
3. Segure o arremesso o pulo inteiro: ao tocar o chão ele arremessa sozinho.
4. Sem estar carregando, o pulo com a bola continua sendo só a enterrada (perto da cesta, com as setinhas verdes).

**Pulo**
5. Toque rápido no pulo (sem bola): pula na hora em que você solta. Segurando: pula sozinho em ~0,18 s, sem ficar agachado esperando.
6. A trava ao pousar continua igual. No artifact: "Tempo de carga do salto".

**Rodapé**
7. As teclinhas do canto inferior esquerdo estão bem menores.

**Controle (Xbox por Bluetooth)**
8. Configurações → Controles, embaixo de "Corrida: alternar": aparece "Controles detectados: …" e se algum botão já chegou ao jogo.
9. Se aparecer o controle mas "nenhum botão chegou", é o Steam Input do Spacewar (AppID 480) capturando o controle. Desative: Steam → Biblioteca → Spacewar → botão direito → Propriedades → Controle → "Desativar Steam Input" (ou, com o jogo aberto, Shift+Tab → Configurações do controle). Feche e abra o jogo.
10. Teste rápido para confirmar: feche a Steam toda e abra o jogo — o controle deve funcionar offline.

---
## v0.9 (Etapa 6) — Ícones de botões, vibração, câmera

**Ícones**
1. Jogue com teclado/mouse: no rodapé aparecem teclinhas (Esq., Dir., R, Espaço, E, Shift, Esc) com o que cada uma faz.
2. Pegue o controle e mexa qualquer coisa: as teclinhas viram os botões do controle na hora (RT, RB, Y, A, X roubo, B drible, LT, Menu — A/B/X/Y coloridos). Volte ao mouse: voltam as teclas.
3. Remapeie um botão em Configurações → Controles: a dica do rodapé mostra o novo botão.

**Vibração (com controle)**
4. Pegar a bola, pousar e começar o drible: pulso leve. Seu time pontuar / acertar lance livre: médio. Roubarem de você, acertar enterrada, ficar atordoado: forte.
5. Configurações → Controles: "Vibração do controle" desliga tudo; "Força da vibração" ajusta. Usando teclado/mouse não vibra.
6. Funciona também como cliente online.

**Câmera**
7. Configurações → Controles: "Inverter eixo X", "Campo de visão" (−15° a +20°) e "Distância da câmera" mudam na hora.
8. Com o analógico direito, movimentos pequenos ficam mais precisos (curva); o giro máximo é o mesmo.
9. Carregando arremesso ou passe, a mira fica mais lenta/precisa (mouse e analógico).
10. No artifact: "Curva do analógico", "Mira carregando o arremesso" e a seção "Vibração (controle)".

---
## v0.9 — Correções (round, skill check, lance livre, drible, em cima de jogador, crash)

**Antes:** apague o `Build\tuning.json` antigo.

1. **Round:** gaste a estamina e as 2 cargas de drible, faça um roubo errado; quando a cesta sair e o round recomeçar, estamina cheia, 2 cargas e sem lentidão.
2. **Skill check da enterrada:** aperte bem cedo: o anel some **na hora**, a bola escapa da mão e você cai atordoado. Acertando: zona verde e cesta, como antes.
3. **Lance livre:** aperte cedo no 1º: o anel some, o jogador arremessa e a bola **bate no aro e erra**; quando ela toca o chão, o 2º lance começa. Erre o 2º também: a bola bate no aro e o jogo continua.
4. **Drible:** com um bot (Normal) colado em você, use o drible (E): na maioria das vezes ele dá o bote, erra e fica lento — passe por ele. No Difícil ele cai menos; no Fácil, mais. (Ajuste: "Chance de cair no drible" no artifact.)
5. **Em cima de jogador:** pule sobre um jogador parado: você escorrega para o lado e cai no chão, nunca fica em cima.
6. **Online:** o cliente que erra o skill check vê o anel sumir na hora também.
7. **Crash:** se o jogo fechar sozinho de novo, me mande o arquivo mais novo de `%APPDATA%\Godot\app_userdata\Absolute Basketball\logs\` (agora ficam até 40) e o horário aproximado.

---
## v0.8.2 — Skill check do cliente compensando o atraso da rede

**Os dois PCs precisam desta build nova** (versões diferentes não conectam).
1. Pela Steam, comecem uma partida. O amigo (cliente) enterra: acertando o ponteiro na zona branca **na tela dele**, a enterrada entra.
2. Faça o amigo sofrer uma falta (você pula na frente da enterrada dele): ele cobra os lances livres e consegue acertar normalmente.
3. Apertar cedo/tarde na tela dele continua errando. Para você (host) nada mudou.
4. Se o amigo errar sem apertar, o jogador dele fica uma fração de segundo a mais no aro antes de cair (é o host esperando o aperto chegar pela rede) — normal.

---
## v0.8.1 — Correção: pela Steam o amigo virava bot

**Os dois PCs precisam desta build nova** (versões diferentes não conectam mais — aparece aviso).
1. Pela Steam, crie o lobby e o amigo entra. Comecem a partida.
2. O amigo deve ver e controlar **o próprio jogador** (câmera atrás dele), e não assistir o host.
3. No host, o jogador do amigo deve se mexer conforme ele joga (não como bot).

---
## v0.8 — Etapa 5: multiplayer online (primeira versão)

**Antes:** a pasta `Build` agora tem `steam_api64.dll` e `steam_appid.txt` (480) ao lado do .exe — não apague. Para a Steam, deixe a Steam aberta.

**Abertura do jogo**
1. Com a Steam aberta, abra o `AbsoluteBasketball.exe`: a janela pode piscar uma vez (o jogo se reabre para o overlay da Steam entrar). Na Steam você aparece "jogando Spacewar" (é o app de teste).
2. Shift+Tab deve abrir o overlay da Steam (se não abrir, o convite pela lista do jogo continua funcionando).

**Teste sozinho, duas janelas no mesmo PC (IP direto)**
3. Abra o jogo **duas vezes**. Na janela 1: Online → "Hospedar por IP". Aparece o lobby com você no AZUL e vagas de bot.
4. Na janela 2: Online → troque o nome → "Entrar por IP" (127.0.0.1). Ela entra no lobby (aparece nas duas janelas, no time com menos gente).
5. Troque de time nas duas janelas ("Entrar neste time"); mude as configurações no host (quadra, pontos, jogadores por time): o cliente vê mudar e não consegue mexer.
6. Host → "Começar partida": as duas janelas entram na partida com contagem; vagas vazias são bots.
7. Jogue um pouco em cada janela: mover, arremessar, passar, pular, roubar, driblar, enterrar. Tudo que um faz aparece no outro; placar, sons e mensagens iguais.
8. Faça uma falta enterrando por cima de alguém: os lances livres aparecem nas duas janelas (o skill check só para quem cobra).
9. Esc no cliente: abre o menu mas **o jogo não pausa**. "Sair da partida online" no cliente: no host aparece que um bot assumiu.
10. Fim de jogo: no host "Jogar de novo" (recomeça para os dois) ou "Voltar ao lobby" (os dois voltam ao lobby). No cliente aparece "Aguardando o host...".
11. Feche a janela do host no meio da partida: o cliente volta ao menu com o aviso de que a conexão caiu.

**Com um amigo pela Steam (dois PCs, duas contas)**
12. Os dois com a Steam aberta e o jogo aberto. Você: Online → "Criar lobby (só amigos)". No lobby aparece o **código** e, à direita, a lista de amigos online com "Convidar".
13. Convide o amigo (pela lista ou "Abrir convite da Steam"). Ele aceita pelo chat da Steam **com o jogo já aberto** e entra no lobby. Se o convite não chegar: ele digita o código em Online → "Entrar com código".
14. Comecem a partida e joguem uns minutos. Me diga o ping que aparece no lobby do cliente e se sentiu atraso no próprio boneco (ainda não tem predição — o boneco do cliente responde com o atraso da rede).
15. "Criar lobby (público)" e, no outro PC, "Procurar lobbies públicos": o lobby aparece na lista.

---
## v0.7 — Lances livres, enterrada sem corrida, bloqueio dos bots

**Antes:** apague o `Build\tuning.json` antigo (vários valores padrão mudaram).

**Enterrada**
1. Com a bola, **andando** (sem Shift) em direção à cesta adversária, a até ~7 m: aparecem **duas setinhas verdes** piscando acima da mira. Longe, de costas ou de lado para a cesta, elas somem.
2. Com as setinhas, aperte Espaço: enterra (não precisa correr). Parado e virado para a cesta também vale.
3. No skill check, aperte **Espaço** na hora certa: **não vale** (erra e fica atordoado). Só o botão esquerdo vale.
4. A zona branca ficou um pouquinho maior (0,06 s).

**Ritmo**
5. Segure o arremesso: sai sozinho em **3 s**. Segure o passe: sai sozinho em **3 s**.
6. Com a bola você fica um pouco mais lento que antes. Driblando (E), a velocidade é 70% de andar (e não dá para correr driblando).

**Falta → lances livres** (enterre por cima de um adversário que pula)
7. Apito, pausa curta, e você vai para a **linha de lance livre**; todos os outros ficam parados nos lados do garrafão.
8. Aparece o **anel do skill check** (depois de ~1 s). Aperte o botão esquerdo quando o ponteiro chegar na zona branca: a bola sai sozinha e **entra** (+1).
9. Erre o 1º de propósito (cedo ou sem apertar): zona vermelha e vai **direto para o 2º**.
10. Erre o 2º: a bola bate no aro e **todos voltam a se mexer** (rebote valendo).
11. Acertando o 2º: o round recomeça com o outro time.
12. Quando um bot sofre falta, ele cobra (às vezes erra).

**Bots**
13. Arremesse com um bot perto de você: ele se coloca **na frente da bola** e pula quando você solta — e quando pula, normalmente bloqueia. Sem bot perto (ou longe da trajetória), ele não pula à toa.
14. Seus aliados bots sem a bola trocam de lugar procurando espaço, às vezes cortam para a cesta e ficam se mexendo em vez de parados. Defensores sem marcar ficam em zona acompanhando a bola.
15. Com bonecos de treino: os bonecos passeiam devagar perto do lugar deles; o adversário volta para a posição quando você chega perto da cesta dele.

---
## v0.6 — Enterrada revisada + Etapa 4 (bots)

**Antes:** apague o `Build\tuning.json` antigo.

**Enterrada**
1. Com a bola, corra (Shift) para a cesta adversária: dá para enterrar bem mais de longe (até ~7 m).
2. O skill check está bem mais difícil: zona branca bem fina e o ponteiro muda de velocidade a cada enterrada.
3. Acerte o skill check com **Espaço** (em vez do botão esquerdo): também vale.
4. O tapinha não existe mais (pular perto da bola no aro é só um salto normal).

**Bots (menu → Outros jogadores: Bots; escolha a dificuldade)**
5. Partida 2x2 (1 aliado, 2 adversários), Normal. Os adversários atacam: infiltram, enterram (às vezes erram o skill check e ficam atordoados), passam e arremessam (acertam mais ou menos metade).
6. Na defesa deles: um bot marca você entre você e a cesta, tenta roubar quando você não está driblando/carregando, e pula contra seus arremessos e enterradas (enterrada bloqueada = falta a seu favor).
7. Seu aliado bot se posiciona livre em volta da cesta; às vezes passa para você.
8. Bola solta: o mais perto de cada time vai buscar.
9. Compare **Fácil** (erra bastante, reage devagar) e **Difícil** (acerta mais, reage rápido).
10. Escolha "Bonecos de treino": volta o comportamento antigo (bonecos parados) para treinar.
11. Partidas maiores (ex.: quadra Grande, 4x5) devem continuar fluindo, sem bots travados num canto.

---
## v0.5 — Indicativos, prévia, cursor + Etapa 3 (enterrada e tapinha)

**Antes:** apague o `Build\tuning.json` antigo. Jogue em **tela cheia** (onde aconteciam os problemas do cursor).

**Cursor (tela cheia)**
1. No menu principal o cursor aparece normalmente. Dê alt-tab, volte e mexa o mouse: o cursor acompanha na hora.
2. Comece a partida: sem cursor. Dê alt-tab e volte (abre a pausa, com cursor). Continue: **sem cursor**. Repita 3 vezes.

**Indicativos (sem texto)**
3. Segure o passe: aparece uma **seta reta amarela** abaixo da mira e um anel enchendo (passe automático em 4 s). Aperte **R/Y**: vira um **arco rosa** (passe alto).
4. Segure o arremesso: anel laranja em volta da mira enchendo até o arremesso automático.
5. Drible (E com a bola): anel laranja curto esvaziando. Erre um roubo: anel vermelho. Ao pousar de um salto: anel cinza rápido.

**Prévia e passe**
6. Faça uns 10 arremessos de vários lugares (parado e andando): a bola deve seguir **exatamente** os pontos amarelos.
7. Passe carregado: a bola vai um pouco menos longe/forte que antes.

**Enterrada (Etapa 3)**
8. Com a bola, **corra (Shift)** direto para a cesta adversária e aperte **Espaço** a menos de ~3 m: o jogador voa até o aro e aparece o **skill check** (anel com zona branca à esquerda e ponteiro vermelho).
9. Aperte o **botão esquerdo** quando o ponteiro estiver na zona branca (= quando a bola toca a cesta): a zona fica verde e é cesta (+2).
10. Erre de propósito (aperte cedo ou não aperte): zona vermelha, a bola cai do lado do aro, o jogador cai e fica **atordoado 2 s** (estrelinhas, anel cinza, não anda nem age).
11. Andando (sem correr) ou de longe/de lado, Espaço com a bola não faz nada.

**Falta**
12. Ande devagar com a bola até perto da cesta adversária: o boneco adversário vai para a frente da cesta. Corra e enterre por cima dele: ele pula, **apito**, e o round recomeça com **você com a bola** (sem contagem).
   (Se você chegar rápido, ele não dá tempo de chegar — enterrada livre.)

**Tapinha**
13. Arremesse na cesta adversária e corra para baixo dela. Quando a bola estiver quicando no aro/bem perto dele, pule **na direção da cesta**: o jogador voa até a bola e completa a cesta sozinho (sem skill check).
14. Se um adversário pular e bloquear um tapinha, **não** é falta.

---
## v0.4 — Revisão da Etapa 2 (arremesso, passe carregado, HUD limpa, mouse, drible)

**Antes:** apague o `Build\tuning.json` antigo (o jogo recria com os valores novos).

**Arremesso**
1. Segure o arremesso sem mexer a câmera: a distância **não muda** com o tempo. Suba/desça a câmera: a trajetória amarela vai mais longe/mais perto, até o máximo logo de cara. A barra laranja mostra a "força" (= distância usada), **sem texto**.
2. Segure por 4 s: arremessa sozinho.
3. Colado na cesta adversária: **não existe mais bandeja automática** — você precisa mirar com a câmera.
4. Fique embaixo da cesta e jogue a bola para cima passando por dentro do aro (ou faça um passe carregado de baixo): ao descer de volta pelo aro **não conta ponto**.
5. Arremesse na tabela (quadrado): ajuda um pouco, mas não entra sempre.

**Passe**
6. Toque rápido no **botão direito / RB**: passa para o aliado livre mais próximo (alcance um pouco maior); a bola vai mais rápida e mais reta.
7. Segure o passe: o jogador anda **bem devagar (25%)** e não aparece marcador em ninguém. Solte: a bola é lançada **na direção da mira/câmera** (mesmo sem aliado ali). Com **R / Y** durante a carga, lança em arco alto.
8. Segure o passe por 4 s: lança sozinho na direção da câmera.
9. Tente acertar a cesta adversária com um passe carregado: é possível, mas difícil (sem ajuda).
10. (A imunidade a roubo enquanto carrega o passe só dá para sentir quando os bots roubarem, na Etapa 4; os bonecos não roubam.)

**Outros**
11. **Estamina:** a barra verde demora mais para acabar correndo e recarrega um pouco mais devagar.
12. **Drible: tecla E** (com a bola). O boneco agacha, inclina para frente e quica a bola bem mais rápido, trocando de mão. Sem a bola, **E** continua sendo roubo.
13. Segure o **salto** e gire a câmera: o boneco vira para onde a câmera olha (dá para pular de lado andando para o lado).
14. **Sem avisos no meio do jogo:** roubar, tocar, interceptar ou passar sem aliado não mostra texto. Só aparecem o "+2/+3", prorrogação e fim de partida.
15. **Mouse:** durante a partida o cursor **não aparece**. Esc → aparece no menu de pausa; Continuar → some de novo.
16. Dê **alt-tab** com o jogo no menu principal e na pausa, volte e mexa o mouse: o cursor deve acompanhar na hora (sem ficar parado).

---
## v0.3 — Ajustes da v0.2 + Etapa 2 (passe, salto, roubo, drible)

**Antes:** apague o `Build\tuning.json` antigo (o jogo recria com os valores novos).

**Ajustes de arremesso**
1. Partida com 1 boneco aliado e 1 adversário (padrão do menu). A contagem 3-2-1 aparece **só no início**; depois de cesta, o round recomeça direto.
2. Segure o arremesso com a câmera no meio da tela: a barra mostra "3.0 m (máx. ...)". **A distância não cresce sozinha** — só o máximo cresce (≈1,6 s até 14 m). Suba a câmera para usar mais distância; se pedir mais que o máximo aparece "carregue mais". Desça a câmera para encurtar.
3. Mire **quase** certo na cesta adversária: a trajetória corrige um pouquinho. Mire bem errado: nenhuma correção (sem "teletransporte").
4. Arremesse um pouco longo de propósito, acertando o **quadrado laranja da tabela**: a bola deve cair na cesta na maioria das vezes.
5. Faça uns 10 arremessos de meia distância: a maioria entra; às vezes bate no aro e sai.
6. Compare os três tamanhos de quadra (a Grande é enorme) e sinta a corrida um pouco mais rápida.

**Passe**
7. Toque rápido no **botão direito / RB**: passa para o aliado livre mais próximo. O aliado devolve depois de ~1 s.
8. Segure o passe e gire a câmera: um **marcador amarelo** aparece em cima do aliado na direção da câmera. Solte para passar.
9. Segurando o passe, aperte **R / Y**: o marcador fica **rosa** = passe alto (arco por cima). Aperte de novo para voltar ao normal.
10. Fique com o boneco adversário entre você e o aliado: o passe normal não acha alvo ("Nenhum aliado livre"); o passe alto funciona.

**Salto e bloqueio**
11. Sem a bola, toque **Espaço / A**: salto rápido. Segure: agacha parado e pula mais alto ao soltar. Ao pousar, fica ~0,5 s travado ("POUSANDO").
12. Andando/correndo e pulando: o salto vai na direção em que você se movia; no ar não dá para mudar.
13. Com a bola, chegue colado no boneco adversário e arremesse: ele pula; se a bola passar nos braços dele, **TOCO** (a bola quica e cai, ele não pega).

**Roubo e drible**
14. Faça uma cesta para a posse ir ao adversário (ou espere ele pegar a bola). Ele anda até a cesta dele com a bola.
15. Chegue perto e aperte **E / X**: bote para frente. Se ele estiver driblando, falha: "LENTO" por 2 s (sem correr, pular ou roubar). Tente de novo quando ele não estiver driblando: **ROUBOU!**
16. Com a bola, aperte **Q / B**: drible de 1 s (troca de mão, mais lento, "DRIBLANDO (imune)"). As 2 bolinhas de DRIBLE na HUD gastam e recarregam (1 a cada 10 s, preenchendo aos poucos).
17. Durante o drible você não consegue arremessar nem passar.

---
## v0.2 — Etapa 1 revisada (escala, arremesso novo, animações, sons, tela cheia)

**Antes:** apague o `Build\tuning.json` antigo, se tiver (o jogo recria com os valores novos).

**Tela cheia / alt-tab**
1. Abra o jogo. Em Configurações → Geral, desligue **Tela cheia**: deve virar janela (80% da tela, centralizada). Ligue de novo: volta para tela cheia.
2. Feche e reabra: o jogo deve abrir no modo que você deixou.
3. Durante a partida, dê **alt-tab** e volte: a partida fica **pausada** e a câmera não dá "pulo". Clique em Continuar.

**Escala**
4. Inicie uma partida em cada tamanho: a **Pequena** deve ser claramente maior que a antiga Grande; a bola bem maior; a cesta maior e mais alta.

**Animações e quique**
5. Parado com a bola: o jogador **continua quicando**, com a mão direita acompanhando a bola. Som do quique baixinho.
6. Andando e correndo: pernas alternando, corpo balançando e inclinando para frente; braço esquerdo protegendo.
7. Carregando o arremesso: agacha com a bola acima da cabeça. Ao soltar: pulinho com os braços esticados para cima.

**Arremesso**
8. Segure o arremesso com a câmera reta (meio da tela ou acima): a barra mostra **ALCANCE x m** subindo devagar (≈2 s até 13 m). A trajetória amarela acompanha.
9. Ainda segurando, abaixe a câmera: o alcance diminui. Suba de novo: volta ao alcance carregado.
10. Segure sem soltar: com **3 s** o jogador arremessa sozinho. R / Y **não** cancela mais.
11. Mire um pouco ao lado da cesta adversária: a bola é puxada para ela. Faça o mesmo na **sua** cesta: sem ajuda (e a cesta dá ponto para o LARANJA).
12. Do meio da quadra Pequena não deve alcançar a outra cesta (fim da quadra).
13. Chegue a menos de 3 m da cesta adversária: a barra mostra **BANDEJA**; um toque no botão faz um arremesso alto e certeiro.

**Sons**
14. Faça uma cesta: "fsss" da rede + sininhos. Vença a partida: fanfarra. Modo por tempo até zerar: buzina (mais suave que antes).

**Tuning**
15. No painel de ajustes (seção Arremesso), mude "Alcance com carga máxima" para 20, cole no `Build\tuning.json`, aperte F5 no jogo: o alcance passa a ir até 20 m.

---
## v0.1 — Etapa 1 (núcleo jogável)

**Menu e configurações**
1. Abra o `.exe`. Deve abrir em **tela cheia** com o menu (céu azul, título, botões).
2. Configurações → Geral: mexa no volume geral (efeito audível na partida) e desligue/ligue **Tela cheia** (alterna na hora).
3. Configurações → Controles: troque "Arremesso" (teclado) para outra tecla; troque "Salto" (controle) para outro botão. Feche e reabra o jogo: os atalhos devem continuar trocados. "Restaurar controles padrão" volta tudo.
4. Ative **Corrida: alternar** e confirme na partida que um toque no Shift liga a corrida e ela desliga ao parar de andar.

**Partida**
5. Jogar (local) → escolha quadra **Pequena**, modo **Por pontos**, 5 pontos → Iniciar.
6. Contagem **3, 2, 1, VAI!** com bipes; antes do "VAI!" você não anda.
7. Ande (WASD / analógico esquerdo): a bola **quica sozinha** ao andar e fica na mão parado. Gire a câmera (mouse / analógico direito).
8. Segure **Shift / LT**: corre mais rápido, barra verde (estamina) desce; ao soltar, recarrega rápido. Com estamina zerada não corre.
9. Segure **botão esquerdo / RT**: braços sobem, barra **FORÇA** enche, aparecem **pontos amarelos** da trajetória. Solte → arremessa.
10. Segure e aperte **R / Y** → "Arremesso cancelado", você continua com a bola. Segure por 4 s → cancela sozinho.
11. Acerte uma cesta de dentro do garrafão → **+2**; de trás do arco grande → **+3**. Depois de cada cesta: pausa curta, você volta ao meio e a contagem recomeça.
12. Erre de propósito: a bola quica no aro/tabela/chão; ande até ela para pegar. Uma bola **subindo** não pode ser pega; **caindo** pode.
13. Chegue a 5 pontos → tela de **vitória** com "Jogar de novo" e "Menu principal".
14. Teste o modo **Por tempo** (1 min): o relógio desce só durante o jogo; ao zerar empatado (0x0) aparece "PRORROGAÇÃO".
15. Esc / Menu (controle) pausa; "Configurações" funciona dentro da pausa.
16. Troque entre teclado e controle no meio da partida sem configurar nada.

**Tuning**
17. Abra o artifact de tuning, mude "Velocidade andando" para 8, copie, cole em `Build\tuning.json`, salve, volte ao jogo e aperte **F5**: aparece a mensagem no canto e você fica bem mais rápido.

**O que reportar:** sensação do arremesso (força/ângulo/assistência), velocidade, câmera, qualquer travamento ou erro.
