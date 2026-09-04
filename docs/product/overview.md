# Visão do Produto — Avelri

**Produto:** Avelri
**Versão atual de referência:** 1.0.0+3
**Última atualização:** 04/09/2026
**Autor:** Cleyton Lima

---

## 1. O que é o Avelri?

O Avelri é uma plataforma de gestão de treinos para academias, profissionais e alunos.

O produto foi desenvolvido para digitalizar processos que ainda são realizados por meio de fichas impressas, planilhas ou soluções pouco integradas.

Por meio do Avelri, academias podem centralizar:

- alunos;
- Professores;
- exercícios;
- modelos de treino;
- treinos individuais;
- histórico de execuções;
- avaliações físicas.

Os alunos possuem acesso próprio para consultar seus dados, visualizar o treino atual, acessar os dias e exercícios prescritos, registrar a conclusão dos dias realizados, consultar o histórico e acompanhar suas avaliações físicas.

---

## 2. Problema que o produto resolve

Muitas academias de pequeno e médio porte ainda possuem processos manuais ou fragmentados para organização dos treinos.

Isso pode gerar problemas como:

- dificuldade para atualizar treinos;
- perda ou deterioração de fichas físicas;
- retrabalho para profissionais;
- dificuldade para consultar informações rapidamente;
- ausência de histórico organizado;
- pouca organização das avaliações físicas;
- dificuldade para manter informações atualizadas entre academia, Professor e Aluno.

O Avelri busca centralizar esses processos em um único ambiente e tornar a gestão mais simples, organizada e acessível.

---

## 3. Público-alvo

O Avelri é direcionado principalmente a academias de pequeno e médio porte que desejam digitalizar a gestão de treinos e melhorar a experiência de seus profissionais e alunos.

O produto também foi estruturado para permitir atendimento a múltiplas academias através da mesma aplicação.

---

## 4. Cliente

O cliente do Avelri é a academia.

Cada academia possui seu próprio contexto dentro do sistema.

Seus usuários e dados devem permanecer isolados dos dados das demais academias.

A aplicação pode atender múltiplas academias sem exigir uma aplicação completamente separada para cada uma.

---

## 5. Perfis de usuário

O sistema possui três perfis:

```text
Administrador
Professor
Aluno
```

As permissões detalhadas estão documentadas em:

`docs/product/roles-and-permissions.md`

---

## 6. Administrador

O Administrador é responsável pelas funções administrativas da própria academia.

Entre suas principais possibilidades estão:

- consultar alunos;
- cadastrar alunos;
- editar alunos;
- ativar e desativar alunos;
- listar Professores;
- cadastrar Professores;
- consultar e gerenciar exercícios;
- alterar status de exercícios;
- consultar e gerenciar modelos de treino;
- criar treinos;
- editar treinos;
- consultar históricos;
- registrar avaliações físicas;
- consultar avaliações físicas.

Na versão atual, o gerenciamento de Professores contempla:

```text
listar
+
cadastrar
```

Não existem ainda fluxos para editar, ativar/desativar ou excluir Professores.

---

## 7. Professor

O Professor é responsável principalmente pelos processos técnicos relacionados aos alunos.

Pode:

- consultar alunos;
- acessar detalhes dos alunos;
- consultar exercícios;
- cadastrar exercícios;
- editar exercícios;
- consultar modelos de treino;
- criar modelos;
- editar modelos;
- ativar e desativar modelos;
- criar treinos;
- editar treinos;
- consultar históricos;
- registrar avaliações físicas;
- consultar avaliações físicas.

O Professor não pode:

- cadastrar alunos;
- editar dados cadastrais de alunos;
- ativar ou desativar alunos;
- gerenciar Professores;
- alterar status de exercícios.

---

## 8. Aluno

O Aluno possui acesso aos próprios recursos.

Entre as funcionalidades atuais estão:

- visualizar a Home;
- consultar dados pessoais;
- visualizar o treino atual;
- visualizar os dias do treino;
- visualizar os exercícios de cada dia;
- registrar a conclusão de um dia;
- consultar o próprio histórico;
- consultar a avaliação física mais recente;
- consultar histórico de avaliações físicas;
- consultar detalhes de avaliações físicas.

O Aluno não possui acesso a funcionalidades administrativas.

---

## 9. Gestão de alunos

O Avelri permite manter os alunos da academia em uma estrutura centralizada.

O Administrador pode:

