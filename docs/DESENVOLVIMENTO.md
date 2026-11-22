# Desenvolvimento, testes e releases

## Compilar

Requer Windows x64, PowerShell, Git e SDK do .NET 8. `global.json` aceita a feature band mais recente instalada da série 8.0. Os pacotes atuais são HidSharp 2.1.0 e NAudio.Wasapi/Core 2.2.1.

```powershell
git clone https://github.com/VikThorSkynet/minideck-ptbr.git
cd minideck-ptbr
.\compilar.ps1
```

A saída padrão é autocontida: `dist\win-x64\MiniDeck.exe`, documentação, licenças e um ZIP com SHA-256 em `dist`. Não precisa do runtime previamente instalado no destino. `-FrameworkDependent` gera uma distribuição que requer o runtime .NET 8 Desktop.

O script não encerra nem substitui um MiniDeck em execução em outra pasta. Não publique na pasta de um executável aberto; use outra saída ou encerre-o pela bandeja primeiro.

## Testes

```powershell
# Compila e executa somente os testes de lógica, sem hardware.
.\testar.ps1

# Verifica o executável da distribuição.
.\testar.ps1 -ExecutablePath .\dist\win-x64\MiniDeck.exe

# Também abre janelas descartáveis e verifica a interface.
.\testar.ps1 -ExecutablePath .\dist\win-x64\MiniDeck.exe -Interface

# Também usa a saída de áudio real, com volume de amostras em zero.
.\testar.ps1 -ExecutablePath .\dist\win-x64\MiniDeck.exe -Audio
```

Resultados e imagens ficam em `.artifacts`, fora do Git. Na versão 1.2, a referência local é 42 testes de lógica, 27 verificações de interface e 7 verificações de áudio.

| Grupo | Verifica | Dependências externas |
|---|---|---|
| Lógica | Quadros USB, camadas, perfis, volumes, macros, posicionamento, contrastes e ciclo de vida dos sons com saídas falsas. | Windows; sem teclado ou áudio ativo. |
| Interface | Campos, camada, pressão/liberação, última tecla, painel mínimo, parada de sons e montagem visual de macro. | Sessão gráfica interativa. |
| Áudio | Geração e decodificação de WAV/MP3, ganho das amostras, reprodução WASAPI silenciosa, repetição e parada. | Codec MP3 do Windows e saída padrão de áudio ativa. |

A CI executa apenas lógica. Os testes opcionais não comprovam áudio audível nem resposta física/efeitos de LED. Não realizam programação USB.

## Linha de comando

| Argumento | Efeito |
|---|---|
| `--compact` | Abre ou mostra o painel compacto na instância existente. |
| `--settings` | Abre ou mostra o editor completo. |
| `--quit` | Solicita encerramento da instância. |
| `--self-test arquivo.json` | Testes de lógica, resultado JSON e exit code 0/1. |
| `--ui-smoke pasta` | Verificações de interface em perfil descartável e imagens PNG. |
| `--sound-smoke pasta` | Testes de áudio real em silêncio e resultado JSON. |
| `--diagnostico arquivo.json` | Enumeração de interfaces HID, dispositivos e sessões de áudio; somente leitura. |

Modos de teste e diagnóstico rodam separados da instância normal. Diagnósticos podem conter caminhos e identificadores de dispositivos: revise antes de compartilhar.

## GitHub Actions

O workflow [windows.yml](https://github.com/VikThorSkynet/minideck-ptbr/blob/main/.github/workflows/windows.yml) publica uma compilação autocontida no runner Windows, executa os testes de lógica e guarda ZIP, hash e resultados como artefatos. As Actions são fixadas por SHA.

Push em `main`, pull requests e execução manual verificam o projeto. Tags `v*` também acionam a publicação automática de uma release, depois da compilação e dos testes, usando o ZIP produzido. Pull requests não recebem permissão de escrita para publicar.

Para uma versão nova:

1. Atualize `<Version>` em `MiniDeck.csproj`, o changelog e a documentação afetada.
2. Execute os testes necessários e faça commit.
3. Publique a branch e uma tag correspondente, como `v1.2.1`.
4. Aguarde o workflow e confira os arquivos na release.

## Alterações no protocolo

Leia [PROTOCOLO-USB.md](PROTOCOLO-USB.md) e acrescente exemplos de bytes aos testes antes de alterar a gravação. Distingua o que foi observado no programa, o que foi enviado com sucesso e o que foi confirmado fisicamente. Um identificador USB compartilhado não basta para anunciar compatibilidade com outro firmware.
