# Perfis e Permissões — Avelri

**Produto:** Avelri
**Versão de referência:** 1.0.0+3
**Última atualização:** 04/09/2026

---

## Objetivo

Este documento define os perfis de usuário existentes no Avelri e as permissões implementadas para cada perfil.

As permissões devem ser respeitadas tanto pela interface quanto pelo backend.

Ocultar uma funcionalidade na interface não substitui a validação de autorização no servidor.

---

# 1. Perfis

O Avelri possui três perfis:

- Administrador;
- Professor;
- Aluno.

No backend, os perfis correspondem aos valores:

- `Admin`;
- `Professor`;
- `Student`.

---

# 2. Administrador

O Administrador é o perfil responsável pela gestão da academia dentro do Avelri.

Seu acesso permanece restrito aos dados da própria academia.

## Permissões principais

O Administrador pode:

- visualizar alunos da própria academia;
- cadastrar alunos;
- acessar detalhes dos alunos;
- editar dados de alunos;
- ativar e desativar alunos;
- criar treinos manualmente;
- criar treinos a partir de modelos;
- editar o treino atual de alunos;
- consultar o histórico de treinos dos alunos;
- listar professores da própria academia;
- cadastrar professores;
- acessar o banco de exercícios;
- cadastrar exercícios;
- editar exercícios;
- ativar e desativar exercícios;
- consultar modelos de treino;
- criar modelos de treino;
- editar modelos de treino;
- ativar e desativar modelos de treino;
- registrar avaliações físicas de alunos;
- consultar a avaliação física mais recente;
- consultar o histórico de avaliações físicas;
- consultar detalhes de avaliações físicas.

## Restrições

O Administrador não pode:

- acessar dados pertencentes a outra academia;
- criar professores vinculados arbitrariamente a outra academia;
- acessar dados de usuários fora do próprio contexto de academia;
- contornar regras de autorização através de chamadas diretas à API.

Na versão atual, não existe fluxo para editar, ativar ou desativar professores já cadastrados.

---

# 3. Professor

O Professor é o perfil responsável principalmente pelos processos técnicos relacionados aos alunos, treinos, exercícios e avaliações físicas.

Seu acesso permanece restrito à própria academia.

## Permissões principais

O Professor pode:

- visualizar alunos da própria academia;
- acessar detalhes dos alunos;
- criar treinos manualmente;
- criar treinos a partir de modelos;
- editar o treino atual dos alunos;
- consultar o histórico de treinos dos alunos;
- acessar o banco de exercícios;
- cadastrar exercícios;
- editar exercícios;
- consultar modelos de treino;
- criar modelos de treino;
- editar modelos de treino;
- ativar e desativar modelos de treino;
- registrar avaliações físicas;
- consultar a avaliação física mais recente;
- consultar o histórico de avaliações físicas;
- consultar detalhes de avaliações físicas.

## Restrições

O Professor não pode:

- cadastrar professores;
- listar professores através da área administrativa;
- acessar a área de gerenciamento de professores;
- cadastrar novos alunos;
- editar dados cadastrais de alunos;
- ativar ou desativar alunos;
- ativar ou desativar exercícios;
- acessar dados pertencentes a outra academia;
- acessar funções administrativas restritas ao Administrador.

A opção **Professores** não deve ser exibida para esse perfil.

Mesmo que uma rota ou endpoint seja chamado manualmente, o backend deve impedir o acesso quando a operação exigir o perfil Administrador.

---

# 4. Aluno

O Aluno utiliza o Avelri para acessar informações relacionadas ao próprio cadastro, treino, histórico e avaliações físicas.

## Permissões principais

O Aluno pode:

- visualizar a própria Home;
- visualizar os próprios dados pessoais;
- visualizar o próprio treino atual;
- visualizar os dias do próprio treino;
- visualizar os exercícios de cada dia;
- marcar um dia do próprio treino como concluído;
- consultar o próprio histórico de execuções;
- consultar a própria avaliação física mais recente;
- consultar o próprio histórico de avaliações físicas;
- consultar detalhes das próprias avaliações físicas;
- acessar apenas as áreas destinadas ao perfil Aluno.

