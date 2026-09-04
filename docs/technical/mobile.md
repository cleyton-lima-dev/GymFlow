# Aplicativo Flutter — Avelri

**Produto:** Avelri
**Versão de referência:** 1.0.0+3
**Última atualização:** 04/09/2026
**Tecnologia principal:** Flutter / Dart

---

## 1. Objetivo

Este documento descreve a arquitetura e a organização do cliente Flutter do Avelri.

Ele registra:

- estrutura do projeto;
- dependências principais;
- configuração por ambiente;
- inicialização;
- autenticação e sessão;
- armazenamento do JWT;
- comunicação HTTP;
- gerenciamento de estado;
- navegação;
- autorização visual por perfil;
- funcionalidades principais;
- multi-tenancy;
- branding por academia;
- Android;
- Web/PWA;
- segurança;
- testes;
- limitações conhecidas.

---

# 2. Plataformas

A versão atual do cliente atende:

- Android;
- Web.

O mesmo projeto Flutter compartilha a maior parte da implementação.

Estrutura principal:

```text
mobile/
├── android/
├── assets/
├── lib/
├── test/
├── web/
└── pubspec.yaml
```

---

# 3. Versão atual

A versão definida no `pubspec.yaml` é:

```text
1.0.0+3
```

Onde:

```text
1.0.0
```

é a versão visível do produto e:

```text
3
```

é o build number.

O ambiente Dart definido no projeto é:

```text
^3.13.1
```

---

# 4. Dependências principais

Entre as dependências estruturais utilizadas atualmente estão:

```text
flutter_localizations
flutter_secure_storage
go_router
http
provider
```

Versões declaradas na auditoria atual:

```text
flutter_secure_storage: 10.3.1
go_router: ^17.5.0
http: ^1.6.0
provider: ^6.1.5+1
```

Para desenvolvimento:

```text
flutter_test
flutter_lints
```

---

# 5. Organização do código

O código principal está em:

```text
mobile/lib/
```

Organização de alto nível:

```text
lib/
├── app/
├── core/
├── features/
└── main.dart
```

---

# 6. app

A pasta `app/` contém elementos globais da aplicação.

Na versão atual, inclui responsabilidades relacionadas a:

```text
app/
├── config/
├── router/
├── session/
└── theme/
```

Essas áreas concentram:

- configuração de ambiente;
- navegação;
- sessão autenticada;
- perfis;
- branding;
- tema global.

---

# 7. core

A pasta `core/` contém infraestrutura compartilhada.

Entre os componentes atuais estão:

```text
core/
├── network/
└── storage/
```

Responsabilidades incluem:

- comunicação HTTP;
- tratamento de erros HTTP;
- armazenamento seguro do token.

---

# 8. features

As funcionalidades principais estão organizadas por domínio.

Estrutura atual:

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

As features utilizam, conforme necessário:

```text
data/
models/
presentation/
widgets/
```

---

# 9. Inicialização da aplicação

O ponto de entrada é:

```text
lib/main.dart
```

O fluxo atual é:

```text
AppConfig.fromEnvironment()
        │
        ▼
FlutterSecureStorage
        │
        ▼
TokenStorage
        │
        ▼
ApiClient
        │
        ▼
AuthService
        │
        ▼
SessionController
        │
        ▼
BrandingController
        │
        ▼
AppRouter
        │
        ▼
GymFlowApp
```

Apesar de alguns nomes internos históricos ainda utilizarem `GymFlow`, a marca pública exibida ao usuário é Avelri.

---

# 10. Configuração por ambiente

A aplicação exige duas configurações durante execução/build:

```text
APP_ENV
API_BASE_URL
```

Elas são lidas através de:

```dart
String.fromEnvironment(...)
```

e normalmente fornecidas com:

```text
--dart-define
```

---

## 10.1 APP_ENV

Os únicos valores aceitos atualmente são:

```text
development
production
```

Qualquer outro valor produz erro de configuração.

A ausência de `APP_ENV` também impede a inicialização.

---

## 10.2 API_BASE_URL

