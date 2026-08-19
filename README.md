# Vox — assistente de voz (pt-BR)

Assistente de voz local para Windows (WPF/.NET 10). Segure uma tecla global (ou diga "Ei Vox") e fale em português para abrir apps/sites, controlar o PC e mais. Reconhecimento e síntese são offline (Windows Speech + Piper TTS).

## Funcionalidades

- **Atalhos por voz**: "abra o youtube", "abra o visual studio code", "abra o youtube em coldplay paradise" (busca com query)
- **Controle de mídia e volume real**: "pausa", "proxima faixa", "aumenta o volume", "volume 50", "mudo" (volume via Windows Core Audio, não teclas repetidas)
- **Utilitários**: "que horas são", "que dia é hoje", "calcule 15 vezes 3", "leia a área de transferência", "tire um print", "previsão do tempo", "feche o chrome", "minimize tudo"
- **Lembrete**: "me lembre em 5 minutos" (toast + voz; "vox cancela" para cancelar)
- **Busca universal configurável**: campo `searchTemplate` por binding (Spotify, Amazon, Wikipedia...)
- **Segurança**: dormir/hibernar/bloquear pedem confirmação (8s, "vox cancela")
- **Wake word opcional**: "Ei Vox ..." sem precisar segurar tecla (Configurações > Ouvir sempre)
- **Tema**: "tema escuro", "tema claro", "tema do sistema"
- **Histórico e exemplos clicáveis** na janela principal

## Requisitos

- Windows 10/11 com **pt-BR** habilitado em `Configurações > Hora e idioma > Idioma` (idioma de fala Windows instalado)
- Permissão de microfone e **Fala online** em `Configurações > Privacidade e segurança > Fala` (o app tenta ativar sozinho)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) para compilar
- `voice/piper.exe` + modelo `faber.onnx` (Piper TTS, voz pt-BR). Baixe em https://github.com/rhasspy/piper/releases — o app usa o `piper.exe`, dlls e `espeak-ng-data` da pasta `voice/` ao lado do exe.

## Compilar e executar

```powershell
# versão de desenvolvimento (Debug)
dotnet build Vox.csproj -c Debug

# publish Release em ./publish (poda espeak-ng-data p/ pt-BR)
.\build.ps1

# publish + instalar em %LOCALAPPDATA%\Vox + iniciar com o Windows
.\build.ps1 -Install
```

O `build.ps1` mantém seu `config.json` instalado (só copia se não existir).

## Configuração

`config.json` ao lado do exe (viaja com a pasta `publish/` entre computadores):

```jsonc
{
  "voice": { "enabled": true, "talkHotkey": "F9", "wakeWord": false },
  "appearance": { "theme": "system" },
  "hotkeys": [
    { "category": "site", "description": "Spotify", "target": "https://open.spotify.com/search/{q}", "searchTemplate": "https://open.spotify.com/search/{q}" },
    { "category": "app", "description": "Visual Studio Code", "modifiers": "Alt", "key": "V", "target": "%LOCALAPPDATA%\\Programs\\Microsoft VS Code\\Code.exe" }
  ]
}
```

- `searchTemplate` opcional: `{q}` é substituído pela query falada; sem ele, usa o Google como fallback.
- Comandos são editáveis pela janela do app (novo/editar/excluir), com hotkey global opcional.

## Testes

```powershell
dotnet test tests\Vox.Tests\Vox.Tests.csproj
```

Cobre o parser de comandos (verbos, volume, timer, clima, fechar app, fuzzy), calculadora e busca.

## CI

`.github/workflows/ci.yml` roda build + testes no Windows (GitHub Actions).

## Instalador

`installer.iss` gera um instalador com [Inno Setup](https://jrsoftware.org/isinfo.php) (ISCC):
`ISCC.exe installer.iss`

## Notas

- O Git ignora `bin/` e `obj/`; `publish/` e `voice/` são versionados de propósito (LFS) para levar o exe pronto para outro PC.
- Ações que exigem API externa (clima) usam https://wttr.in (sem chave); sem internet, responde "não consegui consultar".