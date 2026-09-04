# Ambientes — Avelri

**Produto:** Avelri
**Versão de referência:** 1.0.0+3
**Última atualização:** 04/09/2026

---

## 1. Objetivo

Este documento descreve os ambientes utilizados pelo Avelri e como seus componentes são configurados.

Ele registra:

- Development;
- Production;
- configuração do backend;
- configuração do Flutter;
- PostgreSQL;
- Supabase;
- Fly.io;
- Cloudflare Pages;
- Android;
- Web;
- CORS;
- timezone por academia;
- secrets;
- práticas de segurança.

Procedimentos de publicação estão documentados em:

`docs/operations/deployment.md`

---

# 2. Ambientes atuais

Os ambientes reconhecidos atualmente pela aplicação Flutter são:

```text
development
production
```

A arquitetura geral é:

```text
Development
    │
    ├── Flutter local / emulador / navegador
    ├── ASP.NET Core local
    └── PostgreSQL configurado para desenvolvimento
```

```text
Production
    │
    ├── Android / Google Play
    ├── Web / Cloudflare Pages
    ├── ASP.NET Core API / Fly.io
    └── PostgreSQL / Supabase
```

Não existe atualmente um terceiro valor de `APP_ENV` como `staging`.

A homologação fechada utiliza a infraestrutura de Production com contas e tenants apropriados para teste.

---

# 3. Development

Development é utilizado para:

- implementação;
- depuração;
- testes automatizados;
- testes manuais;
- execução em emulador;
- execução Web local;
- validação antes de deploy.

O backend pode ser executado localmente através do projeto:

```text
src/GymFlow.Api
```

Exemplo:

```powershell
dotnet run --project .\src\GymFlow.Api\GymFlow.Api.csproj
```

A URL efetiva deve ser confirmada pela saída da aplicação ou pela configuração local.

---

# 4. Configuração do Flutter

A configuração do cliente é lida por:

```text
AppConfig.fromEnvironment()
```

e depende de:

```text
APP_ENV
API_BASE_URL
```

Esses valores são fornecidos normalmente através de:

```text
--dart-define
```

---

# 5. APP_ENV

`APP_ENV` é obrigatório.

Valores aceitos:

```text
development
production
```

Caso esteja ausente:

```text
APP_ENV was not provided.
```

Caso possua outro valor:

```text
Invalid APP_ENV
```

A aplicação não utiliza fallback silencioso para um ambiente desconhecido.

---

# 6. API_BASE_URL

`API_BASE_URL` também é obrigatória.

A aplicação valida se o valor:

- pode ser interpretado como URI;
- possui scheme;
- possui authority.

Em Production existe uma regra adicional:

```text
scheme = https
```

Assim, uma configuração como:

```text
APP_ENV=production
API_BASE_URL=http://...
```

é rejeitada durante a inicialização.

---

# 7. Flutter em Development

Exemplo para Android Emulator:

```powershell
flutter run -d emulator-5554 `
  --dart-define=APP_ENV=development `
  --dart-define=API_BASE_URL=http://10.0.2.2:<PORTA>
```

Em Android Emulator:

```text
localhost
```

representa o próprio emulador.

Para acessar a máquina host, normalmente é utilizado:

```text
10.0.2.2
```

quando compatível com o ambiente utilizado.

---

# 8. Flutter Web em Development

Quando Flutter Web e API são executados localmente, podem ser utilizados endereços como:

```text
http://localhost:<PORTA>
```

ou:

```text
http://127.0.0.1:<PORTA>
```

A política de CORS da API permite esses hosts adicionalmente quando:

```text
ASPNETCORE_ENVIRONMENT = Development
```

---

# 9. Backend em Development

O backend pode receber configurações através de:

- .NET User Secrets;
- variáveis de ambiente;
- configurações locais não versionadas.

Entre as configurações importantes estão:

```text
ConnectionStrings:DefaultConnection

Jwt:Key
Jwt:Issuer
Jwt:Audience
Jwt:ExpirationMinutes

Cors:AllowedOrigins

GymTimeZones:{GymId}
```

Quando fornecidas como variáveis de ambiente .NET, `:` é representado normalmente por:

```text
__
```

Exemplo:

```text
ConnectionStrings__DefaultConnection
Jwt__Key
```

---

# 10. JWT em todos os ambientes

Para inicializar corretamente a API são exigidos:

```text
Jwt:Key
Jwt:Issuer
Jwt:Audience
Jwt:ExpirationMinutes
```

Regras atuais:

```text
Jwt:Key
→ obrigatório
→ mínimo de 32 bytes UTF-8

Jwt:Issuer
→ obrigatório

Jwt:Audience
→ obrigatório

Jwt:ExpirationMinutes
→ inteiro
→ maior que zero
```

Valores reais nunca devem ser documentados ou versionados.

---

# 11. Development e Swagger

Quando:

```text
ASPNETCORE_ENVIRONMENT = Development
```

a API disponibiliza os recursos de OpenAPI/Swagger configurados pelo projeto.

Esses recursos são utilizados para:

- inspeção dos endpoints;
- desenvolvimento;
- testes manuais controlados.

---

# 12. HTTPS em Development

O `Program.cs` não força redirecionamento HTTPS quando a aplicação está em Development.

Isso permite, por exemplo, executar localmente:

```text
http://localhost:<PORTA>
```

ou acessar a API pelo emulador através de HTTP local.

Essa exceção é destinada ao ambiente de desenvolvimento.

---

# 13. Production

Production representa a infraestrutura utilizada atualmente por:

- usuários;
- testadores fechados;
- Android;
- Web.

Arquitetura:

```text
Android / Web
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

---

# 14. API de Production

A API atual está disponível em:

```text
https://gymflow-api-prod.fly.dev
```

Health:

```text
https://gymflow-api-prod.fly.dev/health
```

O nome técnico do serviço ainda utiliza `gymflow`, enquanto o produto público utiliza a marca Avelri.

---

# 15. Ambiente ASP.NET Core

A API de Production é executada com:

```text
ASPNETCORE_ENVIRONMENT=Production
```

Na infraestrutura atual do Fly.io, a aplicação escuta internamente através da configuração equivalente a:

```text
ASPNETCORE_URLS=http://+:8080
```

Essa comunicação interna não significa que o cliente externo utilize HTTP.

Externamente, a API é acessada através de HTTPS fornecido pela infraestrutura do Fly.io.

---

# 16. HTTPS no Fly.io

Quando a aplicação detecta execução no Fly.io, o `Program.cs` não executa seu próprio redirecionamento HTTPS.

Fluxo:

```text
Cliente
   │
   │ HTTPS
   ▼
Fly.io
   │
   │ conexão interna com a aplicação
   ▼
ASP.NET Core
```

A terminação TLS é responsabilidade da infraestrutura do Fly.io.

---

# 17. OpenAPI e Swagger em Production

Em Production, os endpoints de OpenAPI e a interface Swagger não são mapeados pelo código atual.

Portanto:

```text
Development
→ OpenAPI / Swagger disponíveis
```

```text
Production
→ OpenAPI / Swagger não publicados
```

---

# 18. Health Check

O endpoint:

```http
GET /health
```

é público e retorna:

```json
{
  "status": "ok"
}
```

Ele pode ser utilizado para:

- validação pós-deploy;
- monitoramento;
- diagnóstico básico de disponibilidade.

Um health check bem-sucedido não garante sozinho que:

- banco;
- autenticação;
- regras de negócio;
- fluxos completos

estejam todos funcionando corretamente.

---

# 19. Verificação manual de saúde

Exemplo:

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

# 20. PostgreSQL de Production

O banco utilizado em Production é PostgreSQL hospedado no Supabase.

Fluxo:

```text
Fly.io API
    │
    ▼
