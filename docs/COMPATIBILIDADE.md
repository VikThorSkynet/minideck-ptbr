# Compatibilidade e limites

## Unidade analisada

| Item | Valor observado |
|---|---|
| Plataforma | Windows x64; WPF / .NET 8 |
| USB | VID `1189`, PID `8890` |
| Interface de configuração | `MI_01`, uso de fornecedor `FF00:0001` |
| Relatórios da unidade | Entrada e saída de 65 bytes; Report ID 3 |
| Tentativas de compatibilidade | Report IDs 3, 0 e 2, conforme o configurador analisado |
| Layout inicial | 6 teclas, 1 knob |

O código suporta desenho de 1–12 teclas e 0–2 knobs, mas isso não equivale a validação física de todos esses modelos. Existem firmwares que compartilham VID/PID e usam protocolos diferentes. A identificação USB sozinha não comprova compatibilidade.

## Software e memória nativa

| Recurso | Precisa do MiniDeck aberto? |
|---|---|
| Camadas livres, soundboard, áudio por processo/dispositivo, texto, programas e macros | Sim |
| Painel compacto e identificação da tecla | Sim |
| Atalho/sequência de até 5 passos, mídia e mouse gravados diretamente na memória | Não, depois de gravar |
| Modo de LED gravado | Depende do firmware/unidade; o envio é persistido no aparelho |

O modo de camadas dispõe de F13–F24: **12 entradas**, contando três por knob. Seis teclas e dois knobs usam todas. Um layout com 12 teclas e knobs excede o modo de camadas, mas pode usar a gravação direta.

Há até três memórias nativas nas versões compatíveis; para Report ID 0, o MiniDeck limita a gravação à memória 1. O comando de seleção encontrado serve para **configurar** a memória. Não foi encontrada uma ação gravável de tecla que faça alternância nativa entre elas.

## O que não foi encontrado

- Leitura dos bindings atuais e backup da memória existente.
- Consulta confiável de versão legível do firmware ou confirmação do conteúdo gravado.
- Controle RGB, brilho, velocidade e LED físico por tecla.
- Modo comprovado de alternância nativa de camadas por tecla.

Não há atualização de firmware, bootloader, driver, serviço, inicialização automática do Windows ou conexão de rede no fluxo normal do aplicativo.

## Limites práticos

- O modo exclusivo reserva os F13–F24 físicos usados pelo layout em todos os teclados. Hooks do Windows não identificam a origem física; não há filtragem por dispositivo nesse caminho. Teclas normais fora dessa faixa passam.
- O modo compartilhado usa Raw Input filtrado por dispositivo e não suprime os gatilhos originais.
- O Windows só define F até F24. A geração automática oferece 96 combinações com Ctrl/Alt/Shift: 16 bancos com seis teclas ou 8 bancos com doze. Camadas de ações continuam com limite de 100. Knobs em Sem ação não recebem combinação automática.
- Aplicativos que instalam outros hooks ou capturam entrada por caminhos próprios precisam de teste no destino. A instalação do hook não comprova que todo atalho global de qualquer aplicativo será isolado.
- Use uma única unidade desse modelo conectada ao configurar.
- Atalhos são enviados ao aplicativo em primeiro plano; o Windows pode bloquear envio a processos com privilégio maior.
- Controle de microfone altera o nível de entrada do Windows, não filtros internos de programas.
- Áudio por processo exige uma sessão de áudio ativa.
- MP3/WAV e a saída WASAPI foram testados; outros formatos dependem dos codecs instalados.
- Uma macro roda por vez; até 16 sons podem tocar em paralelo.
- Efeitos de LED, áudio audível, roteamento e gravação devem ser conferidos no equipamento de destino.
