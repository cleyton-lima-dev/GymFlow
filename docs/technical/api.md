# API — Avelri

**Produto:** Avelri
**Versão de referência:** 1.0.0+3
**Última atualização:** 04/09/2026
**Tecnologia:** ASP.NET Core
**Autenticação:** JWT Bearer

---

## 1. Objetivo

Este documento descreve a API atualmente utilizada pelo Avelri.

Ele registra:

- arquitetura de comunicação;
- autenticação;
- autorização;
- claims JWT;
- multi-tenancy;
- Controllers;
- endpoints implementados;
- perfis permitidos;
- paginação e filtros;
- códigos HTTP;
- segurança;
- integração com Flutter e Web.

A relação de endpoints apresentada aqui foi conferida diretamente com os Controllers da versão atual.

---

# 2. Papel da API

A API é o único ponto de acesso funcional dos clientes Avelri ao banco de dados.

Fluxo:

```text
Flutter Android / Web
        │
        │ HTTPS + JSON
        ▼
ASP.NET Core API
        │
        ├── autenticação
        ├── autorização
        ├── multi-tenancy
        ├── validações
        └── regras de negócio
        │
        ▼
Application
        │
        ▼
Infrastructure
        │
        ▼
Entity Framework Core
        │
        ▼
PostgreSQL / Supabase
```

O Flutter não acessa diretamente:

- PostgreSQL;
- Supabase Data API;
- PostgREST.

---

# 3. Formato de comunicação

O formato predominante utilizado é:

```text
application/json
```

Requisições protegidas utilizam:

```http
Authorization: Bearer <JWT>
```

Exemplo:

```http
GET /api/workouts/me/current
Authorization: Bearer <JWT>
```

Tokens e dados reais nunca devem aparecer em documentação, Issues ou logs públicos.

---

# 4. HTTPS

Em Production, a API deve ser consumida através de HTTPS.

O aplicativo também valida sua configuração de Production e rejeita uma `API_BASE_URL` que não utilize:

```text
https
```

Na infraestrutura Fly.io, a terminação HTTPS é realizada pela própria plataforma.

---

# 5. Autenticação

O Avelri utiliza JWT Bearer.

Fluxo:

```text
e-mail + senha
     │
     ▼
POST /api/auth/login
     │
     ▼
validação do usuário
     │
     ▼
validação da senha
     │
     ▼
JWT
     │
     ▼
cliente armazena token
     │
     ▼
requisições protegidas
```

Usuários inativos não conseguem realizar login.

---

# 6. Claims do JWT

O JWT gerado atualmente contém:

```text
ClaimTypes.NameIdentifier
ClaimTypes.Name
ClaimTypes.Email
ClaimTypes.Role
gym_id
```

Correspondendo conceitualmente a:

```text
UserId
Name
Email
Role
GymId
```

O `gym_id` é utilizado como parte do contexto multi-tenant.

O cliente não escolhe arbitrariamente o tenant durante operações protegidas.

---

# 7. Validação do JWT

A API valida:

```text
Issuer
Audience
Lifetime
Signing Key
```

A chave configurada em:

```text
Jwt:Key
```

deve possuir pelo menos:

```text
32 bytes
```

Também são obrigatórias:

```text
Jwt:Issuer
Jwt:Audience
Jwt:ExpirationMinutes
```

`Jwt:ExpirationMinutes` deve ser um inteiro maior que zero.

---

# 8. Verificação do usuário durante a sessão

A validade do JWT não depende apenas de sua assinatura e expiração.

Após a validação do token, a API também verifica se:

```text
UserId
+
GymId
```

correspondem a um usuário existente e ativo.

Caso o usuário tenha sido inativado após a emissão do token, requisições posteriores autenticadas são rejeitadas.

---

# 9. Perfis

Os perfis utilizados são:

```text
Admin
Professor
Student
```

A autorização é aplicada nos endpoints por meio do backend.

