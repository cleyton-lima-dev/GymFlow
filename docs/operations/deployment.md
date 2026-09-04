# Deploy e Publicação — Avelri

**Produto:** Avelri
**Versão de referência:** 1.0.0+3
**Última atualização:** 04/09/2026

---

## 1. Objetivo

Este documento descreve o processo atual de build, deploy e publicação do Avelri.

Abrange:

- backend ASP.NET Core;
- PostgreSQL;
- migrations;
- Fly.io;
- Flutter Android;
- Google Play;
- Flutter Web;
- Cloudflare Pages;
- páginas legais;
- versionamento;
- validações;
- rollback;
- segurança.

Configurações específicas dos ambientes estão documentadas em:

`docs/operations/environments.md`

---

# 2. Arquitetura de Production

## Android

```text
Google Play
    │
    ▼
Avelri Android
    │
    │ HTTPS
    ▼
Fly.io
ASP.NET Core API
    │
    ▼
Entity Framework Core
    │
    ▼
Supabase
PostgreSQL
```

## Web

```text
Browser
   │
   ▼
Cloudflare Pages
https://avelri.pages.dev
   │
   │ HTTPS
   ▼
Fly.io
https://gymflow-api-prod.fly.dev
   │
   ▼
Entity Framework Core
   │
   ▼
Supabase PostgreSQL
```

---

# 3. Componentes publicados

A infraestrutura atual utiliza:

```text
API
→ Fly.io

Banco
→ Supabase PostgreSQL

Web
→ Cloudflare Pages

Android
→ Google Play
```

Aplicação Fly:

```text
gymflow-api-prod
```

Package Android:

```text
com.cleytonlimadev.avelri
```

A marca pública é:

```text
Avelri
```

Nomes internos históricos como `GymFlow` continuam presentes na solução e infraestrutura.

---

# 4. Princípio geral de deploy

Um deploy deve partir de código conhecido, validado e versionável.

Fluxo recomendado:

```text
alteração
   │
   ▼
revisão local
   │
   ▼
formatação
   │
   ▼
análise estática
   │
   ▼
testes
   │
   ▼
migration, se necessária
   │
   ▼
deploy da API, se necessário
   │
   ▼
health + testes funcionais
   │
   ▼
build dos clientes, se necessário
   │
   ├── Web
   │
   └── Android
   │
   ▼
homologação
```

Nem toda alteração exige publicação de todos os componentes.

---

# 5. Pré-requisitos

Antes de uma publicação relevante, verificar:

- branch correta;
- `git status`;
- alterações conhecidas;
- testes aprovados;
- análise estática aprovada;
- migrations revisadas, quando existirem;
- secrets configurados;
- ambiente correto;
- API correta;
- versão/build corretos;
- branding esperado;
- ausência de credenciais no repositório.

---

# 6. Git

Na raiz:

```powershell
git status
```

Para revisar os commits recentes:

```powershell
git log --oneline -n 10
```

Também é recomendável revisar:

```powershell
git diff
```

antes de criar um commit de release ou deploy.

Não publicar código cuja origem ou alterações locais não estejam compreendidas.

---

# 7. Testes backend

Na raiz do repositório:

```powershell
dotnet test
```

Os projetos de teste atuais estão em:

```text
tests/
├── GymFlow.Application.Tests/
└── GymFlow.IntegrationTests/
```

Um deploy não deve prosseguir com falhas de testes relevantes sem que a causa e o impacto tenham sido analisados explicitamente.

---

# 8. Flutter — preparação

Na pasta:

```text
mobile/
```

executar:

```powershell
dart format lib
```

Depois:

```powershell
flutter analyze
```

Resultado esperado:

```text
No issues found!
```

E:

```powershell
flutter test
```

---

# 9. Banco e migrations

Alterações estruturais do PostgreSQL devem ser representadas por migrations do Entity Framework Core.

Os projetos relevantes são:

```text
src/GymFlow.Api/GymFlow.Api.csproj
src/GymFlow.Infrastructure/GymFlow.Infrastructure.csproj
```

A API não executa migrations automaticamente no startup atual.

---

# 10. Criar migration

A partir da raiz do repositório:

```powershell
dotnet ef migrations add <NomeDaMigration> `
  --project .\src\GymFlow.Infrastructure\GymFlow.Infrastructure.csproj `
  --startup-project .\src\GymFlow.Api\GymFlow.Api.csproj
```

Antes de aceitar a migration, revisar:

