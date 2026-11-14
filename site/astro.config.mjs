// Documentation site for nanora-unity-mcp, published to GitHub Pages from the public repository.
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';
import starlightLlmsTxt from 'starlight-llms-txt';

const site = 'https://nanora-sama.github.io';
const base = '/nanora-unity-mcp';
const repo = 'https://github.com/nanora-sama/nanora-unity-mcp';

// Lets search engines read what the package is, who makes it and what it costs.
const jsonLd = {
  '@context': 'https://schema.org',
  '@type': 'SoftwareApplication',
  name: 'nanora-unity-mcp',
  applicationCategory: 'DeveloperApplication',
  operatingSystem: 'Windows, macOS, Linux (Unity Editor 2021.3 or later)',
  description: 'An MCP server that runs inside the Unity Editor. Connect Claude Code, Codex, Cursor or any MCP client to build, play, test and profile a Unity project. No Node.js or Python.',
  url: `${site}${base}/`,
  downloadUrl: `${repo}/releases`,
  codeRepository: repo,
  license: 'https://opensource.org/licenses/MIT',
  offers: { '@type': 'Offer', price: '0', priceCurrency: 'USD' },
  author: { '@type': 'Person', name: 'nanora', url: 'https://github.com/nanora-sama' },
};

export default defineConfig({
  site,
  base,
  trailingSlash: 'always',
  integrations: [
    starlight({
      title: 'nanora-unity-mcp',
      description: 'An MCP server that runs inside the Unity Editor — for Claude Code, Codex, Cursor and any MCP client. No Node.js or Python.',
      logo: { src: './src/assets/icon.png', alt: '' },
      favicon: '/favicon.png',
      defaultLocale: 'root',
      locales: {
        root: { label: 'English', lang: 'en' },
        ja: { label: '日本語', lang: 'ja' },
      },
      social: [{ icon: 'github', label: 'GitHub', href: repo }],
      editLink: { baseUrl: `${repo}/edit/main/site/` },
      lastUpdated: true,
      routeMiddleware: './src/route-data.ts',
      head: [
        { tag: 'meta', attrs: { property: 'og:image', content: `${site}${base}/og.png` } },
        { tag: 'meta', attrs: { name: 'twitter:card', content: 'summary_large_image' } },
        { tag: 'script', attrs: { type: 'application/ld+json' }, content: JSON.stringify(jsonLd) },
      ],
      sidebar: [
        {
          label: 'Getting started', translations: { ja: 'はじめに' },
          items: ['getting-started/install', 'getting-started/first-run'],
        },
        {
          label: 'Connect your AI', translations: { ja: 'AI をつなぐ' },
          items: ['connect/claude-code', 'connect/codex', 'connect/cursor', 'connect/claude-desktop', 'connect/other-clients'],
        },
        {
          label: 'Guides', translations: { ja: 'ガイド' },
          items: ['guides/testing', 'guides/skills', 'guides/supersave', 'guides/multiple-projects', 'guides/domain-reload', 'guides/security'],
        },
        {
          label: 'Reference', translations: { ja: 'リファレンス' },
          items: ['reference/u-selector', 'reference/endpoints'],
        },
        {
          label: 'Tools', translations: { ja: 'ツール' },
          collapsed: true,
          items: [{ autogenerate: { directory: 'tools' } }],
        },
        {
          label: 'Help', translations: { ja: 'ヘルプ' },
          items: ['troubleshooting', 'faq', 'comparison'],
        },
      ],
      plugins: [
        starlightLlmsTxt({
          projectName: 'nanora-unity-mcp',
          description: 'An MCP server that runs inside the Unity Editor. Claude Code, Codex, Cursor and any MCP client can build, play, test and profile a Unity project through it.',
          exclude: ['ja/**'],
        }),
      ],
    }),
  ],
});
