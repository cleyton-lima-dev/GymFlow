# Aceite local de licença no instalador integrado

Implementação de 25/11/2026 para revisão. A estratégia permanece um único instalador Desktop + AccessAgent. A minuta 1.0 não foi aprovada nem utilizada como contrato nos testes. A proposta anterior de famílias compatíveis de aceite é substituída pela regra abaixo, conforme a decisão atual do responsável.

## Identidade e fluxo implementados

A identidade é o par exato revisão + SHA-256 dos bytes do documento. AppVersion e BuildNumber não decidem o aceite. Mesma revisão com texto alterado exige nova manifestação; revisão diferente também não herda recibo. Não existe lista de hashes equivalentes nem opção de aceitar silenciosamente por parâmetro.

O `AvelriGestao-Production.iss` apresenta `LicenseFile` em português brasileiro, inicialmente sem aceitação. `LicenseAcceptance.iss` é a política compartilhada com o instalador sintético de teste. A página só é dispensada quando o usuário Windows atual possui recibo completo da revisão e do hash exatos.

A instalação silenciosa sem esse recibo para em `InitializeSetup`. No modo interativo, `NextButtonClick` exige seleção explícita na página da licença. `PrepareToInstall` verifica novamente a autorização antes de qualquer pré-requisito VC ou provisionamento AccessAgent. Recusa/cancelamento não grava aceite nem alcança essas ações. O Inno pode criar seus arquivos temporários e logs de execução; a garantia é não alterar o payload, os pré-requisitos e os serviços por falta de aceite.

O recibo é gravado somente em `ssDone`, após a instalação bem-sucedida. Atualização com termos idênticos preserva o recibo e a data anteriores. O marcador `Completed=1` é escrito por último: recibo incompleto não serve para dispensar aceite. Se a gravação falhar, a conclusão informa a falha e a próxima instalação exige nova manifestação; não se promete reversão automática de uma instalação já concluída.

## Registro e documentos locais

Chave por usuário Windows:

`HKCU\Software\Avelri\Gestao\Legal\Accepted\<revisão>\<SHA-256>`

Valores: `Revision`, `SHA256`, `AcceptedAtUtc` (UTC), `InstallerVersion`, `Language=pt-BR`, `Scope=CurrentWindowsUser` e `Completed`. Os recibos anteriores permanecem. Não há chave de ativação, identificação fiscal no registro, servidor ou licença remota.

São copiados para `{app}\legal`: `AVELRI-LICENSE.pt-BR.txt`, `THIRD-PARTY-NOTICES.txt`, `COMPONENT-LICENSES.txt`, `FLUTTER-ENGINE-LICENSE.txt` e `inventory.json`. Há atalhos para termos e avisos no grupo do aplicativo. Os três textos originais de terceiros preparados anteriormente permanecem inalterados. O arquivo de termos nessa pasta representa o documento atual; o histórico de recibos não é um arquivo imutável de todos os textos anteriores.

Registro HKCU é evidência operacional alterável pelo próprio usuário; relógio local e posse da conta não fornecem identidade absoluta ou não repúdio. O aceite do instalador não prova poderes para contratar por uma organização nem concordância de todos os usuários externos. A revisão humana deve definir a prova contratual correspondente ao alcance Microsoft 8.b(ii), acesso e retenção. Não foi implementado aceite no primeiro uso do Desktop nesta etapa.

## Bloqueio da minuta no empacotador

`DistributionLegal.psm1` exige documento privado de aprovação com schema 1, status `APPROVED` e booleanos verdadeiros para aprovação comercial do texto, identificação legal completa e validade técnica aprovada. Verifica revisão, idioma, hash e ausência de marcadores de minuta/identificação pendente. Confere os avisos pelo inventário; o builder exige inventário vinculado ao SourceCommit escolhido.

