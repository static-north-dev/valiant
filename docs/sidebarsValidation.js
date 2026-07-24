const sidebars = {
  validationSidebar: [
    'index',
    'quick-start',
    'samples',
    {
      type: 'category',
      label: 'Guides',
      items: [
        'guides/authoring-rules',
        'guides/composition-control-flow',
        'guides/aspnetcore-openapi',
        'guides/mediator-pipelines',
      ],
    },
    {
      type: 'category',
      label: 'Reference',
      items: [
        'reference/validation-checks',
        'reference/runtime-generation',
        'reference/packages',
      ],
    },
  ],
};

module.exports = sidebars;