- cadastrar;
- consultar;
- editar;
- ativar;
- desativar.

O Professor possui acesso de consulta aos alunos e aos recursos técnicos relacionados a eles.

Alunos inativos permanecem registrados e seus dados históricos são preservados.

A política futura de exclusão definitiva após período prolongado de inatividade ainda não faz parte da versão atual.

---

## 10. Gestão de Professores

O Administrador pode:

- visualizar Professores da própria academia;
- cadastrar novos Professores.

O novo Professor é automaticamente vinculado ao mesmo contexto de academia do Administrador autenticado.

O Professor não possui acesso ao gerenciamento de outros Professores.

---

## 11. Banco de exercícios

Cada academia possui seu próprio catálogo de exercícios.

Os exercícios podem conter:

- nome;
- grupo muscular;
- descrição;
- status ativo/inativo.

Eles são reutilizados na criação de:

- modelos de treino;
- treinos de alunos.

Admin e Professor podem cadastrar e editar exercícios.

A alteração de status é exclusiva do Administrador.

---

## 12. Modelos de treino

Modelos de treino são estruturas reutilizáveis utilizadas para acelerar a criação de novos treinos.

Um modelo pode possuir:

- nome;
- descrição;
- dias;
- exercícios;
- séries;
- repetições;
- descanso;
- observações;
- ordem dos dias e exercícios.

Admin e Professor podem criar, editar e alterar o status de modelos.

---

## 13. Gestão de treinos

Treinos são atribuídos a alunos específicos.

Um treino pode ser:

```text
criado manualmente
```

ou:

```text
criado a partir de um modelo
```

Cada aluno pode possuir no máximo um treino ativo por vez.

Quando um novo treino é criado para um aluno que já possui treino ativo:

```text
treino anterior
→ inativo
```

```text
novo treino
→ ativo
```

Os treinos anteriores podem ser preservados para manter a integridade do histórico.

---

## 14. Edição e histórico do treino

Admin e Professor podem editar o treino atual.

Quando o treino ainda não possui execuções, sua estrutura pode ser atualizada diretamente.

Quando já existem execuções, o sistema preserva a versão histórica anterior e cria uma nova versão ativa.

Esse comportamento evita que alterações futuras modifiquem retroativamente dados utilizados no histórico.

---

## 15. Execução do treino pelo Aluno

Na versão atual, a execução persistida ocorre no nível do:

```text
dia de treino
```

O fluxo é:

```text
Aluno abre o treino
      │
      ▼
seleciona um dia
      │
      ▼
visualiza exercícios
      │
      ▼
executa a rotina
      │
      ▼
marca o dia como concluído
```

O mesmo dia não pode ser concluído duas vezes na mesma data local da academia.

---

## 16. Conclusão individual de exercícios

A versão atual **não possui conclusão individual persistida por exercício**.

Essa evolução está planejada para uma próxima etapa da V1.

Quando implementada, deverá permitir acompanhar progresso parcial dentro do dia de treino.

---

## 17. Histórico de treinos

Cada conclusão de um dia gera uma execução registrada.

O histórico permite consultar atividades realizadas anteriormente.

Ele permanece preservado mesmo quando:

- o treino atual é substituído;
- uma nova versão do treino é criada;
- estruturas futuras de treino são alteradas.

O Aluno acessa somente seu próprio histórico.

Admin e Professor podem consultar o histórico dos alunos da própria academia.

---

## 18. Avaliações físicas

O Avelri permite registrar avaliações físicas contendo informações como:

- data;
- peso;
- altura;
- percentual de gordura;
- medidas corporais;
- observações.

Admin e Professor podem registrar avaliações.

O Aluno pode consultar suas próprias avaliações, mas não cadastrar uma nova.

---

## 19. Reavaliação física

A avaliação mais recente é utilizada como referência atual.

A regra de reavaliação considera:

```text
AssessmentDate + 2 meses
```

como próxima data prevista.

Quando a data local da academia alcança ou ultrapassa essa data, a reavaliação passa a ser considerada pendente.

---

## 20. Multi-tenancy

O Avelri utiliza uma arquitetura multi-tenant.

Cada academia é identificada por um:

```text
GymId
```

O contexto autenticado determina quais dados podem ser acessados.

Um usuário de uma academia não deve conseguir acessar recursos de outra academia.

O isolamento é aplicado a recursos como:

- usuários;
- alunos;
- Professores;
- exercícios;
- modelos;
- treinos;
- históricos;
- avaliações físicas.

A segurança do isolamento é responsabilidade do backend.

---

## 21. Identidade visual

O Avelri possui identidade visual própria e suporta branding específico por academia.

O branding pode alterar elementos como:

- nome apresentado;
- logo;
- cores;
- tema visual.

Na implementação atual, as configurações específicas conhecidas são resolvidas localmente pelo aplicativo através do contexto da academia autenticada.

Quando não existe branding específico, é utilizada a identidade padrão:

```text
Avelri
```

---

## 22. Plataformas

A versão atual utiliza o mesmo projeto Flutter para:

```text
Android
Web
```

O Android é distribuído através da Google Play.

A versão Web é publicada separadamente.

Ambas utilizam a mesma API e as mesmas regras de negócio.

---

## 23. Segurança

O produto utiliza múltiplas camadas de proteção.

Entre elas:

- autenticação JWT;
- autorização por perfil;
- validação de usuário ativo;
- isolamento por `GymId`;
- validações no backend;
- consultas limitadas ao tenant;
- constraints de banco;
- HTTPS em Production.

Ocultar uma funcionalidade no aplicativo não substitui autorização no servidor.

---

## 24. Simplicidade

Um dos objetivos do Avelri é reduzir etapas desnecessárias.

Fluxos frequentes devem ser:

- fáceis de localizar;
- compreensíveis;
- rápidos;
- coerentes entre telas.

A evolução do produto deve evitar complexidade desnecessária para operações rotineiras.

---

## 25. Organização

Informações de:

- usuários;
- exercícios;
- modelos;
- treinos;
- execuções;
- avaliações

devem permanecer centralizadas e estruturadas.

O histórico não deve depender exclusivamente da versão atual do treino do aluno.

---

## 26. Segurança como princípio de produto

Cada usuário deve acessar somente:

```text
recursos permitidos ao seu perfil
+
dados pertencentes ao seu contexto
```

Falhas de autorização ou isolamento entre academias possuem prioridade elevada durante desenvolvimento e homologação.

---

## 27. Evolução contínua

Novas funcionalidades devem ser guiadas por:

- problemas reais;
- feedback de academias;
- feedback de profissionais;
- feedback de alunos;
- resultados da homologação;
- impacto sobre os fluxos existentes.

Nem toda melhoria identificada deve ser adicionada imediatamente ao produto.

---

## 28. Proximidade com o usuário

O desenvolvimento busca contato próximo com academias e usuários reais.

Esse contato ajuda a identificar:

- processos manuais;
- dificuldades operacionais;
- problemas de usabilidade;
- oportunidades de simplificação;
- necessidades futuras.

---

## 29. Melhorias planejadas para a V1

Entre as evoluções planejadas estão:

- conclusão individual persistida de exercícios;
- possibilidade de desfazer conclusão antes da finalização;
- retomada de treino em andamento;
- reorganização de dias e exercícios;
- refinamento dos estados vazios existentes;
- polimento de navegação e ações;
- evolução da política de inatividade e eventual exclusão definitiva de alunos.

Esses itens ainda não representam comportamento atual da aplicação.

---

## 30. Evoluções posteriores

Entre as funcionalidades planejadas para etapas futuras estão:

- progresso visual avançado;
- evolução de cargas;
- temporizador de descanso;
- vídeos demonstrativos;
- gráficos avançados de evolução física;
- notificações e lembretes.

A presença desses itens no roadmap não significa que pertençam à versão atualmente distribuída.

---

## 31. Estado atual

O Avelri encontra-se em seu ciclo inicial de produto e está em:

```text
Closed Testing
```

no Android.

A versão de referência atualmente distribuída é:

```text
1.0.0+3
```

O foco desta etapa é:

- identificar bugs;
- corrigir regressões;
- validar fluxos reais;
- validar permissões;
- validar multi-tenancy;
- verificar funcionamento em dispositivos reais;
- melhorar estabilidade antes da disponibilização pública.

Melhorias e funcionalidades futuras devem ser tratadas separadamente dos bugs da versão atual.

---

## 32. Documentos relacionados

- `docs/product/business-rules.md`
- `docs/product/roles-and-permissions.md`
- `docs/qa/homologation-guide.md`
- `docs/qa/test-scenarios.md`
- `docs/qa/known-issues.md`
- `docs/technical/architecture.md`