As regras completas estão em:

```text
docs/product/roles-and-permissions.md
```

Ocultar funcionalidades no Flutter não substitui a proteção realizada pela API.

---

# 10. Multi-tenancy

O isolamento entre academias é baseado em:

```text
gym_id
```

obtido do token autenticado.

Fluxo:

```text
requisição
   │
   ▼
JWT
   │
   ▼
gym_id
   │
   ▼
Controller
   │
   ▼
Application Service
   │
   ▼
Repository
   │
   ▼
consulta limitada ao tenant
```

---

# 11. Recursos sem GymId direto

Nem todas as entidades possuem `GymId`.

Exemplo:

```text
PhysicalAssessment
       │
       ▼
Student
       │
       ▼
User
       │
       ▼
GymId
```

Outro exemplo:

```text
WorkoutExecution
       │
       ▼
WorkoutDay
       │
       ▼
Workout
       │
       ▼
GymId
```

Nesses casos, o tenant é validado através dos relacionamentos.

---

# 12. Controllers atuais

A API possui atualmente os seguintes Controllers:

```text
AuthController
ExercisesController
StudentsController
WorkoutTemplatesController
WorkoutsController
PhysicalAssessmentsController
MyPhysicalAssessmentsController
```

---

# 13. Autenticação

Base:

```text
/api/auth
```

## 13.1 Login

```http
POST /api/auth/login
```

**Autenticação:** pública.

Request conceitual:

```json
{
  "email": "usuario@exemplo.com",
  "password": "<PASSWORD>"
}
```

Em caso de sucesso, a resposta contém:

```text
UserId
GymId
Name
Email
Role
Token
```

Principais respostas:

```text
200 OK
401 Unauthorized
429 Too Many Requests
```

Credenciais inválidas e usuário inativo não revelam informações específicas sobre qual parte falhou.

---

## 13.2 Rate limiting do login

O login possui limite por IP:

```text
10 requisições
por janela fixa de 1 minuto
```

Não existe fila de espera.

Quando excedido:

```text
429 Too Many Requests
```

Resposta atual:

```json
{
  "message": "Muitas tentativas de login. Tente novamente em instantes."
}
```

---

## 13.3 Cadastro de Professor

```http
POST /api/auth/register
```

**Perfil:** `Admin`.

O `GymId` não é recebido livremente do cliente.

Ele é obtido do:

```text
gym_id
```

do JWT do Administrador.

O usuário criado recebe:

```text
Role = Professor
IsActive = true
```

Principais respostas:

```text
201 Created
400 Bad Request
409 Conflict
401 Unauthorized
403 Forbidden
```

---

## 13.4 Listar Professores

```http
GET /api/auth/professors
```

**Perfil:** `Admin`.

Retorna somente Professores do `GymId` autenticado.

Resposta conceitual:

```text
Id
Name
Email
IsActive
CreatedAt
```

---

## 13.5 Usuário autenticado

```http
GET /api/auth/me
```

**Perfil:** qualquer usuário autenticado.

Retorna:

```text
userId
gymId
name
email
role
```

O endpoint é utilizado pelo Flutter para reconstruir uma sessão já armazenada no dispositivo.

---

## 13.6 Endpoint administrativo de diagnóstico

```http
GET /api/auth/admin
```

**Perfil:** `Admin`.

Esse endpoint existe como recurso protegido de validação administrativa.

Não representa uma funcionalidade principal do produto.

---

# 14. Alunos

Base:

```text
/api/students
```

---

## 14.1 Cadastrar Aluno

```http
POST /api/students
```

**Perfil:** `Admin`.

Principais respostas:

```text
201 Created
400 Bad Request
409 Conflict
```

O aluno criado pertence automaticamente à mesma academia do Admin autenticado.

---

## 14.2 Listar Alunos

```http
GET /api/students
```

**Perfis:**

```text
Admin
Professor
```

Query parameters:

```text
search
isActive
page
pageSize
```

Valores padrão:

```text
page = 1
pageSize = 20
```

`pageSize` deve estar entre:

```text
1 e 100
```

A busca atual considera:

```text
nome
e-mail
telefone
```

---

## 14.3 Consultar Aluno

```http
GET /api/students/{id}
```

**Perfis:**

```text
Admin
Professor
```

A consulta valida também o tenant.

Principais respostas:

```text
200 OK
404 Not Found
```

---

## 14.4 Editar Aluno

```http
PUT /api/students/{id}
```

**Perfil:** `Admin`.

Principais respostas:

```text
204 No Content
400 Bad Request
404 Not Found
409 Conflict
```

O Professor não possui permissão para editar dados cadastrais de aluno.

---

## 14.5 Alterar status do Aluno

```http
PATCH /api/students/{id}/status
```

**Perfil:** `Admin`.

Request conceitual:

```json
{
  "isActive": false
}
```

Principais respostas:

```text
204 No Content
404 Not Found
```

---

## 14.6 Próprio cadastro do Aluno

```http
GET /api/students/me
```

**Perfil:** `Student`.

O backend identifica o aluno através de:

```text
UserId
+
GymId
```

presentes no contexto autenticado.

O aluno não envia seu próprio `StudentId`.

---

# 15. Exercícios

Base:

```text
/api/exercises
```

O Controller é destinado a:

```text
Admin
Professor
```

com exceção da alteração de status, que é exclusiva do Admin.

---

## 15.1 Criar Exercício

```http
POST /api/exercises
```

**Perfis:**

```text
Admin
Professor
```

Principais respostas:

```text
201 Created
400 Bad Request
409 Conflict
```

---

## 15.2 Listar Exercícios

```http
GET /api/exercises
```

**Perfis:**

```text
Admin
Professor
```

Query parameters:

```text
search
muscleGroup
isActive
page
pageSize
```

Padrões:

```text
page = 1
pageSize = 20
```

---

## 15.3 Comportamento da busca

O parâmetro:

```text
search
```

pesquisa atualmente somente:

```text
Exercise.Name
```

através de comparação `ILIKE`.

O parâmetro:

```text
muscleGroup
```

é um filtro separado.

A API atual não aplica normalização específica para remover acentos.

Portanto:

```text
biceps
```

não é garantido como equivalente a:

```text
bíceps
```

Esse comportamento está registrado como bug conhecido.

---

## 15.4 Editar Exercício

```http
PUT /api/exercises/{id}
```

**Perfis:**

```text
Admin
Professor
```

Resposta de sucesso:

```text
204 No Content
```

Também podem ocorrer erros de validação ou conflito conforme o caso.

---

## 15.5 Alterar status do Exercício

```http
PATCH /api/exercises/{id}/status
```

**Perfil:** `Admin`.

O Professor pode criar e editar exercícios, mas não pode ativá-los ou desativá-los.

Resposta de sucesso:

```text
204 No Content
```

---

## 15.6 Ausência de GET individual

Na versão atual não existe endpoint específico:

```text
GET /api/exercises/{id}
```

A interface utiliza os dados provenientes da listagem ao abrir o fluxo de edição.

---

# 16. Modelos de treino

Base:

```text
/api/workout-templates
```

**Perfis do Controller:**

```text
Admin
Professor
```

---

## 16.1 Criar modelo

```http
POST /api/workout-templates
```

Principais respostas:

```text
201 Created
400 Bad Request
409 Conflict
```

---

## 16.2 Listar modelos

```http
GET /api/workout-templates
```

Query parameters:

```text
search
isActive
page
pageSize
```

Padrões:

```text
page = 1
pageSize = 20
```

A busca textual é realizada pelo nome do modelo.

---

## 16.3 Consultar modelo

```http
GET /api/workout-templates/{id}
```

Principais respostas:

```text
200 OK
404 Not Found
```