O padrão é `docs/distribution/legal-approval.DRAFT.json`, com todas as aprovações falsas. Não editá-lo para simular aprovação. Após revisão humana, o responsável deverá fornecer um JSON e texto final em diretório privado fora do repositório público; informar esse JSON por `-DistributionLegalApprovalPath`. Os caminhos relativos de termos e avisos são resolvidos em relação ao JSON; caminhos absolutos também são aceitos. O registro de aprovação contém apenas metadados, sem transcrever os dados legais privados. Aprovação do texto não representa atestação Microsoft ou autorização Toletus.

O próprio Inno inclui `ProductionLegalInputs.iss`: exige aprovação, confere o SHA-256 e rejeita o marcador `AVELRI_LEGAL_DRAFT_DO_NOT_DISTRIBUTE` mesmo com define de aprovação. Não há bypass de teste no script Production. Permanecem SourceCommit, snapshot Git, toolchain Stable/MSVC, pins e hash VC, atestação Microsoft, validação do ZIP integrado e Ed25519. Não foi executado o builder comercial nem fornecida atestação nesta etapa.

## Atualizações e preservação

| Situação | Resultado |
| --- | --- |
| Termos exatamente idênticos e recibo completo | Atualização sem novo aceite; mantém momento original. |
| Texto ou revisão alterados | Mostra o documento e exige novo aceite, mesmo com revisão numérica repetida. |
| Recibo ausente, incompleto ou de outra conta Windows | Não presume aceite; modo silencioso aborta. |
| Recusa/cancelamento antes de instalar | Preserva payload e não executa pré-requisitos ou ações sobre serviços. |
| Novo aceite explícito e instalação concluída | Grava novo recibo, conservando o histórico anterior. |

O atualizador conserva seu contrato existente: confirmação, Ed25519/tamanho/hash e lançamento interativo do Inno. Não foram alterados manifesto, chaves, arquitetura ou ciclo do serviço AccessAgent. A rotina existente de pré-requisitos/agente é idêntica à anterior, acrescida somente da barreira inicial de aceite.

## Evidência e pendências

A execução funcional usou três instaladores sintéticos, a mesma política Pascal e as cinco entradas legais do Production, sem Desktop real, VC ou serviço. Passaram instalação inicial, atualização idêntica silenciosa, bloqueio silencioso de termos alterados, recusa pela interface, novo aceite e rejeição de recibo incompleto. O compilador também verificou, em fixtures que terminam deliberadamente com `#error` e nunca geram executável, o caminho válido e os bloqueios de hash/minuta. Relatório: `artifacts/issue69/license-acceptance-2026-11-25.md`.

Permanecem revisão jurídica final, identificação privada, aprovação do prazo técnico de 12 meses/data inicial, adequação Microsoft 8.b, contrato comercial/política próprios e permissões/avisos finais do AccessAgent, especialmente Toletus.LiteNet2 8.30.1 e Toletus.SM25 8.6.1. Os avisos Desktop não substituem os avisos do runtime .NET/AccessAgent. Proveniência do pacote AccessAgent continua a pendência do checkpoint; nenhuma investigação ou reconstrução foi retomada.

## Fontes

[LicenseFile](https://jrsoftware.org/inno-setup/documentation/topic_setup_licensefile.htm) e [eventos do Inno](https://jrsoftware.org/inno-setup/documentation/topic_scriptevents.htm) documentam os mecanismos usados. A minuta mantém a informação de validade técnica e suporte para revisão conforme [Lei 9.609/1998, arts. 7 e 8](https://www.planalto.gov.br/ccivil_03/leis/l9609.htm), o acesso prévio e compreensão conforme [CDC](https://www.planalto.gov.br/ccivil_03/leis/l8078compilado.htm) e a separação da transparência de dados conforme [LGPD](https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13709.htm). Nenhuma aprovação jurídica é presumida.

Nota de ferramenta, sem novo bloqueio artificial: o ISCC 6.7.3 mostra “Non-commercial use only”, mas a [licença original](https://jrsoftware.org/files/is/license.txt) instalada permite aplicações comerciais sob suas condições e o [FAQ oficial](https://jrsoftware.org/inno-setup/purchase) informa que a compra não é estritamente obrigatória. Não foi adquirida licença, aceito contrato ou criado custo.
