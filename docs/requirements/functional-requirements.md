# Requisitos Funcionais — Avelri

**Produto:** Avelri
**Versão de referência:** 1.0.0+3
**Última atualização:** 04/09/2026

---

## Objetivo

Este documento registra os requisitos funcionais da versão atual do Avelri.

Os requisitos descrevem o comportamento esperado do produto independentemente de eventuais bugs conhecidos da build em homologação.

Permissões detalhadas estão documentadas em:

`docs/product/roles-and-permissions.md`

---

# Autenticação e sessão

## RF-001 — Autenticação de usuários

O sistema deverá permitir autenticação por e-mail e senha para usuários dos perfis:

- Administrador;
- Professor;
- Aluno.

---

## RF-002 — Bloqueio de usuário inativo

O sistema deverá impedir autenticação e uso posterior de sessão por usuários inativos.

Um token emitido anteriormente não deverá continuar concedendo acesso caso o usuário seja posteriormente inativado.

---

## RF-003 — Identificação do usuário autenticado

O sistema deverá disponibilizar os dados básicos do usuário autenticado necessários para reconstrução da sessão, incluindo:

- identificador;
- academia;
- nome;
- e-mail;
- perfil.

---

## RF-004 — Encerramento de sessão

O aplicativo deverá permitir que o usuário encerre sua sessão e remova localmente o token de autenticação.

---

# Academias e multi-tenancy

## RF-005 — Isolamento entre academias

O sistema deverá garantir que usuários de uma academia não consigam consultar ou modificar dados pertencentes a outra academia.

O isolamento deverá ser aplicado aos recursos multi-tenant do produto.

---

## RF-006 — Contexto de academia autenticado

Operações protegidas deverão determinar a academia através do contexto autenticado do usuário.

O cliente não deverá escolher arbitrariamente outra academia para executar operações.

---

# Alunos

## RF-007 — Cadastro de alunos

O sistema deverá permitir que Administradores cadastrem novos alunos.

O aluno criado deverá ser automaticamente vinculado à mesma academia do Administrador autenticado.

---

## RF-008 — Listagem de alunos

O sistema deverá permitir que Administradores e Professores consultem os alunos pertencentes à própria academia.

A listagem deverá suportar:

- paginação;
- busca;
- filtro por status.

---

## RF-009 — Busca de alunos

A busca de alunos deverá permitir consulta por:

- nome;
- e-mail;
- telefone.

---

## RF-010 — Consulta de detalhes do aluno

Administradores e Professores deverão poder consultar os detalhes dos alunos pertencentes à própria academia.

---

## RF-011 — Edição de aluno

Somente o Administrador deverá poder editar os dados cadastrais de um aluno.

---

## RF-012 — Ativação e inativação de aluno

Somente o Administrador deverá poder alterar o estado ativo/inativo de um aluno.

A inativação não deverá remover automaticamente seus dados históricos.

---

## RF-013 — Consulta do próprio cadastro

O Aluno deverá poder consultar seus próprios dados pessoais sem precisar informar manualmente seu `StudentId`.

---

# Professores

## RF-014 — Cadastro de Professor

O sistema deverá permitir que somente Administradores cadastrem novos Professores.

O Professor deverá ser automaticamente vinculado à mesma academia do Administrador responsável pelo cadastro.

---

## RF-015 — Listagem de Professores

O sistema deverá permitir que somente Administradores consultem a lista de Professores da própria academia.

---

## RF-016 — Restrição do gerenciamento de Professores

O perfil Professor não deverá possuir acesso ao gerenciamento de Professores.

Na versão atual não fazem parte do escopo funcional:

- edição de Professor;
- ativação/inativação de Professor;
- exclusão de Professor.

---

# Exercícios

## RF-017 — Cadastro de exercícios

Administradores e Professores deverão poder cadastrar exercícios no catálogo da própria academia.

---

## RF-018 — Consulta de exercícios

Administradores e Professores deverão poder consultar o catálogo de exercícios da própria academia.

A listagem deverá suportar:

- paginação;
- busca;
- filtro por grupo muscular;
- filtro por status.

---

## RF-019 — Edição de exercícios

