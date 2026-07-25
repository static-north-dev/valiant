const assert = require('node:assert/strict');
const test = require('node:test');
const {classifyHighAdvisories} = require('./audit-dependencies');

test('allows only the documented no-fix Docusaurus advisory chain', () => {
  const result = classifyHighAdvisories({
    vulnerabilities: {
      '@docusaurus/core': {
        severity: 'high',
        via: ['serve-handler'],
      },
      'serve-handler': {
        severity: 'high',
        via: ['minimatch'],
      },
      minimatch: {
        severity: 'high',
        via: [{
          severity: 'high',
          url: 'https://github.com/advisories/GHSA-mh99-v99m-4gvg',
        }],
      },
    },
  });

  assert.deepEqual(result, {unapprovedAdvisories: [], unresolvedPackages: []});
});

test('rejects new high-severity advisories and untraceable findings', () => {
  const result = classifyHighAdvisories({
    vulnerabilities: {
      known: {
        severity: 'high',
        via: [{
          severity: 'high',
          url: 'https://github.com/advisories/GHSA-mh99-v99m-4gvg',
        }],
      },
      unknown: {
        severity: 'critical',
        via: [{
          severity: 'critical',
          url: 'https://github.com/advisories/GHSA-aaaa-bbbb-cccc',
        }],
      },
      untraceable: {
        severity: 'high',
        via: [],
      },
    },
  });

  assert.deepEqual(result, {
    unapprovedAdvisories: ['GHSA-AAAA-BBBB-CCCC'],
    unresolvedPackages: ['untraceable'],
  });
});
