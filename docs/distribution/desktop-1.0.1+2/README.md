# Inventário e avisos do Desktop 1.0.1+2

Documentação da pasta Release local aprovada em 25/11/2026, SourceCommit `f3a9cdf15cd1f850082e0a5f987f777fb3c3cc27`. Estes arquivos não constituem liberação comercial nem atestação de redistribuição. Não foram incorporados a instalador.

## Conteúdo e alcance

| Arquivo | Conteúdo e origem |
| --- | --- |
| inventory.json | Todos os 22 arquivos distribuíveis do diretório Release com tamanho/hash da evidência aprovada; associação às dependências nativas, assets e insumos de compilação; versões, revisão do engine e índice de todos os avisos incorporados. |
| THIRD-PARTY-NOTICES.txt | Descompressão exata dos bytes de `data/flutter_assets/NOTICES.Z` do build aprovado, sem reescrever os textos originais: 1.626 blocos de avisos/licenças. |
| COMPONENT-LICENSES.txt | Textos originais dos arquivos LICENSE/NOTICE dos componentes observados, mais a atribuição original do cabeçalho Android JNI e a licença do SDK Dart. Os rótulos de origem são adições de navegação; os textos abaixo de cada rótulo foram conservados. |
| FLUTTER-ENGINE-LICENSE.txt | Arquivo original do engine `sky_engine/LICENSE` correspondente ao SDK usado, incluindo avisos de suas dependências nativas. |

O inventário de arquivos é exato para o diretório Release aprovado. Os 64 pacotes associados vêm do depfile do compilador, dos plugins nativos e dos assets efetivamente copiados. Isso não significa que todas as funções de todos esses pacotes sobreviveram ao tree shaking; implementações de outras plataformas podem aparecer nos insumos Dart sem produzir DLLs dessas plataformas. Os 93 pacotes meramente resolvidos, inclusive ferramentas de desenvolvimento, não foram tratados como se todos tivessem sido distribuídos no binário.

As dependências incorporadas ao engine, incluindo ICU/fontes e outras bibliotecas, são registradas pelos blocos originais de atribuição do fornecedor. Pacotes de código aberto têm termos próprios; não aplicar a eles indiscriminadamente as restrições propostas para o código proprietário Avelri ou para componentes Microsoft.

## Dependências externas e direitos pendentes

O diretório Flutter requer Windows compatível e as bibliotecas VC/UCRT indicadas na evidência nativa. O redistribuível Microsoft aprovado é pré-requisito externo a esta pasta Release; sua eventual inclusão em instalador continua condicionada aos termos finais aplicáveis. Não foi copiado/extraído/executado novamente para gerar esta documentação.

`dartjni.dll` está na saída aprovada e importa `jvm.dll`. Não há importação direta dessa DLL pelo EXE e o NativeAssetsManifest está vazio. Não se afirma necessidade de instalar Java, nem se inclui um JRE ou seus direitos de redistribuição. Esse ponto deve ser considerado ao preparar o pacote final, sem remover binários do build aprovado nesta etapa.

As imagens de branding existentes aparecem no inventário; nenhuma autorização nova de uso de marca/imagem foi presumida ou prestada. Dados de alunos, credenciais, binários de testes, caches e logs não fazem parte desta documentação.

Toletus e o ZIP AccessAgent não estão nesta pasta Flutter. O instalador integrado atual os adicionaria separadamente; portanto, estes avisos do Desktop não resolvem a pendência de autorização Toletus ou substituem os avisos do runtime .NET/AccessAgent. Essa autorização continua sob responsabilidade do fornecedor e do responsável pela distribuição.

## Reprodução da documentação

Usar o gerador local `scripts/desktop-installer/Export-DesktopDistributionNotices.mjs` com `--approved-run` apontando para a evidência aprovada e `--output` para pasta nova/vazia. O gerador usa apenas Node.js padrão, não compila, não baixa dependências e não sobrescreve uma documentação anterior. Os hashes dos textos originais e dos arquivos gerados estão em inventory.json.

Antes de futura distribuição, confirmar que o inventário corresponde exatamente ao novo artefato aprovado e incluir todos os avisos originais em forma legível no material entregue ao usuário. Se o conteúdo de um build posterior mudar, gerar outra documentação em pasta exclusiva; não reaproveitar o hash deste EXE como identidade de outro pacote.
