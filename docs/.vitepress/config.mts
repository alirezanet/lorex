import { defineConfig } from 'vitepress'

export default defineConfig({
  vite: {
    resolve: {
      preserveSymlinks: true,
    },
  },

  title: 'Lorex',
  description: 'The Shared Knowledge Registry for AI Agents and People',
  base: '/Lorex/',

  head: [
    ['link', { rel: 'icon', type: 'image/svg+xml', href: '/Lorex/logo.svg' }],
    ['meta', { name: 'og:type', content: 'website' }],
    ['meta', { name: 'og:title', content: 'Lorex — AI Skill Manager' }],
    ['meta', { name: 'og:description', content: 'Stop repeating yourself to AI. Version-control your knowledge and share it across every agent.' }],
  ],

  themeConfig: {
    logo: '/logo.svg',
    siteTitle: 'Lorex',

    nav: [
      { text: 'Guide', link: '/guide/getting-started', activeMatch: '/guide/' },
      { text: 'Reference', link: '/reference/commands', activeMatch: '/reference/' },
      { text: 'Contributing', link: '/contributing' },
      {
        text: 'Links',
        items: [
          { text: 'GitHub', link: 'https://github.com/alirezanet/lorex' },
          { text: 'Releases', link: 'https://github.com/alirezanet/lorex/releases' },
          { text: 'Report an Issue', link: 'https://github.com/alirezanet/lorex/issues' },
        ],
      },
    ],

    sidebar: {
      '/guide/': [
        {
          text: 'Introduction',
          items: [
            { text: 'Getting Started', link: '/guide/getting-started' },
            { text: 'Core Concepts', link: '/guide/concepts' },
            { text: 'How It Works', link: '/guide/how-it-works' },
          ],
        },
        {
          text: 'Workflows',
          items: [
            { text: 'Working with Skills', link: '/guide/skills' },
            { text: 'Team Registry', link: '/guide/team-registry' },
          ],
        },
      ],
      '/reference/': [
        {
          text: 'Reference',
          items: [
            { text: 'Commands', link: '/reference/commands' },
            { text: 'Adapters', link: '/reference/adapters' },
            { text: 'Skill Format', link: '/reference/skill-format' },
            { text: 'Registry Policy', link: '/reference/registry-policy' },
            { text: 'Troubleshooting', link: '/reference/troubleshooting' },
          ],
        },
      ],
    },

    socialLinks: [
      { icon: 'github', link: 'https://github.com/alirezanet/lorex' },
    ],

    footer: {
      message: 'Released under the MIT License.',
      copyright: 'Copyright © 2025-present AliReza Sabouri',
    },

    search: {
      provider: 'local',
    },

    editLink: {
      pattern: 'https://github.com/alirezanet/lorex/edit/main/docs/:path',
      text: 'Edit this page on GitHub',
    },

    lastUpdated: {
      text: 'Updated at',
      formatOptions: {
        dateStyle: 'medium',
        timeStyle: 'short',
      },
    },
  },
})
