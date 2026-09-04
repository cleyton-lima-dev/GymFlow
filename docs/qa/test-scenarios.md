# Cenários de Teste — Avelri

**Produto:** Avelri
**Versão de referência:** 1.0.0+3
**Ambiente:** Production — Closed Testing
**Última atualização:** 04/09/2026

---

## Objetivo

Este documento reúne os principais cenários funcionais, de autorização, segurança e multi-tenancy para homologação do Avelri.

Cada cenário pode conter:

- identificador;
- perfil;
- pré-condição;
- passos;
- resultado esperado;
- observação, quando aplicável.

Os cenários devem ser executados preferencialmente com contas e dados fictícios destinados à homologação.

---

# 1. Autenticação

## AUTH-001 — Login válido como Administrador

**Perfil:** Administrador

### Pré-condição

Existir uma conta de Administrador ativa.

### Passos

1. Abrir o Avelri.
2. Informar e-mail válido.
3. Informar senha válida.
4. Entrar.

### Resultado esperado

- login realizado com sucesso;
- usuário direcionado para `/admin`;
- dados pertencentes à academia correta carregados;
- branding correspondente carregado, quando configurado.

---

## AUTH-002 — Login válido como Professor

**Perfil:** Professor

### Resultado esperado

- login realizado;
- usuário direcionado para `/professor`;
- funções exclusivas de Administrador não são exibidas.

---

## AUTH-003 — Login válido como Aluno

**Perfil:** Aluno

### Resultado esperado

- login realizado;
- usuário direcionado para `/student`;
- somente recursos destinados ao Aluno são exibidos.

---

## AUTH-004 — Senha inválida

### Passos

1. Informar e-mail válido.
2. Informar senha incorreta.
3. Tentar entrar.

### Resultado esperado

- acesso negado;
- mensagem adequada apresentada;
- usuário permanece fora da aplicação;
- nenhuma informação sensível é exposta.

---

## AUTH-005 — Usuário inexistente

### Resultado esperado

- acesso negado;
- nenhuma informação interna do sistema é exposta;
- resposta não permite distinguir indevidamente detalhes internos da autenticação.

---

## AUTH-006 — Usuário inativo não consegue autenticar

### Pré-condição

Existir um usuário anteriormente ativo e posteriormente inativado.

### Resultado esperado

- login rejeitado;
- usuário não recebe nova sessão válida.

---

## AUTH-007 — Sessão existente é restaurada

### Pré-condição

Usuário autenticado com token ainda válido armazenado no dispositivo.

### Passos

1. Fechar o aplicativo.
2. Abrir novamente.
3. Aguardar o bootstrap.

### Resultado esperado

- token é lido do armazenamento seguro;
- `/api/auth/me` é consultado;
- sessão válida é restaurada;
- usuário é direcionado ao perfil correto.

---

## AUTH-008 — Token de usuário posteriormente inativado

### Pré-condição

1. Usuário possui token válido.
2. Usuário é inativado pelo Administrador.

### Resultado esperado

- nova requisição autenticada é rejeitada;
- sessão do Flutter é invalidada;
- token local é removido;
- usuário retorna ao fluxo de login.

---

## AUTH-009 — Logout

### Passos

1. Entrar normalmente.
2. Executar logout.

### Resultado esperado

- token removido;
- usuário removido da sessão;
- aplicação retorna ao login;
- áreas autenticadas não permanecem acessíveis.

---

# 2. Administrador

## ADM-001 — Visualizar alunos

**Perfil:** Administrador

### Resultado esperado

- lista apresenta somente alunos da própria academia;
- paginação funciona;
- filtros funcionam.

---

## ADM-002 — Cadastrar aluno

**Perfil:** Administrador

### Passos

1. Abrir Alunos.
2. Selecionar cadastro de novo aluno.
3. Preencher os campos obrigatórios.
4. Salvar.

### Resultado esperado

