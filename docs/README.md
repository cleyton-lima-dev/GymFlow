# Documentação — Avelri

Este diretório reúne a documentação funcional, técnica, operacional, arquitetural e de homologação do Avelri.

O objetivo é manter as informações relevantes do produto versionadas junto ao código-fonte e sincronizadas com a implementação.

---

# 1. Estrutura

```text
docs/
├── README.md
│
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
│   └── ...
│
└── history/
    ├── product-vision-v0.1.0.md
    └── mvp-scope-original.md
```

---

# 2. Produto

Documentação relacionada ao funcionamento atual e às regras do Avelri.

## [Visão do Produto](product/overview.md)

Apresenta:

- problema que o Avelri resolve;
- público-alvo;
- perfis;
- funcionalidades atuais;
- execução dos treinos;
- avaliações físicas;
- multi-tenancy;
- branding;
- estado atual do produto;
- evoluções planejadas.

---

## [Regras de Negócio](product/business-rules.md)

Define regras relacionadas a:

- academias e tenants;
- usuários;
- alunos;
- Professores;
- exercícios;
- modelos;
- treinos;
- execuções;
- histórico;
- avaliações físicas;
- autenticação;
- segurança;
- comportamentos futuros já planejados.

---

## [Perfis e Permissões](product/roles-and-permissions.md)

Define as permissões dos três perfis:

```text
Administrador
Professor
Aluno
```

Inclui:

- ações permitidas;
- ações proibidas;
- diferenças entre Admin e Professor;
- regras de autorização;
- matriz de permissões.

---

# 3. QA e Homologação

Documentação utilizada para validar a versão distribuída.

## [Guia de Homologação](qa/homologation-guide.md)

Documento principal para orientar o QA.

Inclui:

- ambiente de teste;
- perfis;
- fluxos principais;
- regras atuais;
- segurança;
- multi-tenancy;
- classificação de bugs;
- critérios de aprovação;
- processo de reteste.

---

## [Cenários de Teste](qa/test-scenarios.md)

Contém cenários funcionais, de autorização, segurança e isolamento.

Os identificadores utilizam grupos como:

```text
AUTH
ADM
PROF
EXE
TPL
WKT
STU
RUN
HIST
PA
TEN
SEC
UI
```

Exemplos:

```text
AUTH-001
ADM-001
EXE-001
WKT-001
TEN-001
SEC-001
```

---

## [Bugs Conhecidos](qa/known-issues.md)

Registra os problemas conhecidos da versão atualmente distribuída.

O documento permite:

- evitar Issues duplicadas;
- orientar o QA;
- diferenciar bug de melhoria;
- acompanhar necessidade de reteste;
- registrar a situação da build distribuída.

O GitHub Project continua sendo a fonte operacional para o estado mais recente das Issues.

---

# 4. Documentação Técnica

Documentação sobre a implementação atual.

## [Arquitetura](technical/architecture.md)

Descreve:

- arquitetura cliente-servidor;
- camadas backend;
- dependências;
- autenticação;
- multi-tenancy;
- timezone;
- arquitetura Flutter;
- infraestrutura;
- limitações arquiteturais atuais.

---

## [Banco de Dados](technical/database.md)

Documenta:

- PostgreSQL;
- Entity Framework Core;
- entidades;
- tabelas;
- relacionamentos;
- índices;
- constraints;
- `citext`;
- regras de unicidade;
- `DeleteBehavior`;
- multi-tenancy;
- migrations;
- histórico de treinos.

---

## [API](technical/api.md)

Documenta:

- autenticação JWT;
- claims;
- autorização;
- multi-tenancy;
- Controllers;
- endpoints atuais;
- códigos HTTP;
- rate limiting;
- CORS;
- health check;
- comunicação com o Flutter.

---

## [Aplicativo Flutter](technical/mobile.md)

Documenta:

- estrutura do projeto Flutter;
- configuração por ambiente;
- sessão;
- armazenamento seguro do JWT;
- `ApiClient`;
- Provider;
- ChangeNotifier;
- GoRouter;
- rotas;
- branding;
- Android;
- Web;
- limitações e débitos técnicos conhecidos.

---

# 5. Operações e Deploy

## [Ambientes](operations/environments.md)

Descreve:

- Development;
- Production;
- configuração do Flutter;
- configuração da API;
- Fly.io;
- Supabase;
- Cloudflare Pages;
- CORS;
- timezone;
- Android;
- Google Play;
- secrets.

---

## [Deploy e Publicação](operations/deployment.md)

Descreve procedimentos para:

- migrations;
- deploy da API;
- Fly.io;
- build Web;
- Cloudflare Pages;
- build APK;
- build AAB;
- Google Play Closed Testing;
- páginas legais;
- versionamento;
- rollback;
- checklists de publicação.

---

# 6. Requisitos

Os requisitos ficam em:

```text
docs/requirements/
```

Arquivos atuais:

- [Requisitos Funcionais](requirements/functional-requirements.md)
- [Requisitos Não Funcionais](requirements/non-functional-requirements.md)

Esses documentos devem permanecer coerentes com:

- regras de negócio;
- permissões;
- implementação atual;
- escopo vigente do produto.

Quando houver divergência, os requisitos devem ser revisados em conjunto com o código e a documentação atual do produto.

---

# 7. ADRs

Decisões arquiteturais relevantes são registradas como:

```text
ADR — Architecture Decision Record
```

e ficam em:

```text
docs/adrs/
```

Os ADRs registram decisões importantes e o contexto que levou a elas.

Entre as decisões atualmente preservadas estão temas relacionados a:

- modelo de negócio;
- evolução orientada a feedback;
- prioridade de UX simples e rápida;
- utilização de uma única aplicação para os diferentes perfis.

O ADR mais recente criado durante a reorganização documental registra formalmente a decisão de aplicação única.

ADRs históricos não devem ser alterados apenas porque uma decisão posterior evoluiu; quando necessário, uma nova decisão deve registrar a mudança.

---

# 8. Histórico

Documentos que representam fases anteriores do projeto ficam em:

```text
docs/history/
```

Atualmente:

- [Visão do Produto v0.1.0](history/product-vision-v0.1.0.md)
- [Escopo Original do MVP](history/mvp-scope-original.md)

Esses arquivos possuem valor histórico e acadêmico, mas **não devem ser utilizados como fonte principal para determinar o comportamento atual do Avelri**.

Exemplo:

o escopo original do MVP foi definido antes da inclusão das Avaliações Físicas no MVP atual.

A documentação vigente em:

```text
docs/product/
```

deve ser utilizada para compreender o produto atual.

---

# 9. Fonte de verdade

A documentação procura representar fielmente a implementação.

Entretanto, para comportamento técnico executável, a referência definitiva continua sendo:

```text
código
+
migrations
+
configuração
+
testes
```

Quando houver divergência entre documentação e implementação:

1. confirmar o comportamento no código;
2. determinar se existe bug ou documentação desatualizada;
3. corrigir a fonte apropriada;
4. manter ambos sincronizados.

---

# 10. Segurança da documentação

Nenhum documento versionado deve conter:

- senhas;
- JWTs reais;
- signing keys;
- connection strings reais;
- credenciais PostgreSQL;
- secrets;
- keystores;
- senhas de assinatura;
- credenciais de infraestrutura;
- identificadores privados de tenant sem necessidade;
- dados pessoais reais usados em testes.

Exemplos devem utilizar placeholders ou dados fictícios.

---

# 11. Informações públicas

Informações necessárias para operação pública podem ser documentadas.

Exemplos:

```text
nome do produto
package Android
URL pública da Web
URL pública da API
versão/build
```

Esses valores não devem ser confundidos com secrets.

---

# 12. Atualização da documentação

A documentação deve ser revista quando houver mudanças relevantes em:

- funcionalidades;
- regras de negócio;
- perfis;
- permissões;
- endpoints;
- banco;
- arquitetura;
- navegação;
- ambientes;
- deploy;
- homologação;
- bugs conhecidos.

Uma alteração relevante no produto deve incluir documentação no mesmo ciclo sempre que aplicável.

---

# 13. Bugs e melhorias

Problemas confirmados devem ser registrados no GitHub Project.

Convenções utilizadas:

```text
[BUG]
[IMPROVEMENT]
[V2]
```

Workflow:

```text
Backlog
   ↓
In Progress
   ↓
Testing
   ↓
Done
```

Uma Issue permanece aberta enquanto estiver em `Testing`.

Ela deve ser fechada após validação e passagem para `Done`.

---

# 14. Leitura recomendada — novo colaborador

Ordem recomendada:

1. [README principal](../README.md)
2. [Visão do Produto](product/overview.md)
3. [Regras de Negócio](product/business-rules.md)
4. [Perfis e Permissões](product/roles-and-permissions.md)
5. [Arquitetura](technical/architecture.md)
6. [API](technical/api.md)
7. [Banco de Dados](technical/database.md)
8. [Aplicativo Flutter](technical/mobile.md)

---

# 15. Leitura recomendada — QA

Para homologação:

1. [Visão do Produto](product/overview.md)
2. [Regras de Negócio](product/business-rules.md)
3. [Perfis e Permissões](product/roles-and-permissions.md)
4. [Guia de Homologação](qa/homologation-guide.md)
5. [Cenários de Teste](qa/test-scenarios.md)
6. [Bugs Conhecidos](qa/known-issues.md)

---

# 16. Leitura recomendada — Deploy

Para publicação e operação:

1. [Arquitetura](technical/architecture.md)
2. [Ambientes](operations/environments.md)
3. [Banco de Dados](technical/database.md)
4. [API](technical/api.md)
5. [Deploy e Publicação](operations/deployment.md)