`API_BASE_URL`:

- é obrigatória;
- precisa ser uma URI válida;
- precisa possuir scheme;
- precisa possuir authority.

Em Production existe uma validação adicional:

```text
API_BASE_URL deve utilizar HTTPS
```

Uma URL HTTP em Production causa falha de configuração antes da aplicação prosseguir.

---

# 11. Exemplo de execução

Exemplo de execução em Production:

```powershell
flutter run `
  --dart-define=APP_ENV=production `
  --dart-define=API_BASE_URL=https://<API>
```

Os valores reais de infraestrutura devem ser tratados conforme os documentos operacionais.

---

# 12. Aplicação global

O widget principal é:

```text
GymFlowApp
```

Ele configura:

```text
MultiProvider
MaterialApp.router
tema
locale
router
branding
```

São disponibilizados globalmente:

```text
ApiClient
SessionController
BrandingController
```

---

# 13. Idioma

A versão atual está configurada para:

```text
pt-BR
```

Locale:

```dart
Locale('pt', 'BR')
```

Os delegates do Flutter utilizados incluem:

```text
GlobalMaterialLocalizations
GlobalWidgetsLocalizations
GlobalCupertinoLocalizations
```

Não existe atualmente seleção de idioma pelo usuário.

---

# 14. Material Design e tema

O aplicativo utiliza:

```text
Material 3
```

O tema é construído através do branding ativo.

Fluxo:

```text
GymBranding
    │
    ▼
AppTheme.fromBranding(...)
    │
    ▼
ThemeData
```

São utilizados elementos como:

- cor primária;
- cor secundária;
- background;
- surface;
- modo claro/escuro.

---

# 15. Branding

O Avelri possui suporte a identidade visual por academia.

A estrutura central é:

```text
GymBranding
```

Ela pode definir:

```text
id
displayName
primaryColor
secondaryColor
backgroundColor
surfaceColor
brightness
logoAsset
```

---

# 16. Branding por tenant

Na implementação atual, o branding não é carregado de uma API ou tabela de academias.

Ele utiliza:

```text
LocalBrandingRepository
```

com um mapa local entre:

```text
GymId
→
GymBranding
```

Os identificadores reais de tenants não devem ser publicados na documentação.

---

# 17. Fluxo do branding

Após autenticação:

```text
SessionController
      │
      ▼
SessionUser.gymId
      │
      ▼
BrandingController
      │
      ▼
LocalBrandingRepository
      │
      ├── branding encontrado
      │       ▼
      │   branding específico
      │
      └── não encontrado
              ▼
       branding padrão Avelri
```

O roteador aguarda o branding correspondente à academia ser carregado antes de liberar a navegação autenticada.

---

# 18. Branding padrão

Quando não existe configuração local específica para determinada academia, o aplicativo utiliza a identidade padrão:

```text
Avelri
```

O fallback permite que um tenant não configurado visualmente continue utilizando a aplicação com a identidade neutra do produto.

---

# 19. Assets de branding

Os assets declarados atualmente incluem identidades específicas armazenadas em:

```text
assets/branding/
```

Eles são registrados no:

```text
pubspec.yaml
```

A inclusão de novo branding local exige também que seus assets sejam declarados no projeto quando necessário.

---

# 20. Sessão

A sessão é controlada por:

```text
SessionController
```

Estados possíveis:

```text
unknown
unauthenticated
authenticated
```

Representados por:

```text
SessionStatus
```

---

# 21. Usuário da sessão

O estado autenticado utiliza:

```text
SessionUser
```

com:

```text
userId
gymId
name
email
role
```

O perfil é representado por:

```text
AppRole
```

Valores:

```text
admin
professor
student
```

A conversão aceita os valores da API:

```text
Admin
Professor
Student
```

Um perfil desconhecido resulta em erro de formato.

---

# 22. Armazenamento do token

O JWT é armazenado através de:

```text
flutter_secure_storage
```

A abstração utilizada é:

```text
TokenStorage
```

A chave interna atual é:

```text
auth_token
```

