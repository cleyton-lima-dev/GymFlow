# Bugs Conhecidos — Avelri

**Produto:** Avelri
**Versão atualmente distribuída:** 1.0.0+3
**Ambiente:** Production — Closed Testing
**Última atualização:** 04/09/2026

---

## Objetivo

Este documento registra problemas já identificados durante a homologação do Avelri.

O acompanhamento operacional e o estado mais atual das correções continuam sendo feitos através do GitHub Project.

Este arquivo serve principalmente para:

- evitar abertura duplicada de bugs;
- informar o QA sobre problemas já conhecidos;
- registrar quais comportamentos pertencem à build atualmente distribuída;
- facilitar o reteste após novas versões;
- diferenciar bugs confirmados de funcionalidades futuras.

---

# 1. Status utilizados

- **Aberto** — problema confirmado e ainda não corrigido.
- **Em correção** — correção em desenvolvimento.
- **Aguardando release** — correção implementada e validada localmente, mas ainda não distribuída aos testadores.
- **Aguardando reteste** — correção já disponível no ambiente ou artefato necessário e aguardando validação do QA.
- **Resolvido** — correção distribuída e validada através de reteste.

Uma correção existente apenas no código local não deve ser considerada `Resolvido`.

---

# 2. KI-001 — Título "Professores" quebra incorretamente

**Área:** Mobile / Administrador
**Severidade:** Baixa
**Status:** Aguardando release
**Versão afetada:** 1.0.0+3

## Problema

Em determinados tamanhos de tela, o título:

```text
Professores
```

era comprimido pelo botão:

```text
Novo professor
```

provocando uma quebra inadequada de linha.

## Resultado atual na versão distribuída

A build `1.0.0+3` ainda pode apresentar o problema.

## Resultado esperado

O título e sua descrição devem possuir espaço adequado, mantendo o botão corretamente posicionado também em telas menores.

## Situação atual

A correção já foi implementada e validada localmente.

O layout foi alterado para evitar que título, descrição e botão disputem o mesmo espaço horizontal.

A correção somente poderá ser considerada homologada depois de:

1. nova build ser distribuída;
2. cenário ser retestado;
3. comportamento ser confirmado.

---

# 3. KI-002 — Card "Treino atual" da Home do Aluno não possui ação de toque

**Área:** Mobile / Aluno
**Severidade:** Média
**Status:** Em correção
**Versão afetada:** 1.0.0+3

## Problema

O card principal que apresenta o treino atual na Home do Aluno não responde ao toque.

## Resultado atual

Ao tocar no card:

```text
SEU TREINO ATUAL
```

nenhuma ação é executada.

Os cards individuais dos dias de treino continuam funcionando e permitem abrir cada dia normalmente.

## Resultado esperado

O card deve possuir uma ação coerente com o conteúdo apresentado e facilitar o acesso ao treino atual.

A solução deve manter o fluxo existente dos dias de treino e evitar criar uma navegação redundante ou confusa.

## Situação atual

O comportamento foi confirmado no código da versão atual.

A correção está sendo tratada durante o ciclo de homologação.

---

# 4. KI-003 — Edição do treino atual não oferece substituição direta por outro modelo

**Área:** Treinos / Administrador / Professor
**Severidade:** Média
**Status:** Aberto
**Versão afetada:** 1.0.0+3

## Problema

Quando um aluno já possui um treino ativo e o responsável entra no fluxo de edição desse treino, não existe opção direta para selecionar outro modelo e utilizá-lo como substituição.

## Resultado atual

A tela de edição permite modificar:

- nome;
- descrição;
- dias;
- exercícios.

Porém, não oferece uma ação como:

```text
Substituir por outro modelo
```

ou equivalente.

## Comportamento do backend

O backend **já suporta a substituição de um treino ativo**.

Quando um novo treino é criado para um aluno que já possui treino ativo:

```text
treino anterior
→ IsActive = false
```

e:

```text
novo treino
→ IsActive = true
```

Isso funciona tanto para criação manual quanto para criação a partir de modelo.

Portanto, o problema não é uma limitação estrutural do backend.

## Resultado esperado

A interface deve oferecer um fluxo claro para que Administrador ou Professor possa:

1. escolher substituir o treino atual;
2. selecionar um modelo ativo existente;
3. revisar ou ajustar sua estrutura;
4. criar o novo treino;
5. manter o treino anterior inativo e preservado no histórico quando aplicável.

## Observação

Não existe requisito atual para excluir fisicamente o treino anterior.

A ausência de um endpoint:

```text
DELETE Workout
```

não faz parte deste bug.

O comportamento esperado é substituição através de inativação/versionamento.

---

# 5. KI-004 — Busca de exercícios possui limitações de acentuação e grupo muscular

**Área:** Exercícios / Interface de busca
**Severidade:** Média
**Status:** Aberto
**Versão afetada:** 1.0.0+3

## Problema

O campo principal de busca de exercícios apresenta um comportamento mais limitado do que o texto exibido na interface sugere.

A interface informa:

```text
Buscar exercício ou grupo muscular...
```

Entretanto, o parâmetro geral de busca enviado ao backend pesquisa atualmente apenas:

```text
Exercise.Name
```

O grupo muscular possui um filtro separado.

---

## Acentuação

A busca atual não possui normalização específica para remoção de acentos.

