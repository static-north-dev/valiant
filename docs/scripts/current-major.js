const fs = require('node:fs');
const path = require('node:path');

const defaultConfigPath = path.resolve(__dirname, '..', 'current-major.json');

function readCurrentMajor(configPath = defaultConfigPath) {
  let config;

  try {
    config = JSON.parse(fs.readFileSync(configPath, 'utf8'));
  } catch (error) {
    throw new Error(
      `Could not read documentation major from ${configPath}: ${error.message}`,
    );
  }

  if (
    typeof config.major !== 'string' ||
    !/^(0|[1-9][0-9]*)$/.test(config.major)
  ) {
    throw new Error(
      'docs/current-major.json must contain a SemVer major string without a leading zero.',
    );
  }

  return config.major;
}

if (require.main === module) {
  try {
    process.stdout.write(`${readCurrentMajor()}\n`);
  } catch (error) {
    console.error(error instanceof Error ? error.message : error);
    process.exitCode = 1;
  }
}

module.exports = {readCurrentMajor};