Operações disponíveis:

```text
saveToken
readToken
deleteToken
```

O token não é armazenado em preferências simples do aplicativo.

---

# 23. Login

Fluxo atual:

```text
Login
  │
  ▼
AuthService.login()
  │
  ▼
POST /api/auth/login
  │
  ▼
LoginResponse
  │
  ▼
SessionController.startSession()
  │
  ├── salva JWT
  ├── configura JWT no ApiClient
  ├── cria SessionUser
  └── status = authenticated
```

---

# 24. Restauração da sessão

Ao iniciar o aplicativo:

```text
TokenStorage.readToken()
        │
        ├── null
        │    ▼
        │ unauthenticated
        │
        └── token
             │
             ▼
       ApiClient.setAccessToken()
             │
             ▼
       GET /api/auth/me
```

Se `/api/auth/me` for válido:

```text
SessionUser reconstruído
status = authenticated
```

Se retornar:

```text
401 Unauthorized
```

o token é removido e a sessão passa para:

```text
unauthenticated
```

Outros erros são propagados para o fluxo de bootstrap.

---

# 25. Tela de bootstrap

A rota inicial é:

```text
/bootstrap
```

Ela utiliza:

```text
SessionLoadingPage
```

Durante o bootstrap:

```text
carregando sessão
```

Caso ocorra erro não relacionado a uma simples sessão inválida, a interface apresenta:

```text
Tentar novamente
```

---

# 26. Invalidação automática da sessão

O `ApiClient` possui um callback para respostas:

```text
401 Unauthorized
```

em requisições autenticadas.

Fluxo:

```text
API retorna 401
      │
      ▼
ApiClient
      │
      ▼
SessionController.invalidateSession()
      │
      ├── remove token do ApiClient
      ├── remove token do secure storage
      ├── remove SessionUser
      └── status = unauthenticated
```

O roteador então redireciona para o login.

---

# 27. Logout

O logout utiliza o mesmo mecanismo de invalidação local:

```text
SessionController.logout()
```

O resultado é:

```text
token removido
+
usuário removido
+
sessão unauthenticated
```

---

# 28. ApiClient

A comunicação HTTP é centralizada em:

```text
core/network/api_client.dart
```

O cliente utiliza o package:

```text
http
```

---

## 28.1 Métodos atuais

O `ApiClient` implementa:

```text
GET
POST
PUT
PATCH
```

Não existe atualmente método:

```text
DELETE
```

Isso é coerente com a API funcional atual, que não possui endpoints `DELETE`.

---

## 28.2 Headers

Por padrão são enviados:

```http
Accept: application/json
Content-Type: application/json
```

Em requisições autenticadas, quando existe token:

```http
Authorization: Bearer <JWT>
```

---

## 28.3 Timeout

O timeout padrão é:

```text
15 segundos
```

---

## 28.4 Respostas

Status:

```text
200–299
```

são tratados como sucesso.

Outros status produzem:

```text
ApiException
```

contendo:

```text
statusCode
message
```

Respostas sem body retornam:

```text
null
```

---

# 29. Services

Cada feature utiliza Services para traduzir operações do produto em chamadas HTTP.

Entre os Services atuais estão:

```text
AuthService
StudentsService
ExercisesService
WorkoutTemplatesService
WorkoutsService
PhysicalAssessmentsService
```

Fluxo típico:

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
Avelri API
```

---

# 30. Gerenciamento de estado

A aplicação utiliza:

```text
Provider
+
ChangeNotifier
```

para diversos fluxos.

Exemplo:

```text
Page
 │
 ▼
ChangeNotifierProvider
 │
 ▼
ViewModel
 │
 ├── dados
 │
 ├── loading
 │
 ├── erros
 │
 └── ações
 │
 ▼
notifyListeners()
 │
 ▼
UI
```

---

# 31. Navegação

O roteamento é centralizado em:

```text
lib/app/router/app_router.dart
```

e utiliza:

```text
GoRouter
```

Rota inicial:

```text
/bootstrap
```

---

# 32. Redirecionamento por sessão

O Router observa:

```text
SessionController
BrandingController
```

Fluxo principal:

```text
SessionStatus.unknown
→ /bootstrap

