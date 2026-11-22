# Atalhos externos por camada

Este modo resolve o caso de um mesmo botão físico disparar o atalho de dois programas porque todas as camadas usavam o mesmo F17, por exemplo.

## Usar

1. Deixe as camadas ativas e **Atalhos exclusivos por camada** marcado.
2. Selecione a camada e a tecla.
3. Escolha **Sem ação** e clique em **Salvar ação**.
4. Veja a combinação mostrada abaixo do título do editor ou no compacto.
5. Abra a configuração de atalhos do programa de destino e cadastre essa combinação, apertando a tecla física enquanto a camada correta está selecionada.

O MiniDeck não modifica os atalhos salvos no OBS, no programa de ditado ou em outro aplicativo. Remova/substitua os atalhos antigos nesses programas para fazer a migração.

**Exemplo:** com seis teclas, a tecla 5 na primeira camada envia F17; na segunda, F23; na terceira, Ctrl+F17. Mantenha no OBS a combinação da camada OBS e recadastre no ditado a combinação da camada de ditado.

## Faixas com seis teclas

| Banco | Teclas 1–6 |
|---|---|
| 1 | F13–F18 |
| 2 | F19–F24 |
| 3 | Ctrl+F13–F18 |
| 4 | Ctrl+F19–F24 |
| 5 | Alt+F13–F18 |
| 6 | Alt+F19–F24 |
| 7 | Shift+F13–F18 |
| 8 | Shift+F19–F24 |
| 9 | Ctrl+Alt+F13–F18 |
| 10 | Ctrl+Alt+F19–F24 |
| 11 | Ctrl+Shift+F13–F18 |
| 12 | Ctrl+Shift+F19–F24 |
| 13 | Alt+Shift+F13–F18 |
| 14 | Alt+Shift+F19–F24 |
| 15 | Ctrl+Alt+Shift+F13–F18 |
| 16 | Ctrl+Alt+Shift+F19–F24 |

O [Windows define F1–F24](https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes). Não existe uma sequência nativa F25, F26 etc. Este modo oferece 96 combinações distintas com F13–F24 e Ctrl/Alt/Shift, sem usar a tecla Windows ou inventar códigos reservados.

A capacidade depende da quantidade de teclas: são 16 bancos de seis teclas ou oito de doze. O MiniDeck continua aceitando até 100 camadas de ações próprias. Se o banco passar da faixa automática, a tecla sem ação não envia uma combinação reutilizada: o painel pede uma ação ou um atalho manual. Use as ações do MiniDeck ou uma combinação manual que o seu programa aceite.

Os bancos ficam salvos em `HotkeyBank` na camada e `NextHotkeyBank` no perfil. Renomear, reordenar ou excluir outra camada não altera o atalho já atribuído. Bancos excluídos não são reciclados automaticamente. Alterar a quantidade de teclas modifica a distribuição e exige recadastrar os atalhos externos.

## Pressionar e soltar

O atalho externo fica pressionado enquanto a tecla física estiver pressionada. Soltar a tecla libera a combinação original, mesmo se a camada mudou nesse intervalo. Isso permite atalhos de ditado enquanto segura, desde que o programa de destino suporte a combinação.

Teclas com ações configuradas — trocar de camada, tocar som, mídia, macro etc. — executam essas ações sem enviar um F adicional. **Sem ação nos knobs** permanece sem comando; para um knob, configure uma ação ou um **Atalho de teclado** explícito.

## Reserva global e modo anterior

O modo exclusivo usa um hook do Windows em uma thread própria para reservar os F13–F24 físicos utilizados pelo layout. Essa API **não identifica de qual teclado veio a tecla**: a reserva abrange a faixa nos demais teclados enquanto o modo está ativo, as camadas estão preparadas e uma única unidade está conectada. Eventos injetados são deixados passar, evitando recapturar os atalhos que o MiniDeck envia.

Desmarcar **Atalhos exclusivos por camada** volta ao modo compartilhado via Raw Input filtrado pelo USB, no qual os gatilhos originais também passam. Pausar as ações, desconectar ou encerrar libera as combinações mantidas pelo MiniDeck. Não há instalação de driver nem nova gravação USB para alternar entre os modos.

Programas que instalam seus próprios hooks, usam outra API de entrada ou exigem privilégio de administrador podem tratar os eventos de outra forma. Confira no programa de destino que só o comando desejado dispara. A CI testa a política e o envio abstrato; a verificação nativa confirma a instalação do hook, sem provar a integração com todos os programas.
