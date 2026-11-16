# Avelri Downloads

Portal público e estático do Avelri Gestão Desktop. Projeto Astro independente dos aplicativos `mobile` e `platform`.

## Desenvolvimento local

```powershell
cd downloads
pnpm install --frozen-lockfile
pnpm dev
```

Abra o endereço local exibido pelo Astro.

## Verificações

```powershell
pnpm check
pnpm build
```

O build estático é gerado em `dist/`. O projeto Pages comercial é `avelri-gestao-downloads`. O portal publicado oferece o instalador Windows Production `1.0.0+1` aprovado, com origem oficial, tamanho e SHA-256 visíveis.

A captura do produto permanece identificada como placeholder.

## Rotas de atualização Production

`functions/updates/production/[[path]].js` lê somente o binding R2 privado `RELEASES`. Não há listagem nem escrita. As únicas correspondências permitidas são:

| URL relativa | Chave R2 no bucket `avelri-gestao-releases-prod` |
| --- | --- |
| `/updates/production/release.json` | `production/release.json` |
| `/updates/production/release.sig` | `production/release.sig` |
| `/updates/production/installers/AvelriGestao-Production-<versão>+<build>-<commit8>.exe` | `production/installers/AvelriGestao-Production-<versão>+<build>-<commit8>.exe` |

`<versão>` tem três números separados por pontos; `<build>` é inteiro positivo; `<commit8>` contém oito dígitos hexadecimais minúsculos. O nome do instalador corresponde ao emitido por `Build-ProductionInstaller.ps1`. GET e HEAD retornam 404 para objetos ausentes; outros métodos em caminhos permitidos retornam 405. Binding ausente ou falha de leitura R2 retorna 503, sem fallback. Manifesto e assinatura usam `Cache-Control: no-store`; instaladores versionados usam cache imutável e suas chaves nunca devem ser sobrescritas. Publique bytes alterados somente sob outro nome versionado. O bucket continua privado.

Caminhos não canônicos recebidos pela Function retornam 404. Se o cliente ou a plataforma normalizar um caminho antes da Function, ela só verá a rota final; em todos os casos, apenas as chaves acima podem ser consultadas.

`public/_routes.json` limita a invocação de Functions a `/updates/production/*`; a landing estática não invoca R2. O upload manual pelo navegador não compila a pasta `functions/`. Em um **deploy futuro autorizado**, execute a partir de `downloads/`:

```powershell
pnpm build
wrangler pages deploy dist --project-name=avelri-gestao-downloads --branch=<branch-Production-confirmada>
```

Confirme no painel a branch Production antes do comando: sem `--branch`, Wrangler pode inferir a branch Git atual e criar um Preview. Não adicione um arquivo Wrangler com bindings locais para substituir a configuração `RELEASES` já salva no projeto. Para releases futuros, aprove versão, instalador e identidade Ed25519 antes do upload; publique instalador, assinatura e manifesto nessa ordem, com o manifesto por último. Nunca sobrescreva uma chave de instalador versionado.
