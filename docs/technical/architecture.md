# Arquitetura Técnica — Avelri

**Produto:** Avelri
**Versão de referência:** 1.0.0+3
**Última atualização:** 04/09/2026

---

## 1. Objetivo

Este documento apresenta a arquitetura técnica geral da versão atual do Avelri.

Ele descreve:

- arquitetura cliente-servidor;
- organização do backend;
- dependências entre camadas;
- persistência;
- autenticação;
- autorização;
- multi-tenancy;
- fuso horário por academia;
- arquitetura Flutter;
- sessão;
- branding;
- ambientes;
- infraestrutura de Production;
- princípios para evolução.

Detalhes específicos estão documentados em:

- `docs/technical/database.md`
- `docs/technical/api.md`
- `docs/technical/mobile.md`
- `docs/operations/environments.md`
- `docs/operations/deployment.md`

---

# 2. Visão geral

O Avelri utiliza uma arquitetura cliente-servidor.

```text
┌─────────────────────────────────┐
│           Clientes              │
│                                 │
│      Flutter Android / Web      │
└────────────────┬────────────────┘
                 │
                 │ HTTPS / JSON
                 ▼
┌─────────────────────────────────┐
│       ASP.NET Core API          │
│                                 │
│ Autenticação                    │
│ Autorização                     │
│ Multi-tenancy                   │
│ Regras de negócio               │
│ Validações                      │
└────────────────┬────────────────┘
                 │
                 ▼
┌─────────────────────────────────┐
│       Application / Domain      │
└────────────────┬────────────────┘
                 │
                 ▼
┌─────────────────────────────────┐
│         Infrastructure          │
│                                 │
│ EF Core / Repositories / JWT    │
└────────────────┬────────────────┘
                 │
                 ▼
┌─────────────────────────────────┐
│          PostgreSQL             │
│                                 │
│       Supabase Production       │
└─────────────────────────────────┘
```

O cliente nunca acessa o PostgreSQL diretamente.

---

# 3. Estrutura da solução backend

Os projetos backend estão localizados em:

```text
src/
├── GymFlow.Api/
├── GymFlow.Application/
├── GymFlow.Domain/
└── GymFlow.Infrastructure/
```

Os nomes `GymFlow.*` permanecem como nomenclatura técnica interna e histórica do projeto.

A marca pública do produto é:

```text
Avelri
```

---

# 4. Direção das dependências

A arquitetura segue separação por responsabilidades.

Visão simplificada:

```text
GymFlow.Api
    │
    ├──────────────► GymFlow.Application
    │
    └──────────────► GymFlow.Infrastructure

GymFlow.Infrastructure
    │
    ├──────────────► GymFlow.Application
    └──────────────► GymFlow.Domain

GymFlow.Application
    │
    └──────────────► GymFlow.Domain

GymFlow.Domain
    │
    └── não depende de infraestrutura ou apresentação
```

O objetivo é manter regras centrais desacopladas de detalhes externos como:

- banco;
- HTTP;
- Fly.io;
- Supabase;
- Flutter.

---

# 5. GymFlow.Domain

A camada Domain contém as entidades centrais do sistema.

Entre elas estão:

```text
User
Student
Exercise

WorkoutTemplate
WorkoutTemplateDay
WorkoutTemplateExercise

Workout
WorkoutDay
WorkoutExercise
WorkoutExecution

PhysicalAssessment
```

Também contém os perfis:

```text
Admin
Professor
Student
```

A camada não deve conhecer:

- Controllers;
- Entity Framework Core;
- Flutter;
- infraestrutura de hospedagem.

---

# 6. GymFlow.Application

A camada Application contém os casos de uso e contratos utilizados pela aplicação.

Responsabilidades incluem:

- Services;
- DTOs e contratos de entrada/saída;
- interfaces de Repository;
- interfaces de segurança;
- interfaces relacionadas a tempo;
- validações;
- regras de aplicação;
- coordenação dos casos de uso.

Exemplos de responsabilidades:

```text
AuthenticationService
StudentService
ExerciseService
WorkoutTemplateService
WorkoutService
PhysicalAssessmentService
```

---

# 7. GymFlow.Infrastructure

A camada Infrastructure implementa detalhes técnicos exigidos pela Application.

Entre suas responsabilidades estão:

