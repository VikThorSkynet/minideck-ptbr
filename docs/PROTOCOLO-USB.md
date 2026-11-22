# Protocolo recuperado do programa fornecido

Fonte principal: `MINI KeyBoard.exe`, SHA-256 `4AE53D3A40921E4F57444C489FA1B38FE91A2E907FEDA17630450FC97A7CD7B1`.

A análise foi realizada sobre uma cópia local, por decompilação com ILSpyCmd 9.1.0.7988. O código-fonte original do fabricante não foi fornecido. O executável, seus recursos e os arquivos reconstruídos pelo decompilador não são distribuídos neste repositório. Esta documentação registra o protocolo implementado no MiniDeck.

## Dispositivo observado

- USB VID `0x1189`, PID `0x8890`.
- Interface de configuração `MI_01`, uso de fornecedor `FF00:0001`.
- Nesta unidade, os relatórios de entrada e saída dessa interface têm **65 bytes**. O descritor reconstruído pelo HidSharp contém Report ID **3** (`85 03`).
- Também existem interfaces de teclado, consumidor/mídia e mouse. `MiniDeck.exe --diagnostico diagnostico.json` gera um diagnóstico local somente de leitura. Diagnósticos individuais não são publicados neste repositório.
- O código original procura somente o VID e `mi_01`. O substituto restringe também o PID para não configurar outros dispositivos do mesmo fabricante.

## Transporte

`HIDTester.HidLib.WriteDevice` cria um relatório HID, define seu ID e copia oito bytes do payload para `Data[0..7]`. Os demais bytes ficam zerados. O primeiro byte no fio é o Report ID; segue o payload, completado até o tamanho de saída anunciado pelo dispositivo.

`KeyBoardVersion_Check` tenta enviar payload zerado com IDs 3, 0 e 2, nessa ordem. A identificação é baseada no sucesso de escrita, sem consulta de versão ou confirmação da memória. O substituto mantém essa compatibilidade. Para Report ID 0, o original omite os bits da camada; o MiniDeck limita a gravação direta à camada 1 nessa versão.

## Entradas físicas

| Entrada | ID no payload |
|---|---:|
| Teclas 1 a 12 | 1 a 12 |
| Knob 1: esquerda, pressionar, direita | 13, 14, 15 |
| Knob 2: esquerda, pressionar, direita | 16, 17, 18 |
| Configuração de LED | `B0` |

O formulário tem controles adicionais para teclas 13–16 e knob 3, mas esta versão não lhes atribui handlers de gravação. Eles não foram tratados como recursos confirmados.

## Payloads

Os exemplos abaixo mostram **oito bytes**, sem o Report ID. Para a unidade observada, prefixe `03` e complete o quadro até 65 bytes.

`L`: camada 1, 2 ou 3. `S`: ID da entrada. `T = (L << 4) | tipo`. Para Report ID 0, `T = tipo`.

| Operação | Payload |
|---|---|
| Selecionar camada para configuração | `A1 L 00 00 00 00 00 00` |
| Início de sequência de N teclas | `S T N 00 modificador_do_primeiro_passo 00 00 00` |
| Passo I da sequência | `S T N I modificadores uso_USB 00 00` |
| Salvar configuração de entrada | `AA AA 00 00 00 00 00 00` |
| Mídia | `S T uso_low uso_high 00 00 00 00` |
| Mouse | `S T botões delta_X delta_Y roda modificadores 00` |
| LED | `B0 T modo 00 00 00 00 00` |
| Salvar configuração de LED | `AA A1 00 00 00 00 00 00` |

Tipos: `1` teclado, `2` consumidor/mídia, `3` mouse, `8` LED. O original envia `A1 L` antes de gravar quando o Report ID não é zero. Esse comando aparece como seleção da camada **para configuração**; não foi encontrada uma ação gravável de tecla que alterne camadas nativas.

Uma sequência usa de um a cinco passos. Modificadores são a máscara USB: Ctrl esquerdo 1, Shift esquerdo 2, Alt esquerdo 4, Win esquerdo 8; correspondentes direitos 16, 32, 64, 128. O primeiro quadro de índice 0 repete os modificadores do primeiro passo exatamente como o programa fornecido.

Mouse: esquerdo 1, direito 2, central 4. Roda para cima 1; para baixo `FF`. Ctrl/Shift/Alt com roda usam os bits 1/2/4 no campo de modificadores.

Report ID 3 usa usos de consumidor: play/pause `CD`, anterior `B6`, próxima `B5`, mudo `E2`, volume + `E9`, volume − `EA`. Os IDs 0 e 2 possuem codificações próprias, recuperadas de `MULKey.cs` e cobertas pelos testes. Não foi encontrado comando nativo de parar reprodução nessa tela.

## LED e memória

`LEDkey.cs` expõe apenas três valores de modo: 0, 1 e 2. Os recursos os chamam somente de `LED_Mode0`, `LED_Mode1` e `LED_Mode2`. Não existe seletor de cor, brilho, velocidade ou LED por tecla. A existência física e os efeitos não podem ser concluídos pela presença da tela; precisam de observação do dispositivo.

Não foram encontrados comandos para ler bindings atuais, obter o layout físico, exportar a memória, consultar uma versão legível ou confirmar o conteúdo gravado. O novo aplicativo não anuncia backup da configuração prévia nem confirmação de leitura após gravação.

## Camadas e áudio do novo aplicativo

As entradas configuradas para o modo MiniDeck emitem F13–F24 (usos USB 104–115). A versão 1.3 separa esses gatilhos internos da saída externa. No modo exclusivo, um hook `WH_KEYBOARD_LL` reserva os F13–F24 físicos usados pelo layout e impede sua propagação normal; o evento é despachado para a camada, e somente uma tecla em Sem ação gera seu atalho externo. Eventos injetados não são reprocessados pelo hook. Esse caminho reserva a faixa em todos os teclados, pois a API não fornece identidade do dispositivo.

O modo compartilhado anterior continua usando Raw Input com `RIDEV_INPUTSINK`, filtro `VID_1189&PID_8890` e propagação dos gatilhos originais. A preparação USB não muda ao alternar esses modos. Não é instalado driver ou firmware.

Camadas de software e o controle por processo/dispositivo são recursos implementados no Windows, não capacidades atribuídas ao firmware. O MiniDeck precisa permanecer aberto. Áudio usa WASAPI/Core Audio via NAudio e identifica sessões pelo nome do processo.

O soundboard também é inteiramente de software: Media Foundation decodifica os arquivos, e saídas WASAPI reproduzem o sinal com ganho aplicado às amostras. Nenhum arquivo MP3/WAV é gravado no teclado.

## Referências de implementação

- [Documentação oficial de Raw Input do Windows](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-raw-input).
- [Hook de teclado do Windows e supressão](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc).
- [Flags para eventos injetados](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-kbdllhookstruct).
- [Código e documentação do NAudio](https://github.com/naudio/NAudio).
- [HidSharp, projeto do autor](https://www.zer7.com/software/hidsharp). No Windows, seus descritores são reconstruídos a partir da API do sistema.
- [Experimento independente com F13–F18 no modelo 1189:8890](https://github.com/devkev/hid-minikb-libusb). Corrobora a estratégia de teclas estendidas; não substitui a validação desta unidade.

Há outros firmwares que compartilham o mesmo VID/PID e usam pacotes diferentes. Os pacotes implementados aqui seguem o executável fornecido pelo usuário, não foram substituídos por protocolos de outro modelo encontrados na internet.
