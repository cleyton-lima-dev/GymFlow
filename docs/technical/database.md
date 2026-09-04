# Banco de Dados — Avelri

**Produto:** Avelri
**Versão de referência:** 1.0.0+3
**Última atualização:** 04/09/2026
**SGBD:** PostgreSQL
**ORM:** Entity Framework Core

---

## 1. Objetivo

Este documento descreve a estrutura de persistência utilizada pela versão atual do Avelri.

O objetivo é registrar:

- tecnologia utilizada;
- entidades persistidas;
- relacionamentos;
- índices e constraints;
- regras de integridade;
- estratégia de multi-tenancy;
- migrations;
- comportamento de exclusão;
- segurança de acesso ao banco.

Este documento representa o modelo implementado e deve acompanhar alterações futuras do schema.

---

# 2. Tecnologia

O Avelri utiliza:

- PostgreSQL;
- Entity Framework Core;
- migrations do EF Core;
- Supabase como infraestrutura PostgreSQL em Production.

Fluxo principal:

```text
Flutter / Web
     │
     ▼
ASP.NET Core API
     │
     ▼
Entity Framework Core
     │
     ▼
PostgreSQL
     │
     ▼
Supabase
```

A API acessa diretamente o PostgreSQL.

O funcionamento principal do sistema não depende da Data API/PostgREST do Supabase.

O aplicativo Flutter nunca recebe credenciais diretas do banco.

---

# 3. DbContext

O contexto utilizado pela aplicação é:

```text
AppDbContext
```

Ele pertence à camada:

```text
GymFlow.Infrastructure
```

O `DbContext` expõe conjuntos para:

- `Users`;
- `Students`;
- `Exercises`;
- `WorkoutTemplates`;
- `WorkoutTemplateDays`;
- `WorkoutTemplateExercises`;
- `Workouts`;
- `WorkoutDays`;
- `WorkoutExercises`;
- `WorkoutExecutions`;
- `PhysicalAssessments`.

As configurações de entidades são aplicadas através das configurações do Entity Framework Core registradas na camada de infraestrutura.

---

# 4. Tabelas atuais

O schema possui atualmente as seguintes tabelas principais:

```text
Users
Students
Exercises

WorkoutTemplates
WorkoutTemplateDays
WorkoutTemplateExercises

Workouts
WorkoutDays
WorkoutExercises
WorkoutExecutions

PhysicalAssessments

__EFMigrationsHistory
```

`__EFMigrationsHistory` é uma tabela interna utilizada pelo Entity Framework Core para registrar migrations já aplicadas.

---

# 5. Ausência de tabela Gym

Na versão atual não existe entidade nem tabela `Gym`.

O tenant é identificado diretamente por:

```text
GymId
```

presente em determinadas entidades.

Portanto, `GymId` funciona atualmente como identificador lógico do tenant, mas não como chave estrangeira para uma tabela `Gyms`.

Exemplo:

```text
User
 └── GymId
```

Isso deve ser considerado em futuras evoluções do modelo caso academias passem a possuir cadastro próprio persistido no banco.

---

# 6. Users

A tabela `Users` representa todos os usuários autenticáveis.

Os perfis são diferenciados pelo campo `Role`.

## Principais campos

```text
Id
GymId
Name
Email
PasswordHash
Role
IsActive
CreatedAt
UpdatedAt
```

Perfis existentes:

```text
Admin
Professor
Student
```

Não existem tabelas separadas para Administradores e Professores.

Ambos são representados por registros em `Users`.

---

## 6.1 Restrições

Principais restrições:

```text
Name
→ obrigatório
→ máximo 150 caracteres

Email
→ obrigatório
→ máximo 200 caracteres

PasswordHash
→ obrigatório

Role
→ obrigatório

IsActive
→ obrigatório

CreatedAt
→ obrigatório
```

---

## 6.2 Unicidade de e-mail

Existe índice único sobre:

```text
Email
```

Portanto, na implementação atual, o e-mail é **globalmente único**.

A regra não é:

```text
UNIQUE (GymId, Email)
```

Ela é:

```text
UNIQUE (Email)
```

Consequentemente, duas academias diferentes não podem possuir usuários distintos utilizando o mesmo endereço de e-mail.

