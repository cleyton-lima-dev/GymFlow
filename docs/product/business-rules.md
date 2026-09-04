# Regras de Negócio — Avelri

**Produto:** Avelri
**Versão de referência:** 1.0.0+3
**Última atualização:** 04/09/2026

---

## Objetivo

Este documento registra as principais regras de negócio implementadas na versão atual do Avelri.

As regras descritas aqui representam o comportamento esperado do sistema durante a homologação.

Funcionalidades planejadas, mas ainda não implementadas, são identificadas explicitamente como futuras.

---

# 1. Academias e multi-tenancy

## 1.1 Isolamento por academia

O Avelri suporta múltiplas academias utilizando a mesma aplicação.

Cada academia é identificada por um `GymId`.

Usuários e dados pertencentes a uma academia não devem ser acessíveis por usuários de outra academia.

O `GymId` utilizado nas operações protegidas é obtido a partir do contexto autenticado do usuário e não deve ser escolhido arbitrariamente pelo cliente.

O isolamento é aplicado conforme a estrutura de cada recurso.

Exemplos:

- usuários possuem `GymId`;
- exercícios possuem `GymId`;
- modelos de treino possuem `GymId`;
- treinos possuem `GymId`;
- alunos são relacionados à academia através de `Student -> User -> GymId`;
- avaliações físicas são relacionadas à academia através de `PhysicalAssessment -> Student -> User -> GymId`;
- execuções de treino são relacionadas através de `WorkoutExecution -> WorkoutDay -> Workout -> GymId`.

---

## 1.2 Identidade visual

O aplicativo pode apresentar identidade visual específica para determinada academia.

Na implementação atual, o branding conhecido pelo aplicativo é associado localmente ao `GymId`.

Quando não existe uma configuração específica para a academia autenticada, deve ser utilizada a identidade visual padrão do Avelri.

A existência de uma identidade visual específica não altera as regras de autenticação, autorização ou isolamento de dados.

---

## 1.3 Fuso horário da academia

Operações dependentes de data utilizam o fuso horário configurado para a academia.

A configuração segue a chave:

`GymTimeZones:{GymId}`

O fuso horário é utilizado, entre outros casos, para:

- determinar a data local de uma execução de treino;
- determinar se um dia já foi concluído hoje;
- validar a data de uma avaliação física;
- calcular o estado de reavaliação física.

A ausência de configuração de fuso horário para uma academia impede corretamente operações que dependam dessa informação.

---

# 2. Perfis de acesso

O sistema possui três perfis:

- Administrador;
- Professor;
- Aluno.

Cada perfil possui permissões específicas.

As permissões detalhadas estão documentadas em:

`docs/product/roles-and-permissions.md`

---

# 3. Administrador

O Administrador possui acesso às funções administrativas da própria academia.

Entre as operações disponíveis estão:

- consultar alunos;
- cadastrar alunos;
- editar dados de alunos;
- ativar e desativar alunos;
- listar professores;
- cadastrar professores;
- consultar e gerenciar exercícios conforme suas permissões;
- consultar e gerenciar modelos de treino;
- criar e editar treinos de alunos;
- consultar históricos de treino;
- consultar e registrar avaliações físicas.

Na versão atual, não existe operação para editar, ativar ou desativar professores já cadastrados.

Um Administrador não deve acessar dados pertencentes a outra academia.

---

# 4. Professor

O Professor atua principalmente nos processos técnicos relacionados aos alunos.

Entre as operações disponíveis estão:

- consultar alunos;
- acessar detalhes dos alunos;
- consultar, cadastrar e editar exercícios;
- consultar e gerenciar modelos de treino;
- criar e editar treinos;
- consultar históricos de treino;
- consultar e registrar avaliações físicas.

O Professor não pode:

- cadastrar alunos;
- editar dados cadastrais de alunos;
- ativar ou desativar alunos;
- listar ou cadastrar professores através da área administrativa;
- ativar ou desativar exercícios;
- acessar dados de outra academia.

---

# 5. Aluno

O Aluno possui acesso aos próprios recursos.

Entre as operações disponíveis estão:

- visualizar os próprios dados pessoais;
- visualizar o próprio treino atual;
- visualizar os dias do treino;
- visualizar os exercícios de cada dia;
- marcar um dia de treino como concluído;
- consultar o próprio histórico de execuções;
- consultar a própria avaliação física mais recente;
- consultar o próprio histórico de avaliações físicas;
- consultar detalhes das próprias avaliações.

O Aluno não pode:

- acessar áreas administrativas;
- consultar dados de outro aluno;
- criar ou editar o próprio treino;
- registrar a própria avaliação física;
- cadastrar ou editar recursos administrativos.

---

# 6. Usuários e autenticação

## 6.1 E-mail

O e-mail é utilizado para autenticação.

Antes da persistência, o e-mail é normalizado com remoção de espaços externos e conversão para letras minúsculas.

Na implementação atual, o e-mail possui unicidade global.

Isso significa que o mesmo endereço de e-mail não pode estar associado simultaneamente a usuários de academias diferentes.

---

## 6.2 Senha

A senha deve possuir:

- mínimo de 8 caracteres;
- máximo de 128 caracteres.

Senhas não são armazenadas em texto puro.

O backend armazena somente o hash da senha utilizando o mecanismo de `PasswordHasher` do ASP.NET Core Identity.

---

## 6.3 Usuário ativo

Somente usuários ativos podem realizar login.

Além disso, a validade da sessão não depende apenas de o JWT ainda estar dentro do prazo de expiração.

Durante a validação de um token autenticado, o backend verifica se o usuário identificado por `UserId + GymId` continua existente e ativo.

Assim, a inativação do usuário invalida o uso subsequente de tokens anteriormente emitidos.

---

# 7. Alunos

## 7.1 Cadastro

O cadastro de novos alunos é exclusivo do Administrador.

Ao cadastrar um aluno, o sistema cria:

- um registro `User` com perfil `Student`;
- um registro `Student` vinculado a esse usuário.

O aluno recebe automaticamente o mesmo `GymId` do Administrador autenticado.

O cliente não pode informar outro `GymId` para esse cadastro.

---

## 7.2 Dados do aluno

O cadastro pode conter:

- nome;
- e-mail;
- senha inicial;
- telefone;
- data de nascimento.

Nome e e-mail possuem limites definidos pela política de persistência.

O telefone, quando informado, possui limite máximo de 20 caracteres.

---

## 7.3 Ativação e inativação

Somente o Administrador pode ativar ou desativar um aluno.

A inativação altera o estado do usuário relacionado ao aluno.

Um aluno inativo:

- permanece registrado;
- mantém seus dados;
- não consegue realizar login;
- não pode receber um novo treino;
- não pode registrar execução de treino.

A reativação restaura o acesso sem excluir os dados já existentes.

---

## 7.4 Exclusão futura por inatividade

A exclusão automática após 90 dias consecutivos de inatividade está planejada como melhoria da versão 1 e ainda não faz parte do comportamento atual.

A implementação futura deverá considerar os relacionamentos históricos existentes antes de realizar exclusão definitiva.

Na estrutura atual, treinos relacionados ao aluno possuem restrição de exclusão, portanto uma remoção física simples do aluno não é suficiente.

---

# 8. Professores

Somente o Administrador pode cadastrar professores.

O cadastro cria um `User` com perfil `Professor`.

O novo Professor recebe automaticamente o mesmo `GymId` do Administrador autenticado.

Na versão atual:

- o Administrador pode listar professores;
- o Administrador pode cadastrar professores;
- não existe endpoint de edição de Professor;
- não existe endpoint de ativação ou desativação de Professor;
- não existe endpoint de exclusão de Professor.

---

# 9. Exercícios

## 9.1 Cadastro

Um exercício pertence a uma academia.

Para ser criado, deve possuir:

- nome;
- grupo muscular.

A descrição é opcional.

Os limites atuais são:

- nome: até 150 caracteres;
- grupo muscular: até 100 caracteres;
- descrição: até 500 caracteres.

---

## 9.2 Unicidade

O nome de um exercício deve ser único dentro da mesma academia.

A unicidade utiliza o conjunto:

`GymId + Name`

O campo `Name` utiliza `citext` no PostgreSQL.

Consequentemente, diferenças apenas entre letras maiúsculas e minúsculas não permitem duplicar o nome dentro da mesma academia.