- `Up`;
- `Down`;
- foreign keys;
- índices;
- constraints;
- defaults;
- nullable/non-nullable;
- impacto em registros existentes.

---

# 11. Gerar script de migration

Para produzir um script idempotente:

```powershell
dotnet ef migrations script --idempotent `
  --project .\src\GymFlow.Infrastructure\GymFlow.Infrastructure.csproj `
  --startup-project .\src\GymFlow.Api\GymFlow.Api.csproj `
  --output .\artifacts\release\GymFlow-migrations.sql
```

O nome do arquivo pode ser adaptado para a versão ou release.

O SQL deve ser revisado antes de ser aplicado em Production.

---

# 12. Aplicação da migration em Production

Migrations devem ser executadas através de um contexto com permissões adequadas.

A role utilizada pela API em runtime segue o princípio:

```text
menor privilégio necessário
```

e não precisa possuir privilégios para modificar o schema.

Fluxo recomendado:

```text
migration versionada
       │
       ▼
script revisado
       │
       ▼
credencial adequada para schema
       │
       ▼
aplicação no PostgreSQL correto
       │
       ▼
verificação
       │
       ▼
deploy da API
```

---

# 13. Histórico de migrations

O EF Core registra o estado aplicado em:

```text
__EFMigrationsHistory
```

A role normal da API em Production não precisa possuir acesso de manutenção a essa tabela.

Antes de alterações estruturais importantes, confirmar que o schema de Production está no estado esperado.

---

# 14. Rollback de banco

Rollback de banco deve ser tratado com mais cautela do que rollback de código.

Não executar automaticamente:

```text
migration Down
```

em Production sem analisar:

- possível perda de dados;
- colunas já preenchidas;
- novas constraints;
- foreign keys;
- dados gravados pela versão nova;
- compatibilidade com a API anterior.

Muitas vezes uma migration corretiva é mais segura que reverter uma migration já utilizada em Production.

---

# 15. Deploy da API — Fly.io

Antes:

```powershell
fly config validate
```

Deploy:

```powershell
fly deploy --app gymflow-api-prod
```

A configuração atual utiliza internamente:

```text
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
```

Externamente, a aplicação é acessada por HTTPS através do Fly.io.

---

# 16. Secrets do backend

Entre os valores necessários estão:

```text
ConnectionStrings__DefaultConnection

Jwt__Key
Jwt__Issuer
Jwt__Audience
Jwt__ExpirationMinutes
```

Também podem existir configurações não secretas como:

```text
Cors__AllowedOrigins
GymTimeZones__<TENANT>
```

Os valores reais não devem ser documentados publicamente.

Identificadores reais de tenant também não devem ser incluídos neste documento.

---

# 17. Health após deploy

Executar:

```powershell
Invoke-RestMethod https://gymflow-api-prod.fly.dev/health
```

Resultado esperado:

```text
status
------
ok
```

---

# 18. Health não substitui homologação

`/health` confirma disponibilidade básica do processo da API.

Depois dele, validar pelo menos:

- login;
- JWT;
- acesso ao PostgreSQL;
- leitura de recurso;
- escrita controlada;
- permissão de Admin;
- permissão de Professor;
- permissão de Student;
- isolamento por tenant;
- timezone em fluxo que dependa de data.

---

# 19. Teste local contra Production

Na pasta `mobile`:

```powershell
flutter run -d emulator-5554 `
  --dart-define=APP_ENV=production `
  --dart-define=API_BASE_URL=https://gymflow-api-prod.fly.dev
```

Outro dispositivo pode substituir:

```text
emulator-5554
```

Esse modo utiliza dados reais de Production.

Utilizar contas destinadas à homologação.

---

# 20. Alterações reais ao testar Production

Quando o cliente aponta para Production, operações como estas são reais:

- cadastro de aluno;
- cadastro de Professor;
- edição de aluno;
- ativação/inativação;
- criação/edição de exercício;
- criação/edição de modelo;
- criação/substituição de treino;
- conclusão de dia;
- avaliação física.

Na versão atual, a API não oferece endpoints gerais de exclusão física desses recursos.

---

# 21. Build Web

Na pasta:

```text
mobile/
```

executar:

```powershell
flutter build web --release `
  --dart-define=APP_ENV=production `
  --dart-define=API_BASE_URL=https://gymflow-api-prod.fly.dev