Entity Framework Core
    │
    ▼
PostgreSQL
Supabase
```

O Flutter não possui conexão direta com o banco.

---

# 21. Data API do Supabase

A Data API/PostgREST não é utilizada pelo fluxo funcional do Avelri e permanece desabilitada na configuração atual.

Não existe:

```text
Flutter
   │
   ▼
Supabase Data API
```

O fluxo é:

```text
Flutter
   │
   ▼
Avelri API
   │
   ▼
EF Core
   │
   ▼
PostgreSQL
```

Alertas ou respostas relacionadas ao PostgREST não devem ser confundidos com disponibilidade da API do Avelri quando a Data API estiver deliberadamente desabilitada.

---

# 22. Role da API no banco

A aplicação em Production utiliza uma identidade própria de banco destinada ao runtime.

O princípio aplicado é:

```text
menor privilégio necessário
```

A role utilizada normalmente pela API não deve possuir permissões administrativas desnecessárias.

Operações de migrations podem exigir um contexto de maior privilégio separado do runtime.

---

# 23. Migrations em Production

A API não executa migrations automaticamente durante seu startup na configuração atual.

Mudanças estruturais devem ser tratadas como etapa controlada de deploy.

Fluxo esperado:

```text
nova migration
      │
      ▼
revisão
      │
      ▼
testes
      │
      ▼
aplicação no banco correto
      │
      ▼
deploy da aplicação
```

A aplicação em runtime não deve precisar alterar o schema durante cada inicialização.

---

# 24. Timezone por academia

Operações dependentes de calendário utilizam configuração:

```text
GymTimeZones:{GymId}
```

Exemplos de funcionalidades dependentes:

- data da execução de treino;
- conclusão do dia "hoje";
- validação da data de avaliação física;
- próxima avaliação;
- reavaliação pendente.

Os identificadores reais dos tenants não devem ser incluídos na documentação pública.

---

# 25. Ausência de timezone configurado

Quando uma academia não possui timezone configurado e a operação necessita dessa informação, o backend não utiliza silenciosamente UTC como fallback.

A operação falha através do mecanismo específico de configuração de timezone.

Isso evita produzir regras de calendário incorretas para determinado tenant.

---

# 26. Web Production

A aplicação Web atual está publicada em:

```text
https://avelri.pages.dev
```

A hospedagem utiliza:

```text
Cloudflare Pages
```

---

# 27. Build Web de Production

Executado a partir de:

```text
mobile/
```

Exemplo:

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

# 28. CORS em Production

A API utiliza:

```text
Cors:AllowedOrigins
```

para configurar origens permitidas.

A origem Web atualmente utilizada em Production é:

```text
https://avelri.pages.dev
```

A política utiliza correspondência de origem configurada.

Uma nova origem Web deve ser adicionada explicitamente quando necessária.

---

# 29. CORS em Development

Em Development, além das origens configuradas, a API aceita origens cujos hosts sejam:

```text
localhost
127.0.0.1
```

Isso facilita o desenvolvimento Flutter Web local.

---

# 30. Regras da política CORS

A política atual permite:

```text
qualquer header
qualquer método
```

para origens consideradas permitidas.

A configuração não utiliza:

```text
AllowCredentials()
```

CORS não substitui:

- autenticação;
- autorização;
- `gym_id`;
- isolamento por tenant.

---

# 31. Android e CORS

Aplicativos Android nativos não ficam sujeitos à política CORS do navegador da mesma maneira que Flutter Web.

Ainda assim, todas as chamadas continuam protegidas por:

- HTTPS;
- autenticação;
- autorização;
- multi-tenancy.

---

# 32. Android Production

O application ID atual é:

```text
com.cleytonlimadev.avelri
```

O nome apresentado pelo Android é:

```text
Avelri
```

A versão atual de referência é:

```text
1.0.0+3
```

---

# 33. Configuração Android

A configuração atual possui:

```text
minSdk = 25
```

`compileSdk` e `targetSdk` são obtidos da configuração utilizada pelo Flutter:

```text
flutter.compileSdkVersion
flutter.targetSdkVersion
```

O build atualmente distribuído foi preparado para os requisitos da Google Play vigentes no ciclo de publicação, mas o número do target não está hardcoded diretamente no arquivo Gradle.

---

# 34. Assinatura Android

O build de release utiliza:

```text
key.properties
```

para localizar os dados necessários à assinatura.

Valores como:

```text
keyAlias
keyPassword
storeFile
storePassword
```

são sensíveis.

Não devem aparecer:

- no Git;
- na documentação;
- em Issues;
- em prints públicos.

---

# 35. Build AAB de Production

Exemplo:

```powershell
flutter build appbundle --release `
  --dart-define=APP_ENV=production `
  --dart-define=API_BASE_URL=https://gymflow-api-prod.fly.dev