SessionStatus.unauthenticated
→ /login

SessionStatus.authenticated
→ árvore correspondente ao perfil
```

Se o branding da academia autenticada ainda não estiver carregado:

```text
→ /bootstrap
```

---

# 33. Árvores de navegação por perfil

Destinos principais:

```text
Admin
→ /admin

Professor
→ /professor

Student
→ /student
```

O Router impede que um usuário autenticado permaneça em uma árvore pertencente a outro perfil.

Exemplo:

```text
Student
→ tentativa de /admin/...
→ /student
```

Essa proteção é somente uma camada do cliente.

A autorização definitiva continua no backend.

---

# 34. Rotas do Administrador

Entre as rotas atuais estão:

```text
/admin
/admin/more

/admin/professors
/admin/professors/new

/admin/students
/admin/students/new
/admin/students/:studentId
/admin/students/:studentId/edit

/admin/exercises
/admin/exercises/new
/admin/exercises/:exerciseId/edit

/admin/templates
/admin/templates/new
/admin/templates/:templateId
/admin/templates/:templateId/edit
```

Também existem rotas para:

- criação de treino;
- seleção de modelo;
- edição de treino;
- histórico;
- avaliações físicas.

---

# 35. Rotas do Professor

Entre as rotas atuais estão:

```text
/professor
/professor/more

/professor/students
/professor/students/:studentId

/professor/exercises
/professor/exercises/new
/professor/exercises/:exerciseId/edit

/professor/templates
/professor/templates/new
/professor/templates/:templateId
/professor/templates/:templateId/edit
```

Também existem rotas para:

- treinos;
- histórico;
- avaliações físicas.

Não existe rota de cadastro ou edição cadastral de aluno para Professor.

---

# 36. Rotas do Aluno

Rotas atuais:

```text
/student
/student/workout/day
/student/history
/student/more
/student/profile
/student/physical-assessment
/student/physical-assessment/history
/student/physical-assessment/:assessmentId
```

---

# 37. Permissões na interface

A interface adapta ações conforme o perfil.

Exemplos atuais:

```text
Cadastrar Aluno
→ somente Admin
```

```text
Editar Aluno
→ somente Admin
```

```text
Ativar/Desativar Aluno
→ somente Admin
```

```text
Professores
→ somente Admin
```

```text
Criar/Editar Treino
→ Admin + Professor
```

```text
Registrar Avaliação Física
→ Admin + Professor
```

A API continua sendo responsável pela autorização real.

---

# 38. Alunos

A listagem de alunos oferece:

- pesquisa;
- filtro por status;
- paginação;
- refresh;
- estados de loading;
- estados de erro;
- estado vazio.

A busca envia o parâmetro:

```text
search
```

que no backend considera:

```text
nome
e-mail
telefone
```

Admin e Professor podem visualizar alunos.

Somente Admin visualiza:

```text
Novo aluno
```

---

# 39. Detalhes do aluno

A tela de detalhes carrega paralelamente dados relacionados a:

```text
StudentDetails
PhysicalAssessmentSummary
CurrentWorkout
```

Admin e Professor podem:

- consultar detalhes;
- consultar treino;
- consultar histórico;
- registrar/consultar avaliações físicas.

Somente Admin pode:

- editar dados cadastrais;
- ativar aluno;
- desativar aluno.

---

# 40. Professores

O gerenciamento de Professores é exclusivo do Admin.

Fluxo atual:

```text
Mais
  │
  ▼
Professores
  │
  ├── listagem
  └── Novo professor
