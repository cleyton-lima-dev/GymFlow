# Configurações administrativas da academia — #76

`GET /api/gym-settings` e `PUT /api/gym-settings` são exclusivos de Admin.
Academia e autor são obtidos do JWT validado. O corpo não aceita `gymId`,
`actorUserId`, `erpEnabled`, credenciais ou propriedades desconhecidas.

## Persistência e edição

Uma linha em `GymSettings` por GymId contém `AdministrativeName` (nome fantasia,
até 150 caracteres), `TimeZoneId` (IANA, até 100 caracteres), `Version`,
`CreatedAt` e `UpdatedAt`. O nome não é inferido do branding.

O GET retorna `administrativeName`, `timeZoneId`, `timeZoneStatus`,
`timeZoneSource` e `version`. Sem registro persistido, nome e versão são nulos.
O timezone pode continuar disponível pela configuração legada.

O PUT recebe os campos abaixo:

```json
{
  "administrativeName": "Academia sintética",
  "timeZoneId": "America/Sao_Paulo",
  "version": null,
  "confirmTimeZoneChange": true
}
```

`version: null` significa primeira gravação. Edições devem enviar a versão do
último GET. A chave primária protege a criação concorrente e o token de versão
protege edições. Conflitos retornam HTTP 409, sem tentativa automática de gravação.
Campos inválidos, desconhecidos ou mudança de fuso sem confirmação retornam 400.

## Timezone e compatibilidade

A configuração persistida tem prioridade. Somente a ausência de registro permite
fallback para `GymTimeZones:{GymId}`. Ausência das duas fontes continua explícita;
não existe default UTC. Configuração persistida inválida não é ocultada pelo fallback.

O provider e o repositório são scoped. A leitura é reaproveitada por GymId dentro
do escopo, mantendo um snapshot para a operação. Novos escopos consultam o valor
salvo; não há cache global de configurações persistidas.

A confirmação visual explica que o novo fuso pode mudar a interpretação de hoje,
a vigência de matrículas, cobranças vencidas e períodos de relatórios e frequência.
Datas contratuais e timestamps históricos não são convertidos. A interpretação
local usada em novas consultas pode mudar.

Login e JWT não criam configurações. A política `GymFeatures:{GymId}:ErpEnabled`
permanece independente, sem alteração ou opção de edição nesta funcionalidade.
Agentes continuam usando suas credenciais e controles operacionais existentes.

## Auditoria e migration

`GymSettingsAuditLogs` registra GymId, Admin responsável, valores anteriores e
novos e instante UTC. A primeira gravação inclui o timezone legado efetivo no
estado anterior, quando disponível. Configuração e auditoria são salvas na mesma
transação de `SaveChanges`; falha de auditoria desfaz a alteração.
A auditoria administrativa não usa `FinancialAuditLogs`.

`20261012203545_AddGymSettingsAndAudit` cria somente essas duas tabelas,
relacionamentos da auditoria e índices. Não preenche tenants, não altera tabelas
anteriores e não habilita ERP. Foi validada somente em PostgreSQL descartável.
Aplicação em `gymflow_dev`, bancos existentes ou Production não foi autorizada
nesta tarefa. Homologação e autorização de implantação permanecem necessárias.