```text
AppDbContext
Entity Framework Core
Repositories
PostgreSQL
PasswordHasher
JwtTokenService
ConfigurationGymTimeZoneProvider
```

Fluxo típico:

```text
Application Service
        │
        ▼
Repository Interface
        │
        ▼
Repository Implementation
        │
        ▼
Entity Framework Core
        │
        ▼
PostgreSQL
```

---

# 8. GymFlow.Api

A camada API é a fronteira HTTP do backend.

Ela contém:

- Controllers;
- `Program.cs`;
- autenticação JWT;
- autorização;
- CORS;
- rate limiting;
- tratamento global de exceções;
- OpenAPI/Swagger em Development;
- endpoint de health;
- configuração da aplicação.

Controllers atuais:

```text
AuthController
StudentsController
ExercisesController
WorkoutTemplatesController
WorkoutsController
PhysicalAssessmentsController
MyPhysicalAssessmentsController
```

---

# 9. Injeção de dependências

A inicialização registra separadamente:

```text
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
```

Essa divisão concentra os registros de cada camada em seus respectivos projetos.

Posteriormente a API configura recursos específicos de HTTP e segurança.

---

# 10. Fluxo de uma requisição

Exemplo de consulta ao treino atual do Aluno:

```text
Flutter
   │
   │ GET /api/workouts/me/current
   │ Authorization: Bearer <JWT>
   ▼
ASP.NET Core
   │
   ▼
JWT Authentication
   │
   ▼
WorkoutsController
   │
   ▼
WorkoutService
   │
   ▼
WorkoutRepository
   │
   ▼
Entity Framework Core
   │
   ▼
PostgreSQL
   │
   ▼
resultado
   │
   ▼
JSON
   │
   ▼
Flutter
```

Controllers não devem concentrar regras complexas de domínio.

---

# 11. Autenticação

O backend utiliza:

```text
JWT Bearer
```

Após login válido, é gerado um token contendo:

```text
UserId
Name
Email
Role
gym_id
```

As requisições protegidas utilizam:

```http
Authorization: Bearer <JWT>
```

---

# 12. Validação do token

A configuração atual valida:

```text
Issuer
Audience
Lifetime
Signing Key
```

Além da validação criptográfica, após um token ser aceito o backend também confirma:

```text
UserId
+
GymId
+
IsActive
```

no banco.

Assim, a inativação de um usuário impede o uso posterior de um JWT anteriormente emitido.

---

# 13. Autorização

A autorização definitiva ocorre no backend.

Exemplo:

```text
POST /api/students
→ Admin
```

```text
POST /api/workouts
→ Admin + Professor
```

```text
GET /api/workouts/me/current
→ Student
```

O Flutter também restringe menus e navegação conforme o perfil, mas essa camada existe principalmente para:

- experiência;
- consistência;
- redução de navegação indevida.

Ela não é considerada fronteira de segurança.

---

# 14. Multi-tenancy

O Avelri utiliza multi-tenancy lógico através de:

```text
GymId
```

Na versão atual não existe:

```text
Gym
```

como entidade ou tabela persistida.

`GymId` funciona como identificador lógico do tenant.

---

# 15. Entidades com GymId direto

Algumas entidades possuem `GymId` diretamente:

```text
User
Exercise
WorkoutTemplate
Workout
```

Exemplo:

```text
Exercise
 └── GymId
```

Repositories utilizam esse valor para restringir consultas ao tenant autenticado.

---

# 16. Multi-tenancy por relacionamento

Outras entidades obtêm o tenant indiretamente.

## Student

```text
Student
   │
   ▼
User
   │
   ▼
GymId
```

## PhysicalAssessment

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

## WorkoutExecution

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

Esses caminhos são utilizados pelos Repositories para aplicar isolamento.

---

# 17. Origem do GymId autenticado

Em operações protegidas, o contexto da academia vem do claim:

```text
gym_id
```

do JWT.

O cliente não escolhe arbitrariamente o `GymId` da operação.

Exemplo:

```text
Admin autenticado
      │
      ▼
gym_id do JWT
      │
      ▼
POST /api/auth/register
      │
      ▼
novo Professor
      │
      ▼
mesmo GymId
```

---

# 18. Garantia multi-tenant