```

Na versão atual não existem telas de:

- editar Professor;
- ativar/desativar Professor;
- excluir Professor.

---

# 41. Exercícios

A interface de exercícios permite:

- listar;
- cadastrar;
- editar;
- filtrar por status;
- filtrar por grupo muscular;
- buscar;
- paginar.

Admin e Professor podem criar e editar exercícios.

A alteração de status é protegida pelo backend e pertence ao Admin.

---

# 42. Busca de exercícios

A interface atual apresenta o placeholder:

```text
Buscar exercício ou grupo muscular...
```

Entretanto, esse campo envia apenas:

```text
search
```

O backend utiliza `search` somente para:

```text
Exercise.Name
```

O grupo muscular é enviado através de outro parâmetro:

```text
muscleGroup
```

A interface do filtro atualmente orienta o usuário a digitar o grupo muscular como cadastrado.

Não existe normalização específica para acentuação.

Essa divergência está registrada como bug conhecido.

---

# 43. Modelos de treino

Admin e Professor podem:

- listar modelos;
- pesquisar;
- criar;
- visualizar detalhes;
- editar;
- alterar status;
- utilizar um modelo durante a criação de treino.

A busca atual utiliza o nome do modelo.

---

# 44. Criação de treino

O fluxo permite:

```text
Criar treino
     │
     ├── Usar modelo
     │
     └── Criar manualmente
```

A tela:

```text
CreateOrAssignWorkoutPage
```

apresenta as duas opções.

---

# 45. Seleção de modelo

O fluxo de seleção utiliza:

```text
SelectWorkoutTemplatePage
```

A tela:

- lista modelos ativos;
- permite pesquisar;
- possui carregamento adicional;
- retorna o modelo selecionado ao fluxo anterior.

Na implementação principal, os detalhes do modelo podem ser utilizados para pré-preencher o rascunho do treino antes do envio.

---

# 46. Edição de treino

A edição utiliza:

```text
EditWorkoutPage
```

Ela permite alterar:

- nome;
- descrição;
- divisões;
- exercícios das divisões.

Também permite:

- adicionar divisão;
- editar divisão;
- remover divisão.

Na versão atual, essa tela **não oferece seleção de outro modelo** para substituir diretamente o treino existente.

Essa limitação está registrada como bug conhecido.

---

# 47. Treino atual do Aluno

A Home utiliza:

```text
CurrentWorkoutViewModel
```

com:

```text
WorkoutsService.getMyCurrentWorkout()
```

que chama:

```http
GET /api/workouts/me/current
```

O aluno não envia `StudentId`.

---

# 48. Home do Aluno

A Home apresenta:

- branding da academia;
- saudação;
- status de avaliação física;
- treino atual;
- quantidade de dias;
- quantidade de exercícios;
- dias concluídos hoje;
- lista ordenada dos dias;
- estados de loading;
- erro;
- ausência de treino;
- refresh.

---

# 49. Card de treino atual

O card:

```text
SEU TREINO ATUAL
```

apresenta:

- nome do treino;
- data de criação;
- quantidade de dias;
- quantidade de exercícios;
- quantidade de dias concluídos hoje.

Na versão atualmente auditada, o card não possui `onTap`.

A navegação ocorre pelos cards individuais dos dias.

Esse comportamento está registrado como bug conhecido.

---

# 50. Dia de treino

Ao tocar em um dia:

```text
/student/workout/day
```

é aberta:

```text
StudentWorkoutDayPage
```

A tela apresenta:

- nome do dia;
- posição;
- quantidade de exercícios;
- séries;
- repetições;
- descanso;
- observações;
- status de conclusão.

---

# 51. Unidade de conclusão

Na versão atual, o aluno não marca exercícios individualmente como persistidos.

A unidade de conclusão é:

```text
WorkoutDay
```

O fluxo é:

```text
visualizar exercícios
       │
       ▼
executar o dia
       │
       ▼
Marcar dia como concluído
       │
       ▼