A aplicação também normaliza o e-mail para letras minúsculas antes da persistência.

---

# 7. Students

A tabela `Students` contém informações adicionais específicas do perfil Aluno.

Principais campos:

```text
Id
UserId
BirthDate
Phone
CreatedAt
UpdatedAt
```

O `Student` não possui `GymId`.

Sua academia é determinada através de:

```text
Student
   │
   ▼
User
   │
   ▼
GymId
```

---

## 7.1 Relação com User

A relação é um-para-um:

```text
User
 │
 └── Student
```

`UserId` possui constraint de unicidade.

Isso impede que o mesmo `User` esteja associado a múltiplos registros `Student`.

O relacionamento configurado é:

```text
User → Student
DeleteBehavior.Cascade
```

Portanto, caso um `User` seja removido diretamente do banco, seu registro `Student` relacionado também será removido.

Essa regra não significa que exista atualmente um endpoint da aplicação para exclusão de usuários.

---

## 7.2 Telefone

O campo:

```text
Phone
```

é opcional e possui limite máximo de:

```text
20 caracteres
```

---

# 8. Exercises

A tabela `Exercises` representa o catálogo de exercícios de cada academia.

Principais campos:

```text
Id
GymId
Name
MuscleGroup
Description
IsActive
CreatedAt
UpdatedAt
```

---

## 8.1 Limites

```text
Name
→ obrigatório
→ máximo 150 caracteres

MuscleGroup
→ obrigatório
→ máximo 100 caracteres

Description
→ opcional
→ máximo 500 caracteres
```

---

## 8.2 citext

`Exercise.Name` utiliza o tipo PostgreSQL:

```text
citext
```

Isso torna comparações de igualdade do nome insensíveis a diferenças entre letras maiúsculas e minúsculas.

Exemplo:

```text
Supino Reto
supino reto
SUPINO RETO
```

são equivalentes para a constraint de unicidade.

`citext` não torna automaticamente as comparações insensíveis a acentuação.

---

## 8.3 Índices

Existem:

```text
INDEX (GymId)

UNIQUE (GymId, Name)
```

Assim, duas academias podem possuir exercícios com o mesmo nome, mas uma mesma academia não pode possuir dois exercícios equivalentes pelo nome.

---

# 9. WorkoutTemplates

`WorkoutTemplates` representa modelos reutilizáveis de treino.

Principais campos:

```text
Id
GymId
Name
Description
IsActive
CreatedAt
UpdatedAt
```

---

## 9.1 Nome

O campo `Name`:

- é obrigatório;
- possui limite máximo de 150 caracteres;
- utiliza PostgreSQL `citext`.

Existe:

```text
UNIQUE (GymId, Name)
```

A unicidade é, portanto, por academia e insensível a diferenças somente de maiúsculas/minúsculas.

---

## 9.2 Índices

Existe índice para:

```text
GymId
```

além do índice único composto:

```text
GymId + Name
```

---

# 10. WorkoutTemplateDays

`WorkoutTemplateDays` representa as divisões/dias pertencentes a um modelo.

Principais campos:

```text
Id
WorkoutTemplateId
Name
Order
```

`Name`:

```text
obrigatório
máximo 100 caracteres
```

---

## 10.1 Ordenação

Existe constraint única:

```text
UNIQUE (WorkoutTemplateId, Order)
```

Portanto, dois dias pertencentes ao mesmo modelo não podem ocupar a mesma ordem.

Também existe índice em:

```text
WorkoutTemplateId
```

---

## 10.2 Exclusão

A relação entre modelo e dias utiliza:

```text
WorkoutTemplate
    │
    └── WorkoutTemplateDays

DeleteBehavior.Cascade
```

Quando um modelo é removido no nível de persistência, seus dias são dependentes do modelo.

Na versão atual, entretanto, não existe endpoint público para exclusão de modelos.

---

# 11. WorkoutTemplateExercises

`WorkoutTemplateExercises` representa a prescrição de exercícios dentro de um dia do modelo.

Principais campos:

```text
Id
WorkoutTemplateDayId
ExerciseId
Sets
Repetitions
RestSeconds
Notes
Order
```

---

## 11.1 Restrições

