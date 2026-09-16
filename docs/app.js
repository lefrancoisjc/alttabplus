const copy = {
  en: {
    navFeatures: "Features",
    eyebrow: "Windows 10 / 11",
    heroTitle: "Alt+Tab that treats a Snap Group as one thing.",
    heroLede: "Native Alt+Tab lists the group and every window inside it. AltTabPlus collapses the layout into a single floating picker — the same idea as the Windows switcher, without the clutter.",
    download: "Download for Windows",
    source: "Source",
    expand: "Expand · Space or ↓",
    windows2: "2 windows",
    search: "Search",
    shotSwitcher: "Alt+Tab on the desktop",
    shotNative: "Windows Alt+Tab",
    shotOurs: "AltTabPlus",
    nativeNote: "The snap shows up twice: once as a group, once as each window.",
    oursNote: "The snapped pair is one tile. Expand it only if you want a single window.",
    shotSettings: "Settings window",
    settingsTitle: "Settings",
    settingsSub: "Switcher, hot corners, and startup",
    rowAlt: "Replace Alt+Tab",
    rowHidden: "Hidden windows",
    rowCorners: "Hot corners",
    f1t: "One tile per group",
    f1d: "Snapped windows stay a layout. Release Alt to bring the whole group forward without breaking the snap.",
    f2t: "Expand when you need one",
    f2d: "Click, Space, or ↓ opens the group. Pick a single window. Esc goes back.",
    f3t: "Type to filter",
    f3d: "Start typing. Delete or middle-click closes a window. Alt+` cycles the same app.",
    f4t: "Hot corners",
    f4d: "macOS-style corners: Task View, this switcher, desktop, next or previous virtual desktop. Off during exclusive fullscreen games.",
    f5t: "Your scope",
    f5d: "Current monitor only, hidden windows, other desktops, ignore list. Settings stay in LocalAppData.",
    f6t: "English or French",
    f6d: "The app follows the Windows display language. No account, no telemetry, no network.",
    license: "PolyForm Noncommercial — personal use is fine. Commercial use needs permission from Jean-Charles Lefrançois.",
  },
  fr: {
    navFeatures: "Fonctions",
    eyebrow: "Windows 10 / 11",
    heroTitle: "Un Alt+Tab qui traite un Snap Group comme une seule chose.",
    heroLede: "L’Alt+Tab natif liste le groupe et chaque fenêtre. AltTabPlus replie le layout en un sélecteur flottant — le même geste que Windows, sans le bazar.",
    download: "Télécharger pour Windows",
    source: "Code",
    expand: "Déplier · Espace ou ↓",
    windows2: "2 fenêtres",
    search: "Rechercher",
    shotSwitcher: "Alt+Tab sur le bureau",
    shotNative: "Alt+Tab Windows",
    shotOurs: "AltTabPlus",
    nativeNote: "Le snap apparaît deux fois : le groupe, puis chaque fenêtre.",
    oursNote: "Le duo ancré est une tuile. Déplie seulement si tu veux une fenêtre seule.",
    shotSettings: "Fenêtre des réglages",
    settingsTitle: "Réglages",
    settingsSub: "Switcher, coins chauds et démarrage",
    rowAlt: "Remplacer Alt+Tab",
    rowHidden: "Fenêtres cachées",
    rowCorners: "Coins chauds",
    f1t: "Une tuile par groupe",
    f1d: "Les fenêtres ancrées restent un layout. Relâche Alt pour ramener tout le groupe, sans casser le snap.",
    f2t: "Déplier au besoin",
    f2d: "Clic, Espace ou ↓ ouvre le groupe. Choisis une fenêtre. Échap pour revenir.",
    f3t: "Filtrer en tapant",
    f3d: "Tape quelques lettres. Suppr ou clic milieu ferme une fenêtre. Alt+` cycle la même app.",
    f4t: "Coins chauds",
    f4d: "Comme sur macOS : Vue des tâches, ce switcher, bureau, bureau suivant ou précédent. Coupés pendant un jeu plein écran exclusif.",
    f5t: "Ta portée",
    f5d: "Moniteur actuel, fenêtres cachées, autres bureaux, liste d’ignorés. Réglages dans LocalAppData.",
    f6t: "Anglais ou français",
    f6d: "L’app suit la langue d’affichage de Windows. Pas de compte, pas de télémétrie, pas de réseau.",
    license: "PolyForm Noncommercial — l’usage perso est libre. Un usage commercial demande l’accord de Jean-Charles Lefrançois.",
  },
};

const button = document.getElementById("lang");

function apply(lang) {
  const table = copy[lang];
  document.documentElement.lang = lang;
  document.querySelectorAll("[data-i18n]").forEach((node) => {
    const key = node.getAttribute("data-i18n");
    if (table[key]) node.textContent = table[key];
  });
  button.textContent = lang === "en" ? "FR" : "EN";
  localStorage.setItem("lang", lang);
}

const start = localStorage.getItem("lang")
  || (navigator.language.startsWith("fr") ? "fr" : "en");
apply(start);
button.addEventListener("click", () => {
  apply(document.documentElement.lang === "en" ? "fr" : "en");
});