- aluno criado;
- `User` criado com perfil `Student`;
- aluno vinculado automaticamente à academia do Administrador;
- novo cadastro aparece na listagem.

---

## ADM-003 — Cadastro de aluno com dados inválidos

### Resultado esperado

- cadastro não é concluído;
- validações são apresentadas;
- nenhum estado parcialmente persistido deve permanecer.

---

## ADM-004 — Editar aluno

### Passos

1. Abrir os detalhes de um aluno.
2. Selecionar edição.
3. Alterar dados válidos.
4. Salvar.

### Resultado esperado

- dados persistidos;
- tela anterior apresenta dados atualizados após retorno.

---

## ADM-005 — Inativar aluno

### Resultado esperado

- aluno permanece cadastrado;
- status passa para inativo;
- aluno deixa de conseguir autenticar;
- histórico existente permanece preservado.

---

## ADM-006 — Reativar aluno

### Pré-condição

Aluno inativo.

### Resultado esperado

- status volta para ativo;
- dados históricos permanecem;
- autenticação volta a ser permitida com credenciais válidas.

---

## ADM-007 — Visualizar Professores

### Resultado esperado

- opção Professores disponível;
- listagem contém somente Professores da própria academia.

---

## ADM-008 — Cadastrar Professor

### Passos

1. Abrir Mais.
2. Abrir Professores.
3. Selecionar Novo professor.
4. Informar nome, e-mail e senha válidos.
5. Confirmar.

### Resultado esperado

- Professor criado;
- `Role = Professor`;
- usuário ativo;
- Professor vinculado automaticamente à mesma academia do Administrador;
- novo Professor aparece na listagem.

---

## ADM-009 — Cadastrar Professor com e-mail já utilizado

### Resultado esperado

- operação rejeitada;
- mensagem adequada;
- nenhum usuário duplicado criado.

---

## ADM-010 — Ausência de edição/status de Professor

### Resultado esperado

Na versão atual não devem existir ações de:

- editar Professor;
- ativar/desativar Professor;
- excluir Professor.

A ausência desses recursos não deve ser registrada como bug sem novo requisito.

---

# 3. Professor

## PROF-001 — Visualizar alunos

**Perfil:** Professor

### Resultado esperado

- Professor visualiza somente alunos da própria academia.

---

## PROF-002 — Não visualizar gerenciamento de Professores

### Passos

1. Entrar como Professor.
2. Abrir Mais.

### Resultado esperado

- opção Professores não é exibida.

---

## PROF-003 — Não cadastrar Professor pela API

### Resultado esperado

Ao tentar executar uma operação protegida equivalente a:

```http
POST /api/auth/register
```

o Professor deve receber acesso negado.

---

## PROF-004 — Não listar Professores pela API

### Resultado esperado

```http
GET /api/auth/professors
```

deve ser rejeitado para Professor.

---

## PROF-005 — Cadastro de aluno indisponível

### Resultado esperado

- opção Novo aluno não aparece;
- Professor não consegue cadastrar aluno pela API.

---

## PROF-006 — Edição de aluno indisponível

### Resultado esperado

Professor não deve possuir ações para:

- editar cadastro;
- ativar aluno;
- desativar aluno.

Tentativas equivalentes pela API devem ser rejeitadas.

---

# 4. Banco de exercícios

## EXE-001 — Visualizar exercícios

**Perfil:** Administrador ou Professor

### Resultado esperado

- exercícios da academia são carregados;
- paginação funciona;
- filtros funcionam;
- exercícios de outros tenants não aparecem.

---

## EXE-002 — Cadastrar exercício

### Passos

1. Abrir Banco de exercícios.
2. Selecionar Novo exercício.
3. Preencher nome e grupo muscular.
4. Salvar.

### Resultado esperado

- exercício criado;
- exercício pertence à academia autenticada;
- fica disponível para uso enquanto ativo.

---

## EXE-003 — Nome duplicado na mesma academia

### Pré-condição

Existir exercício com determinado nome.

### Passos