Na versão atual, a conclusão ocorre no nível do **dia de treino**.

Não existe conclusão individual persistida de exercícios.

## Restrições

O Aluno não pode:

- visualizar a lista de alunos;
- visualizar dados de outro aluno;
- cadastrar ou editar alunos;
- ativar ou desativar alunos;
- cadastrar ou consultar professores;
- acessar gerenciamento de professores;
- cadastrar ou editar exercícios;
- alterar status de exercícios;
- cadastrar ou editar modelos de treino;
- alterar status de modelos de treino;
- criar ou editar o próprio treino;
- registrar a própria avaliação física;
- acessar áreas administrativas;
- acessar dados pertencentes a outra academia.

---

# 5. Matriz de permissões

| Funcionalidade | Administrador | Professor | Aluno |
|---|:---:|:---:|:---:|
| Fazer login | ✅ | ✅ | ✅ |
| Visualizar própria Home | ✅ | ✅ | ✅ |
| Visualizar lista de alunos | ✅ | ✅ | ❌ |
| Cadastrar aluno | ✅ | ❌ | ❌ |
| Visualizar detalhes de aluno | ✅ | ✅ | ❌ |
| Editar dados de aluno | ✅ | ❌ | ❌ |
| Ativar/desativar aluno | ✅ | ❌ | ❌ |
| Listar professores | ✅ | ❌ | ❌ |
| Cadastrar professor | ✅ | ❌ | ❌ |
| Editar/alterar status de professor | ❌ | ❌ | ❌ |
| Consultar banco de exercícios | ✅ | ✅ | ❌ |
| Cadastrar exercício | ✅ | ✅ | ❌ |
| Editar exercício | ✅ | ✅ | ❌ |
| Ativar/desativar exercício | ✅ | ❌ | ❌ |
| Consultar modelos de treino | ✅ | ✅ | ❌ |
| Criar modelo de treino | ✅ | ✅ | ❌ |
| Editar modelo de treino | ✅ | ✅ | ❌ |
| Ativar/desativar modelo de treino | ✅ | ✅ | ❌ |
| Criar treino para aluno | ✅ | ✅ | ❌ |
| Editar treino de aluno | ✅ | ✅ | ❌ |
| Consultar histórico de treino de aluno | ✅ | ✅ | ❌ |
| Visualizar próprio treino | — | — | ✅ |
| Concluir dia do próprio treino | — | — | ✅ |
| Consultar próprio histórico | — | — | ✅ |
| Registrar avaliação física | ✅ | ✅ | ❌ |
| Consultar avaliações de aluno | ✅ | ✅ | ❌ |
| Consultar própria avaliação física | — | — | ✅ |
| Registrar própria avaliação física | — | — | ❌ |
| Acessar dados de outra academia | ❌ | ❌ | ❌ |

---

# 6. Regras de autorização

## 6.1 Autenticação

Rotas e operações protegidas exigem um usuário autenticado.

Tokens inválidos, expirados ou ausentes não permitem acesso a recursos protegidos.

Além da validação criptográfica do JWT, o backend verifica se o usuário identificado pelo token continua existente e ativo dentro da academia informada no token.

Assim, um usuário desativado não deve continuar utilizando um token anteriormente emitido.

---

## 6.2 Autorização por perfil

O backend valida o perfil do usuário antes de executar operações restritas.

Exemplo:

O cadastro e a listagem de professores são exclusivos do Administrador.

Um Professor que tente acessar diretamente um endpoint administrativo deve receber uma resposta de acesso negado.

As regras aplicadas no Flutter complementam essa proteção, mas a autorização definitiva permanece no backend.

---

## 6.3 Autorização por academia

Além do perfil, o sistema valida a academia relacionada ao usuário autenticado.

