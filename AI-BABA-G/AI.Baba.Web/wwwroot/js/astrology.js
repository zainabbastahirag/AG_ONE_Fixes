// ═══════════════════════════════════════════════════════════════════════
//  Astrology Mode — pure data + Wikipedia fetches.
//
//  Everything here is deterministic given the inputs (so the same birthday
//  and date yield the same lucky color/number/mood every time the page is
//  opened). The Ollama narrative is layered on top in workspaces.js.
// ═══════════════════════════════════════════════════════════════════════

export const SIGNS = [
    { name: 'Capricorn',   emoji: '\u2651', element: 'Earth', modality: 'Cardinal',  ruler: 'Saturn',  from: [12, 22], to: [1, 19],  traits: 'disciplined, ambitious, grounded',     spirit: 'Mountain Goat',  color: '#1f2937', stones: ['Garnet', 'Onyx'],     mantra: 'I climb steadily and the summit is mine.' },
    { name: 'Aquarius',    emoji: '\u2652', element: 'Air',   modality: 'Fixed',     ruler: 'Uranus',  from: [1, 20],  to: [2, 18],  traits: 'inventive, independent, humanitarian', spirit: 'Wild Crane',     color: '#06b6d4', stones: ['Amethyst', 'Aquamarine'], mantra: 'I am the future, dreaming forward.' },
    { name: 'Pisces',      emoji: '\u2653', element: 'Water', modality: 'Mutable',   ruler: 'Neptune', from: [2, 19],  to: [3, 20],  traits: 'imaginative, gentle, intuitive',       spirit: 'Dolphin',        color: '#6366f1', stones: ['Moonstone', 'Aquamarine'], mantra: 'My intuition is a quiet ocean.' },
    { name: 'Aries',       emoji: '\u2648', element: 'Fire',  modality: 'Cardinal',  ruler: 'Mars',    from: [3, 21],  to: [4, 19],  traits: 'bold, energetic, pioneering',          spirit: 'Ram',            color: '#ef4444', stones: ['Bloodstone', 'Diamond'], mantra: 'I begin, and the world responds.' },
    { name: 'Taurus',      emoji: '\u2649', element: 'Earth', modality: 'Fixed',     ruler: 'Venus',   from: [4, 20],  to: [5, 20],  traits: 'patient, reliable, sensual',           spirit: 'Bull',           color: '#22c55e', stones: ['Emerald', 'Rose Quartz'], mantra: 'I plant and I shall harvest.' },
    { name: 'Gemini',      emoji: '\u264A', element: 'Air',   modality: 'Mutable',   ruler: 'Mercury', from: [5, 21],  to: [6, 20],  traits: 'curious, witty, adaptable',            spirit: 'Hummingbird',    color: '#eab308', stones: ['Citrine', 'Agate'],  mantra: 'I learn, I share, I delight.' },
    { name: 'Cancer',      emoji: '\u264B', element: 'Water', modality: 'Cardinal',  ruler: 'Moon',    from: [6, 21],  to: [7, 22],  traits: 'caring, intuitive, devoted',           spirit: 'Crab',           color: '#94a3b8', stones: ['Moonstone', 'Pearl'], mantra: 'I love deeply and protect gently.' },
    { name: 'Leo',         emoji: '\u264C', element: 'Fire',  modality: 'Fixed',     ruler: 'Sun',     from: [7, 23],  to: [8, 22],  traits: 'warm, generous, charismatic',          spirit: 'Lion',           color: '#f59e0b', stones: ['Tiger\u2019s Eye', 'Sunstone'], mantra: 'I shine, and others bloom in my warmth.' },
    { name: 'Virgo',       emoji: '\u264D', element: 'Earth', modality: 'Mutable',   ruler: 'Mercury', from: [8, 23],  to: [9, 22],  traits: 'analytical, helpful, refined',         spirit: 'Owl',            color: '#84cc16', stones: ['Sapphire', 'Carnelian'], mantra: 'My care lives in the details.' },
    { name: 'Libra',       emoji: '\u264E', element: 'Air',   modality: 'Cardinal',  ruler: 'Venus',   from: [9, 23],  to: [10, 22], traits: 'fair, sociable, diplomatic',           spirit: 'Swan',           color: '#ec4899', stones: ['Opal', 'Lapis Lazuli'], mantra: 'I balance, and the room finds peace.' },
    { name: 'Scorpio',     emoji: '\u264F', element: 'Water', modality: 'Fixed',     ruler: 'Pluto',   from: [10, 23], to: [11, 21], traits: 'passionate, intense, perceptive',      spirit: 'Phoenix',        color: '#7c3aed', stones: ['Topaz', 'Obsidian'], mantra: 'I transform what I touch.' },
    { name: 'Sagittarius', emoji: '\u2650', element: 'Fire',  modality: 'Mutable',   ruler: 'Jupiter', from: [11, 22], to: [12, 21], traits: 'optimistic, adventurous, philosophical', spirit: 'Stag',         color: '#a855f7', stones: ['Turquoise', 'Amethyst'], mantra: 'I aim for the horizon and I trust the road.' },
];

