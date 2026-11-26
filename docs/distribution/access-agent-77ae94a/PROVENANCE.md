# AccessAgent 77ae94a — candidato interno da Issue #69

Reconstrução controlada concluída em 26/11/2026, para desenvolvimento e teste interno. O ZIP original de 24/11/2026 permanece intacto. Nenhum binário, registro privado de aprovação, log de máquina ou arquivo de termos acompanha esta documentação.

| Evidência | Valor |
| --- | --- |
| Snapshot Git | `77ae94a280554ae4006c2c611a54df322e5a94d0`, com o código e os scripts exportados sem alterações do worktree |
| SHA-256 do snapshot exportado | `7F41B210298EC8030E056CDF9AA78CD4F2A9CA83D3B018980F97CA5EC638E1B0` |
| SDK .NET GA | `10.0.402`, fixado sem roll-forward nem prévia |
| SHA-256 do manifesto do SDK instalado | `BAF6E3DA429E42DCC23B221062B58EFC5DE223419ED808FC418179B8AA043FD9`, 3.757 arquivos |
| Publicação | `Release`, `win-x64`, autocontido, runtime `.NET 10.0.15`. Restore offline com 42 entradas e lockfile. Publicação sem novo restore, código de saída 0. |
| SHA-256 do binlog | `4CA5180227A92DCB5FECC52D2F9D471F3AB13D2BF0F010BD006C17B20762F36F` |
| ZIP candidato | `Avelri-AccessAgent-77ae94a-win-x64-INTERNAL-CANDIDATE.zip`, 39.218.345 bytes |
| SHA-256 do candidato | `3AC10913AE226E5410FDE8FC260D990031771FAB2DAC8B90894973FF9BDB6D50` |
| SHA-256 do manifesto interno | `6AA5D415DB659178E365BFE70A7AD10EAA4E3B194B35B69365209B6B2C8382F7`, 253 arquivos verificados no ZIP |
| SHA-256 do ZIP anterior | `4204750233E9AC59997D6BDD7134873AFC41CC20A791A8883C89B29D2A2176A2` |

O [procedimento de reconstrução](../AccessAgent-Controlled-Rebuild.md) define a seleção do snapshot, SDK e feed offline. A execução registrou o comando exato, a saída de `dotnet --info`, as versões e os hashes dos pacotes, o lockfile, `project.assets.json` e o binlog. Esses arquivos permanecem na pasta local de evidências da execução. As opções relevantes da publicação foram:

```text
dotnet publish <projeto> -c Release -r win-x64 --self-contained true --no-restore
  --disable-build-servers -p:PublishSingleFile=false -p:DebugType=None
  -p:DebugSymbols=false -p:RuntimeFrameworkVersion=10.0.15
  -p:DisableTransitiveFrameworkReferenceDownloads=true
  -o <saída-exclusiva> -bl:<binlog>
```

## Avisos que acompanham o ZIP

O [índice de avisos](THIRD-PARTY-NOTICES.txt) e o [inventário](ACCESSAGENT-LICENSE-INVENTORY.json) são cópias dos arquivos contidos na raiz do candidato. Os seis textos originais correspondem aos hashes do inventário. A cobertura inclui 36 pacotes MIT e quatro Apache-2.0. As versões `Toletus.LiteNet2 8.30.1` e `Toletus.SM25 8.6.1` estão identificadas com permissões não comprovadas. Os avisos não concedem autorização sobre essas bibliotecas.

O ZIP candidato passou na validação estática de composição e integridade. O PE é x64, `appsettings.Development.json` está ausente e a confirmação física permanece desativada por padrão. As seis DLLs Toletus, `coreclr.dll` e `e_sqlite3.dll` correspondem aos respectivos arquivos do ZIP anterior. A validação não executou o agente nem registrou serviços.

## Homologação com os payloads reais

O Desktop local 1.0.1+2 corresponde ao snapshot `f3a9cdf15cd1f850082e0a5f987f777fb3c3cc27`, compilado com Visual Studio Community Stable e MSVC `14.50.35717`. Seu executável tem SHA-256 `1E971CC7D2316237BDFC17A976CE0E390690F4A935BC5B63A813F5BE6EB0E7A6`. A identidade desse build Production e a do ZIP candidato foram conferidas localmente.

A instalação integrada real não foi executada. O fluxo Homologação existente empacota apenas Desktop com configuração sintética e também exige a atestação Microsoft para incluir o redistribuível. O fluxo Production inclui AccessAgent, exige a atestação e recompila o Desktop a partir de um snapshot. O harness de fixtures anteriormente homologado não é um fluxo para trocar os payloads por binários reais. Um Sandbox sem rede isola a execução, mas não oferece um caminho de empacotamento que preserve essas guardas.

O avanço técnico depende de um fluxo interno integrado autorizado que aceite os dois artefatos preexistentes, com elegibilidade documentada dos insumos e das ferramentas usadas. A autorização comercial continua condicionada separadamente ao atendimento da cláusula Microsoft 8.b, à atestação de redistribuição Microsoft e às permissões comerciais Toletus.