---

## 16.4 Editar modelo

```http
PUT /api/workout-templates/{id}
```

Principais respostas:

```text
204 No Content
400 Bad Request
404 Not Found
409 Conflict
```

---

## 16.5 Alterar status do modelo

```http
PATCH /api/workout-templates/{id}/status
```

**Perfis:**

```text
Admin
Professor
```

Essa regra é diferente da alteração de status de exercício.

Professor pode ativar e desativar modelos.

---

# 17. Treinos

Base:

```text
/api/workouts
```

---

## 17.1 Criar treino manual

```http
POST /api/workouts
```

**Perfis:**

```text
Admin
Professor
```

O aluno deve:

- existir;
- pertencer à mesma academia;
- estar ativo.

Os exercícios utilizados também devem pertencer à academia e estar ativos.

Resposta de sucesso:

```text
201 Created
```

---

## 17.2 Criar treino a partir de modelo

```http
POST /api/workouts/from-template
```

**Perfis:**

```text
Admin
Professor
```

O modelo precisa:

- existir;
- pertencer à academia;
- estar ativo.

O treino criado mantém referência opcional ao modelo através de:

```text
SourceWorkoutTemplateId
```

Resposta de sucesso:

```text
201 Created
```

---

## 17.3 Substituição do treino ativo

Ao criar novo treino para um aluno que já possui treino ativo, a camada de aplicação:

```text
treino atual
IsActive = false

      │
      ▼

novo treino
IsActive = true
```

O backend já suporta essa substituição.

A limitação existente na versão em homologação está principalmente no fluxo da interface de edição, que não oferece diretamente troca por outro modelo.

---

## 17.4 Editar treino

```http
PUT /api/workouts/{id}
```

**Perfis:**

```text
Admin
Professor
```

Resposta de sucesso:

```text
200 OK
```

Somente treino ativo pode ser editado.

Quando o treino já possui execuções, a atualização cria uma nova versão para preservar o histórico.

---

## 17.5 Treino atual de um aluno

```http
GET /api/workouts/students/{studentId}/current
```

**Perfis:**

```text
Admin
Professor
```

O aluno e o treino são validados dentro do tenant autenticado.

Principais respostas:

```text
200 OK
404 Not Found
```

---

## 17.6 Próprio treino atual

```http
GET /api/workouts/me/current
```

**Perfil:** `Student`.

Não recebe `StudentId`.

A API localiza o aluno através do usuário autenticado.

---

# 18. Execução de treino

A unidade de execução persistida atualmente é:

```text
WorkoutDay
```

Não existe conclusão persistida individual por exercício na versão atual.

---

## 18.1 Concluir um dia

```http
POST /api/workouts/me/days/{workoutDayId}/complete
```

**Perfil:** `Student`.

Request:

```json
{}
```

Para concluir:

- o aluno precisa estar ativo;
- o dia deve pertencer ao treino ativo do aluno;
- o treino deve pertencer à mesma academia.

A API calcula a data utilizando o fuso horário da academia.

---

## 18.2 Duplicidade

O mesmo dia não pode ser concluído duas vezes na mesma data local.

A regra é equivalente a:

```text
WorkoutDayId
+
ExecutionDate
```

Quando já existe conclusão correspondente, a operação é rejeitada.

---

## 18.3 Resposta de execução

A resposta inclui informações como:

```text
Id
WorkoutDayId
CompletedAt
CompletedAtUtcOffsetMinutes
```

`CompletedAt` representa o instante UTC.

O offset permite ao cliente apresentar corretamente o horário da academia.

---

# 19. Histórico de treino

## 19.1 Histórico do próprio aluno

```http
GET /api/workouts/me/history
```

**Perfil:** `Student`.

Query parameters:

```text
page
pageSize
```

---

## 19.2 Histórico de aluno específico

```http
GET /api/workouts/students/{studentId}/history
```

**Perfis:**