POST /api/workouts/me/days/{workoutDayId}/complete
```

---

# 52. Estado concluído

Após conclusão, a tela exibe:

```text
Dia concluído!
```

e informa que o registro foi salvo no histórico.

O horário recebido da API inclui informação suficiente para apresentação segundo o offset da academia.

---

# 53. Conclusão individual futura

A conclusão individual persistida de exercícios ainda não faz parte do modelo atual.

A melhoria planejada para V1 deverá permitir algo equivalente a:

```text
Exercício 1 → concluído
Exercício 2 → concluído
Exercício 3 → pendente
```

com persistência do estado e possibilidade de desfazer antes da finalização, conforme definição futura.

---

# 54. Histórico de treino

O Aluno possui:

```text
/student/history
```

O histórico consulta:

```http
GET /api/workouts/me/history
```

Admin e Professor possuem fluxo correspondente para consultar histórico de determinado aluno.

---

# 55. Avaliações físicas

Admin e Professor possuem funcionalidades para:

- registrar avaliação;
- consultar última avaliação;
- consultar histórico;
- consultar detalhes.

O Aluno possui funcionalidades para:

- consultar última avaliação;
- consultar histórico;
- consultar detalhes.

O Aluno não registra a própria avaliação.

---

# 56. Data atual da academia

No cadastro de avaliação física, o Flutter pode consultar:

```http
GET /api/students/{studentId}/physical-assessments/current-date
```

Essa data vem do backend segundo o timezone configurado para a academia.

O cliente não deve utilizar apenas a data local do dispositivo para validar a regra de data futura.

---

# 57. Reavaliação

A interface apresenta dados relacionados a:

```text
última avaliação
próxima avaliação
status de reavaliação
```

A regra central é definida pelo backend:

```text
NextAssessmentDate
=
AssessmentDate + 2 meses
```

e o vencimento utiliza a data local da academia.

---

# 58. Estados visuais

A versão atual já implementa diferentes estados em diversas telas:

```text
loading
success
empty
error
refresh
submitting
```

Portanto, a melhoria planejada para V1 relacionada a estados vazios deve ser entendida como:

```text
refinamento e padronização
```

e não como criação do zero.

---

# 59. Feedback de ações

Operações assíncronas utilizam feedback visual como:

```text
CircularProgressIndicator
LinearProgressIndicator
botão desabilitado
mensagem de erro
SnackBar
estado concluído
```

Exemplos:

```text
Salvando...
Concluindo...
Tentar novamente
```

---

# 60. Refresh

Algumas telas utilizam:

```text
RefreshIndicator
```

para recarregar dados.

Exemplos incluem:

- Home do Aluno;
- alunos;
- exercícios;
- professores;
- seleção de modelos.

---

# 61. Atualização após navegação

Diversos fluxos retornam:

```text
true
```

após criação ou edição.

Exemplo:

```text
Tela A
  │
  ▼
Editar
  │
  ▼
Salvar
  │
  ▼
pop(true)
  │
  ▼
Tela A recarrega
```

Esse padrão é utilizado para evitar exibir dados antigos após alterações.

---

# 62. Uso de state.extra

Algumas rotas utilizam:

```text
state.extra
```

para transportar objetos ou dados entre telas.

Exemplos incluem:

- exercício selecionado;
- modelo selecionado;
- treino;
- nome do aluno;
- argumentos de dia de treino.

Isso significa que determinadas telas não foram projetadas como rotas independentes totalmente reconstruíveis apenas a partir da URL.

Deep links futuros deverão considerar essa característica.

---

# 63. Débito técnico observado no Router

Durante a auditoria foi identificada duplicação de declarações de algumas rotas relacionadas à criação de treino do Professor.

Entre os caminhos duplicados estão fluxos equivalentes a:

```text
/professor/students/:studentId/workouts/select-template
/professor/students/:studentId/workouts/manual
/professor/students/:studentId/workouts/create
```

As implementações duplicadas não são idênticas.

Essa duplicação deve ser removida em manutenção futura para:

- reduzir ambiguidade;
- evitar divergência de comportamento;
- facilitar manutenção;
- manter uma única definição por rota.

Não deve ser criada nova funcionalidade dependendo dessa duplicação.

---

# 64. Android

O projeto Android utiliza:

```text
namespace:
com.cleytonlimadev.avelri