```

Saída:

```text
mobile/build/web/
```

---

# 22. Páginas legais antes do deploy Web

O build publicado deve conter:

```text
privacy.html
delete-account.html
```

Eles são necessários para as páginas públicas utilizadas pela distribuição do Avelri.

Antes da publicação, verificar se os arquivos continuam presentes em:

```text
mobile/build/web/
```

---

# 23. Deploy Web

A aplicação Web é publicada no Cloudflare Pages.

Endereço:

```text
https://avelri.pages.dev
```

O conteúdo publicado corresponde ao resultado de:

```text
mobile/build/web/
```

O procedimento operacional pode ser realizado através do mecanismo configurado no projeto Cloudflare Pages.

---

# 24. Validação Web

Após publicação, verificar:

- página inicial;
- carregamento dos assets;
- login;
- comunicação com API;
- ausência de erro de CORS;
- branding;
- navegação;
- fluxos críticos;
- responsividade.

---

# 25. CORS Production

A origem Web atualmente autorizada é:

```text
https://avelri.pages.dev
```

Não utilizar em Production uma política equivalente a:

```text
AllowAnyOrigin
```

sem necessidade técnica explícita.

Uma nova origem Web deve ser configurada de forma controlada.

---

# 26. Validação das páginas legais

Verificar:

```text
https://avelri.pages.dev/privacy.html
```

e:

```text
https://avelri.pages.dev/delete-account.html
```

Esses endereços não devem desaparecer durante um rebuild Web.

---

# 27. Versão Flutter

A versão é definida em:

```text
mobile/pubspec.yaml
```

Formato:

```text
major.minor.patch+build
```

Versão atual de referência:

```text
1.0.0+3
```

---

# 28. Build number Android

A parte após `+` é utilizada como build/version code.

Exemplo:

```text
1.0.0+3
        │
        └── build 3
```

Cada novo AAB enviado à Google Play precisa possuir um `versionCode` ainda não utilizado e superior ao anterior.

Exemplo:

```text
1.0.0+3
→ 1.0.0+4
→ 1.0.0+5
```

---

# 29. Versão semântica

Mudanças de correção podem utilizar, quando fizer sentido:

```text
1.0.1
```

Novas funcionalidades compatíveis podem justificar evolução de `minor`.

Mudanças incompatíveis ou uma mudança significativa de produto podem eventualmente justificar uma nova versão `major`.

O roadmap interno chamado:

```text
V2
```

não implica automaticamente:

```text
2.0.0
```

A numeração pública deve ser decidida de acordo com a estratégia de release do produto.

---

# 30. Configuração Android atual

Package:

```text
com.cleytonlimadev.avelri
```

Min SDK:

```text
25
```

O `targetSdk` é fornecido pela configuração do Flutter e não está hardcoded diretamente no `build.gradle.kts`.

O artefato atualmente distribuído no ciclo de homologação atende:

```text
Target API 36
```

---

# 31. Build APK

```powershell
flutter build apk --release `
  --dart-define=APP_ENV=production `
  --dart-define=API_BASE_URL=https://gymflow-api-prod.fly.dev
```

Saída padrão:

```text
mobile/build/app/outputs/flutter-apk/app-release.apk
```

---

# 32. Uso do APK

O APK pode ser utilizado para:

- teste local;
- dispositivo físico;
- emulador;
- homologação técnica direta.

Instalar um APK manualmente não substitui a participação oficial no Closed Testing da Google Play.

---

# 33. Instalação do APK

Exemplo:

```powershell
flutter install -d <DEVICE> `
  --use-application-binary=.\build\app\outputs\flutter-apk\app-release.apk
```

O build de release deve ser testado, e não apenas uma execução debug.

---

# 34. Build AAB

```powershell
flutter build appbundle --release `
  --dart-define=APP_ENV=production `
  --dart-define=API_BASE_URL=https://gymflow-api-prod.fly.dev
```

Saída:

```text
mobile/build/app/outputs/bundle/release/app-release.aab
```

---

# 35. Assinatura Android

O build release utiliza informações carregadas de:

```text
key.properties
```

A assinatura deve continuar consistente para permitir atualizações do mesmo aplicativo.

Nunca versionar ou publicar:

- keystore;
- senha do keystore;
- key alias sensível;
- key password;
- conteúdo real de `key.properties`.

---

# 36. Artefatos organizados

Artefatos aprovados podem ser preservados em:

```text
artifacts/release/
```

Exemplo:

```text
Avelri-1.0.0+3.apk
Avelri-1.0.0+3.aab
```