O isolamento é garantido principalmente por:

```text
Autenticação
      +
Application Services
      +
Repositories
      +
filtros GymId
```

O banco possui várias constraints de integridade, mas não existe uma tabela central `Gym` com foreign keys capaz de garantir sozinha todas as combinações multi-tenant.

Portanto, as validações no backend são parte essencial da arquitetura de segurança.

---

# 19. Banco de dados

O sistema utiliza:

```text
PostgreSQL
```

com acesso por:

```text
Entity Framework Core
```

Em Production:

```text
Supabase
```

é utilizado como infraestrutura PostgreSQL.

O backend utiliza conexão direta ao banco.

Não utiliza a Data API/PostgREST como caminho funcional da aplicação.

---

# 20. Migrations

Mudanças estruturais do banco são controladas através de:

```text
Entity Framework Core Migrations
```

Fluxo esperado:

```text
Entidade / Configuration
        │
        ▼
Migration
        │
        ▼
Revisão
        │
        ▼
Testes
        │
        ▼
Aplicação
        │
        ▼
__EFMigrationsHistory
```

Detalhes completos:

```text
docs/technical/database.md
```

---

# 21. Integridade e versionamento de treinos

O banco e a camada de aplicação preservam histórico de treino.

Existe a regra:

```text
no máximo um Workout ativo por Student
```

Quando um treino que já possui execuções é editado:

```text
treino antigo
IsActive = false

      │
      ▼

novo treino
IsActive = true
```

As execuções permanecem relacionadas à versão histórica anterior.

Isso evita que alterações futuras modifiquem retroativamente o histórico.

---

# 22. Fuso horário por academia

Funcionalidades dependentes de calendário não utilizam simplesmente a data UTC do servidor.

A configuração é obtida através de:

```text
GymTimeZones:{GymId}
```

O provider atual é:

```text
ConfigurationGymTimeZoneProvider
```

Ele suporta resolução do timezone configurado e conversão entre identificadores IANA e Windows quando necessário.

---

# 23. Funcionalidades dependentes do timezone

O timezone da academia é utilizado em regras como:

```text
ExecutionDate
CompletedToday
AssessmentDate
NextAssessmentDate
IsReassessmentDue
```

Exemplo:

```text
UTC atual
   │
   ▼
timezone da academia
   │
   ▼
data local da academia
   │
   ▼
WorkoutExecution.ExecutionDate
```

A ausência de configuração impede corretamente operações que necessitam dessa informação.

---

# 24. Rate limiting

O endpoint de login possui proteção por IP.

Regra atual:

```text
10 tentativas
por janela fixa de 1 minuto
```

Quando o limite é excedido:

```text
429 Too Many Requests
```

Em execução no Fly.io, a API utiliza:

```text
Fly-Client-IP
```

para identificar o cliente.

---

# 25. Tratamento de exceções

A API registra:

```text
GlobalExceptionHandler
```

e:

```text
ProblemDetails
```

para tratamento centralizado de falhas.

O objetivo é evitar exposição de:

- stack traces;
- connection strings;
- secrets;
- detalhes internos desnecessários.

---

# 26. Health e documentação da API

Health público:

```http
GET /health
```

Resposta:

```json
{
  "status": "ok"
}
```

OpenAPI e Swagger são disponibilizados somente em:

```text
Development
```

Em Production não são mapeados pelo `Program.cs`.

---

# 27. CORS

O backend utiliza política de CORS baseada em:

```text
Cors:AllowedOrigins
```

Em Development também são aceitos:

```text
localhost
127.0.0.1
```

Em outros ambientes somente origens explicitamente configuradas são aceitas.

CORS protege a comunicação do navegador, mas não substitui autenticação ou autorização.

---

# 28. Arquitetura Flutter

O cliente está localizado em:

```text
mobile/
```

Estrutura principal:

```text
mobile/lib/
├── app/
├── core/
├── features/
└── main.dart
```

---

# 29. app no Flutter

A pasta:

```text
app/
```

possui componentes globais.

Estrutura atual inclui:

```text
app/
├── config/
├── router/
├── session/
└── theme/
```

Responsabilidades:

- ambiente;
- navegação;
- sessão;
- perfis;
- tema;
- branding.

---

# 30. core no Flutter

A pasta:

```text
core/
```

