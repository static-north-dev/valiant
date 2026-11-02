const path = require('node:path');
const {pathToFileURL} = require('node:url');

module.exports = function agentDocs(context) {
  let versions;
  return {
    name: 'valiant-agent-docs',
    allContentLoaded({allContent}) {
      versions = allContent['docusaurus-plugin-content-docs'].default.loadedVersions;
    },
    getClientModules() {
      return [path.resolve(context.siteDir, 'src/agents/webmcp-client.mjs')];
    },
    injectHtmlTags() {
      return {headTags: [
        {tagName: 'link', attributes: {rel: 'describedby', href: '/llms.txt', type: 'text/plain'}},
        {tagName: 'link', attributes: {rel: 'ard', href: '/.well-known/ard.json', type: 'application/json'}},
        {tagName: 'link', attributes: {rel: 'ai-catalog', href: '/.well-known/ai-catalog.json', type: 'application/json'}},
      ]};
    },
    async postBuild({outDir}) {
      const {buildAgentArtifacts} = await import(pathToFileURL(path.resolve(context.siteDir, 'scripts/build-agent-artifacts.mjs')));
      await buildAgentArtifacts({siteDir: context.siteDir, outDir, versions, siteUrl: context.siteConfig.url});
    },
  };
};