Cadastrar outro exercício com o mesmo nome alterando apenas maiúsculas/minúsculas.

### Resultado esperado

- operação rejeitada;
- duplicidade não é criada.

Exemplo:

```text
Supino Reto
supino reto
```

devem ser considerados equivalentes para unicidade.

---

## EXE-004 — Editar exercício

### Resultado esperado

- alterações persistidas;
- dados atualizados exibidos após recarregar.

---

## EXE-005 — Alterar status como Administrador

**Perfil:** Administrador

### Resultado esperado

- Admin consegue ativar ou desativar exercício.

---

## EXE-006 — Professor não altera status de exercício

**Perfil:** Professor

### Resultado esperado

- alteração não é disponibilizada ou é rejeitada;
- API não permite a operação.

---

## EXE-007 — Buscar exercício por nome

### Resultado esperado

- exercícios cujo nome corresponda ao termo são retornados.

---

## EXE-008 — Buscar exercício usando texto de grupo muscular no campo geral

### Exemplo

Pesquisar no campo:

```text
peitoral
```

quando o termo existe apenas em `MuscleGroup`.

### Resultado esperado desejado

- a busca deveria encontrar exercícios relacionados.

### Comportamento conhecido atual

O campo geral envia apenas:

```text
search
```

e o backend pesquisa somente:

```text
Exercise.Name
```

### Observação

Bug conhecido.

---

## EXE-009 — Filtrar explicitamente por grupo muscular

### Resultado esperado

- filtro específico de grupo muscular deve funcionar conforme o valor cadastrado.

---

## EXE-010 — Busca sem acentuação

### Exemplo

Pesquisar:

```text
biceps
```

quando existir:

```text
bíceps
```

### Resultado esperado desejado

- busca deveria funcionar independentemente da acentuação.

### Comportamento conhecido atual

Não existe normalização específica de acentos.

### Observação

Bug conhecido.

---

# 5. Modelos de treino

## TPL-001 — Criar modelo

**Perfil:** Administrador ou Professor

### Resultado esperado

- modelo criado;
- dias persistidos;
- exercícios persistidos;
- modelo disponível na listagem.

---

## TPL-002 — Modelo sem dias

### Resultado esperado

- criação rejeitada.

Um modelo precisa possuir pelo menos um dia.

---

## TPL-003 — Dia do modelo sem exercícios

### Resultado esperado

- criação ou edição rejeitada.

Cada dia deve possuir pelo menos um exercício.

---

## TPL-004 — Editar modelo

### Resultado esperado

- alterações persistidas;
- estrutura atualizada corretamente.

---

## TPL-005 — Alterar status do modelo como Admin

### Resultado esperado

- alteração permitida.

---

## TPL-006 — Alterar status do modelo como Professor

### Resultado esperado

- alteração permitida.

Diferentemente de exercícios, Professor possui permissão para alterar status de modelo.

---

## TPL-007 — Modelo inativo não deve ser usado na criação de novo treino

### Pré-condição

Modelo inativo.

### Resultado esperado

- modelo não é disponibilizado como opção ativa;
- tentativa direta de utilização deve ser rejeitada pelo backend.

---

## TPL-008 — Utilizar modelo para criar treino

### Passos

1. Abrir um aluno ativo.
2. Iniciar criação de treino.
3. Selecionar Usar modelo.
4. Escolher modelo ativo.
5. Revisar a estrutura.
6. Concluir criação.

### Resultado esperado

- estrutura do modelo é utilizada;
- treino é criado para o aluno correto;
- dias e exercícios permanecem coerentes.

---

# 6. Treinos

## WKT-001 — Criar treino manualmente

**Perfil:** Administrador ou Professor

### Resultado esperado

- treino criado para o aluno selecionado;
- dias e exercícios persistidos;
- treino exibido como ativo.

---

## WKT-002 — Criar treino a partir de modelo

### Resultado esperado

- modelo ativo utilizado como origem;
- treino vinculado ao aluno correto;
- treino pertence ao mesmo tenant.