Academias diferentes podem possuir exercícios com o mesmo nome.

---

## 9.3 Estado ativo

Exercícios possuem estado ativo/inativo.

Administrador e Professor podem cadastrar e editar exercícios.

Somente o Administrador pode alterar o status ativo/inativo de um exercício.

Um exercício inativo não pode ser utilizado na criação ou atualização de modelos e treinos.

---

## 9.4 Busca

A consulta de exercícios permite:

- busca textual;
- filtro por grupo muscular;
- filtro por status;
- paginação.

Na implementação atual, o parâmetro de busca textual pesquisa somente pelo nome do exercício.

O grupo muscular utiliza um filtro separado.

A busca não possui normalização específica para remoção de acentos.

Por isso, diferenças como `biceps` e `bíceps` podem produzir resultados distintos.

A interface atualmente apresenta uma descrição de busca mais ampla do que o comportamento efetivo do backend; essa divergência está registrada como bug conhecido.

---

# 10. Modelos de treino

## 10.1 Objetivo

Modelos de treino são estruturas reutilizáveis utilizadas como base para a criação de treinos de alunos.

Um modelo pertence a uma academia.

---

## 10.2 Estrutura

Um modelo possui:

- nome;
- descrição opcional;
- status ativo/inativo;
- um ou mais dias;
- um ou mais exercícios em cada dia.

---

## 10.3 Regras de criação e edição

Um modelo deve possuir pelo menos um dia.

Cada dia deve:

- possuir nome;
- possuir pelo menos um exercício;
- possuir uma ordem única dentro do modelo.

Cada exercício do dia deve possuir:

- exercício válido da mesma academia;
- pelo menos uma série;
- repetições preenchidas;
- ordem única dentro daquele dia;
- descanso igual ou superior a zero, quando informado.

O exercício utilizado deve estar ativo.

---

## 10.4 Unicidade do nome

O nome do modelo deve ser único dentro da academia.

A unicidade utiliza:

`GymId + Name`

O nome utiliza `citext`, tornando a unicidade insensível a diferenças entre maiúsculas e minúsculas.

---

## 10.5 Estado do modelo

Administrador e Professor podem ativar ou desativar modelos.

Um modelo inativo permanece armazenado, mas não pode ser utilizado como origem para a criação de um novo treino.

---

# 11. Treinos

## 11.1 Treino atual

Cada aluno pode possuir no máximo **um treino ativo**.

Essa regra é protegida também no banco de dados através de um índice único parcial para o aluno quando `IsActive = true`.

Treinos anteriores podem permanecer armazenados como versões históricas inativas.

---

## 11.2 Estrutura

Um treino possui:

- aluno;
- academia;
- nome;
- descrição opcional;
- status ativo/inativo;
- dias;
- exercícios em cada dia;
- referência opcional ao modelo que originou o treino.

Cada dia possui uma ordem.

Cada exercício dentro do dia também possui uma ordem.

Ordens duplicadas dentro do mesmo contexto não são permitidas.

---

## 11.3 Criação manual

Administrador e Professor podem criar um treino manualmente para um aluno da própria academia.

O aluno deve estar ativo.

O treino deve possuir:

- nome;
- pelo menos um dia;
- pelo menos um exercício em cada dia.

Os exercícios utilizados devem:

- pertencer à mesma academia;
- existir;
- estar ativos.

---

## 11.4 Criação a partir de modelo

Administrador e Professor podem utilizar um modelo ativo como base para criação de um treino.

O modelo deve:

- pertencer à mesma academia;
- existir;
- estar ativo.

O aluno também deve existir na mesma academia e estar ativo.

O treino criado armazena a referência ao modelo de origem em `SourceWorkoutTemplateId`.

---

## 11.5 Substituição do treino atual

Ao criar um novo treino para um aluno que já possui um treino ativo, o backend:

1. desativa o treino atual;
2. cria o novo treino como ativo.

Portanto, o backend já suporta substituição do treino atual tanto para criação manual quanto para criação baseada em modelo.

Na versão atualmente em homologação, existe uma limitação de interface: o fluxo de edição do treino atual não oferece diretamente a opção de selecionar outro modelo.