Não utilizar o diretório intermediário do Flutter como único local de referência para um artefato aprovado.

---

# 37. Hash SHA-256

Para registrar a integridade de um artefato:

```powershell
Get-FileHash .\artifacts\release\Avelri-1.0.0+3.apk -Algorithm SHA256
```

ou:

```powershell
Get-FileHash .\artifacts\release\Avelri-1.0.0+3.aab -Algorithm SHA256
```

Hashes não são secrets e podem ser mantidos junto ao registro de uma release quando necessário.

---

# 38. Google Play

O Android é distribuído através da aplicação:

```text
Avelri
```

na Google Play.

A faixa atualmente utilizada no ciclo de homologação é:

```text
Teste fechado
```

com track de teste configurada no Play Console.

---

# 39. Atualização no Closed Testing

Fluxo:

```text
incrementar build
       │
       ▼
build AAB
       │
       ▼
testar artefato
       │
       ▼
Play Console
       │
       ▼
Teste fechado
       │
       ▼
nova release
       │
       ▼
upload AAB
       │
       ▼
notas
       │
       ▼
revisar
       │
       ▼
publicar na faixa
```

---

# 40. Atualizar AAB durante o teste

Correções de bugs podem ser distribuídas durante o Closed Testing.

Uma atualização de AAB:

- não cria uma nova aplicação;
- não exige nova package name;
- não exige nova faixa apenas por ser uma atualização;
- mantém o aplicativo dentro do ciclo de teste existente.

Requisitos específicos de elegibilidade da conta devem sempre ser conferidos no Play Console quando houver dúvida.

---

# 41. Testadores

Existem dois conceitos independentes:

```text
Conta Google
→ participa do Teste Fechado

Conta Avelri
→ autentica dentro do aplicativo
```

O e-mail Google do testador não precisa ser o mesmo e-mail utilizado como usuário Avelri.

---

# 42. Fluxo do testador oficial

O testador deve:

1. utilizar uma conta Google autorizada na faixa;
2. acessar o link oficial de participação;
3. aceitar participar;
4. instalar o Avelri através da Google Play;
5. permanecer inscrito no teste conforme as regras exigidas para o ciclo.

---

# 43. Publicação pública Android

O acesso à distribuição pública depende das condições e aprovações exigidas pela Google Play.

Isso é diferente do deploy técnico:

```text
API funcionando
≠
aplicativo liberado publicamente na loja
```

A API e o Web podem estar em Production enquanto o Android permanece em Closed Testing.

---

# 44. Notas da release

As notas devem ser curtas e relevantes.

Exemplo:

```text
- Correções de estabilidade.
- Ajustes de navegação.
- Correções visuais.
```

Não incluir:

- IDs de tenants;
- stack traces;
- secrets;
- credenciais;
- detalhes internos desnecessários.

---

# 45. Deploy apenas do backend

Nem toda alteração requer novo cliente.

Exemplos:

- otimização de Repository;
- correção interna de regra;
- melhoria de segurança;
- ajuste de consulta;
- correção compatível de API.

Fluxo:

```text
backend
   │
   ▼
testes
   │
   ▼
Fly.io
   │
   ▼
health
   │
   ▼
homologação
```

---

# 46. Deploy apenas do Flutter

Uma alteração somente no cliente pode não exigir deploy da API.

Exemplo:

```text
ajuste visual da tela de Professores
```

Pode exigir:

```text
Web
e/ou
novo Android build
```

dependendo das plataformas afetadas.

---

# 47. Web e Android são releases independentes

Apesar de compartilharem o projeto Flutter:

```text
Web build
```

e:

```text
Android build
```

são artefatos independentes.

Uma alteração pode ser publicada:

- somente na Web;
- somente no Android;
- nas duas plataformas.

---

# 48. Ordem de deploy quando backend e cliente mudam

Quando possível, utilizar estratégia compatível:

```text
1. backend recebe suporte ao contrato novo

2. contrato anterior permanece funcionando

3. novo Web/App é publicado

4. adoção ocorre

5. contrato legado pode ser removido posteriormente
```

Evitar:

```text
backend quebra contrato existente
        │
        ▼
cliente anterior deixa de funcionar
```

---

# 49. Compatibilidade

Antes de alterar a API, considerar clientes coexistindo:

```text
API Production
     │
     ├── Android build anterior
     ├── Android build atual
     └── Web atual
```

A API atual não possui versão explícita no caminho:

```text
/api/v1
```

Portanto, compatibilidade retroativa merece atenção especial.

---

# 50. Rollback da API

Antes de deploy relevante, identificar:

- commit anterior conhecido;
- alterações do deploy;
- migrations relacionadas;
- compatibilidade entre código anterior e schema atual.

Rollback de código só é seguro se a versão anterior continuar compatível com o banco existente.

---

# 51. Rollback Android

Uma build já utilizada pela Google Play não deve ser substituída tentando diminuir o build number.

Exemplo correto:

```text
+3 possui problema
      │
      ▼
corrigir
      │
      ▼
+4
```

---

# 52. Rollback Web

Caso seja necessário restaurar uma versão anterior do Web, verificar antes:

- compatibilidade com a API atual;
- páginas legais;
- assets;
- branding;
- contratos esperados.

Uma restauração visualmente bem-sucedida pode ainda ser incompatível com um backend mais novo.

---

# 53. Falha no deploy da API

Se `fly deploy` falhar:

1. não publicar clientes dependentes da mudança;
2. revisar a saída do deploy;
3. revisar logs;
4. revisar configuração;
5. revisar secrets;
6. revisar migration;
7. corrigir;
8. realizar novo deploy;
9. executar `/health`;
10. executar teste funcional.

---

# 54. API saudável mas produto falhando

Investigar na seguinte ordem aproximada:

```text
/health
   │
   ▼
login
   │
   ▼
JWT
   │
   ▼
PostgreSQL
   │
   ▼
tenant
   │
   ▼
timezone
   │
   ▼
CORS, quando Web
   │
   ▼
contrato da API
   │
   ▼
cliente
```

Não assumir:

```text
health = 200
```

como prova de que todos os fluxos estão corretos.

---

# 55. Logs de Production

Logs podem ser utilizados durante investigação.

Antes de compartilhar qualquer trecho, remover ou mascarar:

- e-mail real;
- telefone;
- JWT;
- secrets;
- connection strings;
- identificadores privados de tenant;
- dados pessoais desnecessários.

---

# 56. Segurança dos artefatos

APK, AAB e Web não devem conter secrets do servidor.

Nunca incluir no cliente:

- senha PostgreSQL;
- connection string;
- JWT fixo;
- `Jwt:Key`;
- credencial administrativa;
- chave privada de backend.

A URL pública da API:

```text
https://gymflow-api-prod.fly.dev
```

não é segredo.

---

# 57. Branding antes da publicação

Validar:

```text
tenant conhecido
→ branding correto
```

e:

```text
tenant sem configuração
→ branding padrão Avelri
```

Como a implementação atual utiliza:

```text
LocalBrandingRepository
```

uma alteração em branding específico pode exigir nova build do Flutter.

---

# 58. Processo de bug fix

Durante Closed Testing:

```text
Issue
  │
  ▼
Backlog
  │
  ▼
In Progress
  │
  ▼
correção
  │
  ▼
testes locais
  │
  ▼
Testing
  │
  ▼
reteste no artefato necessário
  │
  ▼
Done
```

A Issue deve permanecer aberta enquanto estiver em:

```text
Testing
```

Ela deve ser fechada somente depois da validação da correção e passagem para:

```text
Done
```

---

# 59. Bug que depende de nova build

Fluxo:

```text
correção local
     │
     ▼
analyze/test
     │
     ▼
Testing
     │
     ▼
incrementar build
     │
     ▼
AAB/Web
     │
     ▼
publicar
     │
     ▼
reteste
     │
     ▼
Done
     │
     ▼
fechar Issue
```

---

# 60. Known Issues

Após correção ou mudança de estado, atualizar:

```text
docs/qa/known-issues.md
```

Os estados documentados devem refletir a situação real da versão distribuída.

Uma correção apenas local não deve ser descrita como já disponível para testadores se ainda depender de nova publicação.

---

# 61. Nova funcionalidade

Fluxo recomendado:

```text
requisito
   │
   ▼
regra de negócio
   │
   ▼
perfis/permissões
   │
   ▼
impacto no banco
   │
   ▼
backend
   │
   ▼
Flutter
   │
   ▼
testes
   │
   ▼
documentação
   │
   ▼
homologação
   │
   ▼
release
```

---

# 62. Documentação e release

Uma mudança funcional deve levar à revisão, quando aplicável, de:

```text
README.md

docs/product/
docs/qa/
docs/technical/
docs/operations/
```