---

## WKT-003 — Não criar treino para aluno inativo

### Pré-condição

Aluno inativo.

### Resultado esperado

- criação rejeitada.

---

## WKT-004 — Editar treino atual sem execuções

### Resultado esperado

- alterações são persistidas;
- treino permanece ativo;
- estrutura atualizada aparece ao reabrir.

---

## WKT-005 — Editar treino atual que já possui histórico

### Pré-condição

O treino atual possui pelo menos uma execução.

### Resultado esperado

- versão histórica não é alterada;
- treino anterior é desativado;
- nova versão ativa é criada;
- histórico anterior permanece acessível.

---

## WKT-006 — Criar novo treino quando já existe treino ativo

### Pré-condição

Aluno possui treino ativo.

### Resultado esperado

- treino anterior passa para inativo;
- novo treino passa a ser o único ativo;
- operação não deixa dois treinos ativos.

---

## WKT-007 — Substituir treino atual diretamente por outro modelo durante edição

### Resultado esperado desejado

A interface deveria oferecer fluxo direto para utilizar outro modelo.

### Comportamento atual

`EditWorkoutPage` não oferece seleção direta de outro modelo.

O backend já suporta substituição quando um novo treino é criado.

### Observação

Bug conhecido de fluxo Flutter.

---

## WKT-008 — Exclusão de treino

### Resultado esperado

Na versão atual não existe fluxo nem endpoint geral para:

```text
DELETE Workout
```

A ausência de exclusão não deve ser classificada como bug.

Treinos anteriores são preservados através de inativação/versionamento.

---

# 7. Home do Aluno

## STU-001 — Visualizar treino atual

**Perfil:** Aluno

### Resultado esperado

- card apresenta o treino do próprio aluno;
- dados do treino estão corretos;
- dias disponíveis aparecem ordenados.

---

## STU-002 — Acessar treino através do card principal

### Resultado esperado desejado

O card deve possuir ação coerente que leve o aluno ao conteúdo do treino atual.

### Comportamento conhecido atual

O card não possui ação de toque.

### Observação

Bug conhecido.

---

## STU-003 — Selecionar um dia

### Passos

1. Abrir Home.
2. Localizar os dias do treino.
3. Selecionar um dia.

### Resultado esperado

- `StudentWorkoutDayPage` é aberta;
- dia correto é carregado;
- exercícios corretos aparecem.

---

## STU-004 — Aluno sem treino ativo

### Resultado esperado

- nenhum erro inesperado;
- estado vazio adequado;
- aplicação permanece utilizável.

---

## STU-005 — Refresh da Home

### Resultado esperado

- dados são carregados novamente;
- aplicação permanece estável;
- alterações recentes aparecem.

---

# 8. Execução de treino

## RUN-001 — Visualizar exercícios do dia

### Resultado esperado

- exercícios aparecem na ordem correta;
- séries corretas;
- repetições corretas;
- descanso correto, quando informado;
- observações corretas, quando informadas.

---

## RUN-002 — Concluir dia de treino

**Perfil:** Aluno

### Resultado esperado

- `WorkoutExecution` é criada para o dia;
- feedback visual é apresentado;
- Home reflete a conclusão;
- histórico passa a conter a execução.

---

## RUN-003 — Concluir o mesmo dia duas vezes na mesma data local

### Pré-condição

Dia já concluído na data atual da academia.

### Resultado esperado

- segunda conclusão não gera novo registro;
- operação é rejeitada de forma controlada.

A unicidade é:

```text
WorkoutDayId + ExecutionDate
```

---

## RUN-004 — Data da execução respeita timezone da academia

### Resultado esperado

- `ExecutionDate` corresponde à data local configurada para a academia;
- comportamento não depende simplesmente da data UTC do servidor.

---

## RUN-005 — Horário exibido após conclusão

### Resultado esperado

- horário exibido ao usuário é coerente com o offset retornado para a academia.

