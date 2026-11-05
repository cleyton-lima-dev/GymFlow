export interface DesktopRelease {
  status: 'unavailable';
  version: null;
  requirements: null;
  downloadUrl: null;
}

// Dados de publicação ainda não aprovados. Nenhum link deve ser inferido aqui.
export const desktopRelease: DesktopRelease = {
  status: 'unavailable',
  version: null,
  requirements: null,
  downloadUrl: null,
};