```

O AAB é o formato utilizado para distribuição pela Google Play.

---

# 36. Build APK de Production

Exemplo:

```powershell
flutter build apk --release `
  --dart-define=APP_ENV=production `
  --dart-define=API_BASE_URL=https://gymflow-api-prod.fly.dev
```

O APK pode ser utilizado para:

- instalação direta;
- testes locais;
- homologação técnica específica.

Ele não substitui a participação oficial do testador através da faixa fechada da Google Play.

---

# 37. Google Play Closed Testing

O aplicativo Android encontra-se no ciclo de Teste Fechado da Google Play.

Os testadores oficiais devem:

1. estar incluídos na lista ou grupo utilizado pela faixa;
2. aceitar participação através do fluxo da Google Play;
3. instalar o aplicativo através da faixa correspondente.

Novas builds podem ser distribuídas durante o teste para correção de bugs.

O build number deve ser incrementado para cada novo AAB enviado.

---

# 38. Atualizações durante o teste fechado

Fluxo:

```text
bug
 │
 ▼
correção
 │
 ▼
testes locais
 │
 ▼
novo build number
 │
 ▼
AAB
 │
 ▼
Closed Testing
```

Uma atualização de build durante o período de teste não deve ser tratada como criação de uma nova aplicação.

O processo e os requisitos formais da Google Play devem ser acompanhados diretamente no Play Console quando necessário.

---

# 39. Tenant de homologação

A infraestrutura de Production pode conter tenant fictício destinado especificamente a testes.

Ele permite validar:

- isolamento;
- branding;
- Admin;
- Professor;
- Student;
- exercícios;
- modelos;
- treinos;
- avaliações;
- histórico.

Nenhum `GymId`, usuário, senha ou outra credencial desse ambiente deve ser publicado neste documento.

---

# 40. Dados de Production

Qualquer cliente configurado com:

```text
APP_ENV=production
```

e apontado para a API de Production executa operações reais no banco de Production.

Isso inclui, conforme o fluxo:

- cadastro de usuários;
- alteração de alunos;
- ativação/inativação;
- criação e alteração de exercícios;
- criação e alteração de modelos;
- criação e edição de treinos;
- conclusões de dias;
- avaliações físicas.

Na versão atual não existem endpoints gerais de exclusão física desses recursos.

---

# 41. Teste local contra Production

Exemplo:

```powershell
flutter run -d emulator-5554 `
  --dart-define=APP_ENV=production `
  --dart-define=API_BASE_URL=https://gymflow-api-prod.fly.dev
```

Esse modo deve ser utilizado somente com contas adequadas para homologação, pois os dados persistidos são reais.

---

# 42. Branding e ambiente

O ambiente não determina diretamente o branding.

O fluxo é:

```text
usuário autenticado
      │
      ▼
GymId
      │
      ▼
branding conhecido localmente?
      │
      ├── sim
      │    ▼
      │ branding específico
      │
      └── não
           ▼
      Avelri padrão