```text
Admin
Professor
```

O histórico inclui execuções ligadas a versões antigas de treinos.

A substituição do treino atual não apaga o histórico anterior.

---

# 20. Avaliações físicas — Admin e Professor

Base:

```text
/api/students/{studentId}/physical-assessments
```

**Perfis:**

```text
Admin
Professor
```

---

## 20.1 Criar avaliação

```http
POST /api/students/{studentId}/physical-assessments
```

Principais respostas:

```text
201 Created
400 Bad Request
404 Not Found
409 Conflict
```

Existe conflito quando já há avaliação para:

```text
StudentId
+
AssessmentDate
```

---

## 20.2 Data atual da academia

```http
GET /api/students/{studentId}/physical-assessments/current-date
```

Retorna a data atual segundo o fuso configurado para a academia autenticada.

Esse endpoint é utilizado pelo cliente durante o fluxo de cadastro de avaliação física.

---

## 20.3 Avaliação mais recente

```http
GET /api/students/{studentId}/physical-assessments/latest
```

Principais respostas:

```text
200 OK
404 Not Found
```

---

## 20.4 Histórico

```http
GET /api/students/{studentId}/physical-assessments
```

Query parameters:

```text
page
pageSize
```

---

## 20.5 Detalhes

```http
GET /api/students/{studentId}/physical-assessments/{assessmentId}
```

A consulta valida simultaneamente:

```text
AssessmentId
StudentId
GymId
```

---

## 20.6 Ausência de edição e exclusão

Na versão atual não existem endpoints para:

```text
PUT PhysicalAssessment
PATCH PhysicalAssessment
DELETE PhysicalAssessment
```

Avaliações registradas são somente criadas e consultadas pelos fluxos atuais.

---

# 21. Avaliações físicas — próprio Aluno

Base:

```text
/api/physical-assessments/me
```

**Perfil:** `Student`.

---

## 21.1 Avaliação mais recente

```http
GET /api/physical-assessments/me/latest
```

---

## 21.2 Histórico

```http
GET /api/physical-assessments/me
```

Query parameters:

```text
page
pageSize
```

---

## 21.3 Detalhes

```http
GET /api/physical-assessments/me/{assessmentId}
```

O aluno não fornece `StudentId`.

Seu registro é resolvido através de:

```text
UserId
+
GymId
```

do contexto autenticado.

---

# 22. Paginação

Os endpoints paginados utilizam:

```text
page
pageSize
```

A camada de aplicação valida:

```text
page >= 1

1 <= pageSize <= 100
```

Os valores padrão normalmente utilizados pelos Controllers e pelo cliente são:

```text
page = 1
pageSize = 20
```

Recursos paginados incluem:

- alunos;
- exercícios;
- modelos de treino;
- histórico de treino;
- histórico de avaliações físicas.

---

# 23. Resumo de endpoints