Essa limitação está registrada como bug conhecido e não representa uma limitação estrutural do backend.

---

## 11.6 Edição e preservação do histórico

Somente o treino ativo pode ser editado.

Quando o treino ainda não possui execuções registradas, ele pode ser atualizado mantendo o mesmo registro.

Quando já existem execuções associadas ao treino, a edição não altera a versão histórica utilizada nessas execuções.

Nesse caso, o backend:

1. desativa a versão atual;
2. cria uma nova versão do treino;
3. mantém as execuções ligadas à versão anterior.

Essa regra preserva a integridade do histórico do aluno.

---

## 11.7 Exclusão

Na versão atual, não existe endpoint para exclusão de treino.

Treinos históricos são preservados através de inativação e versionamento.

---

# 12. Execução de treino pelo aluno

## 12.1 Unidade de conclusão

Na versão atual, a unidade de conclusão é o **dia de treino (`WorkoutDay`)**.

O aluno:

1. acessa o treino ativo;
2. seleciona um dia;
3. visualiza os exercícios;
4. executa os exercícios;
5. marca o dia como concluído.

A conclusão individual persistida de exercícios ainda não faz parte da versão atual.

Essa funcionalidade está planejada como melhoria da versão 1.

---

## 12.2 Validação da execução

Para registrar uma conclusão:

- o aluno deve existir;
- o aluno deve estar ativo;
- o dia deve pertencer ao treino ativo daquele aluno;
- o treino deve pertencer à academia autenticada.

---

## 12.3 Uma execução por dia e data

O mesmo `WorkoutDay` pode ser concluído no máximo uma vez na mesma data local da academia.

A regra utiliza:

`WorkoutDayId + ExecutionDate`

como combinação única.

A data da execução é calculada utilizando o fuso horário configurado para a academia.

---

## 12.4 Horário armazenado

O momento exato da conclusão é armazenado em UTC em `CompletedAt`.

As respostas podem incluir o deslocamento UTC correspondente ao fuso da academia para permitir apresentação correta do horário local.

---

# 13. Histórico de treinos

Cada conclusão de um dia cria um registro de execução.

O histórico apresenta execuções anteriores do aluno, incluindo informações como:

- treino;
- dia;
- momento da conclusão.

O histórico inclui execuções associadas a versões antigas de treinos.

Assim, a substituição ou edição versionada do treino atual não deve apagar o histórico anterior.

O Aluno acessa somente o próprio histórico.

Administrador e Professor podem consultar o histórico de alunos pertencentes à própria academia.

---

# 14. Avaliações físicas

## 14.1 Registro

Administrador e Professor podem registrar avaliações físicas de alunos da própria academia.

O Aluno pode consultar suas avaliações, mas não registrar uma nova avaliação.

---

## 14.2 Campos

Uma avaliação contém:

- data da avaliação;
- peso;
- altura;
- percentual de gordura opcional;
- peitoral opcional;
- cintura opcional;
- abdômen opcional;
- quadril opcional;
- braço direito opcional;
- braço esquerdo opcional;
- coxa direita opcional;
- coxa esquerda opcional;
- panturrilha direita opcional;
- panturrilha esquerda opcional;
- observações opcionais.

---

## 14.3 Validações

A data da avaliação:

- é obrigatória;
- não pode estar no futuro considerando a data local da academia.

Peso:

- é obrigatório;
- deve ser maior que zero;
- deve ser menor ou igual a `999.99`.

Altura:

- é obrigatória;
- deve ser maior que zero;
- deve ser menor ou igual a `999.99`.

Percentual de gordura, quando informado:

- deve estar entre `0` e `100`.

As demais medidas corporais, quando informadas:

- devem ser maiores que zero;
- devem ser menores ou iguais a `999.99`.

As observações possuem limite máximo de 500 caracteres.

---

## 14.4 Unicidade

Um aluno não pode possuir mais de uma avaliação física com a mesma data.

A combinação:

`StudentId + AssessmentDate`

é única no banco de dados.

A regra também é validada pela camada de aplicação antes da persistência.

---

## 14.5 Avaliação atual

Quando existem múltiplas avaliações, a avaliação com a data mais recente é utilizada como referência para o estado atual do aluno.

Em caso de necessidade de desempate técnico, a ordenação também considera a data de criação.

