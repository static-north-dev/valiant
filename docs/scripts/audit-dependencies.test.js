const assert = require('node:assert/strict');
const test = require('node:test');
const {classifyHighAdvisories} = require('./audit-dependencies');

test('rejects the formerly exempt image-size advisories through the Docusaurus dependency chain', () => {
  const result = classifyHighAdvisories({
    vulnerabilities: {
      '@docusaurus/mdx-loader': {
        severity: 'high',
        via: ['image-size'],
      },
      'image-size': {
        severity: 'high',
        via: [
          {
            severity: 'high',
            url: 'https://github.com/advisories/GHSA-w3rx-r6r6-pgpr',
          },
          {
            severity: 'high',
            url: 'https://github.com/advisories/GHSA-5p2g-fcmc-qvqq',
          },
        ],
      },
    },
  });

  assert.deepEqual(result, {
    unapprovedAdvisories: ['GHSA-W3RX-R6R6-PGPR', 'GHSA-5P2G-FCMC-QVQQ'],
    unresolvedPackages: [],
  });
});

test('rejects new high-severity advisories and untraceable findings', () => {
  const result = classifyHighAdvisories({
    vulnerabilities: {
      known: {
        severity: 'high',
        via: [{
          severity: 'high',
          url: 'https://github.com/advisories/GHSA-w3rx-r6r6-pgpr',
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
    unapprovedAdvisories: ['GHSA-W3RX-R6R6-PGPR', 'GHSA-AAAA-BBBB-CCCC'],
    unresolvedPackages: ['untraceable'],
  });
});