Administradores e Professores deverão poder editar exercícios pertencentes à própria academia.

---

## RF-020 — Alteração de status de exercícios

Somente o Administrador deverá poder ativar ou desativar exercícios.

---

## RF-021 — Unicidade de exercício por academia

Uma mesma academia não deverá possuir dois exercícios com o mesmo nome considerando comparação sem diferenciação entre letras maiúsculas e minúsculas.

---

# Modelos de treino

## RF-022 — Cadastro de modelos de treino

Administradores e Professores deverão poder criar modelos reutilizáveis de treino.

Um modelo deverá permitir definir:

- nome;
- descrição;
- dias;
- exercícios;
- séries;
- repetições;
- descanso;
- observações;
- ordem.

---

## RF-023 — Validação estrutural de modelo

Um modelo deverá possuir pelo menos um dia.

Cada dia deverá possuir pelo menos um exercício.

---

## RF-024 — Consulta de modelos

Administradores e Professores deverão poder consultar modelos pertencentes à própria academia.

A listagem deverá suportar:

- paginação;
- busca;
- filtro por status.

---

## RF-025 — Edição de modelos

Administradores e Professores deverão poder editar modelos pertencentes à própria academia.

---

## RF-026 — Alteração de status de modelos

Administradores e Professores deverão poder ativar e desativar modelos de treino.

---

# Treinos

## RF-027 — Criação manual de treino

Administradores e Professores deverão poder criar manualmente um treino para um aluno ativo da própria academia.

O treino deverá permitir:

- nome;
- descrição;
- dias;
- exercícios;
- séries;
- repetições;
- descanso;
- observações;
- ordenação.

---

## RF-028 — Criação de treino a partir de modelo

Administradores e Professores deverão poder criar um treino utilizando um modelo ativo da própria academia como base.

---

## RF-029 — Um único treino ativo por aluno

Cada aluno deverá possuir no máximo um treino ativo.

Quando um novo treino for criado para um aluno que já possui treino ativo, o treino anterior deverá ser desativado e o novo deverá se tornar o treino atual.

---

## RF-030 — Consulta do treino atual por profissional

Administradores e Professores deverão poder consultar o treino atual de um aluno pertencente à própria academia.

---

## RF-031 — Consulta do próprio treino

O Aluno deverá poder consultar seu próprio treino ativo sem precisar informar manualmente seu `StudentId`.

---

## RF-032 — Organização do treino por dias

O treino deverá ser organizado em um ou mais dias ordenados.

Cada dia deverá possuir seus respectivos exercícios e prescrições.

---

## RF-033 — Edição de treino

Administradores e Professores deverão poder editar o treino atual de um aluno.

---

## RF-034 — Preservação do histórico durante edição

Quando um treino já possuir execuções registradas, sua edição não deverá alterar retroativamente os dados utilizados pelo histórico.

Nessa situação, o sistema deverá preservar a versão anterior e criar uma nova versão ativa.

---

# Execução de treino

## RF-035 — Visualização do dia de treino

O Aluno deverá poder selecionar um dia de seu treino atual e visualizar:

- exercícios;
- séries;
- repetições;
- descanso;
- observações;
- ordem dos exercícios.

---

## RF-036 — Conclusão de dia de treino

O Aluno deverá poder registrar a conclusão de um `WorkoutDay` pertencente ao seu treino ativo.

---

## RF-037 — Limite de conclusão diária

O mesmo dia de treino não deverá gerar mais de uma execução na mesma data local da academia.

---

## RF-038 — Data local da academia

Regras relacionadas à data de execução deverão utilizar o fuso horário configurado para a academia.

---

# Histórico

## RF-039 — Histórico do Aluno

O Aluno deverá poder consultar seu próprio histórico de execuções.

---

## RF-040 — Histórico por profissional

Administradores e Professores deverão poder consultar o histórico de um aluno pertencente à própria academia.

---

## RF-041 — Preservação do histórico

O histórico deverá permanecer disponível mesmo quando:

- o treino atual for substituído;
- o treino anterior se tornar inativo;
- uma nova versão do treino for criada.

---

# Avaliações físicas

## RF-042 — Cadastro de avaliação física

