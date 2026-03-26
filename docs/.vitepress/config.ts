import { defineConfig } from 'vitepress'

export default defineConfig({
  title: 'MonadicSharp.Framework',
  description: 'Enterprise-grade AI agent infrastructure for .NET 8 — Railway-Oriented Programming at scale.',
  base: '/MonadicSharp.Framework/',
  cleanUrls: true,

  head: [
    ['meta', { property: 'og:type', content: 'website' }],
    ['meta', { name: 'twitter:card', content: 'summary' }],
  ],

  themeConfig: {
    logo: '/logo.svg',
    siteTitle: 'MonadicSharp.Framework',

    nav: [
      {
        text: 'Guide',
        items: [
          { text: 'Getting Started', link: '/getting-started' },
          { text: 'Why Framework?', link: '/why' },
          { text: 'Architecture', link: '/architecture' },
        ],
      },
      {
        text: 'Packages',
        items: [
          { text: 'Agents', link: '/packages/agents' },
          { text: 'Security', link: '/packages/security' },
          { text: 'Telemetry', link: '/packages/telemetry' },
          { text: 'Http', link: '/packages/http' },
          { text: 'Persistence', link: '/packages/persistence' },
          { text: 'Caching', link: '/packages/caching' },
        ],
      },
      {
        text: 'Ecosystem',
        items: [
          { text: 'MonadicSharp Core', link: 'https://danny4897.github.io/MonadicSharp/' },
          { text: 'NuGet', link: 'https://www.nuget.org/packages/MonadicSharp.Framework' },
        ],
      },
    ],

    sidebar: {
      '/': [
        {
          text: 'Guide',
          items: [
            { text: 'Getting Started', link: '/getting-started' },
            { text: 'Why Framework?', link: '/why' },
            { text: 'Architecture', link: '/architecture' },
          ],
        },
        {
          text: 'Packages',
          items: [
            { text: 'Agents', link: '/packages/agents' },
            { text: 'Security', link: '/packages/security' },
            { text: 'Telemetry', link: '/packages/telemetry' },
            { text: 'Http', link: '/packages/http' },
            { text: 'Persistence', link: '/packages/persistence' },
            { text: 'Caching', link: '/packages/caching' },
          ],
        },
      ],
    },

    socialLinks: [
      { icon: 'github', link: 'https://github.com/Danny4897/MonadicSharp.Framework' },
    ],

    search: { provider: 'local' },

    footer: {
      message: 'Released under the MIT License.',
      copyright: 'Copyright © 2024–2026 Danny4897',
    },

    outline: { level: [2, 3], label: 'On this page' },
  },

  markdown: {
    theme: { light: 'github-light', dark: 'one-dark-pro' },
  },
})
