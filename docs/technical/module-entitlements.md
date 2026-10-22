# Autorização de módulos — issue #121, G6

Classificação comercial confirmada pelo usuário em 19/10/2026. Papel e contratação
são controles distintos: ser Admin não habilita ERP. A issue #130 continua
bloqueando a implantação em Production.

## Produtos

App: autenticação, identidade digital/convites, alunos/professores, exercícios,
modelos, treinos, execuções, avaliações e configurações compartilhadas. Não exige
planos, matrículas ou cobranças ERP.

Por decisão expressa do usuário, check-ins/frequência, catracas/acesso físico,
relatórios cadastrais/operacionais e indicadores técnicos do dashboard também são
compartilhados. Seus controles de papel, credencial, bloqueio, atividade,
arquivamento e academia continuam valendo. `PhysicalAccessContracted` no onboarding
não é sinônimo de `ErpEnabled` nem ganhou bloqueio comercial implícito.

ERP: planos financeiros, matrículas, cobranças, confirmação de pagamentos,
relatórios `financial`/`enrollments`, importação/migração, histórico financeiro de
aluno e reativação com contratação de plano. Apenas essas ações financeiras do
controller de alunos exigem ERP; o CRUD tradicional permanece compartilhado.

Por segunda decisão expressa do usuário, os metadados opcionais de matrícula
(`EnrollmentStatus`, `PlanName`, `EnrollmentEndDate`) ficam nulos sem ERP e para
Professor. Somente um Admin do próprio tenant com entitlement ERP
pode recebê-los. A lista
não consulta matrículas nesse caso. O relatório cadastral JSON/CSV conserva apenas
ID, nome e situação cadastral, sem colunas de contratos ou dependência financeira.
Filtros de matrícula sem ERP recebem 403 para não revelar contratos através da
contagem ou seleção de alunos. Essa restrição não impede o CRUD compartilhado.

## Backend

`ErpEnabledFilter` protege Plans, Enrollments, Charges, Migrations, StudentImports,
dashboard financeiro e as duas ações financeiras de Students. `ErpReportFilter`
delega ao mesmo filtro apenas nos kinds financeiros e de matrículas, incluindo
exportação. O entitlement usa exclusivamente o GymId autenticado.

`GET /api/dashboard` conserva seu contrato financeiro para tenants ERP.
`GET /api/dashboard/operational` é uma projeção independente de alunos e avaliações:
não consulta autorização ERP, matrículas ou cobranças, nem inventa zeros financeiros.

401 indica autenticação ausente/inválida, 403 indica papel/entitlement insuficiente
e 404 preserva o isolamento de recursos quando aplicável. Erros inesperados de
resolução continuam 500 genérico, sem executar a operação ERP; não são convertidos
indiscriminadamente em 403. As leituras técnicas independentes continuam disponíveis.
Repositórios financeiros validam Student.GymId e Plan.GymId.

Somente o enriquecimento opcional de cadastros tem fallback explícito em falha da
política: registra o erro e omite metadados ERP, preservando a leitura cadastral.
Não transforma exceções de endpoints financeiros em sucesso ou 403 genérico.

GymId vazio é rejeitado no JWT. Usuários legados não precisam de linha de onboarding
Gym: o usuário/agente precisa estar ativo e pertencer à academia assinada no token.
Parâmetros de requisição não substituem essa identidade.

Configuração ausente/false não habilita ERP; apenas true explícito habilita um
GymId válido. A política atual é um snapshot imutável por processo; mudar configuração
exige reinício controlado da API. Configuração malformada continua falhando na
inicialização conforme os contratos existentes. Não se transforma erro em false
silencioso, que poderia retirar restrições financeiras de um tenant ERP. Esse
procedimento deve constar do rollout da #130.

## Desktop Windows

O backend é autoridade por requisição. O Desktop isola permissões por revisão de
sessão, descarta respostas antigas e reconsulta ao retomar o app e periodicamente.
403 de endpoint ERP fecha suas ações; 403 operacional não muda a contratação.
Rotas financeiras são protegidas além da ocultação de menus. Dashboard técnico e
relatórios compartilhados continuam utilizáveis sem ERP.

JWT usa flutter_secure_storage com leitura compatível do armazenamento Windows
legado. Não há persistência de senha ou refresh token pelo Desktop. Antes de
excluir o JWT, o arquivo independente e não secreto `session.signed-out` registra
a intenção de sair. Sua presença, inclusive arquivo vazio, bloqueia restauração;
falha de leitura não concede acesso. Só novo login concluído pode remover o
marcador. Operações de persistência são serializadas.

Se secure delete falhar e o marcador persistir, a próxima instância não restaura
o JWT remanescente. Se marcador e delete falharem juntos, não existe garantia
durável local: a memória é limpa e a interface informa a falha e permite tentar
novamente. Isso não equivale a revogação no servidor. Não há criptografia própria.

Testes usam credenciais sintéticas, secure storage simulado e diretórios temporários.
Build não substitui validação real de Credential Manager, upgrade e ACL do perfil.
O Access Agent mantém DPAPI LocalMachine combinado à ACL restrita de ProgramData
aplicada pelo instalador; nenhuma instalação ou credencial real foi alterada.
