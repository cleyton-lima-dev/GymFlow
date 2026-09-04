# ADR-001 — Modelo de Negócio

## Status

Aprovado

---

## Contexto

Era necessário definir quem seria o cliente responsável pela contratação do GymFlow.

O produto possui diferentes tipos de usuários, principalmente:

- academias;
- Professores;
- Alunos.

Era necessário determinar qual desses participantes seria responsável pela contratação e pelo pagamento da plataforma.

---

## Decisão

O GymFlow será comercializado para academias.

A academia será o cliente da plataforma e será responsável pela contratação e pelo pagamento.

Professores e Alunos utilizarão a plataforma como usuários vinculados à academia.

---

## Justificativa

- A academia é a organização que contrata o serviço.
- A academia é responsável pelo pagamento.
- Professores utilizam a plataforma como ferramenta de trabalho.
- Alunos utilizam a plataforma como um serviço disponibilizado pela academia.
- A aplicação pode oferecer identidade visual específica para cada academia.
- O modelo permite atender múltiplos usuários através de uma única contratação por academia.

---

## Consequências

A partir desta decisão:

- o modelo comercial deve ser estruturado em torno da academia;
- usuários individuais não precisam contratar o produto separadamente;
- Professores e Alunos devem permanecer associados ao contexto da academia;
- permissões e isolamento de dados devem considerar a academia à qual o usuário pertence;
- funcionalidades comerciais futuras devem priorizar a relação entre Avelri e a academia cliente.

---

## Observação histórica

Este ADR foi criado quando o projeto ainda utilizava publicamente o nome `GymFlow`.

A marca pública atual do produto é:

```text
Avelri
```

A mudança de marca não altera a decisão registrada neste ADR.
