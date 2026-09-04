# Requisitos Não Funcionais — Avelri

**Produto:** Avelri
**Versão de referência:** 1.0.0+3
**Última atualização:** 04/09/2026

---

## Objetivo

Este documento registra os requisitos não funcionais da versão atual do Avelri.

Eles descrevem características de qualidade relacionadas a:

- desempenho;
- usabilidade;
- segurança;
- multi-tenancy;
- confiabilidade;
- compatibilidade;
- manutenção;
- testabilidade;
- configuração;
- privacidade;
- operação.

Este documento não estabelece métricas quantitativas que ainda não tenham sido formalmente medidas ou definidas.

---

# Desempenho

## RNF-001 — Fluidez em dispositivos Android suportados

O aplicativo deverá permanecer utilizável e responsivo nos dispositivos Android compatíveis com a versão atual.

Operações de rede não deverão bloquear a interface principal enquanto aguardam resposta da API.

Estados de carregamento deverão ser apresentados quando necessário.

---

## RNF-002 — Consumo responsável de recursos

O aplicativo deverá evitar consumo desnecessário de:

- memória;
- processamento;
- rede;
- armazenamento.

Novas funcionalidades deverão evitar manter objetos, requisições ou estados ativos sem necessidade.

---

## RNF-003 — Tempo de resposta percebido

Fluxos frequentes deverão fornecer feedback visual imediato quando dependerem de processamento ou comunicação com a API.

O usuário não deverá interpretar uma operação em andamento como travamento da aplicação.

Podem ser utilizados recursos como:

- indicadores de carregamento;
- botões temporariamente desabilitados;
- estados de submissão;
- mensagens de erro;
- refresh.

---

## RNF-004 — Timeout de comunicação

O cliente HTTP deverá evitar espera indefinida por respostas da API.

A implementação atual utiliza timeout padrão de:

```text
15 segundos
```

Após o limite, a aplicação deverá tratar a falha de forma controlada.

---

# Usabilidade

## RNF-005 — Navegação simples

A navegação deverá ser compreensível e buscar reduzir interações desnecessárias nos fluxos frequentes.

A organização deverá permanecer coerente com o perfil autenticado.

---

## RNF-006 — Feedback de estado

Telas dependentes de operações assíncronas deverão tratar, quando aplicável:

```text
Loading
Success
Empty
Error
Submitting
```

A aplicação não deverá deixar o usuário sem indicação do estado de uma operação relevante.

---

## RNF-007 — Consistência visual

Componentes equivalentes deverão possuir comportamento visual consistente.

Estados como:

```text
habilitado
desabilitado
carregando
erro
selecionado
```

deverão ser visualmente distinguíveis.

Problemas que façam um botão habilitado aparentar estar desativado deverão ser tratados como defeitos de UX/UI.

---

## RNF-008 — Responsividade

A interface deverá permanecer utilizável em diferentes dimensões de tela suportadas.

Devem ser evitados:

- overflow;
- conteúdo essencial cortado;
- texto comprimido indevidamente;
- botões inacessíveis;
- layouts incompatíveis com telas estreitas.

A validação deverá incluir dispositivos Android reais e Web quando aplicável.

---

# Acessibilidade

## RNF-009 — Evolução de acessibilidade

A evolução da interface deverá considerar progressivamente:

- contraste;
- legibilidade;
- escalonamento de texto;
- áreas de toque;
- semântica;
- navegação compreensível;
- tecnologias assistivas.

A versão atual não deve ser considerada como tendo passado por auditoria completa de acessibilidade.

---

# Compatibilidade

## RNF-010 — Compatibilidade Android

O aplicativo deverá suportar dispositivos compatíveis com:

```text
minSdk = 25
```

O identificador Android deverá permanecer:

```text
com.cleytonlimadev.avelri
```

para preservar a continuidade das atualizações pela Google Play.

---

## RNF-011 — Compatibilidade Web

A aplicação Web deverá utilizar os mesmos contratos principais da API utilizados pelo Android.

Diferenças funcionais não intencionais entre Android e Web deverão ser investigadas como possíveis defeitos.

---

## RNF-012 — Compatibilidade entre versões de cliente e API

Atualizações do backend deverão considerar que versões anteriores do aplicativo podem permanecer instaladas.

Mudanças incompatíveis nos contratos da API deverão ser evitadas ou tratadas através de estratégia apropriada de evolução/versionamento.

---

# Multi-tenancy

## RNF-013 — Isolamento entre academias

A aplicação deverá manter isolamento lógico entre academias em todos os ambientes.

Nenhuma funcionalidade poderá depender apenas do cliente Flutter para garantir esse isolamento.

A validação deverá ocorrer no backend.

---

## RNF-014 — Multi-tenancy consistente entre ambientes

As mesmas regras de isolamento deverão ser utilizadas em:

```text
Development
Production
```

