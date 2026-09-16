\# Integração com Wellhub



\## Status



Spike técnico da issue #66.



Objetivo: investigar como o Avelri Gestão poderá integrar check-ins e controle de acesso de usuários Wellhub antes da implementação definitiva.



\---



\## Contexto



Academias parceiras do Wellhub podem integrar seus sistemas de gestão para centralizar check-ins, reservas e controle de acesso.



O Wellhub informa oficialmente que sistemas de gestão ainda não integrados podem solicitar integração e passar por um processo de orientação junto à equipe do Wellhub.



A AC Power Gym utiliza atualmente o Actuar, que possui integração com Gympass/Wellhub.



\---



\## Descoberta principal



A API pública disponível atualmente no Wellhub Developer Hub é direcionada principalmente a fluxos corporativos, como:



\- elegibilidade de funcionários;

\- folha de pagamento;

\- benefícios corporativos;

\- sincronização entre empresas e Wellhub.



Essa API pública não deve ser confundida com a integração de check-in e controle de acesso utilizada pelos sistemas de academias.



A integração necessária ao Avelri depende do processo específico de integração de sistemas parceiros do Wellhub.



\---



\## Processo oficial esperado



O fluxo inicial é:



```text

Avelri solicita integração

&#x20;       ↓

Wellhub analisa o sistema

&#x20;       ↓

Wellhub fornece orientações/documentação de integração

&#x20;       ↓

Desenvolvimento e homologação

&#x20;       ↓

Avelri torna-se sistema integrado

### Momento de implementação

A solicitação formal de integração ao Wellhub será adiada até que o ERP Core esteja concluído e o Avelri Gestão esteja operando em uma academia real.

Essa decisão permite apresentar ao Wellhub um produto funcional e já utilizado comercialmente, além de evitar que processos externos de aprovação, documentação ou homologação atrasem o desenvolvimento principal do ERP.

A integração Wellhub não é requisito bloqueante para a implantação do Avelri na AC Power Gym.

Após a estabilização do ERP Core e da primeira implantação real, o processo de integração será retomado através da issue #67.