---

## 14.6 Reavaliação

A próxima avaliação é calculada utilizando:

`AssessmentDate.AddMonths(2)`

Exemplo:

Avaliação:

`10/08/2026`

Próxima avaliação:

`10/10/2026`

O estado de reavaliação pendente segue:

`IsReassessmentDue = data atual da academia >= NextAssessmentDate`

O cálculo utiliza a data local da academia, e não simplesmente a data UTC do servidor.

---

# 15. Paginação

As principais listagens utilizam paginação.

Na versão atual, a camada de aplicação aceita `pageSize` entre 1 e 100 registros por página.

Entre os recursos paginados estão:

- alunos;
- exercícios;
- modelos de treino;
- histórico de treinos;
- histórico de avaliações físicas.

---

# 16. Segurança e autorização

Todas as operações protegidas exigem autenticação válida.

O backend valida permissões independentemente do que é exibido pela interface.

Ocultar um botão, menu ou tela no aplicativo não substitui a autorização no servidor.

Tokens JWT possuem informações de:

- usuário;
- academia;
- nome;
- e-mail;
- perfil.

O backend também verifica se o usuário continua ativo durante a validação do token.

---

# 17. Proteção contra tentativas excessivas de login

O endpoint de login possui limitação de requisições por endereço IP.

A configuração atual permite até 10 tentativas dentro de uma janela fixa de 1 minuto.

Quando o limite é excedido, a API responde com:

`429 Too Many Requests`

---

# 18. Dados sensíveis

O sistema não deve expor:

- senhas;
- hashes de senha;
- JWTs em respostas que não façam parte do fluxo de autenticação;
- segredos de configuração;
- chaves de assinatura;
- connection strings;
- credenciais de infraestrutura;
- dados pertencentes a outra academia.

Respostas da API devem retornar somente os dados necessários para cada operação.

---

# 19. Exclusão de dados

Na versão atual, não existem endpoints gerais de exclusão para:

- alunos;
- professores;
- exercícios;
- modelos de treino;
- treinos;
- avaliações físicas.

Recursos que possuem estado ativo/inativo utilizam essa estratégia quando a operação está disponível.

Qualquer política futura de exclusão definitiva deve preservar consistência referencial, segurança e histórico quando aplicável.

---

# 20. Comportamentos conhecidos em homologação

Durante o teste fechado, alguns comportamentos estão registrados como bugs conhecidos.

Esses itens devem ser acompanhados no GitHub Project e no documento:

`docs/qa/known-issues.md`

Um comportamento listado como bug conhecido não deve ser interpretado como regra de negócio definitiva.

Entre os comportamentos atualmente conhecidos estão limitações relacionadas a:

- interação do card de treino atual do Aluno;
- fluxo de substituição do treino através de outro modelo;
- busca de exercícios por grupo muscular e sem acentuação;
- ajustes visuais pontuais ainda aguardando distribuição em nova build.

---

# 21. Funcionalidades planejadas

As seguintes regras não representam o comportamento atual e estão planejadas para versões posteriores.

## Versão 1

Entre as melhorias previstas estão:

- conclusão individual persistida de exercícios;
- possibilidade de desfazer a conclusão individual antes da finalização;
- retomada de treino em andamento;
- melhorias na reorganização de dias e exercícios;
- refinamento de estados vazios existentes;
- refinamentos de navegação e ações;
- política de exclusão definitiva após período prolongado de inatividade, ainda dependente de definição e implementação técnica compatível com o histórico existente.

## Versão 2

Funcionalidades previstas para evolução posterior incluem:

- progresso visual avançado de treino;
- evolução de cargas;
- temporizador de descanso;
- vídeos demonstrativos de exercícios;
- gráficos avançados de evolução física;
- notificações e lembretes.

---

# 22. Evolução das regras

Este documento deve ser atualizado sempre que:

- uma regra de negócio for alterada;
- uma nova funcionalidade relevante for adicionada;
- uma limitação temporária for resolvida;
- uma nova regra de segurança ou permissão for definida;
- uma funcionalidade planejada passar a fazer parte da versão atual.

Alterações relevantes devem acompanhar a evolução da versão do produto e permanecer coerentes com a implementação.
