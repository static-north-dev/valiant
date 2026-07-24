const sidebars = {
  endpointsSidebar: [
    'index',
    'quick-start',
    'samples',
    {
      type: 'category',
      label: 'Guides',
      items: [
        'guides/groups-conventions',
        'guides/conditional-registration',
        'guides/request-binding',
      ],
    },
    {
      type: 'category',
      label: 'Reference',
      items: ['reference/query-parameters', 'reference/registration-generation'],
    },
  ],
};

module.exports = sidebars;
