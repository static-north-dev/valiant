import {parse, postprocess, preprocess} from 'micromark';

export function transformMarkdown(markdown, resolveLink, headingOffset = 0) {
  const events = postprocess(parse().document().write(preprocess()(markdown, undefined, true)));
  const edits = [];
  let headingStart = -1;
  for (const [phase, token] of events) {
    if (phase !== 'enter') continue;
    const start = token.start.offset;
    const end = token.end.offset;
    if (token.type === 'resourceDestinationString' || token.type === 'definitionDestinationString') {
      const target = markdown.slice(start, end);
      const parts = /^([^?#]+\.md)([?#].*)?$/i.exec(target);
      if (parts && !/^(?:[a-z][a-z\d+.-]*:|\/)/i.test(target)) {
        const url = resolveLink(decodeURIComponent(parts[1]), parts[2] ?? '');
        edits.push({start, end, replacement: url});
      }
    } else if (headingOffset && token.type === 'atxHeading') {
      headingStart = start;
    } else if (headingOffset && token.type === 'atxHeadingSequence' && start === headingStart) {
      edits.push({start, end, replacement: '#'.repeat(Math.min(6, end - start + headingOffset))});
    }
  }

  let result = '';
  let position = 0;
  for (const {start, end, replacement} of edits) {
    result += markdown.slice(position, start) + replacement;
    position = end;
  }
  return result + markdown.slice(position);
}
