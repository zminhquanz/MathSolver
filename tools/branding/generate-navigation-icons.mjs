// One geometry source for the SVG originals and MAUI's native vector glyphs.
// Run: node tools/branding/generate-navigation-icons.mjs
// No third-party packages; all contours contain on-curve points only.
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const output = path.join(root, 'MathSolver/Resources/Icons/Navigation');
const fontPath = path.join(root, 'MathSolver/Resources/Fonts/MathNavigationIcons.ttf');
const icons = [];
let contours;
const area = p => p.reduce((sum, a, i) => { const b = p[(i + 1) % p.length]; return sum + a[0]*b[1]-b[0]*a[1]; }, 0);
function polygon(points, hole = false) {
  points = points.map(p => p.map(v => Math.round(v * 1000) / 1000));
  if ((area(points) > 0) === hole) points.reverse();
  contours.push(points);
}
function disc(x, y, radius, hole = false) {
  polygon(Array.from({length:32}, (_, i) => [x+radius*Math.cos(i*Math.PI/16), y+radius*Math.sin(i*Math.PI/16)]), hole);
}
function line(x1, y1, x2, y2, weight = 1.75) {
  const angle = Math.atan2(y2-y1, x2-x1), dx = Math.sin(angle)*weight/2, dy = Math.cos(angle)*weight/2;
  polygon([[x1+dx,y1-dy],[x2+dx,y2-dy],[x2-dx,y2+dy],[x1-dx,y1+dy]]);
  disc(x1,y1,weight/2); disc(x2,y2,weight/2);
}
function stroke(points, weight = 1.75, closed = false) {
  for (let i=1;i<points.length;i++) line(...points[i-1],...points[i],weight);
  if(closed) line(...points.at(-1),...points[0],weight);
}
function ring(x,y,r,weight = 1.75) { disc(x,y,r);disc(x,y,r-weight,true); }
function icon(name, draw) { contours=[];draw();icons.push({name,code:0xe900+icons.length,contours}); }
const cross=(x,y,size=5)=>{line(x,y,x+size,y+size);line(x+size,y,x,y+size);};
const two=(x,y,scale=1)=>stroke([[0,1],[.9,0],[2.7,0],[3.6,1],[3.6,2],[0,5],[3.6,5]].map(([a,b])=>[x+a*scale,y+b*scale]),1.65);
icon('arithmetic',()=>{
  stroke([[5,3],[19,3],[20.5,4.5],[20.5,19.5],[19,21],[5,21],[3.5,19.5],[3.5,4.5]],1.75,true);
  line(7,7,17,7);line(8,11,8,15);line(6,13,10,13);line(14,13,18,13);line(7,18,9,18);line(15,18,17,18);
});
icon('number_theory',()=>{
  stroke([[3,8],[5,6],[5,18]],1.65);two(9,7,1.8);
  stroke([[18,6],[21,6],[19,10],[21,12],[21,16],[20,18],[17,18]],1.65);
});
icon('average',()=>{line(5,5,19,5);cross(6,9,12);});
icon('power_root',()=>{
  cross(4,4,6);two(14,2,.8);
  stroke([[3,16],[6,15],[9,21],[12,11],[21,11]]);line(15,16,20,16);
});
icon('fraction',()=>{stroke([[10,3.5],[12,2.5],[12,8]]);line(5,11.5,19,11.5);two(9.8,14.5,1.3);});
icon('unknown',()=>{
  cross(3,9,6);line(12,10,16,10);line(12,14,16,14);
  stroke([[18,6],[19,5],[21,5],[22,6],[22,8],[20,10],[20,13]],1.7);disc(20,17,1);
});
icon('equation',()=>{
  stroke([[3,3],[3,21],[21,21]]);line(6,17,20,5,1.6);
  stroke([[6,7],[7,10],[8,13],[9,15],[10,16.2],[11,17],[12,17.2],[13,17],[14,16.2],[15,15],[16,13],[17,10],[18,7]],1.7);
});
icon('geometry',()=>{stroke([[3,20],[10,3],[17,20]],1.75,true);ring(17,8,4.25);});
icon('matrix',()=>{
  stroke([[6,3],[3,3],[3,21],[6,21]]);stroke([[18,3],[21,3],[21,21],[18,21]]);
  for(const y of [6,12,18]) for(const x of [9,15]) disc(x,y,1.25);
});
icon('proportion',()=>{
  stroke([[3,4],[3,21],[21,21]]);line(6,18,20,4);
  stroke([[6,4],[7,8],[9,12],[12,15],[16,17],[20,18]],1.75);
});
icon('motion',()=>{
  ring(9,9,6);stroke([[9,5],[9,9],[12,10]],1.7);
  line(4,19,21,19);stroke([[17,15],[21,19],[17,23]],1.75);
});
icon('units',()=>{
  line(3,7,21,7);stroke([[17,3],[21,7],[17,11]]);
  line(3,17,21,17);stroke([[7,13],[3,17],[7,21]]);
});

