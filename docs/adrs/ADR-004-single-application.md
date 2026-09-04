# ADR-004 — Aplicativo Único para Todos os Perfis

## Status

Aprovado

---

## Contexto

Era necessário decidir se o Avelri utilizaria aplicativos separados para Administradores, Professores e Alunos ou uma única aplicação compartilhada por todos os perfis.

Manter aplicativos separados aumentaria:

- esforço de desenvolvimento;
- duplicação de código;
- manutenção;
- testes;
- publicação;
- distribuição;
- necessidade de sincronização entre versões;
- risco de divergência funcional entre perfis.

Ao mesmo tempo, cada perfil precisa possuir experiência, navegação e permissões próprias.

---

## Decisão

O Avelri utilizará **uma única aplicação Flutter** para todos os perfis.

Após a autenticação, o sistema identifica o perfil do usuário e disponibiliza a experiência correspondente.

Os perfis atuais são:

```text
Admin
Professor
Student
```

Fluxo conceitual:

```text
Login
  │
  ▼
Usuário autenticado
  │
  ▼
Perfil identificado
  │
  ├── Admin
  ├── Professor
  └── Student
```

Cada perfil utiliza uma árvore de navegação própria dentro da mesma aplicação.

Na implementação atual:

```text
Admin
→ /admin

Professor
→ /professor

Student
→ /student
```

---

## Administrador

O Administrador utiliza a mesma aplicação e possui acesso às funções administrativas autorizadas.

Entre elas:

- alunos;
- Professores;
- exercícios;
- modelos de treino;
- treinos;
- históricos;
- avaliações físicas.

Funcionalidades exclusivas do Administrador devem ser disponibilizadas somente para esse perfil.

---

## Professor

O Professor utiliza a mesma aplicação, porém com permissões próprias.

Entre seus recursos estão:

- consulta de alunos;
- exercícios;
- modelos de treino;
- criação e edição de treinos;
- históricos;
- avaliações físicas.

O Professor não deve receber acesso visual ou funcional às operações exclusivas do Administrador.

---

## Aluno

O Aluno utiliza a mesma aplicação para acessar sua área individual.

Entre os recursos atuais estão:

- Home;
- dados pessoais;
- treino atual;
- dias e exercícios;
- conclusão de dias;
- histórico de treinos;
- avaliações físicas.

O Aluno não deve possuir acesso às áreas administrativas.

---

## Autenticação e navegação

Após o login:

```text
JWT + perfil
      │
      ▼
SessionController
      │
      ▼
GoRouter
      │
      ▼
árvore correspondente ao perfil
```

O cliente restringe a navegação para manter o usuário dentro de sua área correspondente.

Exemplo:

```text
Student
→ tentativa de acessar /admin/...
→ redirecionamento para /student
```

---

## Autorização

A diferença entre perfis não deve existir somente na interface.

O Flutter adapta:

- menus;
- rotas;
- botões;
- ações disponíveis.

A API é responsável pela autorização efetiva.

Portanto:

```text
Flutter
→ experiência e navegação de acordo com o perfil
```

```text
Backend
→ autenticação e autorização efetivas
```

Ocultar um botão ou impedir uma rota no cliente não substitui proteção no servidor.

---

## Multi-tenancy

A utilização de uma única aplicação também não altera o isolamento entre academias.

Após autenticação, o contexto inclui:

```text
Role
+
GymId
```

O perfil determina **o que** o usuário pode fazer.

O tenant determina **sobre quais dados** ele pode operar.

Essas duas dimensões devem permanecer independentes.

Exemplo:

```text
Professor da Academia A
```

pode possuir permissão para consultar alunos, mas somente alunos pertencentes à:

```text
Academia A
```

---

## Branding

A utilização de uma única aplicação não impede personalização visual por academia.

Fluxo atual:

```text
Usuário autenticado
      │
      ▼
GymId
      │
      ▼
branding específico disponível?
      │
      ├── sim → branding da academia
      └── não → Avelri padrão
```

Assim, múltiplas academias podem utilizar a mesma aplicação mantendo identidades visuais diferentes.

---

## Plataformas

A decisão de aplicação única também se aplica à base Flutter utilizada atualmente para:

```text
Android
Web
```

Não existem aplicativos Android separados por perfil.

A distribuição Android utiliza um único package:

```text
com.cleytonlimadev.avelri
```

e a versão Web utiliza a mesma base funcional.

---

## Justificativa

A utilização de uma única aplicação:

- reduz duplicação de código;
- reduz custo de desenvolvimento;
- simplifica manutenção;
- simplifica publicação;
- reduz quantidade de artefatos;
- facilita reutilização de componentes;
- mantém uma identidade única de produto;
- reduz risco de divergência entre aplicativos;
- simplifica evolução de funcionalidades compartilhadas.

---

## Consequências positivas

- uma única base Flutter;
- uma única publicação Android;
- uma única aplicação Web;
- componentes compartilhados;
- sessão centralizada;
- manutenção centralizada;
- comportamento mais consistente;
- correções compartilhadas entre perfis quando aplicável.

---

## Cuidados necessários

A decisão exige atenção especial a:

- redirecionamento correto após login;
- isolamento das árvores de navegação;
- ocultação adequada de ações;
- autorização real no backend;
- isolamento multi-tenant;
- testes de acesso indevido;
- evitar componentes que assumam incorretamente um único perfil.

---

## Testabilidade

A aplicação deverá possuir testes e homologação que cubram cenários como:

```text
Admin
→ recursos administrativos permitidos
```

```text
Professor
→ recursos técnicos permitidos
→ operações exclusivas do Admin proibidas
```

```text
Student
→ somente recursos próprios
```

Também deverão ser validadas tentativas de acesso indevido diretamente pela API, pois a proteção visual do Flutter não é suficiente.

---

## Alternativa rejeitada

A alternativa rejeitada foi manter aplicações independentes, por exemplo:

```text
Avelri Admin
Avelri Professor
Avelri Aluno
```

Essa opção aumentaria significativamente:

- código duplicado;
- builds;
- testes;
- publicação;
- manutenção;
- sincronização de versões.

O benefício não justificaria a complexidade adicional para o estágio atual do produto.

---

## Histórico

Essa decisão existe desde as primeiras fases do projeto, quando o produto ainda utilizava o nome `GymFlow`.

Ela foi posteriormente formalizada através deste ADR durante a reorganização da documentação.

A marca pública atual é:

```text
Avelri
```

A mudança de marca não altera a decisão arquitetural.