| Método | Endpoint | Perfil |
|---|---|---|
| POST | `/api/auth/login` | Público |
| POST | `/api/auth/register` | Admin |
| GET | `/api/auth/professors` | Admin |
| GET | `/api/auth/me` | Autenticado |
| GET | `/api/auth/admin` | Admin |
| POST | `/api/students` | Admin |
| GET | `/api/students` | Admin, Professor |
| GET | `/api/students/{id}` | Admin, Professor |
| PUT | `/api/students/{id}` | Admin |
| PATCH | `/api/students/{id}/status` | Admin |
| GET | `/api/students/me` | Student |
| POST | `/api/exercises` | Admin, Professor |
| GET | `/api/exercises` | Admin, Professor |
| PUT | `/api/exercises/{id}` | Admin, Professor |
| PATCH | `/api/exercises/{id}/status` | Admin |
| POST | `/api/workout-templates` | Admin, Professor |
| GET | `/api/workout-templates` | Admin, Professor |
| GET | `/api/workout-templates/{id}` | Admin, Professor |
| PUT | `/api/workout-templates/{id}` | Admin, Professor |
| PATCH | `/api/workout-templates/{id}/status` | Admin, Professor |
| POST | `/api/workouts` | Admin, Professor |
| POST | `/api/workouts/from-template` | Admin, Professor |
| PUT | `/api/workouts/{id}` | Admin, Professor |
| GET | `/api/workouts/students/{studentId}/current` | Admin, Professor |
| GET | `/api/workouts/students/{studentId}/history` | Admin, Professor |
| GET | `/api/workouts/me/current` | Student |
| GET | `/api/workouts/me/history` | Student |
| POST | `/api/workouts/me/days/{workoutDayId}/complete` | Student |
| POST | `/api/students/{studentId}/physical-assessments` | Admin, Professor |
| GET | `/api/students/{studentId}/physical-assessments/current-date` | Admin, Professor |
| GET | `/api/students/{studentId}/physical-assessments/latest` | Admin, Professor |
| GET | `/api/students/{studentId}/physical-assessments` | Admin, Professor |
| GET | `/api/students/{studentId}/physical-assessments/{assessmentId}` | Admin, Professor |
| GET | `/api/physical-assessments/me/latest` | Student |
| GET | `/api/physical-assessments/me` | Student |
| GET | `/api/physical-assessments/me/{assessmentId}` | Student |
| GET | `/health` | Público |

---

# 24. Ausência de DELETE endpoints

Nenhum dos Controllers funcionais atuais expõe operações:

```http
DELETE
```

para:

- alunos;
- professores;
- exercícios;
- modelos;
- treinos;
- avaliações físicas;
- execuções.

Quando existe estado ativo/inativo, a API utiliza operações específicas de status conforme as permissões disponíveis.

---

# 25. Códigos HTTP

## 200 OK

Utilizado para consultas e operações que retornam conteúdo.

---

## 201 Created

Utilizado em criação de recursos.

Exemplos:

```text
Professor
Aluno
Exercício
Modelo
Treino
Avaliação Física
```

---

## 204 No Content

Utilizado em operações concluídas sem corpo de resposta.

Exemplos:

```text
editar Aluno
alterar status de Aluno
editar Exercício
alterar status de Exercício
editar Modelo
alterar status de Modelo
```

---

## 400 Bad Request

Usado para erros de validação ou entrada inválida.

Exemplos:

- paginação inválida;
- campo obrigatório ausente;
- data inválida;
- valores de medida inválidos;
- estrutura de treino inválida.

---

## 401 Unauthorized

Indica ausência de autenticação válida.

Também pode ocorrer quando:

- token expirou;
- token é inválido;
- claims obrigatórias não são válidas;
- usuário foi inativado;
- usuário não existe mais no contexto informado pelo token.

---

## 403 Forbidden

Indica usuário autenticado sem o perfil necessário.

Exemplo:

```text
Professor
   │
   ▼
POST /api/students
   │
   ▼
403 Forbidden
```

---

## 404 Not Found

Indica recurso não encontrado dentro do contexto permitido.

O uso de filtros por tenant também evita retornar dados pertencentes a outra academia.

---

## 409 Conflict

Utilizado em conflitos como:

- e-mail já utilizado;
- duplicidade de avaliação na mesma data;
- determinadas violações de regras durante criação/edição;
- conclusão já registrada conforme o fluxo.

---

## 429 Too Many Requests

Utilizado quando o limite de login por IP é excedido.

---

## 500 Internal Server Error

Representa erro inesperado.

Informações internas sensíveis não devem ser expostas ao cliente.

---

# 26. Tratamento de exceções

A aplicação registra:

```text
GlobalExceptionHandler
```

e:

```text
ProblemDetails
```

para tratamento centralizado de exceções.

Erros conhecidos também são tratados diretamente pelos Controllers quando fazem parte do contrato da operação.

Stack traces, connection strings, secrets e detalhes internos não devem ser enviados ao cliente em Production.

