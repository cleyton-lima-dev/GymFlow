# Guia de Homologação — Avelri

**Produto:** Avelri
**Versão em teste:** 1.0.0+3
**Ambiente:** Production
**Distribuição Android:** Teste fechado — Google Play
**Última atualização:** 04/09/2026

---

## 1. Objetivo

Este documento orienta a homologação funcional da versão atual do Avelri.

O objetivo desta etapa é identificar:

- bugs funcionais;
- falhas de navegação;
- comportamentos inesperados;
- problemas de autorização;
- falhas de isolamento entre academias;
- inconsistências visuais;
- problemas de usabilidade;
- falhas nos principais fluxos;
- problemas específicos de dispositivos reais;
- diferenças entre Android e Web quando não forem intencionais.

A homologação complementa, mas não substitui, os testes automatizados existentes no projeto.

---

# 2. Contexto do produto

O Avelri é uma plataforma de gestão de treinos para academias.

Os perfis atuais são:

```text
Administrador
Professor
Aluno
```

O produto é multi-tenant.

Cada usuário pertence ao contexto de uma academia e não deve acessar dados pertencentes a outra.

Documentos de referência:

- `docs/product/overview.md`
- `docs/product/business-rules.md`
- `docs/product/roles-and-permissions.md`

---

# 3. Ambiente de homologação

A homologação Android deve utilizar preferencialmente a versão distribuída através do Teste Fechado da Google Play.

## Android

Versão de referência:

```text
1.0.0+3
```

Package:

```text
com.cleytonlimadev.avelri
```

Distribuição:

```text
Google Play — Closed Testing
```

## Backend

A versão em teste utiliza a API de Production.

## Web

A aplicação Web de Production também pode ser utilizada para testes complementares.

Como Android e Web utilizam a mesma API, diferenças funcionais não intencionais entre as plataformas devem ser investigadas.

---

# 4. Atenção ao uso de Production

A homologação utiliza infraestrutura de Production.

Consequentemente, operações realizadas durante os testes podem persistir dados reais, como:

- cadastro de alunos;
- cadastro de Professores;
- alterações cadastrais;
- ativação e inativação;
- exercícios;
- modelos;
- treinos;
- conclusões de dias;
- avaliações físicas.

Devem ser utilizadas contas e dados destinados à homologação.

Na versão atual não existem endpoints gerais de exclusão física desses recursos.

---

# 5. Contas de teste

Credenciais devem ser fornecidas separadamente ao responsável pela homologação.

Nunca registrar neste documento:

```text
senhas
JWTs
tokens
connection strings
secrets
chaves privadas
identificadores privados de tenants
credenciais reais de clientes
```

Sempre que possível, utilizar:

```text
tenant fictício
+
usuários fictícios
+
dados fictícios
```

---

# 6. Perfis que devem ser testados

Os três perfis precisam ser validados.

## Administrador

Validar principalmente:

- login;
- Home;
- navegação;
- listagem de alunos;
- cadastro de alunos;
- edição de alunos;
- ativação/inativação de alunos;
- listagem de Professores;
- cadastro de Professor;
- banco de exercícios;
- criação e edição de exercícios;
- alteração de status de exercício;
- modelos de treino;
- criação e edição de treinos;
- histórico de treinos;
- avaliações físicas;
- permissões administrativas.

Na versão atual não existe fluxo de:

```text
editar Professor
ativar/desativar Professor
excluir Professor
```

Essas ausências não devem ser registradas como bug sem novo requisito definido.

---

## Professor

Validar principalmente:

- login;
- Home;
- navegação;
- listagem de alunos;
- detalhes de alunos;
- banco de exercícios;
- criação e edição de exercícios;
- modelos de treino;
- criação e edição de modelos;
- alteração de status de modelos;
- criação de treino;
- edição de treino;
- histórico de treinos;
- avaliações físicas.

O Professor não deve:

- cadastrar aluno;
- editar dados cadastrais do aluno;
- ativar ou desativar aluno;
- acessar gerenciamento de Professores;
- cadastrar Professor;
- alterar status de exercício.

---

## Aluno

Validar principalmente:

- login;
- Home;
- dados pessoais;
- treino atual;
- dias do treino;
- exercícios;
- conclusão de um dia;
- histórico;
- avaliação física mais recente;
- histórico de avaliações;
- detalhes das avaliações;
- navegação entre áreas destinadas ao Aluno.

O Aluno não deve:

- acessar funções administrativas;
- criar ou editar o próprio treino;
- registrar a própria avaliação física;
- acessar dados pertencentes a outro aluno.

---

# 7. Autenticação

Validar:

```text
credenciais válidas
→ login aprovado
```

```text
senha inválida
→ acesso negado
```

```text
usuário inexistente
→ acesso negado
```

```text
usuário inativo
→ acesso negado
```

Também validar:

- restauração da sessão após reabrir o aplicativo;
- logout;
- comportamento quando o token deixa de ser válido;
- redirecionamento correto conforme perfil.

O aplicativo deve retornar ao estado não autenticado quando uma sessão válida deixa de existir.

---

# 8. Redirecionamento por perfil

Após autenticação:

```text
Admin
→ /admin
```

```text
Professor
→ /professor
```

```text
Student
→ /student
```

Um perfil não deve conseguir utilizar normalmente a árvore visual pertencente a outro.

Essa validação no Flutter não substitui os testes de autorização da API.

---

# 9. Gestão de alunos

## Admin

Validar:

- listagem;
- paginação;
- busca;
- filtro por status;
- cadastro;
- validações;
- detalhes;
- edição;
- ativação;
- inativação;
- atualização dos dados após retorno à tela anterior.

Busca atual:

```text
nome
e-mail
telefone
```

## Professor

Validar:

- listagem;
- paginação;
- busca;
- filtro;
- detalhes.

Confirmar que não aparecem ações de:

```text
Novo aluno
Editar aluno
Ativar
Desativar
```

---

# 10. Aluno inativo

Após desativar um aluno com Admin:

- o registro deve continuar existindo;
- seus dados anteriores devem permanecer;
- o aluno não deve conseguir realizar login;
- ele deve aparecer corretamente quando o filtro permitir inativos;
- a reativação deve restaurar seu acesso.

A exclusão automática após período prolongado de inatividade ainda não faz parte da versão atual.

---

# 11. Professores

## Admin

Validar:

- acesso através de `Mais`;
- listagem;
- estado vazio;
- cadastro;
- nome;
- e-mail;
- senha;
- tratamento de e-mail duplicado;
- vinculação automática ao mesmo tenant do Admin.

## Professor

Confirmar:

```text
opção Professores
→ não aparece
```

Também validar que acesso direto a operação administrativa seja rejeitado pela API.

---

# 12. Banco de exercícios

Admin e Professor devem conseguir:

- listar;
- paginar;
- cadastrar;
- editar;
- utilizar exercícios em modelos e treinos.

Também validar:

- nome obrigatório;
- grupo muscular obrigatório;
- descrição opcional;
- duplicidade de nome dentro da mesma academia;
- isolamento por tenant.

---

# 13. Status de exercícios

A alteração do status de exercício é exclusiva do:

```text
Admin
```

Validar:

```text
Admin
→ consegue alterar status
```

```text
Professor
→ não possui autorização
```

Essa regra é diferente da alteração de status de modelo de treino.

---

# 14. Busca de exercícios

Esse fluxo possui comportamento conhecido e deve receber atenção especial.

A interface atualmente apresenta:

```text
Buscar exercício ou grupo muscular...
```

Entretanto, a busca textual geral pesquisa somente pelo nome do exercício.

O grupo muscular é tratado por um filtro separado.

Também não existe normalização específica para acentuação.

Exemplo relevante:

```text
biceps
```

pode não produzir o mesmo resultado que:

```text
bíceps
```

Esse comportamento está registrado em:

```text
docs/qa/known-issues.md
```

Não é necessário abrir uma Issue duplicada para o mesmo problema.

---

# 15. Modelos de treino

Admin e Professor devem conseguir:

- listar;
- pesquisar;
- criar;
- abrir detalhes;
- editar;
- ativar/desativar;
- utilizar o modelo na criação de treino.

Validar também:

- nome;
- descrição;
- dias;
- ordem dos dias;
- exercícios;
- ordem dos exercícios;
- séries;
- repetições;
- descanso;
- observações.

Um modelo novo deve possuir ao menos um dia e cada dia deve possuir ao menos um exercício.

---

# 16. Exercícios utilizados em modelos

Durante criação e edição de modelos, validar que:

- exercício pertence à mesma academia;
- exercício está ativo;
- exercício selecionado aparece corretamente;
- séries e repetições são persistidas;
- descanso opcional é preservado;
- observações são preservadas;
- ordem apresentada corresponde à estrutura salva.

---

# 17. Criação de treino

Admin e Professor possuem dois fluxos:

```text
Criar manualmente
```

e:

```text
Usar modelo
```

Validar os dois.

O aluno deve estar:

```text
ativo
```

para receber novo treino.

---

# 18. Criação manual de treino

Validar:

- nome;
- descrição;
- criação de divisões;
- exercícios;
- séries;
- repetições;
- descanso;
- observações;
- persistência;
- retorno à tela de aluno;
- novo treino exibido como atual.

---

# 19. Criação a partir de modelo

Validar:

```text
Selecionar modelo
      │
      ▼
carregar estrutura
      │
      ▼
revisar/configurar
      │
      ▼
criar treino
```

O modelo utilizado deve estar ativo e pertencer à mesma academia.

---

# 20. Substituição de treino atual

O backend suporta substituir um treino ativo por outro.

Quando um novo treino é criado:

```text
treino anterior
→ inativo
```

```text
novo treino
→ ativo
```

Entretanto, existe atualmente uma limitação no Flutter:

```text
Editar treino
```

não oferece diretamente a opção de:

```text
substituir por outro modelo
```

Esse comportamento já está registrado como bug conhecido.

---

# 21. Edição de treino

Admin e Professor devem conseguir alterar:

- nome;
- descrição;
- divisões;
- exercícios.

Validar:

- adicionar divisão;
- editar divisão;
- remover divisão;
- alterar exercícios;
- salvar;
- retornar;
- visualizar dados atualizados.

Quando um treino já possui execuções, a edição deve preservar o histórico existente.

---

# 22. Home do Aluno

Validar:

- branding correto;
- saudação;
- status da avaliação física;
- treino atual;
- nome do treino;
- data de criação;
- quantidade de dias;
- quantidade de exercícios;
- dias concluídos hoje;
- lista dos dias;
- refresh;
- estados de erro;
- ausência de treino.

---

# 23. Card de treino atual

Existe bug conhecido no card:

```text
SEU TREINO ATUAL
```

Na build de referência, o card não possui ação de toque.

Os dias individuais logo abaixo continuam sendo utilizados para abrir o treino.

Esse problema já está registrado em:

```text
docs/qa/known-issues.md
```

---

# 24. Dia de treino do Aluno

Ao selecionar um dia, validar:

- nome do dia;
- posição;
- quantidade de exercícios;
- ordem;
- nome dos exercícios;
- grupo muscular;
- séries;
- repetições;
- descanso;
- observações;
- botão de conclusão.

---

# 25. Conclusão do treino

Na versão atual, a conclusão ocorre no nível do:

```text
WorkoutDay
```

Portanto, o fluxo correto é:

```text
abrir dia
   │
   ▼
executar exercícios
   │
   ▼
Marcar dia como concluído
```

Não existe conclusão individual persistida de exercício na versão atual.

---

# 26. Após concluir um dia

Validar:

- mensagem de conclusão;
- estado visual concluído;
- horário apresentado;
- retorno à Home;
- atualização do indicador;
- registro no histórico;
- impedimento de conclusão duplicada no mesmo dia local da academia.

