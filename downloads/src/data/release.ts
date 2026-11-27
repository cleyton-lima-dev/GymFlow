export interface DesktopRelease {
  status: 'unavailable' | 'available';
  version: string;
  requirements: null;
  downloadUrl: string;
  sizeBytes: number;
  sha256: string;
}

// A URL está preparada; o portal só exibe o link após autorização explícita.
export const desktopRelease: DesktopRelease = {
  status: 'available',
  version: '1.0.1+2',
  requirements: null,
  downloadUrl: 'https://avelri-gestao-downloads.pages.dev/updates/production/installers/AvelriGestao-Production-1.0.1+2-f3a9cdf1.exe',
  sizeBytes: 70008767,
  sha256: '03AD826D1ED3E8A9281ACEC405F18D92B2ADF961D45B22AA0022285321E76DAB',
};
