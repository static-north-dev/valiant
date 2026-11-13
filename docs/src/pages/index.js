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

function SourceFlow() {
  return (
    <div className={styles.flow} aria-label="Valiant source generation flow">
      <div className={styles.flowHeader}>
        <span>compile.pipeline</span>
        <span className={styles.flowStatus}>4 generators ready</span>
      </div>
      <div className={styles.flowBody}>
        <div className={styles.declarations}>
          <span className={styles.flowLabel}>explicit C# declarations</span>
          <div className={styles.declarationGrid}>
            {modules.map((module) => (
              <Link
                className={`${styles.declaration} ${styles[module.key]}`}
                to={module.href}
                key={module.key}>
                <span className={styles.declarationModule}>{module.label}</span>
                <code>{module.marker}</code>
              </Link>
            ))}
          </div>
        </div>
        <div className={styles.compileStep}>
          <span>incremental source generation</span>
        </div>
        <div className={styles.generated}>
          <span className={styles.generatedLabel}>outputs across the Valiant family</span>
          <div><i /> Generated C#</div>
          <div><i /> Typed contracts</div>
          <div><i /> Registration where applicable</div>
          <div><i /> Build-time diagnostics</div>
        </div>
      </div>
    </div>
  );
}

function HomepageHeader() {
  return (
    <header className={styles.hero}>
      <div className={styles.heroGrid} aria-hidden="true" />
      <div className="container">
        <div className={styles.heroLayout}>
          <div className={styles.heroCopy}>
            <p className={styles.eyebrow}>Source-generated .NET building blocks</p>
            <Heading as="h1">Compile the repetitive.<br /><span>Keep the intent.</span></Heading>
            <p className={styles.lede}>
              Valiant turns explicit C# declarations into fast validation, endpoints,
              mapping, and settings infrastructure—with no runtime assembly scanning.
            </p>
            <div className={styles.actions}>
              <a className={styles.primaryAction} href="#modules">
                Explore the generators <span aria-hidden="true">→</span>
              </a>
              <a className={styles.secondaryAction} href="#approach">
                Why source generation
              </a>
            </div>
          </div>
          <SourceFlow />
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
              <Heading as="h2">Choose the contract your code needs.</Heading>
              <p>Each package stands on its own. Together, they share one design: explicit discovery, generated hot paths, and ordinary C# APIs.</p>
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

        <section className={styles.principles} id="approach">
          <div className="container">
            <div className={styles.principleGrid}>
              <div><span>01</span><strong>Scoped discovery</strong><p>Markers define exactly what generators inspect.</p></div>
              <div><span>02</span><strong>Direct output</strong><p>Generated C# keeps production paths predictable.</p></div>
              <div><span>03</span><strong>Ordinary C# authoring</strong><p>Declarations stay explicit, typed, and easy to navigate.</p></div>
              <div><span>04</span><strong>Build-time feedback</strong><p>Diagnostics catch unsupported shapes before runtime.</p></div>
            </div>
          </div>
        </section>
      </main>
    </Layout>
  );
}
