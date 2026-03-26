import { defineConfig } from 'vitepress'

export default defineConfig({
  title: 'MonadicSharp.Framework',
  description: 'Enterprise-grade AI agent infrastructure for .NET 8 — Railway-Oriented Programming at scale.',
  base: '/MonadicSharp.Framework/',
  cleanUrls: true,
  ignoreDeadLinks: true,

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
          {
            text: 'Core',
            items: [
              { text: 'MonadicSharp', link: 'https://danny4897.github.io/MonadicSharp/' },
              { text: 'MonadicSharp.Framework', link: 'https://danny4897.github.io/MonadicSharp.Framework/' },
            ],
          },
          {
            text: 'Extensions',
            items: [
              { text: 'MonadicSharp.AI', link: 'https://danny4897.github.io/MonadicSharp.AI/' },
              { text: 'MonadicSharp.Recovery', link: 'https://danny4897.github.io/MonadicSharp.Recovery/' },
              { text: 'MonadicSharp.Azure', link: 'https://danny4897.github.io/MonadicSharp.Azure/' },
              { text: 'MonadicSharp.DI', link: 'https://danny4897.github.io/MonadicSharp.DI/' },
            ],
          },
          {
            text: 'Tooling',
            items: [
              { text: 'MonadicLeaf', link: 'https://danny4897.github.io/MonadicLeaf/' },
              { text: 'MonadicSharp × OpenCode', link: 'https://danny4897.github.io/MonadicSharp-OpenCode/' },
              { text: 'AgentScope', link: 'https://danny4897.github.io/AgentScope/' },
            ],
          },
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