```

Portanto:

```text
Production
```

pode atender múltiplos tenants com identidades visuais diferentes.

---

# 43. Branding não é configuração remota

Na implementação atual, o branding específico é mantido no aplicativo através de:

```text
LocalBrandingRepository
```

Adicionar uma nova identidade específica pode exigir:

- alteração no código/configuração local;
- inclusão de assets;
- novo build do cliente.

Uma futura implementação remota poderá alterar esse processo.

---

# 44. Valores públicos

Informações como as seguintes não são tratadas como secrets:

```text
https://avelri.pages.dev
https://gymflow-api-prod.fly.dev
com.cleytonlimadev.avelri
1.0.0+3
```

São endereços ou identificadores públicos necessários para operação e distribuição.

Isso é diferente de:

- senhas;
- JWT keys;
- connection strings;
- tokens;
- credenciais;
- keystores.

---

# 45. Secrets que nunca devem ser versionados

Nunca adicionar ao Git:

```text
Connection string real

Jwt:Key real

senha PostgreSQL

key.properties com valores reais

keystore

senhas de assinatura

tokens

credenciais Supabase

credenciais Fly.io

credenciais Google Play

chaves privadas
```

---

# 46. Compatibilidade cliente/API

Uma atualização da API deve considerar que builds anteriores do aplicativo podem continuar em uso.

Exemplo:

```text
API Production
     │
     ├── Android build atual
     ├── Android build anterior
     └── Web atual
```

Alterações incompatíveis devem ser evitadas ou tratadas através de estratégia adequada de versionamento.

---

# 47. Multi-tenancy em todos os ambientes

Development e Production devem utilizar as mesmas regras de isolamento.

Nunca deve existir lógica do tipo:

```text
Development
→ ignorar GymId
```

Isso impediria que testes locais representassem corretamente o comportamento de Production.

---

# 48. Logs

Logs de qualquer ambiente devem evitar dados sensíveis.

Não registrar intencionalmente:

- senha;
- JWT completo;
- signing key;
- connection string;
- credenciais;
- dados pessoais sem necessidade.

Production deve ser tratada com nível adicional de cuidado devido à presença de dados reais.

---

# 49. Validação antes de deploy

Antes de uma alteração ser distribuída:

```text
build
  │
  ▼
testes
  │
  ▼
configuração
  │
  ▼
deploy
  │
  ▼
health
  │
  ▼
login
  │
  ▼
fluxos críticos
```

Devem ser validados especialmente:

- autenticação;
- perfis;
- tenant;
- banco;
- timezone;
- CORS Web;
- cliente apontando para o ambiente correto.

---

# 50. Checklist de Production

Antes de uma publicação relevante:

- [ ] `ASPNETCORE_ENVIRONMENT` correto;
- [ ] API saudável;
- [ ] banco correto e acessível;
- [ ] migrations necessárias aplicadas;
- [ ] secrets configurados;
- [ ] JWT configurado;
- [ ] timezone dos tenants necessários configurado;
- [ ] CORS correto;
- [ ] Web apontando para a API correta;
- [ ] Android apontando para a API correta;
- [ ] `APP_ENV=production`;
- [ ] `API_BASE_URL` HTTPS;
- [ ] build number incrementado quando necessário;
- [ ] `flutter analyze` aprovado;
- [ ] `flutter test` aprovado;
- [ ] testes backend aprovados;
- [ ] login validado;
- [ ] permissões principais validadas;
- [ ] isolamento multi-tenant validado;
- [ ] nenhum secret adicionado ao repositório;
- [ ] nenhum identificador privado de tenant publicado na documentação.

---

# 51. Documentos relacionados

- `docs/technical/architecture.md`
- `docs/technical/database.md`
- `docs/technical/api.md`
- `docs/technical/mobile.md`
- `docs/operations/deployment.md`
- `docs/qa/homologation-guide.md`
- `docs/qa/test-scenarios.md`
