# Dashboard gerencial ERP — issue #62

Implementação de leitura em `feature/erp-dashboard`, baseada em `origin/main`.
Não integra os PRs #122/#123, altera autenticação, estados financeiros ou schema.

## Contrato e período

`GET /api/dashboard` exige JWT e role Admin. O GymId vem exclusivamente da claim
autenticada; nenhum parâmetro seleciona outra academia. Consultas financeiras
validam tanto `Enrollment.Student.User.GymId` quanto `Enrollment.Plan.GymId`.

O período financeiro é o mês corrente no timezone configurado da academia,
com início inclusivo e próximo mês exclusivo. Datas de pagamento UTC são
comparadas aos limites locais convertidos para UTC; vencimentos usam DateOnly.
`today`, `periodStart` e `periodEndExclusive` identificam a posição consultada.

| Indicador | Base de cálculo |
|---|---|
| Receita prevista | Soma Amount − DiscountAmount das cobranças não canceladas com vencimento no mês, incluindo as já pagas. Não projeta parcelas inexistentes. |
| Receita recebida | Soma PaidAmount das cobranças Paid com PaidAt no mês, independentemente do vencimento. Não soma Amount nem auditorias. |
| Cobranças vencidas | Status Overdue ou Pending com DueDate anterior a hoje, em todos os períodos; total líquido dos descontos registrados. No vencimento, Pending não está atrasada. |
| Matrículas vigentes | Active com StartDate ≤ hoje e EndDate > hoje, respeitando término exclusivo. Não inclui futura ou PendingPayment. |
| Matrículas a vencer | Subconjunto vigente com término até hoje + 15 dias, inclusive. |
| Alunos ativos | Student não arquivado com User.IsActive. |
| Sem avaliação | Alunos ativos sem avaliação física registrada. |
| Reavaliações pendentes | Alunos ativos cuja avaliação mais recente + 2 meses ≤ hoje, conforme regra existente, incluindo ajuste de fim de mês. |

Prevista e recebida usam datas diferentes e não são parcelas somáveis. Cancelar
matrícula paga não apaga o pagamento nem o retira da receita recebida. Não há
alteração de status, lançamento, matrícula, usuário ou auditoria pelo dashboard.

## Interface e limites

A Visão geral mantém a Home e os atalhos. O card de cobranças vencidas abre
`/finance?status=overdue`, aplicando o filtro existente. Os demais indicadores
não têm aparência de ação, pois não há listagem com filtro equivalente.

Carregamento, erro com retry, última consulta preservada e ausência de dados
são explícitos. Valores são agregados no servidor em três consultas, sem
carregamento de entidades nem N+1. As consultas não constituem um snapshot
transacional único: alterações concorrentes podem ocorrer entre indicadores.

Frequência e alunos ausentes permanecem indisponíveis: main não contém os
check-ins do PR #122. Eventos de autorização física não substituem presença.

## Validação

Testes automatizados cobrem agregações, descontos, datas, mês local, fim de mês,
relacionamentos entre tenants, número fixo de consultas, autorização HTTP com
JWT assinado, carregamento/erro/vazio e navegação com filtro. Os testes SQL usam
SQLite em memória; não equivalem a homologação PostgreSQL ou Windows manual.
Nenhuma migration, conexão a Production, ativação por academia ou publicação
é necessária ou autorizada por este documento.
