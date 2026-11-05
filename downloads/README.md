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

O build estático é gerado em `dist/`. Quando houver autorização para publicação, uma configuração de Pages poderá usar `downloads` como diretório raiz do projeto, `pnpm build` como comando e `dist` como saída.

O botão de download permanece desativado até a aprovação de versão, requisitos, instalador e endereço oficiais. A captura do produto também permanece identificada como placeholder.
