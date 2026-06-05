import type { ForgeConfig } from '@electron-forge/shared-types';
import { MakerZIP } from '@electron-forge/maker-zip';
import { MakerDeb } from '@electron-forge/maker-deb';
import { MakerRpm } from '@electron-forge/maker-rpm';
import { VitePlugin } from '@electron-forge/plugin-vite';
import { FusesPlugin } from '@electron-forge/plugin-fuses';
import { FuseV1Options, FuseVersion } from '@electron/fuses';
import { AutoUnpackNativesPlugin } from '@electron-forge/plugin-auto-unpack-natives';

const config: ForgeConfig = {
  packagerConfig: {
    name: 'NERIS Sublingual',
    executableName: 'neris-sublingual',
    asar: {
      unpackDir: 'node_modules/**/*.node',
    },
    icon: 'assets/logo',
    appBundleId: 'com.neris.sublingual',
    appCategoryType: 'public.app-category.utilities',
    extraResource: ["bin", "native"],
    prune: false,
    ignore: (file: string) => {
      if (!file) return false;
      if (file.startsWith('/node_modules/.pnpm')) return true;
      if (file.startsWith('/node_modules/.bin')) return true;
      if (file.startsWith('/node_modules/.vite')) return true;
      if (file === '/node_modules/.modules.yaml') return true;
      if (file === '/node_modules/.pnpm-workspace-state-v1.json') return true;
      const devOnly = [
        '/node_modules/electron',
        '/node_modules/typescript',
        '/node_modules/eslint',
        '/node_modules/vite',
        '/node_modules/tailwindcss',
        '/node_modules/@tailwindcss',
        '/node_modules/@vitejs',
        '/node_modules/@electron-forge',
        '/node_modules/@electron',
        '/node_modules/@types',
        '/node_modules/@typescript-eslint',
        '/node_modules/electron-winstaller',
        '/node_modules/electron-squirrel-startup',
      ];
      for (const pattern of devOnly) {
        if (file.startsWith(pattern)) return true;
      }
      return false;
    },
  },
  rebuildConfig: {},
  makers: [
    new MakerZIP({}, ['darwin', 'win32']),
    new MakerRpm({}),
    new MakerDeb({}),
  ],
  plugins: [
    new AutoUnpackNativesPlugin({}),
    new VitePlugin({
      build: [
        {
          entry: 'src/main.ts',
          config: 'vite.main.config.ts',
          target: 'main',
        },
        {
          entry: 'src/main/asr/vosk-worker.ts',
          config: 'vite.main.config.ts',
          target: 'main',
        },
        {
          entry: 'src/preload.ts',
          config: 'vite.preload.config.ts',
          target: 'preload',
        },
        {
          entry: 'src/overlay/overlay-preload.ts',
          config: 'vite.preload.config.ts',
          target: 'preload',
        },
      ],
      renderer: [
        {
          name: 'main_window',
          config: 'vite.renderer.config.ts',
        },
      ],
    }),
    new FusesPlugin({
      version: FuseVersion.V1,
      [FuseV1Options.RunAsNode]: false,
      [FuseV1Options.EnableCookieEncryption]: true,
      [FuseV1Options.EnableNodeOptionsEnvironmentVariable]: false,
      [FuseV1Options.EnableNodeCliInspectArguments]: false,
      [FuseV1Options.EnableEmbeddedAsarIntegrityValidation]: true,
      [FuseV1Options.OnlyLoadAppFromAsar]: true,
    }),
  ],
};

export default config;