---

## RUN-006 — Conclusão individual de exercícios

### Resultado esperado atual

Não existe conclusão persistida individual por exercício.

A conclusão atual ocorre por:

```text
WorkoutDay
```

### Observação

Funcionalidade individual planejada para evolução da V1.

---

# 9. Histórico

## HIST-001 — Consultar próprio histórico

**Perfil:** Aluno

### Resultado esperado

- somente o histórico do próprio aluno é exibido.

---

## HIST-002 — Dia concluído aparece no histórico

### Pré-condição

Concluir um dia.

### Resultado esperado

- execução aparece com informações coerentes.

---

## HIST-003 — Histórico vazio

### Resultado esperado

- nenhum erro;
- estado vazio apresentado adequadamente.

---

## HIST-004 — Histórico preservado após novo treino

### Pré-condição

1. Aluno possui execução registrada.
2. Novo treino é criado para o mesmo aluno.

### Resultado esperado

- histórico anterior permanece visível;
- novo treino passa a ser o ativo.

---

## HIST-005 — Admin consulta histórico de aluno

### Resultado esperado

- histórico do aluno correto carregado;
- dados de outro tenant não aparecem.

---

## HIST-006 — Professor consulta histórico de aluno

### Resultado esperado

- operação permitida;
- somente aluno da própria academia acessível.

---

# 10. Avaliações físicas

## PA-001 — Registrar avaliação física

**Perfil:** Administrador ou Professor

### Resultado esperado

- avaliação criada para o aluno correto;
- campos persistidos.

---

## PA-002 — Campos opcionais

### Resultado esperado

- avaliação pode ser salva sem preencher medidas opcionais.

---

## PA-003 — Data futura

### Passos

Tentar registrar avaliação com data posterior à data atual da academia.

### Resultado esperado

- operação rejeitada.

---

## PA-004 — Duas avaliações na mesma data

### Resultado esperado

- segunda avaliação para o mesmo aluno e mesma data rejeitada.

Regra:

```text
StudentId + AssessmentDate
```

---

## PA-005 — Consultar avaliação mais recente

### Resultado esperado

- avaliação de data mais recente utilizada como referência atual.

---

## PA-006 — Calcular próxima avaliação

### Exemplo

```text
AssessmentDate
10/08/2026
```

### Resultado esperado

```text
NextAssessmentDate
10/10/2026
```

---

## PA-007 — Reavaliação pendente

### Pré-condição

Data local da academia igual ou posterior à próxima avaliação.

### Resultado esperado

```text
IsReassessmentDue = true
```

---

## PA-008 — Reavaliação ainda não pendente

### Pré-condição

Data local anterior à próxima avaliação.

### Resultado esperado

```text
IsReassessmentDue = false
```

---

## PA-009 — Aluno consulta próprias avaliações

**Perfil:** Aluno

### Resultado esperado

- somente avaliações do próprio aluno são exibidas.

---

## PA-010 — Aluno não registra avaliação própria

### Resultado esperado

- interface não apresenta ação;
- endpoint administrativo não pode ser utilizado pelo Aluno.

---

## PA-011 — Data atual da academia

**Perfil:** Administrador ou Professor

### Resultado esperado

O endpoint de data atual deve devolver a data correspondente ao timezone configurado para a academia.

---

# 11. Multi-tenancy

Os cenários desta seção possuem prioridade crítica.

## TEN-001 — Admin não visualiza alunos de outra academia

### Pré-condição

Existirem pelo menos dois tenants com dados distintos.

### Resultado esperado

- Admin da Academia A visualiza somente alunos da Academia A.

---

## TEN-002 — Professor não visualiza alunos de outra academia

### Resultado esperado

- nenhum aluno da outra academia é retornado.

---

## TEN-003 — Professores isolados

### Resultado esperado

- Admin lista somente Professores da própria academia.

---

## TEN-004 — Exercícios isolados

### Resultado esperado

- exercícios de outro tenant não aparecem.

