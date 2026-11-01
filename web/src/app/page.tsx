export default function Home() {
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand"><span className="brand-mark">S</span><span>syncify</span></div>
        <nav className="nav" aria-label="Primary navigation">
          <a className="nav-link active" href="#overview"><span>◈</span> Overview</a>
          <a className="nav-link" href="#playlists"><span>♫</span> Playlists</a>
          <a className="nav-link" href="#activity"><span>↻</span> Activity</a>
        </nav>
        <div className="sidebar-footer"><span className="status-dot" /> API ready<br /><small>Local workspace</small></div>
      </aside>

      <main className="main-content" id="overview">
        <header className="topbar">
          <div><p className="eyebrow">Workspace / Overview</p><h1>Your music, in sync.</h1></div>
          <button className="avatar" aria-label="Open account menu">M</button>
        </header>

        <section className="hero-panel">
          <div><p className="eyebrow accent">First connection</p><h2>Bring your playlists together.</h2><p className="hero-copy">Connect your streaming accounts to start moving music between platforms.</p><button className="primary-button">Connect an account <span>→</span></button></div>
          <div className="orbit-art" aria-hidden="true"><div className="orbit orbit-one" /><div className="orbit orbit-two" /><div className="record"><span>♫</span></div><div className="platform spotify">SPOTIFY</div><div className="platform youtube">YOUTUBE</div></div>
        </section>

        <section className="section-block" id="playlists">
          <div className="section-heading"><div><p className="eyebrow">At a glance</p><h2>Workspace pulse</h2></div><span className="quiet-label">No activity yet</span></div>
          <div className="metrics"><article><span className="metric-label">Linked playlists</span><strong>0</strong><span className="metric-note">Connect a source to begin</span></article><article><span className="metric-label">Tracks synchronized</span><strong>0</strong><span className="metric-note">Your library is waiting</span></article><article><span className="metric-label">Needs review</span><strong>0</strong><span className="metric-note">Nothing needs attention</span></article></div>
        </section>

        <section className="empty-state" id="activity"><div className="empty-icon">✦</div><div><p className="eyebrow">Next step</p><h2>Set up your first playlist link</h2><p>Once accounts are connected, your sync history and unmatched tracks will appear here.</p></div><button className="secondary-button">View setup guide <span>→</span></button></section>
      </main>
    </div>
  );
}
