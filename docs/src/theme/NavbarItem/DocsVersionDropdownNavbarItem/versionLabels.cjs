function formatVersionLabel({name, label, hasCustomLabel = false}) {
  if (hasCustomLabel) {
    return label;
  }

  if (name === 'current') {
    return 'Next';
  }

  if (/^\d+$/.test(label)) {
    return `Version ${label}`;
  }

  return label;
}

module.exports = {formatVersionLabel};