---

## TEN-005 — Modelos isolados

### Resultado esperado

- modelos de outro tenant não aparecem;
- não podem ser utilizados indevidamente.

---

## TEN-006 — Treinos isolados

### Resultado esperado

- Admin/Professor não conseguem consultar ou modificar treino pertencente a aluno de outro tenant.

---

## TEN-007 — Avaliações físicas isoladas

### Resultado esperado

- avaliações de outro tenant não são retornadas.

---

## TEN-008 — Históricos isolados

### Resultado esperado

- histórico de aluno de outra academia não é retornado.

---

## TEN-009 — Manipular StudentId de outro tenant

### Passos

1. Autenticar na Academia A.
2. Utilizar manualmente `StudentId` pertencente à Academia B.

### Resultado esperado

- recurso da Academia B não é exposto.

---

## TEN-010 — Manipular ExerciseId de outro tenant

### Resultado esperado

- exercício da outra academia não pode ser usado indevidamente em treino ou modelo.

---

## TEN-011 — Manipular TemplateId de outro tenant

### Resultado esperado

- modelo de outra academia não pode ser utilizado para criar treino.

---

## TEN-012 — Aluno não acessa recurso de outro aluno

### Resultado esperado

Endpoints `/me` devem resolver o próprio aluno através da sessão autenticada.

O Aluno não escolhe arbitrariamente outro `StudentId`.

---

# 12. Segurança e autorização

## SEC-001 — Endpoint protegido sem autenticação

### Resultado esperado

```text
401 Unauthorized
```

e nenhum dado protegido retornado.

---

## SEC-002 — Token inválido

### Resultado esperado

- acesso negado;
- nenhum dado protegido retornado.

---

## SEC-003 — Token expirado

### Resultado esperado

- acesso negado;
- sessão do cliente deve ser invalidada quando aplicável.

---

## SEC-004 — Perfil sem permissão

### Exemplo

```text
Professor
→ POST /api/students
```

### Resultado esperado

```text
403 Forbidden
```

---

## SEC-005 — Professor não altera status de exercício

### Resultado esperado

- operação rejeitada.

---

## SEC-006 — Professor pode alterar status de modelo

### Resultado esperado

- operação permitida.

Esse cenário ajuda a validar que as duas regras não foram confundidas.

---

## SEC-007 — Aluno não acessa endpoints administrativos

### Resultado esperado

- operação rejeitada.

---

## SEC-008 — Dados sensíveis nas respostas

### Resultado esperado

Respostas não devem expor:

- `PasswordHash`;
- signing key;
- connection string;
- secrets;
- credenciais;
- dados pertencentes a outro tenant.

---

## SEC-009 — Rate limit do login

### Pré-condição

Executar tentativas suficientes para exceder o limite permitido pelo mesmo IP.

### Resultado esperado

Após o limite configurado:

```text
429 Too Many Requests
```

A configuração atual é:

```text
10 tentativas por minuto por IP
```

---

# 13. Navegação e interface

## UI-001 — Botões de retorno

### Resultado esperado

- retorno coerente;
- usuário não fica preso em fluxo.

---

## UI-002 — Elementos visualmente clicáveis

### Resultado esperado

Elementos que indicam claramente ação devem responder de forma coerente.

### Observação

O card principal de treino atual possui bug conhecido e deve ser registrado como reprodução do item existente.

---

## UI-003 — Layout em dispositivo real

### Validar

- textos;
- botões;
- cards;
- espaçamentos;
- overflow;
- quebra de linha;
- teclado;
- scroll.

### Resultado esperado

Nenhum conteúdo essencial fica cortado ou inutilizável.

---

## UI-004 — Tela de Professores

### Resultado esperado desejado

O título:

```text
Professores
```

não deve quebrar de forma inadequada.

### Observação

A correção já existe localmente, mas só pode ser marcada como homologada depois de distribuída e retestada.

---

## UI-005 — Estados de carregamento

### Resultado esperado

