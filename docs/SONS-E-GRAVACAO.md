# Sons e gravação

## Monte sua camada

1. Clique em **+ Sons**. No layout de seis teclas, as primeiras cinco ficam disponíveis e a última muda de camada.
2. Escolha uma tecla e a ação **Sons · tocar arquivo MP3 / WAV**.
3. Clique em **Escolher som…** e selecione um arquivo seu.
4. Escolha a **saída de áudio**: padrão do Windows, headset, caixas ou uma saída virtual já instalada.
5. Ajuste o **volume deste som**.
6. Escolha **reiniciar**, **tocar/parar** ou **sobrepor**; marque repetição se precisar.
7. Salve e teste com a tecla física ou **Testar ação em 3 segundos**.

O arquivo permanece na pasta original. Use uma pasta estável, como `C:\Sons`, e atualize o caminho se mover o arquivo. MP3 e WAV foram testados. Outros formatos do seletor dependem dos codecs do Windows.

## Entenda os volumes

| Controle | Altera |
|---|---|
| Volume deste som | Percentual individual da ação, aplicado na próxima reprodução. |
| Sons · volume geral | Todos os sons do MiniDeck, inclusive os que já estão tocando. |
| Knob da camada criada por + Sons | Esquerda diminui o geral; clique alterna mudo; direita aumenta. |
| Saída de áudio / mixer do Windows | Volume do dispositivo ou da sessão, controlado pelo Windows. |

Exemplo: um som a 50% com volume geral a 60% usa ganho de 30%, antes do mixer e do dispositivo. O controle interno dos sons não muda o volume de outros aplicativos nem o volume mestre do Windows.

Se o indicador mostrar **mudo**, clique novamente no knob com **Sons · mutar / desmutar**. Aumentar o percentual não remove esse estado de mudo.

## Faça aparecer no OBS

O som precisa chegar a uma fonte capturada pela gravação. Escolha uma destas rotas conforme sua configuração:

| Rota | MiniDeck | OBS |
|---|---|---|
| Saída já capturada | Escolha a mesma saída que usa para ouvir. | Capture essa saída em Áudio do desktop ou Captura de saída de áudio. |
| Captura por aplicativo | Toque um som no MiniDeck. | Adicione/configure uma captura de áudio de aplicativo apontada para o MiniDeck, quando disponível na sua versão. |
| Voicemeeter / cabo virtual | Escolha a entrada de reprodução virtual correspondente na lista de saídas. | Capture o dispositivo de gravação/barramento correspondente à rota que você configurou. |

Os nomes variam conforme os dispositivos e a versão do OBS. Escolher uma saída no MiniDeck não instala cabo virtual nem altera cenas, fontes, monitoramento ou barramentos do OBS/Voicemeeter.

Antes de gravar algo importante, faça uma gravação curta: toque um som, confira o medidor da fonte no OBS e reproduza o arquivo gravado. Evite capturar a mesma rota duas vezes, o que pode duplicar o som.

## Pare ou repita

- **Reiniciar:** outro toque começa o mesmo arquivo do zero na mesma saída.
- **Tocar/parar:** outro toque interrompe; o seguinte começa de novo.
- **Sobrepor:** cada toque cria outra reprodução, com limite total de 16.
- **Repetir:** volta ao começo até receber uma parada.
- **Parar sons:** encerra tudo. Disponível no editor, na bandeja, como ação e no botão **♪ ■** do compacto.

Trocar de camada mantém os sons tocando. Encerrar o MiniDeck para todos. Sons iniciados por macros continuam independentes do cancelamento da macro.

## Perfil de exemplo

[examples/perfil-sons.json](../examples/perfil-sons.json) demonstra duas camadas com um caminho fictício `C:\Sons\aplausos.mp3`. Antes de usar, escolha um arquivo existente e a saída correta.

Importar esse exemplo substitui o perfil aberto após confirmação. Exporte seu perfil atual primeiro se quiser guardá-lo. A importação não contém áudio e não recupera a memória original do teclado.
