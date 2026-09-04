# Avelri

Plataforma de gestão de treinos para academias, Professores e Alunos.

> O projeto teve início com o nome interno **GymFlow**.
> **Avelri** é a identidade pública e comercial atual do produto.

**Versão de referência:** `1.0.0+3`

---

## Sobre o projeto

O Avelri foi desenvolvido para digitalizar e organizar a gestão de treinos em academias.

A plataforma centraliza recursos como:

- alunos;
- Professores;
- exercícios;
- modelos de treino;
- treinos individuais;
- histórico de execuções;
- avaliações físicas.

Administradores e Professores possuem áreas voltadas à gestão e acompanhamento técnico, respeitando as permissões de cada perfil.

Os Alunos possuem acesso próprio para consultar seus dados, visualizar o treino atual, acessar os exercícios prescritos, registrar a conclusão de dias de treino, consultar histórico e acompanhar avaliações físicas.

O sistema foi desenvolvido com suporte a múltiplas academias, mantendo o contexto e os dados de cada organização isolados.

Mais detalhes:

- [Visão do Produto](docs/product/overview.md)
- [Regras de Negócio](docs/product/business-rules.md)
- [Perfis e Permissões](docs/product/roles-and-permissions.md)

---

## Status atual

O Avelri encontra-se no ciclo inicial de homologação.

A versão Android é distribuída atualmente através do:

```text
Google Play — Closed Testing
```

Versão de referência:

```text
1.0.0+3
```

O foco atual é:

- identificação e correção de bugs;
- validação dos principais fluxos;
- validação de permissões;
- testes de multi-tenancy;
- homologação em dispositivos reais;
- estabilidade antes da disponibilização pública.

Bugs conhecidos:

- [Bugs Conhecidos](docs/qa/known-issues.md)

---

## Perfis

O sistema possui três perfis:

```text
Administrador
Professor
Aluno
```

### Administrador

Pode, entre outras ações:

- cadastrar, editar e ativar/inativar alunos;
- listar e cadastrar Professores;
- cadastrar e editar exercícios;
- alterar status de exercícios;
- criar e editar modelos de treino;
- criar e editar treinos;
- consultar históricos;
- registrar e consultar avaliações físicas.

### Professor

Pode:

- consultar alunos;
- acessar detalhes dos alunos;
- cadastrar e editar exercícios;
- criar e editar modelos de treino;
- alterar status de modelos;
- criar e editar treinos;
- consultar históricos;
- registrar e consultar avaliações físicas.

O Professor não possui as operações administrativas exclusivas do Administrador.

### Aluno

Pode:

- consultar dados pessoais;
- visualizar o treino atual;
- acessar os dias do treino;
- visualizar exercícios;
- registrar a conclusão de um dia;
- consultar o próprio histórico;
- consultar avaliações físicas.

---

## Principais funcionalidades

### Gestão de alunos

- cadastro pelo Administrador;
- consulta por Admin e Professor;
- busca;
- paginação;
- filtro por status;
- edição pelo Administrador;
- ativação/inativação pelo Administrador.

### Professores

- cadastro pelo Administrador;
- listagem pelo Administrador;
- isolamento por academia.

### Exercícios

- catálogo por academia;
- cadastro;
- edição;
- busca e filtros;
- status ativo/inativo.

### Modelos de treino

- criação;
- edição;
- dias;
- exercícios;
- séries;
- repetições;
- descanso;
- observações;
- utilização como base para novos treinos.

### Treinos

Podem ser criados:

```text
manualmente
```

ou:

```text
a partir de modelo
```

Cada aluno pode possuir no máximo um treino ativo.

Quando um novo treino é criado, o treino ativo anterior é desativado.

### Execução

Na versão atual, a persistência da execução ocorre por:

```text
WorkoutDay
```

O Aluno acessa um dia, executa seus exercícios e registra a conclusão desse dia.

A conclusão individual persistida por exercício está planejada para evolução posterior.

### Histórico

Execuções anteriores permanecem disponíveis mesmo quando o treino atual é substituído ou versionado.

### Avaliações físicas

Admin e Professor podem registrar avaliações com:

- peso;
- altura;
- percentual de gordura;
- medidas corporais;
- observações.

O Aluno pode consultar suas próprias avaliações.

A próxima reavaliação é calculada a partir da avaliação mais recente, considerando intervalo de dois meses.

---

## Multi-tenancy

O Avelri atende múltiplas academias através da mesma plataforma.

O tenant é identificado por:

```text
GymId
```

O contexto autenticado determina quais dados podem ser acessados.

O isolamento se aplica a recursos como:

- usuários;
- alunos;
- Professores;
- exercícios;
- modelos;
- treinos;
- históricos;
- avaliações físicas.

O Flutter adapta a experiência ao usuário autenticado, mas a proteção efetiva do isolamento é realizada pelo backend.

Mais detalhes:

- [Regras de Negócio](docs/product/business-rules.md)
- [Arquitetura Técnica](docs/technical/architecture.md)
- [Banco de Dados](docs/technical/database.md)

---

## Branding

O Avelri possui identidade visual padrão e suporta personalização por academia.

A versão atual resolve configurações específicas de branding no cliente com base no contexto do tenant autenticado.

Quando não existe configuração específica, é utilizado o branding padrão:

```text
Avelri
```

Branding não interfere nas regras de autenticação, autorização ou multi-tenancy.

---

## Tecnologias

### Backend

- C#
- ASP.NET Core
- Entity Framework Core
- PostgreSQL
- JWT Bearer
- OpenAPI / Swagger em Development

