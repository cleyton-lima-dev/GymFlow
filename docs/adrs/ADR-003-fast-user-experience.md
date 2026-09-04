# ADR-003 — Velocidade e Eficiência de Uso

## Status

Aprovado

---

## Contexto

Um dos objetivos centrais do GymFlow é reduzir o esforço necessário para que Professores realizem tarefas recorrentes no dia a dia da academia.

Processos baseados em:

- fichas impressas;
- planilhas;
- anotações manuais;
- sistemas com muitos passos

podem aumentar o tempo gasto em atividades operacionais.

Era necessário estabelecer um princípio de UX para orientar os fluxos do produto.

---

## Decisão

Sempre que existirem alternativas funcionalmente equivalentes, deverá ser priorizada a solução que permita concluir a tarefa com:

- menos etapas;
- menos interações desnecessárias;
- menor tempo;
- menor esforço cognitivo.

Essa prioridade não deverá comprometer:

- segurança;
- autorização;
- integridade dos dados;
- clareza;
- acessibilidade;
- confirmações necessárias;
- compreensão do impacto de uma ação.

---

## Justificativa

A produtividade do Professor é uma prioridade importante do produto.

O Avelri deve buscar oferecer uma experiência mais eficiente do que processos baseados em fichas impressas, planilhas ou fluxos excessivamente manuais.

Reduzir interações desnecessárias pode:

- acelerar tarefas recorrentes;
- diminuir retrabalho;
- reduzir erros operacionais;
- melhorar a experiência do usuário;
- facilitar adoção da plataforma.

---

## Consequências

A partir desta decisão:

- fluxos recorrentes devem evitar etapas sem valor funcional;
- informações já conhecidas pelo sistema não devem ser solicitadas novamente sem necessidade;
- ações frequentes devem ser fáceis de localizar;
- navegação excessivamente profunda deve ser evitada;
- modelos reutilizáveis devem ser utilizados quando reduzirem trabalho repetitivo;
- telas equivalentes devem manter padrões de interação consistentes.

Entretanto, nem sempre o fluxo com menos toques será o mais adequado.

Exemplos em que etapas adicionais podem ser justificadas:

```text
ação destrutiva
→ confirmação
```

```text
mudança com risco de perda de dados
→ aviso explícito
```

```text
operação sensível
→ validação adicional
```

---

## Critério de decisão

Quando duas soluções forem avaliadas, considerar:

```text
eficiência
+
clareza
+
segurança
+
consistência
+
impacto no usuário
```

A quantidade de toques é um indicador de eficiência, mas não deve ser utilizada isoladamente.

---

## Aplicação atual

Esse princípio pode ser observado em funcionalidades como:

- modelos de treino reutilizáveis;
- criação de treino a partir de modelo;
- acesso aos principais recursos através da navegação principal;
- ações específicas conforme o perfil;
- resolução automática do próprio usuário nos endpoints `/me`;
- vinculação automática ao tenant autenticado, sem pedir seleção de academia.

---

## Observação histórica

Este ADR foi criado quando o projeto ainda utilizava publicamente o nome `GymFlow`.

A marca pública atual é:

```text
Avelri
```

A mudança de marca não altera a decisão registrada.