O mesmo `WorkoutDay` pode possuir no máximo uma execução por data local da academia.

---

# 27. Funcionalidades de execução ainda futuras

Não registrar como bugs pela simples ausência:

```text
conclusão individual persistida de exercícios
retomada de treino em andamento
desfazer exercício individual antes da finalização
```

Esses itens estão planejados para evolução da V1.

---

# 28. Histórico de treino

## Aluno

Validar:

- próprio histórico;
- paginação;
- datas;
- treino;
- dia concluído;
- estados vazios.

## Admin e Professor

Validar histórico do aluno selecionado.

O histórico deve continuar preservado mesmo após substituição ou versionamento de treino.

---

# 29. Avaliações físicas — Admin e Professor

Validar:

- acesso pelo aluno correto;
- cadastro;
- data;
- peso;
- altura;
- percentual de gordura;
- medidas opcionais;
- observações;
- histórico;
- avaliação mais recente;
- detalhes.

O Aluno não possui permissão para cadastrar a própria avaliação.

---

# 30. Data da avaliação física

A data da avaliação:

- é obrigatória;
- não pode ser futura segundo a data local da academia.

O Flutter possui fluxo que consulta a data atual da academia no backend durante o cadastro.

Ao testar alterações de data, considerar o timezone configurado para o tenant.

---

# 31. Unicidade da avaliação

O mesmo aluno não pode possuir:

```text
mesmo StudentId
+
mesma AssessmentDate
```

em duas avaliações.

Tentar registrar novamente uma avaliação na mesma data deve produzir rejeição controlada.

---

# 32. Reavaliação física

A regra atual é:

```text
NextAssessmentDate
=
AssessmentDate + 2 meses
```

E:

```text
reavaliação pendente
=
data atual da academia >= NextAssessmentDate
```

Exemplo:

```text
Avaliação
10/08/2026

Próxima
10/10/2026
```

---

# 33. Avaliações físicas do Aluno

Validar:

- status na Home;
- avaliação mais recente;
- histórico;
- detalhes;
- próxima avaliação;
- indicação de reavaliação.

Confirmar que o Aluno não possui ação para:

```text
Nova avaliação
```

---

# 34. Multi-tenancy

Essa é uma área crítica da homologação.

Quando houver contas adequadas em mais de um tenant, validar que:

```text
Academia A
```

não acessa dados de:

```text
Academia B
```

Os testes devem cobrir, quando possível:

- alunos;
- Professores;
- exercícios;
- modelos;
- treinos;
- avaliações físicas;
- históricos.

---

# 35. Testes de manipulação de identificadores

Quando a homologação técnica permitir chamadas controladas à API, testar alteração manual de identificadores.

Exemplo:

```text
usuário Academia A
+
ID conhecido de recurso da Academia B
```

Resultado esperado:

```text
recurso não exposto
```

Possuir o identificador de um recurso não deve ser suficiente para acessá-lo.

---

# 36. Segurança por perfil

Validar especialmente:

```text
Professor
→ POST /api/students
→ proibido
```

```text
Professor
→ gerenciamento de Professores
→ proibido
```

```text
Professor
→ alterar status de exercício
→ proibido
```

```text
Student
→ endpoints administrativos
→ proibido
```

```text
sem JWT válido
→ endpoint protegido
→ não autorizado
```

---

# 37. Estados de interface

A versão atual já possui diversos estados:

```text
loading
success
empty
error
submitting
refresh
```

Durante a homologação, verificar que:

- loading não bloqueia indefinidamente;
- erro apresenta informação compreensível;
- retry funciona;
- estado vazio não parece falha;
- botões não permitem envio duplicado quando a operação está em andamento.

A melhoria planejada de estados vazios na V1 é um refinamento dos estados existentes, e não uma funcionalidade inexistente.

---

# 38. Responsividade e UI

Validar em dispositivo real:

- textos longos;
- nomes longos;
- e-mails longos;
- tela estreita;
- teclado aberto;
- scroll;
- botões;
- cards;
- diálogos;
- navegação inferior;
- overflow;
- quebra inadequada de texto.

Problemas apenas estéticos devem ser classificados de acordo com o impacto real.

---

# 39. Tela de Professores

Existe correção visual já implementada localmente para a quebra inadequada do título:

```text
Professores
```

A correção deve ser considerada efetivamente homologada somente depois de estar presente em um artefato distribuído e ser retestada.

Não marcar como resolvido para os testadores apenas porque o código local foi corrigido.

---

# 40. Fora do escopo atual

Não devem ser considerados bugs pela ausência, sem requisito adicional:

```text
vídeos demonstrativos
temporizador de descanso
evolução de carga
gráficos avançados
notificações
progresso visual avançado
```

Esses recursos pertencem à evolução planejada do produto.

---

# 41. Melhorias planejadas para V1

Também existem melhorias já planejadas, mas que não representam defeitos da versão atual simplesmente por ainda não existirem:

```text
conclusão individual de exercícios
retomada de treino em andamento
reorganização de dias e exercícios
refinamento de estados vazios
polimento de navegação e ações
política futura de exclusão de alunos após inatividade prolongada
```

Uma Issue de melhoria pode existir para esses itens, mas eles não devem ser classificados automaticamente como `[BUG]`.

---

# 42. Bugs conhecidos

A fonte oficial é:

```text
docs/qa/known-issues.md
```

Na versão atualmente auditada, os principais itens conhecidos são:

```text
1. ajuste visual do título Professores
   → corrigido localmente
   → aguardando distribuição/reteste

2. card Treino atual da Home do Aluno
   → não possui ação ao tocar

3. substituição do treino atual por outro modelo
   → backend suporta
   → fluxo Flutter não expõe diretamente durante edição

4. busca de exercícios
   → busca geral não inclui grupo muscular
   → não há normalização específica de acentuação
```

Antes de abrir nova Issue, verificar se o problema já está registrado.

---

# 43. Quando abrir novo bug

Abrir nova Issue quando:

- o comportamento não estiver em `known-issues`;
- o resultado divergir de uma regra atual;
- existir crash;
- existir perda ou corrupção de dados;
- houver permissão incorreta;
- houver vazamento entre tenants;
- um fluxo esperado estiver bloqueado;
- houver regressão em funcionalidade anteriormente funcional.

---

# 44. Formato do bug

Título:

```text
[BUG] Descrição objetiva do problema
```

Exemplo:

```text
[BUG] Card "Treino atual" da Home do aluno não abre o treino
```

---

# 45. Conteúdo recomendado da Issue

## Problema

Descrever resumidamente o comportamento encontrado.

## Como reproduzir

```text
1. Estado inicial.
2. Ação realizada.
3. Próxima ação.
4. Momento em que ocorre o problema.
```

## Resultado atual

Descrever o que aconteceu.

## Resultado esperado

Descrever o comportamento esperado.

## Ambiente

Informar:

```text
versão/build
plataforma
modelo do aparelho
versão do Android
perfil
rede, quando relevante
```

## Evidência

Quando possível:

```text
screenshot
vídeo
mensagem exibida
horário aproximado
```

Nunca incluir secrets.

---

# 46. Criticidade — Bloqueador

Problema que impede completamente o uso de fluxo essencial ou representa risco grave.

Exemplos:

```text
aplicativo não inicia
login impossível para todos
perda grave de dados
vazamento entre academias
```

Falhas de isolamento ou exposição indevida de dados devem receber prioridade máxima de investigação.

---

# 47. Criticidade — Alta

Funcionalidade essencial ou importante está indisponível e não existe alternativa prática.

Exemplos:

```text
não é possível criar treino
Aluno acessa dados de outro Aluno
Professor executa operação administrativa exclusiva do Admin
```

---

# 48. Criticidade — Média