### Cliente

- Flutter
- Dart
- Material 3
- Provider
- ChangeNotifier
- GoRouter
- `flutter_secure_storage`
- HTTP

### Infraestrutura

- Supabase PostgreSQL
- Fly.io
- Cloudflare Pages
- Google Play

---

## Arquitetura

O backend está localizado em:

```text
src/
```

e é dividido em:

```text
src/
├── GymFlow.Api/
├── GymFlow.Application/
├── GymFlow.Domain/
└── GymFlow.Infrastructure/
```

Os nomes `GymFlow.*` permanecem como nomenclatura técnica interna do projeto.

A marca pública é Avelri.

O cliente Flutter está em:

```text
mobile/
```

com estrutura principal:

```text
mobile/lib/
├── app/
├── core/
├── features/
└── main.dart
```

Mais detalhes:

- [Arquitetura Técnica](docs/technical/architecture.md)
- [API](docs/technical/api.md)
- [Banco de Dados](docs/technical/database.md)
- [Aplicativo Flutter](docs/technical/mobile.md)

---

## Estrutura resumida do repositório

```text
GymFlow/
├── README.md
│
├── src/
│   ├── GymFlow.Api/
│   ├── GymFlow.Application/
│   ├── GymFlow.Domain/
│   └── GymFlow.Infrastructure/
│
├── tests/
│   ├── GymFlow.Application.Tests/
│   └── GymFlow.IntegrationTests/
│
├── mobile/
│   ├── android/
│   ├── assets/
│   ├── lib/
│   ├── test/
│   ├── web/
│   └── pubspec.yaml
│
└── docs/
    ├── product/
    ├── qa/
    ├── technical/
    ├── operations/
    ├── requirements/
    ├── adrs/
    └── history/
```

---

## Executando o backend

Na raiz do repositório:

```powershell
dotnet run --project .\src\GymFlow.Api\GymFlow.Api.csproj
```

Configurações necessárias devem ser fornecidas através de mecanismos seguros.

Exemplos:

```text
ConnectionStrings__DefaultConnection

Jwt__Key
Jwt__Issuer
Jwt__Audience
Jwt__ExpirationMinutes
```

Configurações reais não devem ser adicionadas ao repositório.

---

## Executando o Flutter

Entre na pasta:

```powershell
cd mobile
```

Instale dependências:

```powershell
flutter pub get
```

Development:

```powershell
flutter run `
  --dart-define=APP_ENV=development `
  --dart-define=API_BASE_URL=<URL_DA_API>
```

O Flutter exige:

```text
APP_ENV
API_BASE_URL
```

Valores aceitos de ambiente:

```text
development
production
```

Em Production, a URL da API deve utilizar HTTPS.

---

## Testes

### Backend

Na raiz:

```powershell
dotnet test
```

### Flutter

Na pasta `mobile`:

```powershell
flutter test
```

e:

```powershell
flutter analyze
```

A homologação manual complementa os testes automatizados.

Documentação de QA:

- [Guia de Homologação](docs/qa/homologation-guide.md)
- [Cenários de Teste](docs/qa/test-scenarios.md)
- [Bugs Conhecidos](docs/qa/known-issues.md)

---

## Production

### API

```text
https://gymflow-api-prod.fly.dev
```

Health:

```text
https://gymflow-api-prod.fly.dev/health
```

### Web

```text
https://avelri.pages.dev
```

A versão Web e o aplicativo Android utilizam a mesma API e as mesmas regras de negócio.

Procedimentos:

- [Ambientes](docs/operations/environments.md)
- [Deploy e Publicação](docs/operations/deployment.md)

---

## Documentação

A documentação completa está em:

```text
docs/
```

Índice:

- [Documentação do Avelri](docs/README.md)

Estrutura:

```text
docs/
├── product/
│   ├── overview.md
│   ├── business-rules.md
│   └── roles-and-permissions.md
│
├── qa/
│   ├── homologation-guide.md
│   ├── test-scenarios.md
│   └── known-issues.md
│
├── technical/
│   ├── architecture.md
│   ├── database.md
│   ├── api.md
│   └── mobile.md
│
├── operations/
│   ├── environments.md
│   └── deployment.md
│
├── requirements/
│   ├── functional-requirements.md
│   └── non-functional-requirements.md
│
├── adrs/
│   └── ADR-*.md
│
└── history/
    ├── product-vision-v0.1.0.md
    └── mvp-scope-original.md
```

Os documentos de `history/` registram fases anteriores e não são a fonte principal para determinar o comportamento atual.

---

## Segurança

Nunca devem ser adicionados ao repositório:

- senhas;
- JWTs reais;
- signing keys;
- connection strings reais;
- secrets;
- credenciais de banco;
- keystores;
- senhas de assinatura;
- credenciais de usuários;
- identificadores privados de tenant sem necessidade;
- dados pessoais reais utilizados em testes.

Configurações sensíveis devem ser fornecidas através de mecanismos seguros e externos ao código-fonte.

---

## Princípios do projeto

O desenvolvimento busca preservar:

- simplicidade;
- segurança;
- isolamento entre academias;
- eficiência para tarefas frequentes;
- preservação de histórico;
- separação de responsabilidades;
- evolução orientada por necessidades reais;
- documentação sincronizada com o código.

As principais decisões estão registradas em:

```text
docs/adrs/
```

---

## Autor

**Cleyton Lima**

Projeto desenvolvido como produto de software voltado à digitalização da gestão de treinos em academias.