```text
Sets
→ obrigatório

Repetitions
→ obrigatório
→ máximo 50 caracteres

Notes
→ opcional
→ máximo 500 caracteres

Order
→ obrigatório
```

---

## 11.2 Índices

Existem índices em:

```text
WorkoutTemplateDayId
ExerciseId
```

e constraint:

```text
UNIQUE (WorkoutTemplateDayId, Order)
```

Assim, não podem existir dois exercícios ocupando a mesma posição dentro do mesmo dia do modelo.

---

## 11.3 Relação com Exercise

A relação com o catálogo de exercícios utiliza:

```text
Exercise
   │
   └── WorkoutTemplateExercise

DeleteBehavior.Restrict
```

Um exercício que esteja sendo referenciado por uma prescrição de modelo não pode ser simplesmente removido e deixar a referência órfã.

---

# 12. Workouts

`Workouts` representa os treinos efetivamente atribuídos aos alunos.

Principais campos:

```text
Id
StudentId
GymId
SourceWorkoutTemplateId
Name
Description
IsActive
CreatedAt
UpdatedAt
```

---

## 12.1 Relação com Student

Um treino pertence a um aluno.

A relação utiliza:

```text
Student
   │
   └── Workouts

DeleteBehavior.Restrict
```

Portanto, um aluno que possui treinos relacionados não pode ser simplesmente excluído fisicamente através de uma operação comum de cascade.

Essa regra é especialmente relevante para qualquer futura política de exclusão definitiva de alunos.

---

## 12.2 Índices

Existem índices em:

```text
GymId
StudentId
```

---

## 12.3 Um único treino ativo

Existe um índice único parcial baseado em:

```text
StudentId
```

com filtro equivalente a:

```text
IsActive = true
```

Isso garante no banco:

```text
no máximo um Workout ativo por Student
```

Treinos históricos inativos podem coexistir para o mesmo aluno.

---

# 13. Referência ao modelo de origem

Um treino criado a partir de um modelo pode possuir:

```text
SourceWorkoutTemplateId
```

Essa referência é opcional.

A relação utiliza:

```text
WorkoutTemplate
       │
       └── Workout.SourceWorkoutTemplateId

DeleteBehavior.SetNull
```

Assim, a remoção do modelo de origem não exige apagar o treino que já foi criado.

A referência pode ser transformada em `NULL`, preservando o treino do aluno.

---

# 14. WorkoutDays

`WorkoutDays` representa os dias pertencentes a um treino.

Principais campos:

```text
Id
WorkoutId
Name
Order
```

O nome possui limite máximo de:

```text
100 caracteres
```

---

## 14.1 Ordenação

Existe:

```text
INDEX (WorkoutId)

UNIQUE (WorkoutId, Order)
```

Portanto, dois dias do mesmo treino não podem possuir a mesma ordem.

---

## 14.2 Relação com Workout

A relação:

```text
Workout
   │
   └── WorkoutDays
```

está configurada com:

```text
DeleteBehavior.Cascade
```

Entretanto, a presença de execuções relacionadas aos dias pode impedir uma exclusão em cascata completa, conforme as regras de `WorkoutExecutions`.

---

# 15. WorkoutExercises

`WorkoutExercises` representa a prescrição de um exercício dentro de um dia real do treino.

Principais campos:

```text
Id
WorkoutDayId
ExerciseId
Sets
Repetitions
RestSeconds
Notes
Order
```

---

## 15.1 Restrições

```text
Sets
→ obrigatório

Repetitions
→ obrigatório
→ máximo 50 caracteres

Notes
→ opcional
→ máximo 500 caracteres

Order
→ obrigatório
```

---

## 15.2 Índices

Existem índices em:

```text
WorkoutDayId
ExerciseId
```

e constraint:

```text
UNIQUE (WorkoutDayId, Order)
```

Assim, a ordem dos exercícios deve ser única dentro de cada dia.

---

## 15.3 Relação com Exercise

A relação com `Exercise` utiliza:

```text
DeleteBehavior.Restrict
```

Um exercício utilizado por uma prescrição de treino não pode ser removido fisicamente deixando referências inválidas.

---

# 16. WorkoutExecutions

`WorkoutExecutions` registra a conclusão de um dia de treino.

