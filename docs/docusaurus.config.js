const {themes: prismThemes} = require('prism-react-renderer');

const includeCurrentVersion = process.env.DOCS_INCLUDE_CURRENT !== 'false';

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
          path: 'content',
          routeBasePath: '/',
          sidebarPath: './sidebars.js',
          editUrl: undefined,
          includeCurrentVersion,
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
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
          sidebarId: 'endpointsSidebar',
          position: 'left',
          label: 'Endpoints',
        },
        {
          type: 'docSidebar',
          sidebarId: 'mappingSidebar',
          position: 'left',
          label: 'Mapping',
        },
        {
          type: 'docSidebar',
          sidebarId: 'settingsSidebar',
          position: 'left',
          label: 'Settings',
        },
        {
          type: 'docsVersionDropdown',
          position: 'left',
          className: 'navbar-version-selector',
        },
      ],
    },
    footer: {
      style: 'light',
      copyright:
        'Valiant is an open-source project from <a class="static-north-footer-link" href="https://staticnorth.dev/">Static North</a>.',
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
      additionalLanguages: ['csharp'],
    },
  },
};

module.exports = config;