applicationId:
com.cleytonlimadev.avelri
```

O identificador deve permanecer estável para continuidade na Google Play.

---

# 65. Android SDK

A configuração atual define:

```text
minSdk = 25
```

Já:

```text
compileSdk
targetSdk
```

são obtidos através dos valores fornecidos pela configuração Flutter:

```text
flutter.compileSdkVersion
flutter.targetSdkVersion
```

O número do target não está hardcoded diretamente no `build.gradle.kts`.

---

# 66. Java e Kotlin

A configuração Android utiliza:

```text
Java 17
```

com:

```text
sourceCompatibility = 17
targetCompatibility = 17
```

O target JVM do Kotlin também é:

```text
17
```

---

# 67. Manifest Android

O `AndroidManifest.xml` define:

```text
android:label="Avelri"
```

e solicita:

```xml
<uses-permission android:name="android.permission.INTERNET"/>
```

O Flutter utiliza embedding Android:

```text
v2
```

---

# 68. Assinatura de release

O build Android de release utiliza configuração de assinatura externa.

Os dados são lidos de:

```text
key.properties
```

e incluem referências como:

```text
keyAlias
keyPassword
storeFile
storePassword
```

Valores reais nunca devem ser versionados ou publicados na documentação.

---

# 69. Versionamento Android

O Gradle utiliza os valores fornecidos pelo Flutter:

```text
versionCode = flutter.versionCode
versionName = flutter.versionName
```

Eles são derivados do:

```text
pubspec.yaml
```

Para:

```text
1.0.0+3
```

temos conceitualmente:

```text
versionName = 1.0.0
versionCode = 3
```

Cada novo AAB enviado à Google Play precisa utilizar um build number ainda não utilizado pela aplicação.

---

# 70. APK

Build de release:

```powershell
flutter build apk --release `
  --dart-define=APP_ENV=production `
  --dart-define=API_BASE_URL=https://<API>
```

O APK pode ser utilizado para:

- testes locais;
- instalação direta;
- homologação controlada.

Ele não substitui a instalação via Closed Testing quando a participação oficial no teste da Google Play é necessária.

---

# 71. Android App Bundle

O formato utilizado para distribuição pela Google Play é:

```text
.aab
```

Build:

```powershell
flutter build appbundle --release `
  --dart-define=APP_ENV=production `
  --dart-define=API_BASE_URL=https://<API>
```

---

# 72. Web

A versão Web utiliza o mesmo projeto Flutter.

Build:

```powershell
flutter build web --release `
  --dart-define=APP_ENV=production `
  --dart-define=API_BASE_URL=https://<API>
```

Saída:

```text
build/web/
```

---

# 73. CORS no Web

A versão Web executa dentro do navegador e está sujeita a CORS.

Portanto:

```text
Web
 │
 ▼
API
 │
 ▼
Cors:AllowedOrigins
```

precisa estar configurado adequadamente.

CORS não substitui:

- autenticação;
- autorização;
- multi-tenancy.

---

# 74. Segurança do cliente

O Flutter não deve conter:

- senha de banco;
- connection string;
- JWT hardcoded;
- JWT real em logs;
- chave privada;
- secret do backend;
- credenciais administrativas.

A URL pública da API não é um segredo, mas deve ser fornecida através da configuração de ambiente do build.

---

# 75. Banco de dados

O Flutter nunca acessa diretamente o PostgreSQL.

Fluxo obrigatório:

```text
Flutter
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

Isso mantém no servidor:

- autorização;
- validações;
- tenant;
- regras de negócio;
- persistência.

---

# 76. Multi-tenancy no Flutter

O Flutter conhece o:

```text
gymId
```

do usuário autenticado principalmente para funcionalidades do cliente, como branding.

Ele não deve utilizar esse identificador como mecanismo de segurança para decidir quais dados podem ser acessados.

A proteção real permanece no backend.

---

# 77. Responsividade

Diversas páginas utilizam limites como:

```text
maxWidth: 680
```

para evitar expansão excessiva em telas maiores.

Também são utilizados:

- `Expanded`;
- `Flexible`;
- scroll;
- `SafeArea`;
- layouts adaptáveis;
- truncamento controlado.