Não deverá existir configuração de desenvolvimento que ignore deliberadamente a proteção por tenant nos fluxos normais.

---

# Segurança

## RNF-015 — Comunicação segura em Production

Toda comunicação externa dos clientes com a API em Production deverá utilizar:

```text
HTTPS
```

O cliente Flutter deverá rejeitar uma `API_BASE_URL` HTTP quando:

```text
APP_ENV=production
```

---

## RNF-016 — Autenticação e autorização no servidor

Recursos protegidos deverão utilizar autenticação e autorização no backend.

A interface Flutter poderá ocultar ações conforme o perfil, mas essa ocultação não deverá ser considerada mecanismo suficiente de segurança.

---

## RNF-017 — Proteção da sessão

O JWT deverá ser tratado como dado sensível.

No Flutter, o token deverá ser armazenado por mecanismo destinado a armazenamento seguro.

A implementação atual utiliza:

```text
flutter_secure_storage
```

O token não deverá ser:

- hardcoded;
- enviado em query string;
- registrado desnecessariamente em logs;
- incluído em documentação;
- incluído em Issues.

---

## RNF-018 — Invalidação de usuário inativo

A API deverá verificar o estado atual do usuário durante requisições autenticadas.

A inativação do usuário deverá invalidar efetivamente o uso posterior de um token anteriormente emitido.

---

## RNF-019 — Proteção de secrets

Não deverão ser incorporados ao código-fonte ou aos artefatos públicos:

- senhas;
- connection strings reais;
- JWT signing keys;
- credenciais PostgreSQL;
- credenciais administrativas;
- keystores;
- senhas de assinatura;
- tokens de infraestrutura.

Secrets do backend deverão ser fornecidos por mecanismos de configuração externos.

---

## RNF-020 — CORS restrito

Em Production, o backend deverá aceitar requisições Web somente de origens explicitamente autorizadas.

CORS não deverá substituir autenticação, autorização ou multi-tenancy.

---

## RNF-021 — Proteção contra abuso no login

O endpoint de autenticação deverá possuir proteção contra excesso de tentativas.

A configuração atual limita:

```text
10 tentativas por minuto por IP
```

ao endpoint de login.

Quando excedido, a API deverá responder com:

```text
429 Too Many Requests
```

---

# Privacidade

## RNF-022 — Minimização de exposição de dados

A aplicação deverá evitar expor dados além dos necessários para cada operação.

Respostas da API não deverão retornar informações como:

- `PasswordHash`;
- signing keys;
- connection strings;
- secrets;
- dados de outro tenant.

---

## RNF-023 — Segurança de logs e evidências

Logs, screenshots e evidências de homologação deverão evitar exposição desnecessária de:

- senhas;
- JWTs;
- telefone;
- e-mail;
- avaliações físicas reais;
- credenciais;
- identificadores privados de tenant.

Dados fictícios deverão ser utilizados sempre que possível durante homologação.

---

# Confiabilidade e integridade

## RNF-024 — Integridade de operações críticas

Operações que alteram múltiplos registros relacionados deverão evitar persistência parcial quando o caso de uso exigir atomicidade.

Isso inclui fluxos como:

```text
desativar treino anterior
+
criar novo treino
```

---

## RNF-025 — Integridade através do banco

Regras importantes deverão possuir constraints no PostgreSQL quando apropriado.

Exemplos atuais:

- e-mail único;
- um único `Student` por `User`;
- nome de exercício único por academia;
- nome de modelo único por academia;
- um único treino ativo por aluno;
- ordem única dentro de dias;
- uma execução por dia/data;
- uma avaliação por aluno/data.

---

## RNF-026 — Preservação de histórico

Alterações de treino não deverão modificar retroativamente registros utilizados para representar execuções históricas.

Quando necessário, o sistema deverá utilizar versionamento/inativação em vez de reescrever a estrutura histórica.

---

# Fuso horário

## RNF-027 — Regras de calendário independentes do timezone do servidor

Regras de negócio dependentes de data deverão utilizar o timezone configurado para a academia.

Isso se aplica, entre outros, a:

- `ExecutionDate`;
- conclusão do dia atual;
- avaliação física;
- reavaliação.

O sistema não deverá assumir silenciosamente UTC como data local da academia quando a configuração necessária estiver ausente.

---

# Configuração

## RNF-028 — Configuração externa do backend

Valores dependentes do ambiente deverão ser fornecidos por configuração externa.

Exemplos:

```text
ConnectionStrings:DefaultConnection
Jwt:Key
Jwt:Issuer
Jwt:Audience
Jwt:ExpirationMinutes
Cors:AllowedOrigins
GymTimeZones:{GymId}
```

---

## RNF-029 — Configuração externa do Flutter

O cliente deverá receber configuração de ambiente através de:

```text
APP_ENV
API_BASE_URL
```

A aplicação deverá rejeitar configurações obrigatórias ausentes ou inválidas.

---

