# Avelri Platform — issue #77

Aplicativo Flutter Web separado do Avelri Gestão. Nenhum cadastro público de academias ou operadores.

## Configuração da API

O Platform permanece desabilitado quando `Platform:Enabled` está ausente ou false. Para um ambiente autorizado, configurar por gerenciador de segredos/variáveis de ambiente:

- `Platform__Enabled=true`.
- `Platform__Jwt__Key`: segredo aleatório exclusivo, pelo menos 32 bytes; diferente de `Jwt:Key`.
- `Platform__Jwt__Issuer` e `Platform__Jwt__Audience`: exclusivos; padrões `avelri-platform` e `avelri-platform-web`.
- `Platform__MfaEncryptionKey`: chave AES-256 aleatória (32 bytes), codificada em Base64, exclusiva. Não perder esta chave: protege os segredos TOTP persistidos. Rotação/recovery exige procedimento operacional próprio, não incluído nesta issue.
- `Cors__AllowedOrigins`: origem exata do Web. Não usar wildcard.

Preservar as configurações existentes de banco e JWT das academias. A API não aplica migrations automaticamente. A migration `AddPlatformOnboarding` é aditiva, sem seed, backfill ou alteração de `GymFeatures`.

## Primeiro operador

Com schema já preparado em PostgreSQL local isolado e destino explicitamente confirmado:

```powershell
dotnet run --project src/GymFlow.Api --configuration Release --no-launch-profile -- --bootstrap-platform-operator
```

Exige console interativo, Platform habilitado, ambiente diferente de Production e banco loopback diferente de `gymflow_dev`. Confirma o nome do banco antes de conectar. Solicita nome, e-mail, senha oculta e confirmação. Exibe uma única chave TOTP para cadastrar no autenticador; exige código válido antes de persistir. Não gravar/transcrever o console. Não inicia HTTP, não aplica migrations e não permite um segundo bootstrap. O identificador fixo do primeiro operador é apenas trava de unicidade, nunca credencial.

## Web e convite

Na pasta `platform`:

```powershell
flutter run -d chrome --web-port=8077 --dart-define=API_BASE_URL=http://127.0.0.1:5177
flutter build web --dart-define=API_BASE_URL=http://127.0.0.1:5177
```

Essas URLs servem apenas à homologação local. Fora de loopback a URL da API exige HTTPS. Publicação e uso de Production não estão autorizados.

O operador entra com senha e TOTP; sessão de 15 minutos, somente em memória. O convite é exibido uma vez e entregue manualmente por canal seguro: não há envio automático de e-mail. Usa fragmento de URL, removido ao carregar. Validade de 24 horas; reemissão revoga o anterior. Nenhuma senha padrão é compartilhada. Logout encerra a sessão local; desativação/versão de credencial revoga acesso no servidor.

Pareamento e operação de equipamentos permanecem nos fluxos existentes do Gestão. O operador registra a referência da homologação física quando prevista no contrato; telemetria online e decisões Allowed não concluem essa etapa. Testes sintéticos não homologam hardware real.

## Validação

`flutter analyze`, `flutter test`, `flutter build web`. Testes backend `PlatformOnboardingTests` e `PlatformPostgresTests`. Os testes PostgreSQL exigem cluster descartável novo e verificado; nunca usar connection strings de ambientes existentes. Bootstrap e segredos reais não são necessários nos testes automatizados.