const CHINESE = [
    'Monkey', 'Rooster', 'Dog', 'Pig',
    'Rat', 'Ox', 'Tiger', 'Rabbit',
    'Dragon', 'Snake', 'Horse', 'Goat'
];
const CHINESE_EMOJI = {
    Rat: '\uD83D\uDC00', Ox: '\uD83D\uDC02', Tiger: '\uD83D\uDC05', Rabbit: '\uD83D\uDC07',
    Dragon: '\uD83D\uDC09', Snake: '\uD83D\uDC0D', Horse: '\uD83D\uDC0E', Goat: '\uD83D\uDC10',
    Monkey: '\uD83D\uDC12', Rooster: '\uD83D\uDC13', Dog: '\uD83D\uDC15', Pig: '\uD83D\uDC16',
};
const CHINESE_TRAITS = {
    Rat: 'quick-witted and resourceful',
    Ox: 'steady and dependable',
    Tiger: 'brave and magnetic',
    Rabbit: 'gentle and elegant',
    Dragon: 'ambitious and lucky',
    Snake: 'wise and graceful',
    Horse: 'free-spirited and energetic',
    Goat: 'creative and kind',
    Monkey: 'clever and playful',
    Rooster: 'observant and brave',
    Dog: 'loyal and honest',
    Pig: 'generous and warm-hearted',
};

export function westernSign(month, day) {
    for (const s of SIGNS) {
        const [fm, fd] = s.from, [tm, td] = s.to;
        if (fm > tm) {
            if ((month === fm && day >= fd) || (month === tm && day <= td) ||
                (month > fm) || (month < tm)) return s;
        } else {
            if ((month === fm && day >= fd) || (month === tm && day <= td) ||
                (month > fm && month < tm)) return s;
        }
    }
    return SIGNS[0];
}

export function chineseSign(year) {
    const animal = CHINESE[year % 12];
    return { animal, emoji: CHINESE_EMOJI[animal] || '', traits: CHINESE_TRAITS[animal] || '' };
}

// Tiny deterministic hash → 32-bit int.
function hash(str) {
    let h = 2166136261 >>> 0;
    for (let i = 0; i < str.length; i++) {
        h ^= str.charCodeAt(i);
        h = (h + ((h << 1) + (h << 4) + (h << 7) + (h << 8) + (h << 24))) >>> 0;
    }
    return h >>> 0;
}
function rand(seed, lo, hi) { return lo + (hash(seed) % (hi - lo + 1)); }

const LUCKY_NUMBERS_BY_SIGN = {
    Aries: [1, 9], Taurus: [2, 6], Gemini: [3, 5], Cancer: [2, 7], Leo: [1, 5], Virgo: [5, 6],
    Libra: [4, 6], Scorpio: [3, 9], Sagittarius: [3, 7], Capricorn: [4, 8], Aquarius: [4, 7], Pisces: [3, 7],
};
const ELEMENT_PALETTES = {
    Fire:  ['#ef4444', '#f59e0b', '#dc2626', '#fb923c'],
    Earth: ['#22c55e', '#84cc16', '#65a30d', '#a3a3a3'],
    Air:   ['#06b6d4', '#0ea5e9', '#a78bfa', '#e0f2fe'],
    Water: ['#6366f1', '#0891b2', '#7c3aed', '#94a3b8'],
};
const COLOR_NAMES = {
    '#ef4444': 'Crimson', '#f59e0b': 'Amber', '#dc2626': 'Ember Red', '#fb923c': 'Sunset',
    '#22c55e': 'Emerald', '#84cc16': 'Forest Green', '#65a30d': 'Olive', '#a3a3a3': 'River Stone',
    '#06b6d4': 'Cyan', '#0ea5e9': 'Sky', '#a78bfa': 'Lavender', '#e0f2fe': 'Cloud',
    '#6366f1': 'Indigo', '#0891b2': 'Deep Lagoon', '#7c3aed': 'Violet Flame', '#94a3b8': 'Moon Mist',
};

const DAYS_OF_WEEK = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

