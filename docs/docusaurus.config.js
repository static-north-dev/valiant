const {themes: prismThemes} = require('prism-react-renderer');

const config = {
  title: 'Valiant',
  tagline: 'Source-generated building blocks for dependable .NET applications.',
  favicon: 'img/favicon.svg',

  url: 'https://valiant.dev',
  baseUrl: '/',

  organizationName: 'ignitea-dev',
  projectName: 'valiant',

  onBrokenLinks: 'throw',
  onBrokenAnchors: 'throw',
  markdown: {
    hooks: {
      onBrokenMarkdownLinks: 'throw',
    },
  },

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      {
        docs: {
          path: 'validation',
          routeBasePath: 'validation',
          sidebarPath: './sidebarsValidation.js',
          editUrl: undefined,
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      },
    ],
  ],

  plugins: [
    [
      '@docusaurus/plugin-content-docs',
      {
        id: 'endpoints',
        path: 'endpoints',
        routeBasePath: 'endpoints',
        sidebarPath: './sidebarsEndpoints.js',
        editUrl: undefined,
      },
    ],
    [
      '@docusaurus/plugin-content-docs',
      {
        id: 'settings',
        path: 'settings',
        routeBasePath: 'settings',
        sidebarPath: './sidebarsSettings.js',
        editUrl: undefined,
      },
    ],
    [
      '@docusaurus/plugin-content-docs',
      {
        id: 'mapping',
        path: 'mapping',
        routeBasePath: 'mapping',
        sidebarPath: './sidebarsMapping.js',
        editUrl: undefined,
      },
    ],
  ],

  themeConfig: {
    navbar: {
      title: 'Valiant',
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'validationSidebar',
          position: 'left',
          label: 'Validation',
        },
        {
          type: 'docSidebar',
          docsPluginId: 'endpoints',
          sidebarId: 'endpointsSidebar',
          position: 'left',
          label: 'Endpoints',
        },
        {
          type: 'docSidebar',
          docsPluginId: 'mapping',
          sidebarId: 'mappingSidebar',
          position: 'left',
          label: 'Mapping',
        },
        {
          type: 'docSidebar',
          docsPluginId: 'settings',
          sidebarId: 'settingsSidebar',
          position: 'left',
          label: 'Settings',
        },
      ],
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
      additionalLanguages: ['csharp'],
    },
  },
};

module.exports = config;