---

# 27. Validação

A validação ocorre em mais de uma camada.

## Flutter

Pode validar antecipadamente:

- campos obrigatórios;
- formato;
- confirmação;
- limites básicos;
- experiência de preenchimento.

## API

Sempre deve validar novamente:

- autorização;
- tenant;
- campos;
- limites;
- regras de domínio;
- existência de recursos;
- status ativo/inativo.

O backend nunca confia exclusivamente na validação do cliente.

---

# 28. Senhas

Política atual:

```text
mínimo: 8 caracteres
máximo: 128 caracteres
```

O hash utiliza:

```text
Microsoft.AspNetCore.Identity.PasswordHasher
```

A senha original não é persistida.

---

# 29. E-mail

Antes da utilização para autenticação ou cadastro, a aplicação normaliza o e-mail com:

```text
Trim()
+
ToLowerInvariant()
```

A implementação atual utiliza unicidade global de e-mail.

---

# 30. CORS

A API lê as origens autorizadas através de:

```text
Cors:AllowedOrigins
```

A configuração aceita uma lista separada por vírgulas.

Em Development, também são aceitos hosts locais:

```text
localhost
127.0.0.1
```

Em ambientes que não sejam Development, somente origens explicitamente configuradas são aceitas.

O aplicativo Android nativo não depende de CORS da mesma forma que navegadores Web.

---

# 31. OpenAPI e Swagger

A aplicação registra OpenAPI e Swagger, porém seus endpoints são publicados somente quando:

```text
Environment = Development
```

Em Production:

```text
Swagger UI
→ não publicado
```

```text
OpenAPI endpoint
→ não publicado
```

Isso corresponde ao comportamento atual do `Program.cs`.

---

# 32. Health Check

Existe endpoint público:

```http
GET /health
```

Resposta:

```json
{
  "status": "ok"
}
```

Ele possui:

```text
AllowAnonymous
```

e pode ser utilizado para:

- validação pós-deploy;
- monitoramento;
- verificação rápida de disponibilidade.

Ele não valida todos os casos de uso da aplicação.

---

# 33. Configuração externa

Entre as configurações exigidas pela API estão:

```text
ConnectionStrings:DefaultConnection
Jwt:Key
Jwt:Issuer
Jwt:Audience
Jwt:ExpirationMinutes
Cors:AllowedOrigins
GymTimeZones:{GymId}
```

Quando configuradas por variável de ambiente .NET, `:` é normalmente representado por:

```text
__
```

Exemplo:

```text
Jwt__Key
```

Nenhum valor real deve ser incluído nesta documentação.

---

# 34. Fuso horário por academia

Operações dependentes do calendário local utilizam:

```text
GymTimeZones:{GymId}
```

A API utiliza essa informação em funcionalidades como:

- conclusão diária de treino;
- `CompletedToday`;
- avaliação física;
- reavaliação.

Caso não exista configuração correspondente, operações dependentes do fuso podem falhar com erro de configuração específico.

---

# 35. Logging de autenticação

A API registra eventos relevantes de autenticação e segurança.

Entre eles:

- login concluído;
- tentativa de login inválida;
- token com claims inválidas;
- token de usuário inativo ou inexistente;
- acionamento do rate limit.

Não devem ser registrados:

- senha;
- JWT completo;
- connection string;
- secrets.

---

# 36. IP do cliente no Fly.io

Quando a aplicação está executando no Fly.io, a API utiliza o header:

```text
Fly-Client-IP
```

para identificar o IP do cliente em funcionalidades como rate limiting e logging de login.

Fora do Fly.io, utiliza:

```text
HttpContext.Connection.RemoteIpAddress
```

---

# 37. Integração Flutter

O fluxo predominante no aplicativo é:

```text
Page
 │
 ▼
ViewModel
 │
 ▼
Service
 │
 ▼
ApiClient
 │
 ▼
HTTP
 │
 ▼
ASP.NET Core API
```