possui infraestrutura compartilhada.

Entre os componentes atuais estão:

```text
network/
storage/
```

Com recursos como:

```text
ApiClient
ApiException
TokenStorage
```

---

# 31. features no Flutter

As principais features são:

```text
features/
├── auth/
├── exercises/
├── home/
├── physical_assessments/
├── students/
├── workout_templates/
└── workouts/
```

Cada feature pode utilizar:

```text
data/
models/
presentation/
widgets/
```

---

# 32. Gerenciamento de estado

O cliente utiliza principalmente:

```text
Provider
+
ChangeNotifier
```

Fluxo comum:

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
API
```

ViewModels controlam estados como:

```text
loading
data
error
submitting
refresh
```

---

# 33. ApiClient

A comunicação HTTP do Flutter é centralizada em:

```text
ApiClient
```

Ele utiliza o package:

```text
http
```

e implementa:

```text
GET
POST
PUT
PATCH
```

O timeout padrão é:

```text
15 segundos
```

Requisições autenticadas adicionam automaticamente o JWT.

---

# 34. Sessão Flutter

A sessão é controlada por:

```text
SessionController
```

Estados:

```text
unknown
unauthenticated
authenticated
```

O JWT é persistido através de:

```text
flutter_secure_storage
```

utilizando:

```text
TokenStorage
```

---

# 35. Bootstrap da sessão

Fluxo de inicialização:

```text
ler token seguro
     │
     ├── inexistente
     │      ▼
     │   login
     │
     └── existente
            │
            ▼
        GET /api/auth/me
            │
            ├── válido
            │     ▼
            │ sessão restaurada
            │
            └── 401
                  ▼
             token removido
```

---

# 36. Tratamento automático de 401

Quando uma requisição autenticada recebe:

```text
401 Unauthorized
```

o cliente:

```text
remove JWT do ApiClient
        │
        ▼
remove JWT do secure storage
        │
        ▼
remove SessionUser
        │
        ▼
unauthenticated
        │
        ▼
/login
```

---

# 37. Navegação Flutter

O cliente utiliza:

```text
GoRouter
```

Rota inicial:

```text
/bootstrap
```

Árvores principais:

```text
/admin
/professor
/student
```

O Router observa:

```text
SessionController
BrandingController
```

---

# 38. Restrição de navegação por perfil

O Router restringe cada sessão à sua árvore.

```text
Admin
→ /admin...

Professor
→ /professor...

Student
→ /student...
```

Se um usuário autenticado tentar navegar para uma árvore incompatível, o Router o redireciona para a Home correspondente.

Essa proteção continua sendo complementar à autorização do backend.

---

# 39. Configuração do cliente

O Flutter exige:

```text
APP_ENV
API_BASE_URL
```

Valores aceitos para ambiente:

```text
development
production
```

Em Production:

```text
API_BASE_URL
```

deve utilizar HTTPS.

---

# 40. Branding

O cliente possui:

```text
BrandingController
```

e:

```text
GymBranding
```

O branding pode definir:

- nome;
- logo;
- cores;
- background;
- surface;
- light/dark mode.

---

# 41. Branding atual por tenant

Na arquitetura atual, a resolução ocorre através de:

```text
LocalBrandingRepository
```

Fluxo:

```text
SessionUser.gymId
      │
      ▼
LocalBrandingRepository
      │
      ├── configuração existente
      │        ▼
      │ branding da academia
      │
      └── não existente
               ▼
       branding padrão Avelri
```

Portanto, a arquitetura suporta branding por tenant, mas a fonte atual de configuração é local ao aplicativo.

Não existe atualmente serviço remoto de branding.

---

# 42. Infraestrutura de Production

A arquitetura atual de Production utiliza:

```text
Android
   │
   ▼
Google Play
   │
   ▼
Flutter App
   │
   │ HTTPS
   ▼
Fly.io
ASP.NET Core API
   │
   ▼
Supabase
PostgreSQL
```

Para Web:

```text
Browser
   │
   ▼
Cloudflare Pages
   │
   ▼
Flutter Web
   │
   │ HTTPS
   ▼
Fly.io API
   │
   ▼
