# Relatórios e exportação — ERP Core #65

Módulo de leitura do Avelri Gestão. Os endpoints `GET /api/reports/{kind}` e
`GET /api/reports/{kind}/export` exigem Admin e usam exclusivamente o GymId
da identidade autenticada. Nenhuma consulta altera situações, pagamentos ou acesso.

| kind | Fonte e significado do período | Filtros específicos |
| --- | --- | --- |
| students | Cadastro atual, sem período; matrícula vigente exige Active, início ≤ hoje e fim > hoje | status: active/inactive/archived; enrollmentStatus: valid/none |
| financial | Charges, sem joins com auditoria ou presença | view: charges/payments/overdue/revenue; status: pending/overdue/paid/cancelled |
| enrollments | Vencimento do contrato, independentemente do início | status: active/future/expired/pending-payment/cancelled |
| attendance | CheckIns.OccurredAt | status: manual/physical |
| access-events | PhysicalAccessEvents.OccurredAt | status: allowed/denied |

Filtros comuns: `studentId`, `search` (nome, até 100 caracteres), `fromDate`
e `toDate` em yyyy-MM-dd nos relatórios com período. Ambos os limites são
inclusivos em datas locais; timestamps usam início inclusivo/fim exclusivo em UTC.
Sem período explícito, utiliza-se o mês atual da academia. O período máximo é
366 dias. Datas de vencimento são DateOnly, sem conversão artificial de fuso.

Em financeiro, charges e overdue filtram pelo vencimento; payments e revenue
filtram pelo pagamento efetivo e incluem somente cobranças Paid. Receita recebida
é PaidAmount, nunca Amount somado novamente ao pagamento. Líquido é
Amount − DiscountAmount; canceladas não entram no total líquido. Na visão por
vencimento, o recebido refere-se às cobranças selecionadas, ainda que o pagamento
tenha ocorrido em outro período. O texto da consulta informa essa distinção.
Pending com vencimento anterior a hoje é exibida como vencida, conforme o módulo
financeiro. Contrato Active terminado é exibido como expirado; Active ainda não
iniciado é apresentado separadamente como futuro, sem alterar o status persistido.

Frequência conta somente check-ins existentes e informa alunos distintos.
Allowed não cria presença. Check-in manual não prova passagem física. Confirmação
de passagem significa um CheckIn PhysicalAccess vinculado ao evento; sua exibição
não comprova homologação de equipamento nem implementação da captura automática.
IDs de credenciais, identificadores externos, segredos e biometria não são exportados.

Paginação: page ≥ 1, pageSize de 1 a 100, padrão 25. Exportação: todas as páginas
dos mesmos filtros, máximo 10.000 registros; excesso é rejeitado com HTTP 400,
sem truncamento silencioso. Ordenação estável inclui ID como desempate. Totais
financeiros abrangem todo o resultado filtrado, não apenas a página carregada.

CSV: UTF-8 com BOM, delimitador ponto e vírgula, decimais com vírgula,
datas brasileiras, cabeçalhos de timestamps com fuso, células entre aspas e aspas
internas duplicadas. Prefixos de fórmula são neutralizados para Excel. CSV contém
cabeçalho e linhas; totais da interface podem ser conferidos a partir dos registros.
Não há dependências de XLSX ou PDF. O Desktop salva em caminho local explícito,
pedindo confirmação antes de substituir arquivo existente.

Filtros alterados precisam ser consultados antes da exportação. O período efetivo
retornado pelo servidor é preservado na paginação/exportação, inclusive ao virar
o mês. Consultas e exportação são leituras atuais; mudanças legítimas na origem
entre requisições podem mudar resultados. Não existe snapshot histórico congelado.

Testes usam SQLite em memória e, por opt-in, um PostgreSQL recém-criado e
descartável com destino verificado. Não dependem de catraca, banco existente ou
Production. Nenhuma migration ou mudança de schema é necessária.
