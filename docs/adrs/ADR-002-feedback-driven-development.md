# ADR-002 — Desenvolvimento Guiado por Necessidades Reais

## Status

Aprovado

---

## Contexto

Era necessário definir como o GymFlow deveria evoluir ao longo do tempo.

Projetos de software podem acumular funcionalidades que:

- aumentam a complexidade;
- possuem pouco uso;
- dificultam manutenção;
- consomem tempo de desenvolvimento;
- não resolvem problemas relevantes para os usuários.

Era necessário estabelecer um princípio para priorizar a evolução do produto.

---

## Decisão

O desenvolvimento será guiado prioritariamente por necessidades reais identificadas durante o uso da plataforma.

Novas funcionalidades e melhorias deverão buscar resolver problemas concretos observados em:

- academias parceiras;
- Professores;
- Alunos;
- homologação;
- operação do sistema;
- manutenção técnica.

Funcionalidades não deverão ser adicionadas apenas por parecerem interessantes ou comuns em produtos concorrentes.

---

## Justificativa

- Evita funcionalidades desnecessárias.
- Mantém a plataforma mais simples.
- Reduz complexidade de manutenção.
- Direciona esforço para problemas de maior impacto.
- Permite entregar valor continuamente.
- Aproxima o produto dos clientes e usuários.
- Facilita priorização do roadmap.

---

## Consequências

A partir desta decisão:

- feedback de usuários deve ser considerado na priorização;
- bugs e dificuldades recorrentes devem influenciar o roadmap;
- melhorias devem possuir uma justificativa clara;
- funcionalidades futuras podem permanecer no backlog até existir prioridade suficiente;
- a existência de uma ideia não significa que ela deva ser implementada imediatamente;
- requisitos técnicos, segurança, acessibilidade, confiabilidade e manutenção também podem justificar mudanças mesmo sem solicitação direta de um cliente.

---

## Aplicação atual

O princípio é utilizado na organização atual do roadmap.

Os itens são separados entre categorias como:

```text
BUG
IMPROVEMENT
V2
```

Isso permite distinguir:

```text
problema atual
```

de:

```text
melhoria planejada
```

e:

```text
evolução futura
```

antes de iniciar a implementação.

---

## Observação histórica

Este ADR foi criado quando o projeto ainda utilizava publicamente o nome `GymFlow`.

A marca pública atual é:

```text
Avelri
```

A mudança de marca não altera a decisão registrada.