// Minimal, deterministic TrueType writer. The font is only a transport for our
// SVG outlines: native FontImageSource can tint every glyph to exact TextColor.
const u16 = n => {const b=Buffer.alloc(2);b.writeUInt16BE(n&65535);return b;};
const i16 = n => {const b=Buffer.alloc(2);b.writeInt16BE(n);return b;};
const u32 = n => {const b=Buffer.alloc(4);b.writeUInt32BE(n>>>0);return b;};
const cat = (...b) => Buffer.concat(b.flat());
const pad = b => cat(b,Buffer.alloc((4-b.length%4)%4));
const checksum = b => {b=pad(b);let n=0;for(let i=0;i<b.length;i+=4)n=(n+b.readUInt32BE(i))>>>0;return n;};
const glyphs = [[] , ...icons.map(i=>i.contours.map(p=>p.map(([x,y])=>[Math.round(x*1024/24),Math.round((24-y)*1024/24)])))];
const glyphData=glyphs.map(contours=>{
  if(!contours.length) return Buffer.alloc(0);
  const points=contours.flat(),xs=points.map(p=>p[0]),ys=points.map(p=>p[1]);
  let end=-1,px=0,py=0;
  const dx=points.map(p=>{const d=p[0]-px;px=p[0];return i16(d);});
  const dy=points.map(p=>{const d=p[1]-py;py=p[1];return i16(d);});
  return pad(cat(i16(contours.length),i16(Math.min(...xs)),i16(Math.min(...ys)),i16(Math.max(...xs)),i16(Math.max(...ys)),contours.map(p=>u16(end+=p.length)),u16(0),Buffer.alloc(points.length,1),dx,dy));
});
let offset=0;
const loca=cat(...glyphData.map(g=>{const n=u32(offset);offset+=g.length;return n;}),u32(offset));
const glyphCount=glyphs.length;
const head=cat(u32(0x10000),u32(0x10000),u32(0),u32(0x5f0f3cf5),u16(3),u16(1024),Buffer.alloc(16),i16(0),i16(0),i16(1024),i16(1024),u16(0),u16(8),i16(2),i16(1),i16(0));
const hhea=cat(u32(0x10000),i16(1024),i16(0),i16(0),u16(1024),i16(0),i16(0),i16(1024),i16(1),i16(0),i16(0),Buffer.alloc(8),i16(0),u16(glyphCount));
const maxp=cat(u32(0x10000),u16(glyphCount),u16(Math.max(...glyphs.map(g=>g.flat().length))),u16(Math.max(...glyphs.map(g=>g.length))),u16(0),u16(0),u16(2),Buffer.alloc(16));
const os2=Buffer.alloc(96);os2.writeUInt16BE(4,0);os2.writeInt16BE(1024,2);os2.writeUInt16BE(400,4);os2.writeUInt16BE(5,6);
os2.writeUInt32BE(0x10000000,46);os2.write('MSOL',58,'ascii');os2.writeUInt16BE(0x40,62);os2.writeUInt16BE(icons[0].code,64);os2.writeUInt16BE(icons.at(-1).code,66);
os2.writeInt16BE(1024,68);os2.writeUInt16BE(1024,74);os2.writeUInt16BE(3,94);
const segCount=2, first=icons[0].code, last=icons.at(-1).code;
const format4=cat(u16(4),u16(32),u16(0),u16(segCount*2),u16(4),u16(1),u16(0),u16(last),u16(65535),u16(0),u16(first),u16(65535),u16(1-first),u16(1),u16(0),u16(0));
const cmap=cat(u16(0),u16(1),u16(3),u16(1),u32(12),format4);
const names=['Math Solver Navigation','Regular','MathSolverNavigation-1.0','Math Solver Navigation','Version 1.0','MathSolverNavigation'];
const nameIds=[1,2,3,4,5,6];let nameOffset=0;
const nameStrings=names.map(s=>{const b=Buffer.from(s,'utf16le');b.swap16();return b;});
const name=cat(u16(0),u16(names.length),u16(6+12*names.length),nameStrings.map((s,i)=>{const r=cat(u16(3),u16(1),u16(0x409),u16(nameIds[i]),u16(s.length),u16(nameOffset));nameOffset+=s.length;return r;}),nameStrings);
const post=cat(u32(0x30000),u32(0),i16(-100),i16(50),u32(0),Buffer.alloc(16));
const tables=Object.entries({head,hhea,maxp,'OS/2':os2,cmap,glyf:cat(glyphData),loca,hmtx:cat(glyphs.map(g=>cat(u16(1024),i16(g.length ? Math.min(...g.flat().map(p=>p[0])) : 0)))),name,post}).sort(([a],[b])=>a<b?-1:a>b?1:0);
const power=2**Math.floor(Math.log2(tables.length));offset=12+16*tables.length;
const records=tables.map(([tag,data])=>{const r={tag,data,offset};offset+=pad(data).length;return r;});
const font=cat(u32(0x10000),u16(tables.length),u16(power*16),u16(Math.log2(power)),u16(tables.length*16-power*16),records.map(r=>cat(Buffer.from(r.tag),u32(checksum(r.data)),u32(r.offset),u32(r.data.length))),records.map(r=>pad(r.data)));
font.writeUInt32BE((0xb1b0afba-checksum(font))>>>0,records.find(r=>r.tag==='head').offset+8);
await fs.mkdir(output,{recursive:true});
for(const icon of icons) {
  const d=icon.contours.map(p=>'M'+p.map(v=>v.join(' ')).join('L')+'Z').join('');
  await fs.writeFile(path.join(output,icon.name+'.svg'),`<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24"><path fill="currentColor" d="${d}"/></svg>\n`);
}
await fs.writeFile(fontPath,font);
console.log(`Generated ${icons.length} SVG icons and ${font.length}-byte vector font.`);
