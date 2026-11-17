/* neradio concept demo: made-up data, one state object, and a render per screen.
   A classic script, not a module, so the page also opens straight from disk. No audio plays. */
(function () {
  'use strict';

  // ---------- Example data: fictional stations, so no real brand appears ----------

  function svgImage(size, body) {
    var svg = '<svg xmlns="http://www.w3.org/2000/svg" width="' + size + '" height="' + size + '" viewBox="0 0 100 100">' + body + '</svg>';
    return 'data:image/svg+xml,' + encodeURIComponent(svg);
  }

  var LOGOS = {
    harbor: svgImage(256, '<rect width="100" height="100" fill="#13263d"/><path d="M50 78V40" stroke="#f6b54a" stroke-width="7"/><path d="M22 34a40 40 0 0 1 56 0M32 44a26 26 0 0 1 36 0" stroke="#f6b54a" stroke-width="5" fill="none" stroke-linecap="round"/><rect x="18" y="78" width="64" height="6" fill="#f6b54a"/>'),
    nightowl: svgImage(256, '<rect width="100" height="100" fill="#123b3a"/><circle cx="36" cy="46" r="15" fill="#f2efe4"/><circle cx="64" cy="46" r="15" fill="#f2efe4"/><circle cx="38" cy="48" r="6" fill="#123b3a"/><circle cx="62" cy="48" r="6" fill="#123b3a"/><path d="M44 66l6 8 6-8z" fill="#e9a83b"/>'),
    tiny: svgImage(16, '<rect width="100" height="100" fill="#2a6f97"/><path d="M10 60q20-30 40 0t40 0" stroke="#fff" stroke-width="10" fill="none"/>'),
    solCatalog: svgImage(256, '<rect width="100" height="100" fill="#0f6e73"/><path d="M8 58q14-14 28 0t28 0 28 0" stroke="#fff" stroke-width="7" fill="none"/><path d="M8 74q14-14 28 0t28 0 28 0" stroke="#7fd1c8" stroke-width="7" fill="none"/>'),
    solMine: svgImage(256, '<rect width="100" height="100" fill="#f08a24"/><circle cx="50" cy="50" r="22" fill="#ffe08a"/><g stroke="#ffe08a" stroke-width="5" stroke-linecap="round"><path d="M50 12v10M50 78v10M12 50h10M78 50h10M23 23l7 7M70 70l7 7M77 23l-7 7M30 70l-7 7"/></g>'),
    fjord: svgImage(256, '<rect width="100" height="100" fill="#1f4e79"/><path d="M6 80L34 36l16 22 12-16 32 38z" fill="#e8f0f7"/><path d="M6 80h88" stroke="#9cc3e6" stroke-width="4"/>'),
    valley: svgImage(256, '<rect width="100" height="100" fill="#2f5d3a"/><path d="M50 18L24 60h16L28 78h44L60 60h16z" fill="#cfe8b8"/><rect x="46" y="78" width="8" height="10" fill="#cfe8b8"/>'),
    orbit: svgImage(256, '<rect width="100" height="100" fill="#26213b"/><circle cx="50" cy="50" r="16" fill="#f4c7b8"/><ellipse cx="50" cy="50" rx="36" ry="12" fill="none" stroke="#f4c7b8" stroke-width="4" transform="rotate(-20 50 50)"/>'),
    oldies: svgImage(256, '<rect width="100" height="100" fill="#7a2531"/><circle cx="50" cy="50" r="32" fill="#1a1a1a"/><circle cx="50" cy="50" r="24" fill="none" stroke="#333" stroke-width="2"/><circle cx="50" cy="50" r="10" fill="#f3d28b"/><circle cx="50" cy="50" r="2" fill="#1a1a1a"/>'),
    lounge: svgImage(256, '<rect width="100" height="100" fill="#3d3a33"/><g stroke="#e4d6b0" stroke-width="6" stroke-linecap="round"><path d="M20 44v12M32 34v32M44 26v48M56 38v24M68 30v40M80 44v12"/></g>'),
    talk: svgImage(256, '<rect width="100" height="100" fill="#5b2a6e"/><path d="M20 26h60v36H46L32 76V62H20z" fill="#f3e8f7"/>'),
    kaktus: 'https://logos.example.invalid/kaktus-radio.png'
  };

  // A bright "photo" for testing text over the user's own background.
  var BRIGHT_PHOTO = svgImage(1200, '<defs><linearGradient id="s" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#8fd3ff"/><stop offset=".7" stop-color="#e9f7ff"/><stop offset="1" stop-color="#fff9e6"/></linearGradient></defs><rect width="100" height="100" fill="url(#s)"/><circle cx="78" cy="20" r="9" fill="#fffbe0"/><g fill="#ffffff"><ellipse cx="25" cy="30" rx="14" ry="5"/><ellipse cx="35" cy="27" rx="10" ry="6"/><ellipse cx="62" cy="42" rx="16" ry="5"/></g><path d="M0 86q25-8 50 0t50 0V100H0z" fill="#f6e7b8"/>');

  var STATIONS = [
    { id: 'harbor', name: 'Harbor Lights FM', logo: LOGOS.harbor, genres: ['Electronic', 'Downtempo'], country: 'Latvia', language: 'English', format: 'MP3', bitrate: 128, rate: '44.1 kHz', streams: 2,
      titles: ['Mira Kalns - Lantern Street', 'Sunfold - Paper Boats', 'Low Tide Choir - Salt', 'Veras - Night Ferry', 'Ilze Ozola - Amber Coast'] },
    { id: 'nightowl', name: 'Night Owl Jazz', logo: LOGOS.nightowl, genres: ['Jazz'], country: 'France', language: 'French', format: 'HE-AAC', bitrate: 64, rate: '44.1 kHz', streams: 1,
      titles: ['Lucie Marchand Trio - Quatre Heures', 'Paul Ferrand - Rue des Martyrs', 'Odile Sax Quartet - Brume', 'Les Trois Lunes - Minuit Moins Cinq'] },
    { id: 'news', name: 'Baltic News 24', logo: null, genres: ['News'], country: 'Lithuania', language: 'Lithuanian', format: 'MP3', bitrate: 96, rate: '44.1 kHz', streams: 1,
      titles: ['Morning headlines', 'Business at nine', 'Weather and traffic'] },
    { id: 'kaktus', name: 'Kaktus Radio', logo: LOGOS.kaktus, genres: ['Rock'], country: 'Germany', language: 'German', format: 'Ogg Vorbis', bitrate: 192, rate: '48 kHz', streams: 2,
      titles: ['The Gravel Kings - Long Way North', 'Ola Brenn - Highway Hum', 'Stahlwerk - Funken', 'Die Weiten - Kilometer'] },
    { id: 'tiny', name: 'Tiny Waves', logo: LOGOS.tiny, genres: ['Ambient'], country: 'Netherlands', language: null, format: 'Opus', bitrate: 48, rate: '48 kHz', streams: 1,
      titles: ['Drift Lab - Polder Fog', 'Anke Vos - Slow Tide', 'Mistral Kids - 4 AM'] },
    { id: 'sol', name: 'Radio Sol', logo: LOGOS.solCatalog, genres: ['Latin', 'Pop'], country: 'Spain', language: 'Spanish', format: 'AAC', bitrate: 128, rate: '44.1 kHz', streams: 1,
      titles: ['Carla Mena - Verano Lento', 'Los Faros - Sal y Limón', 'Inés Rey - Calle Mayor'] },
    { id: 'fjord', name: 'Fjord Classical', logo: LOGOS.fjord, genres: ['Classical'], country: 'Norway', language: 'Norwegian', format: 'AAC', bitrate: 256, rate: '48 kHz', streams: 2,
      titles: ['Grieg - Morning Mood', 'Bergen Philharmonic - Holberg Suite: Prelude', 'Ingrid Sand - Nocturne in E flat'] },
    { id: 'valley', name: 'Northern Valley and Lakeside Community Radio for Everyone Who Lives Here', logo: LOGOS.valley, genres: ['Community', 'Folk'], country: 'Finland', language: 'Finnish', format: 'MP3', bitrate: 64, rate: '44.1 kHz', streams: 1,
      titles: ['Village choir - Kesäyö', 'Local news in brief', 'Aino Laine - Järven Ranta'] },
    // An HLS station counts as one without titles (#7).
    { id: 'orbit', name: 'Orbit Lo-Fi', logo: LOGOS.orbit, genres: ['Lo-fi'], country: 'Japan', language: null, format: 'HLS AAC', bitrate: 96, rate: '48 kHz', streams: 1, noTitles: true, titles: [] },
    { id: 'oldies', name: 'Old Town Oldies', logo: LOGOS.oldies, genres: ['Oldies'], country: 'Poland', language: 'Polish', format: 'MP3', bitrate: 128, rate: '44.1 kHz', streams: 1, inCatalog: false,
      titles: ['Czerwone Maki - Tango 1962', 'Anna Bielska - Stare Miasto'] },
    { id: 'lounge', name: 'Lossless Lounge', logo: LOGOS.lounge, genres: ['Lounge'], country: 'Italy', language: 'Italian', format: 'FLAC', bitrate: 1411, rate: '44.1 kHz', streams: 1, unsupported: true, titles: [] },
    { id: 'talk', name: 'Open Mic Talk', logo: LOGOS.talk, genres: ['Talk'], country: 'Ireland', language: 'English', format: 'MP3', bitrate: 64, rate: '44.1 kHz', streams: 1, noTitles: true, titles: [] },
    { id: 'bare', name: 'Kitchen radio', logo: null, genres: [], country: null, language: null, format: null, bitrate: null, streams: 1, fromUserList: true, inCatalog: false, titles: ['Untitled broadcast'] }
  ];

  var TRACKS = [
    { id: 't1', title: 'Mira Kalns - Lantern Street', station: 'harbor', seconds: 252, date: '14 Nov' },
    { id: 't2', title: 'The Gravel Kings - Long Way North', station: 'kaktus', seconds: 231, date: '12 Nov' },
    { id: 't3', title: 'Ola Brenn - Highway Hum', station: 'kaktus', seconds: 198, date: '12 Nov' },
    { id: 't4', title: 'Lucie Marchand Trio - Quatre Heures', station: 'nightowl', seconds: 344, date: '10 Nov' },
    { id: 't5', title: 'Sunfold - Paper Boats', station: 'harbor', seconds: 287, date: '9 Nov' }
  ];

  var GROUPS = [
    { id: 'favorites', name: 'Favorites', builtIn: true, items: [s('harbor'), s('nightowl'), s('oldies'), t('t1')] },
    { id: 'focus', name: 'Focus', items: [s('harbor'), s('fjord'), s('orbit')] },
    { id: 'road', name: 'Road trip', items: [s('harbor'), s('kaktus'), t('t2'), t('t3')] },
    { id: 'morning', name: 'Morning', items: [s('sol'), s('fjord')] }
  ];

  var PLAYLISTS = [
    { id: 'evening', name: 'Evening', minutes: 20, loop: true, items: [s('nightowl'), t('t4'), s('talk'), s('fjord'), t('t1')] },
    { id: 'drive', name: 'Drive', minutes: 30, loop: false, items: [s('kaktus'), t('t2'), t('t3'), s('harbor')] }
  ];

  function s(id) { return { type: 'station', id: id }; }
  function t(id) { return { type: 'track', id: id }; }

  var BACKGROUNDS = [
    { id: 'plain', name: 'Plain' },
    { id: 'dial', name: 'Tuning dial' },
    { id: 'dusk', name: 'Dusk' },
    { id: 'studio', name: 'Studio' },
    { id: 'daylight', name: 'Daylight' }
  ];

  var EQ_BANDS = ['31', '62', '125', '250', '500', '1k', '2k', '4k', '8k', '16k'];
  var EQ_PRESETS = {
    Flat: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
    Bass: [6, 5, 4, 2, 0, 0, 0, 0, 0, 0],
    Voice: [-2, -1, 0, 2, 4, 4, 3, 1, 0, -1],
    Treble: [0, 0, 0, 0, 0, 1, 2, 4, 5, 6]
  };

  var LOGO_MIN_PX = 48;
  var TITLE_EVERY_MS = 12000;

  // ---------- State ----------

  var state = {
    route: { name: 'browse', id: null },
    layout: 'desktop',
    forcePhone: false,
    overrides: { nightowl: { name: 'Late Jazz' }, sol: { logo: LOGOS.solMine } },
    player: { kind: 'station', id: 'harbor', status: 'playing', title: STATIONS[0].titles[0], titleIndex: 0, since: Date.now() },
    history: {
      harbor: [{ time: '21:31', title: 'Veras - Night Ferry' }, { time: '21:27', title: 'Low Tide Choir - Salt' }],
      nightowl: [{ time: '20:58', title: 'Paul Ferrand - Rue des Martyrs' }, { time: '20:52', title: 'Odile Sax Quartet - Brume' }]
    },
    problem: null,
    sleepEndsAt: null,
    recording: null,
    sheet: null,
    guideOpen: false,
    query: '',
    genre: null,
    background: 'dial',
    userImage: null,
    eq: EQ_PRESETS.Flat.slice(),
    eqPreset: 'Flat',
    vizOn: true,
    toast: null,
    navigations: 0
  };
  state.history.harbor.unshift({ time: clock(), title: state.player.title });

  var badLogos = new Set();
  var timers = { connect: 0, reconnect: 0, toast: 0 };
  var app = document.getElementById('app');

  // One file input that outlives every render: the page re-renders while the picker is open,
  // which would otherwise leave the user's choice on an input that's gone.
  var picker = document.createElement('input');
  picker.type = 'file';
  picker.accept = 'image/*';
  picker.hidden = true;
  picker.id = 'image-picker';
  document.body.appendChild(picker);
  var pickFor = null;

  // ---------- Helpers ----------

  function esc(text) {
    return String(text).replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  }
  function station(id) { return STATIONS.find(function (x) { return x.id === id; }); }
  function track(id) { return TRACKS.find(function (x) { return x.id === id; }); }
  function group(id) { return GROUPS.find(function (x) { return x.id === id; }); }
  function playlist(id) { return PLAYLISTS.find(function (x) { return x.id === id; }); }
  function nameOf(st) { var o = state.overrides[st.id]; return o && o.name ? o.name : st.name; }
  function isRenamed(st) { var o = state.overrides[st.id]; return !!(o && o.name); }
  function hasOwnLogo(st) { var o = state.overrides[st.id]; return !!(o && o.logo !== undefined); }
  function logoOf(st) { var o = state.overrides[st.id]; return o && o.logo !== undefined ? o.logo : st.logo; }
  function groupsWith(item) {
    return GROUPS.filter(function (g) { return g.items.some(function (i) { return i.type === item.type && i.id === item.id; }); });
  }
  function inGroup(g, item) { return g.items.some(function (i) { return i.type === item.type && i.id === item.id; }); }
  function clock() { var d = new Date(); return pad(d.getHours()) + ':' + pad(d.getMinutes()); }
  function pad(n) { return (n < 10 ? '0' : '') + n; }
  function duration(seconds) { return Math.floor(seconds / 60) + ':' + pad(seconds % 60); }
  function countdown(ms) { var total = Math.max(0, Math.ceil(ms / 1000)); return pad(Math.floor(total / 60)) + ':' + pad(total % 60); }
  function hue(id) { var h = 0; for (var i = 0; i < id.length; i++) h = (h * 31 + id.charCodeAt(i)) % 360; return h; }
  function initials(name) {
    var skip = /^(fm|am|radio|the|of|and|for)$/i;
    var words = name.split(/\s+/).filter(function (w) { return w && !skip.test(w); });
    if (words.length === 0) words = name.split(/\s+/);
    return words.slice(0, 2).map(function (w) { return w.charAt(0).toUpperCase(); }).join('');
  }
  function stationMeta(st) {
    var parts = [st.genres[0], st.country, st.format ? st.format + (st.bitrate ? ' ' + st.bitrate + ' kbps' : '') : null].filter(Boolean);
    return parts.length ? parts.join(' · ') : 'Only a name and a stream address';
  }
  function itemCounts(items) {
    var stations = items.filter(function (i) { return i.type === 'station'; }).length;
    var tracks = items.length - stations;
    var parts = [];
    if (stations) parts.push(stations + (stations === 1 ? ' station' : ' stations'));
    if (tracks) parts.push(tracks + (tracks === 1 ? ' recording' : ' recordings'));
    return parts.length ? parts.join(' · ') : 'Empty';
  }

  var ICONS = {
    play: '<path class="f" d="M8 5.5v13l11-6.5z"/>',
    stop: '<rect class="f" x="6.5" y="6.5" width="11" height="11" rx="1.5"/>',
    star: '<path d="M12 3.5l2.6 5.4 5.9.8-4.3 4.1 1 5.9L12 17l-5.2 2.7 1-5.9-4.3-4.1 5.9-.8z"/>',
    search: '<circle cx="11" cy="11" r="6.5"/><path d="M16 16l4.5 4.5"/>',
    plus: '<path d="M12 5v14M5 12h14"/>',
    x: '<path d="M6.5 6.5l11 11M17.5 6.5l-11 11"/>',
    back: '<path d="M15 5l-7 7 7 7"/>',
    next: '<path d="M9 5l7 7-7 7"/>',
    browse: '<circle cx="12" cy="12" r="8.5"/><path d="M15.5 8.5l-2 5-5 2 2-5z"/>',
    now: '<circle cx="12" cy="12" r="2"/><path d="M8.5 8.5a5 5 0 0 0 0 7M15.5 8.5a5 5 0 0 1 0 7M5.6 5.6a9 9 0 0 0 0 12.8M18.4 5.6a9 9 0 0 1 0 12.8"/>',
    groups: '<rect x="4" y="4" width="7" height="7" rx="1.5"/><rect x="13" y="4" width="7" height="7" rx="1.5"/><rect x="4" y="13" width="7" height="7" rx="1.5"/><rect x="13" y="13" width="7" height="7" rx="1.5"/>',
    playlist: '<path d="M4 6h12M4 11h12M4 16h7"/><path class="f" d="M15 14v6l5-3z"/>',
    recordings: '<path d="M4 12h1.5M7.5 8v8M11 5v14M14.5 9v6M18 7v10M21 11v2"/>',
    prefs: '<path d="M4 7h9M17 7h3M4 17h3M11 17h9"/><circle cx="15" cy="7" r="2"/><circle cx="9" cy="17" r="2"/>',
    record: '<circle cx="12" cy="12" r="6.5"/>',
    sleep: '<path d="M19.5 14.5A8 8 0 1 1 9.5 4.5a6.5 6.5 0 0 0 10 10z"/>',
    edit: '<path d="M4.5 19.5h4l10-10-4-4-10 10z"/><path d="M13 7l4 4"/>',
    reset: '<path d="M4.5 5v5h5"/><path d="M5.2 15a7 7 0 1 0 1.6-7.4L4.5 10"/>',
    up: '<path d="M12 19V5M6.5 10.5L12 5l5.5 5.5"/>',
    down: '<path d="M12 5v14M6.5 13.5L12 19l5.5-5.5"/>',
    alert: '<path d="M12 4l9 16H3z"/><path d="M12 10v4M12 17v.5"/>',
    trash: '<path d="M4.5 7h15M9.5 7V4.5h5V7M6.5 7l1 12.5h9l1-12.5"/>',
    image: '<rect x="3.5" y="5" width="17" height="14" rx="2"/><circle cx="9" cy="10" r="1.8"/><path d="M20.5 16l-5-5-8.5 8"/>',
    station: '<rect x="3.5" y="8.5" width="17" height="11" rx="2"/><path d="M7 8.5l10-5"/><circle cx="8.5" cy="14" r="2.5"/><path d="M14 12.5h3.5M14 15.5h3.5"/>',
    check: '<path d="M5 12.5l4.5 4.5 9.5-10"/>'
  };
  function icon(name, extra) {
    return '<svg class="icon' + (extra ? ' ' + extra : '') + '" viewBox="0 0 24 24" aria-hidden="true">' +
      ICONS[name].replace(/class="f"/g, 'fill="currentColor" stroke="none"') + '</svg>';
  }

  // Station logos come from URLs at runtime. A missing one, one that fails to load, and one under
  // LOGO_MIN_PX in either side all get the initials tile; its color comes from the station's ID,
  // so it stays the same everywhere and across restarts.
  function logo(st, size) {
    var src = logoOf(st);
    var tile = '<span class="logo logo--initials" style="--size:' + size + 'px;--h:' + hue(st.id) + '" aria-hidden="true">' + esc(initials(nameOf(st))) + '</span>';
    if (!src || src === 'initials' || badLogos.has(src)) return tile;
    return '<span class="logo" style="--size:' + size + 'px"><img src="' + esc(src) + '" alt="" data-logo data-initials="' + esc(initials(nameOf(st))) + '" data-h="' + hue(st.id) + '"></span>';
  }
  function groupLogo(g, size) {
    if (g.logo) return '<span class="logo" style="--size:' + size + 'px"><img src="' + esc(g.logo) + '" alt=""></span>';
    return '<span class="logo logo--initials" style="--size:' + size + 'px;--h:' + hue('group-' + g.id) + '" aria-hidden="true">' + esc(initials(g.name)) + '</span>';
  }
  function trackTile(size) {
    return '<span class="row-kind" style="width:' + size + 'px;height:' + size + 'px" aria-hidden="true">' + icon('recordings') + '</span>';
  }
  function swapToInitials(img) {
    badLogos.add(img.getAttribute('src'));
    var holder = img.parentElement;
    holder.className = 'logo logo--initials';
    holder.style.setProperty('--h', img.dataset.h);
    holder.textContent = img.dataset.initials;
  }

  // ---------- Routing ----------

  // Only plain #name hashes pass through the artifact viewer, so each screen gets one.
  function parseRoute() {
    var hash = location.hash.replace(/^#/, '');
    var m = hash.match(/^(station|group|playlist)-([a-z0-9-]+)$/);
    if (m) return { name: m[1], id: m[2] };
    if (/^(browse|now|groups|playlists|recordings|preferences|preferences-background|preferences-equalizer|preferences-lists)$/.test(hash)) return { name: hash, id: null };
    return { name: 'browse', id: null };
  }
  function section(route) {
    if (route.name === 'station') return 'browse';
    if (route.name === 'group') return 'groups';
    if (route.name === 'playlist') return 'playlists';
    if (route.name.indexOf('preferences') === 0) return 'preferences';
    return route.name;
  }
  function parentOf(route) {
    if (route.name === 'now') return state.layout === 'phone' ? 'browse' : undefined;
    return { station: 'browse', group: 'groups', playlist: 'playlists', 'preferences-background': 'preferences', 'preferences-equalizer': 'preferences', 'preferences-lists': 'preferences' }[route.name];
  }
  function titleOf(route) {
    switch (route.name) {
      case 'browse': return 'Browse';
      case 'now': return 'Now playing';
      case 'groups': return 'Groups';
      case 'playlists': return 'Playlists';
      case 'recordings': return 'Recordings';
      case 'preferences': return 'Settings';
      case 'preferences-background': return 'Background';
      case 'preferences-equalizer': return 'Equalizer';
      case 'preferences-lists': return 'Station lists';
      case 'station': { var st = station(route.id); return st ? nameOf(st) : 'Station'; }
      case 'group': { var g = group(route.id); return g ? g.name : 'Group'; }
      case 'playlist': { var p = playlist(route.id); return p ? p.name : 'Playlist'; }
    }
    return '';
  }

  // ---------- Shell ----------

  var NAV = [
    { route: 'browse', label: 'Browse', icon: 'browse' },
    { route: 'now', label: 'Now playing', icon: 'now', desktopOnly: true },
    { route: 'groups', label: 'Groups', icon: 'groups' },
    { route: 'playlists', label: 'Playlists', icon: 'playlist' },
    { route: 'recordings', label: 'Recordings', icon: 'recordings' },
    { route: 'preferences', label: 'Settings', icon: 'prefs' }
  ];

  function shell() {
    var r = state.route;
    var current = section(r);
    var desktop = state.layout === 'desktop';
    var html = '<div class="backdrop" data-bg="' + state.background + '"' + (state.background === 'image' && state.userImage ? ' style="background-image:url(&quot;' + esc(state.userImage) + '&quot;)"' : '') + '></div>';
    if (desktop) {
      html += '<nav class="side" aria-label="Main">' +
        '<div class="wordmark"><span class="lamp" data-status="' + (state.player.status) + '"></span>neradio</div>' +
        NAV.map(function (n) {
          return '<a class="nav-item" href="#' + n.route + '"' + (current === n.route ? ' aria-current="page"' : '') + '>' + icon(n.icon) + n.label + '</a>';
        }).join('') +
        '<div class="nav-label">Your groups</div>' +
        GROUPS.map(function (g) {
          var here = r.name === 'group' && r.id === g.id;
          return '<a class="nav-item" href="#group-' + g.id + '"' + (here ? ' aria-current="page"' : '') + '>' + groupLogo(g, 24) + esc(g.name) + '</a>';
        }).join('') +
        '<button class="guide-toggle" data-action="guide">Review guide</button>' +
        '</nav>';
    } else {
      var parent = parentOf(r);
      html += '<header class="topbar">' +
        (parent ? '<button class="icon-btn" data-action="back" aria-label="Back">' + icon('back') + '</button>' : '<span class="spacer"></span>') +
        '<h1>' + esc(titleOf(r)) + '</h1>' +
        '<button class="guide-toggle" data-action="guide">Review</button>' +
        '</header>';
    }
    html += '<main class="main" id="main">' + page() + '</main>';
    html += '<section class="player" id="player" aria-label="Player">' + player() + '</section>';
    if (!desktop) {
      html += '<nav class="tabs" aria-label="Main">' + NAV.filter(function (n) { return !n.desktopOnly; }).map(function (n) {
        return '<a class="tab" href="#' + n.route + '"' + (current === n.route ? ' aria-current="page"' : '') + '>' + icon(n.icon) + n.label + '</a>';
      }).join('') + '</nav>';
    }
    if (state.sheet) html += '<div class="sheet-layer" data-action="close-sheet-outside">' + sheet() + '</div>';
    if (state.guideOpen) html += guide();
    if (state.toast) html += '<div class="toast" role="status">' + esc(state.toast) + '</div>';
    return html;
  }

  function page() {
    var r = state.route;
    var view = {
      browse: browseView, now: nowView, station: stationView, groups: groupsView, group: groupView,
      playlists: playlistsView, playlist: playlistView, recordings: recordingsView, preferences: preferencesView,
      'preferences-background': backgroundView, 'preferences-equalizer': equalizerView, 'preferences-lists': listsView
    }[r.name];
    return '<div class="page">' + view(r.id) + '</div>';
  }

  // On a phone the top bar carries the title and the back button; the page keeps its actions.
  // A null title leaves the heading to the page, as the station and group pages do.
  function pageHead(title, actions) {
    var parent = parentOf(state.route);
    var back = state.layout === 'desktop' && parent ? '<a class="back" href="#' + parent + '" data-action="back">' + icon('back') + esc(titleOf({ name: parent })) + '</a>' : '';
    var heading = state.layout === 'desktop' && title ? '<h1>' + esc(title) + '</h1>' : '';
    return back + (heading || actions ? '<div class="page-head">' + heading + (actions || '') + '</div>' : '');
  }

  // ---------- Player ----------

  function playing() {
    var p = state.player;
    if (!p.id) return null;
    return p.kind === 'track' ? track(p.id) : station(p.id);
  }
  function statusText(p) {
    return { playing: 'On air', connecting: 'Connecting…', reconnecting: 'Reconnecting…', stopped: 'Stopped' }[p.status];
  }
  function playButton(item, kind, big) {
    var p = state.player;
    var active = p.kind === kind && p.id === item.id && p.status !== 'stopped';
    var label = (active ? 'Stop ' : 'Play ') + (kind === 'track' ? item.title : nameOf(item));
    return '<button class="play-btn' + (big ? ' play-btn--big' : '') + '" data-action="' + (active ? 'stop' : 'play') + '" data-kind="' + kind + '" data-id="' + item.id + '" aria-label="' + esc(label) + '">' + icon(active ? 'stop' : 'play') + '</button>';
  }
  function favButton(st) {
    var fav = inGroup(group('favorites'), s(st.id));
    return '<button class="icon-btn" data-action="favorite" data-id="' + st.id + '" aria-pressed="' + fav + '" aria-label="' + (fav ? 'Remove from Favorites' : 'Add to Favorites') + '">' + icon('star') + '</button>';
  }
  function recordButton() {
    var p = state.player;
    var can = p.kind === 'station' && (p.status === 'playing' || p.status === 'reconnecting');
    var on = !!state.recording;
    return '<button class="icon-btn icon-btn--rec" data-action="record" aria-pressed="' + on + '"' + (can || on ? '' : ' disabled') + ' aria-label="' + (on ? 'Stop recording' : 'Record this station') + '" title="' + (on ? 'Stop recording' : 'Record the station that is playing') + '">' + icon('record', 'icon--fill') + '</button>';
  }
  function sleepButton() {
    var running = state.sleepEndsAt !== null;
    return running
      ? '<button class="chip timer-chip" data-action="sleep" aria-label="Sleep timer">' + icon('sleep') + '<span data-live="sleep">' + countdown(state.sleepEndsAt - Date.now()) + '</span></button>'
      : '<button class="icon-btn" data-action="sleep" aria-label="Sleep timer" title="Sleep timer">' + icon('sleep') + '</button>';
  }

  function player() {
    var p = state.player;
    var item = playing();
    if (!item) return '<div class="player-inner"><span class="hint">Nothing is playing. Pick a station in Browse.</span></div>';
    var isTrack = p.kind === 'track';
    var name = isTrack ? item.title : nameOf(item);
    var line = isTrack ? 'Recording · from ' + nameOf(station(item.station))
      : state.problem ? state.problem
      : p.status === 'playing' ? (p.title || 'No track titles from this station')
      : statusText(p);
    var art = isTrack ? trackTile(state.layout === 'desktop' ? 48 : 40) : logo(item, state.layout === 'desktop' ? 48 : 40);
    var mini = '<a class="now-mini" href="#now">' + art +
      '<span class="now-mini-text"><span class="now-mini-name"><span class="lamp" data-status="' + p.status + '"></span><span>' + esc(name) + '</span></span>' +
      '<span class="now-mini-title" data-bind="title">' + esc(line) + '</span></span></a>';
    if (state.layout === 'phone') {
      var timer = state.sleepEndsAt !== null ? sleepButton() : '';
      return '<div class="player-inner">' + mini + '<div class="player-controls">' + timer + playButton(item, p.kind) + '</div></div>';
    }
    var readout = isTrack ? duration(item.seconds) : [item.format, item.bitrate ? item.bitrate + ' kbps' : null, item.rate].filter(Boolean).join(' · ');
    return '<div class="player-inner">' + mini +
      '<div class="player-controls">' + (isTrack ? '' : favButton(item)) + playButton(item, p.kind) + (isTrack ? '' : recordButton()) + sleepButton() + '</div>' +
      '<div class="player-side">' + (state.recording ? '<span class="tag tag--warn">' + icon('record', 'icon--fill') + 'Rec <span data-live="rec">' + countdown(Date.now() - state.recording.startedAt) + '</span></span>' : '') +
      '<span class="readout">' + esc(readout) + '</span></div></div>';
  }

  // ---------- Screens ----------

  function stationRow(st) {
    var p = state.player;
    var here = p.kind === 'station' && p.id === st.id && p.status !== 'stopped';
    var tags = st.inCatalog === false && !st.fromUserList ? '<span class="tag tag--warn">Not in the catalog</span>' : '';
    return '<div class="row" data-playing="' + here + '">' + logo(st, 44) +
      '<a class="row-main" href="#station-' + st.id + '"><span class="row-title"><span>' + esc(nameOf(st)) + '</span>' + tags + '</span>' +
      '<span class="row-meta">' + esc(stationMeta(st)) + '</span></a>' +
      '<span class="row-actions">' + favButton(st) + playButton(st, 'station') + '</span></div>';
  }

  function matchingStations() {
    var q = state.query.trim().toLowerCase();
    return STATIONS.filter(function (st) {
      if (state.genre && st.genres.indexOf(state.genre) < 0) return false;
      if (!q) return true;
      var hay = [nameOf(st), st.name, st.country || '', st.genres.join(' ')].join(' ').toLowerCase();
      return hay.indexOf(q) >= 0;
    });
  }
  function results() {
    var list = matchingStations();
    if (!list.length) return '<p class="hint">No station matches “' + esc(state.query) + '”.</p>';
    return '<div class="list">' + list.map(stationRow).join('') + '</div>';
  }

  function browseView() {
    var genres = [];
    STATIONS.forEach(function (st) { st.genres.forEach(function (g) { if (genres.indexOf(g) < 0) genres.push(g); }); });
    genres.sort();
    return pageHead('Browse') +
      '<label class="search">' + icon('search') + '<span class="sr-only">Search stations</span><input id="search" type="search" placeholder="Search stations, genres, countries" value="' + esc(state.query) + '" data-input="search" autocomplete="off"></label>' +
      '<div class="chips" role="group" aria-label="Genre">' +
      '<button class="chip" data-action="genre" data-id="" aria-pressed="' + (state.genre === null) + '">All</button>' +
      genres.map(function (g) { return '<button class="chip" data-action="genre" data-id="' + esc(g) + '" aria-pressed="' + (state.genre === g) + '">' + esc(g) + '</button>'; }).join('') +
      '</div>' +
      '<div id="results">' + results() + '</div>';
  }

  function groupChips(item) {
    var member = groupsWith(item);
    return '<div class="chips">' + member.map(function (g) {
      return '<span class="chip chip--member">' + esc(g.name) +
        '<button class="icon-btn" data-action="ungroup" data-group="' + g.id + '" data-type="' + item.type + '" data-id="' + item.id + '" aria-label="Remove from ' + esc(g.name) + '">' + icon('x') + '</button></span>';
    }).join('') +
      '<button class="chip chip--add" data-action="groups-sheet" data-type="' + item.type + '" data-id="' + item.id + '">' + icon('plus') + (member.length ? 'Add to group' : 'Add to a group') + '</button></div>';
  }

  function facts(st, live) {
    var rows = [
      ['Genre', st.genres.join(', ')],
      ['Country', st.country],
      ['Language', st.language],
      ['Stream', live || (st.format ? [st.format, st.bitrate ? st.bitrate + ' kbps' : null].filter(Boolean).join(' · ') : null)],
      ['Addresses', st.streams > 1 ? st.streams + ' stream addresses: if one fails, the next is tried' : null]
    ].filter(function (row) { return row[1]; });
    if (!rows.length) return '';
    return '<dl class="facts">' + rows.map(function (row) { return '<dt>' + row[0] + '</dt><dd>' + esc(row[1]) + '</dd>'; }).join('') + '</dl>';
  }

  function about(factList) {
    return factList ? '<div class="section"><h2>About</h2>' + factList + '</div>' : '';
  }

  function historyList(st) {
    var items = state.history[st.id];
    if (!items || !items.length) return '';
    return '<div class="section"><div class="section-head"><h2>Heard on this station</h2></div>' +
      '<p class="hint">Titles the station reported while you listened. Past tracks can’t be played again.</p>' +
      '<div class="list history">' + items.slice(0, 8).map(function (h) {
        return '<div class="row"><span class="history-time">' + esc(h.time) + '</span><span class="row-main"><span class="row-title"><span>' + esc(h.title) + '</span></span></span></div>';
      }).join('') + '</div></div>';
  }

  function identityNotes(st) {
    var notes = '';
    if (isRenamed(st)) {
      notes += '<div class="original"><span class="tag tag--accent">Renamed</span><span>Station list name: ' + esc(st.name) + '</span>' +
        '<button class="btn btn--quiet" data-action="reset-name" data-id="' + st.id + '">' + icon('reset') + 'Reset name</button></div>';
    }
    if (hasOwnLogo(st)) {
      notes += '<div class="original"><span class="tag tag--accent">Your logo</span>' +
        '<button class="btn btn--quiet" data-action="reset-logo" data-id="' + st.id + '">' + icon('reset') + 'Reset logo</button></div>';
    }
    if (st.inCatalog === false && !st.fromUserList) {
      notes += '<div class="original"><span class="tag tag--warn">No longer in the catalog</span><span>It stays in your groups.</span></div>';
    }
    if (st.fromUserList) {
      notes += '<div class="original"><span class="tag">From your own list</span></div>';
    }
    return notes;
  }

  function stationView(id) {
    var st = station(id);
    if (!st) return '<p class="hint">This station isn’t in the demo.</p>';
    var p = state.player;
    var here = p.kind === 'station' && p.id === st.id;
    var live = here && p.status !== 'stopped' ? [st.format, st.bitrate + ' kbps', st.rate].join(' · ') : null;
    return pageHead(null) +
      '<div class="station-head">' + logo(st, state.layout === 'desktop' ? 112 : 88) +
      '<div class="station-head-text"><h1>' + esc(nameOf(st)) + '</h1>' +
      '<span class="row-meta">' + esc(stationMeta(st)) + '</span>' + identityNotes(st) +
      '<div class="inline">' + playButton(st, 'station') + favButton(st) +
      '<button class="btn" data-action="edit-station" data-id="' + st.id + '">' + icon('edit') + 'Name and logo</button></div></div></div>' +
      (here && state.problem ? '<div class="banner">' + icon('alert') + '<span>' + esc(state.problem) + '</span></div>' : '') +
      '<div class="section"><div class="section-head"><h2>In your groups</h2></div>' + groupChips(s(st.id)) + '</div>' +
      about(facts(st, live)) +
      historyList(st);
  }

  function nowView() {
    var p = state.player;
    var item = playing();
    if (!item) return pageHead('Now playing') + '<p class="hint">Nothing is playing yet.</p>';
    if (p.kind === 'track') {
      var from = station(item.station);
      return pageHead('Now playing') +
        '<div class="now"><div class="now-art">' + trackTile(state.layout === 'desktop' ? 280 : 200) + '</div>' +
        '<div class="now-info"><div class="now-status"><span class="lamp" data-status="' + p.status + '"></span>' + (p.status === 'stopped' ? 'Stopped' : 'Playing a recording') + '</div>' +
        '<div class="now-name">' + esc(item.title) + '</div><div class="readout">' + duration(item.seconds) + ' · recorded ' + esc(item.date) + ' from ' + esc(nameOf(from)) + '</div>' +
        '<div class="now-controls">' + playButton(item, 'track', true) + sleepButton() + '</div>' +
        '<div class="section"><h2>In your groups</h2>' + groupChips(t(item.id)) + '</div></div></div>';
    }
    var st = item;
    var titleLine = state.problem ? '<div class="banner">' + icon('alert') + '<span>' + esc(state.problem) + '</span></div>'
      : p.status === 'stopped' ? '<div class="now-title hint">Press play to tune in to the live broadcast.</div>'
      : st.noTitles ? '<div class="now-title hint">This station doesn’t send track titles.</div>'
      : '<div class="now-title" data-bind="title">' + esc(p.title || '') + '</div>';
    var readout = [st.format, st.bitrate ? st.bitrate + ' kbps' : null, st.rate].filter(Boolean).join(' · ');
    return pageHead('Now playing') +
      '<div class="now"><div class="now-art">' + logo(st, state.layout === 'desktop' ? 280 : 200) +
      (state.vizOn ? '<canvas class="viz" id="viz" width="560" height="144" aria-hidden="true"></canvas>' : '') + '</div>' +
      '<div class="now-info">' +
      '<div class="now-status"><span class="lamp" data-status="' + p.status + '"></span>' + statusText(p) + (state.recording ? ' · <span class="tag tag--warn">Rec <span data-live="rec">' + countdown(Date.now() - state.recording.startedAt) + '</span></span>' : '') + '</div>' +
      '<div><div class="now-name">' + esc(nameOf(st)) + '</div>' + (isRenamed(st) ? '<div class="hint">Station list name: ' + esc(st.name) + '</div>' : '') + '</div>' +
      titleLine +
      '<div class="readout">' + esc(readout) + '</div>' +
      '<div class="now-controls">' + favButton(st) + playButton(st, 'station', true) + recordButton() + sleepButton() + '</div>' +
      '<div class="section"><h2>In your groups</h2>' + groupChips(s(st.id)) + '</div>' +
      about(facts(st, null)) +
      '<div class="inline"><a class="btn" href="#preferences-equalizer">Equalizer</a><button class="btn" data-action="viz" aria-pressed="' + state.vizOn + '">' + (state.vizOn ? 'Hide visualizer' : 'Show visualizer') + '</button><a class="btn btn--quiet" href="#station-' + st.id + '">Station page' + icon('next') + '</a></div>' +
      historyList(st) + '</div></div>';
  }

  function groupsView() {
    return pageHead('Groups', '<button class="btn" data-action="new-group">' + icon('plus') + 'New group</button>') +
      '<div class="group-grid">' + GROUPS.map(function (g) {
        return '<a class="group-card" href="#group-' + g.id + '">' + groupLogo(g, 48) +
          '<span class="row-title"><span>' + esc(g.name) + '</span>' + (g.builtIn ? '<span class="tag">Built in</span>' : '') + '</span>' +
          '<span class="row-meta">' + itemCounts(g.items) + '</span></a>';
      }).join('') + '</div>';
  }

  function itemRow(item, removeAction, extra) {
    if (item.type === 'station') {
      var st = station(item.id);
      return '<div class="row">' + logo(st, 44) + '<a class="row-main" href="#station-' + st.id + '"><span class="row-title"><span>' + esc(nameOf(st)) + '</span></span><span class="row-meta">Station · ' + esc(stationMeta(st)) + '</span></a>' +
        '<span class="row-actions">' + playButton(st, 'station') + (removeAction || '') + '</span></div>';
    }
    var tr = track(item.id);
    return '<div class="row">' + trackTile(44) + '<span class="row-main"><span class="row-title"><span>' + esc(tr.title) + '</span></span><span class="row-meta">Recording · ' + duration(tr.seconds) + ' · ' + esc(nameOf(station(tr.station))) + ', ' + esc(tr.date) + '</span></span>' +
      '<span class="row-actions">' + (extra || '') + playButton(tr, 'track') + (removeAction || '') + '</span></div>';
  }

  function groupView(id) {
    var g = group(id);
    if (!g) return '<p class="hint">This group was deleted.</p>';
    var head = '<div class="station-head">' + groupLogo(g, 72) + '<div class="station-head-text"><h1>' + esc(g.name) + '</h1>' +
      '<span class="row-meta">' + itemCounts(g.items) + '</span>' + (g.builtIn ? '<div class="original"><span class="tag">Built in</span><span>Favorites can be renamed and given a logo, but not deleted.</span></div>' : '') +
      '<div class="inline"><button class="btn" data-action="edit-group" data-id="' + g.id + '">' + icon('edit') + 'Name and logo</button></div></div></div>';
    var rows = g.items.map(function (item) {
      var remove = '<button class="icon-btn" data-action="ungroup" data-group="' + g.id + '" data-type="' + item.type + '" data-id="' + item.id + '" aria-label="Remove from ' + esc(g.name) + '">' + icon('x') + '</button>';
      return itemRow(item, remove);
    }).join('');
    return pageHead(null) + head +
      (g.items.length ? '<div class="list">' + rows + '</div>' : '<p class="hint">Add stations from their page, and recordings from Recordings.</p>');
  }

  function playlistsView() {
    return pageHead('Playlists', '<button class="btn" data-action="new-playlist">' + icon('plus') + 'New playlist</button>') +
      '<div class="list">' + PLAYLISTS.map(function (pl) {
        return '<div class="row"><span class="row-kind">' + icon('playlist') + '</span><a class="row-main" href="#playlist-' + pl.id + '"><span class="row-title"><span>' + esc(pl.name) + '</span></span>' +
          '<span class="row-meta">' + itemCounts(pl.items) + (pl.loop ? ' · loops' : '') + '</span></a><span class="row-actions"><a class="icon-btn" href="#playlist-' + pl.id + '" aria-label="Edit ' + esc(pl.name) + '">' + icon('next') + '</a></span></div>';
      }).join('') + '</div>';
  }

  // Stations and recordings share one ordered list. Each row says what it is and how the
  // playlist moves on from it.
  function playlistView(id) {
    var pl = playlist(id);
    if (!pl) return '<p class="hint">This playlist isn’t in the demo.</p>';
    var rows = pl.items.map(function (item, index) {
      var art, title, meta, link;
      if (item.type === 'station') {
        var st = station(item.id);
        art = logo(st, 44);
        title = nameOf(st);
        meta = 'Station · ' + (st.noTitles ? 'no titles, so after ' + pl.minutes + ' min' : 'next title, or after ' + pl.minutes + ' min');
        link = '#station-' + st.id;
      } else {
        var tr = track(item.id);
        art = trackTile(44);
        title = tr.title;
        meta = 'Recording · ' + duration(tr.seconds) + ' · plays to the end';
        link = null;
      }
      var main = '<span class="row-title"><span>' + esc(title) + '</span></span><span class="row-meta">' + esc(meta) + '</span>';
      return '<div class="row pl-row"><span class="pl-pos">' + (index + 1) + '</span>' + art +
        (link ? '<a class="row-main" href="' + link + '">' + main + '</a>' : '<span class="row-main">' + main + '</span>') +
        '<span class="row-actions">' +
        '<button class="icon-btn" data-action="move" data-id="' + pl.id + '" data-index="' + index + '" data-step="-1" aria-label="Move up"' + (index === 0 ? ' disabled' : '') + '>' + icon('up') + '</button>' +
        '<button class="icon-btn" data-action="move" data-id="' + pl.id + '" data-index="' + index + '" data-step="1" aria-label="Move down"' + (index === pl.items.length - 1 ? ' disabled' : '') + '>' + icon('down') + '</button>' +
        '<button class="icon-btn" data-action="remove-item" data-id="' + pl.id + '" data-index="' + index + '" aria-label="Remove">' + icon('x') + '</button></span></div>';
    }).join('');
    return pageHead(pl.name, '<button class="btn btn--primary" data-action="play-playlist" data-id="' + pl.id + '">' + icon('play') + 'Play</button>') +
      '<div class="panel"><div class="field"><label for="pl-minutes">Moving on from a station</label>' +
      '<div class="inline"><span>At each new title, or after</span><input id="pl-minutes" type="number" min="1" max="240" value="' + pl.minutes + '" data-input="minutes" data-id="' + pl.id + '"><span>minutes</span></div>' +
      '<span class="hint">A station that sends no titles moves on after the time only. A recording plays to its end.</span></div>' +
      '<label class="switch"><span>Start again from the top after the last item</span><input id="pl-loop" type="checkbox" data-input="loop" data-id="' + pl.id + '"' + (pl.loop ? ' checked' : '') + '></label></div>' +
      '<div class="section"><div class="section-head"><h2>' + pl.items.length + ' items</h2><span class="hint">' + itemCounts(pl.items) + '</span></div>' +
      (pl.items.length ? '<div class="list">' + rows + '</div>' : '<p class="hint">Add stations and recordings, in any mix.</p>') +
      '<div class="inline"><button class="btn" data-action="add-items" data-id="' + pl.id + '">' + icon('plus') + 'Add stations or recordings</button></div></div>';
  }

  function recordingsView() {
    var rec = state.recording;
    var now = rec ? '<div class="banner">' + icon('record', 'icon--fill') + '<span>Recording ' + esc(nameOf(station(rec.stationId))) + ' · <span data-live="rec">' + countdown(Date.now() - rec.startedAt) + '</span> · ' + rec.titles.length + (rec.titles.length === 1 ? ' track' : ' tracks') + ' so far. A new file starts at each new title.</span><button class="btn" data-action="record">Stop</button></div>' : '';
    return pageHead('Recordings') + now +
      '<p class="hint">Recorded from the station that’s playing, split where the title changes.</p>' +
      (TRACKS.length ? '<div class="list">' + TRACKS.map(function (tr) {
        var extra = '<button class="icon-btn" data-action="groups-sheet" data-type="track" data-id="' + tr.id + '" aria-label="Add to a group">' + icon('groups') + '</button>';
        var remove = '<button class="icon-btn" data-action="delete-track" data-id="' + tr.id + '" aria-label="Delete">' + icon('trash') + '</button>';
        return itemRow(t(tr.id), remove, extra);
      }).join('') + '</div>' : '<p class="hint">No recordings yet. Press record while a station plays.</p>');
  }

  function preferencesView() {
    var bg = state.background === 'image' ? 'Your image' : BACKGROUNDS.find(function (b) { return b.id === state.background; }).name;
    var rows = [
      ['preferences-background', 'image', 'Background', bg],
      ['preferences-equalizer', 'prefs', 'Equalizer', state.eqPreset],
      ['preferences-lists', 'station', 'Station lists', 'The catalog, and lists you add']
    ];
    return pageHead('Settings') + '<div class="list">' + rows.map(function (r) {
      return '<div class="row"><span class="row-kind">' + icon(r[1]) + '</span><a class="row-main" href="#' + r[0] + '"><span class="row-title"><span>' + r[2] + '</span></span><span class="row-meta">' + esc(r[3]) + '</span></a><span class="row-actions"><a class="icon-btn" href="#' + r[0] + '" aria-label="' + r[2] + '">' + icon('next') + '</a></span></div>';
    }).join('') + '</div><p class="hint">Light and dark follow the system.</p>';
  }

  function backgroundView() {
    var tiles = BACKGROUNDS.map(function (b) {
      return '<button class="swatch" data-action="background" data-id="' + b.id + '" aria-pressed="' + (state.background === b.id) + '"><span class="backdrop" data-bg="' + b.id + '"></span>' + b.name + '</button>';
    }).join('');
    var own = '<button class="swatch" data-action="pick-image" data-id="background" aria-pressed="' + (state.background === 'image') + '"><span class="backdrop" data-bg="image"' + (state.userImage ? ' style="background-image:url(&quot;' + esc(state.userImage) + '&quot;)"' : '') + '>' + (state.userImage ? '' : '<span class="swatch-icon">' + icon('image') + '</span>') + '</span>' +
      'Your image…</button>';
    return pageHead('Background') +
      '<p class="hint">One background for the whole app. Text and controls sit on a tinted layer, so they stay readable over any picture.</p>' +
      '<div class="swatches">' + tiles + own + '</div>';
  }

  function equalizerView() {
    return pageHead('Equalizer') +
      '<p class="hint">Ten bands, applied to every station while it plays.</p>' +
      '<div class="chips" role="group" aria-label="Preset">' + Object.keys(EQ_PRESETS).concat(state.eqPreset === 'Custom' ? ['Custom'] : []).map(function (name) {
        return '<button class="chip" data-action="eq-preset" data-id="' + name + '" aria-pressed="' + (state.eqPreset === name) + '">' + name + '</button>';
      }).join('') + '</div>' +
      '<div class="panel eq">' + EQ_BANDS.map(function (band, i) {
        return '<label class="readout" for="eq-' + i + '">' + band + ' Hz</label><input id="eq-' + i + '" type="range" min="-12" max="12" step="1" value="' + state.eq[i] + '" data-input="eq" data-index="' + i + '"><span class="readout" data-live="eq-' + i + '">' + (state.eq[i] > 0 ? '+' : '') + state.eq[i] + ' dB</span>';
      }).join('') + '</div>';
  }

  function listsView() {
    return pageHead('Station lists') +
      '<div class="list"><div class="row"><span class="row-kind">' + icon('station') + '</span><span class="row-main"><span class="row-title"><span>Station catalog</span><span class="tag">Built in</span></span><span class="row-meta">Updated today · ' + STATIONS.length + ' stations in this demo</span></span><span></span></div>' +
      '<div class="row"><span class="row-kind">' + icon('station') + '</span><span class="row-main"><span class="row-title"><span>kitchen.json</span></span><span class="row-meta">Your list · 1 station</span></span><span></span></div></div>' +
      '<div class="inline"><button class="btn" data-action="todo" data-text="Adding lists and import and export come with #5.">' + icon('plus') + 'Add a list from a file or a web address</button></div>';
  }

  // ---------- Sheets ----------

  function sheet() {
    var sh = state.sheet;
    var body = { groups: groupsSheet, 'edit-station': editStationSheet, 'edit-group': editGroupSheet, 'new-group': newGroupSheet, 'add-items': addItemsSheet, sleep: sleepSheet }[sh.kind](sh);
    return '<div class="sheet" role="dialog" aria-modal="true">' + body + '</div>';
  }
  function sheetHead(title) {
    return '<div class="sheet-head"><h2>' + esc(title) + '</h2><button class="icon-btn" data-action="close-sheet" aria-label="Close">' + icon('x') + '</button></div>';
  }

  function groupsSheet(sh) {
    var item = { type: sh.type, id: sh.id };
    var name = item.type === 'station' ? nameOf(station(item.id)) : track(item.id).title;
    return sheetHead('Groups for ' + name) +
      '<div>' + GROUPS.map(function (g) {
        var id = 'grp-' + g.id;
        return '<div class="check-row"><input id="' + id + '" type="checkbox" data-input="group-member" data-group="' + g.id + '"' + (inGroup(g, item) ? ' checked' : '') + '>' +
          '<label for="' + id + '">' + groupLogo(g, 28) + '<span>' + esc(g.name) + '</span><span class="row-meta">' + itemCounts(g.items) + '</span></label></div>';
      }).join('') + '</div>' +
      '<div class="field"><label for="new-group-name">New group</label><div class="inline"><input id="new-group-name" type="text" placeholder="Name" style="flex:1;min-width:0" data-input="new-group-name"><button class="btn" data-action="create-group-for">Create and add</button></div></div>' +
      '<div class="sheet-actions"><button class="btn btn--primary" data-action="close-sheet">Done</button></div>';
  }

  function editStationSheet(sh) {
    var st = station(sh.id);
    var draft = sh.draft;
    var preview = { id: st.id, name: draft.name || st.name, logo: st.logo };
    var previewLogo = draft.logo === undefined ? st.logo : draft.logo;
    var tile = previewLogo && previewLogo !== 'initials' && !badLogos.has(previewLogo)
      ? '<span class="logo" style="--size:64px"><img src="' + esc(previewLogo) + '" alt="" data-logo data-initials="' + esc(initials(preview.name)) + '" data-h="' + hue(st.id) + '"></span>'
      : '<span class="logo logo--initials" style="--size:64px;--h:' + hue(st.id) + '">' + esc(initials(preview.name)) + '</span>';
    var changed = draft.name || draft.logo !== undefined;
    return sheetHead('Name and logo') +
      '<div class="field"><label for="st-name">Name</label><input id="st-name" type="text" value="' + esc(draft.name || '') + '" placeholder="' + esc(st.name) + '" data-input="draft-name">' +
      '<span class="hint">Shown everywhere in neradio. The station list keeps “' + esc(st.name) + '”.</span></div>' +
      '<div class="field"><label>Logo</label><div class="inline">' + tile +
      '<button class="btn" data-action="pick-image" data-id="station-logo">' + icon('image') + 'Choose image…</button>' +
      '<button class="btn" data-action="draft-initials">Initials</button>' +
      (st.logo ? '<button class="btn btn--quiet" data-action="draft-catalog-logo">Station’s own logo</button>' : '') + '</div></div>' +
      '<div class="sheet-actions">' + (changed ? '<button class="btn btn--quiet" data-action="draft-reset">' + icon('reset') + 'Reset to the original</button>' : '') +
      '<button class="btn" data-action="close-sheet">Cancel</button><button class="btn btn--primary" data-action="save-station">Save</button></div>';
  }

  function editGroupSheet(sh) {
    var g = group(sh.id);
    return sheetHead('Group name and logo') +
      '<div class="field"><label for="g-name">Name</label><input id="g-name" type="text" value="' + esc(sh.draft.name) + '" data-input="group-draft-name"></div>' +
      '<div class="field"><label>Logo</label><div class="inline">' + groupLogo({ id: g.id, name: sh.draft.name || g.name, logo: sh.draft.logo }, 56) +
      '<button class="btn" data-action="pick-image" data-id="group-logo">' + icon('image') + 'Choose image…</button>' +
      (sh.draft.logo ? '<button class="btn" data-action="group-initials">Initials</button>' : '') + '</div></div>' +
      '<div class="sheet-actions">' + (g.builtIn ? '' : '<button class="btn btn--quiet" data-action="delete-group" data-id="' + g.id + '">' + icon('trash') + 'Delete group</button>') +
      '<button class="btn" data-action="close-sheet">Cancel</button><button class="btn btn--primary" data-action="save-group">Save</button></div>';
  }

  function newGroupSheet() {
    return sheetHead('New group') +
      '<div class="field"><label for="ng-name">Name</label><input id="ng-name" type="text" placeholder="For example: Sunday mornings" data-input="new-group-name"></div>' +
      '<p class="hint">A group holds stations and recordings. Add them from a station’s page or from Recordings.</p>' +
      '<div class="sheet-actions"><button class="btn" data-action="close-sheet">Cancel</button><button class="btn btn--primary" data-action="create-group">Create</button></div>';
  }

  function addItemsSheet(sh) {
    return sheetHead('Add to the playlist') +
      '<div class="segmented" role="group" aria-label="Kind"><button data-action="add-tab" data-id="station" aria-pressed="' + (sh.tab === 'station') + '">Stations</button><button data-action="add-tab" data-id="track" aria-pressed="' + (sh.tab === 'track') + '">Recordings</button></div>' +
      '<label class="search">' + icon('search') + '<span class="sr-only">Search</span><input id="add-search" type="search" placeholder="Search" value="' + esc(sh.query) + '" data-input="add-search" autocomplete="off"></label>' +
      '<div id="sheet-results">' + addResults(sh) + '</div>' +
      '<div class="sheet-actions"><button class="btn btn--primary" data-action="close-sheet">Done</button></div>';
  }
  function addResults(sh) {
    var q = sh.query.trim().toLowerCase();
    var pl = playlist(sh.id);
    var count = function (type, id) { return pl.items.filter(function (i) { return i.type === type && i.id === id; }).length; };
    var rows = sh.tab === 'station'
      ? STATIONS.filter(function (st) { return !st.unsupported && (!q || nameOf(st).toLowerCase().indexOf(q) >= 0); }).map(function (st) {
        var n = count('station', st.id);
        return '<div class="row">' + logo(st, 36) + '<span class="row-main"><span class="row-title"><span>' + esc(nameOf(st)) + '</span></span><span class="row-meta">' + esc(stationMeta(st)) + '</span></span>' +
          '<button class="btn" data-action="add-item" data-type="station" data-id="' + st.id + '">' + (n ? icon('check') + 'Add again' : icon('plus') + 'Add') + '</button></div>';
      })
      : TRACKS.filter(function (tr) { return !q || tr.title.toLowerCase().indexOf(q) >= 0; }).map(function (tr) {
        var n = count('track', tr.id);
        return '<div class="row">' + trackTile(36) + '<span class="row-main"><span class="row-title"><span>' + esc(tr.title) + '</span></span><span class="row-meta">' + duration(tr.seconds) + ' · ' + esc(nameOf(station(tr.station))) + '</span></span>' +
          '<button class="btn" data-action="add-item" data-type="track" data-id="' + tr.id + '">' + (n ? icon('check') + 'Add again' : icon('plus') + 'Add') + '</button></div>';
      });
    return rows.length ? '<div class="list">' + rows.join('') + '</div>' : '<p class="hint">Nothing matches.</p>';
  }

  function sleepSheet() {
    var running = state.sleepEndsAt !== null;
    return sheetHead('Sleep timer') +
      (running ? '<p>Playback stops in <span class="readout" data-live="sleep">' + countdown(state.sleepEndsAt - Date.now()) + '</span>.</p>' : '<p class="hint">Playback stops when the time is up, even while reconnecting.</p>') +
      '<div class="chips">' + [15, 30, 45, 60, 90].map(function (m) { return '<button class="chip" data-action="sleep-set" data-id="' + m + '">' + m + ' min</button>'; }).join('') + '</div>' +
      '<p class="hint">Stopping playback yourself also cancels the timer. Playing another station keeps it.</p>' +
      '<div class="sheet-actions">' + (running ? '<button class="btn" data-action="sleep-cancel">Cancel the timer</button>' : '') + '<button class="btn btn--primary" data-action="close-sheet">Done</button></div>';
  }

  // ---------- Review guide (demo only) ----------

  function guide() {
    var theme = document.documentElement.dataset.theme || 'system';
    return '<aside class="guide" aria-label="Review guide">' +
      '<div class="sheet-head"><h2>Review guide</h2><button class="icon-btn" data-action="guide" aria-label="Close">' + icon('x') + '</button></div>' +
      '<p class="hint">This panel belongs to the demo, not to the app. Nothing plays and the stations are made up.</p>' +
      '<div class="section"><h3>Settled by this demo: Q36</h3><ol>' +
      '<li>Groups on the station page: <a href="#station-harbor">a station in three groups</a>, <a href="#station-news">one in none</a>.</li>' +
      '<li>Renamed stations: <a href="#station-nightowl">a renamed one</a>, <a href="#station-sol">one with your own logo</a>. Lists show only your name.</li>' +
      '<li>Logo fallback, in <a href="#browse">Browse</a>: Baltic News 24 has no logo, Kaktus Radio’s doesn’t load, Tiny Waves’ is 16 px. Under ' + LOGO_MIN_PX + ' px, the initials tile is used.</li>' +
      '<li>Stations and recordings mixed: <a href="#playlist-evening">the Evening playlist</a>, also <a href="#group-road">a group</a>.</li>' +
      '</ol></div>' +
      '<div class="section"><h3>Shown with their proposed defaults</h3><ul>' +
      '<li><span class="guide-q">Q32</span> Station information: <a href="#now">Now playing</a></li>' +
      '<li><span class="guide-q">Q34</span> Background: <a href="#preferences-background">Settings, Background</a> <button class="btn" data-action="bright-photo">Use a bright photo</button></li>' +
      '<li><span class="guide-q">Q13</span> <a href="#station-oldies">A station gone from the catalog</a></li>' +
      '<li><span class="guide-q">Q16</span> A new name applies everywhere</li>' +
      '<li><span class="guide-q">Q18</span> <a href="#group-favorites">Favorites can’t be deleted</a></li>' +
      '<li><span class="guide-q">Q20</span> Playlist settings, in <a href="#playlist-evening">Evening</a></li>' +
      '<li><span class="guide-q">Q33</span> <a href="#preferences-equalizer">Equalizer</a></li>' +
      '</ul></div>' +
      '<div class="section"><h3>States</h3><div class="chips">' +
      '<button class="btn" data-action="demo-connect">Connecting</button>' +
      '<button class="btn" data-action="demo-reconnect">Reconnecting</button>' +
      '<button class="btn" data-action="play" data-kind="station" data-id="talk">No titles</button>' +
      '<button class="btn" data-action="play" data-kind="station" data-id="lounge">Format not supported</button>' +
      '<button class="btn" data-action="demo-title">Next title</button>' +
      '<button class="btn" data-action="sleep-set" data-id="30">Sleep timer</button>' +
      '</div><p class="hint">Also: <a href="#station-valley">a very long name</a>, <a href="#station-bare">only a name and a stream address</a>.</p></div>' +
      '<div class="section"><h3>Look</h3><div class="chips">' +
      (window.innerWidth >= 900 ? '<button class="btn" data-action="frame" aria-pressed="' + state.forcePhone + '">' + (state.forcePhone ? 'Desktop layout' : 'Phone layout') + '</button>' : '') +
      ['system', 'light', 'dark'].map(function (th) { return '<button class="chip" data-action="theme" data-id="' + th + '" aria-pressed="' + (theme === th) + '">' + th.charAt(0).toUpperCase() + th.slice(1) + '</button>'; }).join('') +
      '</div></div>' +
      '<div class="section"><h3>Not decided here</h3><p class="hint">Q12 export contents, Q17 built-in groups besides Favorites, Q23 how much history is kept, Q25–Q27 recording details. Q8, XAML or Blazor Hybrid, is decided from this demo.</p></div>' +
      '</aside>';
  }

  // ---------- Rendering ----------

  function render() {
    var main = document.getElementById('main');
    var scroll = main && app.dataset.route === location.hash ? main.scrollTop : 0;
    var focused = document.activeElement && document.activeElement.id;
    app.dataset.layout = state.layout;
    app.dataset.frame = state.forcePhone && window.innerWidth >= 900 ? 'on' : 'off';
    app.dataset.route = location.hash;
    app.innerHTML = shell();
    var newMain = document.getElementById('main');
    if (newMain) newMain.scrollTop = scroll;
    if (focused) {
      var el = document.getElementById(focused);
      if (el && el.focus) {
        el.focus();
        if (el.setSelectionRange && el.type === 'search') el.setSelectionRange(el.value.length, el.value.length);
      }
    }
    startViz();
  }
  function renderPlayerOnly() {
    var region = document.getElementById('player');
    if (region) region.innerHTML = player();
    document.querySelectorAll('[data-bind="title"]').forEach(function (el) {
      if (!el.closest('#player')) el.textContent = state.player.title || '';
    });
  }
  // Renders everything unless the user is typing, which a full render would interrupt.
  function refresh() {
    var active = document.activeElement;
    if (active && (active.tagName === 'INPUT' && active.type !== 'checkbox' && active.type !== 'range')) renderPlayerOnly();
    else render();
  }
  function updateLayout() {
    var layout = state.forcePhone || window.innerWidth < 900 ? 'phone' : 'desktop';
    var frame = state.forcePhone && window.innerWidth >= 900 ? 'on' : 'off';
    if (layout !== state.layout || frame !== app.dataset.frame) {
      state.layout = layout;
      render();
    }
  }
  function toast(text) {
    state.toast = text;
    clearTimeout(timers.toast);
    timers.toast = setTimeout(function () { state.toast = null; refresh(); }, 2600);
  }

  // The visualizer only runs while it's on screen and a station plays, to save battery.
  var vizFrame = 0;
  function startViz() {
    cancelAnimationFrame(vizFrame);
    var canvas = document.getElementById('viz');
    if (!canvas) return;
    var ctx = canvas.getContext('2d');
    var reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    var bars = 36;
    var levels = new Array(bars).fill(0);
    function draw() {
      if (!document.body.contains(canvas)) return;
      var color = getComputedStyle(app).getPropertyValue('--accent').trim();
      var live = state.player.status === 'playing' && state.player.kind === 'station';
      ctx.clearRect(0, 0, canvas.width, canvas.height);
      ctx.fillStyle = color;
      var w = canvas.width / bars;
      for (var i = 0; i < bars; i++) {
        var target = live ? (0.25 + 0.75 * Math.abs(Math.sin(Date.now() / (260 + i * 37) + i))) * (1 - i / (bars * 1.4)) : 0.04;
        levels[i] += (target - levels[i]) * 0.25;
        var h = Math.max(3, levels[i] * canvas.height * 0.9);
        ctx.fillRect(i * w + 2, canvas.height - h, w - 4, h);
      }
      if (!reduce) vizFrame = requestAnimationFrame(draw);
    }
    draw();
  }

  // ---------- Playback (simulated) ----------

  function play(kind, id) {
    clearTimeout(timers.connect);
    clearTimeout(timers.reconnect);
    stopRecording(false);
    state.problem = null;
    if (kind === 'track') {
      state.player = { kind: 'track', id: id, status: 'playing', title: null, titleIndex: 0, since: Date.now() };
      refresh();
      return;
    }
    var st = station(id);
    state.player = { kind: 'station', id: id, status: 'connecting', title: null, titleIndex: 0, since: Date.now() };
    refresh();
    timers.connect = setTimeout(function () {
      if (st.unsupported) {
        state.player.status = 'stopped';
        state.problem = nameOf(st) + ' streams ' + st.format + ', a format neradio can’t play.';
        toast('This station’s format isn’t supported.');
        refresh();
        return;
      }
      state.player.status = 'playing';
      if (!st.noTitles) setTitle(st, 0);
      refresh();
    }, 1200);
  }
  function stop() {
    clearTimeout(timers.connect);
    clearTimeout(timers.reconnect);
    stopRecording(true);
    state.player.status = 'stopped';
    state.player.title = null;
    if (state.sleepEndsAt !== null) {
      state.sleepEndsAt = null;
      toast('Sleep timer cancelled.');
    }
    refresh();
  }
  function setTitle(st, index) {
    var title = st.titles[index % st.titles.length];
    if (title === state.player.title) return;
    state.player.titleIndex = index;
    state.player.title = title;
    (state.history[st.id] = state.history[st.id] || []).unshift({ time: clock(), title: title });
    if (state.recording) state.recording.titles.push(title);
  }
  function nextTitle() {
    var p = state.player;
    if (p.kind !== 'station' || p.status !== 'playing') return;
    var st = station(p.id);
    if (st.noTitles || !st.titles.length) return;
    setTitle(st, p.titleIndex + 1);
    refresh();
  }
  function stopRecording(announce) {
    var rec = state.recording;
    if (!rec) return;
    state.recording = null;
    rec.titles.forEach(function (title, i) {
      TRACKS.unshift({ id: 'r' + Date.now() + i, title: title, station: rec.stationId, seconds: 60 + Math.floor(Math.random() * 180), date: 'Today' });
    });
    if (announce) toast(rec.titles.length + (rec.titles.length === 1 ? ' track' : ' tracks') + ' saved to Recordings.');
  }

  // ---------- Actions ----------

  var actions = {
    play: function (el) { play(el.dataset.kind, el.dataset.id); },
    stop: function () { stop(); },
    favorite: function (el) {
      var g = group('favorites');
      var item = s(el.dataset.id);
      if (inGroup(g, item)) g.items = g.items.filter(function (i) { return !(i.type === 'station' && i.id === item.id); });
      else g.items.push(item);
      refresh();
    },
    record: function () {
      if (state.recording) { stopRecording(true); refresh(); return; }
      var p = state.player;
      if (p.kind !== 'station' || (p.status !== 'playing' && p.status !== 'reconnecting')) return;
      state.recording = { stationId: p.id, startedAt: Date.now(), titles: p.title ? [p.title] : ['Untitled'] };
      toast('Recording. A new file starts at each new title.');
      refresh();
    },
    sleep: function () { state.sheet = { kind: 'sleep' }; refresh(); },
    'sleep-set': function (el) {
      state.sleepEndsAt = Date.now() + Number(el.dataset.id) * 60000;
      state.sheet = null;
      toast('Playback stops in ' + el.dataset.id + ' minutes.');
      refresh();
    },
    'sleep-cancel': function () { state.sleepEndsAt = null; state.sheet = null; refresh(); },
    genre: function (el) { state.genre = el.dataset.id || null; refresh(); },
    ungroup: function (el) {
      var g = group(el.dataset.group);
      g.items = g.items.filter(function (i) { return !(i.type === el.dataset.type && i.id === el.dataset.id); });
      toast('Removed from ' + g.name + '.');
      refresh();
    },
    'groups-sheet': function (el) { state.sheet = { kind: 'groups', type: el.dataset.type, id: el.dataset.id, newName: '' }; refresh(); },
    'create-group-for': function () {
      var sh = state.sheet;
      var name = (sh.newName || '').trim();
      if (!name) { toast('Type a name for the new group first.'); return; }
      var g = { id: 'g' + Date.now(), name: name, items: [{ type: sh.type, id: sh.id }] };
      GROUPS.push(g);
      sh.newName = '';
      toast('Created ' + name + '.');
      refresh();
    },
    'new-group': function () { state.sheet = { kind: 'new-group', newName: '' }; refresh(); },
    'create-group': function () {
      var name = (state.sheet.newName || '').trim();
      if (!name) { toast('Type a name for the new group first.'); return; }
      var g = { id: 'g' + Date.now(), name: name, items: [] };
      GROUPS.push(g);
      state.sheet = null;
      location.hash = 'group-' + g.id;
    },
    'edit-group': function (el) { var g = group(el.dataset.id); state.sheet = { kind: 'edit-group', id: g.id, draft: { name: g.name, logo: g.logo || null } }; refresh(); },
    'group-initials': function () { state.sheet.draft.logo = null; refresh(); },
    'save-group': function () {
      var sh = state.sheet;
      var g = group(sh.id);
      g.name = sh.draft.name.trim() || g.name;
      g.logo = sh.draft.logo || undefined;
      state.sheet = null;
      refresh();
    },
    'delete-group': function (el) {
      var g = group(el.dataset.id);
      GROUPS.splice(GROUPS.indexOf(g), 1);
      state.sheet = null;
      toast('Deleted ' + g.name + '. Its stations and recordings are still in the app.');
      location.hash = 'groups';
    },
    'edit-station': function (el) {
      var o = state.overrides[el.dataset.id] || {};
      state.sheet = { kind: 'edit-station', id: el.dataset.id, draft: { name: o.name || '', logo: o.logo } };
      refresh();
    },
    'draft-initials': function () { state.sheet.draft.logo = 'initials'; refresh(); },
    'draft-catalog-logo': function () { state.sheet.draft.logo = undefined; refresh(); },
    'draft-reset': function () { state.sheet.draft = { name: '', logo: undefined }; refresh(); },
    'save-station': function () {
      var sh = state.sheet;
      var o = {};
      if (sh.draft.name.trim()) o.name = sh.draft.name.trim();
      if (sh.draft.logo !== undefined) o.logo = sh.draft.logo;
      if (o.name || o.logo !== undefined) state.overrides[sh.id] = o;
      else delete state.overrides[sh.id];
      state.sheet = null;
      refresh();
    },
    'reset-name': function (el) {
      var o = state.overrides[el.dataset.id];
      delete o.name;
      if (o.logo === undefined) delete state.overrides[el.dataset.id];
      toast('The station list’s name is back.');
      refresh();
    },
    'reset-logo': function (el) {
      var o = state.overrides[el.dataset.id];
      delete o.logo;
      if (!o.name) delete state.overrides[el.dataset.id];
      toast('The station’s own logo is back.');
      refresh();
    },
    'new-playlist': function () {
      var pl = { id: 'p' + Date.now(), name: 'New playlist', minutes: 20, loop: true, items: [] };
      PLAYLISTS.push(pl);
      location.hash = 'playlist-' + pl.id;
    },
    'play-playlist': function (el) {
      var pl = playlist(el.dataset.id);
      if (!pl.items.length) { toast('Add something to the playlist first.'); return; }
      var first = pl.items[0];
      play(first.type, first.id);
      toast('Playing ' + pl.name + '.');
    },
    move: function (el) {
      var pl = playlist(el.dataset.id);
      var i = Number(el.dataset.index);
      var j = i + Number(el.dataset.step);
      var item = pl.items.splice(i, 1)[0];
      pl.items.splice(j, 0, item);
      refresh();
    },
    'remove-item': function (el) { playlist(el.dataset.id).items.splice(Number(el.dataset.index), 1); refresh(); },
    'add-items': function (el) { state.sheet = { kind: 'add-items', id: el.dataset.id, tab: 'station', query: '' }; refresh(); },
    'add-tab': function (el) { state.sheet.tab = el.dataset.id; state.sheet.query = ''; refresh(); },
    'add-item': function (el) {
      var pl = playlist(state.sheet.id);
      pl.items.push({ type: el.dataset.type, id: el.dataset.id });
      toast('Added as item ' + pl.items.length + '.');
      refresh();
    },
    'delete-track': function (el) {
      var tr = track(el.dataset.id);
      TRACKS.splice(TRACKS.indexOf(tr), 1);
      GROUPS.forEach(function (g) { g.items = g.items.filter(function (i) { return !(i.type === 'track' && i.id === tr.id); }); });
      PLAYLISTS.forEach(function (pl) { pl.items = pl.items.filter(function (i) { return !(i.type === 'track' && i.id === tr.id); }); });
      if (state.player.kind === 'track' && state.player.id === tr.id) state.player = { kind: 'station', id: null, status: 'stopped' };
      toast('Deleted the recording.');
      refresh();
    },
    background: function (el) { state.background = el.dataset.id; refresh(); },
    'pick-image': function (el) {
      pickFor = el.dataset.id;
      picker.value = '';
      picker.click();
    },
    'bright-photo': function () { state.userImage = BRIGHT_PHOTO; state.background = 'image'; refresh(); },
    'eq-preset': function (el) { if (EQ_PRESETS[el.dataset.id]) { state.eq = EQ_PRESETS[el.dataset.id].slice(); } state.eqPreset = el.dataset.id; refresh(); },
    viz: function () { state.vizOn = !state.vizOn; refresh(); },
    todo: function (el) { toast(el.dataset.text); },
    guide: function () { state.guideOpen = !state.guideOpen; refresh(); },
    frame: function () { state.forcePhone = !state.forcePhone; state.layout = state.forcePhone ? 'phone' : 'desktop'; render(); },
    theme: function (el) {
      if (el.dataset.id === 'system') delete document.documentElement.dataset.theme;
      else document.documentElement.dataset.theme = el.dataset.id;
      refresh();
    },
    'demo-connect': function () { var p = state.player; play('station', p.kind === 'station' && p.id ? p.id : 'harbor'); },
    'demo-reconnect': function () {
      var p = state.player;
      if (p.kind !== 'station' || p.status !== 'playing') { toast('Play a station first.'); return; }
      p.status = 'reconnecting';
      refresh();
      timers.reconnect = setTimeout(function () { p.status = 'playing'; refresh(); }, 4000);
    },
    'demo-title': function () { nextTitle(); },
    back: function (el, e) {
      e.preventDefault();
      if (state.navigations > 0) history.back();
      else location.hash = parentOf(state.route) || 'browse';
    },
    'close-sheet': function () { state.sheet = null; refresh(); },
    'close-sheet-outside': function (el, e) { if (e.target === el) { state.sheet = null; refresh(); } }
  };

  app.addEventListener('click', function (e) {
    var el = e.target.closest('[data-action]');
    if (!el || !app.contains(el)) return;
    var handler = actions[el.dataset.action];
    if (handler) handler(el, e);
  });

  app.addEventListener('input', function (e) {
    var el = e.target;
    switch (el.dataset.input) {
      case 'search':
        state.query = el.value;
        document.getElementById('results').innerHTML = results();
        break;
      case 'add-search':
        state.sheet.query = el.value;
        document.getElementById('sheet-results').innerHTML = addResults(state.sheet);
        break;
      case 'new-group-name': state.sheet.newName = el.value; break;
      case 'draft-name': state.sheet.draft.name = el.value; break;
      case 'group-draft-name': state.sheet.draft.name = el.value; break;
      case 'minutes': {
        var minutes = Math.max(1, Math.min(240, Number(el.value) || 1));
        playlist(el.dataset.id).minutes = minutes;
        break;
      }
      case 'eq': {
        var i = Number(el.dataset.index);
        state.eq[i] = Number(el.value);
        state.eqPreset = 'Custom';
        var label = document.querySelector('[data-live="eq-' + i + '"]');
        if (label) label.textContent = (state.eq[i] > 0 ? '+' : '') + state.eq[i] + ' dB';
        break;
      }
    }
  });

  app.addEventListener('change', function (e) {
    var el = e.target;
    switch (el.dataset.input) {
      case 'loop': playlist(el.dataset.id).loop = el.checked; break;
      case 'minutes': render(); break;
      case 'eq': render(); break;
      case 'group-member': {
        var sh = state.sheet;
        var g = group(el.dataset.group);
        var item = { type: sh.type, id: sh.id };
        if (el.checked && !inGroup(g, item)) g.items.push(item);
        if (!el.checked) g.items = g.items.filter(function (i) { return !(i.type === item.type && i.id === item.id); });
        render();
        break;
      }
    }
  });

  picker.addEventListener('change', function () {
    var file = picker.files && picker.files[0];
    if (!file) return;
    var url = URL.createObjectURL(file);
    if (pickFor === 'background') {
      state.userImage = url;
      state.background = 'image';
    } else if (state.sheet && state.sheet.draft) {
      state.sheet.draft.logo = url;
    }
    render();
  });

  // Load and error don't bubble, so they're caught on the way down.
  app.addEventListener('error', function (e) {
    if (e.target.tagName === 'IMG' && e.target.hasAttribute('data-logo')) swapToInitials(e.target);
  }, true);
  app.addEventListener('load', function (e) {
    var img = e.target;
    if (img.tagName === 'IMG' && img.hasAttribute('data-logo') && (img.naturalWidth < LOGO_MIN_PX || img.naturalHeight < LOGO_MIN_PX)) swapToInitials(img);
  }, true);

  document.addEventListener('keydown', function (e) {
    if (e.key !== 'Escape') return;
    if (state.sheet) { state.sheet = null; refresh(); } else if (state.guideOpen) { state.guideOpen = false; refresh(); }
  });

  window.addEventListener('hashchange', function () {
    state.route = parseRoute();
    state.navigations++;
    state.sheet = null;
    if (state.layout === 'phone') state.guideOpen = false;
    render();
    var main = document.getElementById('main');
    if (main) main.scrollTop = 0;
  });
  window.addEventListener('resize', updateLayout);

  // One clock for the sleep timer, the recording time and the simulated title changes.
  setInterval(function () {
    if (state.sleepEndsAt !== null && Date.now() >= state.sleepEndsAt) {
      state.sleepEndsAt = null;
      stop();
      toast('The sleep timer stopped playback.');
      return;
    }
    if (state.sleepEndsAt !== null) document.querySelectorAll('[data-live="sleep"]').forEach(function (el) { el.textContent = countdown(state.sleepEndsAt - Date.now()); });
    if (state.recording) document.querySelectorAll('[data-live="rec"]').forEach(function (el) { el.textContent = countdown(Date.now() - state.recording.startedAt); });
    var p = state.player;
    if (p.kind === 'station' && p.status === 'playing' && Date.now() - p.since > TITLE_EVERY_MS) {
      p.since = Date.now();
      nextTitle();
    }
  }, 1000);

  state.route = parseRoute();
  state.layout = window.innerWidth < 900 ? 'phone' : 'desktop';
  render();
})();