export function buildBirthChart({ name, month, day, year }) {
    const sign = westernSign(month, day);
    const chinese = chineseSign(year);
    const seedBase = `${sign.name}-${month}-${day}-${year}`;
    const palette = ELEMENT_PALETTES[sign.element];
    const luckyColor = palette[hash(seedBase) % palette.length];
    const lucky = LUCKY_NUMBERS_BY_SIGN[sign.name] || [3, 7];
    const luckyNum = lucky[hash(seedBase + 'n') % lucky.length];
    const luckyDay = DAYS_OF_WEEK[hash(seedBase + 'd') % DAYS_OF_WEEK.length];
    return {
        name: name || '',
        sign, chinese,
        ruler: sign.ruler,
        modality: sign.modality,
        spirit: sign.spirit,
        luckyColor, luckyColorName: COLOR_NAMES[luckyColor] || luckyColor,
        luckyNumber: luckyNum,
        luckyDay,
        crystal: sign.stones[hash(seedBase + 'c') % sign.stones.length],
        mantra: sign.mantra,
    };
}

/** Mood gauges (Energy/Love/Money/Health/Magic) for a given (sign, dateKey). */
export function moodGauges(signName, dateKey) {
    const dims = ['Energy', 'Love', 'Money', 'Health', 'Magic'];
    return dims.map(d => ({
        name: d,
        value: 30 + (hash(`${signName}-${dateKey}-${d}`) % 70),  // 30..99
    }));
}

