import {useEffect, useState} from 'react';
import useBaseUrl from '@docusaurus/useBaseUrl';
import Heading from '@theme/Heading';
import Layout from '@theme/Layout';
import Link from '@docusaurus/Link';
import {moduleNames, searchDocuments} from '../agents/documentation.mjs';
import styles from './search.module.css';

export default function DocumentationSearch() {
  const indexUrl = useBaseUrl('/agents/documents.json');
  const [index, setIndex] = useState(null);
  const [loadFailed, setLoadFailed] = useState(false);
  const [query, setQuery] = useState('');
  const [module, setModule] = useState('');
  const [version, setVersion] = useState('');
  const [outcome, setOutcome] = useState(null);

  useEffect(() => {
    const controller = new AbortController();
    fetch(indexUrl, {signal: controller.signal})
      .then((response) => {
        if (!response.ok) {
          throw new Error('Unable to load documentation.');
        }
        return response.json();
      })
      .then(setIndex)
      .catch(() => {
        if (!controller.signal.aborted) {
          setLoadFailed(true);
        }
      });
    return () => controller.abort();
  }, [indexUrl]);

  function submit(event) {
    event.preventDefault();
    try {
      const {results, total} = searchDocuments(index, {
        query: query.trim(),
        module: module || undefined,
        version: version || undefined,
        limit: 20,
      });
      const summary = total
        ? `Showing ${results.length} of ${total} results.`
        : 'No results. Try a different term or change the filters.';
      setOutcome({summary, results});
    } catch (error) {
      setOutcome({summary: error.message, results: []});
    }
  }

  return (
    <Layout title="Search documentation" description="Find Valiant guides, examples, and API references.">
      <main className={`container margin-vert--xl ${styles.page}`}>
        <Heading as="h1">Search documentation</Heading>
        <form role="search" className={styles.form} onSubmit={submit}>
          <label>
            Search terms
            <input type="search" value={query} onChange={(event) => setQuery(event.target.value)} maxLength={200} required />
          </label>
          <label>
            Module
            <select value={module} onChange={(event) => setModule(event.target.value)}>
              <option value="">All modules</option>
              {moduleNames.map((name) => (
                <option key={name} value={name}>{name[0].toUpperCase() + name.slice(1)}</option>
              ))}
            </select>
          </label>
          <label>
            Version
            <select value={version} onChange={(event) => setVersion(event.target.value)}>
              <option value="">Latest published</option>
              {index?.versions.map((item) => (
                <option key={item.name} value={item.name}>
                  {item.name === 'current' ? 'Next' : `Version ${item.name}`}
                </option>
              ))}
            </select>
          </label>
          <button type="submit" className="button button--primary" disabled={!index}>Search</button>
        </form>
        <p role="status">
          {loadFailed && 'Search is unavailable. Reload the page to try again.'}
          {!loadFailed && !index && 'Loading documentation index…'}
          {outcome?.summary}
        </p>
        {outcome?.results.length > 0 && (
          <ol className={styles.results}>
            {outcome.results.map((result) => (
              <li key={result.path}>
                <Heading as="h2"><Link to={result.path}>{result.title}</Link></Heading>
                <p className={styles.module}>{result.module}</p>
                <p>{result.description}</p>
              </li>
            ))}
          </ol>
        )}
      </main>
    </Layout>
  );
}
