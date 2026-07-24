const sidebars = {
  settingsSidebar: [
    'index',
    'quick-start',
    'samples',
    {
      type: 'category',
      label: 'Guides',
      items: [
        'guides/schema-generation',
        'guides/binding-validation',
        'guides/aspnetcore-ui',
      ],
    },
    'reference/schema-api',
  ],
};

module.exports = sidebars;
