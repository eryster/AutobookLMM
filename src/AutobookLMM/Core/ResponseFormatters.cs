namespace AutobookLMM.Core;

/// <summary>
/// Pre-built response extraction scripts for advanced usage.
/// </summary>
public static class ResponseFormatters
{
    /// <summary>
    /// Default extraction script that removes noise and extracts clean text.
    /// </summary>
    public const string Default = @"el => {
        const root = el.querySelector('[id^=""model-response-message""]') || el;
        const NOISE = new Set(['source-footnote','sup','sources-carousel-inline','button','mat-icon','script','style', 'code-block-decoration', 'suggested-change-actions']);

        function walk(node) {
            if (!node) return '';
            if (node.nodeType === 3) return node.textContent;
            if (node.nodeType !== 1) return '';

            const tag = node.tagName.toLowerCase();
            const className = node.className || '';
            if (NOISE.has(tag) || (typeof className === 'string' && className.includes('suggested-change-actions'))) return '';

            if (tag === 'suggested-change' || className.includes('suggested-change')) {
                const fileEl = node.querySelector('.file-name, [class*=""file-path""]');
                const filePath = fileEl ? fileEl.innerText.trim() : 'unknown_file';
                const codeEl = node.querySelector('.new-code, code, pre');
                const content = codeEl ? codeEl.innerText.trim() : '';
                return `\n--- SUGGESTED CHANGE ---\nFile: ${filePath}\n${content}\n------------------------\n`;
            }

            if (tag === 'p') return '\n' + Array.from(node.childNodes).map(walk).join('') + '\n';
            if (tag === 'br') return '\n';
            if (/^h[1-6]$/.test(tag)) return '\n' + Array.from(node.childNodes).map(walk).join('') + '\n';

            return Array.from(node.childNodes).map(walk).join('');
        }

        return walk(root).trim();
    }";

    /// <summary>
    /// Script to extract ONLY code blocks from the Gemini response.
    /// </summary>
    public const string CodeBlocksOnly = @"el => {
        const preElements = el.querySelectorAll('pre, code');
        let codeContent = '';
        preElements.forEach(item => {
            codeContent += item.innerText + '\n';
        });
        return codeContent.trim();
    }";

    /// <summary>
    /// Script to extract the raw text content with zero filtering.
    /// </summary>
    public const string Raw = @"el => el.innerText";
}
