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
  version: '1.0.0+1',
  requirements: null,
  downloadUrl: 'https://avelri-gestao-downloads.pages.dev/updates/production/installers/AvelriGestao-Production-1.0.0+1-0ba7dc25.exe',
  sizeBytes: 31575668,
  sha256: '500A7C82D34CC2E9D502F5FA24EF59D26729B7856E09094A2AC6C2BC4B1858EA',
};