Administradores e Professores deverão poder registrar avaliações físicas para alunos pertencentes à própria academia.

---

## RF-043 — Dados da avaliação física

Uma avaliação deverá permitir registrar:

- data da avaliação;
- peso;
- altura;
- percentual de gordura, quando informado;
- tórax;
- cintura;
- abdômen;
- quadril;
- braço direito;
- braço esquerdo;
- coxa direita;
- coxa esquerda;
- panturrilha direita;
- panturrilha esquerda;
- observações.

---

## RF-044 — Campos opcionais da avaliação

O sistema deverá permitir salvar uma avaliação física sem exigir o preenchimento das medidas corporais opcionais.

---

## RF-045 — Unicidade de avaliação por data

Um aluno não deverá possuir duas avaliações físicas com a mesma data de avaliação.

---

## RF-046 — Bloqueio de avaliação futura

O sistema deverá impedir o registro de avaliação com data posterior à data atual da academia.

---

## RF-047 — Consulta da avaliação mais recente

Administradores e Professores deverão poder consultar a avaliação física mais recente de um aluno da própria academia.

---

## RF-048 — Histórico de avaliações

Administradores e Professores deverão poder consultar o histórico de avaliações físicas de um aluno da própria academia.

---

## RF-049 — Consulta das próprias avaliações

O Aluno deverá poder consultar:

- avaliação mais recente;
- histórico;
- detalhes

de suas próprias avaliações físicas.

---

## RF-050 — Restrição de cadastro pelo Aluno

O Aluno não deverá poder cadastrar sua própria avaliação física.

---

## RF-051 — Cálculo da próxima avaliação

O sistema deverá calcular a próxima avaliação a partir da avaliação mais recente utilizando:

`AssessmentDate + 2 meses`

---

## RF-052 — Reavaliação pendente

O sistema deverá indicar reavaliação pendente quando:

`data atual da academia >= próxima data de avaliação`

---

# Branding

## RF-053 — Branding por academia

O aplicativo deverá permitir apresentar identidade visual específica conforme a academia do usuário autenticado quando existir configuração disponível.

---

## RF-054 — Branding padrão

Quando não existir configuração visual específica para a academia, o aplicativo deverá utilizar a identidade padrão do Avelri.

---

# Navegação e interface

## RF-055 — Navegação por perfil

Após autenticação, o aplicativo deverá direcionar o usuário para a área correspondente ao seu perfil:

- Administrador;
- Professor;
- Aluno.

---

## RF-056 — Ocultação de ações não permitidas

A interface deverá evitar apresentar ações que o perfil autenticado não pode executar.

Essa regra de interface não substitui a autorização da API.

---

## RF-057 — Estados de carregamento, erro e ausência de dados

Telas que dependem de operações assíncronas deverão apresentar estados adequados para:

- carregamento;
- sucesso;
- erro;
- ausência de dados.

---

# Segurança funcional

## RF-058 — Proteção de endpoints

Recursos protegidos deverão exigir autenticação válida.

---

## RF-059 — Autorização por perfil

Operações restritas deverão validar o perfil do usuário no backend.

---

## RF-060 — Proteção dos recursos do próprio Aluno

Operações destinadas ao próprio Aluno deverão resolver seus recursos através da sessão autenticada sempre que aplicável, evitando depender de identificadores fornecidos arbitrariamente pelo cliente.

---

# Fora do escopo funcional atual

Os seguintes itens estão planejados para evoluções posteriores e não fazem parte dos requisitos funcionais da versão atualmente distribuída:

- conclusão individual persistida de exercícios;
- retomada de treino em andamento;
- reordenação interativa de dias e exercícios;
- exclusão automática definitiva de alunos após período prolongado de inatividade;
- vídeos demonstrativos;
- temporizador de descanso;
- evolução de carga;
- gráficos avançados;
- notificações;
- progresso visual avançado.

---

# Documentos relacionados

- `docs/product/overview.md`
- `docs/product/business-rules.md`
- `docs/product/roles-and-permissions.md`
- `docs/qa/test-scenarios.md`
- `docs/technical/api.md`
- `docs/technical/database.md`
