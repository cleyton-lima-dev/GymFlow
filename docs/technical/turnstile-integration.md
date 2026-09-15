\# Integração com catracas físicas



\## Status



Spike técnico da issue #81.



Objetivo: definir a arquitetura inicial para integração do Avelri Gestão com catracas físicas, sem acoplar o ERP a um fabricante específico.



\---



\## Cenário inicial



A catraca utilizada pela AC Power Gym foi identificada visualmente como sendo da fabricante \*\*Toletus\*\*.



O modelo exato não pôde ser confirmado porque o equipamento é antigo e não possui mais etiqueta ou identificação legível.



Para permitir o avanço do projeto, será adotada como hipótese inicial a família:



\*\*Toletus LiteNet2\*\*



Essa identificação deve ser tratada como hipótese técnica até futura validação em equipamento físico.



\---



\## Primeira integração a ser homologada



Fabricante:



\- Toletus



Família assumida:



\- LiteNet2



Comunicação esperada:



\- TCP/IP

\- rede local da academia

\- IPv4

\- porta padrão esperada: 7878



A integração deve ser construída de forma que o restante do ERP não dependa diretamente do protocolo da Toletus.



\---



\## Arquitetura escolhida



```text

Catraca física

&#x20;     ↓

Adaptador do fabricante

&#x20;     ↓

Agente local Avelri

&#x20;     ↓ HTTPS

API Avelri

&#x20;     ↓

Regras de acesso do ERP