O Flutter não conhece detalhes do PostgreSQL.

---

# 38. ApiClient do Flutter

O `ApiClient` compartilhado:

- utiliza URL base configurável;
- envia `Accept: application/json`;
- envia `Content-Type: application/json`;
- adiciona `Authorization: Bearer` quando autenticado;
- implementa GET;
- implementa POST;
- implementa PUT;
- implementa PATCH;
- possui timeout padrão de 15 segundos;
- converte respostas não `2xx` em `ApiException`.

Não existe método `DELETE` no `ApiClient` atual porque a API funcional também não utiliza DELETE na versão atual.

---

# 39. Tratamento de 401 no Flutter

Quando uma requisição autenticada recebe:

```text
401 Unauthorized
```

o `ApiClient` chama o handler de sessão.

O `SessionController`:

```text
limpa token em memória
        │
        ▼
remove token do secure storage
        │
        ▼
remove usuário da sessão
        │
        ▼
SessionStatus.unauthenticated
```

O roteador então direciona o usuário novamente ao login.

---

# 40. Bootstrap da sessão

Na inicialização:

```text
Flutter Secure Storage
       │
       ▼
token encontrado?
       │
       ├── não
       │    ▼
       │  login
       │
       └── sim
            │
            ▼
       GET /api/auth/me
            │
            ├── válido
            │    ▼
            │ sessão restaurada
            │
            └── 401
                 ▼
              token apagado
```

---

# 41. Versionamento da API

As rotas atuais não possuem prefixo formal de versão.

Exemplo:

```text
/api/students
/api/workouts
/api/exercises
```

Não existe atualmente:

```text
/api/v1/...
```

Mudanças futuras incompatíveis deverão considerar uma estratégia formal de versionamento antes de quebrar clientes distribuídos.

---

# 42. Compatibilidade com clientes anteriores

Versões antigas do aplicativo podem permanecer instaladas após uma nova publicação da API.

Por isso, alterações futuras devem avaliar:

- remoção de endpoints;
- alteração de tipos;
- remoção de campos;
- novos campos obrigatórios;
- mudanças de semântica;
- necessidade de versionamento.

Evoluções compatíveis devem ser preferidas quando possível.

---

# 43. Segurança para novos endpoints

Todo novo endpoint deve definir explicitamente:

1. se exige autenticação;
2. quais perfis podem acessar;
3. como `GymId` será obtido;
4. como o vínculo com o tenant será validado;
5. quais campos entram;
6. quais campos podem sair;
7. quais regras de negócio devem ser aplicadas;
8. quais códigos HTTP são esperados;
9. se a operação exige atomicidade;
10. quais testes serão necessários.

---

# 44. Testes recomendados

A API deve continuar sendo testada em cenários como:

## Autenticação

```text
sem token
→ 401
```

```text
token inválido
→ 401
```

```text
usuário inativo com token anterior
→ 401
```

## Autorização

```text
Professor
→ endpoint exclusivo de Admin
→ 403
```

## Multi-tenancy

```text
Admin academia A
→ recurso academia B
→ não retorna o recurso
```

## Aluno

```text
Aluno A
→ endpoint /me
→ somente recursos do Aluno A
```

## Regras de domínio

```text
avaliação duplicada
→ conflito
```

```text
conclusão duplicada do mesmo dia na mesma data
→ conflito
```

```text
novo treino para aluno com treino ativo
→ versão anterior inativa
→ novo treino ativo
```

---

# 45. Documentos relacionados

- `docs/technical/architecture.md`
- `docs/technical/database.md`
- `docs/technical/mobile.md`
- `docs/product/business-rules.md`
- `docs/product/roles-and-permissions.md`
- `docs/qa/homologation-guide.md`
- `docs/qa/test-scenarios.md`
- `docs/operations/environments.md`
- `docs/operations/deployment.md`