A unidade persistida atualmente não é a conclusão individual de um exercício.

É a conclusão de:

```text
WorkoutDay
```

Principais campos:

```text
Id
WorkoutDayId
ExecutionDate
CompletedAt
```

---

## 16.1 ExecutionDate

`ExecutionDate` representa a data local da academia em que o dia foi concluído.

É diferente de:

```text
CompletedAt
```

que representa o instante da conclusão.

---

## 16.2 CompletedAt

`CompletedAt` é armazenado como data/hora da execução e é produzido pela aplicação a partir de:

```text
DateTime.UtcNow
```

A aplicação também calcula o offset do fuso da academia para apresentação correta ao cliente.

---

## 16.3 Unicidade

Existe:

```text
UNIQUE (WorkoutDayId, ExecutionDate)
```

Consequentemente, o mesmo dia de treino não pode possuir duas conclusões na mesma data local da academia.

---

## 16.4 Índices

Existem índices em:

```text
WorkoutDayId
CompletedAt
```

---

## 16.5 Relação com WorkoutDay

A relação entre `WorkoutDay` e `WorkoutExecution` utiliza:

```text
DeleteBehavior.Restrict
```

Assim, um dia que possui execuções históricas não pode ser removido de maneira que apague silenciosamente o histórico.

Essa regra também influencia tentativas futuras de exclusão física de treinos.

---

# 17. Versionamento de treinos

O modelo atual permite preservar versões históricas de treino.

Quando um treino ainda não possui execuções, uma edição pode modificar a estrutura existente.

Quando já existem execuções, a camada de aplicação preserva o treino anterior:

```text
Workout atual
IsActive = false

        │
        ▼

novo Workout
IsActive = true
```

As execuções continuam relacionadas aos dias da versão antiga.

Essa estratégia permite preservar o histórico sem alterar os registros utilizados em execuções anteriores.

O banco complementa essa regra garantindo que exista no máximo um treino ativo por aluno.

---

# 18. PhysicalAssessments

`PhysicalAssessments` representa avaliações físicas realizadas para os alunos.

Campos atuais:

```text
Id
StudentId
AssessmentDate

WeightKg
HeightCm
BodyFatPercentage

ChestCm
WaistCm
AbdomenCm
HipCm

RightArmCm
LeftArmCm

RightThighCm
LeftThighCm

RightCalfCm
LeftCalfCm

Notes

CreatedAt
UpdatedAt
```

---

## 18.1 Obrigatoriedade

São obrigatórios:

```text
StudentId
AssessmentDate
WeightKg
HeightCm
CreatedAt
UpdatedAt
```

São opcionais:

```text
BodyFatPercentage

ChestCm
WaistCm
AbdomenCm
HipCm

RightArmCm
LeftArmCm

RightThighCm
LeftThighCm

RightCalfCm
LeftCalfCm

Notes
```

---

## 18.2 Precisão decimal

Os campos numéricos de peso, altura, percentual de gordura e medidas corporais utilizam precisão:

```text
decimal(5,2)
```

A camada de aplicação limita valores compatíveis com essa representação.

---

## 18.3 Observações

`Notes` possui limite máximo de:

```text
500 caracteres
```

---

# 19. Unicidade de avaliação física

Existe constraint única:

```text
UNIQUE (StudentId, AssessmentDate)
```

Portanto, o mesmo aluno não pode possuir duas avaliações físicas na mesma data.

Essa regra é protegida em múltiplos níveis:

```text
Application
     +
Repository
     +
PostgreSQL unique constraint
```

A camada de infraestrutura também trata especificamente a violação da constraint para convertê-la em conflito de domínio apropriado.

---

# 20. Relação PhysicalAssessment → Student

A relação utiliza:

```text
Student
   │
   └── PhysicalAssessments

DeleteBehavior.Cascade
```

Entretanto, isso não significa que o aluno possa atualmente ser removido normalmente.

A relação:

```text
Student → Workouts
```

utiliza `Restrict`, o que impede uma exclusão física simples quando existem treinos associados.

---

# 21. Reavaliação física

Os seguintes valores não precisam ser armazenados como colunas:

```text
NextAssessmentDate
IsReassessmentDue
```

Eles são calculados pela aplicação.