export function dateKey(d) {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/** Wikipedia: famous births on the same MM-DD with thumbnail. */
export async function fetchFamousBirthdayMatch(month, day, max = 3) {
    const mm = String(month).padStart(2, '0');
    const dd = String(day).padStart(2, '0');
    const url = `https://en.wikipedia.org/api/rest_v1/feed/onthisday/births/${mm}/${dd}`;
    const res = await fetch(url, { headers: { 'Api-User-Agent': 'AI-BABA-G/1.0' } });
    if (!res.ok) throw new Error(`Wikipedia returned ${res.status}`);
    const data = await res.json();
    const out = [];
    for (const ev of (data.births || [])) {
        const page = (ev.pages || []).find(p => p.thumbnail);
        if (!page) continue;
        out.push({
            year: ev.year,
            text: ev.text,
            title: page.titles?.normalized || (page.title || '').replace(/_/g, ' '),
            extract: page.extract || '',
            thumb: page.thumbnail.source,
            photo: page.originalimage?.source || page.thumbnail.source,
            url: page.content_urls?.desktop?.page || `https://en.wikipedia.org/wiki/${page.title}`,
        });
        if (out.length >= max) break;
    }
    return out;
}

/** Wikipedia: notable historical events on the same MM-DD. Used for "fun facts". */
export async function fetchFunFacts(month, day, max = 4) {
    const mm = String(month).padStart(2, '0');
    const dd = String(day).padStart(2, '0');
    const url = `https://en.wikipedia.org/api/rest_v1/feed/onthisday/events/${mm}/${dd}`;
    const res = await fetch(url, { headers: { 'Api-User-Agent': 'AI-BABA-G/1.0' } });
    if (!res.ok) throw new Error(`Wikipedia returned ${res.status}`);
    const data = await res.json();
    const out = [];
    for (const ev of (data.events || [])) {
        out.push({ year: ev.year, text: ev.text, page: ev.pages?.[0] || null });
        if (out.length >= max) break;
    }
    return out;
}

/** Wikipedia: notable people who died on the same MM-DD with thumbnails. */
export async function fetchDeaths(month, day, max = 3) {
    const mm = String(month).padStart(2, '0');
    const dd = String(day).padStart(2, '0');
    const url = `https://en.wikipedia.org/api/rest_v1/feed/onthisday/deaths/${mm}/${dd}`;
    const res = await fetch(url, { headers: { 'Api-User-Agent': 'AI-BABA-G/1.0' } });
    if (!res.ok) throw new Error(`Wikipedia returned ${res.status}`);
    const data = await res.json();
    const out = [];
    for (const ev of (data.deaths || [])) {
        const page = (ev.pages || []).find(p => p.thumbnail);
        if (!page) continue;
        out.push({
            year: ev.year,
            text: ev.text,
            title: page.titles?.normalized || (page.title || '').replace(/_/g, ' '),
            extract: page.extract || '',
            thumb: page.thumbnail.source,
            url: page.content_urls?.desktop?.page || `https://en.wikipedia.org/wiki/${page.title}`,
        });
        if (out.length >= max) break;
    }
    return out;
}

/** Wikipedia summary endpoint — returns title + thumbnail + extract for any
 *  page. CORS-enabled. Used to fetch images for the spirit animal and lucky
 *  food cards so the astrology dashboard is genuinely visual. */
export async function fetchWikiSummary(title) {
    if (!title) return null;
    try {
        const url = `https://en.wikipedia.org/api/rest_v1/page/summary/${encodeURIComponent(title)}`;
        const res = await fetch(url, { headers: { 'Api-User-Agent': 'AI-BABA-G/1.0' } });
        if (!res.ok) return null;
        const data = await res.json();
        return {
            title: data.title || title,
            extract: data.extract || '',
            thumb: data.thumbnail?.source || null,
            photo: data.originalimage?.source || data.thumbnail?.source || null,
            url: data.content_urls?.desktop?.page || `https://en.wikipedia.org/wiki/${title.replace(/\s+/g, '_')}`,
        };
    } catch (_) { return null; }
}

/** Each sign maps to a spirit animal (with a Wikipedia article title) and a
 *  lucky food. Both are fetched live for their picture. */
export const SPIRIT_ANIMAL_WIKI = {
    Aries: 'Mouflon', Taurus: 'Bull', Gemini: 'Hummingbird', Cancer: 'Crab',
    Leo: 'Lion', Virgo: 'Owl', Libra: 'Mute_swan', Scorpio: 'Phoenix_(mythology)',
    Sagittarius: 'Red_deer', Capricorn: 'Mountain_goat', Aquarius: 'Sandhill_crane', Pisces: 'Common_bottlenose_dolphin',
};
export const LUCKY_FOOD = {
    Aries:       { name: 'Chili pepper',  wiki: 'Chili_pepper',  why: 'fiery, bold, ignites courage' },
    Taurus:      { name: 'Chocolate',     wiki: 'Chocolate',     why: 'sensual, grounding, slow indulgence' },
    Gemini:      { name: 'Coffee',        wiki: 'Coffee',        why: 'sparks the mind, fuels conversation' },
    Cancer:      { name: 'Cucumber',      wiki: 'Cucumber',      why: 'cooling, watery, soothing to feelings' },
    Leo:         { name: 'Mango',         wiki: 'Mango',         why: 'sun-ripened, golden, regal' },
    Virgo:       { name: 'Almonds',       wiki: 'Almond',        why: 'precise nutrition, clean fuel' },
    Libra:       { name: 'Strawberries',  wiki: 'Strawberry',    why: 'sweet, balanced, beautiful to share' },
    Scorpio:     { name: 'Dark chocolate',wiki: 'Dark_chocolate',why: 'intense, transformative, deep' },
    Sagittarius: { name: 'Avocado',       wiki: 'Avocado',       why: 'travels well, generous, abundant' },
    Capricorn:   { name: 'Pomegranate',   wiki: 'Pomegranate',   why: 'patient harvest, ancient strength' },
    Aquarius:    { name: 'Blueberries',   wiki: 'Blueberry',     why: 'small revolutions of antioxidants' },
    Pisces:      { name: 'Salmon',        wiki: 'Salmon',        why: 'swims home, intuitive nourishment' },
};

/** Build the prompt that asks Ollama for a sectioned, TTS-friendly reading. */
export function buildAdvicePrompt({ chart, famous }) {
    const { name, sign, chinese } = chart;
    const youAre = name || 'the seeker';
    const famousLine = famous?.length ? `A famous figure born on the same day is ${famous[0].title} (${famous[0].year}).` : '';
    return [
        `You are AI Baba-G as the Astrologer Sage.`,
        `${youAre} is a ${sign.name} (${sign.element} \u00B7 ${sign.modality} \u00B7 ruler ${sign.ruler}). Chinese animal: ${chinese.animal}.`,
        famousLine,
        `Write a warm, professional astrological report. Use these EXACT section headers (each on its own line, in this order). Each section is 2 to 3 short sentences. Plain prose, no bullet points, no markdown. Address ${youAre} directly.`,
        ``,
        `## TODAY`,
        `## NEXT_3_DAYS`,
        `## NEXT_MONTH`,
        `## YEAR_AHEAD`,
        `## CAREER`,
        `## LOVE`,
        `## FAMILY`,
        `## HEALTH`,
        `## MAGIC`,
        ``,
        `Sound like a wise elder. Never invent quotes from real people. End TODAY with a gentle blessing.`
    ].filter(Boolean).join('\n');
}

/** Parse the streaming Ollama markdown into named sections.
 *  Tolerant of leading newlines, headers with or without trailing newline,
 *  and partial output (returns whatever has streamed so far). */
export function parseSections(full) {
    const out = {};
    if (!full) return out;
    const re = /^##\s*([A-Z0-9_]+)\s*$/gm;
    const matches = [];
    let m; while ((m = re.exec(full)) !== null) matches.push({ key: m[1].toLowerCase(), idx: m.index, end: re.lastIndex });
    for (let i = 0; i < matches.length; i++) {
        const start = matches[i].end;
        const stop = i + 1 < matches.length ? matches[i + 1].idx : full.length;
        out[matches[i].key] = full.slice(start, stop).trim();
    }
    return out;
}
