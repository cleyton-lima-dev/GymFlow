# Modelos oficiais de importação do Avelri Gestão

No **Motor de migração**, selecione o tipo de lote e clique em **Baixar modelo**. O arquivo CSV baixado contém uma linha inteiramente fictícia: substitua-a pelos dados da academia ou remova-a antes de gerar a prévia. O modelo não é uma exigência para importar: arquivos CSV e XLSX de outros sistemas continuam aceitos com mapeamento automático ou manual.

Os quatro arquivos oficiais também estão versionados em `desktop/assets/import_templates/`: `modelo-alunos.csv`, `modelo-planos.csv`, `modelo-matriculas.csv` e `modelo-situacao-financeira.csv`. Cada arquivo usa apenas os campos que o tipo de lote aceita hoje.

## Preenchimento

1. Salve CSV em UTF-8, com a primeira linha de cabeçalhos e até 5.000 registros. O assistente aceita também XLSX e limita o arquivo a 5 MB. Não inclua fórmulas, senhas ou biometria.
2. Use um `externalId` não vazio e único por tipo de registro e sistema de origem. Conserve o mesmo ID em reprocessamentos; ele é sensível a maiúsculas/minúsculas. Mantenha o mesmo **Sistema de origem** nas sessões que precisam reutilizar vínculos.
3. Prepare os lotes nesta ordem: **Alunos → Planos → Matrículas e cobrança atual → Situação financeira atual**. Confirme os lotes relacionados antes de preparar a situação financeira. Os IDs relacionados devem coincidir exatamente com os `externalId` dos arquivos anteriores ou com vínculos já liberados na mesma academia e origem.
4. Confira mapeamento, prévia, duplicidades, erros e decisões de cada linha. Apenas a liberação explícita do Admin coloca os registros em operação; uma falha na liberação reverte a transação.

| Modelo | Campos e regras de preenchimento |
| --- | --- |
| Alunos | `externalId` e `name` são obrigatórios. `email`, `phone` e `birthDate` são opcionais; data em `AAAA-MM-DD`. A importação não cria conta digital nem senha. |
| Planos | `externalId`, `name`, `price`, `durationMonths` e `billingCycle`. Preço positivo com até duas casas decimais; duração inteira positiva; ciclo `mensal`, `trimestral`, `semestral` ou `anual`. |
| Matrículas e cobrança atual | `externalId`, `studentExternalId`, `planExternalId`, `name`, `price`, `durationMonths`, `billingCycle`, `startDate`, `endDate`, `chargeState`, `chargeDueDate`. Os IDs relacionados, datas inicial/final e termos contratuais devem ser conferidos; data final posterior à inicial. `chargeState` aceita `Paid`, `Pending`, `Unverified` ou vazio. `chargeDueDate` é opcional. Se termos contratuais não forem informados, o importador pode herdá-los do plano confirmado. |
| Situação financeira atual | `externalId`, `studentExternalId`, `enrollmentExternalId`, `chargeExternalId`, `chargeState`. Informe o aluno e a matrícula **ou** uma cobrança previamente vinculada. `chargeExternalId` pode ficar vazio quando `enrollmentExternalId` identifica a matrícula. `chargeState` é obrigatório: `Paid`, `Pending` ou `Unverified`. Divergências com a matrícula exigem revisão explícita. |

Use ponto decimal nos exemplos CSV (`129.90`); o importador também reconhece vírgula decimal quando o arquivo estiver corretamente delimitado. `Paid` representa uma situação trazida do sistema anterior, não um recebimento registrado no Avelri nem receita nova. Nunca substitua um pagamento real por um estado importado. Os exemplos oficiais usam `Pending` e não representam clientes ou cobranças reais.
