# Histórico de versões

## Próxima versão

- Atalhos, macros e combinações por camada enviados pelo MiniDeck agora levam o scan code físico da tecla. Aplicativos Chromium/Electron, como o Claude desktop, comparam `KeyboardEvent.code` e ignoravam atalhos como Ctrl+F12 enviados sem ele.
- Visual renovado e autoexplicativo, sem mudança de funcionalidade: tema escuro único (`Tema.xaml`) com listas, campos e barras de rolagem escuras e barra de título escura; menu com descrição de cada página e destaque da página atual; guia 1-2-3 na tela principal; explicação abaixo de cada campo, caixa que descreve cada tipo de ação e dicas ao passar o mouse em botões e campos; páginas de Iluminação e Gravar no teclado em passos numerados com avisos destacados; “Como funciona” em cartões agrupados, com glossário; editor de sequência com instruções.

## 1.3.0

- Atalhos exclusivos por camada para teclas configuradas como Sem ação.
- Sequência F13–F18, F19–F24 e depois combinações Ctrl/Alt/Shift, sem inventar teclas F25+.
- Bloqueio dos gatilhos físicos F13–F24 usados pelo layout enquanto o modo está ativo; a reserva abrange todos os teclados.
- Teclas com ações internas não enviam mais um F extra no modo exclusivo.
- Pressão/liberação, contagem de modificadores e liberação ao pausar/desconectar/encerrar.
- Bancos persistentes que não mudam ao excluir ou renomear outra camada.
- Atalho externo visível no editor e compacto, preservando os nomes personalizados.
- Limite explícito de 96 combinações automáticas e opção de retornar ao modo compartilhado.
- Testes de isolamento, unicidade, persistência, liberação e ciclo do hook nativo.

## 1.2.0

- Soundboard com MP3/WAV, escolha de saída e volume individual.
- Reinício, tocar/parar na mesma tecla, sobreposição e repetição contínua.
- Volume geral dos sons, mudo, parada de todos e controles para teclas/knob.
- Botão **+ Sons** para criar uma camada com controles de volume no knob.
- Parada de sons no editor, compacto e bandeja.
- Preservação dos perfis anteriores e testes de áudio/recursos.
- Documentação pública, scripts de distribuição e CI no GitHub.

## 1.1.1

- Destaque amarelo por dois segundos após soltar a tecla.
- Marca persistente da última entrada, preservada na troca de camada.
- Contraste independente da cor personalizada da camada.

## 1.1.0

- Painel compacto movível, redimensionável e fixável para segunda tela.
- Visualização da camada e das ações de teclas/knob.
- Captura de atalhos, argumentos de programas e volumes absolutos.
- Macros editáveis, pausas, repetição e cancelamento.
- Ação para aplicar um dos modos de LED recuperados.
- Instância única e comandos para alternar editor/compacto.

## 1.0.0

- Interface PT-BR e configuração de teclas e knobs.
- Camadas de software via F13–F24 e Raw Input filtrado pelo USB.
- Áudio por aplicativo/dispositivo, microfone, mídia, atalhos, texto e mouse.
- Gravação direta de comandos nativos e modos de LED 0–2.
- Perfis JSON, exportação/importação e bandeja.
- Documentação inicial dos quadros USB recuperados do configurador.
