import {documentationTools, executeDocumentationTool} from './documentation.mjs';

const registrations = new WeakMap();

export function registerDocumentationTools(context, loadIndex) {
  if (!context?.registerTool) {
    return Promise.resolve(false);
  }
  if (registrations.has(context)) {
    return registrations.get(context);
  }
  const controller = new AbortController();
  const registration = (async () => {
    try {
      for (const tool of documentationTools) {
        await context.registerTool({
          ...tool,
          annotations: {readOnlyHint: true},
          execute: async (args) => ({content: [{type: 'text', text: JSON.stringify(executeDocumentationTool(await loadIndex(), tool.name, args))}]}),
        }, {signal: controller.signal});
      }
      return true;
    } catch (error) {
      controller.abort();
      registrations.delete(context);
      throw error;
    }
  })();
  registrations.set(context, registration);
  return registration;
}