Existe problema funcional, mas o fluxo ainda pode ser concluído através de alternativa aceitável.

Exemplo:

```text
uma ação direta não funciona,
mas o mesmo objetivo pode ser alcançado por outro caminho
```

---

# 49. Criticidade — Baixa

Problemas com impacto funcional pequeno.

Exemplos:

```text
alinhamento
espaçamento
texto quebrado
mensagem pouco clara
inconsistência visual pequena
```

---

# 50. Evidências e privacidade

Screenshots, vídeos e logs utilizados em Issues devem evitar exposição de:

- senhas;
- JWTs;
- telefone real;
- e-mail real sem necessidade;
- dados físicos reais de clientes;
- IDs privados de tenant;
- secrets;
- connection strings.

Utilizar dados fictícios sempre que possível.

---

# 51. Critério de aprovação de um caso

Um caso pode ser considerado aprovado quando:

- produz o resultado esperado;
- respeita a regra de negócio;
- persiste dados corretamente quando aplicável;
- respeita o perfil;
- respeita o tenant;
- não apresenta erro inesperado;
- possui navegação funcional;
- apresenta feedback adequado;
- não possui problema visual que impeça o uso.

---

# 52. Critério de rejeição

Rejeitar quando houver:

- crash;
- resultado funcional incorreto;
- persistência incorreta;
- perda de dados;
- autorização indevida;
- acesso cross-tenant;
- navegação bloqueada;
- estado inconsistente;
- comportamento contrário à documentação atual;
- problema visual grave que impeça o fluxo.

---

# 53. Resultado esperado versus melhoria

Durante a homologação é importante separar:

```text
Bug
=
comportamento atual deveria funcionar
mas não funciona corretamente
```

de:

```text
Melhoria
=
produto funciona conforme a regra atual,
mas poderia oferecer experiência melhor
```

e de:

```text
Funcionalidade futura
=
recurso ainda não pertence ao contrato atual
```

Essa distinção evita registrar itens de roadmap como defeitos.

---

# 54. Workflow das Issues

O projeto utiliza:

```text
Backlog
   ↓
In Progress
   ↓
Testing
   ↓
Done
```

Uma Issue permanece aberta durante:

```text
Testing
```

Ela deve ser fechada somente depois de:

1. correção implementada;
2. testes técnicos aprovados;
3. artefato publicado, quando necessário;
4. reteste realizado;
5. correção confirmada;
6. item movido para `Done`.

---

# 55. Correção que exige nova build

Quando o problema está no Flutter:

```text
correção local
     │
     ▼
flutter analyze
     │
     ▼
flutter test
     │
     ▼
novo build quando necessário
     │
     ▼
distribuição
     │
     ▼
reteste
     │
     ▼
Done
```

Uma correção local não deve ser considerada homologada antes do reteste no artefato relevante.

---

# 56. Correção somente no backend

Quando a correção é exclusivamente no servidor:

```text
correção
   │
   ▼
testes backend
   │
   ▼
deploy Fly.io
   │
   ▼
health
   │
   ▼
reteste funcional
   │
   ▼
Done
```

Um novo AAB não é obrigatório quando o contrato do cliente continua compatível.

---

# 57. Resultado final da homologação

Ao final do ciclo, espera-se possuir:

- fluxos críticos validados;
- bugs encontrados registrados;
- bugs críticos corrigidos;
- correções retestadas;
- permissões verificadas;
- multi-tenancy validado;
- funcionamento em dispositivo real validado;
- evidências suficientes;
- known issues atualizado;
- versão com estabilidade adequada para o próximo estágio de distribuição.

---

# 58. Documentos relacionados

- `docs/product/overview.md`
- `docs/product/business-rules.md`
- `docs/product/roles-and-permissions.md`
- `docs/qa/test-scenarios.md`
- `docs/qa/known-issues.md`
- `docs/technical/api.md`
- `docs/technical/mobile.md`
- `docs/operations/environments.md`
- `docs/operations/deployment.md`