Exemplo:

```text
biceps
```

pode não localizar:

```text
bíceps
```

---

## Grupo muscular

Digitar apenas um grupo muscular no campo geral de busca não garante que exercícios desse grupo sejam encontrados.

Exemplo:

```text
peitoral
```

não é pesquisado automaticamente em:

```text
Exercise.MuscleGroup
```

através do parâmetro geral `search`.

Existe filtro específico:

```text
muscleGroup
```

para essa finalidade.

## Resultado esperado

A experiência de busca deve ser consistente com o texto apresentado ao usuário.

A solução desejada deve permitir, conforme definição final:

- pesquisar pelo nome;
- pesquisar pelo grupo muscular;
- ignorar diferenças de acentuação;
- manter comportamento consistente nas telas que utilizem busca de exercícios.

## Observação

O PostgreSQL utiliza `citext` para o nome do exercício, o que trata diferenças entre maiúsculas e minúsculas em determinados contextos, mas isso não fornece automaticamente busca sem acentuação.

---

# 6. KI-005 — Texto de alguns botões aparenta estar desabilitado

**Área:** Mobile / Interface / Botões
**Severidade:** Baixa
**Status:** Aberto
**Versão afetada:** 1.0.0+3

## Problema

Em alguns botões que utilizam cores de destaque mais fortes, o texto apresenta aparência excessivamente opaca ou com baixo contraste.

Visualmente, o botão pode parecer estar:

```text
desativado
```

mesmo quando continua disponível para interação.

## Resultado atual

Em determinados botões:

- o fundo possui cor forte;
- o texto apresenta contraste ou opacidade inadequados;
- a aparência se aproxima do estado visual normalmente associado a um botão desabilitado.

Isso pode gerar dúvida sobre a disponibilidade da ação.

## Resultado esperado

Um botão habilitado deve apresentar:

- texto claramente legível;
- contraste adequado com o fundo;
- aparência visual de estado ativo;
- diferenciação clara em relação ao estado desabilitado.

O estado desabilitado deve possuir tratamento visual próprio e não ser confundido com o estado habilitado.

## Impacto

O problema é principalmente visual e de usabilidade.

Na ocorrência atualmente conhecida, a ação permanece funcional.

A severidade deve ser reavaliada caso seja identificado algum botão em que a aparência leve usuários a não conseguir concluir um fluxo importante.

## Validação da correção

Após ajuste visual, devem ser verificados:

- botões primários;
- botões secundários;
- botões com fundo de branding;
- estados habilitado/desabilitado;
- diferentes tenants e respectivas cores;
- Android em dispositivo real;
- Web, quando o mesmo componente for utilizado.

---

# 7. Resumo dos bugs conhecidos

| ID | Problema | Severidade | Status |
|---|---|---|---|
| `KI-001` | Título "Professores" quebra incorretamente | Baixa | Aguardando release |
| `KI-002` | Card "Treino atual" não possui ação de toque | Média | Em correção |
| `KI-003` | Edição do treino não oferece substituição direta por modelo | Média | Aberto |
| `KI-004` | Busca de exercícios limitada por acentuação/grupo muscular | Média | Aberto |
| `KI-005` | Texto de alguns botões aparenta estar desabilitado | Baixa | Aberto |

---

# 8. Como proceder ao encontrar um bug conhecido

Caso um dos problemas acima seja encontrado durante a homologação:

1. não criar nova Issue quando o comportamento for exatamente o mesmo;
2. adicionar novas evidências à Issue existente quando forem úteis;
3. registrar dispositivo e versão do Android quando houver diferença de comportamento;
4. informar se o problema ocorrer em outro perfil, tenant ou tela;
5. informar quando a severidade observada for maior do que a já documentada.

Uma nova Issue deve ser criada quando o comportamento representar um problema distinto.

---

# 9. Reteste

Quando uma correção chegar a:

```text
Testing
```

os cenários relacionados devem ser executados novamente.

O bug somente deve ser considerado resolvido depois de:

1. correção implementada;
2. testes técnicos concluídos;
3. artefato ou ambiente necessário atualizado;
4. comportamento retestado;
5. resultado esperado confirmado.

Quando a correção for exclusivamente no Flutter, uma validação apenas no código local não equivale a homologação da versão distribuída.

---

# 10. Atualização após nova build

Quando uma nova build Android for publicada, revisar para cada item:

```text
Versão afetada
Status
Situação atual
```

Exemplo:

```text
Aguardando release
        ↓
nova build publicada
        ↓
Aguardando reteste
        ↓
QA aprovado
        ↓
Resolvido
```

O mesmo princípio se aplica a correções Web e backend.

---

# 11. Fonte de acompanhamento

O estado operacional mais recente das Issues deve ser consultado no GitHub Project do Avelri.

O fluxo utilizado é:

```text
Backlog
   ↓
In Progress
   ↓
Testing
   ↓
Done
```

A Issue permanece aberta durante `Testing`.

Ela deve ser fechada somente após a validação e passagem para `Done`.

Este documento representa uma fotografia dos problemas conhecidos da versão documentada.

---

# 12. Documentos relacionados

- `docs/qa/homologation-guide.md`
- `docs/qa/test-scenarios.md`
- `docs/product/business-rules.md`
- `docs/product/roles-and-permissions.md`
- `docs/technical/mobile.md`