Ter o perfil correto não concede acesso a dados pertencentes a outro tenant.

Exemplo:

Um Administrador da Academia A não pode acessar um aluno da Academia B, mesmo possuindo o perfil Administrador.

O contexto da academia é transportado pelo claim `gym_id` do JWT e utilizado nas consultas de dados protegidos.

---

## 6.4 Autorização do Aluno

O perfil Aluno opera exclusivamente sobre os próprios recursos quando a funcionalidade é destinada ao usuário autenticado.

Os endpoints destinados ao aluno utilizam a identidade presente no token para localizar o registro correspondente.

Não deve ser possível alterar identificadores em uma requisição para consultar treino, histórico, dados pessoais ou avaliações físicas pertencentes a outro aluno.

---

# 7. Testes recomendados de permissão

Durante a homologação, devem ser testados pelo menos os seguintes cenários.

## Administrador

- consegue acessar a área de professores;
- consegue listar professores;
- consegue cadastrar Professor;
- consegue cadastrar Aluno;
- consegue editar Aluno;
- consegue ativar e desativar Aluno;
- consegue gerenciar treinos;
- consegue registrar avaliações físicas;
- consegue ativar e desativar exercícios;
- não consegue acessar dados de outra academia.

## Professor

- consegue acessar alunos;
- consegue visualizar detalhes dos alunos;
- consegue gerenciar treinos;
- consegue utilizar exercícios e modelos;
- consegue cadastrar e editar exercícios;
- consegue registrar avaliações físicas;
- não visualiza a opção Professores;
- não consegue acessar endpoints de professores;
- não consegue cadastrar Professor pela API;
- não consegue cadastrar Aluno;
- não consegue editar Aluno;
- não consegue ativar ou desativar Aluno;
- não consegue ativar ou desativar exercício;
- não consegue acessar dados de outra academia.

## Aluno

- consegue acessar o próprio treino;
- consegue acessar os dias e exercícios do próprio treino;
- consegue marcar um dia do próprio treino como concluído;
- consegue acessar o próprio histórico;
- consegue consultar as próprias avaliações físicas;
- não consegue registrar a própria avaliação física;
- não consegue acessar áreas administrativas;
- não consegue acessar dados de outro aluno;
- não consegue acessar endpoints de gestão.

---

# 8. Respostas esperadas para acesso indevido

Quando um usuário tenta acessar uma operação para a qual não possui permissão, a API deve negar a operação.

De forma geral:

- `401 Unauthorized` deve ser utilizado quando não existe autenticação válida;
- `403 Forbidden` deve ser utilizado quando existe autenticação válida, mas o perfil não possui permissão para a operação.

O sistema não deve retornar dados protegidos antes de realizar as validações necessárias.

Recursos pertencentes a outra academia também não devem ser expostos ao usuário solicitante.

---

# 9. Segurança da interface

A interface deve evitar apresentar ações que o usuário não pode executar.

Exemplos:

- Professor não visualiza gerenciamento de professores;
- Professor não visualiza ações de editar, ativar ou desativar aluno;
- Aluno não visualiza menus administrativos;
- ações exclusivas do Administrador permanecem ocultas para outros perfis.

O roteamento do Flutter também restringe cada usuário à árvore correspondente ao próprio perfil:

- Administrador: `/admin`;
- Professor: `/professor`;
- Aluno: `/student`.

Essa proteção melhora a experiência e reduz navegações indevidas, mas não substitui a autorização no backend.

---

# 10. Alterações futuras

Sempre que uma nova funcionalidade for adicionada, deve ser definido explicitamente:

1. quais perfis podem visualizar o recurso;
2. quais perfis podem criar dados;
3. quais perfis podem editar dados;
4. quais perfis podem alterar status;
5. quais perfis podem excluir dados;
6. como o isolamento por academia será aplicado;
7. como o acesso do Aluno aos próprios recursos será validado.

Este documento deve acompanhar qualquer alteração relevante nas permissões do Avelri.
