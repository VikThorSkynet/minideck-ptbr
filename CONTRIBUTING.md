# Como contribuir

Abra uma issue para bugs ou propostas; os formulários ajudam a informar a versão, o layout e os passos necessários. Uma proposta de recurso não precisa de código pronto.

Para alterar o código:

1. Faça um fork e crie uma branch para a mudança.
2. Configure o ambiente descrito em [desenvolvimento](docs/DESENVOLVIMENTO.md).
3. Faça uma alteração com escopo claro e preserve perfis anteriores.
4. Execute `testar.ps1` e as verificações adicionais relevantes.
5. Atualize a documentação e o changelog quando houver mudança de comportamento.
6. Abra um pull request explicando o problema, o resultado e como verificou.

Use português claro nos textos da interface e mantenha parâmetros, caminhos e erros legíveis. Evite executar trabalho demorado na thread da interface. Libere arquivos, objetos de áudio e conexões HID quando deixarem de ser usados.

Mudanças de protocolo precisam de exemplos de bytes e testes. Não anuncie recursos de hardware com base apenas em campos do software ou em um VID/PID compartilhado. Diga o que foi confirmado fisicamente e o que ainda é uma hipótese.

Não envie arquivos pessoais, áudio sem permissão de distribuição, diagnósticos locais, credenciais, binários do fabricante ou arquivos decompilados. A documentação do protocolo é suficiente para discutir os formatos implementados.