Supabase PostgreSQL
```

---

# 43. Responsabilidades da infraestrutura

## Google Play

Responsável pela distribuição Android.

## Cloudflare Pages

Responsável pela publicação da aplicação Web.

## Fly.io

Responsável pelo runtime da API em Production.

## Supabase

Responsável pela infraestrutura PostgreSQL utilizada em Production.

Nenhum desses serviços substitui as regras de autorização e multi-tenancy implementadas no backend.

---

# 44. Development

Durante Development, podem ser utilizados:

```text
API local
PostgreSQL configurado para desenvolvimento
Flutter local
Android Emulator
Browser
```

O Flutter pode utilizar `--dart-define` para apontar para a API apropriada.

Em emulador Android, acesso à máquina host pode exigir endereço apropriado, como:

```text
10.0.2.2
```

conforme o ambiente utilizado.

---

# 45. Production

Production utiliza configuração externa para:

```text
ConnectionStrings:DefaultConnection
Jwt:Key
Jwt:Issuer
Jwt:Audience
Jwt:ExpirationMinutes
Cors:AllowedOrigins
GymTimeZones:{GymId}
```

No Flutter:

```text
APP_ENV
API_BASE_URL
```

Secrets nunca devem ser versionados no repositório.

---

# 46. Testes backend

Os testes backend estão localizados sob:

```text
tests/
```

incluindo projetos como:

```text
GymFlow.Application.Tests
GymFlow.IntegrationTests
```

Comando:

```powershell
dotnet test
```

A cobertura atual inclui áreas como:

- Services;
- autorização;
- multi-tenancy;
- repositories;
- persistência;
- regras de negócio;
- integração.

---

# 47. Testes Flutter

Comandos principais:

```powershell
flutter test
```

e:

```powershell
flutter analyze
```

A homologação manual complementa os testes automatizados em:

- Android;
- Web;
- dispositivo físico;
- fluxos por perfil.

---

# 48. Segurança arquitetural

A segurança utiliza múltiplas camadas:

```text
HTTPS
  +
JWT
  +
validação de usuário ativo
  +
autorização por Role
  +
gym_id
  +
Services
  +
Repositories tenant-scoped
  +
constraints do banco
```

Nenhuma dessas camadas isoladamente deve ser tratada como suficiente para todos os casos.

---

# 49. Limitações arquiteturais atuais

Algumas decisões da versão atual são deliberadamente simples e poderão evoluir.

## Gym não persistido

Não existe tabela `Gym`.

`GymId` funciona como identificador lógico.

## Branding local

As configurações específicas de branding estão compiladas no cliente.

## API sem versão explícita

As rotas utilizam:

```text
/api/...
```

e não:

```text
/api/v1/...
```

## Conclusão por dia

A persistência atual registra conclusão de `WorkoutDay`, não de cada exercício individual.

## Exclusão definitiva

Não existem endpoints gerais de DELETE e a futura política de exclusão de alunos exigirá tratamento dos históricos existentes.

## Router

Foi identificada duplicação de algumas rotas de Professor relacionadas à criação de treino. Essa duplicação deve ser eliminada em manutenção futura.

---

# 50. Princípios para evolução

## Separação de responsabilidades

Controllers, Services, Repositories e UI devem continuar com responsabilidades distintas.

## Backend como fronteira de segurança

Permissões nunca devem depender exclusivamente do Flutter.

## Multi-tenancy explícito

Toda nova funcionalidade deve definir como o tenant será determinado e validado.

## Preservação de histórico

Evoluções de treino e execução não devem destruir dados históricos existentes.

## Configuração externa

Secrets e parâmetros de infraestrutura não devem ser incorporados ao código-fonte.

## Compatibilidade

Alterações de API devem considerar versões anteriores do aplicativo instaladas.

## Testabilidade

Novas regras importantes devem possuir testes automatizados sempre que viável.

## Evolução incremental

Mudanças devem evitar redesign ou reestruturação desnecessária de áreas já estáveis.

---

# 51. Documentos relacionados

- `docs/product/overview.md`
- `docs/product/business-rules.md`
- `docs/product/roles-and-permissions.md`
- `docs/technical/database.md`
- `docs/technical/api.md`
- `docs/technical/mobile.md`
- `docs/qa/homologation-guide.md`
- `docs/qa/test-scenarios.md`
- `docs/operations/environments.md`
- `docs/operations/deployment.md`
