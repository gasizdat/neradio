# Concept demo

A clickable mock of neradio's screens, for review in #10. Open `index.html` in a browser: there's
nothing to build or install, and it works offline.

- The window's width picks the layout: the phone layout under 900 px, the desktop layout above.
- **Review guide** lists what to look at. It also switches to the phone layout on a wide screen,
  changes the theme, and shows states such as reconnecting or a format that can't be played.
- The stations are made up and nothing plays. `concept.js` holds the data and the behavior,
  `concept.css` the look.
