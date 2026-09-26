const {spawnSync} = require('node:child_process');

function advisoryId(advisory) {
  return advisory.url?.match(/GHSA-[a-z0-9-]+/i)?.[0]?.toUpperCase();
}

function collectAdvisories(vulnerabilities, name, visited = new Set()) {
  if (visited.has(name)) {
    return [];
  }

  visited.add(name);
  const vulnerability = vulnerabilities[name];
  if (vulnerability === undefined) {
    return [];
  }

  return vulnerability.via.flatMap((via) => {
    if (typeof via === 'string') {
      return collectAdvisories(vulnerabilities, via, visited);
    }

    if (!['high', 'critical'].includes(via.severity)) {
      return [];
    }

    const id = advisoryId(via);
    return id === undefined ? [] : [id];
  });
}

function classifyHighAdvisories(report) {
  const vulnerabilities = report.vulnerabilities ?? {};
  const highOrCritical = Object.entries(vulnerabilities)
    .filter(([, vulnerability]) => ['high', 'critical'].includes(vulnerability.severity));
  const advisories = new Set();

  for (const [name] of highOrCritical) {
    for (const id of collectAdvisories(vulnerabilities, name)) {
      advisories.add(id);
    }
  }

  return {
    unresolvedPackages: highOrCritical
      .filter(([name]) => collectAdvisories(vulnerabilities, name).length === 0)
      .map(([name]) => name),
    unapprovedAdvisories: [...advisories],
  };
}

function main() {
  const npm = process.platform === 'win32' ? 'npm.cmd' : 'npm';
  const result = spawnSync(npm, ['audit', '--omit=dev', '--json'], {
    encoding: 'utf8',
  });

  if (result.error !== undefined) {
    throw result.error;
  }

  if (![0, 1].includes(result.status)) {
    process.stderr.write(result.stderr);
    throw new Error(`npm audit exited with ${result.status ?? 'an unknown status'}.`);
  }

  const report = JSON.parse(result.stdout);
  const {unapprovedAdvisories, unresolvedPackages} = classifyHighAdvisories(report);
  if (unapprovedAdvisories.length > 0 || unresolvedPackages.length > 0) {
    process.stderr.write(result.stdout);
    throw new Error(
      `Unapproved high-severity audit findings: ${[
        ...unapprovedAdvisories,
        ...unresolvedPackages,
      ].join(', ')}.`,
    );
  }

  if (result.status === 1) {
    console.warn('Dependency audit passed the high/critical gate; lower-severity findings remain.');
  }
}

if (require.main === module) {
  main();
}

module.exports = {classifyHighAdvisories};
