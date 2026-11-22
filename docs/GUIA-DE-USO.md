# Guia de uso

## 1. Entenda o teclado

No modelo inicial existem seis teclas e um botão giratório, o **knob**. Esse knob tem três ações diferentes: girar à esquerda, apertar e girar à direita.

Uma **camada** é uma página de funções. A tecla 1 pode mutar o microfone em uma camada, tocar um som em outra e copiar texto em outra. No perfil inicial, a tecla 6 passa para a próxima camada: Principal → Áudio → Atalhos → Principal.

## 2. Prepare uma vez

1. Abra o MiniDeck e confira se aparece **Mini teclado conectado**.
2. Em **Ajustar layout e gerenciar perfil**, confira seis teclas e um knob, ou ajuste para o seu aparelho.
3. Clique em **Ativar camadas** e revise as entradas que serão preparadas.
4. Aguarde o envio e confira **Camadas ativas**.
5. Pressione uma tecla física: o cartão correspondente deve acender no painel.

A preparação troca os comandos anteriores das entradas por F13–F24 nas camadas nativas disponíveis. O programa não lê nem faz backup da configuração antiga da memória do aparelho. Feche o configurador original antes de gravar.

Depois dessa preparação, mudar as ações de software exige apenas **Salvar ação**. Se outro programa reconfigurar o teclado ou você alterar o layout, prepare as entradas novamente.

## 3. Configure uma tecla

1. Escolha a camada.
2. Clique no desenho da tecla ou na direção/clique do knob.
3. Escreva um **Nome no painel** fácil de reconhecer.
4. Em **O que esta entrada faz?**, escolha a ação.
5. Preencha as opções que aparecem e clique em **Salvar ação**.

**Testar ação em 3 segundos** executa os valores do formulário antes de salvar. Use o intervalo para colocar o aplicativo de destino em primeiro plano. Salvar e testar são operações diferentes.

Para atalhos, **Capturar atalho** registra a combinação. Pressione Esc para cancelar a captura.

## 4. Crie camadas

Use **+ Nova** para uma camada comum ou **+ Sons** para uma camada com o knob preparado para o soundboard. Renomear, duplicar, excluir e escolher a cor do painel ficam perto das abas das camadas.

Reserve uma tecla com **Camadas · próxima**, **anterior** ou **ir para**. **Enquanto segura** muda temporariamente de camada e volta ao soltar; precisa de uma tecla ou do clique do knob e de eventos de pressão/liberação mantidos pelo firmware.

Há um limite de 100 camadas. A mudança de camada de software precisa do MiniDeck aberto.

## 5. Deixe o painel na outra tela

1. Clique em **Modo compacto**.
2. Arraste o cabeçalho MINIDECK para a segunda tela.
3. Marque **Fixar** para manter por cima das janelas.
4. Ajuste o tamanho pelo canto inferior direito.

O nome da camada e as funções acompanham o teclado. O cartão fica amarelo durante a pressão e por dois segundos após soltar. A bolinha, a borda e o texto **Última** continuam mostrando a última entrada usada.

Clicar em um cartão compacto mostra a descrição; não executa a ação. **Configurar** abre o editor. **×** oculta o painel e mantém o programa na bandeja. Posição, tamanho e fixação ficam salvos.

## 6. Controle o áudio

- **Saída de áudio:** volume do dispositivo escolhido ou da saída padrão do Windows.
- **Microfone:** volume de entrada ou mudo de um dispositivo específico; não altera filtros nem ganho interno de OBS/Discord/Voicemeeter.
- **Aplicativo:** volume das sessões do processo informado, sem `.exe`, como `chrome`. O aplicativo precisa reproduzir áudio para aparecer na lista.
- **Sons:** volume dos arquivos que o próprio MiniDeck está tocando. Tem volume individual e geral.

Para sons e gravação, siga [este passo a passo](SONS-E-GRAVACAO.md).

## 7. Monte uma macro

Escolha **Sequência de ações / macro**, clique em **Montar sequência de ações** e adicione os passos na ordem desejada. Você pode mover/remover passos, inserir pausas e repetir a sequência.

Limites: 100 passos, até 10 minutos de espera por passo e 1–100 repetições. Sequências não contêm outras sequências nem ações de camada enquanto segura. Só uma macro roda por vez. Use **Parar sequência** ou uma tecla com essa ação para cancelar. Sons já iniciados têm parada independente: **Parar sons**.

## 8. Ajuste a iluminação

Abra **Iluminação**, escolha a memória nativa e um dos modos 0, 1 e 2. O envio é gravado no teclado; observe o aparelho para descobrir o efeito desta unidade.

Também existe a ação **LED do teclado · aplicar modo** para uma tecla ou macro. Cor do painel, RGB físico e modos do firmware são coisas diferentes: os únicos controles físicos recuperados foram os três modos, sem ajuste comprovado de cor, brilho ou velocidade.

## 9. Salve e recupere seus ajustes

Em **Ajustar layout e gerenciar perfil**, use **Exportar perfil**. O JSON inclui camadas, ações e caminhos de sons. Os arquivos de áudio ficam nas pastas originais e não são incluídos na exportação.

O perfil local fica em `%LOCALAPPDATA%\MiniDeck\perfil.json`; `perfil.json.bak` guarda a versão anterior à última gravação. A importação substitui o perfil aberto após confirmação e exige revisar/preparar as entradas novamente.

## Se algo não funcionar

| Sintoma | O que conferir |
|---|---|
| Tecla continua com função antiga | Camadas ativas, layout correto e preparação das entradas; outro configurador pode ter regravado a memória. |
| Aplicativo não aparece no áudio | Reproduza um som nele e atualize a lista, ou digite o nome do processo. |
| Som não entra no OBS | Confira a saída escolhida e a fonte de captura de áudio do OBS. |
| Som está mudo | Volume individual, volume geral de Sons, mudo do knob, mixer do Windows e roteamento do dispositivo. |
| Arquivo não abre | Confira se foi movido/apagado ou tente um MP3/WAV válido. |
| Atalho não chega ao destino | Coloque o destino em primeiro plano; o Windows pode bloquear envio entre níveis diferentes de administrador. |
| Não há efeito de LED | A tela não comprova LED físico; os efeitos precisam de verificação visual no hardware. |
| Dois teclados do modelo aparecem | Deixe apenas o dispositivo que quer configurar conectado. |

Para encerrar de verdade, use **Encerrar MiniDeck** no ícone da bandeja.
