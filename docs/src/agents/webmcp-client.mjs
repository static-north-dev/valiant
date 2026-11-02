import ExecutionEnvironment from '@docusaurus/ExecutionEnvironment';
import {registerDocumentationTools} from './webmcp.mjs';

if (ExecutionEnvironment.canUseDOM) {
  let indexPromise;
  const loadIndex = () => {
    indexPromise ??= fetch('/agents/documents.json').then((response) => {
      if (!response.ok) {
        throw new Error('Documentation index is unavailable.');
      }
      return response.json();
    }).catch((error) => {
      indexPromise = undefined;
      throw error;
    });
    return indexPromise;
  };
  registerDocumentationTools(document.modelContext ?? navigator.modelContext, loadIndex)
    .catch((error) => console.warn('Valiant documentation tools could not register.', error));
}
