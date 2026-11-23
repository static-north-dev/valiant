import Heading from '@theme/Heading';
import Layout from '@theme/Layout';
import Link from '@docusaurus/Link';
import styles from './index.module.css';

const modules = [
  {
    key: 'validation',
    label: 'Validation',
    href: '/validation/',
    marker: '[ValiantValidator]',
    description: 'Fluent and attribute rules, native control flow, composition, severity, and application pipelines.',
    output: 'IValidator<T>',
  },
  {
    key: 'endpoints',
    label: 'Endpoints',
    href: '/endpoints/',
    marker: '[ValiantEndpoint]',
    description: 'Endpoint-per-class Minimal APIs with groups, conventions, feature switches, and rich query binding.',
    output: 'Route registrar',
  },
  {
    key: 'mapping',
    label: 'Mapping',
    href: '/mapping/',
    marker: '[ValiantMapper]',
    description: 'Direct object mapping for mutable and immutable targets, with explicit rules and mapper composition.',
    output: 'IMapper<TIn,TOut>',
  },
  {
    key: 'settings',
    label: 'Settings',
    href: '/settings/',
    marker: '[ValiantSettings]',
    description: 'Deterministic settings schemas, Options binding, validation, and an ASP.NET Core configuration explorer.',
    output: 'Schema provider',
  },
];

function GeneratedExample() {
  return (
    <figure className={styles.flow}>
      <figcaption className={styles.flowHeader}>Generated mapping example</figcaption>
      <div className={styles.flowBody}>
        <p className={styles.flowLabel}>You declare the mapping pair</p>
        <pre><code>{`[ValiantMapper]
public sealed partial class OrderMapper
    : MapperDefinition<Order, OrderDto>;`}</code></pre>
        <p className={styles.flowLabel}>Valiant generates the Map method</p>
        <pre><code>{`// Constructor mapping in Fast mode
return new OrderDto(source.Id, source.Total);`}</code></pre>
      </div>
    </figure>
  );
}

function HomepageHeader() {
  return (
    <header className={styles.hero}>
      <div className={styles.heroGrid} aria-hidden="true" />
      <div className="container">
        <div className={styles.heroLayout}>
          <div>
            <Heading as="h1">Source-generated<br /><span>.NET libraries</span></Heading>
            <p className={styles.lede}>
              Define validation, Minimal API endpoints, object mapping, and settings in C#.
              Valiant generates typed implementations, registration, and build-time diagnostics.
            </p>
            <div className={styles.actions}>
              <a className={styles.primaryAction} href="#modules">
                Explore the generators <span aria-hidden="true">→</span>
              </a>
              <Link className={styles.secondaryAction} to="/validation/reference/packages">
                Packages and compatibility
              </Link>
            </div>
          </div>
          <GeneratedExample />
        </div>
      </div>
    </header>
  );
}

export default function Home() {
  return (
    <Layout
      title="Source-generated .NET building blocks"
      description="Documentation for Valiant Validation, Endpoints, Mapping, and Settings.">
      <HomepageHeader />
      <main>
        <section className={styles.modules} id="modules">
          <div className="container">
            <div className={styles.sectionHeading}>
              <p className={styles.eyebrow}>Four focused modules</p>
              <Heading as="h2">Choose a module</Heading>
              <p>Use any module on its own, or combine them in one application.</p>
            </div>
            <div className={styles.moduleGrid}>
              {modules.map((module) => (
                <Link
                  className={`${styles.moduleCard} ${styles[module.key]}`}
                  to={module.href}
                  key={module.key}>
                  <span className={styles.marker}>{module.marker}</span>
                  <Heading as="h3">{module.label}</Heading>
                  <p>{module.description}</p>
                  <span className={styles.output}>
                    emits <strong>{module.output}</strong>
                    <span aria-hidden="true">↗</span>
                  </span>
                </Link>
              ))}
            </div>
          </div>
        </section>

        <section className={styles.principles}>
          <div className="container">
            <Heading as="h2">See the modules together</Heading>
            <p>Build a Minimal API that binds configuration, validates a request, and maps a response.</p>
            <Link to="/endpoints/guides/validated-api">Build a validated API</Link>
          </div>
        </section>
      </main>
    </Layout>
  );
}