## RNF-030 — Separação entre Development e Production

Development e Production deverão possuir configuração explicitamente diferenciada.

A aplicação não deverá depender de alteração manual de URLs espalhadas pelas telas para trocar de ambiente.

---

# Manutenibilidade

## RNF-031 — Separação de responsabilidades no backend

A arquitetura deverá preservar responsabilidades distintas entre:

```text
GymFlow.Api
GymFlow.Application
GymFlow.Domain
GymFlow.Infrastructure
```

Regras de negócio não deverão ser concentradas desnecessariamente nos Controllers.

---

## RNF-032 — Separação de responsabilidades no Flutter

A interface deverá manter separação adequada entre:

```text
Page
ViewModel
Service
ApiClient
```

Telas não deverão concentrar diretamente regras complexas de comunicação e persistência.

---

## RNF-033 — Código analisável e formatado

Mudanças Flutter deverão permanecer compatíveis com:

```powershell
flutter analyze
```

e os arquivos alterados deverão manter formatação válida através das ferramentas Dart.

---

## RNF-034 — Migrations versionadas

Alterações estruturais no PostgreSQL deverão ser registradas através de migrations versionadas no repositório.

Mudanças manuais de schema em Production não deverão substituir permanentemente o histórico de migrations.

---

# Testabilidade

## RNF-035 — Testes automatizados backend

Regras críticas do backend deverão possuir cobertura automatizada sempre que viável.

A suíte deverá ser executável através de:

```powershell
dotnet test
```

---

## RNF-036 — Testes Flutter

O cliente deverá manter testes automatizados para fluxos relevantes sempre que viável.

Comando:

```powershell
flutter test
```

---

## RNF-037 — Homologação complementar

Testes automatizados não deverão substituir completamente a homologação manual.

A versão distribuída deverá ser validada em cenários como:

- dispositivo Android real;
- diferentes perfis;
- navegação;
- multi-tenancy;
- branding;
- fluxos críticos;
- erros visuais.

---

# Operação e observabilidade

## RNF-038 — Health check

A API deverá possuir endpoint público de disponibilidade básica:

```text
/health
```

Esse endpoint deverá permitir verificar se o serviço está respondendo sem exigir autenticação.

---

## RNF-039 — Logging seguro

A API deverá registrar informações úteis para diagnóstico sem incluir deliberadamente:

- senhas;
- JWT completo;
- signing keys;
- connection strings;
- secrets;
- dados pessoais sem necessidade.

---

## RNF-040 — Menor privilégio no banco

A identidade utilizada pela API em Production deverá possuir somente as permissões necessárias para seu funcionamento normal.

Permissões administrativas para migrations deverão ser tratadas separadamente quando necessário.

---

# Disponibilidade e recuperação

## RNF-041 — Falhas tratadas de forma controlada

Falhas de rede, timeout, respostas inválidas ou indisponibilidade temporária da API não deverão causar exposição de detalhes internos ou encerramento inesperado do aplicativo quando puderem ser tratadas.

A interface deverá apresentar erro compreensível e permitir recuperação quando apropriado.

---

## RNF-042 — Rollback considerado em mudanças críticas

Deploys que alterem backend ou banco deverão considerar previamente:

- compatibilidade com a versão anterior;
- migrations envolvidas;
- impacto sobre dados;
- possibilidade de recuperação.

Rollback de banco não deverá ser executado automaticamente sem análise de perda de dados.

---

# Evolução

## RNF-043 — Compatibilidade com crescimento multi-tenant

Novas funcionalidades deverão continuar respeitando o modelo de múltiplas academias.

Uma funcionalidade não deverá ser implementada assumindo que existe apenas uma academia no sistema.

---

## RNF-044 — Documentação sincronizada

Mudanças relevantes em:

- arquitetura;
- endpoints;
- regras;
- banco;
- permissões;
- ambientes;
- deploy

deverão ser refletidas na documentação versionada quando aplicável.

---

# Métricas ainda não formalizadas

A versão atual ainda não possui SLAs ou metas quantitativas formalmente definidas para itens como:

```text
tempo máximo de inicialização
latência máxima por endpoint
consumo máximo de RAM
disponibilidade mensal
requisições por segundo
número máximo de academias
número máximo de usuários simultâneos
```

Essas métricas deverão ser estabelecidas futuramente com base em:

- medições reais;
- carga observada;
- necessidades comerciais;
- capacidade da infraestrutura;
- testes de desempenho.

Não devem ser inventados limites numéricos sem evidência técnica.

---

# Documentos relacionados

- `docs/requirements/functional-requirements.md`
- `docs/product/business-rules.md`
- `docs/product/roles-and-permissions.md`
- `docs/technical/architecture.md`
- `docs/technical/api.md`
- `docs/technical/database.md`
- `docs/technical/mobile.md`
- `docs/operations/environments.md`
- `docs/operations/deployment.md`
