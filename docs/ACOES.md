# Catálogo de ações

Todas as ações abaixo podem ser atribuídas a uma entrada física. Um knob tem três entradas. O clique nos cartões do editor seleciona a configuração; a tecla física executa a ação quando as camadas estão ativas.

| ID no perfil | Nome na interface | Parâmetros / efeito |
|---|---|---|
| `none` | Sem ação | Entrada sem comando de software. |
| `nextLayer` | Camadas · próxima | Próxima camada, com retorno circular. |
| `previousLayer` | Camadas · anterior | Camada anterior, com retorno circular. |
| `layer` | Camadas · ir para | `Target`: ID da camada. |
| `holdLayer` | Camadas · enquanto segura | `Target`: camada temporária; volta ao soltar. Tecla/clique, não giro. |
| `soundPlay` | Sons · tocar arquivo MP3 / WAV | `Value`: caminho absoluto; `Target`: saída; `SoundVolume`: 0–100; `SoundBehavior`; `SoundLoop`. |
| `soundStop` | Sons · parar todos | Encerra todos os sons ativos, inclusive repetição. |
| `soundVolumeUp` | Sons · aumentar volume geral | `Step`: 1–100 pontos percentuais por toque. |
| `soundVolumeDown` | Sons · diminuir volume geral | `Step`: 1–100 pontos percentuais por toque. |
| `soundMute` | Sons · mutar / desmutar | Preserva o valor geral e alterna o mudo dos sons. |
| `soundVolumeSet` | Sons · definir volume geral | `Value`: percentual de 0 a 100. |
| `outputUp` | Saída de áudio · aumentar volume | `Target`: dispositivo ou padrão; `Step`. |
| `outputDown` | Saída de áudio · diminuir volume | `Target`: dispositivo ou padrão; `Step`. |
| `outputMute` | Saída de áudio · mutar / desmutar | Alterna o mudo do dispositivo escolhido. |
| `outputSet` | Saída de áudio · definir volume | `Target`: dispositivo; `Value`: 0–100%. |
| `micUp` | Microfone · aumentar volume | `Target`: microfone ou padrão de comunicação; `Step`. |
| `micDown` | Microfone · diminuir volume | `Target`: microfone ou padrão de comunicação; `Step`. |
| `micMute` | Microfone · mutar / desmutar | Alterna o mudo da entrada do Windows. |
| `micSet` | Microfone · definir volume | `Target`: microfone; `Value`: 0–100%. |
| `appUp` | Aplicativo · aumentar volume | `Target`: processo sem `.exe`; `Step`. |
| `appDown` | Aplicativo · diminuir volume | `Target`: processo sem `.exe`; `Step`. |
| `appMute` | Aplicativo · mutar / desmutar | Alterna o mudo das sessões desse processo. |
| `appSet` | Aplicativo · definir volume | `Target`: processo; `Value`: 0–100%. |
| `media` | Multimídia | `play`, `next`, `previous`, `stop`, `mute`, `up`, `down`. |
| `shortcut` | Atalho de teclado | `Value`: combinação como `Ctrl+Shift+M`; pode ser capturada. |
| `text` | Digitar texto | `Value`: texto Unicode para o aplicativo em foco. |
| `open` | Abrir programa, arquivo ou site | `Value`: caminho ou endereço aberto pelo Windows. |
| `program` | Executar programa com argumentos | `Value`: programa; `Arguments`; `WorkingDirectory` opcional. |
| `mouse` | Mouse · clique ou rolagem | Esquerdo, direito, central; rolar para cima/baixo, com Ctrl/Shift/Alt opcionais. |
| `sequence` | Sequência de ações / macro | `Steps`: 1–100 passos; `Repeat`: 1–100; sem sequências aninhadas. |
| `delay` | Esperar entre ações | `DelayMs`: até 600.000 ms; não bloqueia a interface. |
| `cancelMacro` | Parar sequências em execução | Cancela a macro ativa, inclusive durante uma pausa. |
| `led` | LED do teclado · aplicar modo | `Target`: memória nativa 1–3; `Value`: modo 0–2; grava no aparelho. |

## Opções de som

| `SoundBehavior` | Interface | Ao apertar novamente |
|---|---|---|
| `restart` | Reiniciar o som do começo | Substitui as reproduções do mesmo arquivo na mesma saída. |
| `toggle` | Tocar / parar na mesma tecla | Para as reproduções existentes; o próximo toque começa novamente. |
| `overlap` | Sobrepor: tocar outra cópia | Inicia uma cópia independente, até o limite de 16 sons ativos. |

`SoundLoop: true` repete até parar. A identificação de reinício/tocar-parar usa o caminho completo e a saída; dois botões com o mesmo arquivo e saída controlam o mesmo conjunto de reproduções.

`SoundMasterVolume` e `SoundMuted` ficam na raiz do perfil. O volume final é `SoundVolume / 100 × SoundMasterVolume / 100`, ou zero quando os sons estão mudos. As ações gerais atualizam as reproduções em andamento. Mudar a configuração individual de uma ação afeta as próximas reproduções.

## Ações em macros

Macros podem combinar atalhos, texto, mídia, mouse, programas, sons, áudio, LED e mudanças definitivas de camada. Ações de espera são executadas de forma assíncrona. Iniciar um som não espera sua duração: acrescente uma pausa se precisar espaçar os efeitos.

`holdLayer` e `sequence` não podem ser passos de outra sequência. `cancelMacro` cancela a macro; não para os sons já iniciados. `soundStop` para os sons; não cancela a macro.

## Gravação direta

As opções da tela **Gravar no teclado** são um caminho separado das ações de software: atalhos/sequências de até cinco passos, comandos de mídia ou mouse. Não gravam MP3, macros livres, nomes de aplicativos ou arquivos dentro do aparelho. Consulte o [protocolo USB](PROTOCOLO-USB.md).