Regra:

```text
NextAssessmentDate =
AssessmentDate.AddMonths(2)
```

E:

```text
IsReassessmentDue =
CurrentGymDate >= NextAssessmentDate
```

`CurrentGymDate` utiliza o fuso horário configurado para a academia.

---

# 22. Multi-tenancy

O Avelri utiliza isolamento lógico por `GymId`.

Algumas entidades possuem o identificador diretamente.

## Vínculo direto

```text
User
 └── GymId

Exercise
 └── GymId

WorkoutTemplate
 └── GymId

Workout
 └── GymId
```

## Vínculo indireto

```text
Student
   │
   ▼
User
   │
   ▼
GymId
```

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

---

# 23. Isolamento na camada de persistência

Os repositories aplicam filtros por academia nas principais consultas.

Exemplos conceituais:

```text
Exercise.GymId == gymId
```

```text
Workout.GymId == gymId
```

```text
WorkoutTemplate.GymId == gymId
```

```text
Student.User.GymId == gymId
```

```text
PhysicalAssessment.Student.User.GymId == gymId
```

```text
WorkoutExecution.WorkoutDay.Workout.GymId == gymId
```

Isso impede que simplesmente possuir o `Id` de um recurso seja suficiente para acessá-lo em outra academia.

---

# 24. Integridade multi-tenant

O banco não possui uma tabela central `Gym` nem uma foreign key capaz de garantir sozinho todas as relações de tenant.

Existem também relacionamentos em que duas entidades possuem vínculos que precisam pertencer à mesma academia, mas essa equivalência não é expressa por uma constraint composta no PostgreSQL.

Exemplo:

```text
Workout
 ├── StudentId
 └── GymId
```

A consistência:

```text
Workout.GymId
==
Student.User.GymId
```

é garantida pelos Services e Repositories durante os fluxos da aplicação.

O mesmo princípio se aplica à seleção de exercícios para modelos e treinos.

Por isso, a validação multi-tenant no backend é parte essencial da segurança do sistema.

---

# 25. Resumo de índices e unicidades

| Tabela | Índice / Constraint |
|---|---|
| `Users` | `UNIQUE (Email)` |
| `Students` | `UNIQUE (UserId)` |
| `Exercises` | `INDEX (GymId)` |
| `Exercises` | `UNIQUE (GymId, Name)` |
| `WorkoutTemplates` | `INDEX (GymId)` |
| `WorkoutTemplates` | `UNIQUE (GymId, Name)` |
| `WorkoutTemplateDays` | `INDEX (WorkoutTemplateId)` |
| `WorkoutTemplateDays` | `UNIQUE (WorkoutTemplateId, Order)` |
| `WorkoutTemplateExercises` | `INDEX (WorkoutTemplateDayId)` |
| `WorkoutTemplateExercises` | `INDEX (ExerciseId)` |
| `WorkoutTemplateExercises` | `UNIQUE (WorkoutTemplateDayId, Order)` |
| `Workouts` | `INDEX (GymId)` |
| `Workouts` | `INDEX (StudentId)` |
| `Workouts` | índice único parcial para um treino ativo por aluno |
| `WorkoutDays` | `INDEX (WorkoutId)` |
| `WorkoutDays` | `UNIQUE (WorkoutId, Order)` |
| `WorkoutExercises` | `INDEX (WorkoutDayId)` |
| `WorkoutExercises` | `INDEX (ExerciseId)` |
| `WorkoutExercises` | `UNIQUE (WorkoutDayId, Order)` |
| `WorkoutExecutions` | `INDEX (WorkoutDayId)` |
| `WorkoutExecutions` | `INDEX (CompletedAt)` |
| `WorkoutExecutions` | `UNIQUE (WorkoutDayId, ExecutionDate)` |
| `PhysicalAssessments` | `INDEX (StudentId)` |
| `PhysicalAssessments` | `UNIQUE (StudentId, AssessmentDate)` |

---

# 26. Resumo dos comportamentos de exclusão

Entre os comportamentos explicitamente configurados no modelo atual estão:

| Relação | Comportamento |
|---|---|
| `User → Student` | `Cascade` |
| `Student → Workouts` | `Restrict` |
| `Student → PhysicalAssessments` | `Cascade` |
| `Workout → WorkoutDays` | `Cascade` |
| `WorkoutDay → WorkoutExecutions` | `Restrict` |
| `WorkoutTemplate → WorkoutTemplateDays` | `Cascade` |
| `WorkoutTemplate → Workout.SourceWorkoutTemplateId` | `SetNull` |
| `Exercise → WorkoutExercise` | `Restrict` |
| `Exercise → WorkoutTemplateExercise` | `Restrict` |
| `WorkoutTemplateDay → WorkoutTemplateExercises` | `Cascade` |

Essas regras foram escolhidas de modo a evitar referências inválidas e, em alguns pontos, proteger dados históricos.

A aplicação atualmente não oferece operações gerais de exclusão física desses recursos.

---

# 27. Exclusão de alunos

A exclusão automática definitiva após período prolongado de inatividade está planejada para evolução futura.

Ela não existe no comportamento atual.

A implementação não poderá simplesmente executar:

```text
DELETE Student
```

quando existirem dados relacionados.

Isso ocorre principalmente porque:

```text
Student → Workouts
DeleteBehavior.Restrict
```

Além disso, treinos podem possuir dias e execuções históricas protegidas.

Portanto, a política futura deverá definir explicitamente:

- se os dados serão removidos ou anonimizados;
- quais históricos devem ser preservados;
- ordem das operações;
- tratamento das foreign keys;
- retenção necessária;
- reativação antes do prazo;
- execução automática;
- recuperação em caso de falha.

---

# 28. Ausência de conclusão individual de exercícios

O modelo atual não possui entidade persistente para representar algo como:

```text
WorkoutExerciseExecution
```

ou:

```text
CompletedWorkoutExercise
```

A execução persistida é associada ao:

```text
WorkoutDay
```

através de:

```text
WorkoutExecution
```

Portanto, conclusão individual persistida de exercícios exigirá expansão do modelo de dados em versão futura.

---

# 29. Migrations

O schema é versionado através de migrations do Entity Framework Core.

Na auditoria atual, a sequência existente é:

```text
20260810213559_InitialCreate

20260810231219_AddUsers

20260811220007_AddStudents

20260812211536_AddExercises

20260812225133_MakeExerciseNameCaseInsensitive

20260813214011_AddWorkoutTemplates

20260814224923_AddWorkouts

20260819141521_AddWorkoutExecutionDate

20260821140854_AddPhysicalAssessments
```

A migration:

```text
MakeExerciseNameCaseInsensitive
```

faz parte da evolução relacionada ao uso de `citext`.

Mudanças futuras no schema devem continuar sendo representadas por novas migrations.

---

# 30. Processo de evolução do schema

Fluxo esperado:

```text
alteração em entidade/configuração
          │
          ▼
criação de migration
          │
          ▼
revisão da migration
          │
          ▼
testes
          │
          ▼
aplicação no ambiente
          │
          ▼
__EFMigrationsHistory
```

Alterações manuais em Production não devem substituir o histórico de migrations do projeto.

---

# 31. Produção

Em Production:

```text
Flutter / Web
      │
      ▼
Avelri API
Fly.io
      │
      ▼
PostgreSQL
Supabase
```

O aplicativo cliente não acessa PostgreSQL diretamente.

Todas as operações funcionais passam pela API.

---

# 32. Data API do Supabase

A Data API/PostgREST do Supabase não é utilizada pelo fluxo principal do Avelri.

O caminho correto é:

```text
Flutter / Web
     │
     ▼
ASP.NET Core API
     │
     ▼
Entity Framework Core
     │
     ▼
PostgreSQL
```

Isso mantém centralizadas no backend:

- autenticação;
- autorização;
- multi-tenancy;
- validações;
- regras de negócio;
- persistência.

---

# 33. Configuração da conexão

A aplicação utiliza a configuração lógica:

```text
ConnectionStrings:DefaultConnection
```

Quando fornecida através de variável de ambiente .NET, a representação é:

```text
ConnectionStrings__DefaultConnection
```

O valor real nunca deve ser versionado.

Em Development podem ser utilizados:

- User Secrets;
- variáveis de ambiente.

Em Production, a connection string deve ser fornecida pela infraestrutura de secrets.