- indicador adequado;
- interface não parece congelada;
- envio duplicado é evitado quando aplicável.

---

## UI-006 — Estados vazios

### Resultado esperado

- ausência de dados não causa erro;
- mensagem contextual é exibida.

A melhoria prevista para V1 é de refinamento desses estados.

---

## UI-007 — Mensagens de erro

### Resultado esperado

- mensagens compreensíveis;
- ausência de stack trace;
- ausência de conteúdo técnico sensível.

---

## UI-008 — Refresh

### Resultado esperado

- dados são carregados novamente;
- interface permanece utilizável em caso de erro;
- alterações recentes aparecem.

---
## UI-009 — Contraste e estado visual dos botões

### Resultado esperado

Botões habilitados devem apresentar:

- texto claramente legível;
- contraste adequado com o fundo;
- aparência visual compatível com estado ativo;
- diferenciação clara em relação ao estado desabilitado.

### Validar

Verificar especialmente:

- botões com cores de destaque;
- botões primários;
- botões secundários;
- branding de diferentes academias;
- estado habilitado;
- estado desabilitado;
- Android em dispositivo real;
- Web, quando o mesmo componente for utilizado.

### Comportamento conhecido atual

Em alguns botões da versão distribuída, o texto pode apresentar aparência excessivamente opaca, fazendo o botão parecer desabilitado mesmo estando funcional.

### Observação

Bug conhecido:

```text
KI-005
```

Detalhes:

```text
docs/qa/known-issues.md
```

---

# 14. Funcionalidades futuras — não reprovar

Os itens abaixo **não devem ser tratados como cenário reprovado apenas porque ainda não existem**:

```text
conclusão individual persistida de exercícios
retomada de treino em andamento
reordenação interativa de dias/exercícios
vídeos demonstrativos
temporizador de descanso
evolução de carga
gráficos avançados
notificações
progresso visual avançado
exclusão automática futura de aluno por inatividade
```

Podem existir Issues de melhoria ou roadmap associadas a esses recursos.

---

# 15. Bugs conhecidos de referência

Durante esta versão, já existem itens conhecidos relacionados a:

```text
BUG 1
Título "Professores" quebra incorretamente
→ corrigido localmente
→ aguardando distribuição/reteste

BUG 2
Card "Treino atual" da Home do Aluno não abre o treino

BUG 3
Fluxo de edição não oferece substituição direta do treino por outro modelo

BUG 4
Busca geral de exercícios não inclui grupo muscular
e não normaliza acentuação
```

Detalhes:

```text
docs/qa/known-issues.md
```

Não criar Issue duplicada para o mesmo comportamento.

---

# 16. Registro do resultado

Para cada cenário:

```text
✅ Aprovado

❌ Reprovado

⚠️ Aprovado com observação

⏸️ Bloqueado

➖ Não aplicável
```

Um cenário reprovado deve:

1. ser associado a uma Issue já existente; ou
2. gerar uma nova Issue caso represente um novo bug.

---

# 17. Evidência recomendada

Ao reprovar um caso, registrar quando possível:

- versão/build;
- perfil utilizado;
- modelo do aparelho;
- Android;
- passos de reprodução;
- resultado atual;
- resultado esperado;
- screenshot;
- vídeo;
- horário aproximado.

Não incluir:

- senha;
- JWT;
- secret;
- connection string;
- dados pessoais reais desnecessários;
- identificadores privados de tenant.

---

# 18. Reteste

Quando uma correção entrar em:

```text
Testing
```

executar novamente o mesmo cenário.

A Issue somente deve ser movida para:

```text
Done
```

e fechada depois que o resultado esperado for confirmado no artefato adequado.

---

# 19. Documentos relacionados

- `docs/qa/homologation-guide.md`
- `docs/qa/known-issues.md`
- `docs/product/business-rules.md`
- `docs/product/roles-and-permissions.md`
- `docs/technical/api.md`
- `docs/technical/mobile.md`
