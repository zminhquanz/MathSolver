// Licensed artwork stays local. The public manifest records identities, never SVG geometry.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import { svgContours } from './svg-contours.mjs';
import { vectorFont } from './vector-font.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const manifest = JSON.parse(fs.readFileSync(path.join(root, 'icons8.json'), 'utf8'));
const directory = path.join(root, 'MathSolver/Resources/Icons/Icons8');
const records = manifest.icons.map(icon => {
    const svg = fs.readFileSync(path.join(directory, icon.file), 'utf8');
    return { ...icon, code: parseInt(icon.codepoint, 16), contours: svgContours(svg),
        sha256: createHash('sha256').update(svg).digest('hex') };
});
fs.writeFileSync(path.join(root, 'MathSolver/Resources/Fonts/Icons8Actions.ttf'), vectorFont(records));
const paths = records.map(icon => {
    // Use the same normalized contours as the font so header and button artwork agree.
    const data = icon.contours.map(p => 'M' + p.map(v => v.map(n => +n.toFixed(5)).join(',')).join('L') + 'Z').join(' ');
    return `        paths[${JSON.stringify(icon.key)}] = ${JSON.stringify(data)};`;
});
fs.writeFileSync(path.join(root, 'MathSolver/Services/Appearance/Icons8LicensedPaths.g.cs'),
    '// Generated local licensed artwork. Do not commit or redistribute as GPL source.\n' +
    'namespace MathSolver.Services;\ninternal static partial class Icons8LicensedPaths\n{\n' +
    '    static partial void AddPaths(System.Collections.Generic.Dictionary<string, string> paths)\n    {\n' + paths.join('\n') + '\n    }\n}\n');
fs.writeFileSync(path.join(directory, 'provenance.local.json'), JSON.stringify({ pack: manifest.pack,
    license: manifest.license, icons: records.map(({ key, id, file, sha256 }) => ({ key, id, file, sha256 })) }, null, 2) + '\n');
console.log(`Generated ${records.length} licensed glyphs and header paths.`);
