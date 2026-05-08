// ═══════════════════════════════════════════════════════════════════════
//  Markdown formatter for the speech bubble.
//  - renderMarkdown(text)   → safe HTML (escapes first, then applies patterns)
//  - plainText(text)        → human-readable text for TTS (no '**', '###', etc.)
//
//  Goals:
//   * The bot's reply looks neat in the UI (paragraphs, bold, lists, code).
//   * The TTS engine doesn't speak asterisks, hashes, or backticks.
//   * No external dependency; small enough to inline; XSS-safe by escaping
//     all HTML before pattern matching.
// ═══════════════════════════════════════════════════════════════════════

function escapeHtml(s) {
    return s
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

function renderInline(s) {
    // Inline code first so its content isn't re-formatted.
    s = s.replace(/`([^`\n]+)`/g, (_, c) => `<code>${c}</code>`);
    // Bold then italic.
    s = s.replace(/\*\*([^*\n]+?)\*\*/g, '<strong>$1</strong>');
    s = s.replace(/__([^_\n]+?)__/g, '<strong>$1</strong>');
    s = s.replace(/(^|[\s(])\*([^*\n]+?)\*(?=[\s).,!?]|$)/g, '$1<em>$2</em>');
    s = s.replace(/(^|[\s(])_([^_\n]+?)_(?=[\s).,!?]|$)/g, '$1<em>$2</em>');
    // Markdown links [text](url) — only http(s)/mailto allowed.
    s = s.replace(/\[([^\]]+)\]\((https?:\/\/[^\s)]+|mailto:[^\s)]+)\)/g,
        (_, text, href) => `<a href="${href}" target="_blank" rel="noopener noreferrer">${text}</a>`);
    return s;
}

export function renderMarkdown(text) {
    if (!text) return '';
    const escaped = escapeHtml(text);

    // Pull out fenced code blocks first; replace with placeholders.
    const codeBlocks = [];
    let body = escaped.replace(/```([a-z0-9_-]*)\n?([\s\S]*?)```/gi, (_, lang, code) => {
        const idx = codeBlocks.length;
        codeBlocks.push({ lang: (lang || '').toLowerCase(), code: code.replace(/\n+$/, '') });
        return `\u0000CB${idx}\u0000`;
    });

    // Block-by-block rendering.
    const blocks = body.split(/\n{2,}/);
    const out = [];
    for (let block of blocks) {
        block = block.replace(/^\s+|\s+$/g, '');
        if (!block) continue;

        // Code-block placeholder (alone in a block).
        const cb = block.match(/^\u0000CB(\d+)\u0000$/);
        if (cb) {
            const { lang, code } = codeBlocks[Number(cb[1])];
            out.push(`<pre class="md-code"${lang ? ` data-lang="${lang}"` : ''}><code>${code}</code></pre>`);
            continue;
        }

        // Heading
        const h = block.match(/^(#{1,3})\s+(.+)$/);
        if (h) {
            const lvl = h[1].length;
            out.push(`<h${lvl + 2} class="md-h${lvl}">${renderInline(h[2])}</h${lvl + 2}>`);
            continue;
        }

        // Unordered list
        if (/^([-*+])\s+/.test(block)) {
            const items = block.split(/\n/).map(l => l.replace(/^([-*+])\s+/, '')).filter(Boolean);
            out.push('<ul class="md-list">' + items.map(i => `<li>${renderInline(i)}</li>`).join('') + '</ul>');
            continue;
        }

        // Ordered list
        if (/^\d+\.\s+/.test(block)) {
            const items = block.split(/\n/).map(l => l.replace(/^\d+\.\s+/, '')).filter(Boolean);
            out.push('<ol class="md-list">' + items.map(i => `<li>${renderInline(i)}</li>`).join('') + '</ol>');
            continue;
        }

        // Blockquote
        if (/^&gt;\s+/.test(block)) {
            const inner = block.split(/\n/).map(l => l.replace(/^&gt;\s?/, '')).join(' ');
            out.push(`<blockquote class="md-quote">${renderInline(inner)}</blockquote>`);
            continue;
        }

        // Paragraph: keep single newlines as <br>.
        const paragraph = renderInline(block.replace(/\n/g, '<br>'));
        out.push(`<p class="md-p">${paragraph}</p>`);
    }

    return out.join('');
}

/**
 * Convert markdown-flavored text to clean plain text suitable for the TTS
 * engine. Removes punctuation that would otherwise be spoken aloud
 * (asterisks, backticks, headings markers, list bullets) but preserves
 * sentence structure and natural pauses.
 */
export function plainText(text) {
    if (!text) return '';
    let s = text;
    // Drop fenced code blocks entirely (the bot will summarize them in voice
    // when the user asks; speaking source code is meaningless on a phone).
    s = s.replace(/```[\s\S]*?```/g, ' ');
    // Inline code → just the content
    s = s.replace(/`([^`\n]+)`/g, '$1');
    // Bold/italic markers
    s = s.replace(/\*\*([^*\n]+?)\*\*/g, '$1')
         .replace(/__([^_\n]+?)__/g, '$1')
         .replace(/\*([^*\n]+?)\*/g, '$1')
         .replace(/_([^_\n]+?)_/g, '$1');
    // Headings → plain line
    s = s.replace(/^#{1,6}\s+/gm, '');
    // Markdown links [text](url) → text
    s = s.replace(/\[([^\]]+)\]\([^)]+\)/g, '$1');
    // List bullets at line starts
    s = s.replace(/^\s*[-*+]\s+/gm, '');
    s = s.replace(/^\s*\d+\.\s+/gm, '');
    // Blockquote markers
    s = s.replace(/^\s*>\s?/gm, '');
    // Collapse repeated whitespace but preserve paragraph breaks.
    s = s.replace(/[ \t]+/g, ' ').replace(/\n{3,}/g, '\n\n').trim();
    return s;
}