---

# 34. Menor privilégio

A conta utilizada pela API em Production deve possuir apenas as permissões necessárias para o runtime da aplicação.

Operações administrativas de banco e aplicação de migrations não precisam utilizar as mesmas permissões concedidas ao processo normal da API.

O princípio adotado é:

```text
menor privilégio necessário
```

---

# 35. Dados sensíveis

Nunca devem ser versionados:

- senha do PostgreSQL;
- connection string real;
- credenciais administrativas;
- secrets;
- JWT signing keys;
- tokens reais;
- keystores;
- credenciais da infraestrutura.

Exemplos devem utilizar placeholders:

```text
Host=<HOST>
Database=<DATABASE>
Username=<USERNAME>
Password=<PASSWORD>
```

---

# 36. Consultas sem tracking

Diversas consultas somente de leitura utilizam:

```text
AsNoTracking()
```

Isso ocorre em repositories de recursos como:

- alunos;
- exercícios;
- modelos;
- avaliações;
- execuções.

Esse recurso reduz o trabalho de tracking do Entity Framework Core quando as entidades não precisam ser modificadas.

O uso de `AsNoTracking()` não altera as regras de segurança ou isolamento por tenant.

---

# 37. Persistência e atomicidade

Operações críticas devem evitar estados parcialmente persistidos.

No Entity Framework Core, alterações que fazem parte do mesmo `SaveChangesAsync()` são persistidas como uma unidade pelo provider relacional.

Exemplos atuais incluem operações que podem combinar:

```text
desativação do treino atual
+
criação do novo treino
```

ou:

```text
substituição de estrutura
+
persistência dos novos dias/exercícios
```

Fluxos que exigirem múltiplas etapas independentes devem continuar sendo avaliados quanto à necessidade de transação explícita.

Testes de integração devem cobrir operações críticas de persistência.

---

# 38. Datas e horários

O modelo diferencia datas de domínio de instantes temporais.

Exemplo:

```text
AssessmentDate
```

é uma `DateOnly` representando a data da avaliação.

```text
ExecutionDate
```

é uma `DateOnly` representando a data local da academia em que o treino foi executado.

Já campos como:

```text
CreatedAt
UpdatedAt
CompletedAt
```

representam data/hora.

O backend utiliza UTC para timestamps operacionais e converte a data conforme o fuso da academia quando a regra de negócio depende do calendário local.

---

# 39. Visão relacional simplificada

```text
Users
 │
 └── Students
      │
      ├── Workouts
      │    │
      │    └── WorkoutDays
      │         │
      │         ├── WorkoutExercises ─── Exercises
      │         │
      │         └── WorkoutExecutions
      │
      └── PhysicalAssessments


WorkoutTemplates
 │
 └── WorkoutTemplateDays
      │
      └── WorkoutTemplateExercises ─── Exercises


WorkoutTemplates
      │
      └──── SourceWorkoutTemplateId
                    │
                    ▼
                 Workouts
```

Os seguintes recursos possuem `GymId` diretamente:

```text
Users
Exercises
WorkoutTemplates
Workouts
```

`Students`, `PhysicalAssessments` e `WorkoutExecutions` chegam ao contexto da academia por relacionamento.

---

# 40. Princípios para evolução

Mudanças futuras no banco devem respeitar:

## Segurança

Nenhuma alteração pode comprometer o isolamento entre academias.

## Integridade

Relacionamentos, validações e constraints devem impedir estados inválidos.

## Histórico

Dados de execução não devem ser apagados ou reescritos acidentalmente durante alterações de treino.

## Compatibilidade

Migrations devem considerar as versões da API e dos aplicativos em uso.

## Rastreabilidade

Mudanças estruturais devem ser representadas por migrations versionadas.

## Menor privilégio

A aplicação deve operar apenas com as permissões necessárias.

## Testabilidade

Regras críticas de persistência devem possuir cobertura automatizada.

---

# 41. Documentos relacionados

- `docs/technical/architecture.md`
- `docs/technical/api.md`
- `docs/technical/mobile.md`
- `docs/product/business-rules.md`
- `docs/product/roles-and-permissions.md`
- `docs/operations/environments.md`
- `docs/operations/deployment.md`
