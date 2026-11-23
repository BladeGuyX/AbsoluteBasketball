# Roteiros de teste (manual)

Rodar sempre pelo `Build\AbsoluteBasketball.exe`. Os mais recentes ficam no topo.

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