A homologação deve continuar validando:

- celulares estreitos;
- diferentes escalas de texto;
- Web;
- orientação e tamanhos variados.

---

# 78. Acessibilidade

A evolução do aplicativo deve considerar:

- contraste;
- escalonamento de texto;
- área de toque;
- labels compreensíveis;
- semântica;
- feedback visual;
- suporte a tecnologias assistivas.

A implementação atual não deve ser considerada uma auditoria completa de acessibilidade.

---

# 79. Testes Flutter

O projeto possui testes automatizados Flutter.

Comando:

```powershell
flutter test
```

Testes devem acompanhar alterações relevantes de lógica e navegação.

---

# 80. Análise estática

Comando:

```powershell
flutter analyze
```

Resultado esperado antes de concluir alterações:

```text
No issues found!
```

---

# 81. Formatação

Arquivos Dart modificados devem permanecer formatados.

Projeto:

```powershell
dart format lib
```

Arquivo específico:

```powershell
dart format caminho\do\arquivo.dart
```

---

# 82. Bugs conhecidos relacionados ao Flutter

A versão em homologação possui atualmente comportamentos conhecidos relacionados a:

- card de treino atual do Aluno sem ação de toque;
- ausência de fluxo direto para substituir o treino atual por outro modelo durante edição;
- busca de exercícios não compatível com o texto exibido na interface em relação a grupo muscular;
- busca sem normalização de acentuação;
- ajuste visual da tela de Professores já corrigido localmente e dependente de distribuição em nova build.

O documento oficial desses itens é:

```text
docs/qa/known-issues.md
```

---

# 83. Evolução planejada da V1

Itens planejados incluem:

- conclusão individual persistida de exercícios;
- desfazer conclusão individual antes da finalização;
- retomada de treino em andamento;
- reorganização de dias e exercícios;
- refinamento dos estados vazios existentes;
- polimento de navegação e ações;
- política de exclusão de alunos após período prolongado de inatividade, dependente da definição técnica final.

Esses itens não devem ser tratados como funcionalidades já existentes.

---

# 84. Evolução planejada da V2

Entre as ideias registradas para evolução posterior estão:

- progresso visual avançado;
- evolução de carga;
- temporizador de descanso;
- vídeos demonstrativos;
- gráficos avançados;
- notificações e lembretes.

---

# 85. Princípios para novas telas

Ao criar ou alterar uma tela, deve-se verificar:

1. qual perfil pode acessá-la;
2. qual endpoint será utilizado;
3. como o tenant é protegido no backend;
4. qual Service é responsável;
5. qual ViewModel controla o estado;
6. quais estados visuais são necessários;
7. quais erros podem ocorrer;
8. como funciona o refresh;
9. como a tela retorna após alterações;
10. se a rota depende de `state.extra`;
11. como funciona em diferentes tamanhos;
12. quais testes devem ser atualizados.

---

# 86. Princípios de manutenção

## Separação de responsabilidades

A UI não deve concentrar regras de negócio ou acesso HTTP.

## Segurança

A ocultação de uma ação no Flutter nunca substitui autorização da API.

## Consistência

Fluxos equivalentes devem utilizar padrões equivalentes.

## Estado

Loading, erro, vazio e sucesso devem ser apresentados explicitamente quando aplicável.

## Navegação

Cada rota deve possuir uma única definição clara e previsível.

## Multi-tenancy

O cliente não deve ser tratado como fronteira de segurança.

## Testabilidade

Alterações relevantes devem continuar sendo cobertas por análise estática, testes automatizados e homologação controlada.

---

# 87. Documentos relacionados

- `docs/technical/architecture.md`
- `docs/technical/api.md`
- `docs/technical/database.md`
- `docs/product/business-rules.md`
- `docs/product/roles-and-permissions.md`
- `docs/qa/homologation-guide.md`
- `docs/qa/test-scenarios.md`
- `docs/qa/known-issues.md`
- `docs/operations/environments.md`
- `docs/operations/deployment.md`