Código e documentação devem permanecer sincronizados.

---

# 63. Checklist — API antes do deploy

- [ ] branch correta;
- [ ] `git status` revisado;
- [ ] `git diff` revisado;
- [ ] testes backend aprovados;
- [ ] migration criada, se necessária;
- [ ] migration revisada;
- [ ] schema de Production conhecido;
- [ ] secrets necessários configurados;
- [ ] timezone necessário configurado;
- [ ] configuração Fly validada;
- [ ] nenhum secret adicionado ao Git.

---

# 64. Checklist — API depois do deploy

- [ ] `fly deploy` concluído;
- [ ] `/health` retorna sucesso;
- [ ] login funciona;
- [ ] autenticação JWT funciona;
- [ ] leitura do PostgreSQL funciona;
- [ ] escrita controlada funciona;
- [ ] Admin validado;
- [ ] Professor validado;
- [ ] Student validado;
- [ ] multi-tenancy preservado;
- [ ] regra de timezone validada quando aplicável;
- [ ] nenhum erro relevante nos logs.

---

# 65. Checklist — Android

Antes:

- [ ] `dart format` executado nos arquivos alterados;
- [ ] `flutter analyze` aprovado;
- [ ] `flutter test` aprovado;
- [ ] versão conferida;
- [ ] build number novo;
- [ ] `APP_ENV=production`;
- [ ] `API_BASE_URL` correta e HTTPS;
- [ ] assinatura configurada;
- [ ] branding correto.

Depois:

- [ ] APK/AAB gerados;
- [ ] APK release testado quando aplicável;
- [ ] login validado;
- [ ] fluxos críticos validados;
- [ ] AAB enviado à faixa correta;
- [ ] notas preenchidas;
- [ ] versão publicada/revisada no Play Console;
- [ ] reteste realizado quando a release corrige bug.

---

# 66. Checklist — Web

Antes:

- [ ] `flutter analyze`;
- [ ] `flutter test`;
- [ ] `APP_ENV=production`;
- [ ] API Production configurada;
- [ ] CORS preparado;
- [ ] `privacy.html` presente;
- [ ] `delete-account.html` presente.

Depois:

- [ ] `https://avelri.pages.dev` abre;
- [ ] login funciona;
- [ ] chamadas à API funcionam;
- [ ] nenhum erro de CORS;
- [ ] branding correto;
- [ ] `privacy.html` acessível;
- [ ] `delete-account.html` acessível.

---

# 67. Checklist — banco

Quando houver alteração estrutural:

- [ ] migration versionada;
- [ ] `Up` revisado;
- [ ] `Down` revisado;
- [ ] SQL revisado quando utilizado;
- [ ] impacto em dados analisado;
- [ ] foreign keys revisadas;
- [ ] constraints revisadas;
- [ ] índices revisados;
- [ ] Production no estado esperado;
- [ ] estratégia de recuperação considerada;
- [ ] migration aplicada;
- [ ] `__EFMigrationsHistory` consistente;
- [ ] API validada após a alteração.

---

# 68. Checklist — segurança

Antes de publicar:

- [ ] nenhum secret no Git;
- [ ] nenhuma senha na documentação;
- [ ] nenhum JWT real versionado;
- [ ] nenhuma connection string real;
- [ ] nenhum tenant privado exposto desnecessariamente;
- [ ] role de banco com privilégio adequado;
- [ ] HTTPS em Production;
- [ ] CORS restrito;
- [ ] autenticação validada;
- [ ] autorização por perfil validada;
- [ ] isolamento multi-tenant validado.

---

# 69. Checklist — release completa

Quando API, Web e Android forem atualizados em conjunto:

```text
1. revisar Git
2. testar backend
3. testar Flutter
4. aplicar migration, se necessária
5. deploy API
6. health
7. homologar API
8. build Web
9. publicar Web
10. validar Web
11. incrementar build Android
12. build APK/AAB
13. testar release Android
14. publicar Closed Testing
15. retestar
16. atualizar known-issues
17. atualizar documentação
18. concluir Issue/PR quando validado
```

---

# 70. Documentos relacionados

- `README.md`
- `docs/README.md`
- `docs/operations/environments.md`
- `docs/technical/architecture.md`
- `docs/technical/database.md`
- `docs/technical/api.md`
- `docs/technical/mobile.md`
- `docs/qa/homologation-guide.md`
- `docs/qa/test-scenarios.md`
- `docs/qa/known-issues.md`
